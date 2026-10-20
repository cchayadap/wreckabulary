# Canonical entity lifecycle

STATUS: PROPOSAL — NO DOMAIN CHANGES OR COMPILER RUN

Inspected baseline: `17c30b4` in `sethypagna/puzzle-race`. Independent content evidence: `70723cd`, `BuildNotes/flagship-content/BODY-BUDGET.md`. The measured startup is at most 222 bodies; the 1,342-body Team Race figure is a deliberately slow three-lap refill scenario estimate, not an observed playthrough. The runtime reconciliation limit remains 512. Domain and wire bounds currently permit 8,192 bodies, so a valid domain tick can exceed the runtime limit.

## Confirmed causes and references

- `AuthoritativeMatch.Synchronize` clears an inactive lap's recovery deadline but retains that lap's loose tiles. Every lap creates a separate source generation.
- Every consumed or broken item remains in the economy. Its inactive world body also remains. The journal replays a stored effect without looking up its entity, so receipts alone do not require a body or item tombstone.
- Active melee hit lists and thrown-flight hit lists require their referenced bodies. Unsolved mechanism item references require a live deployed body. Solved mechanisms require their economy item, but need not retain that item's body. Physical smash/wind auxiliary evidence currently requires both an economy item and a body.
- Economy conservation is `tiles + bags + reserved + locked item letters == Minted - Spent`. Removing a live loose tile therefore requires an explicit checked increase to Spent. Removing an already-destroyed item has zero letter effect.
- Item and tile allocation frontiers need not be contiguous. Removed IDs can stay retired without reuse or additional tombstone arrays.
- The flagship owner confirmed every puzzle, tool and receiver supply is unique to its consuming puzzle and appears in that puzzle's RecoverySupplyIds. Practice_* supplies are independent always-on optional material.

## First coherent implementation

1. Derive recovery demand from the selected manifest. A supply referenced by one or more puzzles is active only while at least one consuming puzzle in the team's current lap remains unsolved and its existing AfterPuzzleIds are satisfied. A supply not referenced by any puzzle keeps its current general-resource behavior. Use the resolved supply SourceId to match source-document recovery IDs; do not compare generated team IDs directly.
2. After ordered progress advances, retire only loose tiles belonging to completed team lap generations, or to a designated recovery supply whose selected consuming puzzles are all complete. Remove their economy tile and body atomically, count retired letters as Spent, clear their recovery deadline, and do not refill. No player bag, craft reservation, held item, deployed equipment, in-flight item, rival team supply, future-lap row, or authored prop is removed by this step.
3. Remove destroyed item bodies when they are not required by active attack/flight hit lists or physical auxiliary evidence. Remove the corresponding economy tombstone only when it is also absent from mechanism item references and authored source identity needs. Keep historical puzzle evidence exact. Do not rewrite solved evidence to make deletion possible.
4. Run cleanup inside the existing tick/return transaction before publication. Restore and command replay retain the same allocator frontiers and canonical result. No new snapshot fields are needed for these derived operations.

This slice reduces retained history and the flagship's idle supply floor. It does **not** alone bound arbitrary optional crafting in Freestyle and must not be advertised as a complete 512-body guarantee.

## Full runtime bound: proposed pressure contract

Use one shared policy constant of 512 for domain admission, snapshot validation and runtime reconciliation; do not raise it. Count actual canonical bodies, including inactive bodies retained for evidence.

Reserve fixed authored roots plus mandatory players, keepsakes and all fixture roots before assigning equal dynamic allowances to race teams. Shared modes use one dynamic pool. Competitive allowance must depend on the mode's team capacity, not currently connected humans. One team cannot consume the other team's reserve. Large workshop maps whose fixed roots and mandatory recovery reserve cannot fit are rejected before launch with an actionable budget error.

Mandatory reserve must include the largest reached-checkpoint recovery batch, pending craft completions, held/deployed/in-flight bodies, all player bag drops, and required prop-break/reward expansion. Compute it from the actual rules, item definitions and authored checkpoint consumers; do not assume the flagship's counts.

At pressure, retire eligible objects deterministically within the requesting pool: obsolete source tiles first, then unreferenced destroyed bodies, then loose optional letters, then unheld/unthrown/undeployed crafted world items that are not physical puzzle dependencies. Retiring live optional value spends its letters. Never evict live players, keepsakes, authored fixtures/props, active mechanisms, held/reserved value, deployed effects, airborne throws, designated active recovery tiles, or an actor's current hit-ledger reference. Use stable IDs as the final order; no local-clock or random eviction.

If all remaining allowance is pinned, refuse a new optional creation with a stable Capacity effect before spending letters. Defer a general renewable refill until capacity is available. Critical recovery uses its reserved allowance. Mandatory damage, return/disconnect inventory drops and already-started craft completion must have room reserved before the triggering action; they must not throw the whole match into a permanent retry/freeze.

The pressure rule is a visible gameplay policy: abandoned loose optional material can be recycled while held equipment and current puzzle supplies remain. It should be named in concise workshop/runtime help and bound by a versioned policy/content hash before mixed clients can join. It needs coordinator approval before implementation because it goes beyond history cleanup.

## Verification contract

- Complete a real three-lap proof route slowly enough for refills before each lap transition. Publish/import every transition and after old deadlines. Assert obsolete source bodies are gone, future sources still mint, completed evidence still validates and no ID is reused.
- Two teams at different laps: retire only the advancing team's old scope. Equal teams starting from equal state receive equal allowance independent of command arrival order or an undersized party.
- Shared recovery source referenced by two puzzles: solving one keeps the source alive until the other consumer is complete. A supply with no puzzle consumers keeps general behavior.
- Smash/wind historical auxiliary bodies, solved item tombstones and active BALL/melee hit targets survive while referenced. They become removable only when the exact reference is no longer required.
- Replay a retained command after its output body was retired; receipt/effect/frontier stay unchanged. Rehashed hostile snapshots cannot delete still-required identities or reintroduce obsolete active source generations.
- Inject a failure after retirement and compare the full canonical snapshot with its pre-tick form. Repeat around committed migration and at the recovery deadline.
- Repeated optional craft/drop/break/refill cycles in Freestyle remain at or below 512 and bounded economy rows. At a fully pinned allowance, optional operations return Capacity without inventory loss or match failure; after capacity frees they can succeed.
- Full flagship end-to-end body maxima and native performance remain separate gates. Re-run Sethy's actual startup/estimate fixture after the policy is integrated, then measure a played traversal rather than replacing its old estimate with a claimed playthrough.

## Ownership request and next action

The first slice needs `AuthoritativeMatch.cs`, new `AuthoritativeLifecycle.cs`, the existing Wordbound-local Economy partial (or new local partial helper), `MatchStateValidation.cs`, and focused lifecycle tests. Physical auxiliary validation remains unchanged unless a confirmed validation hole requires a separate fix.

The full bound additionally needs a shared policy helper consumed by `WordboundGameplayPlan.cs`, preflight admission/restore validation, and a versioned policy identity. No Controller, Session, service, wire-command enum, UI, shared non-Wordbound economy behavior, or content catalogue edits are proposed.

Next: obtain root agreement on the two-stage contract and exact shared-file ownership; implement only the agreed stage; wait for the compiler window before running focused verification.
