# Canonical entity lifecycle

STATUS: STAGES 1 AND 2 IMPLEMENTED; SOURCE VERIFICATION COMPLETE

The baseline observations and proposed stages below are retained as design history. Stage 2 implementation, independent review repair and current acceptance results are recorded in `progress.md` and `CAPACITY-RESULTS.md`. Native integration remains root-owned.

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

## Stage 2 implementation refinement

Root approved Stage 2 and the fingerprint seam after Stage 1. The proposed public helper is `WordboundEntityBudget`: Version=1, MaximumBodies=512, MaximumCanonicalEntities=8192, and a SHA256 Hash. Root owns adding Hash to WordboundRulesFingerprint. This section makes the reservation algorithm explicit before source changes.

Use a body partition rather than tracking the provenance of every letter in a player's bag:

- Fixed roots K: maximum roster players, race keepsakes, every authored prop/decoration body (including its eventual source tombstone), connection platforms and all team fixture bodies.
- Critical recovery C per pool: maximum simultaneously available designated recovery letters across any reached checkpoint prefix of any selected lap. For a safe bound within a checkpoint, prerequisites may include all of that checkpoint's puzzles while consumers include all not-yet-completed checkpoint puzzles. Supplies used by no puzzle are optional.
- Physical auxiliary reserve H per pool: one possible crafted receiver body per selected RedirectWind physical site. Authored receiver bodies already belong to K; double reservation is conservative. A live or destroyed auxiliary body remains pinned while canonical evidence references it.
- Actor/burst reserve B per pool: team capacity times `MaxLetters + MaxCarried*(largest enabled hand-recipe word length + 1) + MaxDeployed*(largest deploy-recipe word length + 1)`, plus the largest current required prop-break and checkpoint reward batch. It covers held/reserved/carry/deployment transitions, bag drops, expiry/break letters and temporary hit-ledger tombstones. Shared modes use their full maximum player capacity.
- Optional world allowance O is the equal pool share of `512 - K`, less C, H and B. A content/rules combination with no useful optional allowance is rejected before launch. Do not infer a new cap from available heap or increase 512.

Hand/carried/deployed items use B; authored map furniture uses K even while carried. Consequently crafting from an existing bag and deploying a required recipe do not need to consume optional world capacity. A voluntary drop/throw of an otherwise optional crafted object must first reserve a slot in O; a Capacity refusal leaves it held and unchanged. A free replacement wind receiver can use its site's single H entitlement, chosen deterministically and without allowing repeated duplicates. BALL flights and other airborne throws are never pressure-evicted.

Optional refill requires room in O and does not evict player equipment to mint more free material. If blocked, preserve generation and schedule a bounded canonical retry. Process retries by generation then source ID so a repeatedly collected earlier source cannot permanently starve another initial source. Advance the recovery generation only when refill actually succeeds. This also requires deferred initial nonrenewable resource batches to be represented by a deadline, so they can appear once capacity becomes available without becoming renewable.

Mandatory drops/breaks may temporarily use B. Before an optional admission, or when a mandatory burst would exceed a pool's hard share, reclaim only eligible loose optional value in stable-ID order. Current puzzle recovery, physical evidence, active hit references, held/deployed/airborne equipment and fixed roots remain pinned. There must be no whole-tick rollback loop caused by ordinary capacity pressure. Old optional loose material can be retired with checked Spent accounting; source-origin retirement must keep its recovery state consistent.

These are conservative proof-budget reservations, not performance measurements. Exact derived values must be reported against the flagship before accepting the policy. Host tests must deliberately saturate each pool with pinned throws/deployments and verify that essential crafting/recovery, mandatory drops and opposing-team progress still work. The state bound also covers retained economy tombstones: authored records plus selected solved mechanism references plus at most 512 body-associated rows must fit 8,192; unrelated destroyed craft rows remain compacted by Stage 1.

## Implemented integration contract

`new WordboundEntityBudget(mode, resolvedCourse, geometry, items, rules)` performs preflight using the already-resolved manifest and exact gameplay content. `AuthoritativeMatch` calls it before creating entities and exposes the immutable result as `EntityBudget`. The helper publishes `Version=1`, `MaximumBodies=512`, `MaximumCanonicalEntities=8192`, and its canonical policy `Hash`.

The root-owned fingerprint will bind that hash as a separate length-prefixed field and advance its own algorithm version to 2. It is not part of a client-supplied command or a mutable snapshot field. The root-owned reconciliation planner should consume `MaximumBodies`; authored content, client launch and workshop play validation should call the same constructor before party creation.

At import, the snapshot must satisfy both total pool usage and core usage excluding designated recovery and physical auxiliary bodies. Core usage cannot consume C/H even when the gross pool still fits. Pending crafts plus pinned core bodies cannot exceed the same capacity. This mirrors live admission/enforcement and prevents a formally importable checkpoint from forcing an unrecoverable pressure rollback on the next tick.

All retirement runs inside the existing authority transaction. A failed later command or host query restores the exact prior economy, bodies, resources, frontiers and conservation counters. Active evidence references and source identity remain intact; no unrelated game mode's Economy behavior or shared item catalogue is changed.
