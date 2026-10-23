# TABLE native craft timeout investigation

STATUS: IN PROGRESS — bounded fixture diagnostics prepared; no native rerun or compiler job.

Observed root evidence: `TestResults/editmode_20261023T123439Z.xml` records the six-family fixture running for 363.172282 seconds. CollectLetters completed at canonical tick 2160, after which CratePress reached the TABLE craft wait. The failure says its 20-second deadline expired, but no craft/inventory/tick or cancellation state was captured at failure.

Verified source facts:
- BeginCraft returned its expected Ok receipt; the failure occurs after Command. This passed mode recipe policy, body admission, alive/CanAct, free slot, no carried furniture and required letter reservation at acceptance. Full inventory or missing letters would have failed earlier with the explicit receipt refusal.
- Economy.CompleteCraft produces a Crafted/Held item with HolderId equal to the local player index (0), reserves its destination slot and clears reserved letters. The fixture's held-state predicate matches that production policy. Its NextItemId lower bound excludes the preceding consumed TABLE. No auto-deploy assumption is required.
- Production crafting progresses on canonical ticks, using `(Tick + 1) / 60`; the authored rules give TABLE approximately 1.2 simulation seconds. Wall time alone cannot establish that the craft had enough simulation ticks. Neither the final job ReadyAt nor ending tick was logged.
- The fixture starts an independent six-minute CancellationTokenSource after EnterPlayMode. `Until` exits for either wall deadline or lifetime cancellation but previously described both as a deadline. The observed 363.17-second total including setup/cleanup closely matches that lifetime, making aggregate cancellation a strong candidate, not proven runtime causality. StartLocal links the token only during startup; after startup the fixture token can end the test wait without directly stopping the local session, so empty session.Failure does not exclude cancellation.

Bounded repair: retain the six-minute lifetime, 20-second craft wait, all game rules and held-output predicates. Add begin/accepted/completed craft evidence with inventory letters/reservation/slots, CanAct, craft job timing and remaining canonical ticks, item allocation frontiers and every matching item state/holder. Failure diagnostics distinguish lifetime cancellation from the operation deadline and record wait duration/start/end tick, runner readiness, simulation pause state and timeScale. Sampling occurs at craft boundaries or failure only, with no new per-frame scene searches or state polling.

Baseline-only commit `acc2c16` copied the current integration Flow/Actions bytes, preserving the existing CLOCK approach fix. It must NOT be cherry-picked. The subsequent diagnostic delta is the deliverable. No production bug is established by the available evidence; do not mark the six-family proof passed or merely raise its deadline.
