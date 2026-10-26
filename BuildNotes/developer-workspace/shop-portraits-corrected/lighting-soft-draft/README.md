# Softer neutral portrait lighting comparison

STATUS: DRAFT — root's native A/B/C run passed its alpha/transfer/preservation checks. All three RGB images were opened and visually inspected by this author: B improves mint clothing but clips the cream face; C clips it further. Neither is selected for production.

This one-file follow-up changes only the diagnostic invocation and its preset levels. It runs D0.8/0.4 and E0.7/0.35 with the same neutral white lights, camera-relative directions, ambient0.45, corrected head, original materials/tints, Idle, framing and native RGBA readback. It preserves existing A/B/C artifacts by using the already unique run folder and new D/E filenames. No shop PNGs, importer settings or source art are published or modified. The Rebuild path remains unchanged.

Root gate entry remains `Wreckabulary.EditorTools.ShopPreviewImporter.CompareLighting`. Require two clean RGB/RGBA pairs, alphaVerified=true, RGB delta≤2, original/current-target/global preservation and zero owned leaks. Compare face gradients, hair detail, mint hue, badge/cuffs and black accessories against prior A/B/C. Do not choose a lighting level solely by brightness; reject broad clipped face highlights or excessive flattening.

Prior native evidence: `BuildNotes/developer-workspace/shop-portraits-corrected-lighting/20261026T121120648Z-cceec7e1`. All three report R8G8B8A8_SRGB,47,421 transparent pixels,34,499 opaque pixels,0 partial-alpha pixels and RGB maximum delta1. A's RGB SHA exactly matches the published baseline courier. This proves that run's native readback/transfer; actual mint-card compositing remains a required later check.
