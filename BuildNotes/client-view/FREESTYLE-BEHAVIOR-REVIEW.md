# Shared Freestyle native behavior review

STATUS: SOURCE PASS — Online implementation **`a6a2a72b`**. Compiler/native execution pending; no heavy job run by this reviewer.

## Bounded plan reviewed before implementation

Online proposes one case in the existing `WordboundNativeWorkshopTests.cs` partial, using the existing two-peer private authored transfer, `Run(... afterStarted: ...)`, installed content bytes/geometry, `FullWalk`, `Command` and `CommittedState`. A compact Freestyle variant of the existing authored course has two reachable TABLE stations and a four-letter GIFT reward. No production change, body teleport, injected mechanism/progress/health state or longer deadline is needed.

The host selects the first station through normal `PracticePuzzle`; its teammate walks to real letters, crafts TABLE with the normal timer, deploys at the socket, waits for actual native hold evidence and submits `UsePuzzle`. Both peers must observe shared solved state and exact committed/acknowledged state. The test then proves one reward, collects one actual reward tile, switches the shared station through the other peer, and retains inventory/first-station solved state without race progress or result.

Source constraints checked in `AuthoritativePractice`, `AuthoritativeMatch`, `WordboundPracticePolicy` and the existing native helpers:

- Selection requires the exact site, host-validated reach/line of sight, and rejects already solved sites. Freestyle has one shared active site; a different teammate may solve its shared crafted/deployed item.
- Rewards mint loose tiles at the solver with shared owner `-1`; they do not directly grant player inventory. Assert exactly four new reward IDs and the matching letters. Replay the same authenticated command through `Session.SubmitCommand`, require its second observed delivery, and retain one receipt/no additional mint. Submit a separate fresh repeat expecting `AlreadySolved`; do not weaken the shared helper's existing `Ok` assertion.
- `CommittedState` checks the real durable frontier plus matching replica receipt/ACK. The private bundle/roster/content checks in `Run` stay intact.
- Station-switch preservation is bounded to the inventory and solved station actually exercised. It does not prove every resource-recovery, migration or transfer scenario.
- An accepted unarmed `Attack` must have actual canonical active-window observations. Require target range/facing, real public physics line of sight, an actor who can act, expired victim spawn protection and no defense/dodge masking before claiming unchanged health/stun/knockouts. `HitBodyIds` cannot prove a rejected friendly hit because such hits are intentionally not recorded. Observe via `FrameChanged`; a render-frame wait can miss the entire attack window during catch-up.
- No race objectives/result should appear throughout and on both committed peers. No hazard-immunity claim is warranted unless an actual hazard is exercised.

The plan is feasible within the existing fixture and unchanged budgets. Final scope is **compact private authored shared behavior with two authenticated scripted clients**, not public matchmaking, full flagship content, eight-player performance or human/Windows usability acceptance. Exact implementation and callback cleanup still require source review; root alone runs the native gate.

## Implementation review

Reviewed the exact new case and helpers in `WordboundNativeWorkshopTests.cs` against current integration APIs, including its existing FullMatch partial and real `Run`/command/ACK helpers. The historical Online lane does not contain those accepted helper files; this delta correctly does not overwrite them with an old baseline. Existing TimeRun, transfer and public cases are unchanged.

The factory copies the accepted compact authored document, independently resolves it for Freestyle, adds eight legal starts, a distinct second station and GIFT reward, and checks the original document is unchanged. It uses installed geometry/items/rules and retains exact private bundle transfer/roster checks. The host is parked outside the socket actor radius before the teammate's physical hold, making the asserted solver actor meaningful.

Reward checks use the public immutable snapshot counters, sorted tile IDs/letters and actual shared loose bodies. The successful command is resubmitted through the real session and requires a second observed delivery plus one receipt; a new repeat must return `AlreadySolved`. Both peers subsequently acknowledge the solved station, exact reward and rejection. Inventory baselines are defensive snapshot copies and compared after the actual shared station switch; the second station's five TABLE tiles must appear. No live economy/progress/pose field is edited.

The admitted unarmed attack has positive configured damage and real active-window `FrameChanged` samples. A qualifying sample must satisfy native and canonical reach/facing, public physics line of sight, actionable attacker and a live connected victim without spawn protection, dodge or defense. Health/stun/knockouts must remain unchanged through attack completion and replica ACK. This is bounded teammate-damage immunity evidence; the log correctly excludes hazards. The no-race observer spans the actual scenario interval, and temporary observers/input delegates are detached in finally.

No concrete source blocker found. Exact added filter: `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.TwoAuthenticatedPeersShareFreestyleStationRewardAndPreserveInventory`; expected **1 case**, with external gameplay and workshop flags enabled. `ExternalPhotonWorkshopGameplay` now contains 2 cases. The existing eight-minute Run lifetime, ten-minute NUnit limit and helper waits are unchanged. Require the final native XML and strict cleanup result before accepting behavior; early observation markers alone are insufficient.
