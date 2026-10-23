# Atomic reconciliation and validation proposal

STATUS: SOURCE AUDIT COMPLETE; INDEPENDENT DEPENDENCY REVIEW AGREES; IMPLEMENTATION NOT AUTHORIZED. Source-only audit requested by root after the 16:39 native scheduling observation. No implementation, tests, native jobs or deadline changes in this lane. Root source inspected at `c40fe19175da077faec9347d687976143b67db67`; subsequent integration changes must be checked before implementation. The separate resource test repair `0faf11b` takes priority for verification.

## Confirmed boundaries

`WordboundGameplayController.Reconcile` validates before input-authority resolution, any recursive disconnect handling, spawn/despawn, presentation configuration and physical application. `WordboundPhysicsWorld.ApplySnapshot` validates again, then checks no rewind, item motion, registration/update compatibility and authenticated seat before assigning its frame/applying bodies.

The two calls do not necessarily use identical mutable dependencies: PhysicsWorld clones GameRules in its constructor and exposes that mutable clone publicly through Rules. Controller retains a separate private clone. ItemCatalogue is shared but returns mutable ItemDefinition objects and nested stats/arrays. Both values can change between calls.

The interval is synchronous but contains callbacks: runner Spawn/Despawn invokes component lifecycles; Watch and GameplayBody.Configure can call PresentationChanged → VisualChanged → options.ConfigureVisual. A callback may stop/dispose/reenter or mutate accessible physics rules/catalogue. Recursion after missing admission captures a different snapshot. FrameChanged is currently after successful ApplySnapshot and frame assignment; it is outside the duplicate-validation interval and must remain there.

Economy construction (`Economy.cs`) only stores non-null catalogue/rules references. It does not run expensive mutable dependency validation. Snapshot validation then AddPlayers and Economy.Restore; Restore.ValidateReplacement checks roster/inventories/crafts/items/tiles, allocates copies and audits conservation/ownership. These checks remain live and cannot be cached merely because the catalogue or rule object identity is unchanged.

Deleting either full validator or adding a public `trusted`/`skipValidation` flag is therefore not a safe change. A callback-based wrapper without dependency/lifetime fences also does not make the sequence atomic.

## Smallest recommended candidate

Use one synchronous validation/reconciliation/application operation, retaining a fresh second pass over every phase that consumes mutable dependencies. It avoids repeated immutable snapshot checks without needing catalogue/rule equality fingerprints, persistent validation receipts or a mutable-value cache. It does not promise to eliminate the entire second validation cost.

1. Keep public `ApplySnapshot(snapshot)` fully validating with its current physics Rules and all current physical restoration checks. Extract its post-validation implementation privately; never expose an unchecked/trusted overload.
2. Add a public operation on PhysicsWorld that owns full pre-validation, a synchronous reconciliation callback, fresh mutable-dependency validation, and private physical apply. No receipt escapes the call, no async delegate is accepted, and the callback can abort the outer attempt. It must use the controller's initial rules for the first check and the current PhysicsWorld.Rules for the second phase; their identity/equality cannot be assumed.
3. Keep the reusable Rules entry atomic too: a single public validated-operation entry always performs the complete existing validation first, invokes preparation, then the private mutable phase, then the private caller-owned application action. Do not make the partial validator independently public. The existing `Validate` behavior and exception/message contract remain unchanged. Exact API names should be settled only after root authorizes implementation.
4. Abort on a disconnect retry, changed runner/physics instance, stopping/faulted/disposed world or superseded attempt. Run the recaptured retry after the old operation has returned, preserving the existing bounded presence retry count. Keep exact snapshot reference throughout the successful attempt. Nothing from an older attempt can certify a new captured frame.
5. Keep physical no-rewind, body membership, ItemMode, ValidateUpdate and authenticated-seat checks fresh at apply, in their original order. Keep FrameChanged after successful physics application and controller frame assignment. Do not move visual or lifecycle callbacks across this boundary solely to make the optimization appear safe.

This is validation atomicity, not rollback of Unity spawn/despawn side effects. Existing fail/cleanup handling remains responsible for exceptions during preparation. Explicit guard cleanup must run in finally on abort and exception; unexpected callback reentry must not apply an older outer frame after a newer attempt.

The preparation callback must remain outside Validate's exception-to-message catch: its original exception type and failure handling must propagate unchanged. Only validation phases retain the existing validation error conversion. Independent recover_input source review agreed with the five mutable phase boundaries below and specifically confirmed this exception boundary and the separate rule-clone requirement. This is design feedback, not approval of an unimplemented API.

## Second-phase audit

The first full validation still performs every existing assertion. Because public MatchSnapshot/EconomySnapshot data and the resolved course/mode used here are immutable to callers, only the following phases can change their answer solely because a preparation callback mutated accessible rules or item data. Repeat each complete existing phase, in its original relative order, rather than extracting individual predicates in this first implementation:

| Repeat after preparation | Mutable inputs that require it |
| --- | --- |
| Construct Economy, AddPlayer and Restore | MaxCarried, StarterLetters, MaxLetters, pending recipe enabled/Letters and catalogue lookup; retain allocation/ownership/conservation checks and original errors |
| ValidateActors | MaxHealth, MaxStamina, stagger/unarmed timing, melee and shield definitions; includes defense checks |
| ValidateDeployments | Current deployment definition and lifetime |
| MatchThrownValidation.Validate | Current thrown item definition and statistics |
| ValidateBodyBudget | Current MaxDeployed; retain the whole existing phase initially |

Binding/roster/play/counters, MatchProgress restore, command-journal state, wire counts, practice, resources, body/source/fixture identity, mechanisms/physical evidence, random streams and narrative checks use the unchanged snapshot plus immutable binding/mode/resolved course. They run fully before preparation. There is no cross-call result reuse: another operation starts with full validation again.

Source anchors: `MatchStateValidation.cs` Validate (65 onward), BodyBudget (140), Deployments (202), Actors (216), Resources (277), Bodies (323), Practice/Mechanisms (433/445); `EconomySnapshot.cs` ValidateReplacement (73 onward); `WordboundPhysicsWorld.cs` ApplySnapshot (113 onward); `WordboundGameplayController.cs` Reconcile (273 onward), Watch/VisualChanged (414 onward); `WordboundGameplayBody.cs` Configure/Apply (25/37 onward). Reconfirm these offsets after integration.

## Required focused evidence before adoption

- Invalid snapshot fails before preparation or body changes; direct public ApplySnapshot continues rejecting all existing invalid cases.
- Preparation changes PhysicsWorld.Rules only, catalogue recipe enabled/letters, combat/defense/thrown/deploy stats and MaxDeployed: the fresh phase rejects exactly as a complete second validation would. Include valid-to-valid mutation acceptance and original controller/physics rule-clone divergence.
- Malformed versus mutable competing errors retain order: initial invalid snapshot wins before callback; after a valid initial frame, fresh mutable phases preserve the existing second-validation ordering.
- Callback abort/throw, disconnect retry, disposal/stop, runner replacement and attempted recursive application cannot publish FrameChanged or apply the abandoned frame. A subsequent independent valid operation still succeeds.
- Native steady frames, add/remove and carried/deployed shape transitions preserve body/shape/grip/application ordering. Measure unchanged actual flagship workload and marker costs; do not count a theoretical halving as evidence.

Do not introduce a deep catalogue hash merely to skip the fresh mutable phase: ItemCatalogue exposes mutable definitions, nested objects, Letters, arrays and lists, while lookup keys can differ from a mutated ItemDefinition.Id. A correct fingerprint/equality scheme would add a separate maintenance and performance problem. The phased atomic operation is narrower and keeps current mutable checks live.
