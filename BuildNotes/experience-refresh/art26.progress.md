# Experience refresh — art26

STATUS: IN PROGRESS

Owner: art26, branch `sethypagna/hud-readability`, start revision `eb49368757efb4b6eae5bbc5f28c6a3a81106c9e`.

Scope: read actual original Wreckabulary assets, current equipment/cosmetic/motion source and 035159 Windows images; propose an implementable first collection in the original silhouette/material/rig language. Report only. Root owns catalogue/shop UI, runtime edits and heavy jobs. No provider calls, uploads, new asset generation, Blender, Unity, tests or player execution in this lane.

Preservation: `BuildNotes/flagship-content/focused-first-attempt.log` was the sole pre-existing untracked path and is excluded. Original checkout/font/Lunar work and all failed or rejected art remain unchanged. No project AGENTS.md or META-HARNESS.md is present in this worktree; CLAUDE.md and portable core/investigate workflow read. Root shared lane registry and continuing portable run are authoritative coordination evidence.

Question: what is the smallest useful equipment/skin/motion batch that adds true shape variety while preserving the accepted original reference and current no-code/presentation contracts?

Confirmed initial evidence:
- Current art report and ASSET-REVIEW identify original Wreckabulary as default, with DetailV2 explicitly preserved as owner-rejected archive. Historical native success is not owner style approval.
- `game-dev` is not on PATH in this lane. The game asset production skill is used for specification/evidence separation only; no CLI execution is required for this report.
- Exact 035159 images still need direct image inspection; prior review text alone is not visual proof.

Next recoverable action: inspect all three 035159 PNGs with view_image, then trace source interfaces and propose a small cosmetic-only first batch with measurable delivery budgets.

## Confirmed source and image findings

All three actual PNGs under integration `docs/qa/runs/20261024T035159Z-visible-wordbound_original_equipment/` were viewed directly with `view_image`: world, held BAT, held HAMMER. Original soft household forms and red-hood character are coherent. BAT extends behind the hood in its single pose; HAMMER handle/contact is occluded and its head is small beside the mitten. The placed foreground BAT obstructs both close-ups. This establishes framing/readability limits, not a measured contact or animation defect. The original `Web/public/art/supplied-showcase.png` and `docs/art/SUPPLIED_ITEMS_CONTACT_SHEET.png` were also viewed directly: pale simple timber, sage rounded furniture, powder-blue painted pieces, restrained warm metal accents, white ceramics; expressive toy proportions. No new image or model was generated.

Read current integration source: `WordboundVisualResolver`, `WordboundVisualProfile`, `WordboundEquipmentCollection`, `WordboundGameplayPresentation`, `WordboundAvatarMotion`, `ModelVisual`, `ModelLibrary`, `MaterialLibrary`, `Wardrobe`, `CosmeticBundles`, `ShopPage`, and original `PlayerAppearance`/asset pipeline. MaterialLibrary skins are material-family replacements; the equipment collection requires unique itemId/modelKey and is Classic-only. Profile Overrides are per canonical item and take priority, so they are not per-player skins. Outfit.ItemSkins already serializes item-to-skin IDs. Wordbound world creation currently uses Classic default; ShowHeld alone reads carrier skin. Do not promise drop/throw skin persistence from the current path.

Shop cards currently use a palette blob + generic accessory icon for looks, and a skin disc + original recipe icon for finishes. A bounded refresh needs truthful rendered thumbnails for the selected geometry and one shared 3D preview, rather than extra decorative panels. Existing crewneck/cap/hood/satchel meshes provide silhouette choices without changing the rig.

Direct read-only GLB JSON counts (index accessor counts, triangle-mode primitives; these are stored mesh counts, not measured runtime draws): BAT 5,248 triangles / 1 mesh / 2 material primitives; HAMMER 5,872 / 1 / 4; SHIELD 172 / 1 / 4; FAN 4,828 / 1 / 4. Original avatar manifest records 22 bones, 17 clips, 65,086 triangles for all wardrobe meshes and 52,488 default triangles. Its existing browser mobile audit records 34,424 total / 29,136 default triangles; this does not establish a Unity LOD. No LODGroup path was found in the inspected Wordbound presentation/art source. The old Wordbound importer enforces 6,000 triangles and exactly its two collection materials; it is not a generic importer for a new skin batch.

## Concrete first collection proposal (not implemented)

Working name: **Packing Club**. Exactly two equipment geometry variants; no new recipe, damage, reach, collider, resource cost or cooldown. Preserve Classic default and all archived art. This is a design proposal, not owner style approval.

| Variant | Authored form and material language | Parts and provisional budget |
| --- | --- | --- |
| BAT / PackingClub | Pale wood bat with four softened barrel flats and a rounded-square crown, retaining the original narrow handle and cream wrap. Two broad laminated timber strips provide meaningful construction; no brass decoration or noisy grain. This creates a distinct barrel silhouette instead of a recolour. | Barrel, HandleWrap, Knob, Grip_R; combine rigid parts by material. At most 3,000 triangles, two shared material slots; LOD1 target 1,200. |
| SHIELD / PackingClub | Shallow oval blue shield, pale timber rim, cream star and one credible back strap. The oval outline is a deliberate selectable alternative to Classic's pointed shield. | ShieldBody, RimStar, BackGrip, Grip_R; combine rigid parts by material. At most 1,200 triangles, two slots; LOD1 target 600. |

Companion look reuses existing Crewneck + Cap + Satchel (cream/blue/wood-associated colours). Label it as a new coordinated look using existing garment shapes. A new vest/coat requires a skinned-mesh attachment/import contract not present in the inspected wardrobe path, so it is outside batch one.

Canonical gameplay measurements stay unchanged: BAT .172 x 1.018 x .172 m, catalogue grip (0,.17,0), heldScale .5894; SHIELD .46 x .57 x .1877 m, catalogue grip (0,.29,-.11), heldScale .6316. The actual original GLB SHIELD Grip_R is (0,.32,-.11), and HAMMER is (0,.15,0) versus catalogue .13. Preserve the authoritative imported/socket behavior and validate actual transforms before export; do not silently copy one coordinate source over another. The resolver fits equipment independently on each axis, so new variants must be authored to the canonical envelope to prevent unintended stretching. A square barrel's diagonal must remain inside BAT's envelope.

Root data proposal: an additive `(itemId, skinId)` visual entry binding a render-only model, true thumbnail, measured bounds and validated grip/heldScale to the canonical recipe. The same resolver identity must serve held, world and preview states, with a defined Classic fallback. Do not store damage or physics in this visual entry. Existing collection and per-course overrides remain distinct. Root owns this API and state-policy decision; no source mutation here yet.

Budget proposal: one shared 512 x 512 colour atlas for the two variants, optional shared linear normal/mask only if a matched game-camera comparison proves useful. Use original-like rough material response and broad readable shapes. Target at most two active material draws per equipment instance; 8 held BATs would therefore be at most 24,000 source triangles / 16 material submissions before actual engine batching. Retain lower-detail source meshes, but do not claim working LOD until a runtime switch path is implemented/tested. No additional skinned meshes/bones, no particles, lights or per-instance update scripts. Existing provider cap file was read; caps are not authorization to spend. This proposal spends zero provider credits.

## Motion investigation extension

Root authorized a bounded first motion slice in `WordboundAvatarMotion.cs`, `WordboundGameplayPresentation.cs`, focused tests and this report, after an accepted-event hook proposal. Original Wordbound motion currently binds Idle, Walk, Run, Hold and Carry. Original PlayerAppearance uses existing Swing_OneHand, Thrust_OneHand, Throw_OneHand, Pickup, Place, Hit_Reaction, Jump_Preview and other clips, but that does not establish Wordbound playback. Next: inspect authoritative accepted gameplay signals and propose one additive cosmetic hook without changing authority, canonical physics or locomotion/socket transforms.
