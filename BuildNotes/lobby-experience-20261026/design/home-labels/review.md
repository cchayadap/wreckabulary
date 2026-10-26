# Independent Home label migration review

STATUS: SOURCE REVIEW COMPLETE — revision 2 cleared for root-owned native verification. Reviewer `/root/asset_intake`; no live source, saved prefab, native process or player preference writes.

Reviewed patch `7f316e6d72a46d025e94a02e740d37b318a2193a8520a86109e9633fe00b05ae`; helper `ce206ce59531e9686f1220c36818b585bc5b221f21c604f0e50bc49b0a90ba6d`. All three candidate hashes, preserved builder baseline and patch match manifest. Independently parsed all three files with C#9 / UNITY_EDITOR / UNITY_INCLUDE_TESTS / GSK_QA: zero syntax errors. Exact strict git apply check passed. This is not semantic/native test evidence.

The migration accepts only the exact three-label old tuple or complete reviewed tuple. Customized and partial tuples fail before save. Persistent component IDs must be distinct and owned by the referenced project prefab; the masked-text comparison excludes only each exact component's m_text line. Unrelated serialized properties, prefab GUID/meta, dependency files and scene identity/dirty/root state are checked. Existing complete labels are idempotent without a save. The production Run creates unique evidence and saves original prefab bytes before its mutation; saved layout remains editable.

The owned temporary-copy tests exercise persistence with intentionally nondefault layout/font/color/other text, original-prefab preservation, repeat no-save behavior and two refusal cases. They do not edit the production profile or prefab. Current default-builder delta changes only the three labels.

One initial cleanup finding was resolved in revision 2: UnloadPrefabContents failure is now guarded, preservation checks continue, and an already caught original exception is not replaced by a cleanup-only exception. Source inspection found no remaining blocking issue for this bounded native migration.

Native still must prove Unity save/reload and exact non-text-byte proof, temporary-copy tests and production repeat no-save receipt. A save that changes unexpected serialized fields is reported failed with original-byte evidence; the helper does not silently normalize or overwrite customized text to force acceptance. UI fitting and the truthful local/online routes remain separate root visual/gameplay gates.
