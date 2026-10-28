# Parcel Service material setup

STATUS: FROZEN DRAFT — one new Editor source file. No live Assets writes or native execution by this lane.

Depends on the material-authoring runtime draft: optional `WordboundVisualProfile.RoomStyle` and `List<WordboundMaterialRemap>` with `VisualKey`, `Source`, `Replacement`. Compile receipt includes the exact dependency-candidate hashes; those files belong to the map lane.

## Intended result

`Tools > Wordbound > Courses > Create or verify Parcel Service finishes` creates five saved assets under `Assets/_Project/Art/Wordbound/ParcelService`: three materials and room/visual profiles. It then changes only `ParcelServiceV1.VisualProfile` from the expected original authoring profile to the new profile.

Ink-blue plaster, warm chalk ceiling, walnut floor and honey-oak doorway woodwork provide room identity. DESK original wood becomes parcel red with oak light wood; SHELF original wood/light wood becomes oak; CHEST original wood becomes walnut with oak light wood. Gold/metal, dark shelf wood, BOX/CRATE and crafted TABLE/STOOL/CLOCK remain unchanged. This does not claim particular slots correspond to only fronts or tops; actual renderer roles are recorded before the first write.

All three new materials clone saved tactile seeds, retaining base grain, detail albedo/normal, gloss maps, keywords and UV settings. No shared material or mesh is modified. `Inspect Parcel Service material roles` is read-only and prints the exact imported renderer/slot/material references.

## Preservation and recovery

- Edit Mode, clean saved course/source/material/model dependencies, original-source assignment, valid profiles, matching loaded original material references and vacant outputs are required before writing.
- Exact course and metadata bytes are copied under `before-assets` and checksummed before new assets or the course assignment. Source dependencies and metadata are hashed; shared sources and the existing course metadata must match after application.
- Existing course gameplay JSON, mode, seed, catalogue and references other than the visual profile are compared through a temporary serialized clone with only the visual reference cleared. No gameplay fields are changed.
- Complete repeats only validate and select. They do not reapply colours or save assets, so valid saved colour/remap/course edits are retained. Dirty assets are rejected without saving them. A changed course/room-profile assignment is reported and preserved.
- A partial setup is refused, with its exact backup and newly created assets retained for inspection. There is no automatic rollback/delete and no implicit retry that replaces edited output. If failure occurs after assignment but before COMPLETE, inspect the backup and hashes before recovery.
- The initial source-profile clone preserves poses and unrelated visual overrides. The first setup refuses custom mappings/overrides on the affected original fixture keys or a custom Packing material assignment, avoiding hidden replacement of owner artwork.

## Validation performed

Roslyn semantic compile against the current Unity response files, with this candidate and the material-authoring dependency candidates: five assemblies, zero errors (main, presentation, authoring, client, authoring Editor). No Unity/native tests, asset creation or render comparison were run by this lane. Source review requested from the Play/settings lane; its final report is separate.

## Root native acceptance

1. Inspect the original role roster, then run CreateOrVerify once. Check exact source preservation and five saved outputs/course reference; keep the native log and before-assets receipts.
2. Hash all five outputs and course/meta, run CreateOrVerify again, and confirm no bytes change. For edited-repeat proof, save a temporary valid colour edit through Unity, run again and confirm it remains; restore that deliberate test colour through Unity before final capture. Dirty/partial-state rejection is source-reviewed, not yet exercised here.
3. Capture the same ParcelService preview camera. Check red/oak/walnut differentiation, preserved hardware/wood grain, room brightness and door contrast. This is an unrendered candidate; adjust only the saved course-local assets if needed.
4. Check actual Windows player-height room views and Workshop original props/floor palette using the shared profile. Update preview inputs and dependency hashes when importing the new thumbnail. No global map/style acceptance is implied.
