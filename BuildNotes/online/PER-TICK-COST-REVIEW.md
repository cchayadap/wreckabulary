# Per-tick main-thread cost review

STATUS: INDEPENDENT SOURCE PASS for `4c038d45`; integrated by root as `041ce4a8`. Root started R0 then the35-case core filter. No compiler, test, Unity, player, benchmark or deployment job ran in this lane. Existing dirty ModeDefinition and historical logs remain untouched.

## Measured baseline and its limits

Root evidence is `BuildNotes/online/native-clock-atomic-team-20261023.json`, `Logs/gate_test_editmode_20261023T181114Z.log` and `TestResults/editmode_20261023T181114Z.xml`; source at launch `50520a4c`, code `305ff093`. Both native cases failed. Public TeamSolo rejected a transfer after 14766ms against the unchanged 10000ms limit, with matching mark/count/length/wire hash and no duplicate chunk. This is elapsed time observed by the receiving continuation, not a measured network RTT or proof of the transport cause.

The Team profiler window is 61.169s, main thread, all in-process instances, per-marker invocation, capacity32768 with no observed wrap:

| Marker | Calls | Inclusive CPU ms | Maximum sample ms |
| --- | ---: | ---: | ---: |
| Reconcile | 361 | 9084.061 | 1509.061 |
| Advance | 360 | 7187.259 | 128.358 |
| Capture | 1337 | 4507.558 | 38.757 |
| BeforePhysics | 360 | 1614.610 | 31.528 |
| BuildWorld | 2 | 996.795 | 593.872 |
| PhysicsApply | 361 | 591.357 | 18.870 |
| Presentation | 361 | 297.577 | 26.680 |

Scopes overlap and cannot be added. The absent Validate samples mean unavailable under the approved atomic reconciliation boundaries, not zero validation cost. PhysicsApply now covers physical application; its old pre-atomic scope included validation and is not directly comparable. Capture measures the Controller wrapper only, excluding rollback captures inside Advance.

The host recorded a maximum60-forward-tick batch of4746.743ms, maximum Unity Update gap5368.842ms, and no batch-pairing error. Client forward batch maximum was17.049ms. The public test uses two runners in one Editor process. These observations establish substantial host main-thread work and scheduling gaps; they do not establish an internet/network fault, achievable player fps or a promised speedup from this candidate.

## Confirmed source costs

`WordboundGameplayController.Poll` and `PrepareHostPhysics` each call CaptureFrame before SameFrame. SameFrame reads only epoch, progress tick, and sorted player ID/Connected/Returned values. The discarded snapshot also captures the economy and journal, freezes/sorts every body, resource and mechanism, and creates their collections. `MatchSnapshot.Freeze` sorts each section, including constructed resource/mechanism string keys. This work is unnecessary when the tick and presence are unchanged.

Initial BeginHost, post-Advance reconciliation, presence retry, rollback, durable publication and untrusted import have different needs; their full captures and validation remain. An Advance rollback snapshot is required to restore all mutations after a failed host query, so removing it is not proposed. `WordboundFusionAuthority.Presence` also captures the whole world, but its only current caller is migration restoration, not the ordinary per-tick path; it is not included.

Within Advance, Synchronize invokes RetireInactiveResources. That method scans all bodies independently for every inactive resource. A private per-call tile index could remove that repeated scan while retaining resource iteration/removal/error order, but it is a separate candidate after measurement. EnsureCapacity also copies the economy merely to inspect entity counts. Synchronize sorts resources and repeatedly queries source/body availability. Their exact individual CPU contributions are not measured by the present markers; no combined optimization is proposed here.

The Economy constructor itself only assigns catalogue/rules references. Restore performs live replacement validation and builds a replacement economy; caching mutable catalogue/rules validity is not justified. Full initial and fresh mutable atomic reconciliation checks remain intact.

## Approved implementation

Alignment-only commit `800139a2` copies AuthoritativeMatch from root `c3e53b86` and MUST NOT be integrated. Controller and existing authority tests already matched root exactly. Only the additive method, two caller substitutions, removal of unused SameFrame, tests and this report belong to the deliverable.

`AuthoritativeMatch.MatchesTickAndPresence` rejects an in-progress tick using the same guard/message as Capture, then checks the retained snapshot binding, epoch, tick, seat count, sorted seat identities, Connected and Returned. It reads each current player through existing defensive-copy `MatchProgress.Player`; there are at most8 small player copies and no full-world snapshot, mutable reference exposure or cross-call cache. It is not described as allocation-free or as a snapshot validation API.

Poll and PrepareHostPhysics now capture/reconcile only after that comparison returns false. All guards, callback ordering, stop/reentry fences, protocol, authentication, deadlines, writer lease, checkpoint schedule and physics/rules behavior are unchanged. Every full validation at reconciliation/publication/import remains in place. No snapshot is reused across simulation changes.

For supported previously reconciled frames, this preserves the old comparison exactly. Disconnect, Reconnect and Return alter its presence fields; Advance changes tick; committed restore changes epoch. A failed tick rolls back to the preceding fields. Pause, acknowledgement, sequence, wall-clock-only changes and an undone disconnect/reconnect were already outside SameFrame's comparison and remain so. Defensive binding/cardinality checks additionally reject malformed foreign frames that cannot be produced by the Controller's successful reconciliation path.

## Regression contract and required verification

The authority fixture now has35 source-counted cases (28 existing plus7 new). Five mode cases independently run the original captured-frame comparator through advance, disconnect, repeated disconnect, reconnect, return and rejected reconnect, check repeated queries and retained-frame immutability. One case covers publication/acknowledgement/pause/no advance/committed next epoch. One checks foreign binding, short/foreign roster and explicitly proves equal tick/presence does not admit an incomplete body ledger through full validation. Existing rollback and in-tick host callback tests gain assertions against the new method; no original assertion was removed.

Root must run, sequentially:

```powershell
node Tools/kit/gate/game_gate.mjs check all --json
node Tools/kit/gate/game_gate.mjs core 'FullyQualifiedName~WordboundAuthorityTests' --json
$env:WORDBOUND_LOCAL_GAMEPLAY = '1'
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.OfflineGameplayTests.WordboundNativeLocalGameplayTests' --cold --json --timeout 3600
```

Expected counts:35 focused core and8 existing native local cases, including FrameChanged→Stop after a real canonical Disconnect. Native public comparison requires the same exact TeamSolo workload, actual content, environment flags and opt-in native profiler/tick observer as181114Z; root owns those established invocation settings. Preserve the10000ms transfer deadline and every behavioral assertion. Compare Capture calls per advanced tick, inclusive Reconcile/Advance, maximum60-tick batch wall time, and actual public admission/gameplay outcome. A lower Capture count alone is not acceptance. Retain both raw runs and machine/workload limits; never derive zero cost from an unavailable marker or claim60Hz from this source change.

Independent recover_assets source review confirmed ordinal-ID order in Binding.Seats and ProgressSnapshot.Players, completed-tick rejection, preserved captures/validation/atomic fences and the seven new cases plus strengthened rollback/in-tick assertions. Root separately reviewed the same contract before integration. Neither source review is a compile/native/performance pass. Only whitespace/diff inspection ran in this lane; root's compiler/core results and native comparative performance are still pending at this checkpoint.

## Follow-up: remaining Advance and mutable-validation work

STATUS: READ-ONLY PROPOSAL, not implemented. Root subsequently reported R0 PASS (`183255Z`,20.3s) and authority core35/35 PASS (`183349Z`,23.4s). Native comparison `184748` is still pending at this review checkpoint. These results do not establish the capture change's performance or multiplayer acceptance. Inspected current integration sources through `6be3b135`; no compiler, test, profiler, Unity, player or service command ran in this lane.

The recorded actual flagship workload in `validation-resources-20261023.log` has TimeRun94 bodies/57 items/22 loose tiles/294 resources/216 mechanisms and TeamRace Solo186/114/44/588/432, respectively. Both have two human seats and seed `0123456789abcdef`; these are frozen tick-zero probe counts, not an assertion that all later native frames have the same population. The canonical ceiling is8192 entities per item/tile ledger and512 bodies; runtime player count can reach8.

### Smallest next option: capacity counts without an economy snapshot

`AuthoritativeMatch.EnsureCapacity` (`AuthoritativeMatch.cs:1040`) calls `economy.Capture()` once after every successful `Advance`, and on the transactional Return path. It reads only `ItemStates.Length` and `TileStates.Length`. Nevertheless, `EconomySnapshot` sorts and copies every player, slot array, craft job, letter/reservation bag and item, then sorts/copies the tile ledger. In the recorded Team tick-zero workload, that is114 item copies and44 tile entries plus both player inventories per capacity check. This is separate from the required pre-tick rollback capture and the post-Advance published frame.

Prioritize replacing only this count-only capture with current ledger counts if the native comparison still fails. The existing `Economy.Items` enumeration and `Economy.Tiles.Count` permit a narrow implementation without exposing a mutable dictionary or adding a public trusted-state API. `Items.Count()` is sufficient for semantic equivalence even if the installed LINQ implementation enumerates it; do not claim constant time without checking that runtime. An internal direct dictionary count would need explicit scope approval but is not necessary to remove sorting and object copies.

Keep the same `>` comparisons,512/8192 limits, exception text, call positions and surrounding rollback. Count every canonical item, including destroyed tombstones and held/carried/deployed equipment; count only remaining loose tile entries, not inventory letters, historical allocation frontiers or active bodies. `Economy.Capture` performs copying, not `ValidateReplacement` or full snapshot validation. AuthoritativeMatch owns this Economy privately; supported mutation paths retain nonnull inventories/items. This proposal does not make a claim about reflection-corrupted private state or replace any validation boundary. There is no callback or asynchronous gap in this check, and no cached count should be maintained.

The distinguishing regression is a real lifecycle sequence, not an empty-economy count assertion: break authored furniture, collect part of its resulting loose letters, complete/return a craft, capture/restore, and repeat. Verify that the destroyed item still consumes a canonical item entry, collected letters stop consuming loose tile entries, and the restored state makes the same capacity decision. Exercise exact and over-limit counts with publicly constructed Economy content rather than thousands of native ticks; retain the actual Authority rollback and Return regression paths so a failed transaction still restores frontiers, ledgers and receipts. Compare any new count decision against the old snapshot-length decision in these states, without exposing internal setters or weakening native assembly boundaries. Full snapshot validation must continue rejecting malformed ownership/reservations after the change.

This is intentionally a small allocation candidate, not a claim that it will reach60Hz. The existing ValidationProbe does not execute Advance/EnsureCapacity and cannot measure this change. Its acceptance requires the same actual native Team workload, unchanged deadlines, and inclusive Advance/Reconcile and maximum batch wall-time evidence. Do not combine it with retirement buckets in the first comparison.

### Other confirmed repetition, kept separate

`ValidateBodyBudget` (`MatchStateValidation.cs:178`) runs during both the initial full check and the fresh mutable check. For every map item body it scans all `course.Placements` with `Any`, despite the immutable course cache already containing an ordinal placement dictionary. A later one-expression dictionary lookup could preserve the placement-purpose and duplicate-source checks in their current order. A useful regression must distinguish a real non-resource placement from a resource-purpose placement, an unknown source and a duplicated placement identity; the atomic test must still observe a changed `MaxDeployed` after the preparation callback. Do not cache the body's admission result or skip the second phase.

The greater-looking mutable cost is `ValidateEconomy`: new Economy, AddPlayer for each seat, then Restore builds and checks a replacement Economy before copying its dictionaries into the temporary destination. The constructor itself only assigns references. Restore validates current rules, enabled recipes, pending reservations, item origin/state/frontiers, player references, letter conservation, ownership and held categories in order. Both initial controller rules and post-preparation physics rules remain necessary and may differ. Removing this validation or caching its result is not proposed. Splitting a validation-only Restore path would be materially broader than the count change and would need rejection-order and mutation-during-callback coverage before consideration.

`AuxiliaryBodies` also copies body/mechanism enumerations and searches current team/site/receiver candidates. Its choice depends on live item state, progress, mechanism evidence and practice selection; static caching is unsafe. Synchronize's resource sorting and source-availability scans remain candidates for later measured work. The existing retirement-bucket proposal is recorded separately in `RESOURCE-RETIREMENT-COST-REVIEW.md`; none of these changes are bundled or implemented here.

Root's subsequent `184749Z` result was8/10 PASS: all eight local atomic cases passed, CLOCK failed its actual LineBlocked condition, and the public case failed before gameplay authorization. Its Capture/Reconcile/Advance markers were unavailable, so it did not exercise the optimization and cannot support a before/after comparison. Root assigned the authorization timestamp/binding discrepancy for separate read-only attribution.

## Read-only attribution of the184749 authorization failure

The sanitized log/XML record `connection-time-or-binding`, `tokenLifetimeSeconds=15` and `leaseRemainingMilliseconds=15004`, originating in `OnlineAdmissionVerifier.VerifyConnectionCore` via `OnlineConnectionService.Authorize`. At `OnlineAdmissionVerifier.cs:169-178`, remaining lease greater than15000 is independently sufficient to reject. Fifteen seconds does not violate the JWT-lifetime branch. The combined error does not log each identity comparison, so it cannot prove that every other branch matched; no identity or authorization value was read or saved for this review.

`OnlineConnectionService.Authorize` samples its default `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` after awaiting the HTTP response (`OnlineConnectionService.cs:73-78`). The native `WordboundLiveMatch.Connections` factory supplies no clock override. This is not a timestamp retained from before the request. `MatchPool.authorizeConnection` creates the signed lease deadline as transaction `NOW()+15000`, where NOW is `Date.now()` (`pool.ts:323-346`, `model.ts:98`), then calls `signGrant(...,15)`. Exact retries retain the prior lease deadline and do not extend it.

Installed JOSE (`node_modules/jose/dist/webapi/lib/jwt_claims_set.js:4,35-36,132-137`) creates iat and relative exp from separately sampled `new Date()` values floored to seconds. The token therefore has a signed whole-second interval and a separate signed millisecond lease deadline. General Verify already enforces strict signed iat/exp against whole-second client UTC. The extra lease upper bound instead compares server milliseconds with client milliseconds with zero tolerance. A server lease created at second I+504ms and verified at local I+500ms has the legitimate signed exp I+15 and remaining15004ms; it passes the general JWT time check but fails this added bound. This is consistent with a small wall-clock offset/resolution difference. No request timing or clock calibration was recorded, so the precise source of the four milliseconds is unproven.

Renew already validates its15-second deadline against response `serverTime`, separately limiting server/client discrepancy to5000ms (`OnlineConnectionService.cs:88-96`). That is evidence of differing time-domain checks, not permission to copy a5000ms allowance into admission.

### Proposed contract, not an implemented authentication change

Keep all signature/key/issuer/audience/schema, strict JWT future/expiry, positive lifetime, connection lifetime <=15 seconds, nonexpired millisecond lease, match/generation/roster, authenticated player/host, nonce and roster-membership checks. Replace only the client-relative upper deadline check with the signed-second consistency bound `leaseExpiresAt / 1000 <= exp`, equivalent for nonnegative integer milliseconds to `leaseExpiresAt < (exp+1)*1000`. Integer division avoids added large timestamp multiplication. The final fractional second is the unavoidable precision interval of the existing signed exp, not a general clock allowance or extension of service TTL. It is insufficient to prove exact15000ms issuance duration: that would require a new signed millisecond issuance timestamp and a separately reviewed protocol/deployment change.

Because lease creation precedes signing, legitimate fresh deadlines may lie in exp's final fractional second, while a delayed exact retry may expire earlier than its newly signed JWT. Preserve both cases and the original stored lease deadline. Do not require equality with exp, and do not accept an inflated lease in the following second. A client whose clock is a whole second behind still fails strict future-iat validation; an expired JWT remains rejected even if the separate lease has milliseconds left. No automatic retry, clock adjustment, auth bypass, service constant or deadline change is proposed here.

Before source approval, deterministic signed-token regressions should cover: the exact15004ms case with matching bindings; valid final fractions including999ms; the following second's exact boundary and one millisecond beyond rejected; lease equal to/past clientNow rejected; expired/future JWT and lifetime16 rejected; wrong player/host/connection/roster/match and altered signature rejected; a later-signed exact retry preserving its earlier lease expiry. Existing service idempotency, one-use-ticket and terminal-return cases remain required. Worker tests that need controlled JWT times must control `new Date()` as well as Date.now; the existing Date.now spy alone does not establish this boundary.

A public-only rerun can measure the capture optimization if authorization succeeds, but cannot close this deterministic timestamp-contract defect by itself. This review changed documentation only; no tokens, secrets, network calls, service mutation, compiler or test run occurred.

## Approved admission repair checkpoint

Parent approved the bounded signed-second contract. Source/tests commit `ae763dde` changes only `OnlineAdmissionVerifier.cs` and `WordboundOnlineTests.cs`; it replaces one upper-bound predicate with `lease.ExpiresAtMilliseconds / 1000 > signed exp` as the rejection condition. All other checks, diagnostic facts and failure text remain. No Worker, protocol, signer, TTL, renewal, clock, deadline, SDK or retry behavior changed. The verifier matched integration byte-for-byte before editing; the fixture differed only by line endings, so there is no baseline-copy commit to integrate.

There are22 new source-counted signed-token cases and40 total cases in `WordboundOnlineTests`. Positives cover the observed15004ms remaining lease and the signed exp second's999ms endpoint through both host/client verification entry points. Negatives reject the next-second boundary, one millisecond beyond and inflated deadlines even when verification is delayed14 seconds; that delayed case distinguishes signed consistency from the old client-relative ceiling. The remaining cases cover expired/future/16-second JWTs, exclusive lease expiry, six identity/binding fields, outsiders whose claimed/authenticated IDs agree but are absent from the roster, a modified RSA signature, and a later-signed retry that cannot extend the original lease.

Existing eighteen cases and assertions are retained. No reflection, internal setter or private-key persistence is introduced; the fixture uses its existing ephemeral2048-bit RSA helper. Source whitespace checks passed. Independent source review is pending at this checkpoint; compiler, core and native are NOT RUN in this lane.

After root's frozen build/player work ends, required execution is assembled R0 and `node Tools/kit/gate/game_gate.mjs core 'FullyQualifiedName~WordboundOnlineTests' --json` (expected40), followed by the established exact public TeamSolo comparison. Retain the original184749 failure. A future token with iat in the next client second still rejects, and no general cross-second clock-skew support is claimed. Root owns all execution and deployment decisions.

Independent recover_assets review returned bounded SOURCE PASS for `ae763dde`: intentional integer-floor final-second contract, unchanged signature/time/binding checks, all22 focused signed-token cases, exact retry expiry and no source blocker. This is not a compile, core, native or public acceptance result; those gates remain pending.
