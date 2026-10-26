# Independent local creator entry review

STATUS: COMPLETE — no blocking source finding in frozen revision 2; root-owned native tests and rendered acceptance remain required.

Reviewed full replacement patch `revision2/local-workshop-entry.patch`, SHA256 `65e009d73a5c88455c8c6b743611c0a608c9c07583df8a6199006d8cd2014a92`. All nine candidate and seven baseline hashes independently match. Strict application against the owned baseline passed with `--whitespace=error-all`. All runtime/UI sources are byte-identical to revision 1; only the native fixture changed. No live source or native job was run by this reviewer.

## Route and ownership

- Workshop and solo requests cancel each other's pending/return intent. Each launch request is consumed once; ordinary Online cancels both, and Session.Clear resets both. Return-to-creator is separately consumed by the Hub to open Play/Create/Wordbound maps.
- Original house Tutorial/Workshop mode and selected map remain separate. The creator uses the existing seat selection and retains both local bindings; it borrows the primary binding for a one-player playtest without relabeling the authored mode or removing the other couch seat.
- The local editor branch runs before PublicTrust, SnapshotChunk, body-prefab and network-scene validation. It opens the existing assigned course/recovery route, does not create PartyMenu, and does not register private-host callbacks. Online Workshop retains its original callbacks and Back to party behavior. Local playtesting still validates the actual content/prefabs/scene before launch.
- RequestClose extracts the existing cancel-gesture/save/current-revision/close sequence. It refuses unresolved recovery and online preparation. The existing recovery Back branch closes without saving, so it does not overwrite a pending recovered document. Local Close marks the creator return and transitions only when no active match client exists.
- Existing same-scene playtest return resumes the existing Workshop and History; no replacement of that object or serialization of Undo is added. Closing/reopening relies on the existing saved snapshot/recovery prompt. Cross-scene Undo remains intentionally unsupported and must not be advertised.

## Meaningful test expectations and corrected review findings

The two added lobby cases check mutually exclusive one-shot intent, Session.Clear and real Create-family controls while preserving the original home mode/map and both seats. The three native cases cover an actual assigned-course edit/playtest/return/save, recovery cancellation with unchanged file bytes, and invalid assigned content with sanitized failure and no party/client. They use a unique owned temporary recovery folder through the existing safe storage seam; owner storage is not deleted. Exact source/profile JSON, dirty-state, dependency/meta and preference preservation is checked.

Root correctly identified fixture reuse risk in revision 1: authoredMemory/authoredDirty and recoveredJson could survive into a later case. Revision 2 explicitly clears all per-case dictionaries, scalars and handles in SetUp, after rejecting unfinished prior owned lifecycle. It does not rely on EnterPlayMode rebuilding the fixture. The producer also identified that real UI Submit needs stable Hub readiness; revision 2 requires GameHud.Active and a nontransitioning StateController before submitting. Both fixes are present, while the successful assertions and runtime behavior remain unchanged.

The producer's final in-memory semantic receipt reports zero errors for Main171/Client23/Tests68/AuthoringGameplay2 sources. Its initial Assert alias ambiguity is preserved as source-check-initial.json. This reviewer inspected that receipt but did not execute the compiler or Unity.

## Native boundary

Run `Wreckabulary.OfflineGameplayTests.WordboundNativeWorkshopEntryTests` with the existing local-workshop environment gate. Require all three cases and teardown preservation to pass, including actual destroyed-client/world/camera completion and the same History/Undo/Redo checks. Then inspect the actual Create page and local editor in the Windows player. No public/private online compatibility, physical keyboard/controller acceptance, full map completion, final visual approval or new persisted Undo claim follows from this source review.