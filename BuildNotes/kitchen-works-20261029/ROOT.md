# Kitchen Works original-furniture implementation

STATUS: IN PROGRESS

Owner direction confirmed29October: modern neutral UI, vivid artwork and restrained colour accents, retaining original Wreckabulary character/item style. This applies across lobby, Shop, Loadout and Workshop.

Base: `93e09d0ada85cf65b0d109f432932a594b68d894`, `sethypagna/wordbound-integration`. The preceding creator-clarity work made verified progress: Windows build `20261029T001001Z`, six passing smoke flows and 74 strictly audited images. That build remains the available review build. Its map richness and visual acceptance are unfinished.

## Scope and ownership

Implement `flagship/NEXT-ART-IMPLEMENTATION.md` from `BuildNotes/creator-clarity-20261029`: preserve the complete 34-room, 72-site, three-lap course and add four genuine original kitchen furniture bodies in a separately versioned local course. Keep original materials, ordinary start, route reservations and authority semantics. Course-local definitions are disabled for crafting; furniture remains movable and breakable. No global catalogue, original imports, public matchmaking admission, kit or SDK changes.

- Root owns production integration, every native process, evidence review, saved assets and checkpoints.
- `content`: map_redesign drafts Rules factory, augmented Items source and meaningful deterministic tests.
- `authoring`: menu_redesign drafts additive saved-course/scene setup, source inspection and preservation tests.
- `physics`: play_settings_redesign reviews and drafts actual carry/drop/throw/snapshot/break checks in the existing offline assembly.

Delegates write only their lane drafts/evidence. Root integrates frozen exact files after review. Existing tracked and untracked work stays intact.

## Acceptance

- [x] Original model bounds/orientation/materials measured and nine raw views inspected. Gate003920PASS; all fronts are+Z, so the north-wall run uses yaw180. Fitted floor contact remains pending. [Source review](SOURCE-REVIEW.md).
- [~] Full-course factory and local item source preserve original content and pass deterministic geometry/resource checks.
- [~] Additive saved Course/LaunchAssets/authoring scene support normal Unity editing and preserve subsequent edits.
- [ ] Actual item physics and snapshots verified for all three new words; exact letters and ownership budgets checked.
- [ ] Identical-camera original versus richer Kitchen captures inspected in runtime, Workshop and authored preview.
- [ ] Normal-start first-leg regression on the variant, then real traversal to Kitchen; isolated tests do not prove full-route completion.
- [ ] Tested Windows delivery containing the new local content, with measured limits and owner visual review pending.
- [x] Unity preview room selector/Frame implemented. Native004234passes the actual full-map→Kitchen→whole-map flow while retaining saved source bytes, exact content and ordinary start.

The complete project goal remains active. This is one destination improvement; online acceptance, full-route gameplay, wider art/animation/skin variety and other remaining tasks stay in `progress.md`.

## Next recoverable action

Review the frozen source-inspection helper, run cheapest required gates, then measure original Fridge/Counter/Stove models before finalizing poses. Integrate lane drafts sequentially. Never regenerate over owner-authored assets or claim a local course is admitted to public multiplayer.
