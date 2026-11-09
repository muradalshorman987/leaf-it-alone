# Yards and level flow (WP7)

Hollow Creek has five yards, played in order. Each one is a `YardDefinition` asset and its own scene.

| # | Yard | Neighbour | Size (m) | Leaves | Leaf mix | Opens with | First-clear reward |
|---|------|-----------|----------|--------|----------|------------|--------------------|
| 1 | Starter Lawn | Gran Odile | 24 × 20 | 6,000 | oak, maple | from the start | Leaf Blower |
| 2 | Corner Lot | The Okafor twins | 32 × 26 | 10,000 | maple-heavy, some wet clumps; windy corner | clean Starter Lawn | |
| 3 | Orchard | Mr. Halloran | 40 × 34 | 15,000 | oak, lots of wet clumps in the low rows | clean Corner Lot + 300 coins | Leaf Vacuum |
| 4 | Church Green | Verger Pell | 46 × 40 | 20,000 | pine needles under the yews | clean Orchard + 600 coins | Mulcher |
| 5 | Hilltop Manor | Mrs. Ashgrove | 56 × 48 | 28,000 | every leaf type, strong gusts | clean Church Green + 1,000 coins | |

The layouts live in code (`Runtime/Levels/YardLayouts.cs`) so tests can check every yard; the editor tool turns them into assets.

## Building the yards (on the PC)

1. **Fallen Yard > Build All Yards.** Creates `Assets/FallenYard/Data/Yards/` (one definition per yard, `YardCatalog`, `HollowCreekUnlockTable`), per-yard materials in `Assets/FallenYard/Materials/Yards/`, and `Assets/Scenes/Yards/Yard_*.unity`. The yard scenes go first in Build Settings, so the Starter Lawn opens the game. Existing yard assets are kept, so Inspector tweaks survive a rebuild.
2. **Fallen Yard > Yards > Validate Yards** lists layout problems (blocked spawn, bin inside a hedge, missing unlock rules...).
3. **Fallen Yard > Yards > Reset Yard Data From Code** overwrites the assets with `YardLayouts.cs` and rebuilds.

Commit the generated assets and scenes from the PC as usual.

## How a yard scene works

- `YardLoader` (runs first) applies the definition: leaf count, mix and spawn zones on the `LeafField` (through the new `LeafField.SpawnSampler` hook), the goal on `YardProgress`, the yard id for saves, the yard's wind on `WindSystem`, and the player's start point. On Start it raises `GameEvents.YardLoaded`.
- `YardGreybox` builds the yard from primitives: ground, fence with a gate gap, houses, trees, hedges, props, mailbox, compost bin, and hidden wildflowers. WP9 art can replace the primitives; object names (`House_00`, `Tree_03`, ...) and positions stay put.
- `YardRestoration` is the restoration hook: as `YardRestorationChanged` rises, the grass eases to its restored colour and flowers appear one by one (`ShownChanged` event for VFX/audio). The compost bin still sets the global `_FallenYardRestoration` shader float.
- `LevelFlow` moves between yards: saves, sets `GameState.SkipTitleOnNextLoad`, loads the scene. It listens to `GameEvents.YardTravelRequested`.
- Build All Yards also adds the WP10 lighting/weather/VFX rig to every yard scene and a row per yard to `Assets/VFX/Settings/YardGradeTable.asset` (warm Orchard, cool Church Green, ...). Rows someone already tuned are kept.

## Flow for the player

- **Title:** "Start raking / Continue" goes to the saved yard (the last unfinished one); "Hollow Creek map" opens the yard map.
- **Yard map** (`GamePhase.YardMap`, from title, pause or the yard-complete card): every yard with its neighbour, status (New, Best 62%, Cleaned, Clean X first), and Go / Open for N coins / Locked.
- **Yard complete:** "On to <next yard>" (or "Open <next yard> (cost)") travels straight there; arriving shows the neighbour's mailbox note as a toast.

## Shared-file changes (additive)

- `GameEvents`: `YardLoaded`, `YardTravelRequested`.
- `GamePhase`: `YardMap`. `UIFlow`: `OpenYardMap` / `CloseYardMap`.
- `GameState`: `SkipTitleOnNextLoad` / `ConsumeSkipTitle`.
- `LeafField`: `SpawnSampler` (`ILeafSpawnSampler`); null keeps the old even spread.
- `TestYardBuilder`: the player/manager rig is now `BuildRig`, shared with `YardSceneBuilder`; it no longer wipes other scenes from Build Settings.
