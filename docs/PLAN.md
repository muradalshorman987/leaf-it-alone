# Fallen Yard: Project Plan

Fallen Yard is an original, cozy, first-person, low-poly leaf-cleanup game for Unity 6 (URP).
It lives in the "tidy a yard" genre, but its name, code, art, yards, characters and progression are our own.
We don't reuse names, assets, level layouts, UI or music from any existing game.

This document is the source of truth for **what we build, in what order, and who (which agent/thread) owns what**.

---

## 1. Where we are today

The scaffold on `main` (`baa1cf8`) already has a playable core loop, built by the "Build Unity leaf cleanup game" thread:

| Area | State |
| --- | --- |
| First-person controller, dual input (new + legacy) | Done, needs feel tuning |
| `LeafField`: ~12k instanced leaves, grid queries, stacking, slumping | Done, main-thread only |
| `PileDetector`: flood-fill pile detection | Done |
| Tools: Rake, Leaf Blower, Bag | Done, placeholder visuals |
| Upgrades, coins, yard clean %, completion bonus | Done (data-driven `ToolUpgradeDefinition`) |
| Atomic JSON save | Done (coins, tool levels, finished yards) |
| UI | IMGUI debug HUD + shop only |
| Editor: `Fallen Yard > Build Test Yard` | Generates one test scene |
| Art, audio, menus, multiple yards, tests, CI | **Not started** |

Pinned editor: `6000.0.47f1` (any Unity 6 LTS works). Packages: URP 17, Input System 1.11, uGUI.

---

## 2. Game pillars (keep every decision pointed at these)

1. **Satisfying physical leaves.** Raking and blowing should feel tactile: leaves pile, slump, flutter and scatter.
2. **Cozy, not stressful.** No fail states. Gentle music, warm palettes, soft goals.
3. **Visible progress.** Each yard visibly transforms (grass greens up, flowers appear, the house lights come on).
4. **Small, honest scope.** 5 yards, 4 tools, ~2 hours of play for v1.0.

### Our own identity (what makes this not a copy)

- **Setting:** the town of *Hollow Creek* over one autumn. Each yard belongs to a neighbour with a short note on the mailbox.
- **Signature mechanic:** *Compost Bin*. Bagged leaves go to a compost bin; full bins turn into fertilizer that "restores" the yard (grass/flower regrowth shader), which is the completion reward instead of just coins.
- **Wind gusts:** periodic weather events re-scatter un-bagged piles, so you choose when to bag.
- **Leaf types:** maple (light, flies far), oak (normal), wet clumps (heavy, need the rake), pine needles (fine, need the blower or vacuum).
- **Tools v1.0:** Rake, Leaf Blower, Leaf Vacuum (sucks into a hopper you empty), Mulcher (turns piles into instant compost).

---

## 3. Milestones

| Milestone | Goal | Exit criteria |
| --- | --- | --- |
| **M0 Foundations** (week 1) | Everyone can open, build and test the project | Opens clean in Unity 6; CI compiles + runs EditMode tests; MCP for Unity connected on murad's PC |
| **M1 Vertical slice** (weeks 2–4) | One fully dressed yard that's fun for 15 minutes | Real HUD + shop (UI Toolkit), Blender leaf/tool/house assets, rake/blower audio, wind gusts, compost bin, title menu, settings, save/load |
| **M2 Content** (weeks 5–8) | 5 yards, 4 tools, progression curve | Yard select map, vacuum + mulcher, leaf types, seasonal palettes, neighbour notes, 30k leaves at 60 fps on a mid PC |
| **M3 Polish** (weeks 9–10) | Feels finished | VFX, music, juice (camera shake, haptics), tutorial, accessibility options, controller support |
| **M4 Release** (weeks 11–12) | Shippable build | Windows build from CI, itch.io page, bug bash, perf pass, store screenshots |

---

## 4. Work packages (parallel threads)

Each package is sized for one agent thread. Packages own **separate folders** so threads rarely touch the same files.
Shared files (`GameManager.cs`, `SaveData.cs`, `Packages/manifest.json`, `ProjectSettings/`) change only through small, separate PRs, and the owner listed below reviews them.

**Rules for every thread**
- Branch per package (`wp/<id>-<name>`), one PR per meaningful step, PRs into `main`.
- Code in `Assets/Scripts/Runtime/<Area>/` with namespace `FallenYard.<Area>`; tests in `Assets/Tests/EditMode|PlayMode/<Area>/`.
- Talk to other systems through the events/interfaces in section 5, not by reaching into another package's classes.
- Unity can't run in the cloud container, so cloud threads write code + EditMode tests; murad (or a Claude session on murad's PC with MCP for Unity) verifies in the editor.

### Wave 1: start now (no dependencies between them)

| ID | Package | Owns | Deliverables | Needs murad's PC? |
| --- | --- | --- | --- | --- |
| **WP1** | **Build, tests & CI** | `.github/`, `Assets/Tests/`, `*.asmdef` for tests | Unity Test Framework setup (EditMode + PlayMode asmdefs), first tests for `PileDetector`, `UpgradeManager`, `SaveSystem`, `YardProgress`; GitHub Actions with GameCI (`game-ci/unity-test-runner`, `unity-builder`) for tests + Windows build; `.editorconfig` | Yes, once, to create a Unity license secret |
| **WP2** | **Leaf simulation & performance** | `Runtime/Leaves/` | Move `Simulate`/`RebuildGrid` to Burst + Jobs (add `com.unity.burst`, `com.unity.collections`); target 30k leaves @ 60 fps; leaf types (mass, drag, colour); wind gust system; leaf LOD/culling; profiler notes | Yes, for profiling |
| **WP3** | **Gameplay tools & feel** | `Runtime/Tools/`, `Runtime/Player/` | Tool feel pass (rake drag curve, blower cone falloff), tool view-models + simple procedural animation, Leaf Vacuum + hopper, Mulcher, Compost Bin interaction, controller feel (head bob toggle, FOV) | Verify in editor |
| **WP4** | **UI, HUD & shop** | `Runtime/UI/`, `Assets/UI/` | Replace IMGUI with UI Toolkit: HUD (coins, clean %, bag fill, tool), shop screen, title menu, pause menu, settings screen, yard select, toast notifications; keep `DebugHUD` behind a dev flag | Verify in editor |
| **WP5** | **Save, settings & meta-progression** | `Runtime/Save/`, `Runtime/Progression/`, `Runtime/Settings/` | Save versioning + migration, settings (volume, sensitivity, FOV, graphics quality, key rebinding via Input System), yard unlock rules, economy tuning table (ScriptableObject + CSV export) | No |
| **WP6** | **Blender art, batch 1** | `Assets/Art/`, `blender/` (source `.blend`) | Low-poly style guide (palette, texel density, tri budgets); leaf variants (maple, oak, wet clump, pine) < 20 tris each; rake, blower, vacuum, bag, compost bin; FBX export script; import presets in Unity | **Yes**, Blender + Blender MCP on murad's PC |

### Wave 2: start once M0 is done (CI green, interfaces from section 5 merged)

| ID | Package | Owns | Deliverables |
| --- | --- | --- | --- |
| **WP7** | **Yards & level design** | `Assets/Scenes/`, `Runtime/Levels/`, `Editor/Yard*` | `YardDefinition` ScriptableObject (size, leaf count/mix, spawn zones, obstacles, neighbour note, palette); 5 yard layouts (Starter lawn, Corner lot, Orchard, Church green, Hilltop manor); yard builder editor tool; yard-restoration shader hook | 
| **WP8** | **Audio** | `Assets/Audio/`, `Runtime/Audio/` | Audio manager + mixer (Master/Music/SFX/Ambience); rustle that scales with leaves moved, blower/vacuum loops with pitch by throttle, bag rustle, coin chime, wind gusts, birds; licensing log (CC0 sources only, e.g. Freesound CC0, Kenney) |
| **WP9** | **Blender art, batch 2 (environment)** | `Assets/Art/Environment/` | Houses (3 modular kits), fences, trees (bare + leafy), bushes, mailbox, props, grass/flower regrowth meshes, skybox gradients per season |
| **WP10** | **VFX, lighting & post** | `Assets/VFX/`, `Assets/Settings/` (URP volumes) | Leaf burst on bag, dust puffs, blower air streaks, autumn colour grading per yard, time-of-day light rigs, yard "restoration" transition |
| **WP11** | **Onboarding & accessibility** | `Runtime/Tutorial/` | First-yard tutorial prompts, colour-blind safe pile highlights, subtitle/caption system for SFX cues, hold-to-toggle options |

### Wave 3: release

| ID | Package | Deliverables |
| --- | --- | --- |
| **WP12** | **Release & store** | Signed Windows build from CI, itch.io butler upload workflow, store page copy, trailer shot list, screenshots |
| **WP13** | **QA & perf bash** | Test plan, PlayMode smoke tests per yard, perf budget checks in CI, bug triage |

### Dependency map

```
WP1 CI ─────────────┬─► every PR is tested
WP2 Leaves ─────────┼─► WP7 Yards (leaf mix per yard), WP10 VFX
WP3 Tools ──────────┼─► WP8 Audio (tool events), WP11 Tutorial
WP4 UI ─────────────┼─► WP11 Tutorial, WP12 Release
WP5 Save/Settings ──┼─► WP4 settings screen, WP7 yard unlocks
WP6 Art batch 1 ────┴─► WP3 tool view-models, WP2 leaf meshes
WP9 Art batch 2 ────► WP7 Yards
```

---

## 5. Shared contracts (merge first, so packages stay independent)

One small PR (owned by WP1, before Wave 2) adds `Runtime/Core/GameEvents.cs` with plain C# events that every package uses instead of direct references:

```csharp
namespace FallenYard.Core {
    public static class GameEvents {
        public static event System.Action<int> CoinsChanged;           // new total
        public static event System.Action<float> YardCleanChanged;     // 0..1
        public static event System.Action<int, int> PileBagged;        // leaves, coins earned
        public static event System.Action<string> ToolEquipped;        // tool id
        public static event System.Action<string, int> ToolUpgraded;   // tool id, level
        public static event System.Action<float> ToolThrottle;         // 0..1, for audio/VFX
        public static event System.Action<Vector3, float> WindGust;    // direction, strength
        public static event System.Action<string> YardCompleted;       // yard id
        public static event System.Action<GameState> StateChanged;
    }
}
```

Plus three interfaces: `ILeafQuery` (count/move leaves in a radius, owned by WP2), `ITool` (owned by WP3) and `ISaveable` (owned by WP5).

---

## 6. Tooling: plugins, extensions and MCP servers

### MCP servers (let Claude drive the tools on murad's PC)

| Tool | What it gives us | Status | Setup |
| --- | --- | --- | --- |
| **MCP for Unity** (CoplayDev `unity-mcp`) | Claude can open scenes, create GameObjects, edit components, read the Console, run tests and enter Play mode inside the Unity editor | Not installed yet | Package Manager > *Add package from git URL* > `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity`; install Python + `uv`; in *Window > MCP for Unity* run Auto-Setup for Claude Desktop; restart Claude Desktop |
| **Blender MCP** | Claude can model, UV, material and export low-poly assets in Blender, and search/import free assets | Configured on murad's PC, but Blender wasn't running when checked | Open Blender, enable the MCP addon (`uvx mcp-for-blender install-addon` if missing), click *Connect* in its sidebar panel |
| **GitHub** (connected) | Branches, PRs, reviews, CI status for every thread | Working | Nothing to do |

**How threads use them:** cloud threads write code and push PRs. Anything that needs the editor (scene wiring, play-testing, profiling, Blender modelling) runs through MCP on murad's PC, one session at a time, because both apps have a single editor instance.

### Unity packages to add (via the owning package's PR)

| Package | Why | Owner |
| --- | --- | --- |
| `com.unity.test-framework` | EditMode/PlayMode tests | WP1 |
| `com.unity.burst`, `com.unity.collections`, `com.unity.mathematics` | Fast leaf simulation | WP2 |
| `com.unity.cinemachine` | Smooth FP camera, menu cameras | WP3 |
| `com.unity.ui` (built in, UI Toolkit) | HUD and menus | WP4 |
| `com.unity.formats.fbx` | Clean Blender ↔ Unity round-trip | WP6 |
| `com.unity.performance.profile-analyzer` | Perf comparisons | WP2 / WP13 |
| `com.unity.memoryprofiler` | Memory checks | WP13 |

### CI and quality

- **GameCI** GitHub Actions (`game-ci/unity-test-runner@v4`, `game-ci/unity-builder@v4`) with a `UNITY_LICENSE` (Personal) secret. Runs EditMode + PlayMode tests on every PR and builds Windows on `main`.
- **Unity Test Framework** for logic (piles, economy, save migration) and PlayMode smoke tests (load each yard, rake for 5 s, no errors).
- **Git LFS** for `.fbx`, `.blend`, `.png`, `.wav`, `.ogg`. Today `.gitattributes` only marks them `binary`; WP1 switches them to `filter=lfs` before the first art lands (GitHub free LFS is 1 GB storage).

### Editor/IDE extensions

- **VS Code** with the *Unity* extension (Microsoft) + C# Dev Kit, or **JetBrains Rider** (both already in `manifest.json`).
- Optional: **Unity Version Control** is not needed; Git + LFS is enough.

---

## 7. What murad must do by hand

These steps need a human on the Windows PC. Everything else agents can do.

1. **Install Unity:** Unity Hub → Unity 6 LTS (6000.0.x) with *Windows Build Support (IL2CPP)*.
2. **Open the project:** clone `leaf-it-alone`, *Add project from disk*, open it, run *Fallen Yard > Build Test Yard*, press Play. Tell us if anything is pink or throws errors.
3. **Install MCP for Unity** (see section 6) so Claude can wire scenes and run play-tests.
4. **Blender:** install Blender 4.x, open it, and connect the MCP addon whenever an art thread is running.
5. **Unity license for CI:** run the GameCI activation steps once (<https://game.ci/docs/github/activation>) and add `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` as repo secrets.
6. **Git LFS:** `git lfs install` on the PC before pulling art.
7. **Play-test and give feel feedback** at the end of each milestone (that's the part agents can't judge).
8. **Merge PRs** (agents open them, murad merges).

---

## 8. Definition of done (per PR)

- Compiles with zero warnings in the touched assemblies.
- EditMode tests for new logic; CI green.
- No new hard references across package folders (use `GameEvents` / interfaces).
- Inspector-tunable values have tooltips.
- Assets: under the tri budget, GPU instancing on, CC0 or self-made, licence recorded in `Assets/CREDITS.md`.
