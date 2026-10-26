# Shop presentation refresh

STATUS: IN PROGRESS. Root-owned source work in the recovered, initially clean map-core worktree while the integration checkout runs the separate capacity test. No Unity, build, player or core job launched here.

Baseline: ShopPage, ShopCollectionsTests and LobbyTests exactly match integration `9fce9f25`; root checked their Git blobs before editing. LobbyPages differs and remains untouched in this first slice. Existing map-core commits remain intact; integrate only this scoped delta.

The first slice separates Looks, Gear, Colours, Rooms and Extras into five persistent browsing categories. Each view contains one shelf; deep links select the correct shelf and preserve the chosen recipe. Larger art regions replace repeated descriptions; optional pointer/controller hints retain detail. Purchase/equip states and earned-coin rules are unchanged. Category switches restore uncommitted previews and retain accessible focus. Original models and live preview APIs are reused.

Existing regression flows now explicitly navigate category buttons. Added meaningful checks for preview isolation, unchanged coins/ownership, exact shelf membership, invalid category handling, returning to the last category and recipe-specific deep-link focus. Updated the old deep-link test to require the correct category and fully visible target rather than a mandatory scroll offset that the new layout no longer requires.

Source and tests are not yet executed. Added a native Editor portrait renderer that uses the original avatar, original idle pose and each actual catalogue outfit. Shop cards load its generated portraits when available; no portraits have been rendered yet. It is additive, keeps originals untouched and runs only when root schedules the gate. New geometry skins, motion, loadout refinements and combined Windows capture remain subsequent work; this UI change alone does not fulfill the art request.

Capture callers explicitly choose the relevant category before revealing its controls. Static `git diff --check` passes. Root will integrate after the current native job, run R0 and affected native/UI checks, render the portraits and inspect actual output. No rendering success, visual approval or player acceptance is claimed from source alone.
