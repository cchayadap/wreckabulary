# Winter sound bank

Six original deterministic synthesized effects, with no recordings, purchased samples, voices, external audio models or provider jobs. The project owner determines distribution terms. The generator and WAV hashes are recorded in `manifest.json`; actual decoded PCM measurements are in `validation.json`.

| Cue | Files | Durations | Peak | RMS |
| --- | --- | --- | --- | --- |
| Pickup | `bell-01.wav`, `bell-02.wav` | 340 / 390 ms | −4.50 dBFS | −14.11 / −14.66 dBFS |
| Craft | `paper-01.wav`, `paper-02.wav` | 190 / 220 ms | −4.50 dBFS | −18.84 / −18.25 dBFS |
| Dodge | `snow-01.wav`, `snow-02.wav` | 240 / 280 ms | −4.50 dBFS | −18.31 / −18.98 dBFS |

All files are 44.1 kHz mono signed 16-bit PCM. First and last samples are zero. DC mean magnitude stays below 1.4 × 10⁻⁷; clipped sample count is zero. Variants have distinct pitches or noise/envelope timings, and pair correlation remains below 0.06 in magnitude. Bell attacks were lengthened to 6 ms after the initial endpoint-energy check identified a sharper transient; every final check passes.

From the repository root:

```text
python Tools/AssetPipeline/build_winter_audio.py
python Tools/AssetPipeline/build_winter_audio.py --check
```

The first command regenerates only the staged audio bundle by default. The second reads the existing WAVs, recomputes measurements, regenerates PCM in memory to check deterministic identity, and verifies manifest/generator hashes without writing files. The generator rejects destinations inside `Assets`.

## Native import and routing

Run `Wreckabulary.EditorTools.WinterAudioImporter.Build` in Unity after compilation. It copies the six retained sources into `Assets/_Project/Audio/Winter/`, imports PCM with decompression on load and preserved sample rate, and saves `Assets/_Project/Resources/AudioPacks/Winter.asset`. Source files are already mono, so downmix is disabled to avoid importer normalization. Repeated import updates the existing bank, preserving its asset identity.

`GameSoundBank.TryNext(cue, previousIndex, out clip, out index)` selects the next valid variant. The saved Winter bank has two distinct clips for each supported cue. Missing or unsupported entries use the original `GameFeedback` synthesis.

`GameSoundPacks.Selected` reads the explicit `wv.sound.pack` preference. `Select("winter")` equips Winter; `Select("default")` restores Classic and clears the key. Unknown IDs are rejected. Both lobby General settings and pause Audio settings expose the selector and a deliberate preview action. Pause Reset audio restores Classic, unmutes and restores full master volume.

`GameSoundPacks.Preview()` previews the selected pack. `Preview("winter", GameCue.Craft)` previews a named pack without equipping it. It uses a separate variant cursor. `LobbyThemes.Preview` and visual theme equip do not change sound selection.

`GameFeedback.Play` preserves its existing API and persistent 2D source. Mute, zero master volume, a paused listener and the existing 55 ms per-cue throttle run before clip selection. Preview uses the same gates. Master `AudioListener.volume` remains untouched, and source volume remains 0.13. Variant cursors advance only after `PlayOneShot`; muted, silent, paused or throttled calls do not consume a variant. Only generated synthetic clips are destroyed by the feedback reset; imported clips remain owned by their saved bank. `GameFeedback.Played` fires after accepted playback for observable tests.

## Verification status

- Source WAV generation, decoded PCM checks, deterministic reproduction, hashes and pair distinctness: **passed**.
- `waveforms.svg` shows the actual exported samples on a consistent time and amplitude scale.
- Game Studio CLI is unavailable locally; no canonical package verification is claimed.
- Native importer, `WinterAudioImportTests`, `WinterAudioTests`, settings UI screenshots and listening in the actual game mix: **pending root-controlled Unity run**.
- Numeric validation is not an audible-quality review. Listen for bell harshness, paper clicks and snow noise in the existing 0.13 source-volume mix, and record a native sequence with sound before declaring the audio fully reviewed.

The exact next action is the Unity importer above, followed by the two audio test fixtures and a short native pickup/craft/dodge recording with Winter selected, including a muted preview and Classic reset.
