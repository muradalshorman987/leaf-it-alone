# VFX, lighting and weather (WP10)

Everything here is built in code, so a scene only needs one object. Run
*Fallen Yard > Add Lighting, Weather and VFX* on any open scene (the test yard builder
already calls it). It creates the assets below, adds a baseline look to
`Assets/DefaultVolumeProfile.asset`, and adds the **Lighting, Weather & VFX** object with
four components. Run it again any time: it reuses what exists and never overwrites
overrides you set by hand.

| Component | What it does | Reads | Writes |
| --- | --- | --- | --- |
| `WeatherSystem` | Spells of Clear / Breezy / Overcast / Blustery weather that blend into each other. Scales the `WindSystem` breeze and gusts, and gives a short "gust flash" on every `GameEvents.WindGust`. | `WindGust` | `WindSystem.settings`, `GameEvents.WeatherChanged` |
| `LightingRig` | Time of day. Moves the sun (and the moon at night, same light), sets its colour and intensity, trilight ambient and fog from a `LightingPreset`, dimmed and greyed by cloud. | `WeatherSystem.Current` | Directional light, `RenderSettings` |
| `LeafVfx` | Blower air streaks and ground dust, vacuum intake, rake scuffs, the bag poof, loose leaves drifting on the wind, a leaf flurry on gusts and a celebration when a yard is finished. | Current tool and its throttle, `LeavesBaggedAt`, `WindGust`, `YardCompleted`, wind, weather | Particles only |
| `PostProcessDriver` | Per-yard autumn colour grading that warms up as the yard is cleaned and restored, a glow on yard completion, weather and night layered on top. Owns a runtime global Volume (priority 10) above the default profile. | `YardCleanChanged`, `YardRestorationChanged`, `YardCompleted`, weather, daylight | URP Color Adjustments, White Balance, Vignette, Bloom |

## Assets it creates

- `Assets/VFX/Materials/VFX_Dust.mat`, `VFX_Leaf.mat`, `VFX_SoftDot.png`: URP particle materials (Unlit transparent for dust and streaks, Simple Lit double-sided and instanced for leaves). They are referenced from the scene so the shaders ship in builds.
- `Assets/VFX/Settings/LightingPreset_Autumn.asset`: sun colour and intensity, ambient and fog over 24 hours, and how much cloud and gusts change them. Make a copy per yard for a different mood.
- `Assets/VFX/Settings/YardGradeTable.asset`: messy and restored grade per yard id (matches `YardProgress.yardId`). Yards not listed use the defaults, so WP7 can add rows as yards land.
- `DefaultVolumeProfile.asset` gets neutral tonemapping, soft warm bloom, a light vignette and a little contrast, only for components with no overrides yet.

## Tuning

- **Clock**: `LightingRig.day`. Starts at 14:30, 30 real minutes per game day. Set `dayLengthMinutes` to 0 to freeze it (e.g. golden hour for screenshots: 17.5).
- **Weather and gameplay**: each `WeatherProfile` has wind, gust interval and gust strength multipliers. Blustery weather means more, stronger gusts, so bag piles sooner. Untick `WeatherSystem.affectWind` for looks only. Right-click the component for "Next weather" debug entries.
- **Particles**: `LeafVfx.density` (0..2) scales every effect; the budget caps are `maxStreaks`, `maxDust`, `maxLeaves`.

## Performance

The leaf field budget (see `leaves-performance.md`) is untouched: nothing in WP10 reads or writes the `LeafField` or schedules jobs.

| Item | Cost |
| --- | --- |
| Particles | 3 emit-only systems, at most 640 live particles in total. Leaves are 8-triangle meshes drawn with GPU instancing; dust and streaks are billboards. Leaf particles use low-quality world collision so they settle on the ground. |
| Lighting | One directional light (the existing sun) updated once per frame. |
| Post | One extra global Volume with four overrides; Bloom is the only effect with a real GPU cost. |
| Main thread | A few raycasts per frame while a tool is running, plus per-particle `Emit` calls (tens per frame at most). |

If the GPU is the bottleneck, lower `LeafVfx.density`, then Bloom in the default profile.

## Events added to `GameEvents`

- `LeavesBaggedAt(Vector3 position, int leaves)`: raised by `BagInteractor` next to `PileBagged`, so the poof appears on the pile. Audio can use it for a positional rustle.
- `WeatherChanged(string weatherId)`: `"clear"`, `"breezy"`, `"overcast"` or `"blustery"`, for ambience (WP8).
