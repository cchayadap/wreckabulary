# Story presentation and gameplay rubric — story26

STATUS: SOURCE DELIVERED — root integration and native/visual/human acceptance pending

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

Root integrates `1fd6fc90`, `6348559f`, `e7a03e14` and this final evidence commit; excludes baseline `075b30b3`; preserves/integrates performance report `6cc4f20b` separately. Wire/generate the profile, execute the focused native checks below, and record actual view/capture and human evidence before claiming experience acceptance.

## 2026-10-26 source contract and rubric checkpoint

- Scope/interface proposal sent to root before runtime edits: optional profile/localizer overloads, copy-only immutable notice identities, one current earned in-world cue, full optional journal, no authority/endings changes. Root owns HUD wiring and generated assets.
- Confirmed current world signs instantiate every notice in the current lap; journal gating itself is correct. Narrowing the signs to the latest earned notice avoids premature story disclosure.
- Existing HUD reflection test invokes the seven-argument notice constructor. Preserve that exact constructor and `Journal` property, not just ordinary call-site compatibility.
- No story localization service found in inspected Online presentation/client paths. Add an optional key/fallback seam; do not imply localization is already shipped.
- Baseline synchronization commit `075b30b3` copies ten exact existing root story/test files from `9fce9f25`, including their already-generated metadata. **Exclude this commit when integrating**; no new metadata was authored.
- Added `docs/wordbound/GAMEPLAY-EXPERIENCE-RUBRIC.md`: anchored eight-dimension 0–4 rubric, source estimates separated from human evidence, seven participant lenses, causal gameplay application and ten measurable acceptance rows.
- Next: implement profile, plan cue selection and additive editor/tests; inspect diff, then send root exact native-test requests. No jobs have run.

## 2026-10-26 implementation checkpoint

- Root approved the bounded interface. Added `WordboundStoryProfile`: three chapter rows, fourteen default stable clue rows, short cue / full journal text and localization-key/fallback pairs. Validation rejects unknown/duplicate identities and empty/overlong authored copy; empty/absent/key-echo translations use fallback. Default population refuses to replace authored rows.
- Existing two-argument plan constructor, five-argument view binding, seven-argument notice constructor and `Journal` property remain. New profile/localizer arguments are additive. No-profile callers receive concise defaults and unchanged canonical journal prose.
- `ContextualCue` uses exact course identity, local-team reached journal and current lap; migration, disconnection, return, elimination and confirmed completion hide the world cue. Journal history is retained.
- View now creates at most one short cue at its existing recovery anchor and removes the automatic prose banner. Existing camera-facing, 20 m distance and scale bounds remain. Unspaced text wraps at Unicode text-element boundaries; actual glyph coverage is unverified.
- Added an Inspector with validation, empty-profile default population, Undo and canonical clue preview; added a Create menu. No asset was created in this lane, no `.meta` was authored, and no progress is awarded by preview.
- Added regression source for canonical copy/IDs, localization fallback, schema-free Unity serialization, team/future/recovery/transition/result cue gating, exact snapshot identity, unspaced Unicode wrapping and native asset save/reload. These tests are NOT-RUN here; root owns compilation and execution.
- Independent source review delegated before delivery; findings will be recorded separately. Static `git diff --check` passed; rubric lint passed 10 rows without warnings. No gameplay/performance/visual/human pass is implied.

### Root integration requests

1. Pass a configured `WordboundStoryProfile` as sixth argument to `StoryView.Bind`; seventh optional argument is `Func<string,string> localize`. Null profile preserves full canonical prose and uses default short cues. Generate assets/metadata in Unity only.
2. Run normal R0/core prerequisites, then focused native `WordboundStoryPlanTests`, `WordboundStoryProfileTests`, `WordboundStoryReadabilityTests`, `WordboundStoryProfilePersistenceTests` and existing `WordboundMenuGuiTests` for constructor/journal compatibility.
3. Capture current, next-chapter, restored and opponent-ahead states with original art. Verify one reached cue, no automatic story banner, optional journal navigation, 720p/1080p readability and actual Unicode font coverage. Human rubric rows remain pending.
4. Update the main story document's implementation status after integration; this lane intentionally did not overwrite the root-owned current document.

## 2026-10-26 final independent source review

- Reviewer `/root/story26/story_review` inspected the changed source, actual root course/callers and rubric; reported **no blocking source findings**. Confirmed all fourteen canonical cue IDs, preserved old call/reflection shapes, own-team/reached/current-lap cue gating and copy-only profile fields. No independent jobs or writes occurred.
- Review explicitly leaves real `Apply` → `LateUpdate` creation/removal across transition/reconnect, actual Unity asset persistence, on-screen Unicode glyphs and human experience pending. Plan-selection tests plus isolated text helper tests are not full view composition evidence.
- Runtime/source commit: `e7a03e14`. New regression source contains 22 NUnit cases; execution count/pass rate is not yet available. Root's normal verification owns compilation and all native runs.
- New authoring files require Unity-generated metadata at integration. No shared catalogue, core protocol, course identity, match result, scene or package change was made by this lane.
- Final source/diff check: PASS. Rubric acceptance lint: PASS, 10 rows, no failures/warnings. No push, release, asset generation spend or heavy job.
