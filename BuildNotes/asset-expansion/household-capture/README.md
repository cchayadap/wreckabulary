# Household native review capture draft

STATUS: SOURCE PREPARED — native compile, capture and visual inspection are NOT-RUN. There are no images from this draft yet.

Independent revision 2 source review is complete: no remaining blocking source finding; syntax parsing and strict patch applicability passed. The frozen source and patch hashes are in `manifest.json`. This is not a native rendering or preservation result.

`patch` adds one Editor-only `HouseholdReviewCapture.Run` diagnostic. It requires the eight saved Household prefabs and `HouseholdStudio.unity` from the root's verified setup. It refuses to reuse `BuildNotes/asset-expansion/household-capture/capture-01` and writes each output with `FileMode.CreateNew`.

The four planned native images are 1,280 × 800: `gallery.png`, `kitchen.png`, `attic.png` and `rooftop-parcel.png`. Each uses actual prefab instances with their saved materials, original local scales and common neutral preview lights. A closer camera frames each subgroup; receipt bounds, scales and orthographic size preserve the physical-size evidence. No asset is recolored, resized, reimported, repainted or wired into gameplay. There is no floor, character, animation or physics simulation; this keeps the first diagnostic focused on the new static furniture and glass.

Prefab instances are created directly in the `PreviewRenderUtility` owned scene using the existing project's `PrefabUtility.InstantiatePrefab(prefab, scene)` pattern. Prefabs with additional behavior components are rejected before instantiation. Every view closes its preview and destroys owned image/clone objects, including failure paths. No source material or mesh copies are created or edited.

`receipt.json` records source/dependency/meta SHA-256 before and after, material memory fingerprints/dirty flags, source colors/shaders/render queues, native image hashes/formats, framing/bounds, effective lights, and cleanup results. It also compares open/active scenes, root identities, dirty flags, render settings and render pipeline. RenderTexture.active, async shader compilation and the render pass's SSAO keyword are restored. No game/save preferences are accessed. A preservation mismatch is a failure and is retained for inspection without automatic source repair.

The source/meta fingerprint set includes all on-disk prefab, saved-scene/settings and pipeline dependencies, the source package, this diagnostic and relevant ProjectSettings. Explicit inputs, all eight source FBXs, the source license/manifest and required ProjectSettings must exist before capture; explicit Assets inputs must also have metadata. Missing Assets dependencies fail preflight. Built-in/virtual dependencies without ordinary disk files are listed separately as exclusions, not claimed preserved. A non-Assets source file with no metadata is recorded as missing metadata before and after. An unavailable required source is not silently omitted.

Root integration: review and apply the patch, let Unity generate metadata, run required R0/core checks, then execute `Wreckabulary.EditorTools.HouseholdReviewCapture.Run` through the official gate with the pinned Unity editor and graphics enabled. Inspect all four images and the receipt before claiming a successful art review. A completed capture is not owner approval, Windows gameplay acceptance or physics acceptance.
