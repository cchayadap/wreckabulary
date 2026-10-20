# Native migration proof review

STATUS: IN PROGRESS

Scope: read-only review of the installed Fusion 2.1.3 SDK and online adapter source. Root alone runs Unity and creates native assets. This report does not establish successful host migration, deployed custom authentication or configured service access.

Source checkpoint: online b06deb5; adapter source introduced in 39047cc. Root's native 2/4/8-player reliable transport probes passed separately. Those probes do not validate checkpoint durability or migration restoration.

Next recoverable action: inspect the exact PushHostMigrationSnapshot and resume callback semantics, record the native evidence that distinguishes accepted checkpoint bytes from local serialization, and review asynchronous lease/admission operations across Runner replacement.

## Confirmed source finding M1: asynchronous work is not fenced as a whole across Runner replacement

Priority: P1. `WordboundFusionSession.Admit` checks the captured Runner after a successful authorization reply, but its catch calls global `Fail` when `runner != Runner`. A failed old HTTP request can therefore freeze the replacement session. Its unconditional `authorizing.Remove(player)` can also remove a newer admission guard if the replacement Runner reuses that PlayerRef.

Publication has the same ownership gap: `publishing` remains true across `OnHostMigration`. A replacement host that finishes admission while the old push is pending can attempt its bootstrap checkpoint, receive false solely because `publishing` is still true, and fail with “Initial checkpoint was not durable.” Old publication catch/finally paths can fail or modify the replacement operation. `beginning` and `renewing` are likewise shared across Runner lifetimes. `SendAdmission` uses mutable Runner/join fields after awaiting a ticket, so a ticket request started for one Runner can send into its replacement.

Reproduction to require in an executable test: hold an old admission or push task; invoke migration and complete replacement admission; release the old task first with an exception and then, in a separate case, a successful response. Neither outcome may change the replacement's Failure, in-flight flags, admission guard, wire sends or ready state. Repeat with the same PlayerRef value. Stop must invalidate the same work as migration.

Suggested correction, pending coordination: introduce a monotonically increasing Runner operation generation and a cancellation source per Runner; capture it before every asynchronous path, and fence success, catch and finally mutations. Invalidate the generation before beginning shutdown, reset generation-owned in-flight state, and prevent stale credentials/ticket callbacks from touching mutable current-Runner fields. Cancellation alone is insufficient because a transport can finish after cancellation.

Correction now implemented after root authorization: each Runner has its own cancellation, admission guards and publication/begin/renew flags. Replacement invalidates the prior scope before shutdown. Success/catch/finally paths cannot mutate a replacement. Stop and terminal Fail invalidate pending work, including a bootstrap cloud push that later succeeds. Independent reviewer reproduced old success/error and terminal-bootstrap cases against the exact updated method bodies. Native cloud behavior remains a separate gate.

## SDK semantics and required evidence

The installed Fusion.Runtime.xml identifies Fusion2.1.3, describes PushHostMigrationSnapshot as computing/sending cloud state and returning success/failure, and describes HostMigrationResume as a callback for the replacement host. It does not specify an independent durable receipt containing our application sequence/hash. The [current API](https://doc-api.photonengine.com/en/fusion/current/class_fusion_1_1_network_runner.html) agrees. Therefore a local true result alone is insufficient evidence for the game's stronger exact-checkpoint claim.

Photon's [disconnect/reconnect sample](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect) requires a pushed snapshot, callback registration, shutdown of the old Runner, creation of a new Runner with the migration token, and reconstruction from resume objects. The adapter follows that lifecycle and deliberately restores application state from the canonical carrier rather than blindly copying arbitrary dynamic objects.

Root's native test should record non-secret evidence: Unity/SDK version, configuration hash, prefab ID, logical snapshot mark, raw/compressed lengths, chunk count, write tick, push-start/completion tick, returned bool, host-loss time, resume mark/hash, selected new host and final epoch. Never log credentials, admission JWTs, connection tickets/secrets or provider URLs.

| Native case | Reproducible expectation |
|---|---|
| Prefab/write timing | Spawn the real baked chunk prefab through the actual onBeforeSpawned callback. Write nonzero sentinels into first/last uints and every metadata field. Read them after a simulation tick. HasStateAuthority and all woven values must be valid; zero/default payload is failure. |
| Complete cloud restore | Push a valid multi-chunk canonical snapshot, await true, then terminate the host immediately without another push. A surviving client must create a different Runner, receive the resume carrier, reconstruct byte-identical JSON and the exact published mark, and restore under epoch+1. A local packet round trip or reliable peer delivery does not satisfy this case. |
| Last committed boundary | Commit S1, mutate the domain to S2 without completing another push, terminate the host. Restore S1, including its receipts, inventory, mechanisms, body states and original disconnect deadlines. Never replay uncommitted effects automatically. |
| Carrier replacement | Commit many chunks, replace them with fewer chunks and push again, then migrate. Only the replacement count/indices may exist; old leftovers, missing chunks or mixed marks must fail closed. Test the reverse size change too. |
| Capacity | Exercise a real representative eight-player domain snapshot and an additional high-entropy transport fixture approaching256KiB compressed envelope. Record actual total Fusion state cost, which includes NetworkObject/header/network-string overhead beyond that envelope. Failure must not silently reduce content. Values over the declared limit fail before publication. |
| Push failure or interruption | Do not acknowledge the journal when push returns false, throws, is superseded or never completes. Domain advances at most120 ticks beyond the last durable ACK and then pauses. Bootstrap never starts gameplay without its first successful complete push. |
| Double migration | Commit after the first migration, then lose the next host. Restore the second host's committed epoch and advance it exactly once. This catches the absence of a new carrier after resume and stale old-Runner task callbacks. |
| Dynamic reconciliation | Move a Rigidbody after the canonical snapshot is captured but before the cloud push. On restore, physical bodies must match canonical snapshot state, not a later raw resume-body value. Unadmitted/returned bodies stay disabled and cannot receive input authority. |

The packet's256KiB bound is an application limit, not a proven Photon capacity. The required experimental evidence is exact recovery after host loss at that size. The carrier's per-component state is larger than4KiB because its uint payload and metadata both serialize. If the cloud API cannot support the representative state or cannot recover the latest acknowledged mark, keep the release gate closed and change the persistence design explicitly.

## Admission, leases and actor boundaries

Use separate native milestones: carrier/cloud recovery can use the approved private proof room; full session admission requires a deployed approved coordinator and a correctly configured Photon custom-auth provider. A fabricated UserId in a private proof fixture does not prove production identity authentication.

Full-session scenarios must cover: outsider room precreation, fresh per-Runner nonce/proof, all initial approved seats ready, migration with one returning seat, coordinator Return newer than the frozen snapshot, expired one-use tickets, stale connection epoch, and temporary coordinator failure. With five-second renewals and15-second leases, commands and physics input must be denied at the exclusive lease deadline even before the next Poll. No later service reply may revive a terminal Returned seat.

The bridge's queued command behavior is safe at the domain boundary: the independent review queued actual commands before Return/Disconnect and obtained NotAlive receipts on the next tick. New transport input is additionally gated by TryGetAdmittedActor, which checks the current Runner, Photon identity, seat, readiness and live lease.

Pending source repair M2: the original client accepted Admitted/Begin without signed evidence that the room host belonged to the roster. Root authorized a fresh client nonce plus signed coordinator connection authorization before Ready/replica acceptance. Its adversarial tests and final checkpoint will be recorded separately.
