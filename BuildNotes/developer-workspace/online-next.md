# Next public-online capacity verification

STATUS: COMPLETE — runbook only, no new public run. Read-only source/evidence review at integration HEAD a351d7998710f5ddae378e6bbe45f4d11be99cd7; unrelated workspace/Shop/portrait drafts are present and preserved. Root owns service access and every native gate. This report is the only write in this task.

Scope: derive exact isolated TimeRun4 and Freestyle8 retry commands, prerequisites and cleanup acceptance from the current fixtures/repair and existing receipts. No services, credentials, native jobs or runtime/source changes are performed. Historical service resource IDs are excluded from this report.

## Verified starting point

- Cleanup repair is integrated in `0c083004` (`test: bound online fixture cleanup by owned party chains`). The four inspected online fixture files have no working-tree diff. Raw `TestResults/editmode_20261026T062041Z.xml` contains all **15 WordboundLiveCleanupTests cases Passed**, zero failed/skipped, summed case duration 0.505957s. The enclosing mixed run was 143/144; its unrelated story failure does not negate these cases. Older progress text saying these 15 cases are NOT-RUN is stale. Fake-transport success does not establish live service cleanup.
- Historical `014532` remains 1/3 passed: FFA8 passed in 128.264201s with cleanup zero; Freestyle8 reached gameplay/exact maximum roster but failed cleanup; TimeRun4 failed preparation with HttpRequestException after one client and no party. Preserve `BuildNotes/client-view/native-public-max-rosters-contract-20261026.json`, its original XML/logs and operator receipt unchanged. The source repair explains and corrects shared-budget scheduling; it does not establish the cause of the first three historical HTTP0 failures.
- `BuildNotes/experience-refresh/online26.progress.md` and `online-cleanup-operator-20261026.json` record the old match CLOSED, expired live leases and eight retained memberships at their inspection time. Fresh fixture pools/guests do not reuse those records. Their cleanup is a separate unresolved task; no current service state was queried here. Do not copy old resource IDs into this tracked report. If a later authorized investigation needs selectors, keep them in a confirmed ignored `.harness/delivery/` file. `.harness/`, `Logs/` and `TestResults/` are ignored; raw new logs/XML can also contain match/pool selectors.

## Exact scope and prerequisites

Assembly: **Wreckabulary.Online.NativeGameplay.Tests** (despite its file name `Assets/_Project/Tests/OnlineGameplay/Wreckabulary.Online.Gameplay.Tests.asmdef`). Both methods belong to `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests` in `WordboundNativePublicEntryTests.cs`, category `ExternalPhotonPublicCapacityGameplay`, Timeout(600000). Use exact methods, not the whole category.

| First / second | Exact method | Maximum roster and fixture party shape | Fill | Derived cleanup watchdog |
| --- | --- | --- | --- | --- |
| 1 | PublicTimeRunAllocatesFourAuthenticatedCooperativePlayers | 4 authenticated clients, one team, slots 0–3, one whole four-member party | false | 300s + at most 5s abort drain |
| 2, only after reviewing the first result | PublicFreestyleFillsEightAuthenticatedCooperativePlayers | 8 authenticated clients, one team, slots 0–7, eight singleton parties | true | 120s + at most 5s abort drain |

Both use format Solo because only TeamRace/BrawlRace have Duo/Squad formats. The service caps an individual party at four even though Freestyle's team/match capacity is eight (`OnlineServices/src/model.ts:117–135`, `party.ts:108`). Freestyle's eight parties are deliberately the maximum Fill composition; this does not test all two-/four-member Fill combinations.

Before root launches each case:

1. Finish the current gate and source freeze; retain current R0 evidence, HEAD and relevant dirty diff/hash snapshot. Root remains the sole native runner. Use the installed Unity 6000.6.4f1 executable below; do not launch a second editor or alter toolchain/process/cache settings for this run.
2. Confirm production Photon custom authentication and the existing coordinator are available with the pinned configuration already used by these fixtures. No admin/provider credential is needed by the test: it creates fresh guest credentials in memory. Do not print environment values, guest tokens, raw response bodies or private key material, and do not change auth, allowlists or deployment to make a test pass.
3. Confirm the exact native-exported catalogue is still approved on the Worker. Current `Assets/_Project/Resources/Wordbound/public-catalogue.json` SHA256 is `4633207d08f8acce18ca3463bb86a71b1f3eaba41a2adfd475b46e9e5826ea23`. The fixture requires the same bytes as `WordboundLaunchAssets.PublicCoursesJson`, its release version and the selected content bindings; preserve those assertions. No export/regeneration or service approval mutation is included here.
4. If checking reachability immediately before launch, use only the pinned origin's unauthenticated **GET /v1/keys**, with normal TLS and no redirect relaxation; emit status, timing, key-count/fingerprint comparison only. Compare public trust with `OnlineServices/production-client.json` and the fixture's independently pinned fingerprint. HTTP200 is a narrow prerequisite, not proof of guest creation, Photon admission or sustained connectivity. The earlier successful probe is historical, not current health.
5. Each invocation must make its own guests and random isolated `pub-` pool suffix (`WordboundLiveMatch.cs:96–97`); do not provide old identities/pools. Source asserts the isolated pool differs from the approved release pool. No prior guest-secret recovery or public populated-pool joining is needed.

## Root invocation — serial, one case per gate

Run the following process-scoped preparation in root's controlled PowerShell runner. These values are nonsecret opt-in/path/telemetry settings from the historical capacity job. They do not install persistent environment settings. The old ignored job descriptor explicitly used **--timeout 3600**; retain that existing outer gate limit below. The 600s method annotation, eight-minute gameplay lifetime, 120s preparation wait and every 15s HTTP deadline remain unchanged.

```powershell
Set-Location -LiteralPath 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration'
$env:GSK_UNITY = 'C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe'
$env:WORDBOUND_EXTERNAL_GAMEPLAY = '1'
$env:WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY = '1'
$env:WORDBOUND_PUBLIC_CATALOGUE = 'C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration/Assets/_Project/Resources/Wordbound/public-catalogue.json'
$env:WORDBOUND_PROFILE_NATIVE = '1'
$env:WORDBOUND_PROFILE_CHECKPOINTS = '1'
$env:WORDBOUND_PROFILE_PHASES = '1'
```

First, TimeRun4. Use fresh output names and refuse any collision; preserve stdout/stderr even on a failed exit.

```powershell
$capacityStamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$capacityPrefix = '.harness/delivery/public-timerun4-cleanup-' + $capacityStamp
if ((Test-Path -LiteralPath ($capacityPrefix + '-stdout.json')) -or (Test-Path -LiteralPath ($capacityPrefix + '-stderr.log'))) { throw 'Receipt path already exists.' }
& 'C:/Program Files/nodejs/node.exe' Tools/kit/gate/game_gate.mjs test editmode --cold --assemblies 'Wreckabulary.Online.NativeGameplay.Tests' --filter 'Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.PublicTimeRunAllocatesFourAuthenticatedCooperativePlayers' --timeout 3600 --json 1> ($capacityPrefix + '-stdout.json') 2> ($capacityPrefix + '-stderr.log')
$capacityExit = $LASTEXITCODE
'TimeRun4 gate exit: ' + $capacityExit
```

Review its raw gate receipt, named native XML/log and cleanup criteria below before proceeding. A nonzero exit, skipped/zero test, missing receipt, teardown failure or ambiguous timeout stops this runbook; preserve and investigate it instead of retrying mutations or starting the second case automatically. Once TimeRun4 is accepted, invoke Freestyle8 separately in the same prepared root context:

```powershell
$capacityStamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$capacityPrefix = '.harness/delivery/public-freestyle8-cleanup-' + $capacityStamp
if ((Test-Path -LiteralPath ($capacityPrefix + '-stdout.json')) -or (Test-Path -LiteralPath ($capacityPrefix + '-stderr.log'))) { throw 'Receipt path already exists.' }
& 'C:/Program Files/nodejs/node.exe' Tools/kit/gate/game_gate.mjs test editmode --cold --assemblies 'Wreckabulary.Online.NativeGameplay.Tests' --filter 'Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.PublicFreestyleFillsEightAuthenticatedCooperativePlayers' --timeout 3600 --json 1> ($capacityPrefix + '-stdout.json') 2> ($capacityPrefix + '-stderr.log')
$capacityExit = $LASTEXITCODE
'Freestyle8 gate exit: ' + $capacityExit
```

The checked existing `.harness/delivery/run-gate.ps1` wrapper is also compatible with these exact argument/environment values in a new unique job descriptor, but its environment allowlist accepts only WORDBOUND_*: set GSK_UNITY in the parent process so it is inherited. Do not reuse/overwrite the old capacity job files. No process is launched by this report.

## Acceptance and retained cleanup evidence

For **each** new case require gate PASS/exit0, exactly one intended XML case Passed, zero failed/skipped, no post-quit crash, and successful teardown. Capture source revision/diff and catalogue hash plus new gate/log/XML paths in a sanitized receipt. Inspect these markers together, never only the gameplay marker:

- `WORDBOUND_PUBLIC_ENTRY_OBSERVED` with exact approved content and a fresh isolated pool; `WORDBOUND_GAMEPLAY_OBSERVED`; `WORDBOUND_PUBLIC_CAPACITY_OBSERVED` with 4/8 clients, signedTeams=1, signedParties=1/8, exactMaximumRoster=true and allNativeAndCanonicalRosters=true; `WORDBOUND_PUBLIC_MODE_OBSERVED` with the expected mode/Fill. Keep private selector-bearing raw lines in ignored evidence.
- `WORDBOUND_SERVICE_WATCHDOG: budgetSeconds=300/120; drainSeconds=5`; **no** outcome=expired or incomplete-after-drain. Do not extend that bound on failure.
- Complete `WORDBOUND_SERVICE_STAGE` records: TimeRun normally 4 return +1 cancel +4 leave =9 stages; Freestyle 8+8+8 =24. All return stages must settle before the first party read/cancel. Within a party, cancel precedes reverse-member leave, original leader last; each mutation uses a newly read revision. Normal fresh success produces 22/56 request receipts respectively. A verified already-left member legitimately reduces requests; retain its successful Profile evidence rather than inventing a mutation/count. Any failed, blocked, pending or incomplete stage remains a failure.
- `WORDBOUND_SERVICE_REQUEST` must retain attempt=1, stage/peer/party/subrequest, monotonic timings, budgetMs=15000 and failure/cancellation fields. `started=True` means the client delegate was invoked, not proof of transmission. Each normal request succeeds with no cancellation/abort; no hidden retry or new idempotency key is allowed after an ambiguous mutation. A successful request cannot erase a failed stage assertion.
- Final `WORDBOUND_SERVICE_CLEANUP: failures=0; return=0; cancel=0; leave=0; dispose=0; clients=4/8; parties=1/8`. **return/cancel/leave are failure counts, not attempt counts.** Also require native `Proof session/service cleanup must finish without failures` and `Proof-owned native objects remained after destruction` assertions to pass. Profile-after-leave must show no party membership; normal successful cleanup is not a claim that guest/empty-party storage rows are deleted.

On a preparation failure, report the actual partial client/party/stage counts; do not compare them to the fully prepared success shape or call partial cleanup capacity acceptance. An ambiguous CreateParty/Join response may leave unknown server state outside the recorded ownership list; the cleanup patch deliberately does not discover unrelated parties or invent credentials. Preserve all failed receipts even if a later stage or separate fresh run succeeds. Any original failed-run residual follow-up needs its own exact supported read-only reconciliation first, with selectors confined to ignored private evidence and no credential recovery/bypass.

## What a pass establishes, and what remains

These are real authenticated maximum-roster **entry plus gameplay smoke and cleanup** proofs: exact native PlayerRef sets on all peers; host-resolved user IDs and production admission mapping; local own identity; exact signed/canonical team/slot seats; real bodies, client movement, one authenticated command, duplicate receipt suppression and snapshot ACKs. They do not play a whole natural TimeRun objective, exercise every player's complete actions, establish migration/reconnect/full-duration stability, or prove old residual deletion. The existing two-client entry passes cover a smaller roster and are not full-match acceptance. Keep full-match/end/result and authored-workshop/whole-squad cleanup regression work separate after these bounded retries.

Source anchors: `WordboundNativePublicEntryTests.cs:30–39, 42–134`; `WordboundNativeGameplayTests.cs:81–197, 619–710`; `WordboundLiveMatch.cs:96–177, 216–446`; `WordboundLiveCleanupTests.cs`; `OnlineServices/src/model.ts:117–160`; `Tools/kit/gate/lib/jobs.mjs:350–431`; historical ignored job `.harness/delivery/native-public-max-rosters-contract-20261026-job.json`.

Review fingerprints (SHA256): LiveMatch `e6d3f045a6b5b74d98e4c3e5ed31f22e580b13f89c019f1cdb36478659a89426`; NativeGameplayTests `769f6091f706009abc4527a4cf02edf27308c18db0731782e0dab8f0722c4c63`; NativePublicEntryTests `e90ee2fe7a5f7881b1288f3c67bf264b5184b447c70a6eba8bb0e24449976ddc`; LiveCleanupTests `b495e99daaac1953c858e118bb764a687a4d7379f00a77dfacc024c39d903bf2`.

No service request, native/source gate, new test, source edit, commit or provider mutation ran during this review. Next recoverable action: root rechecks the source freeze and current prerequisites, then executes the isolated TimeRun4 invocation and accepts/reconciles its complete receipt before considering Freestyle8.

Toolchain correction: the only approved GSK_UNITY path is **C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe**. The earlier long-path assignment was a documentation carry-forward error, not an executed command. Read-only verification confirms the short executable's6000.6.4f1_12bfff696524 product version and valid Unity Technologies signature against the project's pinned version. Root's short-path recovery/launch contract governs; file existence at another path is insufficient. No job was launched for this correction.
