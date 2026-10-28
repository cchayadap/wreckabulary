# Creator share bundle integration

STATUS: SOURCE VERIFIED DRAFT — no live production sources changed; no Unity/native run.

The existing Share action exported only course JSON, losing the selected random seed and mode/format. This candidate adds a bounded playable-map bundle and retains course-only draft sharing.

Frozen manifest SHA256: `feb6a99a76aeecbfcb87a2cf62c39da47fca99d78f39fd7829464119ffcf5e39`.
Base commit: `77eb9a0e74209fe8552f21caf9d3e658741c5f96`.

## Exact scope

| Candidate path | Change |
| --- | --- |
| `Assets/_Project/Scripts/Rules/Wordbound/WorkshopShare.cs` | New strict codec and prepared import transaction. |
| `Assets/_Project/Scripts/Rules/Wordbound/WorkshopStorage.cs` | Add separate `.wordbound.json` save/load; legacy methods unchanged. |
| `Assets/_Project/Scripts/Workshop/WordboundWorkshop.cs` | Share selector, bundle import, restored mode/format, public import method for native tests. |
| `Assets/_Project/Tests/Rules/WorkshopShareTests.cs` | 35 focused test cases. |

All paths are under `candidate/` here. Compare the two existing live files against `base-manifest.json` before integration; do not overwrite concurrent changes. Add the two new C# files through the root's normal Unity import process and retain their generated metas. No metas, scenes, assets, package changes or harness edits are supplied by this lane.

## Format and boundaries

`wreckabulary.workshop-share`, schema 1, contains exactly: format, schema, course (embedded canonical course JSON), seed, mode, raceFormat, catalogueHash, modeHash. Only Solo/Duo/Squad strings are accepted, and only the mode/format combinations already enabled by the map and approved by `WorkshopModes` are allowed. The bundle cannot supply rules, scripts, model URLs, arbitrary content definitions or editor selections/locks.

The UTF-8 bundle bound is 4,198,400 bytes (`2 * CourseLimits.DocumentBytes + 4096`), allowing JSON escaping of a bounded 2 MiB embedded course. The embedded document is independently parsed through the existing strict course schema and limits. Existing depth, token, unknown/duplicate-key and Unicode checks remain active. Export and new-bundle import run the existing deterministic resolver for the exact seed and mode, then physical puzzle-site validation. Incomplete maps remain importable/exportable as legacy Draft only.

Catalogue and mode hashes must match the current approved catalogue/rules. These are compatibility checks, not signatures or public admission. Importing a template bundle while a different catalogue is active can therefore reject it; open the matching authoring source/version first. Future catalogue migration or automatic source selection is separate work.

## Visible flow

Share opens **Share map**, defaulting to **Map + settings** when the current map is playable. Its single caption is **Keeps seed and mode · private maps**. **Draft only** uses the caption **Course only · no seed or mode**. Existing Copy, Paste, Import, Save file and Done controls remain.

If the current map cannot export a playable bundle, Share still opens on Draft only and reports the first concrete validation issue. This leaves Paste/Import available to repair or replace an incomplete draft. Switching export type prepares its result before replacing text. Import detects the pasted format independently of the export selector. Save file saves the current edited map, retaining the prior control's behavior; pasted text becomes the current map only after Import succeeds.

## Mutation behavior

New imports parse, validate compatibility and resolve before touching history, selection or the chosen mode. `WorkshopShareImport.Apply` installs map, seed and initial editor state through one existing `WorkshopHistory.Execute` transaction. Only after that succeeds does the UI assign the already validated immutable mode requirements, invalidate previews and refresh. Invalid imports retain the current map, seed, mode, selection, Undo/Redo and gestures. Recovery-choice and online-start guards run first.

Imported playable seeds are locked by default. New shares intentionally exclude editor-only selection, hidden/locked layers, groups and grid size. Legacy course import retains the previous default seed behavior (`0000000000000001`) and leaves the chosen mode unchanged because neither is supplied by the legacy file.

Map and seed import is one Undo/Redo step. Mode selection remains a separate session choice, as before; Undo does not restore the earlier mode, and an old map that lacks the new chosen mode shows the existing choose-a-mode guard. Recovery/autosave format is unchanged, so persistence of selected mode across restarting the editor is still outside this bounded fix. This limitation must not be described as completed full creator session persistence.

## Evidence and remaining checks

- `focused-tests.json`: 35 passed, 0 failed using the unchanged engine-free RulesHarness runner/shim and the new fixture. All nine supported mode/format combinations preserve byte-identical resolved manifests. Both legacy course schemas import. Tests cover rejected settings/assets/scripts/oversize/Unicode/depth/compatibility, failed import preserving redo and selection, one-step import Undo/Redo, editor-state isolation and both file formats.
- `source-check.json`: Roslyn C#9 semantic compile against the current Unity references, zero errors for Rules (75 source files), main runtime (178), Rules tests (61). No Unity invocation or DLL writes to the live project.
- The initial attempt to include the old WorkshopTests/WorkshopModeTests in this small engine-free probe hit existing shim omissions (`Assert.Contains`, `StringAssert`). No old tests or harness files were modified; those complete native test sources compiled successfully with Unity's NUnit reference. Their execution remains for root/native validation.
- Live WorkshopStorage and WordboundWorkshop SHA256 hashes still equal their original base-manifest values at freeze.

After integration, root must run the normal rules/native gates and exercise the actual Share panel at small viewport and maximum UI font, including keyboard focus, paste, selected-mode/format restoration, online/recovery guards, Undo and draft fallback. Then rebuild/smoke the changed Windows artifact. The current UI/map delivery remains independent until this candidate is integrated and tested.

Public matchmaking remains unchanged: a local share is not a moderated or signed public-map admission. Arbitrary user-authored gameplay rules, new objective composition, custom scoring and public map discovery remain open creator-plan work.
