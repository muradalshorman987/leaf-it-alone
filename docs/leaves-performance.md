# Leaves: performance notes (WP2)

Target from the plan: **30,000 leaves at 60 fps on a mid PC** (M2 exit criteria).
The numbers in the budget table are estimates until someone profiles on murad's PC; fill in the "Measured" column then.

## How a frame works

| When | What | Where it runs |
| --- | --- | --- |
| `LeafField.Update` (execution order -100) | Finish last frame's simulation jobs | Main thread (normally already done) |
| Tools' `Update` | `Push` / `BlowCone` / `Attract` / `CollectInRadius`, each a small Burst job run on the spot | Main thread, Burst |
| `WindSystem.Update` | Advance breeze/gust timeline, `LeafField.SetWind`, raise `GameEvents.WindGust` on a new gust | Main thread |
| `LeafField.LateUpdate` 1 | `LeafCullJob`: per render slice, frustum + distance cull, near/far LOD split, write compacted matrices | Burst, parallel (one slice per job index) |
| `LeafField.LateUpdate` 2 | `Graphics.RenderMeshInstanced` per slice: near list (full mesh, optional shadows), far list (LOD quad, no shadows) | Main thread |
| `LeafField.LateUpdate` 3 | Schedule `LeafSimulateJob` (parallel, 256 leaves per batch) then `LeafGridJob` (single job: counting sort + pile heights) | Worker threads, overlapping with rendering |

Every public `LeafField` method calls `CompleteJobs()` first, so callers never see half-simulated data.

## Data layout

- One `LeafState` (about 60 bytes) per leaf in a persistent `NativeArray`. 30k leaves = ~1.8 MB.
- Leaves are sorted at spawn by (type, material variant, Z-order cell). A render slice is up to 1023 consecutive leaves of one bucket, so each draw call is one material and the matrix range is contiguous.
- Grid: `cellCount`, `cellStart` (prefix sums), `cellLeaves` (leaf indices sorted by cell), `cellHeight` (smoothed pile height). A 30 x 30 m yard at 0.4 m cells is 5,625 cells.
- Ground height is baked at the grid's cell corners at spawn (terrain can't be sampled off the main thread), then sampled bilinearly in the jobs.
- Two matrix buffers (near and far), one `Matrix4x4` per leaf each: 30k leaves = ~3.8 MB.

## Budget at 30k leaves (estimates)

| Item | Estimate | Measured |
| --- | --- | --- |
| `LeafSimulateJob` (all workers) | 0.2 to 0.4 ms total CPU | |
| `LeafGridJob` (one worker) | 0.2 to 0.3 ms | |
| `LeafCullJob` (all workers) | 0.2 to 0.3 ms total CPU | |
| Main thread wait in `LateUpdate` (cull) | < 0.3 ms | |
| Draw calls | ~30 to 70 (slices x near/far, minus empty) | |
| Tool jobs (`Push`, `BlowCone`) | < 0.05 ms each | |
| GPU: visible leaves x tris | ~15k visible x 8 to 20 tris, far ones 4 tris | |

## How to profile (on the PC)

1. Build the test yard (*Fallen Yard > Build Test Yard*), set `LeafField.leafCount` to 30000, press Play.
2. *Window > Analysis > Profiler*, CPU module, Timeline view. Look for `LeafSimulateJob`, `LeafGridJob` and `LeafCullJob` on the worker threads and `LeafField.LateUpdate` on the main thread.
3. Check *Jobs > Burst > Enable Compilation* is on and *Safety Checks* is off for representative numbers (safety checks roughly double job time in the editor).
4. Rendering: *Window > Analysis > Frame Debugger*, count the `RenderMeshInstanced` draws; or the Rendering profiler module for batches and triangles.
5. For a fair comparison profile a **Development Build**, not the editor.
6. Record results in the budget table above, with the PC's CPU/GPU, and compare against a capture of the old main-thread version (commit `cba915c`) with *Profile Analyzer* if you want a before/after.

## Knobs if it's slow

| Symptom | Try |
| --- | --- |
| Main thread waits in `LeafField.Update` | The simulation didn't finish during rendering: lower `leafCount` or raise `cellSize` (fewer cells for `LeafGridJob`). |
| Many draw calls | Fewer material variants per type (each variant is a bucket). |
| GPU bound | Lower `lodDistance` (more leaves use the 4-tri quad), lower `cullDistance`, keep `castShadows` off. |
| Leaves vanish in the Scene view during Play | Expected: they're culled to the game camera. Untick `frustumCulling` while debugging. |

## Ideas not done yet

- Split `LeafGridJob`'s counting pass into a parallel job if it shows up (it's sequential because of the prefix sum).
- Move culling to the GPU (`RenderMeshIndirect` + a compute shader) if we go past ~100k leaves. Needs a custom URP shader that reads a `StructuredBuffer`, so it waits for WP10's shader work.
- Per-instance colour tint via a custom shader would let all variants of a type share one draw call.
- `PileDetector.Scan` still runs on the main thread every 0.4 s (about 5k cells, cheap); could become a Burst job.
