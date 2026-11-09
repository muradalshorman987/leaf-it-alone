# CI, tests and shared events

## What runs

`.github/workflows/ci.yml` uses [GameCI](https://game.ci):

| Job | When | What |
| --- | --- | --- |
| Check Unity license secrets | always | Skips the Unity jobs with a warning if the secrets below are missing |
| Unity tests | every PR and push to `main` | EditMode + PlayMode tests, results posted as a "Unity test results" check |
| Windows build | push to `main`, or *Actions > CI > Run workflow* | `StandaloneWindows64` (Mono) via `FallenYard.EditorTools.CIBuild.BuildWindows`, uploaded as an artifact for 14 days |

If Build Settings has no scene yet, the build method runs *Fallen Yard > Build Test Yard* first, so CI can build before a real scene is committed.

## One-time setup (murad, on your PC)

GitHub needs a Unity license to run the editor. A free **Unity Personal** license works.

1. Install Unity Hub and sign in, then activate a Personal license (Hub > Preferences > Licenses > Add).
2. Find the license file Hub created:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
3. On GitHub open **Settings > Secrets and variables > Actions > New repository secret** for this repo and add:
   - `UNITY_LICENSE`: the whole contents of `Unity_lic.ulf` (open it in Notepad, copy everything)
   - `UNITY_EMAIL`: the email of your Unity account
   - `UNITY_PASSWORD`: its password
4. Re-run the CI workflow (Actions > CI > Run workflow). The test job should go green.

GameCI's own guide, if anything differs: <https://game.ci/docs/github/activation>.

## Running tests locally

*Window > General > Test Runner*, then **EditMode** or **PlayMode** > *Run All*.

- Tests live in `Assets/Tests/EditMode/<Area>/` and `Assets/Tests/PlayMode/<Area>/`, namespace `FallenYard.Tests.<Area>`.
- EditMode doesn't call `Awake`/`Update`: use `TestUtil.Call(component, "Awake")` and `TestUtil.CreateLeafField(...)`.
- Never touch the real save: call `TestUtil.UseTempSaveFolder()` in `SetUp` (it sets `SaveSystem.DirectoryOverride`).
- Call `GameEvents.ClearAllSubscribers()` in `SetUp`/`TearDown` of any test that listens to events.

## Shared events contract

`Assets/Scripts/Runtime/Core/GameEvents.cs` is how packages talk without referencing each other.
Listen in `OnEnable`, unsubscribe in `OnDisable`; raise with `GameEvents.RaiseX(...)`.

| Event | Payload | Raised by today |
| --- | --- | --- |
| `CoinsChanged` | new total | `GameManager.AddCoins/TrySpend/ResetSave` |
| `YardCleanChanged` | 0..1 | `YardProgress.Update` (when it moves by 0.1%) |
| `PileBagged` | leaves, coins | `BagInteractor` |
| `ToolEquipped` | tool id | `ToolController.Equip` |
| `ToolUpgraded` | tool id, level | `UpgradeManager.TryUpgrade` |
| `ToolThrottle` | 0..1 | not yet (WP3) |
| `WindGust` | direction, strength | `WindSystem` when a gust starts (WP2) |
| `YardCompleted` | yard id | `GameManager.MarkYardCompleted` |
| `StateChanged` | `GamePhase` | not yet (WP4) |

Adding an event is fine in any PR. Renaming, removing or changing a payload needs its own small PR.

## Git LFS

Art, audio, fonts and video are stored with Git LFS (see `.gitattributes`). Run `git lfs install` once before committing any of them.
GitHub's free tier is 1 GB of LFS storage and 1 GB/month of bandwidth; CI checkouts count against bandwidth.

## Smart merge for scenes and prefabs

`.gitattributes` marks Unity YAML files with `merge=unityyamlmerge`. To enable it, add to your global git config
(adjust the editor path):

```
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = '"C:/Program Files/Unity/Hub/Editor/6000.0.47f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
    recursive = binary
```
