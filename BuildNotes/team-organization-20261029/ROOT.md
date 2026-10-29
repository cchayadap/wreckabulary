# Team integration and Unity scene organization

STATUS: COMPLETE FOR THE BOUNDED LOCAL ORGANIZATION REQUEST

Owner request: check GitHub commits, organize human task ownership, reduce merge conflicts and save separate connected Unity entries for easy editing/testing. This bounded request follows the earlier pause; unrelated art/audio/networking feature work remains paused.

Starting HEAD036f352b, branch sethypagna/wordbound-integration. Four pre-existing modified documents and six new uncommitted Kitchen source/data files are preserved. Recovery snapshot `.harness/recovery/developer-workspace-2026-10-29T01-12-08-898Z` contains3453 verified files plus tracked diff. No resets, cleanup, rebases or changes to other checkouts.

- Root: current GitHub/ref evidence, integration, native gates, exact-path commits and final guide.
- ownership delegate: read-only human roles, branch ancestry and patch overlap; reports only.
- scenes delegate: additive scene-entry/setup drafts and preservation checks; root alone integrates/launches Unity.

## Confirmed starting observations

GitHub connector lists upstream main4d5c162, james-v10140d96 and ICYfb94c9a; live Git refs independently confirm them. Fork main isf7c3c903; no sethypagna branches are published. Upstream PR14 is reported mergeable; PR11 is reported conflicting. No open fork PRs. Commit status API returns no statuses for James/ICY; this is not CI success. Full structured API evidence will accompany the final status report.

Local integration already contains upstream James/ICY and fork main ancestry. It is950 commits ahead of fork main. Old assistant feature tips contain patch-equivalent work; blanket merging them is unsafe. Actual human responsibilities remain ICY, James/SethyPagna and the unnamed existing UI owner; assistant lanes are not extra team members.

Scene gap: the workspace normally redirects Play to Hub. Opening an authoring scene alone does not guarantee testing it. Reuse existing explicit Play-opened-scene routing and separate page prefabs; add course entry scenes/settings rather than duplicating the full Hub.

## Acceptance and next action

- [x] Exact GitHub/local status and human owner/file boundaries recorded in [TEAM-WORKFLOW](../../docs/wordbound/TEAM-WORKFLOW.md). Open issue queries returned no assignments in either repository. Before/after remote heads are identical.
- [x] Separate Flagship/Freestyle/Parcel scenes and copied LaunchAssets saved by Unity013411; Kitchen Course/Presentation/LaunchAssets/scene saved013454. Their repeated verification013627/013747 passes. No release Build Settings change.
- [x] Four explicit open-for-Play actions and the saved scene → local launcher settings → assigned course → Workshop/playtest connection documented in [SCENES-AND-TESTING](../../docs/wordbound/SCENES-AND-TESTING.md). UI retains separate page prefabs and existing direct Play previews. No main bootstrap/launcher/assembly modification.
- [x] Native setup/preservation013053 executes7/7 passing cases; its2 skipped entry cases were subsequently run as013323 and pass2/2. Batch fixture guard now matches existing workspace tests; interactive unsaved-work protection and actual assertions remain. Source and scene state/bytes/GUID/manual edits are checked.
- [x] [Original preservation](original-assets-preserved.json):4306 existing authored files unchanged. [Repeat preservation](saved-entries-repeat-preserved.json):4326 files including new outputs unchanged after actual repeats. [Smart Merge](smart-merge.json) combines independent changes and rejects a real conflict in copied scene fixtures.
- [x] Coherent local commits:30fa571a course content/definitions/Rules checks; dc6974a3 saved scene/setup/menu integration; f031098c opt-in physics fixture preservation. Production Assets/Packages/ProjectSettings tree is clean. Documentation/evidence is a separate commit. Untracked historical drafts are retained, not blanket-staged.

## Exact checks and corrections

[Verified local state](verified-local-state.json) binds11 gate receipts and51 source/asset hashes to source headf031098c. R0013221PASS; Kitchen core012505PASS33/33,0skipped. The source tree compiled natively through the Editor gates, including the opt-in fixture; its3 native physics cases have not run.

First attempts are retained: core012243 failed13 cases because the new50mm grid violated the existing100mm minimum;012340 failed7 because the original-ID-only route metrics utility was called on the deliberately new course ID. The new factory now uses the existing validated generic resolver; original route/document/placement/budget checks still pass. Native012756 failed6 setup cases because restoration redundantly called SetActiveScene on the already-active editor scene. Setup now calls it only when the active scene changed, keeping final scene-state/source checks intact. Corrected013053 passes7 cases;013323 passes both explicit entry cases. No shared validation or test assertion was removed to obtain these passes.

## Limits and next action

The bounded saved-asset/reference/navigation work is complete. Human Save/Cancel dialogs, actual keyboard/controller Play/Workshop/playtest/return acceptance for each saved entry, Kitchen furniture physics, close-map artwork, complete route traversal, online/public compatibility and final release remain pending. This work makes those entries available to try; it does not certify all gameplay outcomes. Current tested Windows build remains001001.

The full feature goal remains paused. On explicit full-development resume, start with the committed opt-in Kitchen physics fixture and matched room views, then the remaining production checklist including BELT/material impact audio. For team review, use current integration slices and the handoff template; do not remerge old branch tips or alter PR11/14 to imply they contain the local expansion.

No upstream merge, push, PR modification or external release is performed by this organizational audit.
