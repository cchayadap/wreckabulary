# Proof gameplay rules binding

STATUS: SOURCE VERIFIED; NATIVE INTEGRATION PENDING

Root authorized a bounded Puzzles-only follow-up after `33064df`. The previous physical proof bundle pins geometry, item JSON and the puzzle plan, but omits the gameplay rules. Existing fixtures and the launcher derive effective rules with `Mode.CreateGameplayRules(book.For("Dibs"))`; preserve that exact base.

Add an optional rule-book source argument to the proof factory. The bound path pins exact rule-book source bytes plus a versioned complete effective GameRules fingerprint, exposes defensive rule clones, and verifies runtime rules against signed content. Historical omitted-argument fixtures remain explicitly unbound; production composition must use the bound path. No snapshot schema or online protocol changes.

API:

- `WordboundPuzzleProof.Create(itemsJson, modeId, format, seed, ruleBookJson: rulesJson)`.
- Require `proof.BindsRules`, use the defensive `proof.Rules` clone for controller initialization, and call `proof.Validate(binding, actualRules)`.
- `RuleSourceHash` pins exact UTF-8 rule-book content; `EffectiveRulesHash` pins every public GameRules field including complete Unarmed and BleedOut data.
- `WordboundRulesFingerprint.Version = 1`: explicit fixed field order, named length-prefixed values, invariant integer text, booleans as 0/1, and exact IEEE754 float bits as eight lowercase hex digits; SHA256 over UTF-8 canonical text.
- Bound bundle: SHA256 of `wordbound.proof-content|3|<Geometry.Hash>|<ItemSourceHash>|<Plan.Hash>|<RuleSourceHash>|<EffectiveRulesHash>`. The physical plan version remains 2.

The runtime checks bound gameplay rules in its host precondition. Unbound historical factories retain bundle version 2, report `BindsRules=false`, expose empty rule hashes and throw on `Rules` access. The production launcher must require the bound path. This deliberately preserves historical evidence while making the new path explicit.

Rule-book sources are byte/depth bounded before parsing. The fingerprint rejects nonfinite fields, missing nested rules, oversized strings and oversized bleed-out arrays. Reflection is used only in tests to ensure every current public field changes the explicit production fingerprint, so a new omitted rule field fails the regression.

Verification: 16/16 focused fingerprint + physical puzzle policy tests pass (7 new fingerprint tests, including every-field mutation coverage and an explicit Dibs-only override); installed Unity/Fusion SDK reference compilation has 0 warnings/errors, 5.41s. TRX: `TestResults/runtime-puzzles/rules-binding.trx`. Native Unity tests were NOT RUN in this lane. Next: integrate the checkpoint, generate script metadata through Unity, update the launcher to the bound factory, and run `WordboundRulesFingerprintTests` plus the existing puzzle suite.
