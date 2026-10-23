# Static validation metadata cache

STATUS: DESIGN RECORDED BEFORE IMPLEMENTATION. Root has authorized a bounded cache after actual native attribution. No cache code has been changed yet; root owns all heavy execution.

The completed physical profile in `Logs/gate_test_editmode_20261023T150959Z.log` reports 3,481 reconciliation invocations across 64 sampled frames: Validate 127,330.950 ms, PhysicsApply 130,675.734 ms, Presentation 1,613.030 ms, Advance 30,770.228 ms and Capture 12,677.063 ms. These inclusive scope totals are not additive wall time. Physical had no buffer wrap; public marker retention was incomplete and must not be treated as zero CPU.

## Baseline measurement contract

Deliver the standalone benchmark before production edits so root can execute it against unchanged validation. Load the checkout's actual `Assets/_Project/Data/Config/items.json` and `rules.json`; create the actual flagship with seed `0123456789abcdef`; derive rules with the same `mode.CreateGameplayRules(book.For("Dibs"))` path; construct valid TimeRun/Solo and TeamRace/Solo matches with two seats and freeze their initial snapshots. Use real Rules source and real rules fingerprint code, not a proof course, stub or copied validation implementation. This benchmark binding identifies its local deterministic fixture and is not a public admission certificate.

Warm validation and then measure three bounded batches of 20 validations for each frozen frame. Emit each batch's elapsed time, current-thread managed bytes, validation error count, and positive 65,536-byte allocation controls before/after. Emit exact item/rule/effective-rule/geometry/mode/course/manifest/binding/snapshot hashes, seed, roster, tick and section counts. Require positive calibrated bytes, unchanged snapshot hash and zero validation errors. Timing is informational with no pass threshold. Run the same benchmark binary sources, actual data and frame identity before and after the cache; compare the repeated-batch evidence without claiming Unity throughput from .NET numbers.

## Bounded implementation contract

Use weak exact-identity course/mode keys and privately published immutable derived metadata. Cache only static route/source/placement metadata, lifecycle and internal entity-budget analysis. Every invocation still checks current binding, snapshot, roster, mode/course match, mutable catalogue/rules and all dynamic actor/economy/resource/mechanism/physical invariants. No cached successful validation, bypass overload, wire/schema/content-hash change or global strong dictionary. Prove used course/mode properties are immutable public wrappers and keep mutable Rules/ItemCatalogue values outside the cache. Tests must cover cache reuse with valid and invalid later snapshots, changed mutable rules/catalogue, exact course/mode separation and unchanged error behavior.
