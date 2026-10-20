# Defense review and Inspector authoring

STATUS: IN PROGRESS

- [x] Confirmed own clean lane at 27e0c53 and reviewed owners; root owns native execution, HUD, PlayerView and Guide.
- [x] Independently reviewed defense commits 1cea56e and 1e48142 against practice 27e0c53. No production edits in the reviewed lane.
- [ ] Audit concrete Unity Inspector/editor entry points and propose additive asset profiles.
- [ ] Implement approved isolated authoring surfaces with content-validation boundaries.
- [ ] Source verification after the compiler slot is granted.
- [ ] Native verification: NOT RUN; root owns Unity.
- [ ] User editing acceptance: NOT RUN.

## Defense review result

No confirmed blocking source defect in this bounded review. Inspected exact defense codec keys, snapshot cross-references and lease bounds; selected equipment and actor availability; refresh/replay; per-tick expiry; drop, craft, disconnect, return, fall and migration normalization; complete actor copies; directional melee and incoming-projectile arc; blocked damage wear and knockback; authenticated facing bounds. The canonical hash includes the new defense fields and the rules fingerprint includes the versioned defense policy.

One test gap was sent to the owner: a shield-breaking hit followed by a later fault in the same authoritative tick must roll back defense, durability, returned letters and journal; retry must release letters only once. Sethy accepted this regression and is implementing it separately. Existing tests cover direct recovery preserving original expiry, malformed rehashed states, replayed hold, exact expiry, front/rear and BALL side selection. These are inspected tests; this reviewer ran no compiler/native execution.

Next recoverable action: implement root-approved additive Inspector authoring assets/tools; no shared Client/Presentation/Rules edits. Root retains native and user acceptance. No compiler is authorized in this lane yet.

## Root consumer review

Read-only working-file SHA-256 at review (integration checkout, after profile wiring):

| Path relative to Scripts/Online | SHA-256 |
| --- | --- |
| Client/WordboundMatchHud.cs | BD4197D57C21411E17279314D340FC601538F2FE313B5256DB289EC1FF8CCC9E |
| Client/WordboundPlayerView.cs | B4B1F38E1968B0779DE6ECF16D5A4BC9223F3124C11D2C3EFC39E98143246021 |
| Presentation/WordboundPuzzleGuidePlan.cs | 136C440E9665A0D22FB499F56EF98A53A6DC01A0CEDECC2DC101062909F7A52F |
| Presentation/WordboundVisualResolver.cs | 05AD3B502E70DBB8E17C2B803EB4C9C39A96C3886F55E329597998CC5AB602ED |

No confirmed blocker in Block hold/refresh/release, menu/focus pause, selected-shield checks, bounded camera-facing input or Freestyle guide/use selection. ReleaseDefense clears state before sending Release, preventing recursion through Send(non-Block). Failed delivery is bounded by the canonical lease. Practice selection remains available in the menu when nearby props take Interact priority. Native input/callback ordering remains untested by this reviewer.

Actionable V1: an overridden model without Grip_R still falls back to the replaced item's definition.Grip. Those coordinates belong to the old model and can offset replacement artwork away from the hand. Sent root the bounded correction: require Grip_R or use replacement bounds center for overrides; retain definition.Grip for original models only. Source finding, no native reproduction claimed.

Actionable V2: ModelKey ran before Profile.TryInstantiate, so a validated custom course prop with a profile override still failed unless it used an existing item/default model key. Root reports deferring default lookup until override absence and adding custom-ID variants to the bounds/grip regression. Both V1 and V2 are reported fixed by the owner; root native verification remains pending. No shared files edited by this reviewer.
