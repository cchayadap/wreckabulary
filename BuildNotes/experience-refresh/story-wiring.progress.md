# Story profile launch wiring — story-wiring

STATUS: SOURCE READY — root compilation, setup execution and native verification pending

Lane `experience-refresh/story-wiring`, direct root worktree assignment, 2026-10-26 UTC. Starting root HEAD `031d8646` on `sethypagna/wordbound-integration`, clean observed status. Story source chain already integrated by root.

Scope: additive `WordboundLaunchAssets`, online/local match clients, Launcher call sites, HUD story binding, and new `Assets/_Project/Editor/Online/WordboundStorySetup.cs`. Root owns shared commits, Unity jobs, generated assets/metadata, portrait/motion work and tests. This report records the required evidence and narrative editing handoff. No other path edits.

Acceptance: existing initialization calls remain valid; configured StoryProfile reaches both live and local HUD/StoryView; a gate-callable setup method creates defaults only when missing/empty, preserves authored rows and any assigned launch profile, and is idempotent. No `.meta` or serialized Unity asset is authored by hand.

Confirmed: `WordboundLaunchAssets` currently carries camera, visual and UI profiles; both clients create HUDs explicitly, and story binding happens inside `WordboundMatchHud.Initialize`. Existing optional client parameters can be extended at the end without changing old call sites.

Next recoverable action: inspect all live/local Launcher call sites and existing setup conventions, then edit only the agreed fields/calls/helper. Read Unity editing guide and provide exact narrative-editing steps here. Root compiles, imports and runs native verification.

## Implementation and boundary

- Added `WordboundLaunchAssets.StoryProfile` and `WordboundMatchHud.StoryProfile`. The HUD passes it to the existing additive `StoryView.Bind` overload; the HUD's `Initialize` signature is unchanged.
- Appended optional `WordboundStoryProfile storyProfile = null` to the online `StartMatch` and local `StartLocal` parameter lists. Both set the HUD field before initialization. Existing source calls remain valid; no matching reflection arity use was found in the inspected test/caller paths.
- Launcher passes its configured profile to online and in-game Workshop local paths. Root explicitly extended scope for the Inspector `WordboundCoursePlaytest` argument, which now passes the same launch profile after its existing missing-launch guard.
- Added public gate-callable `Wreckabulary.EditorTools.WordboundStorySetup.Configure` and menu **Tools → Wordbound → Create or verify story profile**. Default asset path is `Assets/_Project/Data/Wordbound/Authoring/LostAddressStory.asset`.
- Setup requires Edit Mode, the existing launcher and item catalogue. It validates an assigned custom profile before changing anything. It rejects a conflicting asset type, populates only a new/default profile with both row lists empty, preserves all nonempty authored rows and title, and fills the launch field only if null. The second valid run performs no new dirty/save operation. `ConfigureAt(launch, destination)` is a private fixture seam for isolated native testing. This idempotence is source-reviewed, not an executed Unity result.
- Root added the helper assembly's direct `Wreckabulary.Rules` and `Wreckabulary.Online.Presentation` references; this lane did not edit the asmdef.
- Exact source diff inspected and `git diff --check` passed. No Unity/heavy job, generated asset, metadata write, shared commit or push was performed. A second independent wiring review could not start because the collaboration thread limit was reached; the earlier narrative slice's independent review does not certify this wiring change.

## Concise narrative editing instructions for the existing guide

Read `docs/wordbound/UNITY-EDITING-GUIDE.md`; the following is a proposed addition for root to integrate after native verification. The shared guide was not edited here.

1. In Edit Mode choose **Tools → Wordbound → Create or verify story profile**. This creates missing default copy and fills an empty **Story Profile** field in `Assets/_Project/Resources/Wordbound/LaunchAssets.asset`; it preserves existing assignments.
2. Select `Assets/_Project/Data/Wordbound/Authoring/LostAddressStory.asset`. For a personal version, duplicate it into your own folder and assign that copy to the launcher's **Story Profile**. Alternatively use **Assets → Create → Wreckabulary → Wordbound → Story profile with defaults**. A blank **Story profile** can be filled with its Inspector's **Populate existing Lost Address copy** button while both row lists are empty.
3. Under **Chapters**, edit each **Title → Fallback**. Under **Notices**, leave each canonical **Id** unchanged. Edit **Cue → Fallback** for the short in-world line (up to 96 characters; the defaults use seven words or fewer), and **Clue → Fallback** for the optional journal explanation (up to 1024 characters). A missing row uses the original/default copy. Avoid turning a cue into a paragraph.
4. Use **Copy preview → Clue** to inspect any canonical notice, including optional branch clues. The Inspector validates the profile and shows the concise cue beside its full journal text. Preview does not award a clue or trigger a chapter; the actual selected route and the reader's team progress still control visibility.
5. **Key** is an optional localization lookup identifier; empty keys use fallback. This wiring does not install or supply a translation service, so the current launcher uses authored fallback text. Keep IDs stable when translating, and verify actual font coverage in play.
6. Save the asset and start a fresh match/playtest to see revised copy; the story plan captures text when it is bound and does not hot-reload profile edits. Online matches, in-game Workshop playtests and Inspector course playtests use the launch profile. The profile currently applies to the existing Lost Address course; it does not add story triggers to an arbitrary custom course.
7. Play through an actual clue checkpoint. At most one concise earned clue appears near its existing anchor; open the equipment menu's **Story clues** entry to reread full collected text. Check another team's progress and a chapter transition. Short cues do not add a reading timer or pause the match.

The profile edits presentation copy only. It does not move checkpoint triggers, change objectives, create restoration tasks, author branching endings or alter the win condition. Title is currently used by the profile preview; chapter titles and clue copy appear in the journal.

## Root verification and exact handoff

- Native checks: compile the composed tree; run setup twice and compare profile/launch asset bytes; preserve an edited default cue and a different assigned custom profile; test an existing empty profile and a conflicting asset type; verify live/local/Inspector profile use and save/reload. Existing StoryProfile/StoryPlan/Readability/ProfilePersistence/MenuGui regressions remain relevant.
- Native command after prerequisites: `node Tools/kit/gate/game_gate.mjs exec Wreckabulary.EditorTools.WordboundStorySetup.Configure --json`. Root alone executes it and retains generated assets/metadata.
- Root commit paths: `Assets/_Project/Scripts/Online/Client/WordboundLaunchAssets.cs`, `WordboundLauncher.cs`, `WordboundMatchClient.cs`, `WordboundLocalMatchClient.cs`, `WordboundMatchHud.cs`; `Assets/_Project/Scripts/Online/Authoring/Editor/WordboundCoursePlaytest.cs`; `Assets/_Project/Editor/Online/WordboundStorySetup.cs`; this progress report and its done marker. The root-owned asmdef update is separate from this lane's file ownership.
- Source READY was sent to root before R0. Current next action is root compilation, idempotence fixture/setup execution and end-to-end cue/journal verification. No native/visual/human pass is claimed.
