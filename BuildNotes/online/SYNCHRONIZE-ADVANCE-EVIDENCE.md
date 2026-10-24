# Synchronize team-copy reuse: independent measured evidence

STATUS: PASS — exact .NET allocation/correctness comparison verified; native public acceptance remains FAIL.

Scope: read-only verification of the integration checkout's exact before/after
`ValidationProbe --advance` artifacts, source manifests, state identities and
reported allocation comparison. This review runs no compiler, probe, Unity job
or player and changes no runtime source.

The candidate is `70cd4c6b`, integrated as `9168a657`. The before source is
`cd4ec714dba7652104144356bc1004d6b8969944`; the after source is
`839bfd3423f9fd231ff6d7d9c7e13067b0db69f3`.

## Artifact and workload verification

Independently read both raw logs and both source manifests, recomputed their byte
counts/SHA256, parsed every batch field, and compared the results with integration
`BuildNotes/client-view/synchronize-advance-comparison-20261024.json`. No mismatch
was found. Paths below are relative to the integration checkout.

| Artifact | Bytes | SHA256 |
| --- | ---: | --- |
| `.harness/delivery/advance-probe-before-0029.log` | 6492 | `d6cc4763f7cf510999ab0359f9a680467a11105bd3b4bf30b31effc04f84a98e` |
| `.harness/delivery/advance-probe-before-0029-source.json` | 13667 | `44f4674878985df8275968a5c60257d39240fbf053c24bc0cc14e56c061e4f3c` |
| `.harness/delivery/advance-probe-after-0038.log` | 6492 | `2a87969aafd1b03afbf07c3f8a00dfa549f1439ca2bfd21e09560b675309f6c3` |
| `.harness/delivery/advance-probe-after-0038-source.json` | 13667 | `0863e972d69219744f21a5789c46bac37dbe1f7ceac2b4b414b55158bf96ba8f` |

The manifests contain the same 78 paths. Exactly one measured source hash changes:
`Assets/_Project/Scripts/Rules/Wordbound/AuthoritativeMatch.cs`, from
`3130a6f5f6a934bb1cd4b6041b3b190b5b029646846d6fddc876a89cabf1da0e`
to `42e76de9b1699fa24a4427685244da1c19b5905062cd14050fe6956da061eb23`.
The Git diff across the recorded sources is the reviewed per-call lazy team-copy
reuse and `HasPuzzle` overload. All 78 after-manifest hashes also match the current
integration files at review. Probe Program remains
`7ed324a6296610eb934a5fa3cade8df810caa4141b828e65ebd82d68fd3e9277`;
the measured content and remaining runtime sources are unchanged.

Both runtime rows are identical: .NET 9.0.15, X64, Windows 10.0.26200,
current-thread managed allocation measurement, no Unity runtime. Both complete
input rows match exactly, including roster, fixed seed `0123456789abcdef`, item,
source/effective rule, geometry, mode, course, manifest, binding and initial
snapshot hashes. This uses the actual flagship content in a local probe binding;
it is not public admission.

Both runs report six batches, 360 measured Advances, 64 warmup Advances and zero
failures. All six corresponding batch rows are identical except `bytes` and
`milliseconds`, including every start/end snapshot hash. Each advances 60 ticks,
returns zero command receipts, retains previous snapshot bytes and unchanged
counts, and has an empty full-validation error. Both positive controls in every
batch report 65,560 bytes for a retained 65,536-byte payload.

The batches are ticks 32→92, 92→152 and 152→212 with supplied canonical wall-clock
values 544→1564, 1564→2584 and 2584→3604 ms; these are deterministic inputs, not
measured execution times. TimeRun retains 94 bodies / 57 items / 22 tiles /
294 resources / 216 mechanisms. TeamRace Solo retains 186 / 114 / 44 / 588 / 432.

## Measured result

Each entry below is one complete 60-Advance batch, in original order. No elapsed
sample or allocation outlier is removed.

| Workload | Before allocated bytes | After allocated bytes | Median byte reduction |
| --- | --- | --- | ---: |
| TimeRun | 15,623,448; 15,623,448; 15,623,448 | 11,640,888; 11,640,888; 11,640,888 | 25.4909159617% |
| TeamRace Solo | 31,200,584; 31,200,584; 31,200,744 | 23,219,856; 23,234,848; 23,219,856 | 25.5787776280% |

| Workload | Before elapsed ms | After elapsed ms |
| --- | --- | --- |
| TimeRun | 76.750; 78.005; 79.348 | 43.232; 48.437; 42.172 |
| TeamRace Solo | 237.306; 331.591; 420.266 | 168.023; 196.211; 222.753 |

Medians were recomputed from the raw byte values, using
`100 * (1 - median(after) / median(before))`; they match the root comparison.
All elapsed samples are lower in this pair of standalone runs, but machine
scheduling, runtime warmup and three-sample variation are not controlled enough
for a general speed claim. The measurement covers idle whole `Advance`, including
its rollback capture. Publication, ACK and full snapshot validation are outside
the measured batch; physical simulation, Unity, rendering and network execution
are absent. It is neither isolated Synchronize time nor a frame-rate measurement.

Independently read `TestResults/core_20261024T003718Z.trx` and its gate log:
65 executed, 65 passed, zero failed/skipped/errors/aborts. Root reports the
assembled R0 passed in 15 s and the complete core gate in 18.1 s; the core test
runner log itself reports approximately 6 s of tests. The source review and
transition/rollback/restore coverage are retained in `SYNCHRONIZE-TEAM-COPY.md`.

## Separate native comparison: acceptance is still open

Read only the exact NUnit output for
`Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.PublicTeamRacePreservesWholeSoloParties`
in `TestResults/editmode_20261024T003140Z.xml` and
`TestResults/editmode_20261024T003910Z.xml`. Independently verified every recorded
byte count/SHA256 for the six artifacts in each of
`BuildNotes/client-view/native-public-delta-20261024.json` and
`BuildNotes/client-view/native-public-team-copy-20261024.json`.

The earlier run is source `596822484af17e272b631c9a55304daac1ff61da`; the later run
is `0adab099b432f711428379978b14c228b3e23bc1`. Across runtime/native-test source the
only executable difference is the reviewed AuthoritativeMatch change; the other
listed native-test difference is a Unity-generated `.meta` file. Both use the
same exact public test, but their actual operation paths and windows differ.

| Ready-window observation | 003140 before | 003910 after |
| --- | ---: | ---: |
| Elapsed window | 21.8503302 s | 16.0679429 s |
| Canonical tick range | 42→784 | 27→530 |
| Synchronize count / inclusive ms | 742 / 2381.287 | 503 / 1377.433 |
| HostStep count / inclusive ms | 742 / 14081.671 | 503 / 9779.482 |
| ObserveHost count / inclusive ms | 742 / 2177.385 | 503 / 1576.921 |
| FullValidation count / inclusive ms | 762 / 3776.742 | 519 / 2849.340 |
| Client Update observations >1 s / maximum | 9 / 1835.871 ms | 7 / 2032.150 ms |
| Host Update observations >1 s / maximum | 11 / 1835.871 ms | 8 / 2045.003 ms |

Both ready collectors report `invalid=None`, `inFlight=0`, 186 bodies,
588 resources and 432 mechanisms. Their scopes are inclusive elapsed, cover all
instances on the owner thread, and nest; totals must not be added as exclusive
CPU. The long-frame measurements are MonoBehaviour.Update observations, not a
direct trace of the SDK time-provider call.

003140 passed movement (7.223 s) and authenticated-command observation (3.177 s),
then failed the exact-snapshot ACK operation with a session FormatException.
003910 reached readiness and exact-body checks but failed movement at 10.059 s,
before the later command/ACK operations. Its one native case is therefore FAIL,
not an online fix. Both recorded clean service cleanup (`failures=0`) and no
crash after quit. The later lower total phase times accompany 239 fewer host
steps and a different failure stage. Even per-call elapsed is workload/scheduling
dependent; these two failed, unequal windows do not establish overall performance
success or regression caused by this change.

The defensible outcome is the exact standalone allocation reduction with retained
state/correctness evidence. Native public movement, transfer completion and
user-visible frame performance remain unresolved. No follow-up runtime, pacing,
validation or SDK change is made or authorized by this evidence review.
