# Shop portraits: bounded production adoption

STATUS: ADOPTION DRAFT PREPARED — native A/B/C and D/E alpha/transfer tests passed. Root selected D(0.8/0.4) after inspecting D/E; a one-file production draft is prepared in `production-adoption-draft/`. Production publication and actual Shop compositing remain root-owned and pending. No live asset change or native job by this author.

Observed native evidence: `shop-portraits-corrected-lighting/20261026T121120648Z-cceec7e1` reports alphaVerified=true for all three, RGB maximum delta1, zero leaks and preserved sources/targets/global state. Both reviewer and root inspected A/B/C; B improves mint but clips the face, C is worse. Neither is selected. The bounded next D/E comparison retains the neutral directions with lower intensities0.8/0.4 and0.7/0.35.

## Decision gate

- Keep the successful saved head correction. The body shading predates that correction; do not alter original FBX normals beyond the reviewed saved head, original materials/textures, wardrobe RGB values, pose or framing.
- Selected control: **D-neutral-front-soft**, neutral white key0.8/fill0.4, fixed camera-relative directions and ambient0.45. Native D/E evidence is `shop-portraits-corrected-lighting/20261026T121705173Z-54f5a1c3`; root and this reviewer inspected D/E. The production receipt must name D and its actual numerical settings. Human style acceptance remains pending.
- Require native RGBA alpha0 borders/background, opaque torso/foreground, an alpha-bearing output format, valid orientation, RGB parity within2/255 against the same frame's RGB24 output, clean ownership and source/global/current-target preservation. If native alpha or transfer fails, preserve the evidence and repair that route before any publication.

## Small implementation change after selection

Use the existing `ShopPreviewImporter.Rebuild` route and its safe backup/stage/publish receipt. Apply the selected light preset consistently to all six looks. Promote the verified native RGBA readback into the production capture; retain the utility RGB output transiently for the transfer check and destroy it after use. Keep the comparison entry available for repeatable diagnosis. No runtime shop, player, mesh or material change is required for this adoption.

Stage all six new RGBA PNGs and their receipts before writing the current targets. Retain the current six RGB PNG/meta files in a new versioned `Previous/` folder, preserving the earlier brighter originals and all previous captures. Reuse exact-reference head validation, original remaining-mesh identity, source/file/meta/memory checks, scene/global restoration, target concurrency checks and zero-leak requirements. Publishing remains sequential; record partial publication accurately and preserve backups/staged outputs on failure instead of silently retrying.

Existing target paths stay `Assets/_Project/Resources/UI/Shop/Looks/<id>.png`. Preserve their GUIDs. Set alpha from the input, transparency handling, Default texture type, sRGB, no mipmaps, NPOT=None and max512 through the six TextureImporters only. No global SaveAssets or manual metadata edits. Explicitly verify256×320 imported dimensions and an alpha-bearing imported graphics format; `background.a=0` and PNG file existence are insufficient.

For these six small UI portraits, prefer uncompressed RGBA to preserve delicate glasses/hair edges and the validated color output. At256×320 with no mipmaps, six RGBA32 textures total1,966,080 bytes (1.875MiB) before engine overhead. If retaining compression is preferred, require a measured alpha-supporting import and actual card-edge review; do not assume Automatic/DXT preserves the required appearance. Source art textures and unrelated import settings remain untouched.

## Actual shop validation

`ShopPage.LookCard` loads these exact Resources textures into a standard RawImage with FitInParent. It does not add a custom tint/material in that path, so first verify the alpha fix through the existing UI rather than changing card colors or adding a compensating shader.

Run the existing native lobby capture after successful six-image rebuild. Open the actual Shop view at the same1600×900 reference size, plus an existing supported smaller view if that capture route already provides one. Require:

- All six cards display the chosen vivid, readable lighting consistently; the cream face, brown hair and black accessories remain distinguishable.
- The mint card panel shows directly around each silhouette; all six cream rectangles disappear.
- Hair tips, cap brims, glasses, cuffs, boot soles and straps have clean edges against mint. Inspect partial-alpha pixels in the actual composite, not only the numerical alpha count. No cream fringe, dark halo, missing opaque pixels or holes.
- Portraits retain their aspect ratio, framing, readable badge and accessory silhouette; no stretched head or clipped hat/boots. Card labels, price/equip controls and raycast behavior remain unchanged.
- Selecting/equipping an existing owned look still updates the real avatar; the thumbnail lighting can be art-directed but must not misrepresent which clothing/colors are selected. Use the existing smoke/capture state-preservation route; avoid purchases or persisting a different user outfit merely for a screenshot.

The actual Shop screenshot and resulting Windows build must use the newly imported textures, not cached in-memory copies from before reimport. Reopen the relevant UI or use a fresh process under the official gate as appropriate. Final Windows smoke/online verification remains root-owned and separate from this portrait task.

## Completion evidence

Record the selected control, source receipt, exact patch hash, six source PNG hashes, stable GUIDs, alpha/dimension/import-format checks, native source/global/cleanup proofs, preserved old images and the actual Shop capture path. Keep `REBUILT_NOT_VISUALLY_APPROVED` until agent inspection is recorded; human style acceptance remains pending unless the user explicitly approves. Do not describe a passing render or the mesh-normal repair as final vivid art by itself.
