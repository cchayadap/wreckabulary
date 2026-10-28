# Saved-prefab reference iterator review

STATUS: COMPLETE — NO BLOCKING SOURCE DEFECT; NATIVE RERUN/PIXEL EVIDENCE SEPARATE

Read-only review of root's live correction after native232056 stopped before mutation/backups on an internal object-reference m_FileID. Scope: reference identity remains compared semantically; only its raw descendant IDs may be skipped. Root owns rerun/native and all live changes.

Reviewed exact live `Assets/_Project/Editor/Authoring/LobbyShopCompactFooterSetup.cs` SHA-256 `37ca8189f4731f5d4fe172070d44e6127bac2e20d70578e778b6cd79498ee103` against the frozen candidate. The entire diff is the iterator descent state: both iterators start with descend=true, advance with the same value, and set the next descent flag false only for SerializedPropertyType.ObjectReference. Their final extra-property check uses the same flag.

The reference property itself still requires matching path/type and passes the unresolved-reference guard. Its target is still compared by local hierarchy/component position or persistent asset GUID/local ID. Thus a changed target is rejected; Unity's raw per-instance reference implementation fields such as m_FileID are not redundantly compared after semantic identity is established. Every referenced local component/GameObject remains independently present in the full object roster and has its own serialized authored fields compared.

Generic properties continue with descend=true, so arrays, nested authored structs and their children remain traversed. Array sizes and all nonreference leaf values still use SerializedProperty.DataEquals. The only intentional authored-value exception remains the root page's SelectionHeight. Hierarchy/component counts and identities, property roster, trailing-property guard, before/after persisted comparison, backups and cleanup are unchanged. This is not a broad property-ignore rule.

At this review read, page SHA remained the expected `62feffcfbd4c3c30d93c97fa47d0b0773047b9293cd29da8c439694d1a3aa8b6`, and native-migration did not yet exist. This corroborates the prior failure's pre-mutation boundary at that moment, not the outcome of root's concurrently starting rerun. No native process was launched or production file changed by this review.
