# Gameplay composition review

STATUS: COMPLETE — bounded source review; native integration gates remain open

Scope: read-only review of the in-progress `wordbound-puzzle-race/Assets/_Project/Scripts/Online/Gameplay` composition and its actual session/physics/domain dependencies. Root owns native Unity execution; James owns gameplay source. This lane will not edit those files or redeploy the service.

Captured source SHA256 before the owner's G4 repair: controller `31d83bb4d06d32ec6a60049d7247e2e1f6836f1ea7f85f2b2f55dfaf0874cb7f`; plan `bccb9b8cfdb9be4b4f107c9bcd591b7e353afe2e405016fe3e6046360b1eee9c`.

Acceptance: geometry must exist before readiness; only currently authenticated approved actors receive input; Runner replacement must invalidate old callbacks and bodies; the canonical committed snapshot must restore every body and mechanism without partial resume; returned/disconnected actors cannot move or reappear; client replicas cannot become authority.

Owner's intended order: Initialize authority → construct Session → AttachSession → Start Session → prepare isolated scene/world → MarkLocalReady → GameplayStarted/committed snapshot → create and reconcile host physics bodies → enable host driver. Migration creates a new Runner and rebuilds physical state from the restored committed authority.

Next recoverable action: capture exact source hashes and inspect actual startup, callbacks, physics readiness and migration teardown; record confirmed gaps with executable or concrete call-sequence evidence.

## G1: migration reuses readiness from the old Runner

Confirmed in session source at `99d919c`. `localReady` remains true through `OnHostMigration` and `BeginRun`. Replacement `AdmitLocal` calls `MarkLocalReady` automatically, and a replacement client sends Ready immediately on Admitted, using the previous Runner's readiness. The controller has separately reset its own `localReady` and may still be loading/building its isolated scene. Its `hostReady` prevents local physics, but the transport has already claimed replacement readiness.

Root authorized a bounded owned-session repair and regression. Readiness must be reset before credentials/start callbacks of each new Runner, then set only by that Runner's completed scene preparation. No service redeployment is required.

## G2: a reconnect activates its canonical body before peer readiness

Confirmed call sequence: existing match → disconnected player → successful coordinator authorization in `WordboundFusionSession.Admit` → `authority.Reconnect` sets Connected/body Active → client has not yet received Admitted and sent Ready. `gate.CanCommand` and therefore `TryGetAdmittedActor` remain false. The controller's next reconciliation sees an active canonical player but `InputAuthority` finds no admitted-ready peer, throws, and stops the match.

Suggested owned-session repair: admit/quarantine the peer and prove host authorization first; perform the domain reconnect only when that authenticated peer actually becomes Ready, rechecking the original deadline then. Preserve initial all-seat readiness. The controller should not fabricate input ownership or silently activate an unready body.

G1/G2 repair verification: 20 actual full-session-source scheduling tests pass, including replacement scene readiness, local/remote delayed reconnect, duplicate Ready and exclusive lease/domain deadline rejection. Actual installed Fusion SDK source compilation passes with zero warnings/errors. These tests do not stand in for native migration.

## G3: clients report paused after a valid start

The original controller's `IsSimulationPaused` unconditionally included `!hostReady` and the local authority pause state; both are host-only conditions. Clients therefore reported paused indefinitely even after verified replica/start. Reported to James, who changed this to shared local-content/session/replica requirements plus host-only authority checks. The current `WordboundGameplayPlan.IsPaused` implements that split. Owner adds focused policy regression; native UI behavior remains unexecuted here.

## G4: lease expiry can race the next host fixed step

`Poll` expires leases and disconnects canonical actors in Update. `TryGetAdmittedActor` correctly denies at the exact wall-clock expiry even between Update calls. A Fusion fixed step at that boundary can still observe an active canonical body, then `AdvanceHost → Reconcile → InputAuthority` finds no ready admitted peer and stops the whole match. Reported to James with a concrete ordering test: last Update just before expiry → advance clock to expiry → fixed-step driver runs before the next Update. Presence and canonical bodies must be reconciled before that simulation step; local-host revocation must prevent the step entirely.

Owner accepted the repair: synchronous Poll/presence reconciliation before the host physics hook, then another Poll/readiness check before post-physics commands. Also flagged the narrower clock-crossing-during-reconciliation boundary: a formerly valid seat losing its current lease should quarantine its input or trigger presence re-sync rather than stop all other players. Duplicate/mismatched actor ownership must remain a hard error. Owner's final source and native behavior are pending verification.

Final source recheck at controller SHA256 `06ae798439e9b84a443cfd1d31e213441567ca88718ff7fe718f334bea7f36ea` confirms both Poll boundaries and bounded canonical presence retries. A missing active-seat admission disconnects that actor through the real authority, re-captures state and retries within the eight-seat bound. Expiry during a new player's binding despawns that temporary body before retry. Duplicate admitted peers still fail. Existing-body input mapping uses one preflight result, while movement retains the live lease check. `AuthoritativeMatch.ApplyPose` ignores poses for an already inactive canonical player, so post-physics disconnection cannot be reversed by the earlier pose capture. No additional blocking source defect found in this bounded recheck. Native expiry/callback ordering remains unverified.

## Native migration handoff

Root's native experiment found that retaining yielded resume components in an array can retain reused SDK temporary objects. Root owns immediate immutable chunk capture in `WordboundFusionSnapshotChunk.Restore`. This lane keeps `RestoreMigration` enumeration lazy so capture can occur before the iterator advances. Complete/mixed/chunk/hash validation must stay strict. Root owns the native proof and carrier implementation.

## Separate Photon authentication fixture review

Read-only review of `wordbound-map-workshop/Assets/_Project/Tests/PhotonAuth/WordboundPhotonAuthenticationTests.cs` found no additional concrete false-pass or credential-output defect. Each negative includes a successful provider-authenticated host, requires an authentication-specific shutdown before host admission, checks no player seat, and checks host health. The positive case checks provider UUIDs despite a caller-supplied UserId. The fixture omits raw response/error text and keeps credentials in memory. Cleanup failures remain test failures. No live auth or Unity execution was performed by this review.

Next recoverable action: James checkpoints its verified composition; root integrates fresh-runner readiness with immediate resume-chunk capture, then proves native authenticated gameplay/migration and full body reconstruction. Native cases should include expiry before the fixed-step driver, expiry during spawn/admission binding, exact body reconciliation after migration, and a client becoming unpaused only after scene/content/verified-replica readiness.
