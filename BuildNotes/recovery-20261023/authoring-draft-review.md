# Inspector draft recovery review

STATUS: SOURCE REVIEW COMPLETE WITH FOLLOW-UP FINDINGS — NATIVE PENDING

Scope: integration WordboundCourseDraftStore, WordboundCourseAssetEditor, authoring Editor assembly reference and four new persistence cases. No files in the root implementation were edited by this reviewer. No compiler/native tests ran here. Reviewed 2026-10-23.

- [x] The recovery codec is editor-only and uses the already-installed Newtonsoft library. Type metadata cannot instantiate arbitrary types; point/box converters retain incomplete or invalid form values. Load is bounded to 4 MiB and depth 32. Apply still goes through the normal validated course import/export path.
- [x] Recovery files are keyed to a saved asset GUID in Library, outside saved course JSON. Writes use a same-directory temporary file followed by replacement. Asset-source changes trigger a separate explicit recovery choice.
- [x] Four source cases exercise invalid form close/reopen and rejected Apply, changed saved source, damaged recovery-file preservation and locked-file fallback. The conflict case now invokes the actual RestorePending handler. These are not executed evidence.
- [x] Initial confirmed data-loss defect repaired at source level: failed storage writes followed by source-change/Undo reload previously replaced the newest in-memory form. Store.Save now retains bounded serialized JSON before disk persistence; Store.Load prefers that fallback. ReloadPreservingPending captures the current dirty form directly as a conflict, and same-source Undo preserves dirty forms. Apply refuses to proceed when a recovery backup cannot be written. The locked-file regression covers newest edits through saved-source change, actual restore choice, inspector recreation and later successful persistence.
- [~] Follow-up finding sent to root: SceneGUI currently lacks the same recovery-choice guard as OnInspectorGUI. Scene handles can mutate the displayed saved draft while conflictingDraft exists; backup persists the conflicting draft instead, and RestorePending can replace the new handle edits. Gate SceneGUI while conflictingDraft is present or recoveryFailed is true.
- [~] Follow-up usability limit sent to root: after inspector recreation from memory-only fallback, Reload clears the previous error and sets retry time to infinity. It presents normal recovered status and waits for another edit or close before trying disk again. Expose memory-only status, keep the storage limitation visible and schedule a retry while the editor stays open.
- [ ] Root: compile and run native editor cases; actual domain reload/quit capture remains separate acceptance.

No broader codec rewrite or additional package was recommended. The fallback is bounded per asset (4 MiB), not by aggregate dictionary entries. A static fallback covers inspector selection changes but cannot preserve data through a domain reload or editor/process exit when persistent storage remains unavailable. Normal disk persistence is required for that guarantee.

Reviewed SHA-256 pins (integration working files; subsequent edits require focused rereview):

| File | SHA-256 |
| --- | --- |
| WordboundCourseDraftStore.cs | ACD19C8D1D69FDF566FC152A249FBF7D48FC7040B2A64A5CB4DFAE897FCFC079 |
| WordboundCourseAssetEditor.cs | F10D9725FA6CA7E0B38C204E5FCCF4B286F5543805C97173A65D47E83ADF0B8D |
| WordboundAuthoringPersistenceTests.cs | 8609A867E1E402345F91DA62E887B925D861A75DC4747E645B9B9319FE8CB4B1 |
| Wreckabulary.Online.Authoring.Editor.asmdef | 8AC91F2ED6FBCDEBDAA70C4A8862796C14635B62A6BBEC3BAB858FC66F07D3AE |

Next action: root addresses the two bounded follow-ups, then compiles and runs native persistence cases. Actual domain reload and quit behavior require separate acceptance evidence. No test pass is inferred from source review.
