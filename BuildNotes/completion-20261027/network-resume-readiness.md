# Public FFA diagnostic resume — current-source readiness

STATUS: SOURCE READINESS PASS; native gameplay acceptance remains FAIL / unfinished.

The frozen contact-observation patch is safe for the integrator to apply next for bounded diagnosis. It still targets the exact live test bytes at `c4810d6a2b83c597c6d30e8ab3017891fb44941c`; no rebase or gameplay change is needed. This is an observation patch, not a fix or evidence that public FFA now completes.

## What was verified

- Patch SHA-256: `cb2347cccc3839c1ff993a716ae70371b8d1e07dd74cb18bd2a272bf06fb410d`, at `network-draft/contact-observation/contact-observation.patch`.
- Both existing test files byte-match their frozen baselines. All three candidates and the patch match the frozen manifest. The new observer and its Unity `.meta` do not yet exist in live Assets. Strict `git apply --check --whitespace=error-all` passes.
- The two existing files differ from Git blobs only by LF/CRLF; normalized HEAD content equals live content. The readiness JSON records both raw hashes rather than treating normalization as a source change.
- Fresh C#9 semantic analysis against the current Unity response file and compiled reference files passes: **20 sources, 315 references, zero errors**. It substitutes the three candidate files in memory, emits nothing and executes no game/test/native code. Source/reference hashes are in `network-resume-semantic.json`.
- The relevant Fusion, Gameplay, native online tests, item/rule configuration and OnlineServices trees have no Git changes between the failed `cd716e6a` source and current HEAD. Later client presentation/input changes exist and are not described as unchanged. Current online source/test/service tracked files have no uncommitted edits.
- All four pinned failure inputs still match their saved SHA-256 values: 184207 start/end receipts, gate log and test XML. No original failure, source draft or evidence was overwritten.

The earlier independent source review remains at `network-draft/contact-observation/review.md`; this resumption rechecks current compatibility and the failure, rather than claiming another independent author.

During this review the integrator published the UI documentation checkpoint `e29c0bcf421a186757dffd7b53fbbfcaae14460d`. Final readback still finds both test baselines unchanged and no relevant Fusion/Gameplay/native-test/configuration/service source delta. The initial `c4810d6a` check and final documentation HEAD are both recorded in the receipt.

## The failure this next run must explain

`TwoAuthenticatedPublicFfaIndividualsReachScoredWinAndConfirmDurableResult` failed on 26 October at the actual contact-return predicate. XML records one failed case, duration **465.879535 s**; gate wall duration was **502.4 s**. The final contact operation ran **35.012 s** with `lifetimeCancelled=False` and maximum observed Update gap **0.426 s**. This differs from the earlier 175059 eight-minute lifetime failure.

Direct log readback confirms host canonical tick **13318→13518**, SDK tick **22145→24249**, and committed tick **13242→13484** over that final operation. Fourteen preceding return operations completed. The failure log contains no final host-native victim steering receipt after the last return began. The old replica position and replica `CanMove` value therefore cannot establish the failing host body's distance, collision or movement permission.

The same raw log records zero return/cancel/leave/dispose cleanup failures for two clients and two parties. Slow canonical advancement is observed; durable-window pressure is a supported investigation target, not an established complete cause. Neither physical obstruction nor steering oscillation has been excluded. See `network-draft/ffa-184207-analysis.md` for the earlier bounded timing analysis; its publication/ACK measurements are inclusive observed wall times, not isolated HTTP RTT or CPU time.

## Scope and preservation of the diagnostic

Only three calls are inserted into existing test code: enroll an actual `ffaReturn`, sample after the existing yielded frame, and observe operation completion from `Until`'s existing operation-finally route. Removing those calls recovers both original files. The added partial class reads the host-owned victim's actual canonical and native body state, and never writes movement, physics, health, score, durability, authority or coordinator state.

The original steering, **35 s** contact deadline, **0.04 m** contact radius, input cap, **480 s** fixture lifetime and **600 s** test timeout remain unchanged. The actual ten-knockout target, four hits per knockout, two genuinely crafted hammers with unreplenished durability, nine automatic respawns, protected-hit rejection, replay deduplication, confirmed result on both peers, and exact durable-result download/ACK checks remain intact. The **120-tick** durable window, lease expiry, service protocol and TTLs are untouched.

Each enrolled walk emits begin/end and at most seven samples spaced at least five seconds apart: at most nine records per walk and 360 across the maximum 40 return walks. Existing diagnostic `Facts` may update its own committed-observation timestamp under its lock; it does not mutate gameplay. Record formatting allocates and has real unbenchmarked overhead. The observer intentionally provides no collision-contact history, and its minimum distance is a sparse sampled minimum, not continuous clearance proof. Missing body/epoch/probe facts must remain unavailable rather than being interpreted as zero or success.

## Exact next native gate — integrator only

Apply the frozen patch after the strict check, then run required R0/core gates on that actual integrated source. Let Unity generate the new test metadata; do not hand-author it. Preserve the previous failure and take a fresh run receipt. The gate must have exclusive ownership of the native slot; do not call Unity directly.

```powershell
$env:WORDBOUND_EXTERNAL_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_FULL_MATCH = '1'
$env:WORDBOUND_PROFILE_NATIVE = '1'
$env:WORDBOUND_PROFILE_PHASES = '1'
$env:WORDBOUND_PROFILE_CHECKPOINTS = '1'
node Tools/kit/gate/game_gate.mjs test editmode --cold --filter Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.TwoAuthenticatedPublicFfaIndividualsReachScoredWinAndConfirmDurableResult --json
```

Use the integrator's existing scoped environment/receipt wrapper and restore its prior environment afterward. The first three flags are required to avoid an ignored case. The three existing profiler flags retain the earlier timing context; checkpoint profiling is needed for pending publication age. Use the already approved exact public catalogue loaded by the production launch asset; do not silently override `WORDBOUND_PUBLIC_CATALOGUE`, use a private allocation, or change credentials/service configuration. This lane made no service readback, so current remote availability is unverified.

Success must be one passing case with zero skips, its natural ten-point result and durable ACK assertions reached, and successful cleanup. If it fails, retain the original bound and inspect the final `WORDBOUND_FFA_CONTACT` begin/sample/end records alongside canonical/SDK/committed progress. Distinguish a moving but tick-starved body, a stationary eligible body, oscillation, canonical/native mismatch, and unavailable observations before proposing a runtime correction. Do not widen the walk, lifetime or durable window as a substitute for a diagnosis. A process crash or unpumped coroutine can still prevent a final receipt; the source patch cannot guarantee logging after process loss.

## Remaining online goal

Even a successful next case covers two authenticated scripted individuals in one Editor process, not separate Windows clients, every mode, all capacity/party combinations or automatic host recovery. TimeRun, TeamRace and BrawlRace full public flagship races and supported formats, sustained Freestyle, timeout/draw cases, reconnect and host-loss recovery remain separate gates. Recovery must retain exact survivor identities, no duplicate inventory/results, the ten-second recovery target and two-second rewind bound.

The original house modes **Dibs, Duos, Moving Out, Moving Day and Escape remain local**. The service allowlist still has only `TimeRun`, `TeamRace`, `BrawlRace`, `FreeForAll`, `Freestyle`. Their names and local rules cannot be silently substituted for the five Wordbound modes. Preserve the explicit authority/content/state/recovery bridge work in `../restart-20261026/all-modes-online-scope.md` and the full-goal audit in `scope-audit.md`.

No live source, scene, service, native job, test deadline, harness file or original gameplay asset was changed by this readiness work. The project-local `.harness/brief.md` and `META-HARNESS.md` were absent; current project rules, approved scope, parent lane ownership and portable investigate workflow were read directly. No shared harness state was regenerated by this read-only lane.
