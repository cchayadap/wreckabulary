# Bounded host recovery timing proposal

STATUS: PROPOSED — no service constants, transport timing or lease schemas changed. Stage 1 source checkpoint `e3194fe` and its pending regressions do not prove a ten-second host recovery target.

## Existing boundaries

`OnlineServices/src/checkpoints.ts` has `LEASE_MS=15000`, strict claim requests containing only `claimId`, `reservationGeneration`, `rosterHash`, and a persisted writer term. `checkpoint-renew` extends that same live term. Claim rejects any takeover before expiry unless the old seat returned. Begin/chunk/finalize each check the live owner and term; finalize checks again after expensive validation. Public writer JSON has exactly seven fields and C# `OnlineCheckpointWriter` rejects unknown fields.

`WordboundFusionSession.RestoreWriter` currently retries busy claim every 500ms for at most 60s. `Poll` schedules writer renewal every 5000ms independently of uploads and separately renews connection leases every 5000ms. The actual HTTP transport permits 15s per request. `RequireWriter` rejects a response after the previously known lease expired, even if the server processed a renewal before that boundary. This conservative local fence must remain.

Photon elects Host/Client through `HostMigrationToken`; an expired coordinator writer lease does not grant a Photon host role. Stage 1 replaces a disconnected runner as Client unless it has that migration token and never creates a room as a retry. It stops old RunScope renewals on confirmed loss, preserves the absolute seat window, and links recovery cancellation through coordinator/restoration work. A still-running host whose coordinator lease expires currently faults; the optional short-lease work must explicitly address that state rather than simply making it happen sooner.

## Proposed compatible lease contract

1. Extend only the strict claim request with optional `leaseProfile: "recovery-v1"`. Omission means the existing 15000ms lease. Reject all unknown values and fields. New calls explicitly select 6000ms through that profile; this must not become a global reduction.
2. Persist the selected profile internally on the writer; missing fields in existing rows mean legacy. Project responses back to the same seven public writer fields. Do not leak an eighth field into old strict decoders. The response's absolute expiry remains authoritative.
3. Renewal keeps the original term's selected duration. Repeating the same claim ID with a changed profile must fail the idempotency check. Preserve generation/roster checks, initial-host requirement, 256-claim limit, term monotonicity, exact head/epoch restoration and all upload/finalize fences.
4. Roll out the service accepting both profiles before the new client requests one. An upgraded client must fail closed with a clear incompatibility result against the old strict service; it must not silently call the old contract after an unknown outcome. Existing clients/rows keep 15s. A legacy host within an otherwise upgraded match therefore retains legacy timing until a new elected host claims a new term. Only a release pool known to contain upgraded clients can be measured against the short target.

## Proposed upgraded caller timing

| Operation | Current | Candidate |
| --- | --- | --- |
| Writer lease on new profile | 15s | 6s |
| Writer renewal cadence | 5s | 1s |
| Individual writer renewal request | shared 15s HTTP ceiling | linked cancellation at min(2s, remaining known lease) |
| Busy writer-claim retry | 500ms | 250ms, bounded by original recovery deadline |
| Native/actual Fusion connection timeout | default/checked-in 10s | unchanged |
| Authenticated connection lease/renewal | 15s / 5s | unchanged |
| Seat grace | 120s | unchanged, never reset by failed retry |
| Checkpoint provisional cap | 120 simulation ticks | unchanged |

Root's acceptance clock starts at detected transport loss. Keep the existing ten-second detection setting initially: shortening it would change false-positive behavior without measured need. The installed Fusion 2.1.3 XML defines `ConnectionTimeout` in seconds as the maximum silence before disconnecting the remote peer; any future test setting must apply to the actual constructed config, not only the global Inspector asset.

Keep one renewal in flight. A canceled/late response must still pass current RunScope, owner, term and prior local-expiry checks. Continue renewal during checkpoint upload/download and scene reconstruction. Do not weaken acknowledgment requirements to avoid freezing. A single Durable Object may be busy validating the largest permitted packet; validate this path under the short lease before activation.

The ten-second target is measured from detected loss. Election, the remainder of the prior six-second writer lease, exact download/restore and readiness must fit that interval. Photon election, cloud connectivity and service latency are outside a simple TTL arithmetic guarantee. A host partition that still renews against the coordinator can prevent safe takeover; do not preempt a live writer to satisfy a timing assertion.

## Additional host-state work that needs explicit ownership

Define whether an existing transport host with an expired writer may remain frozen and reclaim a new term on the same Runner. If implemented, require current RunScope and actual `Runner.IsServer`, cancel old uploads/renewals, restore the exact committed head at the new epoch, refresh leases and repeat admission/readiness before resuming. No client may become Host from a lease response. A competing elected host still loses to any live writer and all old terms remain unusable. This in-place recovery has different fences from transport replacement and needs dedicated tests before enabling a short lease.

The gameplay controller currently resets `hostReady`, physics composition, frame and command sequence state only when `ChangeRunner` sees a different Runner reference. `GameplayStarted` alone cannot rebuild an already-ready host. Same-Runner recovery therefore needs an explicit fenced lifecycle signal that disposes old physics/presentation registrations, resets host readiness and replica/admission state, reconstructs the exact checkpoint bodies, and revalidates readiness before resuming. It must not duplicate global drivers or scenes. Cancel/fence old host-generated publications and commands before restoring the new epoch; late task responses cannot update the new composition. This additional cross-file ownership must be assigned before implementing stage 2.

## Required focused tests before activation

- Real Worker/SQLite: omitted profile grants 15s; new profile grants 6s; legacy stored writer renews 15s; new profile stays 6s; same claim ID/profile is idempotent; changed profile fails; malformed profile fails closed; old public JSON field count remains seven.
- Exact boundary: takeover one millisecond before expiry is rejected; at expiry it increments term once and binds the latest committed head; old renew/begin/chunk/finalize/open fail. Lost claim response replay returns the same term without resetting its expiry.
- Client lifecycle: a renewal stalls until its 2s cancellation; another bounded attempt can run before expiry; a late old response cannot mutate a newer run/term; crossing the last known expiry freezes before accepting input; explicit Stop cancels outstanding work.
- Durability: maximum supported checkpoint upload/validation while renewal continues, coordinator outage, finalization crossing expiry, migration during a partial upload, and duplicate command/item/craft/lap/final-result effects after exact restore. Keep the 120-tick provisional bound and exact receipt identity.
- Native: actual host process loss, isolated transport blackout with coordinator still reachable, 2/4/8 humans, measured RTT/loss profiles, and recorded loss/election/claim/head/readiness/first-tick timestamps. Injected timeout shutdown proves rejoin behavior only. Accept <=10s and <=120 lost ticks only from the measured scenario; retain failures rather than lengthening correctness deadlines.

Next action: root selects the bounded host-state contract and assigns service/client ownership. No stage 2 implementation is included in this report.
