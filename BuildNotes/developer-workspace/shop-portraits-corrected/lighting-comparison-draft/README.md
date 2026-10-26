# Nonpublishing portrait lighting and native alpha comparison

STATUS: SOURCE REVIEW PASS — independent review found no blocking source issue; hashes, C#9 syntax and strict forward patch checks passed. Native execution and visual acceptance remain pending. No live Assets changes or Unity jobs by the author.

Frozen patch SHA-256: `fd38e18cb05983ab897c14235dfcbae74574d35dd797d1d4cb54750692f02b07`. Candidate SHA-256: `d9c41545597caf954ccea71c3d54482d71c0250dddc7ab77eaf58402c9c82d67`.

The one-file patch applies after the six-portrait rebuild importer. It adds `Wreckabulary.EditorTools.ShopPreviewImporter.CompareLighting`, which renders only courier in three explicit lighting presets into a unique `BuildNotes/developer-workspace/shop-portraits-corrected-lighting/<UTC>-<id>/Rendered` folder. It does not call Rebuild, publish PNGs, change importer settings or save source assets. Rebuild keeps its existing lighting/RGB output path because its control field remains empty.

Each render emits the existing RGB24 PNG for matched lighting review plus an RGBA PNG from that same native frame. The preview target must have alpha; native Graphics.Blit writes an sRGB alpha-capable target, then RGBA32 ReadPixels retains alpha. Active render target and GL.sRGBWrite are restored, and temporary output RT/texture objects are released. Numerical alpha evidence requires over1,000 fully transparent pixels, over1,000 opaque pixels, zero nontransparent border pixels and an opaque known torso pixel. RGB channels must match the normal utility export within2/255; a mismatch is a failed transfer test, not permission to alter colors.

The existing source/file/memory/global checks and capture cleanup remain active. All six currently published PNG/meta files are also hashed and must remain unchanged. The original source material/texture, wardrobe tint, sampled Idle, camera, framing and ambient values remain fixed. A→B is an art-directed lighting-preset comparison; B→C changes only intensity. Full parameters are recorded before/after Begin/Render.

Root route: review/apply via normal gates, then `exec Wreckabulary.EditorTools.ShopPreviewImporter.CompareLighting --json` with current approved toolchain flags. Require `CAPTURED_NOT_VISUALLY_APPROVED`, three clean captures, source/global/current-target preservation, zero owned leaks and all three `alphaVerified=true`. Inspect all six diagnostic PNGs. Compare A RGB pixels against the already published courier (do not infer identity only from filenames). If native alpha/transfer fails, preserve the failure outputs and do not publish them. A later bounded adoption is required to change the six shop targets or production lighting.

Source rationale and the independent twelve-image review are in `../visual-review-20261026.md`. Prior utility output was RGB24; no existing transparent-output claim is made. Native alpha validity and visual acceptance are pending.

Review limit: alpha counts and RGB parity do not prove clean edge compositing. Before adoption, inspect partial-alpha hair, cap, glasses, cuffs and straps against the actual mint shop card background. Do not remove cream fringe by color key or repaint; a failed edge/readback result requires preserving evidence and fixing the native output path.
