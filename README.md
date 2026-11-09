# Fallen Yard

An original, cozy, first-person, low-poly leaf-cleanup game for **Unity 6 (URP)**.
Rake leaves into piles, bag them for coins, unlock a leaf blower and bigger bags, and bring the yard back to life.

> Fallen Yard is its own game. It is inspired by the "clean up a yard" genre but uses its own name, code, art and levels.

## What's in the box

| Area | Script | What it does |
| --- | --- | --- |
| Player | `Player/FirstPersonController.cs` | CharacterController walk/sprint/jump + mouse look |
| Input | `Core/GameInput.cs` | Works with the new Input System *or* the legacy Input Manager |
| Leaves | `Leaves/LeafField.cs` | Simulates ~12k leaves as plain arrays, draws them with `Graphics.RenderMeshInstanced` (GPU instancing, 1023 per batch), uniform grid for queries, stacking piles and angle-of-repose slumping |
| Leaves | `Leaves/LeafMeshFactory.cs` | Procedural low-poly leaf mesh until Blender assets exist |
| Piles | `Leaves/PileDetector.cs` | Flood-fills dense grid cells into piles (centre, size, leaf count) |
| Tools | `Tools/Rake.cs` | Hold LMB and sweep: leaves follow the rake head and are drawn toward you |
| Tools | `Tools/LeafBlower.cs` | Hold LMB: cone push with lift so leaves flutter |
| Tools | `Tools/BagInteractor.cs` | Look at a pile, press **E** to bag it (capacity-limited) for coins |
| Tools | `Tools/ToolController.cs` | Switch tools with **1 / 2** |
| Progress | `Progression/UpgradeManager.cs` + `ToolUpgradeDefinition.cs` | Upgrade hooks: tools read radius/strength/range/capacity multipliers per level |
| Progress | `Progression/YardProgress.cs` | Clean percentage, completion bonus at 95% |
| Save | `Save/SaveSystem.cs`, `SaveData.cs` | Atomic JSON save in `Application.persistentDataPath` (coins, tool levels, finished yards) |
| UI | `UI/UIManager.cs` | UI Toolkit HUD (coins, clean % bar with goal marker, tool, bag prompt, toasts), title and pause menus, **Tab** upgrade shop, yard-complete card. Owns the game phase (`UI/UIFlow.cs`) |
| UI | `UI/Views/*`, `Assets/UI/Resources/FallenYardUI.uss` | Screens are built in code; all styling lives in one USS file |
| UI | `UI/DebugHUD.cs` | Developer stats overlay, **F3** in the editor and development builds |
| Editor | `Editor/TestYardBuilder.cs` | Menu **Fallen Yard > Build Test Yard** generates materials, upgrade assets and a playable scene |

## Setup

1. Install **Unity Hub** from <https://unity.com/download>.
2. In Unity Hub, install the latest **Unity 6 LTS** (6000.0.x or newer). Add the build support modules you want (Windows/Mac).
3. Clone this repo, then in Unity Hub click **Add > Add project from disk** and pick the cloned folder.
   If Hub says the editor version differs, choose your installed Unity 6 version; that's fine.
4. Open the project. Unity resolves the packages in `Packages/manifest.json` (URP, Input System, uGUI).
   If asked *"enable the new Input System backends?"*, click **Yes** (the game also works with the old one).
5. Menu **Fallen Yard > Build Test Yard**. This creates `Assets/Scenes/TestYard.unity`, assigns a URP asset if the project has none, and makes all materials and upgrade data.
6. Press **Play**.

### Controls

| Key | Action |
| --- | --- |
| WASD / Shift / Space | Move / sprint / jump |
| Mouse | Look |
| Left mouse (hold) | Use tool |
| 1 / 2 | Rake / Leaf Blower (unlock the blower in the shop) |
| E | Bag the pile you're looking at |
| Tab | Upgrade shop (the Tool Shed) |
| Esc | Pause menu / close whatever is open |
| F3 | Developer stats overlay (editor and development builds) |

### The core loop

Rake leaves toward one spot until a pile forms (press **F3** to see the pile count), look at it and press **E**. Coins buy rake upgrades, bigger bags, and the leaf blower. Bag 95% of the yard for a completion bonus.

## Tests, CI and shared events

- Tests: *Window > General > Test Runner* (EditMode and PlayMode). They live in `Assets/Tests/`.
- CI: GitHub Actions with GameCI runs the tests on every PR and builds Windows on `main`. It needs three repo secrets once; see [.github/CI.md](.github/CI.md).
- Systems talk through `Core/GameEvents.cs` (coins, piles bagged, tool equipped/upgraded, yard clean %, yard completed, wind, game phase) instead of referencing each other.
- Art and audio go through Git LFS: run `git lfs install` once.

## Optional: let Claude drive the editor (MCP for Unity)

1. In Unity: **Window > Package Manager > + > Add package from git URL…** and paste
   `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity`
2. It needs Python and `uv`. On Windows: `powershell -c "irm https://astral.sh/uv/install.ps1 | iex"`
3. Open its window (under the Window/Tools menu, "MCP for Unity") and run Auto-Setup for Claude Desktop.
4. Fully restart Claude Desktop with the project open, then ask Claude to "check my Unity scene".

## Tuning tips

- **More leaves:** raise `LeafField.leafCount`. Up to ~25k is fine on the main thread; beyond that, move `Simulate` and `RebuildGrid` into Burst jobs (the array layout is already job-friendly).
- **Pile shape:** `pileHeightPerLeaf`, `maxPileHeight`, `reposeDifference` (lower = flatter piles).
- **Rake feel:** `Rake.dragFollow`, `pullSpeed`, `baseRadius`, `baseStrength`.
- **Custom leaves:** drop a Blender leaf mesh into `LeafField.leafMesh`. Keep it tiny (under 20 tris) and enable GPU Instancing on its materials.

## Troubleshooting

- **Pink materials:** the project isn't using URP. Create one via **Assets > Create > Rendering > URP Asset (with Universal Renderer)**, assign it in **Project Settings > Graphics** and **Quality**, then run **Build Test Yard** again.
- **Leaves invisible:** the `LeafField` needs at least one material with *Enable GPU Instancing* ticked.
- **Reset progress:** right-click the `GameManager` component > *Delete Save File*.

## Roadmap

- Blender low-poly assets: leaf variants, rake, blower, bags, house, fences, trees
- Settings and yard-select screens, audio (rustle, blower hum), wind gusts that re-scatter leaves
- Multiple yards with seasonal palettes, leaf vacuum, mulcher
