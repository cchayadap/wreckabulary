# ObserveHost cost review

STATUS: IN PROGRESS; read-only proposal, no source change or jobs. Root requested one bounded candidate from the actual003140 ready observation:742 calls /2177.385ms inclusive elapsed, approximately2.934ms/call. This scope is not exclusive CPU and does not establish removable cost or FPS.

Source trace: GameplayController.AdvanceHost validates completed pose coverage then invokes options.ObserveHost. PuzzleRuntime.ObserveHost rechecks current host/frame/rules and exact pose IDs, refreshes current scene collider ownership, then visits unfinished teams and physical sites before progress sensors. Authored observation performs a full frame.Mechanisms.Single scan for every visited site before solved/active filtering; TimedMechanism later queries that same site's previous mechanism again. These are immutable snapshot records, while collider/Rigidbody/admission/rules evidence remains live.

Current candidate to investigate: lazy per-invocation grouping of mechanism records keyed by exact(Team,Lap,PuzzleId), retaining queried-key Single predicate exceptions and original team/site/output order. Construct only at the first original lookup after RequireHost/pose/RefreshColliders checks; do not use eager ToDictionary duplicate rejection or move physical-phase checks. No across-call cache, contact reordering, stage/evidence change, live collider cache, fingerprint skip or authority-validation removal is proposed. Need complete mutation/error-order proof and meaningful old-query parity plus native evidence before recommending implementation.

Root prioritized equipment QA source review next. This audit is checkpointed here; no implementation has been authorized.
