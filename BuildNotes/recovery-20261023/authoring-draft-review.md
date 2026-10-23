# Inspector draft recovery review

STATUS: BOUNDED SOURCE FINDINGS RESOLVED — NATIVE PENDING

Scope: integration WordboundCourseDraftStore, WordboundCourseAssetEditor, authoring Editor assembly reference and four new persistence cases. No files in the root implementation were edited by this reviewer. No compiler/native tests ran here. Reviewed 2026-10-23.

- [x] The recovery codec is editor-only and uses the already-installed Newtonsoft library. Type metadata cannot instantiate arbitrary types; point/box converters retain incomplete or invalid form values. Load is bounded to 4 MiB and depth 32. Apply still goes through the normal validated course import/export path.
- [x] Recovery files are keyed to a saved asset GUID in Library, outside saved course JSON. Writes use a same-directory temporary file followed by replacement. Asset-source changes trigger a separate explicit recovery choice.
- [x] Four source cases exercise invalid form close/reopen and rejected Apply, changed saved source, damaged recovery-file preservation and locked-file fallback. The conflict case now invokes the actual RestorePending handler. These are not executed evidence.
- [x] Initial confirmed data-loss defect repaired at source level: failed storage writes followed by source-change/Undo reload previously replaced the newest in-memory form. Store.Save now retains bounded serialized JSON before disk persistence; Store.Load prefers that fallback. ReloadPreservingPending captures the current dirty form directly as a conflict, and same-source Undo preserves dirty forms. Apply refuses to proceed when a recovery backup cannot be written. The locked-file regression covers newest edits through saved-source change, actual restore choice, inspector recreation and later successful persistence.
- [x] Follow-up scene-edit finding repaired: SceneGUI now returns before catalogue/handle work when conflictingDraft is present or recoveryFailed is true, matching the Inspector recovery-choice policy. The conflict case invokes the actual SceneGUI method before resolving the choice. This source check does not substitute for native Scene view interaction.
- [x] Follow-up memory-only recovery finding repaired: Store.IsMemoryOnly exposes whether the fallback still needs disk persistence. Reload surfaces a specific warning before the recovery choice and schedules a retry in five seconds. Successful persistence clears that warning. The locked-file case asserts warning, finite retry schedule and later warning clearance. Persistent failure across script reload/Unity exit remains an explicit limitation in the displayed warning.
- [ ] Root: compile and run native editor cases; actual domain reload/quit capture remains separate acceptance.

No broader codec rewrite or additional package was recommended. The fallback is bounded per asset (4 MiB), not by aggregate dictionary entries. A static fallback covers inspector selection changes but cannot preserve data through a domain reload or editor/process exit when persistent storage remains unavailable. Normal disk persistence is required for that guarantee.

Reviewed SHA-256 pins (integration working files; subsequent edits require focused rereview):

| File | SHA-256 |
| --- | --- |
| WordboundCourseDraftStore.cs | 7504BBDD3CE31C9C0A9861E2C20095DB98A326CD70098B7E5A8C65E3B1AAEAF6 |
| WordboundCourseAssetEditor.cs | B0C0ED378FC5A1F294E8C60A3B93C1A4D205B053A5470839D13B1B3E0E040B73 |
| WordboundAuthoringPersistenceTests.cs | F64D7C337A41EA493787A225E1177F2A314D0DEE3BBB2F81CB1D53FC599EE64C |
| Wreckabulary.Online.Authoring.Editor.asmdef | 8AC91F2ED6FBCDEBDAA70C4A8862796C14635B62A6BBEC3BAB858FC66F07D3AE |

Nonblocking UI observation sent to root: automatic retry success clears recoveryWarning but the previous storage message in the shared error field remains until another successful Run or Reload. It does not affect persistence, but can leave an outdated red error visible.

Next action: root compiles and runs native persistence cases. Actual domain reload and quit behavior require separate acceptance evidence. No test pass is inferred from source review. No root implementation files were edited during this follow-up.
