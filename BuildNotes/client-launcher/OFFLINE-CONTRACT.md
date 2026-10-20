# Local workshop playtest boundary

STATUS: PROPOSAL ONLY — no Session/controller/physics changes or offline acceptance

Root requested this bounded design while the native online baseline is being verified. Keep implementation after the party source checkpoint and settled native startup.

Use an `IWordboundGameplaySession` boundary for the controller's actual dependencies: Runner, completed RunnerStarted, GameplayReady, Committed, Failure; ConfigureScene, MarkLocalReady, Poll, SubmitCommand, PublishCheckpoint, Stop and TryGetAdmittedActor; GameplayStarted, ReplicaReceived and Failed events. Keep existing online FusionSession behavior and verification intact. Derive the smallest final signatures from actual controller call sites rather than copying unused networking operations.

A separate local implementation starts the installed Fusion SDK in `GameMode.Single`, in a registered isolated gameplay scene. It owns one explicit local actor and resolves the same course, item catalogue and effective rules. Local input maps only the actual local PlayerRef to that actor. It uses the same AuthoritativeMatch, command journal, Rigidbody player/props, puzzle hooks, deploy effects, equipment and presentation. It never calls coordinator, Photon authentication, room APIs or cloud snapshot APIs.

Add a clearly named controller `InitializeLocal` entry point taking a validated local MatchBinding and content. Share only the content/domain/physics initialization body with the existing verified-online initializer. No fabricated JWT, permissive verifier, synthetic online certificate or fake approved roster is involved. Prefer a distinct local protocol/binding identity so an offline state cannot accidentally become an online match.

Local checkpoint publication exports the entire canonical snapshot, strictly imports it against the exact binding/content and verifies its hash before acknowledging that exact immutable mark. Its recovery is memory-only unless an explicitly separate save feature is added. Do not label it cloud durable or promise host migration.

Start with one-human TimeRun and Freestyle workshop playtests. TeamRace/BrawlRace/FreeForAll require multiple real seats under current ModeDefinition; preserve those constraints rather than inventing opponents. A workshop map authored for a competitive mode may need an explicit local TimeRun validation profile or clear unsupported-playtest message. Validation must still check the chosen profile's route, checkpoints, recipes and puzzle requirements.

Acceptance must include a network-disabled native run that resolves the actual workshop document, moves/jumps with the normal motor, collects letters, crafts and places an item into an actual authored socket, completes its physical condition and records a canonical checkpoint. Verify no HTTP/Photon cloud requests, no client-driven domain bypass, complete cleanup and that returning to the editor preserves the authored document and seed. Multiplayer physics/races remain a separate online proof.
