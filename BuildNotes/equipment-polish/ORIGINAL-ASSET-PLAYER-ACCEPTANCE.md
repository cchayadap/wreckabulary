# Original artwork: remaining player and editor acceptance

Status: SOURCE REVIEW COMPLETE; new Unity/Windows captures NOT RUN. Reviewed root `02738cd`, the [editing guide](../../docs/wordbound/UNITY-EDITING-GUIDE.md), [gallery](ASSET-REVIEW.html), current presentation/authoring/workshop source and QA capture routes. No asset, source, profile or pilot was changed.

## Established baseline

- [x] Original Wreckabulary is the assigned presentation default. `Presentation.asset` has no overrides; its default enum is Original Wreckabulary. LaunchAssets and FlagshipCourse reference that profile and `PlayerCamera.asset`.
- [x] All twelve original FBX files exist and their `.meta` GUIDs match the corresponding `Items/<ID>` entries in `Resources/ModelLibrary.asset`. Matching original GLBs and icons exist for all twelve. IDs: **BAT, BROOM, HAMMER, SPEAR, SHIELD, PLATE, TABLE, SOFA, BED, STOOL, FAN, CLOCK**.
- [x] All **127 unique relative gallery links** resolve in the assembled checkout. The page leads with actual originals, identifies its showcase/close-up as source renders, labels DetailV2 owner-rejected and keeps all pilots unapproved. Its 48/48 native presentation claim is scoped to the earlier `editmode_20261023T141231Z.xml` subset; it is not Windows or appearance acceptance.
- [ ] James has reviewed actual player images at game distance and close range. Technical bounds/grip tests do not close this item.

Use these actual files, not the optional collection or a candidate render:

| Purpose | Exact source/asset |
| --- | --- |
| Original models | `Assets/_Project/Art/Imported/Items/<ID>.fbx`; FBX model assets are the original model prefabs |
| Runtime selection | `Assets/_Project/Resources/ModelLibrary.asset`, keys `Items/<ID>` |
| Original reference images/models | `Assets/_Project/Resources/UI/Items/<ID>.png`; `Web/public/art/items/<ID>.glb`; `Web/public/art/supplied-showcase.png` |
| Existing avatar | `Assets/_Project/Art/Imported/Avatar/Avatar.fbx`, library key `Avatar/Avatar`; original gameplay prefab `Assets/_Project/Prefabs/Player.prefab` |
| Wordbound physics prefabs | `Assets/_Project/Prefabs/Wordbound/Network/WordboundPlayerBody.prefab` and `WordboundItemBody.prefab`; artwork is attached by presentation, not stored as replacement body geometry |
| Course and appearance | `Assets/_Project/Data/Wordbound/Authoring/FlagshipCourse.asset`, `Presentation.asset`, `PlayerCamera.asset`; launcher `Assets/_Project/Resources/Wordbound/LaunchAssets.asset` |

Keep clean rounded proportions, pale sage upholstery, light timber, powder-blue FAN, steel/pale-wood HAMMER and the original equipment identities. Do not treat muddy noise, extra trim or lower triangle counts as an improvement. SOFA V4 remains an optional experiment without convincing improvement; FAN has only partial construction-view review and HAMMER only build evidence. No pilot rollout is approved.

## No-code review route and required captures

1. **Profile proof.** In Edit Mode use **Tools → Wordbound → Create or verify Inspector authoring assets** if defaults are missing. Duplicate the course/profiles for review. Select the visual profile: **Base Artwork → Original Wreckabulary**, with **Overrides** empty for baseline captures. Record the assigned course/profile and seed. Recreate the playtest after changing artwork, model or grip assignments.
2. **Static layout.** Course Inspector: **Validate and preview exact seed** opens **Wordbound exact preview**. Left-drag orbits; wheel zooms. This shows static geometry/props only. Use **Play this exact preview locally** for the real camera, avatar and dynamic items. Capture the original furniture group from front/side/behind, then SOFA/FAN/HAMMER close-ups. Check grounded feet, silhouette, material separation, smooth curves and no detached detail. Keep lighting, scale and camera comparable; the HTML gallery is a reference browser, not a native gameplay gallery.
3. **Held and moving objects.** Capture all twelve supported items at their actual gameplay size; include a readable held view for hand-carried items, SHIELD/PLATE defense pose and furniture world/deployed views. Inspect grips, wrist/torso clipping, facing, grounding and material appearance during movement. Include FAN/CLOCK operation. Existing exact-mesh/grip tests do not establish animation or game-camera appearance.
4. **Camera.** Select the assigned `PlayerCamera.asset`. **View and follow** controls field of view, follow distance, look-at height, lift and angles; **Look controls** separates mouse/controller speed; **Walls and visibility** controls obstruction. Capture a clear room, wall/corner and floor transition. Valid changes update during play except **Starting angle**, which needs a profile change/recreated playtest. Confirm no clipping, hidden player or excessive wall-camera jumps at the intended Windows resolution.
5. **Workshop interaction.** **Wreckabulary → Open Latest Hub**, Play, **WORDBOUND → Workshop**. On a duplicate/isolated draft, mouse-select, drag, rotate and undo a module; frame with F/**Frame map**, right-drag orbit, middle-drag pan and wheel zoom. Set/draw a placement range, validate, playtest, return and recover/reload. Capture the selected module/range with legible panels and the returned draft. Try controller tools plus keyboard text entry; no stuck focus/held action or accidental edit is acceptable.
6. **Custom appearance edit.** On a copied render-only prefab/profile, assign one **Overrides** entry by canonical **Visual Key**, adjust `Grip_R` and a renderer material, restart and inspect. Leave override **Materials** empty to preserve different renderer materials. Reopen/reload the profile to prove saved edits remain. Keep original FBXs and ModelLibrary untouched; visual overrides do not change item dimensions, collider or statistics.

For each capture set retain build/commit, resolution/quality, course seed/manifest, profile/art choice and image paths. Keep at least one whole UI frame; a cropped beauty shot does not prove usable text/navigation. Complete player/window review at normal 1080p and the owner's intended display scale. Owner appearance judgement remains separate from screenshot existence.

## Current coverage and concrete rough edges

- [ ] The guide omits **Start from → Assigned course**, now present in `WordboundWorkshop.Palette`. It restores that course's in-memory draft and Undo history after visiting a template; template/source binding is distinct. Add this concise instruction before final delivery. The guide's warning that serialized recovery does not contain Undo remains correct.
- [ ] Visual overrides use a free-text canonical key in the standard Inspector, not an item picker. The author must know an ID; invalid/duplicate keys are rejected by validation. Confirm error messages remain visible and understandable in the actual preview/play route.
- [ ] Preview has orbit/zoom around the whole course, with no per-item selection/frame control. It cannot replace close-up Game/Scene inspection. Inspector module editing also uses **Scene module** plus **Frame module**, rather than direct module picking; the guide discloses this limitation.
- [ ] Existing instances do not rebuild live after Base Artwork/model/grip changes. Camera Starting angle is also deferred. Verify the documented restart action during the owner workflow so stale visuals are not mistaken for a failed edit.
- [ ] `wordbound_workshop_flow` captures `wordbound_party`, `wordbound_workshop`, `wordbound_local_world`, `wordbound_equipment_menu`, `wordbound_returned_workshop`. It invokes real launcher/workshop routes but edits through history/API; its pass would not prove manual dragging or controller usability.
- [ ] `wordbound_synthetic8_low` captures `synthetic_start`, `synthetic_end`, `synthetic_reference_pose`. This is a synthetic presentation workload, not eight online peers or complete equipment/art coverage. The older explicit `PresentationCaptureTests` concern original-game menu/furniture scenarios; they do not substitute for Wordbound player captures.

These are remaining review steps and documented UI limitations, not newly observed native failures. No broken original reference, gallery link or profile assignment was found in this bounded source audit. Root retains exclusive ownership of Unity, player/build tests and screenshots.
