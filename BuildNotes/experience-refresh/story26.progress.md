# Story presentation and gameplay rubric — story26

STATUS: IN PROGRESS

Lane: `experience-refresh/story26`. Owner: SethyPagna. Started 2026-10-26 UTC.

## Scope and baseline

- Worktree: `wordbound-puzzle-race`; branch `sethypagna/puzzle-race`; clean starting HEAD `6cc4f20b4beda6e2673abb40220def6b3f7a0ed3`.
- Preserve performance report commit `6cc4f20b`, pending root integration. Root presentation source baseline is `9fce9f25` in `wordbound-integration`.
- Own this report, a concrete gameplay rubric, existing `WordboundStoryPlan` / `WordboundStoryView`, and additive narrative profile/editor/tests after contract inspection.
- Preserve clue IDs, team progress gating, existing transitions, mode rules and localization compatibility. No ending resolver, authoritative protocol, shared catalogue, lobby/player hooks or kit changes.
- Root owns all Unity/build/player/core/heavy execution and generated metadata. No heavy jobs or hand-authored `.meta` files in this lane.

## Confirmed observations

- Read portable harness startup/core/build and specialist workflow, project `CLAUDE.md` / `docs/kit/HARNESS_RULES.md`, and shared lane recovery registry. The portable binding still points to the original checkout; all work here uses the explicitly assigned isolated path.
- Current lane lacks `WordboundStoryPlan` and `WordboundStoryView`; root has the existing implementation. Baseline synchronization will be an explicit excluded commit before scoped implementation.
- Root story document separates existing three-chapter clues/journal from proposed restoration, epilogues and Unity authoring. No human research or player comprehension evidence is established by that design document.
- Root `WordboundStoryPlan` derives reached clues from the selected flagship route, exact course binding, local actor team and narrative flags. Root `WordboundStoryView` displays full clue prose on nearby signs and an eight-second banner; those surfaces currently duplicate optional journal content.

## Verification / limits

- Initial Git status: clean. Git author configured as SethyPagna; no assistant coauthor trailers.
- Investigation is source inspection only. Unity, visual, multiplayer, gameplay, performance and human acceptance: NOT-RUN for this lane.

## Next recoverable action

Inspect root story callers, localization and existing tests; send exact additive interface and scope to root before runtime edits. Write rubric with anchored scores, confidence, perspectives and measurable acceptance. Then implement the smallest safe profile/cue slice and hand root exact commits and verification requests.

## 2026-10-26 source contract and rubric checkpoint

- Scope/interface proposal sent to root before runtime edits: optional profile/localizer overloads, copy-only immutable notice identities, one current earned in-world cue, full optional journal, no authority/endings changes. Root owns HUD wiring and generated assets.
- Confirmed current world signs instantiate every notice in the current lap; journal gating itself is correct. Narrowing the signs to the latest earned notice avoids premature story disclosure.
- Existing HUD reflection test invokes the seven-argument notice constructor. Preserve that exact constructor and `Journal` property, not just ordinary call-site compatibility.
- No story localization service found in inspected Online presentation/client paths. Add an optional key/fallback seam; do not imply localization is already shipped.
- Baseline synchronization commit `075b30b3` copies ten exact existing root story/test files from `9fce9f25`, including their already-generated metadata. **Exclude this commit when integrating**; no new metadata was authored.
- Added `docs/wordbound/GAMEPLAY-EXPERIENCE-RUBRIC.md`: anchored eight-dimension 0–4 rubric, source estimates separated from human evidence, seven participant lenses, causal gameplay application and ten measurable acceptance rows.
- Next: implement profile, plan cue selection and additive editor/tests; inspect diff, then send root exact native-test requests. No jobs have run.
