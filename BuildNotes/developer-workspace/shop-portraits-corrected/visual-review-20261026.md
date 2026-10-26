# Corrected shop portraits: visual review

STATUS: REVIEW COMPLETE — all six Previous/Rendered pairs, capture04 A/H and the actual Shop screenshot have been opened and inspected. No native job, image edit or live asset change by this reviewer. A bounded lighting/alpha diagnostic draft is prepared; its native result is pending.

The saved head correction improves the face and hair response, but the rebuilt six portraits are not a final vivid-art result. All six outfits are substantially darker than the preserved target PNGs. The same dark courier outfit already appears in capture04's raw-original A image before the head correction. The correction is therefore not a sufficient explanation for the outfit difference.

Source run: `20261026T114839701Z-4992eb60`, native receipt `REBUILT_NOT_VISUALLY_APPROVED`, original source/meta/memory/global preservation true. Further pixel and lighting comparisons follow below.

## What the images show

All six retain the original recognizable silhouette, accessories, badge and clothing. Faces now shade as cream rather than flat gray; visible hair becomes dark brown. No clipped body or missing accessory was found in these front portraits. This does not prove animation, rear/side views or new model detail.

The clothes are too dim for the requested vivid shop presentation: Sunny-side Club reads as ochre, Mint Messenger as subdued green, Sugar Rush as dusty pink, Midnight Arcade as dark grape, Lantern Walker as brown-orange and Holly Housemate as dark red. The old portraits were more immediately colorful, but several channels were also clipped; copying their brightness blindly would erase detail.

Fixed torso rectangle x92–163/y185–189, mean byte RGB (same framing; diagnostic pixels, not semantic segmentation):

| Look | Preserved old PNG | Rebuilt PNG |
| --- | --- | --- |
| Sunny-side Club | 255 / 252 / 92 | 158 / 127 / 50 |
| Mint Messenger | 175 / 250 / 203 | 92 / 141 / 111 |
| Sugar Rush | 255 / 174 / 224 | 160 / 93 / 121 |
| Midnight Arcade | 183 / 122 / 225 | 91 / 62 / 115 |
| Lantern Walker | 253 / 182 / 83 | 161 / 97 / 48 |
| Holly Housemate | 251 / 110 / 88 | 151 / 60 / 50 |

Read-only PNG decoding and exact samples are saved in `pixel-comparison-20261026.json`; its script only reads images and writes numerical evidence. No edited image was produced.

## Cause boundaries

- **The head fix did not darken the clothes.** Rebuilt courier and capture04 raw-original A have **zero changed pixels** in the complete y150–319 body region:43,520 pixels, maximum channel difference0. The same region is identical to corrected H. Across the entire image, current courier differs from H at only107 pixels. The dim clothing therefore predates the saved correction and is reproducible in the original source path.
- **Current export and lights are internally consistent.** All six are Linear-project RGB24/R8G8B8_SRGB outputs with identical chosen light parameters before Begin, after Begin and after Render. SSAO keyword is false before/after every render. These observations weaken a global transfer change or lost-preview-light explanation for this rebuild; they are not a render-time per-pixel SSAO trace. The old targets have no matched renderer/lighting receipt found in the bounded BuildNotes/ArtSource hash search, so their original brightness cannot be assigned to one proven cause.
- **The current preview preset is dim.** The key light color is(0.769,0.769,0.769) at1.15; the rear/side fill is(0.28,0.28,0.315) at0.65. BeginStaticPreview preserves those values. A neutral front/fill comparison is warranted before changing assets.
- **Source colors remain intentionally colorful.** Wardrobe entries include mint(0.549,0.851,0.659), sunflower(0.976,0.788,0.247), bubblegum(0.961,0.549,0.722), grape(0.549,0.353,0.702), tangerine(0.969,0.58,0.227) and tomato(0.91,0.329,0.247). Dress sends the values directly to MaterialPropertyBlock.SetColor; it does not explicitly linearize them twice. Unity documents sRGB input and linear conversion in a Linear project for [SetColor](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/MaterialPropertyBlock.SetColor.html). This supports retaining the source colors; it does not prove the desired final lighting.

## Actual Shop composition and alpha

`lobby-capture-2026-10-26T11-54-47-483Z/lobby_5_shop.png` visibly shows six cream rectangles within mint image panels. This is a real compositing defect. The native receipt says RGB24, and the [Unity 6000.6 utility source](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.6/Editor/Mono/Inspector/PreviewRenderUtility.cs) explicitly converts its render target to sRGB then copies RGB24 in EndStaticPreview. Background alpha0 alone never established transparency.

The proposed diagnostic reads native alpha from the same render target using an alpha-capable sRGB output target and RGBA32 readback. It must show zero-alpha border/background and opaque foreground, and its RGB channels must match the utility RGB image within2/255. No background color key, material recolor or texture repaint is permitted. If this fails, retain both outputs and inspect the target/transfer path before adopting it.

## Next bounded comparison

`lighting-comparison-draft/` adds a nonpublishing `ShopPreviewImporter.CompareLighting` entry. Three courier views keep the corrected production head, original materials and wardrobe, sampled pose, camera, ambient, dimensions and cleanup checks:

1. A: exact current preset.
2. B: neutral white key/fill facing the subject, levels1.15/0.65.
3. C: same neutral directions, levels1.55/0.85.

A→B compares a lighting preset (color and direction both change); B→C isolates intensity. Each frame also captures native RGBA for the transparency check, without extra scene renders. Existing six PNGs/metas are added to preservation proofs and never published by this method. Select a candidate only if the cream face remains readable, mint cloth looks vivid, hair/black accessories keep texture, whites stay distinct and bright channels avoid broad clipping. Then check that preset on all six looks and the actual shop layout. None of the rebuilt portraits is declared final vivid art or owner-approved here.

## Native A/B/C follow-up

Root executed the reviewed diagnostic at `shop-portraits-corrected-lighting/20261026T121120648Z-cceec7e1`; this reviewer opened all three actual RGB images and read the receipt. Native RGBA now passes: R8G8B8A8_SRGB,47,421 transparent pixels,34,499 opaque pixels,0 partial-alpha pixels, zero nontransparent border pixels, RGB maximum channel difference1, no owned leaks and preserved sources/targets/global state. A's RGB SHA matches the existing courier baseline exactly. Native readback succeeds; actual card compositing is still a separate check.

B restores the mint clothing's visual energy, but its cream face loses highlight gradation and becomes largely white; C clips the face more severely. Neither should be published. A further two-view diagnostic at the same neutral directions and ambient, with key/fill0.8/0.4 and0.7/0.35, is prepared in `lighting-soft-draft/`. This changes intensities only; it does not repaint the cream skin or mint fabric. Original A/B/C evidence remains preserved.
