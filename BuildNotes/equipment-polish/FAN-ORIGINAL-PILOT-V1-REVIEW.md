# Original-based FAN V1 — first actual comparison

STATUS: BUILD/RECEIPTS VERIFIED; FIRST REAR DETAIL REVIEW FINDS USEFUL CONSTRUCTION — not approved for runtime replacement

Root built `ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1` with Blender 5.2.2 LTS and rendered one original/candidate detail pair at 960 × 960, 64 samples and six CPU threads. This lane directly viewed both actual PNGs, checked their hashes and the saved blend hash, and verified that paired receipt fields match apart from variant, image hash and elapsed time. No Blender or additional rendering was run here.

| Artifact | Verified SHA-256 |
| --- | --- |
| Original FAN GLB | `67bd384322b83617ac801211fb487f6be4e57c9d712f1b294660c13957700cd7` |
| Editable pilot blend | `5e89db51071535ba3fa0aa8741121fe54f98ced6c5b048fdc3f62fa308e6f381` |
| Original detail PNG | `c82ca4d4e073109db2f94ded18d5423e2fa5ef309e3feb8fdf756b4366a8222d` |
| Candidate detail PNG | `af1ef9a40c7fb083fad389d4d82accded8f853403d3f97c4e0361b92bec02ec3` |

The receipt records source builder `b81bb6e9387a3bd247c63af9bd6b93676646e251dc139691b6a8039877193ff5` and common helper `5f1d81614050a55cc1be070b47deb804d728a2154e7ebf23accc535a2bf9c900`. Original and candidate overall bounds are identical: Blender metres `(-0.2610000074, -0.1250000298, 0)` to `(0.2610000074, 0.1250000298, 0.8209999800)`. Original grip and configured 0.4385 held scale are recorded. The candidate has 68 editable mesh parts and 16,006 triangles; these are source-pilot counts, not accepted delivery/runtime budgets. Render durations were 129.640 seconds original and 190.118 seconds candidate, not game performance measurements.

## Actual visual judgement

The candidate adds a visible powder-blue motor housing, recessed rear vent, rotor shaft connection, silver tilt axles/supports and rear guard. The motor and yoke now explain how the fan head is supported. Original rim, pale blades and blue/silver material language remain recognizable. This is a useful construction improvement in this close-up; no detached motor or support is visible from this angle. The result remains clean stylized artwork, not a photorealistic or owner-approved asset.

The camera is already on the rear side. Its target is `(0, +0.065, 0.56)` and its offset is `(+2.2, +3, +1.8)`. A direct read-only original GLB coordinate check places its front grille around Blender Y `-0.054` to `-0.07633`, with blades around `-0.01379` to `+0.01379`. The added motor is on positive Y. Because the original has no rear motor, its image shows the rotor/blades through the back and initially looked like a front crop. The completed candidate resolves that ambiguity: this pair does show the intended rear construction. **No camera change or replacement render is needed.** Root independently inspected the pair and reached the same correction.

This angle cannot establish the full silhouette, front blade/grille clearance, side neck/shaft contact or actual hand fit. The new mesh detail has not been exported, baked, imported or reviewed in Unity. Original Wreckabulary remains the default. Preserve this pair and editable source; further side/front/held views await root's later bounded review slot, with no full-set rollout or appearance approval implied.

Actual images: [original](../../ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1/Previews/FAN-detail-original.png), [candidate](../../ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1/Previews/FAN-detail-candidate.png). The [local comparison gallery](../../ArtSource/Collections/WordboundPvP/OriginalBasedPilotFANV1/PILOT-REVIEW.html) contains the same receipt-verified pair.
