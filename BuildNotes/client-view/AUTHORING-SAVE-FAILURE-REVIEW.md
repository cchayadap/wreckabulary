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

## Proposed bounded durable-recovery follow-up — NOT IMPLEMENTED

The current gap is loss of discoverable newest recovery after RAM is lost, not an established deletion in the failed publication path. `Save` has no catch/finally deleting `.tmp`; Windows may leave the complete file after a replacement failure. The next save overwrites that same filename, `Load` never examines it, and `Clear` removes only the primary file and dictionary entry. Therefore an orphan might contain the newest complete edit, an older failed edit, or a partial later write. Simply preferring `.tmp` on open would silently resurrect stale data or accept an interrupted write. A no-op save matching the primary can currently leave such an orphan too.

The smallest robust proposal needs an explicit disk ordering/commit distinction. Keep this entirely in the existing Editor draft store and Inspector, with no game/course/network schema changes:

1. Add a versioned **Editor-only recovery envelope** with monotonic revision, asset GUID, saved-source hash, document and a checksum over its exact serialized payload. Continue reading existing v1 primary files. Revision is per asset, derived from its recognized persisted records; do not use file modification times to choose newest edits. Revision overflow, duplicate revisions with differing payloads or unsupported versions fail closed.
2. Before attempting primary replacement, write the new envelope to a fresh, owned staging file in the same directory, flush/close it, then read it through the bounded recovery parser and verify exact bytes. Publish it to a fresh revision-specific pending filename using a same-directory `File.Move` to a name that must not already exist. This move commits an independently recoverable record without replacing/deleting the old primary. A failed or partial staging file is not an automatic recovery candidate. Creation/name collisions must fail safely, not overwrite another candidate.
3. Keep that committed pending file intact while copying its validated bytes to a separate publication temporary and making the existing `File.Replace` / initial `File.Move` primary operation. On failure, preserve the previous primary, committed pending record and newest in-memory draft; expose the original IO error. This improves crash/reload recovery without claiming to cure replacement failures or making unsafe `Delete(primary); Move(...)` substitutions.
4. On open, validate all eligible committed records with strict UTF-8, the existing 4 MiB/document-structure/GUID/source binding checks and new revision/checksum checks. Select the highest consistent valid committed revision; primary and sidecar copies of that same revision must agree. A corrupt or unreadable committed newer record must produce a recovery warning/choice, not silently load an older primary as if nothing happened. Lower-revision leftovers cannot override a newer primary. Keep authored-content validation at Apply: incomplete/invalid user form edits still need recovery.
5. Repeated saving of an identical latest pending payload should retry publication of that record instead of generating one sidecar every five seconds. A deliberate edit back to the old primary's content is a **newer** revision when a different newer pending record exists; the byte-identical-primary fast path must not erase that ordering. Only after a newer committed record is verified may obsolete owned staging/pending files be cleaned; cleanup failure must leave harmless older revisions and report its limited state. Never prune the sole complete newest candidate before a replacement exists.
6. On successful primary publication, verify/read back that committed revision before removing its duplicate sidecar. If cleanup fails or the process exits between publication and cleanup, reopen sees equivalent same-revision data. For explicit Discard or clearing recovery after successful Apply, first commit a higher-revision, checksummed **discard record** through the same fresh-file path; Load treats that as no pending form and never revives older records. Only then remove obsolete owned records. Retain the discard record while any old primary/sidecar remains; remove it last once all older files are gone. Failure to commit the discard must leave existing recovery intact and report failure. Failure of later cleanup must not undo the already committed discard. This tiny record avoids an ambiguous partially completed deletion sequence.
7. Legacy `.tmp` has no revision or trustworthy completion marker. Offer a separately labelled, validated **explicit** recovery choice when it differs from the primary; never call it newer based only on its timestamp. Preserve corrupted/ambiguous legacy evidence, and require explicit discard rather than silent deletion. Same bytes can be treated as a duplicate after full validation.

This is a process-crash/domain-reload recovery design, not a guarantee against disk failure or power-loss durability. If required validation/reads or the first committed pending write cannot succeed, the original memory-only warning remains necessary; do not promise disk recovery in that state. Use existing supported Editor file APIs; native tests must verify the installed Mono/Windows behavior. No new generic storage framework, retry worker, runtime service or public authoring format is proposed.

Minimal proposed source scope is `WordboundCourseDraftStore.cs`, the existing `WordboundCourseAssetEditor.cs` recovery/status/choice path, and `WordboundAuthoringPersistenceTests.cs` (or a narrowly split Editor draft-store fixture if test size requires). The Inspector should distinguish “newest edits saved locally; main draft publication failed” from “newest edits exist only in memory”; keep the original IO failure visible and the saved course asset unchanged until Apply succeeds. No test-only private file surgery should be added to production APIs.

Meaningful acceptance cases, alongside the existing 13:

- Actual read-share lock causes changed publication to fail, but a committed pending record survives clearing the process-static dictionary and recreating the Inspector; newest exact document and original saved asset/primary bytes are independently asserted. Releasing the lock then permits normal publication.
- Interrupted/partial staging is never selected; corrupt, oversized, mismatched-GUID/hash and conflicting same-revision committed records remain untouched and yield an explicit recovery failure/choice. An older valid record is not silently presented as newest.
- A newer primary beats a stale older pending record even when timestamps are reversed; equivalent primary/pending revision is harmless after interrupted cleanup. Newest pending beats older primary. Reverting content to old primary data creates a correctly ordered new edit.
- Legacy complete orphan versus primary requires an explicit choice; damaged legacy bytes survive close/reopen. Explicit discard or successful Apply cannot resurrect leftover older edits on next open, including cleanup-failure behavior.
- Two failed successive distinct edits preserve the latest complete record if a subsequent staging write is interrupted. Identical retries do not create unbounded duplicate records, and all records remain under the per-file size bound.
- Existing genuine changed-save/BOM, strict source-change choice, invalid form retention, saved-asset integrity and scene/theme persistence tests continue unchanged in intent. Use controlled owned temp assets and real file locks; do not manufacture the historical unexpected Windows error or call its cause fixed.

Implementation is awaiting root's selection of the bounded envelope/cleanup contract. No source or file-recovery behavior was changed in this review.
