# Portrait lighting and native alpha diagnostic review

STATUS: SOURCE REVIEW COMPLETE — no native execution or image acceptance claimed. Reviewer: `story_status`.

Reviewed the one-file [lighting comparison draft](../developer-workspace/shop-portraits-corrected/lighting-comparison-draft/lighting-comparison.patch), SHA-256 `fd38e18cb05983ab897c14235dfcbae74574d35dd797d1d4cb54750692f02b07`; candidate `ShopPreviewImporter.cs` SHA-256 `d9c41545597caf954ccea71c3d54482d71c0250dddc7ab77eaf58402c9c82d67`. Both match the manifest. CSharp9 parsing with Editor/QA/test symbols has zero syntax diagnostics, and strict forward patch checking passes against the live source at review time. This does not prove Unity semantic compilation.

No blocking source finding was identified for the nonpublishing `CompareLighting` entry.

- All three courier renders use the same saved exact head correction, original outfit/materials, Idle sample, framing and camera. A to B changes the lighting preset; B to C changes only light intensities. The receipt labels that distinction.
- The diagnostic writes only a fresh evidence directory. Existing six shop PNGs and metadata join the before/after source proofs; no target importer is saved. The original publication path does not request RGBA when `control` is null.
- RGBA is read from the actual native preview target into an explicitly alpha-capable sRGB target. No colour key or image recolouring is performed. The helper restores active render target and `GL.sRGBWrite`, destroys a failed temporary copy, releases/destroys its owned render target, and transfers ownership of a successful copy to the outer capture cleanup.
- The capture retains the existing original/source/global/preview cleanup proofs, records native/output formats and alpha counts, and checks every RGB pixel against the same frame's RGB24 static-preview output. A different orientation or incorrect colour transfer should fail the maximum byte-delta check rather than silently publish a changed image.
- The resulting RGBA still requires a native alpha-bearing format, transparent borders, substantial transparent and opaque regions, an opaque sample inside the expected courier, and maximum RGB delta of two bytes. Those predicates are assertions to test, not results already established by this review. A failed predicate retains diagnostic output and does not change shop portraits.

Before adopting the result, inspect RGB/RGBA pairs and all recorded preservation flags. Transparent borders and an opaque centre do not establish correct antialiased edge compositing: inspect hair, cap/hood, glasses and hand edges on the actual mint card background for a cream fringe. A native gamma/alpha mismatch should be diagnosed, not addressed by relaxing the checks without evidence. These three courier samples do not replace all six outfit reviews, actual Shop recapture or Windows verification.

## Softer-light follow-up

Reviewed [lighting-soft.patch](../developer-workspace/shop-portraits-corrected/lighting-soft-draft/lighting-soft.patch), SHA-256 `977c7151768c33b7fb66ec22ff529d36069ab9f72a9b63de2a4b070f379555ca`, candidate `4d64fd15775d5c17703e4951472d55642c0bf8496953aee08c6425a108eec459`. The only functional changes select two nonpublishing D/E controls with key/fill intensities 0.8/0.4 and 0.7/0.35. Neutral light directions, white colour, ambient, source/pose/framing, native readback and preservation are unchanged; the existing six-portrait publication path is unchanged. Scope and log count accurately describe two comparisons. Old A/B/C evidence is not overwritten.

No blocking source finding. Candidate and patch hashes match, CSharp9 Editor/QA/test syntax has zero errors. Root integrated during review: live equals the candidate ignoring line endings and strict reverse patch checking passes. Root reports prior A/B/C native success and excessive bright-face appearance; this reviewer has not independently inspected those result images here. D/E native output, bright-area detail and actual Shop compositing remain separate acceptance steps.
