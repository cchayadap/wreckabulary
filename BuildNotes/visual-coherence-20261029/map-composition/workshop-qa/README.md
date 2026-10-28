# Parcel Service Workshop Windows scenario

STATUS: FROZEN DRAFT. Three-assembly source compile: zero errors. Root independent review, Unity import and Windows execution remain pending.

Manifest SHA256: `0b3cf31b599d12a75b520202b7788e2476c3fe948ed89522232143349ab1b040`.
New source SHA256: `c9c77ec9f1b063207d8aa3796f75d66f46930e84b8346c88cf9f5a6d8e07c510`.

One new target: `Assets/_Project/Scripts/WordboundQa/WordboundParcelWorkshopQa.cs`. It registers `wordbound_parcel_workshop`. No old QA file, Tools/kit, metadata or production asset was edited. The direct Authoring assembly reference was already integrated by root with the separate Solo QA extension; this file needs that reference.

## Four steps

1. `parcel_workshop_sources`: snapshot saved sources/profiles/text, preferences and lobby bindings; enter the existing Wordbound launcher, set its public QA storage hook to a unique temporary folder, open the real Workshop, edit and autosave the Assigned draft, then explicitly open Parcel Service as its separate source.
2. `parcel_workshop_save_and_focus`: make a real history transaction for title/seed/selected Sorting room, resolve the six-site/21-prop course, wait for exact scoped primary autosave, recover and compare its complete draft, capture the ordinary Workshop, then use its public viewport Frame/Zoom actions to capture original TABLE/SHELF/CRATE meshes closer.
3. `parcel_workshop_exact_playtest`: invoke the real RequestPlaytest action and require one actual Single runner. Verify the exact edited manifest, saved item/rule binding and gameplay rules, course camera/visual profiles, fresh camera render events, canonical ticks and matching guide markers. Capture gameplay and its equipment/menu view; preserve the saved draft and history.
4. `parcel_workshop_return_and_history`: call the actual return handler, require destroyed playtest client/world/camera and disposed owned controls, capture the restored editor, switch to Assigned and back, verify both retained histories and exact Undo/Redo, then return to the original lobby. The Workshop preview camera and launcher must also be destroyed.

The launcher route matches the existing Workshop QA: OpenWordbound → configure isolated storage → existing OpenWorkshop handler. It initializes the party view but starts no online match or matchmaking request. This route creates its own automatic playtest binding, while keeping lobby bindings unchanged. It does not claim testing the separate local Create entry shortcut.

## Captures and saved evidence

Expect four passing steps and exactly nine unchanged full-window PNGs:

- `parcel_workshop_assigned.png`
- `parcel_workshop_saved.png`
- `parcel_workshop_sorting_close.png`
- `parcel_workshop_playtest_world.png`
- `parcel_workshop_playtest_menu.png`
- `parcel_workshop_returned.png`
- `parcel_workshop_assigned_returned.png`
- `parcel_workshop_parcel_returned.png`
- `parcel_workshop_lobby.png`

The run also retains exact JSON copies of Assigned, Parcel and returned Parcel autosaves, with paths and recovery hashes. Temporary storage is intentionally retained as evidence; it is never an owner save path and is not recursively deleted. No capture crops, overlays, alternate framebuffers or edited screenshots are used.

Each step has a 120-second owned work budget inside a 180-second outer limit. A failure disposes nested iterators/render subscriptions, requests actual local shutdown with a 25-second bound, returns through the existing launcher when shutdown is confirmed, and restores captured selection/preferences. An outer interruption records that stop was requested without claiming confirmation. Source mutations are reported as failures, never repaired silently to manufacture a pass.

## Limits

This is only a compiled draft until a new Windows run passes. Root must inspect actual camera composition, prop detail, selected room framing, text clipping and all nine images; nonuniform pixels and mesh identity do not prove aesthetic acceptance. The test invokes existing game/draft actions directly rather than physical pointer or controller events. It does not complete the puzzle route, prove online play or alter the original scenario's strict 45-image audit. Independent review was requested, but the menu reviewer is occupied with the owner's higher-priority material review, so root review is still required.
