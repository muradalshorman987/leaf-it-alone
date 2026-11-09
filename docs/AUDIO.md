# Fallen Yard: Audio (WP8)

Every sound in Fallen Yard is **synthesized by code in this repo** at runtime. There are no
sampled, downloaded or third-party audio files, so there is nothing to license and nothing to
put in Git LFS. Recorded clips can replace any sound later without code changes (see
"Replacing a sound").

Code lives in `Assets/Scripts/Runtime/Audio/` (namespace `FallenYard.Audio`); tests in
`Assets/Tests/EditMode/Audio/`.

## How it fits together

```
GameEvents ─┐                          ┌─► Tool loop source   (rake / blower / vacuum / mulcher)
SettingsMgr ┼─► AudioManager ──────────┼─► One-shot voices    (equip, bag, coins, compost, ...)
            │    ├ ToolLoopModel        ├─► Wind bed + gusts + birds
            │    ├ MusicDirector        └─► Music A/B (cross-fade, low-pass when paused)
            │    ├ AmbienceScheduler + WeatherAmbience
            │    ├ RewardPitch
            │    └ AudioBuses ── Music / Sfx / Ambience sliders (Master is on the AudioListener)
            └─► AudioLibrary (optional overrides) ─► else GeneratedClips ◄─ ProceduralSounds
```

- **`AudioManager`** is the only MonoBehaviour. It creates itself after scene load in any scene
  that has an `AudioListener` (set `AudioManager.AutoCreate = false` to stop that), or you can
  place one in a scene to tune it in the Inspector. It survives scene loads.
- It only **listens**; nothing references it. All logic it uses (`ToolLoopModel`,
  `MusicDirector`, `AmbienceScheduler`, `RewardPitch`, `AudioBuses`) is plain C# and unit tested.
- Clips are rendered on a worker thread at startup (`prewarm`), so the first gust or track
  change doesn't hitch. Rendering everything takes well under a second.

## Event map

| Event | Sound |
| --- | --- |
| `ToolEquipped(id)` | Equip clunk; swaps the tool loop: `rake` → rustle, `blower`/`vacuum`/`mulcher` → engine. `bag` has no loop. |
| `ToolThrottle(0..1)` | Engines idle at ~22% while held and rev (volume + pitch) with throttle; the rake rustle is silent until you sweep and gets louder and brighter with stroke speed. Fast attack, slower release. |
| `PileBagged(leaves, coins)` | Bag rustle, louder for bigger piles; coin chime 0.12 s later if coins > 0. Bagging again within 3 s steps the chime up a major scale (up to an octave). |
| `CoinsChanged(total)` | Purchase chime when the total goes **down** (earning already chimes via `PileBagged`). |
| `ToolUpgraded(id, level)` | Upgrade arpeggio. |
| `CompostChanged(fill, ready)` | Soil thud when fill rises (rate limited); marimba when a fertilizer batch becomes ready. |
| `YardRestorationChanged(r)` | Sparkle when restoration goes up. |
| `WindGust(dir, strength)` | Gust swell, louder with strength, panned by direction relative to the camera; wind bed swells; birds go quiet for 6 s. |
| `YardCompleted(id)` | Short flourish; music ducks for 3.5 s. |
| `StateChanged(phase)` | Music track per phase (below); tool loops fade out outside play; birds stop in pause/shop. |
| `WeatherChanged(id)` (WP10) | Wind bed level and bird chattiness: clear 1×/1×, breezy 1.35×/0.8×, overcast 1.1×/0.45×, blustery 1.8×/0.15×. The wind glides over ~5 s. |
| `SettingsManager.Changed` | Music / SFX / Ambience sliders. |

The manager also raises a new event, **`GameEvents.AudioCuePlayed(string cueId)`**, every time a
one-shot starts, so captions (WP11) can show "Coins", "Wind gust", "Bird song" and so on.

## Music hooks

| Phase | Track | Mix |
| --- | --- | --- |
| Boot | none (yard music after 1 s if nothing changes the phase, so bare test scenes have music) | |
| TitleMenu | Title (68 BPM, Fmaj7–Cmaj7–Am7–G6sus...) | full |
| Playing / YardComplete | Yard (84 BPM, C–Am–F–G...) | full; stinger ducks to 25% |
| Paused | Yard | ducked to 55%, low-passed to 1.1 kHz |
| Shop | Yard | ducked to 55% |

Tracks cross-fade over 2.5 s. `AudioManager.Music.Override = MusicTrack.Title` forces a track
(cutscenes, a later per-yard soundtrack); set it back to `null` to follow the phase again.
Music is generative but seeded: pad chords, a soft bass and a plucked C-major-pentatonic melody.
Notes wrap around the loop end, so the loop has no seam.

## Volume and mixer

- **Master** stays where `SettingsManager` already puts it: `AudioListener.volume`. That covers
  every sound in the game, including any that bypass the AudioManager.
- **Music / SFX / Ambience** sliders multiply each AudioSource's volume (`AudioBuses`).
- **Optional AudioMixer:** assign `mixer` plus `musicGroup`, `sfxGroup`, `ambienceGroup` on the
  AudioManager and expose the group volumes as `MusicVolume`, `SfxVolume`, `AmbienceVolume`
  (dB). The sliders then drive the mixer (via `GameSettings.LinearToDecibels`) instead of the
  sources, which is what you want once you add mixer effects (reverb, ducking snapshots).
  Mixer assets can't be authored from code, so create it in the editor when needed.

Inspector levels (`musicLevel`, `toolLevel`, `sfxLevel`, `windBedLevel`, `gustLevel`,
`birdLevel`) set the mix before the player's sliders.

## Sound list and provenance

All of these are made by `ProceduralSounds.cs` from white noise, state-variable filters and
sine oscillators. Same cue + variant always produces the same samples.

| Cue | Length | How it's made | Source / licence |
| --- | --- | --- | --- |
| RakeRustle | 2 s loop | Hundreds of tiny noise crackles through a 3.8 kHz band-pass, plus a fluttering noise bed | Original, generated in code (project licence) |
| BlowerLoop | 2 s loop | 118 Hz harmonic motor + 2.6 kHz low-passed air + 1.76 kHz whine, 4 Hz wobble | Original, generated |
| VacuumLoop | 2 s loop | 196 Hz motor + resonant 850 Hz "tube" + high whine | Original, generated |
| MulcherLoop | 2 s loop | 74 Hz motor + noise chopped at 12 Hz | Original, generated |
| ToolEquip | 0.2 s | Pitch-dropping thump + click + handle knock | Original, generated |
| BagRustle (3) | ~0.6 s | Dense crinkle grains at 4–6 kHz over a soft swoosh | Original, generated |
| CoinChime | 0.75 s | Two-note bell (E6, B6), inharmonic partials | Original, generated |
| Purchase | 0.8 s | C6–E6–G6 bells | Original, generated |
| Upgrade | 1.3 s | G5–C6–E6–G6–C7 bells | Original, generated |
| CompostThud (2) | 0.45 s | Low sine thump + low-passed soil noise | Original, generated |
| CompostReady | 1.3 s | Marimba-like G4–D5–G5 | Original, generated |
| RestoreSparkle | 1.5 s | Nine random high pentatonic bell pings | Original, generated |
| YardComplete | 3 s | Rising pluck run into a C major pad chord | Original, generated |
| UiClick | 0.06 s | Short tick (for menus; not wired to any event yet) | Original, generated |
| WindBed | 10 s loop | Brown noise, low-passed, with slow swells and a faint whistle | Original, generated |
| WindGust (2) | ~3.5 s | Noise through a band-pass swept by the swell, plus rumble | Original, generated |
| Birdsong (4) | 0.4–1 s | Made-up songbird: 2–6 whistled syllables with pitch sweeps | Original, generated (not modelled on any real species' recording) |
| MusicTitle | 28 s loop | Generative pad/bass/pluck, seed-fixed | Original, generated |
| MusicYard | 23 s loop | Generative pad/bass/pluck, seed-fixed | Original, generated |

Preview WAVs of every cue are attached to the WP8 project thread (not committed, to keep
binary files out of the repo).

## Replacing a sound

1. `Assets > Create > Fallen Yard > Audio Library`, add an entry, pick the cue, drop in one or
   more clips (a random one plays each time) and set a gain.
2. Assign the library to the AudioManager (place one in the scene).
3. **Log the clip below** with its source URL and licence. Only CC0 or self-recorded audio.
   Audio files go through Git LFS (`.gitattributes` already covers `.wav`, `.ogg`, `.mp3`).

### Third-party audio log

| File | Cue | Source | Author | Licence | Date added |
| --- | --- | --- | --- | --- | --- |
| _none yet_ | | | | | |

## Testing

`Assets/Tests/EditMode/Audio/` covers: every cue renders finite, unclipped, audible,
deterministic audio; loops join without a click and one-shots start and end silent; tool
throttle mapping and smoothing; bus maths and mixer mode; music track/duck/muffle per phase;
bird timing and gust suppression; coin streak pitch; and the AudioManager reacting to each
`GameEvents` event and to `SettingsManager.Changed`. Nothing plays during EditMode tests; they
check which clip, volume and pitch were chosen.

Things only a person can judge (do these in Play mode): overall mix balance, whether the
blower is too loud at full throttle, and whether the music loops get tiring. All levels are
Inspector fields on the AudioManager.
