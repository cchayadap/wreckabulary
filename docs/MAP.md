# Wreckabulary project map

| Area | Path | Responsibility |
| --- | --- | --- |
| Approved scope | docs/wordbound/PLAN.md | Owner-approved product requirements |
| Actual progress | progress.md; docs/wordbound/STATUS.md | Integrator; checklist and evidence |
| Pure game rules | Assets/_Project/Scripts/Rules | Wreckabulary.Rules assembly |
| Runtime | Assets/_Project/Scripts | Wreckabulary assembly |
| Editor authoring | Assets/_Project/Editor; Assets/_Project/Scripts/Online/Authoring | Integrator; changes to assets through Unity |
| Separate course entries | Assets/_Project/Scenes/Authoring/Courses; Data/Wordbound/Authoring/FlagshipKitchenV1 | Feature-owned saved scenes/settings; see docs/wordbound/SCENES-AND-TESTING.md |
| Team handoffs | docs/wordbound/TEAM-WORKFLOW.md; docs/wordbound/templates/FEATURE-HANDOFF.md | Actual human owners, additive paths and shared integration boundaries |
| Runtime tests | Assets/_Project/Tests | Wreckabulary.Tests |
| Rules tests | Assets/_Project/Tests/Rules | Pure NUnit and lightweight RulesHarness |
| Asset/editor tests | Assets/_Project/Tests/Editor | Wreckabulary.Editor.Tests |
| Authored worlds | Assets/_Project/Worlds | Preserve existing authored scenes and prefabs |
| Source art | ArtSource | Add WordboundPvP collection independently |
| Existing browser game | Web | Preserve website and score API |
| New coordinator | OnlineServices | Separate deployed service; final public activation pending |
| Verification tools | Tools/kit | Gate commands; generated projects/logs ignored |

Scenes: Hub, LivingRoom, Tutorial, MovingDay, WordboundLobby and WordboundGameplay, in saved Build Settings order. All scene registration and shared catalogs belong to one integrator.

Separate Flagship/Freestyle/Parcel/Kitchen development scenes have explicit course-entry menus and their own LaunchAssets. They remain outside release Build Settings. Shared WordboundGameplay resolves the selected course at runtime; UI pages have separate saved prefabs and the three asset studios remain separate scenes.

Recovery: portable harness run run-7971495a-26f3-4750-8c9c-dad132175de8; baseline and pending actions in wordbound/STATUS.md.
