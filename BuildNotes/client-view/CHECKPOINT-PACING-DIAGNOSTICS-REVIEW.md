# Checkpoint pacing diagnostic review

STATUS: IN PROGRESS. Parent authorized source review of forthcoming opt-in pause/commit-duration observations only. No scheduling, rules, deadlines or production-performance claims are approved by this review; no jobs or runtime edits in this lane.

Current integration boundaries: GameplayController.PublishCheckpoint sets publishing before awaiting Session.PublishCheckpoint and, in its existing finally, clears it and schedules options.Clock()+CheckpointMilliseconds only if the original Runner is still current. Session PublishCheckpointCore includes preparation/validation, service commit, exact writer/receipt verification, possible head-recovery lookup and durable acknowledgement before returning. A continuation-observed duration can include serialization/service work and delayed main-thread continuation; it is not isolated HTTP round-trip time or the controller's complete schedule interval.

SnapshotCommitTracker.CanAdvance requires nextTick>=AcknowledgedTick and nextTick-AcknowledgedTick<=120 under the unchanged mode policy. Authority.IsPaused also includes migration and a result. Diagnostic sampling must retain those distinctions, avoid new options.Clock calls or full captures, distinguish pause samples from elapsed intervals, and disclose observation/late-completion boundaries. Any opt-in output must contain only fixed stage/reason labels and scalar facts, with no payloads, hashes, tokens, player/match identifiers or raw service messages.

Awaiting exact source checkpoint for verification of lifecycle, disabled/release paths, exception propagation and preserved scheduling.
