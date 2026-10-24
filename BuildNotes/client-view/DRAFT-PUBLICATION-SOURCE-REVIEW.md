# Windows Inspector draft publication source review

STATUS: COMPLETE — bounded SOURCE PASS for Input commit `c2a771e8ab4076cbe2fc2e6758fac21b5cd99527`. Exclude baseline-only `71c0b8f1`. No Unity, compiler, filesystem lock test or native API invocation was run by this lane. Native repair acceptance remains pending.

Review boundaries: durable record commit precedes primary publication; lock/publication failure retains the newest valid draft and the original error; no deletion of the old primary as replacement fallback; correct Windows API signature and error handling; unchanged non-Windows behavior; existing failed native cases retained as evidence until rerun.

## Exact reviewed source

The final commit contains the same independently rehashed source as the frozen review:

- `Assets/_Project/Scripts/Online/Authoring/Editor/WordboundCourseDraftStore.cs`: SHA-256 `0E9799A760D919F1907FE1CFDF8D700E94D49350873A9A1DA1405DD864073533`.
- `Assets/_Project/Scripts/Online/Authoring/Tests/WordboundAuthoringPersistenceTests.cs`: SHA-256 `3A99E7E6DCB6C27AFBFDE65767EA93AB1F2D0A36AC14C9A2FE81639F3702261A`.

The Store delta adds the private platform-specific publication helper and changes only the existing-primary replacement call. The test delta adds one Windows read-only-primary regression; the prior 35 cases, including the two actual failures, are retained. The resulting persistence fixture has 36 source-counted cases; together with the unchanged 12 local gameplay cases this gives the parent's intended 48-case rerun, not an executed result.

## Confirmed failure baseline

Root `TestResults/editmode_20261024T032722Z.xml` records 47 total, 44 passed, three failed and zero skipped on source `3fb06f37`. Persistence was 33/35; local gameplay was 11/12. The separate initial-keepsake observation-order correction is reviewed in `NATIVE-EQUIPMENT-READINESS-20261024.md` and is not part of this publication change.

The two persistence failures are `IdenticalDraftBytesAvoidReplacementButChangedLockedContentRemainsRecoverable` and `ReleasedResidueLocksAllowSaveOrDiscardWithoutLosingTheLatestCommittedCandidate(False)`, both at the managed `File.Replace` call. In the first case, the expected locked save recorded `0x80070020`; the later unexpected `store_bom` attempt explicitly recorded `0x80070497` (Windows error 1175), `memoryOnly=False`, an unchanged old primary and a complete expected temporary, with both later exclusive-open probes succeeding. This newer run therefore identifies a numeric error that the earlier historical message-only failure did not capture. The numeric code does not establish the underlying Mono, filesystem or filter cause. The released-cap Save failure contains the same message without its own numeric HRESULT; the Clear case passed.

## Source assessment

- `SaveDraft` still commits the revision/checksum/GUID/source-bound pending record before attempting primary publication. It flushes the owned same-directory `.publish` file, replaces the primary once, verifies exact bytes, and only then clears the RAM draft and reclaims records. A publication exception therefore leaves the latest complete committed recovery record. There is no old-primary deletion, retry, cross-volume copy flag or fallback that could disguise the error.
- `ReplacePublished` verifies source and destination directories after full-path resolution. Under `UNITY_EDITOR_WIN`, its `MoveFileExW` declaration uses Unicode strings, exact entry point, Win32 BOOL return marshalling and `SetLastError=true`. Flags are `REPLACE_EXISTING | WRITE_THROUGH`; the failed-call error is read immediately and retained in the thrown `IOException` message and HRESULT. The declaration is absent outside Windows Editor; the existing `File.Replace(temporary, path, null)` remains the non-Windows path. The native declaration and flag meanings agree with Microsoft's [MoveFileExW contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw).
- This changes publication to replacement by rename for private draft files. It does not promise `File.Replace` metadata-merging semantics, arbitrary ACL/stream preservation or universal power-loss durability. `WRITE_THROUGH` is not evidence of those broader guarantees; the committed record remains the recovery mechanism.
- The new `ReadOnlyPrimaryRejectsPublicationButRetainsTheExactCommittedDraftAfterMemoryLoss` uses an owned temporary asset and real read-only file attribute. It requires access-denied HRESULT `0x80070005`, unchanged primary and saved-course bytes, a complete pending record identical to `.publish`, and correct recovery after clearing static memory. Its `finally` restores attributes. The successful retry must publish those exact committed bytes, remove staging/old records, and remain reloadable after another memory reset. The test does not substitute a synthetic failure for the two existing native regressions.

No concrete source blocker remains in this bounded delta. A Windows native rerun must still verify the original BOM and released-residue cases, the new access-denied case, ordinary saves and Inspector cleanup/reopen behavior. Source review does not establish that this API resolves the observed failure on this machine.
