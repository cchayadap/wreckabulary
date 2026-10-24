# Draft primary publication repair — native 032722

STATUS: IN PROGRESS — actual failure preserved; no compiler/native/probe execution in this lane.

Root assigned the Store/Inspector/current persistence-test slice after native `20261024T032722Z`, integration source `3fb06f37`: 47 cases, 44 PASS / 3 FAIL / 0 SKIP. Two failures are persistence publication errors; the separate first-frame keepsake fixture belongs to Assets. Only the owned lane is writable. Root owns every Unity/core/native/build/player job.

## Exact first evidence

- `IdenticalDraftBytesAvoidReplacementButChangedLockedContentRemainsRecoverable` fails at test line 221, **store_bom**, after the released-lock save already succeeded. Store line 101 calls `File.Replace(.publish, primary, null)`. Actual exception is IOException, HRESULT **0x80070497** (1175), "Unable to remove the file to be replaced". The intentional preceding read-share-lock failure is separately **0x80070020** (32), as expected.
- Failure diagnostics report primary length 2587, byte-equal to the prior BOM representation and not the desired bytes; `.publish` length 2584, byte-equal to the desired replacement. Both have attributes 8224 and both permit an immediate exclusive read/write open. `memoryOnly=False`: the validated committed sidecar already protects the new draft. These observations distinguish the reproduced replacement error from an intentionally still-held sharing lock; they do not identify an OS/filter/Mono root cause or guarantee that no transient lock existed earlier.
- `ReleasedResidueLocksAllowSaveOrDiscardWithoutLosingTheLatestCommittedCandidate(False)` fails at test line 799, the Save after disposing all eight residue locks, through the same Store line 101. It has no detailed HRESULT/file-state output. The True/Clear variant is not one of the failed cases.

Raw source: root `TestResults/editmode_20261024T032722Z.xml`, exact failed test-case failure/output elements; corresponding `Logs/gate_test_editmode_20261024T032722Z.log`. Earlier 224855/225846 unexplained replacement failures and later passing controls remain valid history; this run now captures 1175 numerically for the BOM phase. Neither surviving recovery bytes nor earlier passing tests converts these failures to success.

Next: align only required source blobs with root in an explicitly excluded baseline if needed; inspect primary-publication alternatives which retain complete committed recovery before replacement, exact post-publication bytes, original lock rejection and bounded storage. No blanket retry, primary deletion, test weakening or journal-format change.
