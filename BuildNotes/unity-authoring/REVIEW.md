# Authoring workflow review

STATUS: SOURCE REVIEW COMPLETE; NATIVE VERIFICATION PENDING

Scope: critical source re-review by the implementation agent of commits 3d8b519, 8824dba and 2d8cb4e, plus read-only current root launch/profile wiring. This is not a separate-person independent review. No compiler or Unity is authorized for this pass. Native reviewer must verify actual UI/save/reload/preview/play behavior.

- [x] Persistence, preview identity, manifest bounds and root consumer wiring inspected.
- [x] Scene setup restoration defect corrected in source with regression.
- [x] Actual AssetDatabase save/unload/reload and preview regression authored.
- [ ] Native screenshots and user acceptance: NOT RUN.

## Findings and dispositions

1. The initially missing WorkshopCourse launcher hook is now present in root's working source. OpenWorkshop uses the exact asset document/catalogue/mode and applies the asset seed; local play uses its item/rule TextAssets and presentation profiles with LaunchAssets fallbacks. The runtime workshop draft is independently autosaved; editing it does not overwrite the CourseAsset.
2. **A2, corrected in source:** old scene restoration discarded all saved scene records when any untitled additive scene existed. RestoreSetup now prevalidates paths and active/loaded state, reconstructs saved and empty untitled scenes in order, and preserves the active selection. Missing saved assets fail before replacing current scenes; SessionState recovery records remain intact. Actual Unity behavior is pending native tests.
3. **Root launcher initialization cleanup:** OpenWorkshop assigns/subscribes the component before Open/seed/Refresh completes. If storage creation or initialization throws, retry can reuse a partially initialized workshop. Root was asked to unsubscribe/destroy only the newly created component on failure and preserve an existing valid draft. No root file was edited here.

## Persistence and trust boundaries

The Inspector keeps un-applied course form edits in its editor instance, shows a pending-change warning, and requires Apply before changing asset selection. Applied edits use Undo and AssetDatabase saving. This is an explicit draft limitation, not automatic persistence of unfinished forms. Camera/visual/catalogue settings use serialized asset fields. Model overrides remain render-only and fit canonical dimensions; materials are saved references. Camera edits are presentation-only and root applies a last-valid-value policy.

Course JSON and manifests use bounded parsers and strict schema/catalogue/source/geometry checks. CreateContent validates exact source/seed and catalogue identity, then current mode, authored sites, gameplay rules and budget. Start records source-input and gameplay-content hashes across reload. The physical plan hash includes the exact manifest hash, so a different valid manifest cannot silently replace pinned play content. The Inspector does not write approved public catalogue identities or bypass admission.

Preview builds the real static CourseWorld and approved prop visuals in PreviewRenderUtility and disposes both on close. It does not claim dynamic puzzle completion. Play uses a real local client after Play Mode reload; this review is not evidence that it launched successfully. SessionState survives domain reload but is not a replacement for disk saves after an editor crash.

## New native fixtures (NOT RUN; no compiler slot)

Filter: `Wreckabulary.Tests.WordboundAuthoringPersistenceTests` (3 cases).

- Save CourseAsset, CameraProfile, VisualProfile, render prefab and material through AssetDatabase; mutate only unsaved memory, unload/reload, check persisted references and values, restore the same manifest/content identity, construct/dispose the real preview world, and reject stale play setup without changing scenes or claiming a session.
- Restore a saved scene plus an empty untitled additive scene and the original active scene. This refuses to start from dirty or populated untitled scenes to avoid discarding reviewer work; run from a clean test scene.
- Reject a missing saved scene without replacing current scenes or deleting the recovery record.

These are native save/reload/preview/setup tests, not a claim that a local runner, human controls or a complete race passed. Source-only `git diff --check` passes. Earlier 8824dba reference compilation predates this follow-up.

## Source pins at inspection

| File | SHA256 |
| --- | --- |
| CourseAsset | C9544D97F983FDCC5D74F35833E74B1A6444D84B6D6EF56DF60D878BC4985D9A |
| CourseAssetEditor | 208D6F81122701584BB02551C3CEB97BF1B1F4ECE09865447669971576019AFE |
| CoursePreview | A1E2FEAD14DBF8C35C56BB1A3522B0E8B2CC582693F0E18E3DA70A21090850B1 |
| CoursePlaytest, corrected | E9BD19C94E59E6E414B2A5FF6745559C390579DA5DC071F44716FFF0576EBC97 |
| AuthoringPersistenceTests | 799109CC4B53254396A30C45259A4719B79EF7A4D3B7A30AC0AC694596BA986A |
| Root Launcher, before cleanup follow-up | AB8F57F94393E26D194F8C2946C93A95486B450212272DFB12DD6BA046739F76 |
| Root LaunchAssets | 195313196C0C27BC9F506F61F5B0607233FD1C3EB304C4BE0B9BF56F05BF6AB9 |

Next action: root runs the setup entry point, both native authoring classes (7 existing + 3 new cases), and the real editor capture checklist. No screenshot exists yet.
