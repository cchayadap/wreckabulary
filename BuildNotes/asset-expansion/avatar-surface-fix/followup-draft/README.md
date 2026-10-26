# Avatar correction follow-up

STATUS: SOURCE REVIEW PASS — independent review and seven-file combined C#9 syntax/hash checks completed; native execution remains root-owned. No live Assets writes or native execution by this agent.

Final patch SHA-256: `0526660fdaf20b070354d37b3f8bfce04939c50053a6b6cfc382f9a292e9537f`. Independent findings and limitations: `BuildNotes/restart-20261026/guard-review.progress.md`.

This six-file follow-up applies after the original eleven-file avatar correction patch. Baselines are byte-exact current live sources, recorded in manifest.json. The original patch/evidence is unchanged.

## Stale source failure contract

The runtime profile intentionally uses cheap reference/layout checks. An FBX edited under the same GUID/local IDs can retain those checks but invalidate the saved mesh. Read-only `VerifyExistingProfile` now verifies the reviewed FBX/meta hashes, full imported mesh fingerprint, exact saved bindings and corrected geometry/skin/blendshape-position preservation at three editor boundaries: exiting Edit Mode to Play, build preprocessing and creation of a new player authoring model. A mismatch stops that operation and asks for a reviewed correction version. It never regenerates, overwrites or saves assets. Missing optional correction remains allowed at the Play/build/authoring boundaries. An author can deliberately disable automatic correction in its Inspector while editing the source; new/raw spawns then keep their original mesh. Existing saved renderer overrides remain assigned, as the tooltip explains. Nonrequired verification then bypasses the stale recipe. Required verification (the corrected portrait rebuild) rejects a disabled profile. Existing artist-authored preview/UpgradePlayer no-op branches remain intact.

There is no player-loop hashing or per-spawn runtime clone. Packaged builds use the assets validated before build; a deliberately disabled correction allows the author to proceed with their current renderer references. This is not support for hot replacement of source assets inside a running player; edits during an already running Play session still require leaving/re-entering Play. Three new native tests cover same-count source position edits on an owned clone, read-only repeated validation/build callback behavior, and the disabled/required policy on an owned temporary profile. Persistent FBX/profile bytes and dirty state are preserved; no source asset is mutated to test failure.

## Wrapped actual-player QA

Disabling automatic correction does not restore already assigned saved renderer references. New/raw spawns keep their original head, but existing corrected prefabs/previews keep that artist-visible override. To revert those, explicitly assign the original head mesh on the renderer. No automatic restoration is added because it could overwrite authored mesh choices.

Actual Wordbound artwork adds resolver-owned wrappers above the avatar. The head matcher now finds exactly one corrected renderer through the saved relative renderer path under the supplied instance. It still requires the exact original avatar, original→saved corrected head reference pair and every other ordered mesh reference unchanged. It does not accept any renderer by name alone. The new `WrappedPlayerAdmitsOnlyTheExactSavedHeadCorrection` test constructs the actual resolver player, passes its outer wrapper using the same traversal as the shipping QA, and rejects a custom head, changed second mesh and extra unrelated mesh.

## Root handoff

Review `avatar-followup.patch` and manifest hashes, strict-check then apply only through root. Let Unity generate metadata. Run R0 and the focused native `Wreckabulary.EditorTests.AvatarSurfaceValidationTests` plus `Wreckabulary.Tests.WordboundPresentationTests.WrappedPlayerAdmitsOnlyTheExactSavedHeadCorrection`; repeat strict authored-preservation tests if changed files warrant it. Native Play-transition cancellation itself is not claimed tested by the prepared tests. The shop importer follow-up will require this read-only verification entry point before rebuilding its six images.
