# Camera and Loadout authoring integration review

STATUS: SOURCE REVIEW COMPLETE — NO NEW ACTIONABLE FINDINGS

Scope: independent read-only source review of applied Camera and Loadout integration in wordbound-integration, including current callers and authoring hooks as available. Review persistence, per-Play behavior, destructive dynamic slots, binding/focus and compatibility with preserved procedural Loadout controls. Root owns Workspace wiring, native tests and all Assets changes. This lane writes this report only.

No new actionable source finding remains in the reviewed integration. The earlier null-map fixture problem and destructive Loadout slot-binding gaps are corrected in the applied sources.

The live setup orders typography, Shop and Loadout correctly, creates the camera asset through its non-overwriting helper, and rejects a null camera setup result. Workspace Design links target the exact profile/prefab outputs and label the remaining procedural controls honestly. Loadout refresh, outfit-preview restoration, ownership/equip callbacks, pinned recipe focus and the separate Finish action retain the existing controller paths. Only the newly instantiated view objects and bounded dynamic slots are changed during binding.

Camera settings are copied once per stage and are not written back to the asset; missing/invalid input retains the original four constants. The normal Hub creates its stage in LobbyMenu.Start and destroys it with that menu. No saved LobbyStage component reference was found in the inspected _Project scene/prefab trees. A reused manually authored stage deliberately keeps its startup snapshot; full editor Stop/edit/Play behavior with both reload options disabled remains a native validation item, not a source-test claim.

Validation performed here: current source reads, live/candidate text comparison, scoped diff whitespace check, and exact hashes below. No compilation, Unity execution, prefab setup, native tests, image inspection or visual acceptance was performed. Root owns the eight Camera EditMode/three PlayMode cases and eight Loadout EditMode/two PlayMode cases, preservation checks, and actual preview workflow.

## Source snapshot

Captured 2026-10-26T08:49:28Z. All twelve camera/loadout files match the reviewed candidate text after CRLF normalization. New applied files use CRLF while their candidates use LF, so the raw byte hashes differ as marked below. The two existing-file patches match candidate bytes exactly. Workspace hooks and live callers are separate integration reads.

| Path | SHA-256 | Byte-identical to candidate |
| --- | --- | --- |
| Assets/_Project/Scripts/UI/Lobby/LobbyCameraProfile.cs | 3b75dd9b2268b54c112b3437bb18f6cd8d94bd9058eb36fcb3a5ebcd21ac82a7 | False |
| Assets/_Project/Scripts/UI/Lobby/LobbyStage.cs | eba8c79c64c928839bbb6075aef2aa435f6ee03fb83bd0299b7ee1122c0cffb4 | True |
| Assets/_Project/Editor/Authoring/LobbyCameraSetup.cs | 886d38569e6945d45d2aee8b6e70b915cc2b08bfd3284dcc2e1110a19c451854 | False |
| Assets/_Project/Tests/Editor/LobbyCameraProfileTests.cs | eadc09265222aaffbae6e5bb75c54c55a4f7041012fbb3cd2a6649fc99c64434 | False |
| Assets/_Project/Tests/LobbyCameraRuntimeTests.cs | 5426767f40e49d51abbdf47daff78201442f6d47b43fde162096d5f63a5ac564 | False |
| Assets/_Project/Scripts/UI/Lobby/LobbyLoadoutLayout.cs | bfeb9377bab3b26fcf110a4aed2c54a2a19816cd8f23344d300751b20a4bd7c7 | False |
| Assets/_Project/Scripts/UI/Lobby/LobbyLoadoutPageView.cs | a5e1fa541c9d91be3de2a2ab32f3c47f466581eff876bdc209031e0c28b985fe | False |
| Assets/_Project/Scripts/UI/Lobby/LobbyRecipeTileView.cs | 882ecf8615f0368ab635024ba8096df781d727ea70765af79461ff4ab585e454 | False |
| Assets/_Project/Scripts/UI/Lobby/LobbyPages.cs | 1578593df65293cd56b81cb45afa1d316db4852797a485a3ec1d618c20b39da6 | True |
| Assets/_Project/Editor/Authoring/LobbyLoadoutLayoutSetup.cs | cc94ac3cb8bcbfd3974bbdd0a16cd704aa8b1ce3f3c10ed326f7cdd3ddd59d8f | False |
| Assets/_Project/Tests/Editor/ShopLayout/LoadoutLayoutAuthoringTests.cs | 3fd0c9c619b932be49f1c82e99a38de3b7a1ca0b1715ef043e5470317959d454 | False |
| Assets/_Project/Tests/LoadoutLayoutRuntimeTests.cs | 8269d15850b72ad4f4a484e2c0a9347135b125c92d4103719564ea7ecb3bb984 | False |
| Assets/_Project/Editor/Authoring/WorkspaceStudioSetup.cs | c082059431c55592054128f0965dd3804420956bc4c908e6dc49fe429218f0d6 | Integration caller |
| Assets/_Project/Editor/Authoring/WorkspaceWindow.cs | e4919d552227c864451208e61783ce67623f7a6a0b542e6e1dace2622f210db6 | Integration caller |
| Assets/_Project/Editor/Authoring/WorkspacePlayPreview.cs | f75a578703dd9f9af38ed28102d130eecc582d241b8c297cc8a2acccaf7a2401 | Integration caller |
| Assets/_Project/Scripts/UI/Lobby/LobbyMenu.cs | c302216371fc4f16d1133649aa055c050cd086c4f4455ac06467e1487dbb94a8 | Integration caller |
