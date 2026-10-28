# Parcel Service material direction

STATUS: COMPLETE — actual original capture and read-only source/material review. No production edits, native execution, generation jobs or paid calls. The recommendation below is an unrendered candidate, not owner acceptance.

Image inspected: `map-previews/captures/20261028T193758Z-37c05442/ParcelService.png`, opened at original resolution via view_image. Scope: bounded course-specific editable material/room treatment; preserve original furniture meshes, hardware and wood grain. Recommendations are not rendered or accepted output.

## Actual image

The original desks, shelves and chest have useful construction detail: drawer fronts, handles, rails and plank grain. Keep those meshes. The weak point is material separation: most furniture has the same pale yellow finish, against a broad muted teal wall and brown floor. The result is legible but looks like repeated stock furniture, without a distinct parcel-office identity. Increasing saturation everywhere would make the room noisier without solving that hierarchy.

The 960×540 image is a static preview with its own lighting. It does not prove live lighting, grip, collision, gameplay visibility or the quality of every room. No geometric detail is missing merely because it is small in this thumbnail.

## One bounded direction: ink, oak and parcel red

| Editable role | Candidate colour | Treatment |
| --- | --- | --- |
| Packing wall plaster | `#30465D` | Clear ink blue, replacing generic teal; retain plaster texture and roughness. |
| Ceiling | `#E9DDC6` | Warm chalk, lighter than wall; avoid a cold white ceiling. |
| Floor | `#754D35` | Rich walnut, retaining existing plank texture; separate floor from furniture. |
| Frame / doorway woodwork | `#B47A43` | Honey oak; makes real openings visible without adding strips or changing collision. |
| Selected fixed fixture wood | `#A94735` | Restrained brick-red stained/painted wood, only after exact source-material roles are inspected. Preserve grain, drawer relief and all metal hardware. |
| Remaining furniture wood | Original or honey oak | Leave parcel boxes/crates and crafted equipment natural in the first slice. Avoid recolouring every prop. |
| Metal / brass / glass / fabric | Existing originals | No blanket tint, metallic/smoothness replacement or global library edits. |

Prefer desk wood in parcel red with shelf/chest wood left warmer and darker for separation, subject to the actual native renderer/material roster. A material shared by a whole mesh cannot selectively colour only drawer fronts; do not imply that capability. The new colours are a starting render candidate and must be compared under the same camera/light before approval.

## Implementation contract

Use a saved course-specific `WordboundVisualProfile` with an optional `RoomStyle` and additive named source-material remaps on the normal `ModelVisual.Spawn` path. That preserves original meshes, grip discovery and skin handling. Do not use `WordboundVisualOverride.Materials`: that array repeats the same slot numbers for every renderer and cannot safely express heterogeneous source-material roles. Keep unrelated models and profiles unchanged.

Clone original/tactile-seed materials into a dedicated Parcel Service folder through Unity APIs. `Resources/TactileVariantSeeds/wood_Classic.mat` already retains the base wood texture, detail albedo/normal and gloss map; preserve those maps, keywords and UV scales. Clone colour values only for the selected role. Avoid losing detail by replacing a post-Spawn tactile material with a plain material. Do not modify shared source material assets or the global `MaterialLibrary`.

The existing `WordboundCourseWorldOptions.RoomStyle` already supports a course-local architecture profile. Propagate the selected profile through runtime, Workshop preview and thumbnail stage. All three current modules are in `Packing`, so `ForLayer` yields one wall palette; do not fake different layer IDs to obtain room colours. Current wall insets require at least 4.1m height; this course is 3.6m, so enabling insets would not visibly add detail. No new geometry is proposed here.

Setup should clone the current original visual profile, preserve pose/override data, create exact new assets once, back up the Parcel course bytes and metadata before changing its single visual-profile reference, and refuse unexpected existing/dirty/partial targets. A repeat should validate and select existing edited assets without resetting colours. Native source-role inspection must precede paint assignment; slot order is not semantic evidence.

## Required comparison

- Same 960×540 preview camera before/after, then actual player-height Windows captures in Service, Sorting and Dispatch.
- Check wall/floor separation, door readability, red fixture prominence, grain and hardware close-ups, and the original natural boxes/crates.
- Check the Workshop floor/proxy and placed models use the same palette without hiding editable objects behind room walls.
- Confirm thumbnails use the exact saved course/profile and refresh dependency/hash receipts after material edits.
- Check original model geometry, grip, skins, collider bounds and source files remain unchanged. These are pending implementation/native checks, not claims from this visual review.
