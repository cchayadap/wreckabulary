# Edit the new Parcel Service map in Unity

Use the integration checkout with Unity6000.6.4f1. Existing authoring scenes are in Assets/_Project/Scenes/Authoring; Hub.unity remains the main game lobby.

| Change | Unity asset or action |
|---|---|
| Rooms, original furniture, bounded parcel randomness, puzzle positions | Tools → Wordbound → Courses → Create or select Parcel Service; edit the selected Parcel Service V1 asset |
| Play the saved map | Course Inspector → Validate and preview exact seed → Play this exact preview locally |
| Drag objects and try a draft | Game → Play → Create → Wordbound maps; select Parcel Service, select a room/object, use frame-selection icon, edit and Playtest |
| Room colours and woodwork | Assets/_Project/Art/Wordbound/ParcelService/Profiles/ParcelServiceRooms.asset |
| Per-item material assignments | Same Profiles folder → ParcelServiceVisuals.asset |
| Red paint, oak, walnut | Assets/_Project/Art/Wordbound/ParcelService/Materials |
| Map-card camera | Assets/_Project/Data/Wordbound/Authoring/Previews/SoloMapPreviews.asset → Frame Parcel Service; orbit/zoom and Save camera |
| Gameplay camera | Parcel Service V1 → Camera Profile; this is separate from the card image |

Save outside Play Mode. The course and visuals are separate saved assets; the editor preview and runtime are generated from them. Do not expect edits to a temporary preview hierarchy to rewrite the course. The new map is a local course; public maps and other creators’ drafts are unchanged. Workshop draft edits/autosaves are separate from the saved source asset and keep separate Undo histories.

Visual quality, complete public multiplayer, richer landmarks and the larger production backlog remain open. Current materials and map are editable work in progress.
