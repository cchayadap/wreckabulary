# Parcel Service — next original-art review

STATUS: COMPLETE — READ-ONLY PREVIEW/SOURCE AND EIGHTEEN WINDOWS IMAGES. Root owns physical/Windows verification. No Unity/native invocation or production asset/source edits in this review.

Review requested: fresh Parcel preview `20261028T205930Z-70e5e4cc`, saved fixture bounds and course gameplay-role clarity. Assess remaining plain/generic appearance and identify the smallest original-art improvement without fake usable props, invisible collision or changed gameplay resources. Await root's final eighteen Parcel Windows frames before full Windows visual assessment.

- [x] Open the actual unchanged fresh preview and record image hash.
- [x] Read native fixture inspection/setup receipt and saved placement bounds.
- [x] Check source-defined service/sorting/dispatch objects and the real puzzle/crafting route; live HUD clarity awaits Windows frames.
- [x] Record remaining defects and one bounded next art candidate below, separating observation from proposal.
- [x] Open all eighteen final Parcel Windows images from runs211338 /211423; exact roster below.

## Confirmed preview evidence

Directly opened the original960×540 `BuildNotes/visual-coherence-20261029/map-previews/captures/20261028T205930Z-70e5e4cc/ParcelService.png`; SHA256 `871e71d62b71f51c91aef98162c67fce322ca53e88413bef51e5da4917935886`. Receipt status is `CAPTURED_NOT_VISUALLY_APPROVED`, no errors, fixed seed `0123456789abcdef`, Freestyle,21 real prop visuals. Parcel manifest is `0f8944f4260468577ad2583c687f0e3a7a3a74902eb5ba54b81aff9c6aa1d86f`. This Editor image is not Windows/gameplay proof.

The original brass sconces and coral/ivory T sign now break up the upper Service wall. The red desk bodies, oak tops and brass pulls remain distinct; the chest is darker and the clear doorway gives a useful view into Sorting. No fake window pattern or decorative background stripes appear. The regular lines on the floor are the actual wood-plank material. The main aisle remains visually open.

The owner's plain/generic complaint is still valid. Large uninterrupted blue planes and the broad empty foreground dominate. Two desks, an empty shelf and a chest read as a sparse furniture room; the higher wall details are small accents, not a strong parcel-service composition. The Service T sign is partly cropped by this particular thumbnail, and the distant wall shelves appear as tiny bright lines. Repeating the same room finish/door frame does little to distinguish Service, Sorting and Dispatch. Original model detail is present in drawer pulls, shelf rails and crate braces, but the flat illumination and weak contact shading make it harder to read. No definite fixture clipping is visible in this single view; other angles and actual Windows still matter.

Do not treat the decorative original T as a gameplay target, word prompt or direction arrow. Root's source-face review explicitly accepted it only as an identity mark. This static preview deliberately omits live puzzle/supply presentation; it cannot establish that players understand which letters to collect or where to deploy a crafted object.

## Saved fixture envelope

Read native `parcel-dressing-setup/native/inspections/20261028T205502Z-85587fc5/inspection.json`, creation receipt `native/before-profile/COMPLETE.json`, current profile rows and saved prefab transforms. Inspection SHA recorded by creation is `88e8577296254b6bbd5c411829349b5a55860b5c41ff7cd0144638fbe414f598`. The current profile has exact Service/Sorting/Dispatch references with zero row offset/Euler and unit scale.

The following envelope is calculated from those **native-measured** source bounds plus the saved fixture wrapper poses, rather than a new Unity measurement in this review. All groups face the north interior; their furthest Z is3.895m,5mm inside the3.9m wall face.

| Original fixture | Count | Saved lowest/highest Y | Z envelope | What it establishes |
|---|---:|---|---|---|
| Sconce |4| Service/Dispatch2.5–2.833m; Sorting2.7–3.033m |3.632–3.895m|Shallow, above ordinary head clearance, no Light component. |
| Wall shelf |2|2.55–2.8275m|3.665–3.895m|Fits the shallow wall band; empty architectural shelf, no fake usable books. |
| T sign |2|2.55–3.19m|3.8295–3.895m|Below3.6m ceiling with0.41m top margin; identity accent only. |

These bounds support the initial flat north-wall placement. They do not prove attachment/clearance for arbitrary custom ramps, jumping into the fixtures or future room edits. Dressing adds no physics/resource entities. Saved source models/materials remain borrowed; native creation records exact profile/meta backup and unchanged protected source hashes.

## Gameplay role clarity

Source content has a purposeful three-room loop: Service holds the return CLOCK site and paired desks; Sorting has the TABLE site and actual CRATE/BOX storage; Dispatch has the STOOL site, counter and chests. TimeRun follows outbound, inbound, outbound laps; Freestyle exposes the six physical station instances through the practice selector. Real props retain smash/carry and letter behavior. The route reservation and puzzle-space reservations separate travel from furniture; this review makes no new clearance/progression claim beyond root's tests.

The static art does not yet explain that loop by itself. The staged desks suggest service, but empty shelves and pale parcels make the work areas look unused. The two upper T signs have no distinct room semantics. Final Windows review must check live station prompts/markers, the first readable objective, how crafting is introduced, and whether the player can see an obvious next destination without reading several boxes of text.

## Secondary art candidate, not the next depth correction

An optional **single course-local CRATE finish comparison**, using the existing original mesh and existing saved tactile wood materials. Current `CRATE.fbx.meta` binds `wood_Classic` and `wood_light_Classic`; the Parcel profile currently does not remap CRATE. Native-inspect which renderer/material role forms the body versus diagonal braces, then compare an exact `CRATE + wood_Classic → ParcelService/Materials/Walnut.mat` remap while retaining the original light braces. If that contrast makes the real parcel crates clearer, save only the course-owned profile row after exact backup. No global library material, mesh, visual bounds, collider, item recipe, position, count or letter budget changes. It stays on the existing normal ModelVisual/skin/tactile path.

This is a bounded improvement to an already interactive storage object, not a claim that recolouring will solve the room composition. Do not add more floating shelves or fill them with noninteractive copies of BOOK/LAMP. A meaningful later sorting-bay composition should regroup existing **real** parcels near the workbench in a separately reviewed authored-layout change with route/site/break/carry checks; tabletop support/lifetime is additional gameplay work and is not faked by this art hook.

Also investigate contact depth before increasing mesh detail: the thumbnail uses two shadowless preview lights, while runtime lighting has a separate environment path. The image's flatness is confirmed, but a runtime shadow defect is not established from the thumbnail. Compare the pending Windows images first; any lighting adjustment must remain consistent with actual gameplay and preserve preview-scene isolation. Do not brighten/saturate everything or add one light per sconce.

The eighteen-image Windows review and deeper rendering diagnosis follow. No final art acceptance or production-ready claim.

## Concrete depth diagnosis

This is not a general replacement of original models with unlit meshes. `Materials/Library/wood_Classic.mat`, `Materials/Tint_Base.mat`, Parcel's red/oak/walnut finishes and the original brass material use shader GUID933532a4fcc9baf4fa0491de14d08ed7, verified against the installed URP `Shaders/Lit.shader.meta`. Original furniture takes the normal `ModelVisual.Spawn` path with exact material remaps before tactile enhancement. The saved Parcel wood variants retain base grain, detail normal/albedo maps and metallic/smoothness maps. `Surfaces.Get` supplies Lit plaster/planks with normal maps; architecture meshes have normals/tangents and enable shadow casting/receiving. Adding more normal detail or recolouring a crate is not the first explanation for the broad flat image.

There are three different rendering contexts:

| Context | Confirmed setup | Implication |
|---|---|---|
| Actual Windows gameplay | Saved WordboundGameplay scene has one directional light with soft shadows. PC pipeline supports main/additional shadows and active SSAO. Player camera uses GraphicsOptions post-processing; current Windows frames visibly show avatar, desk, wall-fixture and floor/contact shadows. | Do not change runtime tone/lighting to fix an Editor thumbnail. It is visibly more dimensional already. |
| Solo static thumbnail | `WordboundSoloPreviewStage` sets two white lights1.2/.7, both `LightShadows.None`, plus grey ambient. Preview camera has a separate rendering setup. | Deliberately removes cast-shadow/contact cues and uses a fairly strong fill. It underrepresents the actual game's object depth. Source inspection identifies this; a same-frame native comparison is required before accepting a fix. |
| Workshop cutaway | `Material()` creates `Wordbound/Render/Surface`, whose GUID650dd9526735d5b46b79224bc6e94025 is installed URP/Unlit. It copies base texture/tint but loses the original Lit normal/roughness response. `Box` sets casting Off and receiveShadows false; both preview lights are shadowless. | Floors/edges remain equally bright beneath props, so objects visually float even if their exact transforms are correct. Restore original Lit surface clones before inventing new models. Ghost/selection affordances and borrowed materials need preservation. |

The original `Resources/Environment/SunlitHouse.asset` has an authored warm/cool key/fill ratio1.1/.22 and exposure.15 with contrast8/saturation10. Wordbound's saved gameplay scene has no EnvironmentLighting component; its fallback source uses exposure.85 and neutral contrast/saturation when no other authored environment is active. This is a confirmed setup difference, not proof that the runtime fallback is broken. The Windows images show readable shadows and vivid character colour. Do not globally install the house rig or increase saturation without a scoped comparison.

Scale/composition also matter. Parcel uses8×8m,10×8m and6×8m rooms at3.6m high, with a clear2.5m central corridor. Props are fitted to catalogue dimensions derived from their original item sizes, rather than arbitrarily made miniature in this slice. Twenty-one gameplay props are spread around three rectangular room perimeters; empty shelves, repeated paired counters and sparse middle-ground relationships remain visible. All three modules use the Packing palette. A single high T sign and tiny sconces cannot create a work-area composition. The static image's large floor foreground and diagonal view clip the Service sign; improving framing alone would not furnish the room. CourseWorld builds floors and walls but no new top roof for this single layer, so the sky/background above wall tops in actual play is an open-room presentation, not a detailed indoor ceiling.

## All eighteen Windows images opened

Build `20261028T211157Z`, parent-reported source66687. Both completed `checks.json` files report PASS with zero console errors/exceptions/warnings. Solo has3steps and9captures; Parcel Workshop has4steps and9captures. I opened every original1280×720 PNG listed below with view_image. These are entry/menu/return/authoring smoke views, not a human completion or multiplayer acceptance run.

Solo run `docs/qa/runs/20261028T211338Z-wordbound_parcel_service`:

1. parcel_service_initial_lobby.png
2. solo_parcelservice_timerun_selected.png
3. solo_parcelservice_timerun_world.png
4. solo_parcelservice_timerun_menu.png
5. solo_parcelservice_timerun_returned.png
6. solo_parcelservice_freestyle_selected.png
7. solo_parcelservice_freestyle_world.png
8. solo_parcelservice_freestyle_menu.png
9. solo_parcelservice_freestyle_returned.png

Workshop run `docs/qa/runs/20261028T211423Z-wordbound_parcel_workshop`:

1. parcel_workshop_assigned.png
2. parcel_workshop_saved.png
3. parcel_workshop_sorting_close.png
4. parcel_workshop_playtest_world.png
5. parcel_workshop_playtest_menu.png
6. parcel_workshop_returned.png
7. parcel_workshop_assigned_returned.png
8. parcel_workshop_parcel_returned.png
9. parcel_workshop_lobby.png

Confirmed improvements: TimeRun, Freestyle and Workshop playtest now show the entire character at spawn with space around it, instead of the prior near-camera head filling the view. The figure remains large (about half the image height), consistent with the current close third-person framing. Shadows clearly ground the avatar/desks and attach sconces/sign to the actual wall. TimeRun's keepsake ring/prompt and Freestyle's practice-station ring are visible. Equipment menu has a legible list and large original BAT preview; both return labels lead visibly back to the expected chooser or Workshop. Returned Solo selection and Workshop named drafts are visually retained.

**P2 visual regression:** In saved/close/returned Parcel Workshop cutaways, the wall-only sign, sconces and shelves float in empty sky because the cutaway intentionally removes their supporting walls. This is confirmed in all relevant Parcel editing views, especially `parcel_workshop_sorting_close.png`; it makes the authoring preview appear broken. It is not seen in actual playtest, where the real wall is present. Minimum correction: omit wall-only dressing from the cutaway rendering while preserving the profile/prefab data and normal gameplay/thumbnail instantiation. Do not add fake supports or hide actual gameplay props. Document that decorative fixtures are edited in Unity Prefab Mode and inspected in Playtest/full preview.

Remaining clarity limits: the main doorway is off the initial spawn frame, so the route is not immediately visible in these static entry shots. TimeRun's world label overlaps the right desk visually but remains readable. The initial equipment list defaults to BAT with no letters, whereas the nearby practice site concerns CLOCK; these views do not prove a new player understands the collect→spell→place loop. The source snapshots legitimately show falling/spawned letter tiles at the right edge; single frames cannot establish settled-state readability. The lush lobby illustration and the sparse geometric map cards still establish noticeably different detail expectations. No widespread text clipping was found in the reviewed chooser/menus, but this is not overall UX/art acceptance.

## Bounded next correction

Root authorized a separate Workshop-only source draft under `workshop-depth/`: hide unsupported decorative wall fixtures in the cutaway and use owned clones of the existing original Lit surface materials for opaque floors/edges. Preserve their grain/normal maps, UV scale, source refs, selected tint, translucent adjacent floors, layer31 isolation and cleanup. Do not change gameplay material/lighting or replace original props. A shadow-key toggle alone is insufficient: URP may choose RenderSettings.sun as the main light. No global sun/pipeline or other-scene light mutation is included; a cautious material/cutaway change and actual native comparison is preferable to an unproven shadow claim. Root owns the independent Solo-preview comparison in another lane.

Expected measurable visual difference: opaque floor normals respond to existing illumination, edges separate more clearly, and no isolated wall decorations hang above the cutaway. This source expectation is not a confirmed screenshot gain. Compare exact saved/close/returned Workshop views and rerun owned-material/source-preservation/layer-switch tests before acceptance. The overall parcel-office composition and long-term map variety remain open.
