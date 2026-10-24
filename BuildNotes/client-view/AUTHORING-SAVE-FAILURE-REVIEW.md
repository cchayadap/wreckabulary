# Authoring save failure evidence review

STATUS: COMPLETE — bounded read-only evidence review; historical cause unresolved. No tests, Unity, filesystem repair, installer polling, or runtime/source edits.

Question: what exactly failed in the earlier Inspector/draft-save IOException 1175 evidence, what do the later 13 native authoring passes establish, and is there evidence connecting this failure to the separate long-path Unity installation omission?

Scope: current delivery/progress statements, existing authoring persistence/recovery reports, their exact test/log artifacts, and the save implementation. This report will preserve observed failures and distinguish closure of a tested path from general filesystem reliability.

## Confirmed findings

- The two unexpected failures are existing-file publication failures in `File.Replace(temporary, path, null)`, not creation of the folder, serialization, or initial creation with `File.Move`. Native XML `224855` points to `WordboundCourseDraftStore.Save:46`; `225846` points to `Save:52` and the first save after the test's read-share lock scope ends. The later BOM-normalization section was not reached in that second failure.
- Both unexpected messages are `System.IO.IOException: Unable to remove the file to be replaced`. Their numerical unexpected HRESULT was not retained. The recorded `0x80070020` belongs to the intentionally locked-save control and must not be substituted for the unexpected failure. The historical report's 1175 classification is a message/call-path interpretation, not a directly logged unexpected code.
- The save target is `Library/Wordbound/InspectorDrafts/<32-character AssetDatabase GUID>.json`, with adjacent `.tmp`. Under the actual integration checkout those normalized paths are 140 and 144 characters. The historical individual GUID was not captured, so the exact original basename is unavailable. This is separate from the later Unity installer omissions at 260 or more characters.
- Native `233437` has 13/13 authoring cases passing (13/14 overall, with an unrelated FFA failure). This includes changed locked content recovery, save after release, BOM normalization, real draft payload replacement, null/named-backup controls, saved course/profile content, custom/default theme persistence, and scene restoration. The raw source and result limitations will be recorded below.
- Current Inspector behavior preserves newest failed pending content in a static in-memory dictionary, retries a failed save after five seconds, displays a warning to resolve it before script reload/Unity shutdown, and prevents Apply from overwriting the saved course until the pending backup succeeds. In-memory retention is not durable recovery across process exit or domain reload.

## Exact failure and subsequent evidence

All timestamps below are UTC on 2026-10-23. The XML filenames are `TestResults/editmode_20261023T<timestamp>Z.xml`, with matching `Logs/gate_test_editmode_20261023T<timestamp>Z.log`. The logs identify installed Unity `6000.6.3f1 (45d8eee7de74)` under `C:/Program Files/Unity/Hub/Editor`; these runs predate the separate isolated `.4` installer attempt. Per-case failure/output evidence is in XML.

| Run / source | Observed result | What it establishes |
| --- | --- | --- |
| `224855` / `dc60af3b` | Overall 108/111; authoring 7/8. `RepeatedDraftRecoveryPreservesExactValidZoneCollections` failed after 0.434444 s. | Existing draft replacement threw at Store line 46 / test line 50. The loop saves identical content three times; the recorded failure does not identify which replacement iteration. |
| `225846` / `73a3326c` | Overall 17/18. Repeated-draft case passed; `IdenticalDraftBytesAvoidReplacementButChangedLockedContentRemainsRecoverable` failed after 0.362650 s. | The explicit locked-save control passed its retention assertions and reported `0x80070020`. Unexpected failure was the first Store save after that lock's `using` block, test line 210. It was not the expected lock exception or a BOM failure. |
| `231117` | Null/named native publication control passed, 0.497958 s. | Both backup choices worked fresh and after releasing an intentional read-share lock. This does not establish a defect specific to a null backup. |
| `233231` | 3/3 overall; changed/locked/BOM Store case and real-payload sequence both passed. | The previously failing Store sequence was no longer reproduced. Test-only failure facts were added; there was no new changed-content publication fallback. |
| `233437` / `b36ce231c8420db67e64860e90c0707ca8bb3a1e` | All 13 authoring cases passed, zero skipped; overall 13/14 with a separate FFA failure. | Real Store behavior, retained recovery, themes, authored content and scene restoration passed together. This is finite native regression evidence, not proof that every future OS write will succeed. |

The `233437` authoring suite explicitly includes:

- `RepeatedDraftRecoveryPreservesExactValidZoneCollections`.
- `IdenticalDraftBytesAvoidReplacementButChangedLockedContentRemainsRecoverable`.
- `NativeDraftSequenceRewritesRetainedTemporaryAfterComparisonRead`.
- `NativeReplacementPublicationReportsNullAndNamedBackupFileState`.
- `ChangedSavedCourseRequiresExplicitRecoveryChoiceAndSuccessfulApplyClearsDraft`.
- `DamagedPendingFileIsRetainedWhenInspectorCloses`.
- `InvalidPendingFormSurvivesClosingInspectorAndRejectedApplyWithoutChangingSavedCourse`.
- `LockedRecoveryFileKeepsNewestEditsThroughSavedChangesAndInspectorRecreation`.
- `MissingSavedSceneKeepsCurrentSetupAndRecoveryRecord`.
- `RestorationRetainsSavedSceneBesideUntitledAdditiveSceneAndOriginalActiveSelection`.
- `SavedCourseProfilesPrefabAndMaterialsReloadIntoTheExactPreviewAndPlayContent`.
- `SerializedThemeColoursMetricsAndOriginalFontReferencesSurviveSaveAndReload`.
- `AuthoringSetupPreservesReloadedCustomThemeAndExistingDefaultThenFillsOnlyAnEmptySlot`.

Actual `233437` control outputs preserve these boundaries: `null_fresh`, `null_released`, `named_fresh`, and `named_released` reported no exception, new target bytes, and no remaining source. Named successes retained old bytes in the backup. Both `*_locked` cases reported `IOException / 0x80070020`, original target bytes and the new source still present. `actual_payload_compare_rewrite_released` likewise reported new target bytes and consumed source after the exact comparison/read/rewrite sequence. The expected lock error is evidence of the deliberate test lock only.

## Source contract and classification

Reviewed current root `4bbc31f672b1e83dc2fdfcde0ee4a2cd832d2aee` and historical source with `git show`. Relevant files:

- `Assets/_Project/Scripts/Online/Authoring/Editor/WordboundCourseDraftStore.cs`: records serialized pending data before IO; publishes an adjacent temporary file with `File.Replace(temporary, path, null)` or initial `File.Move`; clears memory-only recovery only after success.
- `Assets/_Project/Scripts/Online/Authoring/Editor/WordboundCourseAssetEditor.cs`: `TrySavePendingDraft`, `SaveDraftWhenDue`, and `Apply` preserve the warning/retry/backup-before-apply behavior described above.
- `Assets/_Project/Scripts/Online/Authoring/Tests/WordboundAuthoringPersistenceTests.cs`: isolated owned test assets; later `SaveDraftWithFailureFacts` records bounded file/error facts and rethrows the original exception, without retrying or replacing it with success.

The sole Store change since `dc60af3b` is commit `31c6ff52`: encode exact UTF-8 bytes once, skip publication when existing bytes match, otherwise retain changed-content atomic replacement. `SameBytes` uses a scoped `FileStream` whose disposal precedes publication. This removes unnecessary identical saves and explains why the original repeated-identical path no longer requires replacement; it does not identify or repair the unexplained changed-content failure. Later passing changed-content/BOM cases are therefore necessary evidence, not redundant with that optimization.

The historical `INSPECTOR-DRAFT-IO-REVIEW.md` records installed Mono's `FileSystem.ReplaceFile` calling Windows `ReplaceFileW` then mapping its error; both failed XML stacks have the same mscorlib module identity. Microsoft's documentation identifies 1175 as `ERROR_UNABLE_TO_REMOVE_REPLACED` and says the replaced/replacement names remain intact for that result. Since the unexpected numeric code was not captured, retain this as the supported message classification rather than direct code readback. [ReplaceFileW documentation](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew), [Windows error codes](https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--1000-1299-).

No artifact identifies a blocking process, antivirus event, permissions change or exact native failure sub-operation. The earlier report observed timestamp-associated `.tmp` files, but test teardown removed primary assets/files and the precise original GUID was not logged. A current bounded read found no matching historical temporary files in that timestamp window; this review makes no claim to have recovered their original disk/lock state.

## Relationship to the Unity installation issue

There is no evidence that the later long installation path explains these draft-save failures. The failed operation already reached an existing project-local draft at a 140-character normalized path, with a 144-character temporary path; it did not access the isolated Editor's missing Sprite/Core files. The `.4` installation diagnosis concerns omitted installed package files at 260 or more characters, before package compilation. The two failures differ in time, selected Editor, path length, operation and artifact scope. Treat them as separate issues on available evidence; do not close the authoring warning because a shorter Editor installation succeeds.

## Disposition and next recoverable action

Keep the delivery limitation, with precise wording: **An earlier intermittent Windows draft-file replacement failure remains unexplained. All 13 later native authoring regressions passed, including changed-content save/recovery. A failed save keeps newest edits in memory and blocks Apply; resolve the warning before closing Unity or reloading scripts.** No additional replacement workaround is justified by this review.

If the root schedules an ordinary authoring rerun, retain the existing exact Store tests and failure-only facts. On any recurrence, preserve the numeric unexpected HRESULT, stage and target/source state before test teardown; do not attribute the error to a process or change atomic publication without that evidence. No new run or user action was requested here. Input's independent original investigator confirmed this classification and reported no contrary evidence.

Evidence SHA-256 read during this review:

| Artifact | SHA-256 |
| --- | --- |
| `224855` XML | `3d2a518a05f04d9e20d1a4ec4c130a96d4a447e07838da9ec8743c3d1351ce98` |
| `225846` XML | `db5b49ba46801dd9d7864a85a91cfe6d6a50b68a23ad70eb41e25863e1dca229` |
| `233437` XML | `1d2a363b6f43014beb5e6671c4458c32e44414e2c8fc680fa6a86d030a241ac1` |
| Current `WordboundCourseDraftStore.cs` | `e3835936d586a66a7679b1747ccc41afb0497a820f7fd83128ab5035e176a44b` |

Related preserved reports: `INSPECTOR-DRAFT-IO-REVIEW.md`, `NATIVE-FFA-AUTHORING-233437-REVIEW.md`, `UNITY-PATCH-PACKAGE-DIAGNOSIS.md`. Installer polling remains stopped. Only this report was written; unrelated logs and all source/evidence files were preserved.
