# Editable environment art plan

STATUS: VERIFIED — native authoring, affected gameplay checks and corrected runtime screenshot review passed. Build/publication status is recorded in `ENVIRONMENT-REFINEMENT-2026-10-09.md`.

## Structure

Keep the five authored map prefabs as the world authority. The editor-only `Assets/_Project/Editor/Authoring/EnvironmentAuthoring.cs` adds a named, versioned presentation subtree under each existing `Geometry/Storey N`. Repeating the upgrade must preserve existing subtrees, transforms and material overrides. Do not rebuild furniture, alter room JSON, replace stair geometry, or move scene cameras.

Use room-sized ceiling panels, a thin roof cap, upper wall strips and cornice. Leave the Garden open. Upper floor slabs and stairwell openings keep their current collision and visibility behavior. Presentation panels have no gameplay colliders; this preserves jump, launch, projectile and couch-play mechanics. Avoid independently closing a stairwell with a ceiling.

The camera layer uses a `CutawaySurface` marker with serialized `Renderer[]`, `CutawayKind`, `OwnerStorey` and `RoomId`. The owner storey is the room beneath a ceiling. Added ceiling/roof/upper wall renderers remain visible in the editor; per-camera gameplay visibility must not modify their authored transforms or meshes. Floors are not included in this marker.

## Art direction

Common architecture: warm cream plaster, honey oak framing, restrained brass fittings, warm white ceiling undersides. Use one stronger room accent through wall panels and textiles: sage kitchens, seafoam bathrooms, muted coral living rooms/cafe, dusty rose bedrooms, ochre play/kids rooms and blue studies. Preserve blue/red/green/yellow flat identities on Walk-up Apartments.

Give a room one readable window/curtain cluster, one framed illustration or shallow wall shelf, and one practical fixture. The generated `Assets/_Project/Art/Generated/Environment/townhouse-print-v1.png` appears on square framed wall planes. Reuse persistent shared surface/material assets; do not create runtime textures for decoration.

Useful imported paths are under `Assets/_Project/Art/Imported/Environment/`:

| Asset | Triangles | Proposed use |
|---|---:|---|
| Wall_Sconce.fbx | 1,128 | Wall fixture, warm emissive bulb; actual light controlled by graphics policy |
| Wall_Shelf.fbx | 528 | One shallow shelf in study/living/cafe |
| Window_Frame.fbx | 3,024 | Window assembly on one selected exterior room face |
| Curtain.fbx | 3,552 | Bedroom/living window textile, omitted in tight utility rooms |
| Kitchen_Counter.fbx | 2,784 | Kitchen perimeter counter |
| Kitchen_Sink.fbx | 4,104 | One kitchen or cafe wet area |
| Kitchen_Fridge.fbx | 2,268 | One kitchen identity prop |
| Radiator.fbx | 4,512 | Selectively below windows, not every room |
| Kitchen_Stove.fbx | 8,064 | At most one focal stove per principal kitchen |
| Wall_Window.fbx | 6,048 | Avoid repeating a complete wall module over existing walls |

Existing craftable furniture remains interactable. New small dressing uses plain model visuals, no `Smashable`, loot or physics ownership. Window assemblies should not imply a traversable opening without a separate gameplay change.

The delivered pass uses shallow window/curtain clusters, framed prints, sconces, bulbs, wall panels and architectural trim. The floor-standing kitchen modules, radiator and counter ideas above remain proposed library options; they were not added over existing collision or furniture routes. Imported fixtures keep their FBX axis conversion beneath editable placement anchors. Door lintels begin above the existing 2.4 m opening and add no collider.

## Map placement

- Pinwheel: retain the playroom balcony and its ramp clearance. Give the living room the principal print/window cluster and keep the central connections readable.
- Garden Courtyard: leave the 12 × 12 m garden roofless and preserve the crossing paths. Place accents toward the inner garden faces and room perimeter.
- City Flat: strongest kitchen/bathroom material contrast; use shallow wall decoration in Hall so connected routes stay legible.
- Terrace House: protect the Hall/Landing stair run at x≈2, z=-4…0.5 and the upper stairwell. Distinguish upstairs bedroom/kids/bathroom by panel color and fixture shape.
- Walk-up Apartments: retain open Lobby/Landing switchback staircase, x≈±1.5. Cafe gets a restrained counter/display; Garage retains extraction/van space; each colored flat gets a distinct small vignette.

## Validation gates

Before adding detail, finish native five-map migration and compile/tests. The first migration exposed Unity errors when cloning non-readable procedural textures. The prepared correction copies their GPU pixels into serializable CPU-backed texture assets, retaining existing asset GUIDs. The expanded tests check persisted floor albedo/normal pixel variation plus exact linear/sRGB test-texture pixels after save, unload and reload. Those tests passed in the separate-process editor run; the repaired textures retain their original GUIDs. Inspect saved scenes after a separate editor launch as a separate visual check.

For the presentation pass, verify overhead and TPP per map, upper/lower local players simultaneously, stairs/doorways, tall avatar jumps and couch camera cutaways. Check that room panels do not fill staircase holes or obscure targets. Limit unique materials and fixture lights; use shared meshes/materials and a small fixed number of practical lights rather than one shadow-casting light per bulb. Capture one close interior and one map overview at minimum before accepting the pass.
