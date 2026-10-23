# Wreckabulary project map

| Area | Path | Responsibility |
| --- | --- | --- |
| Approved scope | docs/wordbound/PLAN.md | Owner-approved product requirements |
| Actual progress | progress.md; docs/wordbound/STATUS.md | Integrator; checklist and evidence |
| Pure game rules | Assets/_Project/Scripts/Rules | Wreckabulary.Rules assembly |
| Runtime | Assets/_Project/Scripts | Wreckabulary assembly |
| Editor authoring | Assets/_Project/Editor | Integrator; changes to assets through Unity |
| Runtime tests | Assets/_Project/Tests | Wreckabulary.Tests |
| Rules tests | Assets/_Project/Tests/Rules | Pure NUnit and lightweight RulesHarness |
| Asset/editor tests | Assets/_Project/Tests/Editor | Wreckabulary.Editor.Tests |
| Authored worlds | Assets/_Project/Worlds | Preserve existing authored scenes and prefabs |
| Source art | ArtSource | Add WordboundPvP collection independently |
| Existing browser game | Web | Preserve website and score API |
| New coordinator | OnlineServices | Separate deployed service; final public activation pending |
| Verification tools | Tools/kit | Gate commands; generated projects/logs ignored |

Scenes: Hub, LivingRoom, Tutorial, MovingDay, WordboundLobby and WordboundGameplay, in saved Build Settings order. All scene registration and shared catalogs belong to one integrator.

Recovery: portable harness run run-7971495a-26f3-4750-8c9c-dad132175de8; baseline and pending actions in wordbound/STATUS.md.
