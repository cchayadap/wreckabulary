# Winter batch preflight — 2026-10-09

STATUS: COMPLETE — source and tool inspection only; no asset production or native validation performed.

Scope: Tools/AssetPipeline, Unity model/material import, runtime animation, lobby themes/shop and audio. Assets remained frozen. This audit wrote only this report; no Unity launch, Git command, provider submission or package regeneration occurred. Parent owns the current native UI verification and next implementation release.

## Proposed batch: Winter House Party

Ship one cohesive, permanently selectable collection. Palette: fir green, cranberry red, oat cream, warm brass, with restrained icy blue highlights. Keep the foreground floor clear for the native avatar and place seasonal detail behind/alongside it. No new map topology, weapon rules or timed event claims in this batch.

| Deliverable | Concrete content and budget | Proposed destination/use |
| --- | --- | --- |
| One GPT background | Bright Christmas sunroom; low eye level, clear central floor, off-centre tree, warm practical bulbs, snow visible outside; no words/characters/logos. One native imagegen request, preserve exact prompt/hash. | `Assets/_Project/Resources/UI/Themes/winter-v1.png`; fourth `LobbyThemes` entry. Existing importer supports this path automatically. |
| Three real 3D prop sources | `WinterTree`: 1.6 m tall, rounded layered fir foliage, warm brass star/ornaments, at most 3k triangles. `WrappedGifts`: three separate reusable parcels in one source collection, total at most 1.5k triangles. `DoorWreath`: 0.65 m diameter, foliage/bow/bells, at most 1.5k triangles. Max two shared materials per prop, one shared 512px atlas if needed. | Editable `.blend` sources under `ArtSource/Winter/`; exported FBX under `Assets/_Project/Art/Seasonal/Winter/Models/`; a saved `Resources/Collections/WinterHouseParty.prefab` references these meshes. Native lobby theme decor and one optional authored Hub corner use the same prefab. These are actual meshes, independently authored in Blender; generated background is not represented as geometry. |
| Two recipe finishes | `Winter` finish on existing SHIELD and SODA only: cranberry enamel/cream bands/brass detail. Preserve silhouette, gameplay colors, grips and colliders. | Shared persistent materials under `Assets/_Project/Materials/Collections/Winter/`; explicit two-recipe availability in `Data/Config/items.json`; existing actual shop miniature preview. |
| One wardrobe look | Existing hoodie/hood, mittens, boots and satchel in fir/cream/cranberry. Reuse current meshes; label as a coordinated look, not a new garment model. | One `CosmeticBundles` entry; use existing wardrobe color IDs where possible. Defer Santa hat until separate attachment/rig scope is justified. |
| One original emote | `WinterShuffle`, 2.4-second side step, small knee bounce, two overhead mitten claps, return to planted neutral. No root translation and no full spin; hands avoid the head. One-shot, returns to Idle. | `Assets/_Project/Art/Seasonal/Winter/Animations/WinterShuffle.fbx` plus canonical source in `ArtSource/Winter/`. Reference its actual imported clip in `Resources/Collections/WinterCollection.asset`; do not replace the original Avatar FBX or its seventeen clips. Play via a lobby emote button and optional round-end cosmetic action. |
| Six short sound files | Bell pair (0.25–0.45 s, pickup), paper pair (0.15–0.25 s, craft), soft snow/crunch pair (0.18–0.30 s, dodge); two deterministic no-immediate-repeat variants per cue. Original local synthesis initially, no voice or purchased samples. | `Assets/_Project/Audio/Winter/{bell,paper,snow}-{01,02}.wav`, referenced by the collection asset. 44.1 kHz mono 16-bit PCM, peak at or below −3 dBFS, tiny fades, no DC bias. Route through existing `GameFeedback` mute/master-volume/throttle behavior. |

This is one playable collection, not a large concept dump. Three decorative prop types, two existing recipe finishes, one existing-mesh look, one playable emote, six short sounds and one backdrop are enough for a meaningful first checkpoint. A new full Christmas map, new garment rig and broad sticker system should follow after this collection has native evidence.

## Verified local capabilities and limitations

- `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe --version` returned **Blender 5.2.2 LTS**, hash `d13f752e3b9c`. This was only a version check; no Blender scene or GPU job was run.
- `Tools/AssetPipeline/build_assets.py` is an existing Blender `bpy` pipeline, not a provider service. It imports selected supplied GLB packs, removes embedded cameras/lights, preserves markers, exports FBX, and writes bounds/material/clip manifests. `selection.json` currently selects 40 items, 26 letters, 21 environment modules, 10 VFX meshes and one avatar: 98 models. The saved `verify_report.json` records 98 checked/0 failed, but that is historical evidence, not a rerun in this audit.
- `verify_assets.py` checks round-trip triangle counts, bounds, authored dimensions, material registration, item `Grip_R`, armature bones and clips. **It writes `Data/Generated/verify_report.json`**, so it was deliberately not executed during the freeze.
- `author_clips.py` authors planted-foot motion for the existing 22-bone Generic rig and has a 4 mm IK-overreach bound. Its current `write_action` replaces an existing named action and preserves its frame range; adding a new clip needs a new-action path, not simply appending a name to `REPLACED`.
- The original `--packs` input directory was not verified. There is no `packs` child under the project parent. Existing imported FBX and original report exist; avoid rebuilding all 98 assets merely to produce seasonal assets.
- Node is on PATH. Although `python` resolves through the WindowsApps alias, `python --version` successfully returned **Python 3.14.3** during the followup. Standalone Python is available for source processing and standard-library PCM WAV synthesis; Blender remains required for `bpy` mesh/animation work.
- `game-dev` and `ffmpeg` did not resolve on PATH. Installed Game Development Studio plugin is skills-only; its README explicitly says the separate CLI is required. Therefore its capabilities/doctor commands could not be invoked. This does not block the repository's Blender workflow.
- Built-in `image_gen.imagegen` is callable and already used for this project. No callable Meshy/Tripo MCP was found in the exposed tool inventory; installed skill names do not prove live provider availability. No provider credential status was read, and no provider job was submitted. Higgsfield remains excluded by the user's instruction.

## Exact runtime extension points and pitfalls

### Theme and native decor

`Scripts/UI/Lobby/LobbyThemes.cs` already provides preview-without-save, owned equip, saved restoration and `Changed`; `LobbyTheme.Background` resolves `UI/Themes/<id>-v1`. `Editor/Art/LobbyThemeImporter.cs` imports new `-v1.png` backgrounds at max2048, sRGB, bilinear, non-readable, no mipmaps. `ShopPage` enumerates all themes; add the corresponding Career offer/free ownership rule so preview and equip agree.

`LobbyStage.ShowArtwork` hides cached map renderers and preserves the geometry hierarchy. Add one native seasonal-decor root that is independent of the map-renderer cache, visible only for artwork mode and the selected winter theme. Do not parent it beneath hidden map geometry. Position it relative to `Spot`/the avatar-camera basis; exclude the item-preview stand and avatar controls. Toggle/release must deactivate this root, bounded effects, and all associated renderers. Use existing 14 cached motes for light snowfall; do not create per-frame snow GameObjects or dynamic lights.

`RoomDressing` is saved editable presentation. For Hub decoration, add one separately named/versioned collection prefab instance only after native placement review; do not regenerate existing RoomDressing or move furniture/routes. Decor must not introduce gameplay colliders, light-budget additions or overhead cutaway conflicts.

### Recipe finishes

A fourth skin currently fails in several independent places:

- `Art/MaterialLibrary.FamilyOf` recognizes only `_Classic`, `_Candy`, `_Arcade`.
- `Art/TactileMaterials.TrySurface` recognizes the same three suffixes.
- `UI/Lobby/LobbyStage.PreviewItem` rejects all other skin strings.
- `Rules.CosmeticBundles.WithItemSkin` checks ownership and `ItemDefinition.HasSkin`; `Data/Config/items.json` supplies those per-item lists.

Extend these from a shared supported-skin definition rather than adding one inconsistent string check. Add matching named material families and preserve fallback to Classic for missing art. Existing SHIELD/SODA miniatures/grips remain intact. Do not sell `Winter` for every recipe when only two have authored finishes.

`ModelLibraryBuilder.Refresh` recreates its library from `Data/Generated/build_report.json`; ad hoc entries disappear at the next refresh. Likewise MaterialLibrary is built from the generated material manifest. Prefer a small separate seasonal collection asset with explicit mesh/material/clip references, or deliberately extend both builders' input merge contracts. Do not inject transient runtime-only library entries and call the integration persistent.

### Animation

`Player/PlayerAppearance.LoadRequiredClips` has fifteen required and two optional clip names. `Play` rejects any clip outside that cached dictionary. `fullBodyAction` has a hard-coded full-body list. A new dance must be registered as an optional full-body cosmetic clip and obey pause, action interruption and return-to-locomotion behavior.

`LobbyStage.SpawnAvatar` creates a one-input PlayableGraph bound only to Idle, using unscaled time. Add a small two-input/mixer playback path with a clear `PlayEmote` entry point and return-to-idle; release/destroy must dispose playable resources. Gameplay emotes use scaled time and must not move the controller or extend combat invulnerability. Reject/interrupt the dance during movement, damage, item use and active combat as appropriate.

`Tests/Editor/NativeAnimationBindingTests.cs` deliberately asserts exactly seventeen original FBX clip identities. Keep that test intact by importing the new dance separately with the same Generic bone hierarchy and a dedicated seasonal clip reference. Native pose samples must prove the added clip animates this rig; a clip-name assertion alone is inadequate.

### Audio

`Assets/_Project/Audio` contains only `.gitkeep`. `Scripts/UI/GameFeedback.cs` synthesizes one cached 16 kHz mono clip per `GameCue`, uses a persistent 2D source at volume0.13, checks `wv.muted`, and throttles identical cues within0.055s. `ThrownGear` separately synthesizes bomb ticks; leave bomb warning timing unchanged.

A collection sound bank can override only Pickup/Craft/Dodge while retaining the existing synthetic fallback for every missing cue. Track no-immediate-repeat variant indices without allocations. Theme preview should not silently replace in-match sounds: use the equipped collection or an explicit sound-pack choice; provide a deliberate preview button. Keep master `AudioListener.volume`, mute and pause-settings persistence intact. No AudioMixer is currently required for this bounded change.

## Required validation before calling the collection complete

1. **Asset integrity:** nonempty imported meshes; triangle/material budgets above; grounded pivots; centimetre/meter scale; source FBX rotation preserved under placement anchors; no cameras, lights or colliders in decorative source. Check final generated PNG dimensions/content, preserve source hash/prompt, and inspect the actual image.
2. **Saved/imported references:** reopen in a fresh Unity process and confirm the collection prefab, material textures and emote clip resolve. Verify `ModelLibraryBuilder.Refresh` does not erase the collection's independent references. Authoring upgrade repeated twice leaves scene transforms/artist edits and object counts unchanged.
3. **Ownership/save rules:** winter preview does not spend/save; equip respects actual ownership; restore returns the prior theme; old save strings still parse. Only SHIELD/SODA accept the new finish; changing one retains unrelated recipe finishes.
4. **Renderer/resource lifecycle:** home → Play → Shop → gameplay → lobby cycles correctly hide winter props/snow in map mode, restore authored map renderers, and leave no stale cameras/lights/graphs/native previews. Repeat ten theme changes and assert stable object/material counts.
5. **Emote behavioral/pose test:** sample start/middle/end after LateUpdate with the existing probe pattern; at least shoulders, hands and knees must move; feet remain within floor tolerance, root displacement negligible, native avatar orientation/bounds valid. Verify pause freezes gameplay pose, damage/action interrupts it, and completion returns to Idle. Original seventeen animation bindings remain unchanged.
6. **Audio content/routing:** parse WAV headers and PCM, assert lengths/rate/channels, finite nonzero RMS, bounded peaks/DC and short fade endpoints. Verify alternating variants, mute suppression, master volume, throttle and fallback. Listen to rendered in-game audio; a passing importer/source-state test is not audible quality proof.
7. **Actual native evidence:** normal Unity camera screenshots for 16:9 and21:9 winter home, shop theme/SHIELD finish, three native prop closeups, dance start/mid/end, and solo plus two-local gameplay. Record camera/theme/emote/clip state and dimensions in the manifest. A short recorded native sequence with sound must demonstrate the dance and variation playback before claiming audio/animation quality.

## Next recoverable action

Finish and preserve the current UI/native checkpoint first. Then split one concrete batch into independent writers: (A) built-in imagegen backdrop/provenance; (B) isolated Blender winter prop+clip source/export and validation; (C) collection/theme/skin/emote/audio runtime plumbing; (D) native test/capture fixture. Use one collection manifest recording proposed/generated/imported/integrated/native-reviewed states per asset. Root owns native Unity runs, final scene placements and verified commit/push. No Christmas assets have been created by this preflight.
