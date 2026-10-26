# Character feel and quieter gameplay

STATUS: IN PROGRESS. Owner feedback takes priority over the earlier visual review. Functional passes do not accept the poses or interface.

Baseline: Windows `20261026T125941Z`, source `78ac4514`. Actual action frames and receipts: `Docs/qa/runs/20261026T130132Z-wordbound_original_equipment`. Gameplay/menu captures: `Docs/qa/runs/20261026T130906Z-wordbound_workshop_flow`. Both are preserved before changes.

| Work | Observable acceptance | Evidence boundary |
| --- | --- | --- |
| Holding | Correct handle direction and continuous hand contact; readable silhouette; no head/body penetration through idle, windup, active, recovery, movement and release. Inspect BAT/HAMMER first, then each distinct handling family. | Coincident grip positions alone are insufficient: inspect socket rotation, geometry and actual images. |
| Facial response | Calm idle, focused exertion, hurt/downed and recovery are distinguishable, smoothly blended and correctly prioritized. Interrupted actions cannot leave a stuck expression. | The original rig has eye/blink and brow channels, not a full mouth rig. Preserve its mouthless style and state this limit. |
| Online presentation | Reactions follow confirmed actor state, tolerate missing/stale snapshots and reset across disconnect/epoch/respawn. No new gameplay authority or network schema for a cosmetic fix. | One local capture is not separate-client multiplayer coverage. |
| Gameplay UI | Essential time/progress/health remain readable; at most one primary contextual interaction; full control help and narrative detail are optional. Remove the broad empty letter footer and redundant objective prose. | Do not hide actionable failures or replace all labels with unexplained symbols. Test gamepad and keyboard, small window and large text. |
| Developer editing | Named saved profile settings, clear units and bounds; tweak, save, reopen and preview in Unity without source edits. | Runtime fallback values alone do not satisfy saved authoring. |
| Delivery | Required source/native tests plus a fresh Windows build and matching before/after captures. | Human feel/style acceptance and full production delivery remain open. |

One writer per draft: `holding/`, `emotion/`, `ui/`. Root integrates, reviews shared paths and runs native jobs serially. Original models, rig, animation assets, combat timing and other branches remain intact. The separate FAN V3 build-02 failed its LOD-bounds check; it is preserved experimental art, not part of this correction.
