# Runtime save coverage — 2026-10-24

STATUS: READ-ONLY REVIEW COMPLETE. Historical acceptance and remaining current-build coverage are separated below. This lane ran no Unity, core, player or IO jobs and made no runtime/source changes. Root's build/smoke work remained untouched.

Scope: existing runtime `WorkshopStorage` and original `HomeStorage` save/readback/recovery paths, their tests, and saved Windows player evidence. The Editor's observed 1175 failures are not automatically attributed to these separate stores. The purpose is to identify exact missing acceptance, not authorize a blanket replacement or refactor.

## Source contract and bounded finding

Inspected integration `9e013dc9` during the frozen build. Runtime source blobs: `WorkshopStorage.cs` = `9ae81cffa32ef04c9d982fa681356cc355b84882`; `HomeStorage.cs` = `a2c852c68f16b5fe37ed8a42252f4b8cf3014e33`.

| Store | Current repeated-save and recovery behavior |
| --- | --- |
| `Scripts/Rules/Wordbound/WorkshopStorage.cs:82` | Writes a unique same-directory temporary file, `Flush(true)`, then `File.Replace(temp, primary, primary + ".bak")` for an existing primary. Recovery validates the primary, falling back to a validated same-course backup on supported read/format failures. `SaveShare` uses the same writer but `LoadShare` has no backup fallback. |
| `Scripts/Core/HomeStorage.cs:35` | Validates/export-serializes the layout, writes a unique temporary file, then `File.Replace(temp, primary, null)` for an existing primary. It returns false plus an error on failure. `TryLoad` validates the saved document and map; there is no backup/journal recovery. The original Home Save and BeginTour UI retain their existing error handling; BeginTour requires save plus readback before opening the tour. |

Both delete a remaining temporary candidate after failure. They therefore do **not** provide the Editor Store's new committed-revision recovery guarantee for the newest failed save after RAM/process loss. Workshop retains its editing history and existing five-second autosave retry while alive; Home leaves the unsaved draft in its UI history and reports failure. This is a source-visible difference in guarantees, not a reproduced 1175 defect. Workshop's temporary-file deletion can also throw and mask its original publication exception; no inspected receipt establishes that occurrence.

No source-proven reason requires blanket replacement of either API now. The named backup in Workshop, null backup in Home and absence of the Editor journal are materially different contracts. The inspected player logs contain an earlier long-path/missing-directory autosave failure (`175202Z`), not a numeric 1175 receipt for either runtime store. The Editor fix must not be generalized into runtime acceptance or an OS-cause claim.

## Existing tests and actual saved receipts

- **Workshop recovery: 11/11 PASS** in `TestResults/editmode_20261023T232239Z.xml` (also 224855 and earlier). `BackAndReopenLeaveBothRecoveryFilesUntouched` performs two changed saves and checks exact primary/backup bytes. `CorruptPrimaryRequiresChoiceAndRecoversTheValidBackup` proves valid backup recovery after deliberate primary corruption. `SavedCourseChoiceArchivesExactPrimaryAndBackupBeforeAllowingSaves` preserves both files exactly, then saves a further changed draft. Other cases retain explicit choice, focus, source identity and archive-failure protection. These use actual filesystem IO and the runtime storage, through Editor tests.
- **Home: 11/11 PASS** in `TestResults/editmode_20261020T094432Z.xml`. `ASecondValidSaveReplacesTheSameMapWithoutChangingTheOtherMap` actually changes and replaces one map, reloads it, and checks the other map. Both-map round trips, malformed/mismatched reads, initial export and traversal rejection pass. `RejectedSaveLeavesPreviousFileExactlyIntact` fails validation **before IO**; it does not exercise a locked/denied replacement. Home has no tested backup behavior to claim.
- **Rules harness:** `WorkshopTests.AutosaveFallsBackToPreviousValidSnapshotAndRejectsTraversalIds` PASS in `TestResults/core_20261023T135423Z.trx` (0.222458 s). It makes a changed second save, tests corrupt/missing-primary backup fallback and initial share export/load. This is separate .NET harness evidence, not a Unity player test.
- No inspected existing tests exercise actual locked/read-only runtime replacement followed by release/retry, failed-newest-save process loss, or changed **second** share/export publication. The Editor Store's new 36-case suite covers its own implementation only.

## Current native selection versus player gap

The smallest existing native rerun is **22 cases**, selected by these exact class filters; neither class declares a custom category:

1. `Wreckabulary.EditorTests.WordboundWorkshopRecoveryTests` — 11.
2. `Wreckabulary.Tests.HomeStorageTests` — 11.

This reruns real changed replacement/readback plus Workshop backup recovery on the current Unity 6000.6.4f1 environment. Historical receipts above are retained, not relabelled as current-patch results. Additional storage fault tests should use a narrow fixture isolated from owner data; no production change is justified merely by scheduling these checks.

Actual `docs/qa/runs/20261024T005757Z-visible-wordbound_workshop_flow/checks.json` is **5/5 PASS**, build `20261024T005254Z`, Unity **6000.6.3f1**. Its `edit_workshop` probes verify a fresh isolated 183314-byte primary, exact edited JSON and recovered snapshot, `used_backup=false`, unchanged history and zero temporary files. It then verifies playtest/return and in-memory Undo/Redo. The scenario makes one authored edit and waits for that autosave; it does not assert a previously persisted different revision, exact previous `.bak`, a second changed readback, or a new-process recovery. Undo/Redo occurs without a disk readback assertion. The equipment scenario similarly checks its initial autosave; it adds no repeat-save acceptance. No Home Windows QA scenario/receipt was found in the inspected scenario sources and saved run directories.

The separate player acceptance gap is therefore: in an isolated QA folder, persist revision A through the real autosave route, make a second meaningful edit B, wait for exact B primary and A backup with no temporary residue, then close/reopen the workshop and finally relaunch the player against that same owned folder to validate B recovery. Keep backup-corruption/fault injection isolated from owner data. For original Home, preserve its existing direction: a separate Save → changed Save → Load/BeginTour check should use its current APIs/UI and original map content, not assume Wordbound covers it. None of this was executed or added here. At the original review boundary, official smoke `035028` was pending. It subsequently passed 5/5; its unchanged scenario does not close the second-revision/restart gap. See [the actual receipt](official-short-editor-pass-20261024.json).
