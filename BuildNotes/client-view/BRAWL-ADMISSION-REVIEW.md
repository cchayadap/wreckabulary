# Brawl public startup rejection

STATUS: COMPLETED READ-ONLY DIAGNOSIS — actual native `213856Z` failed Brawl startup; no source or deadline changes.

Exact artifacts: `TestResults/editmode_20261023T213856Z.xml` (SHA256 `B84C18E79BEDD4A976A27F86E02FE686855D9A317C99ABF99238749B299EA61D`) and `Logs/gate_test_editmode_20261023T213856Z.log` (SHA256 `1813FC7ABE0494C98B9CAABF7D5D5A46A143E8FB704AC5244C67C3A76B9AC665`). The batch discovered three cases: PublicBrawlRacePreservesWholeSoloParties failed in 40.122439 s; PublicFreeForAllAllocatesTwoIndividuals passed in 55.968740 s; PublicFreestyleFillsTwoRealHumansWithCoopRules passed in 48.756358 s. Despite the last historical method name, these use authenticated scripted clients. They are bounded allocation/input/command/ACK checks, not full-match or human-play acceptance.

## Exact rejection

Peer 1 reports `snapshot-transfer-mismatch-duplicate-or-stale` from ReceiveSnapshot → OnReliableDataReceived. Its existing allowlisted facts are:

| Fact | Observed |
| --- | ---: |
| Same mark/count/length/advertised wire hash | 1 / 1 / 1 / 1 |
| Duplicate chunk | 0 |
| Transfer elapsed milliseconds | 14209 |
| Transfer timeout milliseconds | 10000 |

This identifies the **age predicate** in the combined rejection. The preceding local-admission and mark identity/frontier checks passed for this callback. It is not evidence of a new Photon authorization or roster setup failure. Matching mark/header facts identify the same advertised immutable packet; they do not prove the payload decoded or passed its final content hash/authority validation. The transfer was rejected before reconstruction, committed-mark assignment, replica publication or ACK.

ReceiveSnapshot starts its existing wall-clock interval when the first chunk establishes receivedMark. Later chunks must match the mark/count/length/hash, be previously unseen and arrive at the callback before elapsed time reaches 10000 ms. The client catch calls Fail, freezes admission/authority and invalidates the current run. Therefore the fixture's line 461 Healthy assertion is reporting a real terminal session failure, rather than a separate test assertion defect. The blank FailureStage does not make its existing fixed code, origin and scalar facts ambiguous.

## Timing and limits

The all-scenes/durable-start operation ended after 19.927 s with maximum observed Unity frame gap 2.663 s. The host had canonical tick 577 and was ready/unpaused; the client had no canonical frame, no CourseWorld, no committed mark, and had been stopped/paused by this failure. Host forward batches reached 60 ticks, with a measured maximum batch duration of 2372.412 ms. The client observer recorded a 13299.819 ms maximum gap between batch starts, even though it had Unity Update callbacks; batch pairing had no mismatches. These are real same-run responsiveness observations, not a measurement of packet arrival, network RTT, Photon queue time or an exclusive CPU cause. Host Fusion tick count and canonical authority ticks are different counters.

The reported 14209 ms is observed from the session's existing UTC wall clock at callback processing. The artifacts do not isolate transport delay from local scheduling or establish a clock adjustment. Heavy host batches and the client gap support comparing startup responsiveness after the already-reviewed cost changes, without proving those changes will resolve this case. Nested phase totals must not be added to the batch wall time.

All three cases report service cleanup failures=0, return=0, cancel=0, leave=0 for two clients/two parties. The log proceeds through shutdown and reports no leaked weak pointers. Directly read `.harness/delivery/native-public-three-2139-stdout.log`: gate FAIL/exit 1, 213.1 s, tests 2 passed/1 failed/0 skipped, `crashAfterQuit:false`, no infrastructure error. No gameplay movement or command acceptance is claimed for the failed Brawl case.

## Smallest next check

No additional diagnostic or raw exception-message logging is needed to classify this failure. Retain the current 10000 ms limit, admission rules, strict fixture assertions and exact existing scalar diagnostics. Root's next cost comparison should include the same BrawlSolo method alongside the existing public comparison, recording startup readiness, any transfer rejection facts, paired host batch durations/client callback gaps and strict cleanup. Acceptance requires the existing complete case to pass; neither the two sibling passes nor the matching advertised packet identity repairs the failed Brawl admission.
