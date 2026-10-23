# Original Wreckabulary visual reference and revision brief

STATUS: OWNER REJECTED DetailV2 appearance — review complete; replacement samples NOT PRODUCED

The owner clarified that the comparison is with the **original Wreckabulary game assets**, not the first Wordbound batch. Both Wordbound revisions and original game assets remain unchanged. DetailV2's prior technical checks do not establish acceptable appearance. My earlier reviews emphasized construction, pivots and readable parts and did not catch the loss of the original game's visual identity; those reviews must not be treated as owner acceptance.

## Correct original references

Primary reference: [supplied game asset showcase](../../Web/public/art/supplied-showcase.png), a 1440×960 existing render of the original SOFA, TABLE, CHAIR, LAMP, PLANT, BAT, BOMB, letter tiles and avatar together. `Tools/AssetPipeline/render_supplied.py` imports their actual `Web/public/art/items/*.glb` files to produce this composition. It is an existing asset render, not a new concept or a claim about current Unity lighting.

The original item icons at `Assets/_Project/Resources/UI/Items/<ID>.png` provide all twelve actual silhouettes. `Tools/AssetPipeline/render_icons.py` renders those items from the same GLB library. The icons are 192-pixel images; they support shape/color comparison but cannot prove close-up surface quality against a 640-pixel new render. The high-resolution showcase is the better SOFA/TABLE/BAT material and proportion reference.

Original runtime models are `Assets/_Project/Art/Imported/Items/<ID>.fbx`, referenced by `Resources/ModelLibrary.asset` as `Items/<ID>`. Their export alternatives are `Web/public/art/items/<ID>.glb`; importer selection points to the existing vault-v2 source pack. These are the appropriate starting shapes, not the rejected Wordbound replacements. Source-pack location/editable source availability should be established before claiming an original editable Blender file exists.

Read-only SHA-256 checks confirmed SOFA/FAN/BED original icon bytes and SOFA/FAN original FBX bytes in integration exactly match original-main checkout `wreckabulary` at `f7c3c90`. Representative hashes: SOFA icon `bc569f1c62ce4d2e7da14898f4f24ef6cde2ab8499fac9356c3e9962e77f0d63`; FAN icon `6b87d913a5bb1eeb555e244c3b5ce3738cf61a5622e85f750d674ab70aa39aff`; SOFA FBX `88ae113e8fce5a4c4ed647f95b55ff0b0098bc9e668db6c1402597ac1a632a3f`. Original main, font and Lunar work were not modified.

## Actual visual comparison

All twelve original icons, the original showcase and all twelve DetailV2 three-quarter renders were directly viewed in this review. The following is visual judgement from those images, not a performance or geometry pass. Original/render cameras and resolutions differ, so shading contrast alone is not evidence of an engine regression.

| Asset and original reference | Observed DetailV2 regression | Features to retain and improve |
| --- | --- | --- |
| [SOFA](../../Assets/_Project/Resources/UI/Items/SOFA.png) | Original coherent sage two-cushion sofa becomes three angular red cushions with green sides, broad cream bands and a heavy brown plinth. Faceting and piled trim overpower the upholstery. | Keep the original two-seat silhouette, soft continuous arms, gently rounded back and pale feet. Add compressed cushion edges, believable piping and fabric variation at a subtle scale. |
| [FAN](../../Assets/_Project/Resources/UI/Items/FAN.png) | The slender powder-blue frame becomes cream/olive with a swollen layered base and visually heavier trim. Construction detail was added, but the object feels older and less clean. | Retain powder-blue housing, light grille, slender stand and clear blade opening. Add real guard separation, motor ventilation, tilt joint and small fasteners. |
| [BED](../../Assets/_Project/Resources/UI/Items/BED.png) | Pale wood and a blue blanket become dark orange/brown framing, olive panels, a red band and a thin grid on a stiff slab. Added lines do not communicate soft bedding. | Preserve the blue/cream bedding and pale frame. Shape mattress compression, a naturally folded blanket edge, pillow volume and joinery. |
| [TABLE](../../Assets/_Project/Resources/UI/Items/TABLE.png) | Pale three-board top and useful lower shelf are replaced by orange surfaces, a broad cream border and green corner plates. The plates do not read as necessary construction. | Keep the original plank divisions, rounded corners, lower shelf and light timber. Add restrained end grain, mortise/joint cues and edge wear only where plausible. |
| [STOOL](../../Assets/_Project/Resources/UI/Items/STOOL.png) | Cream/pale construction becomes a thick green rim over brown legs with strong surface streaks. Extra braces add bulk without resolving the seat material. | Keep the original clean round seat and leg stance. Decide whether the seat is wood or upholstered, then model its real edge/join and restrained finish accordingly. |
| [BAT](../../Assets/_Project/Resources/UI/Items/BAT.png) | The pale wood bat becomes orange with wavy, shiny large-scale grain and green trim. The grain dominates the form and looks smeared near the tip. | Keep the pale rounded barrel, narrow neck, cream wrap and knob. Add longitudinal grain, clean end grain and credible wrap overlap; no unrelated collar color. |
| [HAMMER](../../Assets/_Project/Resources/UI/Items/HAMMER.png) | The original blue-gray rectangular metal head becomes an ivory rounded mallet with brass caps and a textured centre that reads fabric-like. This changes the tool's identity. | Preserve the metal head silhouette and pale handle. Add forged/machined bevels, a believable handle-eye connection and restrained grip detail. |
| [BROOM](../../Assets/_Project/Resources/UI/Items/BROOM.png) | Light handle/red head becomes dark wood/olive hardware; bristles read as many rigid beige rods. | Preserve the everyday red broom head and light handle. Use clustered tapered bristles, a socket/attachment and subtle irregular ends. |
| [SHIELD](../../Assets/_Project/Resources/UI/Items/SHIELD.png) | The blue pointed shield with a white star becomes an olive octagon with a red diamond and oversized brass tabs. Original recognition is lost. | Preserve pointed silhouette, blue field and star. Add a coherent rim, underside handle/strap and restrained surface finish. |
| [SPEAR](../../Assets/_Project/Resources/UI/Items/SPEAR.png) | Slender silver-blue tip becomes a broad cream leaf shape with decorative gold fittings and brown/green shaft. | Retain the original slender tool and metal tip. Add a clear metal socket, fastening and real grip wrap while keeping the tip readable. |
| [PLATE](../../Assets/_Project/Resources/UI/Items/PLATE.png) | Plain white ceramic acquires green/gold rings that make it look like an unrelated formal dinner service. | Preserve clean white glaze, shallow dish and rounded lip. Add an underside foot ring and subtle glaze response; decoration should be a deliberate variant. |
| [CLOCK](../../Assets/_Project/Resources/UI/Items/CLOCK.png) | Restrained cream face/light rim becomes a thick orange wooden case with ball feet and more markings. The extra case/feet change the original portable shape. | Preserve the cream dial, restrained pale/brass rim and dark hands. Improve real case depth, hand clearance, crown/back details and material separation. |

DetailV2 comparison images are at `ArtSource/Collections/WordboundPvP/DetailV2/Previews/<ID>-three-quarter.png`. These should remain clearly labeled as rejected, not shown as the target design.

## Coherent next direction

Build on the original game's clean, rounded household objects and calm color relationships. “Detailed” should mean convincing shape, assembly and material response: soft upholstery should compress and fold, metal should have readable edge highlights and actual joints, wood grain should follow the part, and ceramic should have a believable rim/foot and glaze. The same olive/red/cream/orange palette and decorative brass fittings should not be spread across every object.

The largest correction is form and identity. Smooth highlight flow and cushion volume must be established before adding small seams or maps. Keep details at their real purpose and scale. Use restrained surface variation; broad noisy normal patterns and uniformly glossy grain can make clean objects look cheap or worn. The original game's friendly proportions and colors remain the reference while material construction becomes more believable. This is a proposed art direction; only an actual new sample can establish whether it succeeds.

## Small pilot before another full set

1. **SOFA first:** copy the original game shape into a new revision source; preserve its two cushions, sage palette and rounded arm/back silhouette. Rework genuine cushion volume, seam placement and feet/joints. Show a neutral clay view first to expose silhouette and faceting, then fabric/wood material views. Do not use DetailV2's three-cushion color blocking or plinth as the base.
2. **FAN second:** preserve the original powder-blue silhouette. Refine grille depth, housing ventilation, axle/tilt support, blade thickness and controls. The front, side and rear must explain how the parts connect. Keep the original lightness; added construction must not turn the base into a stack of oversized blocks.
3. **Held tool only after those samples establish the direction:** use HAMMER to prove the original metal-head identity, handle connection, grip and material response survive first-person/third-person handling.

For each pilot, preserve original bytes and create a separate editable source with meaningful parts/material names. Produce matched original-versus-candidate framing, scale, camera and neutral lighting, plus front/side/rear and close-up detail. Include a view beside the existing avatar and original furniture, and a gameplay-distance image before deciding that detail is useful. A source render is not final Unity acceptance. The root will coordinate the next bounded render/production slot and visible owner review.

The visual sample is the first gate. Existing dimensions, grips and runtime compatibility remain required, but renderer-count reductions are not a substitute for quality. Apply budget optimization after the approved form/material direction; if an initial budget prevents preserving a visibly stronger original curve, surface that tradeoff instead of silently simplifying the shape. Full-set production should wait for the two representative samples to visibly improve on the original game references.

## Verification boundary and next action

This review read source, resolved model references, checked five original-main hashes, and viewed existing images. No Blender, Unity, image generation, model modification, source deletion or new rendering occurred. No photorealistic or production-ready claim is made. DetailV2 remains owner-rejected regardless of its prior technical passes. Next action: root confirms the pilot brief and schedules production of the original-based SOFA sample, followed by FAN, while preserving all existing revisions.
