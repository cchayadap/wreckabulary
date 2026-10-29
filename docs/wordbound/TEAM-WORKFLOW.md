# Team ownership and safe integration

Updated 29 October 2026. James and SethyPagna are the same person. This guide organizes the existing responsibilities; it does not assign new people or move another person's work.

## GitHub position

At the 29 October audit, local integration `036f352b` contains cchayadap/main `4d5c162`, james-v1 `0140d96`, ICY `fb94c9a` and SethyPagna/main `f7c3c903` as ancestors. Integration is 950 commits ahead of fork main at that pinned checkpoint. Current Wordbound work is local; no `sethypagna/*` branch is published on the fork.

Upstream [PR14, James v1](https://github.com/cchayadap/wreckabulary/pull/14) remains open and GitHub reported it mergeable. [PR11, James](https://github.com/cchayadap/wreckabulary/pull/11) remains open and GitHub reported conflicts. Neither contains the current Wordbound integration. Commit-status API returned no statuses for James/ICY; that is not a green CI result. No PR was changed or merged by this audit.

Exact refs, local ancestry and patch-equivalence evidence: [ownership review](../../BuildNotes/team-organization-20261029/ownership/REVIEW.md), [GitHub API receipt](../../BuildNotes/team-organization-20261029/github-api.json). Fetch again before a future merge; the table is a dated checkpoint.

## People and file ownership

| Direction | Existing owner | Prefer separate files/assets | Integrator handles shared changes |
| --- | --- | --- | --- |
| Lobby / character / UI | Existing UI person, name still unspecified | Page-specific components and `Assets/_Project/Prefabs/UI/{Home,Play,Loadout,Shop,Settings}`; local layout/profile variants | Lobby navigation, shared fonts/theme, player/wardrobe hooks and runtime registration |
| Character colours and Escape | ICY | Escape-specific helpers, content, tests and development scene | Shared input, inventory, rules, appearance and existing scene integration |
| PvP items / furniture / weapons | James | One folder per item/collection; source meshes, materials, icons, animation and audio profiles; EquipmentStudio | Global item/model/material catalogues, combat authority and public content admission |
| Puzzle direction and Wordbound | James | New mode/content files, course-local data and dedicated course scenes | Mode/protocol/schema contracts, shared launcher and approved public courses |
| Workshop / map generator | Work package under James; no extra human assigned | Course-specific content, authoring tools and tests | Shared document/Undo/import contracts and generic selection/source binding |
| Online services | Work package under James; no extra human assigned | Service/client/transport files and targeted tests | Shared match state, player lifecycle, SDK/packages and deployment |

Assistant lane names such as map-core/online are not additional teammates. Git authorship alone does not identify the currently unnamed UI owner.

One integrator owns `ProjectSettings`, package/assembly contracts, global Resources/catalogues, shared player/input/lobby hooks and the six existing runtime scenes. Feature authors hand over the smallest necessary registration change instead of all editing those files simultaneously. Changes to an original imported model/material should use an owned variant where the design must stay isolated.

## Branches and merge sequence

1. Start a new coherent feature slice from the reviewed current integration commit. Record its exact base SHA, owner and owned paths. Reuse a suitable current worktree; keep unrelated dirty work intact.
2. Implement new components, item/course folders, prefabs and authoring scenes. Preserve existing Unity GUIDs. Save/move assets through Unity and include each asset's `.meta` and required folder metadata.
3. Make small commits for content/runtime, authoring/presentation and shared integration where that separation is meaningful. Include relevant tests/evidence with the changed behavior. Stage exact paths; do not blanket-add local drafts, builds, logs, credentials or third-party SDKs.
4. Hand off the [feature template](templates/FEATURE-HANDOFF.md). The integrator reviews shared changes once, then checks creation/reopen/Play/return and affected original modes.
5. Review historical branches by exact commit/path slices and `git cherry`, not a blanket merge. Many old branch-only commits are already patch-equivalent. A positive `git cherry` entry can also be obsolete or intentionally excluded.
6. Publish a source-matched review branch only after reviewing its intended code/assets and LFS objects. Publishing, PR review and a production release are distinct states. The current local expansion has not been pushed by this organizational work.

The folder `wordbound-map-workshop` currently checks out `sethypagna/hud-readability`; record the actual branch and SHA in handoffs. Do not rename/switch someone else's worktree to make the label match. Original font/Lunar work and the online checkout's dirty ModeDefinition remain owned work.

## Unity collaboration

Use [separate scene entry points](SCENES-AND-TESTING.md), per-page prefabs and course-local launch settings. The common gameplay scene loads the explicitly chosen course. Workshop saves are separate from Inspector-authored course assets; use their explicit save/import workflow.

Force Text serialization is already enabled. The repository's `unityyamlmerge` attributes now have a verified local Git driver on this PC. It combined independent adjacent camera edits in copied scene fixtures and rejected a real conflict; original scenes were unchanged. The driver is repository-local and shared by its worktrees, not a global setting. Other PCs need their own installed UnityYAMLMerge path. [Unity's setup and command reference](https://docs.unity.com/en-us/engine/6000.6/manual/get-started/project-configuration/version-control/smart-merge) explains the tool. It assists structural merges; saved reference and gameplay checks remain necessary.

Never resolve a whole scene/catalogue by taking one side without reviewing the discarded changes. Stop and coordinate when two people need the same shared file; finish one small registration change before applying the other.
