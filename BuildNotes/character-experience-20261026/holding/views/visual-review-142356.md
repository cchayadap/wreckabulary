# Multi-angle diagnostic image review — 142356

STATUS: REJECTED AS POSE-CLEARANCE EVIDENCE — all 18 images viewed; diagnostic skin/weapon pose mismatch found.

Evidence: captures/20261026T142356007Z-b1ab8619. The native run remains FAIL for the strict framing predicate. All 18 image hashes independently match the receipt. All source/memory/scene/global/owned-resource/tactile cleanup flags are true; no input damage was reported.

Two distinct diagnostic problems exist:

1. Baked bounds used BakeMesh(false) followed by the full renderer transform, applying the fitted parent scale twice. The resulting head bounds exceed the visibly framed avatar. Revision5 addresses this coordinate contract without relaxing framing. None of this run's AABB flags establishes body clearance.
2. The rendered avatar holds substantially the same pose across early windup, late windup and active within each item, while the weapon and recorded hand transforms move. In BAT active front the bat floats over the torso away from the displayed right mitten; active side shows a large gap from both displayed mittens. HAMMER late-windup side similarly floats in front of the face with no displayed hand at its handle. Zero recorded socket distance therefore does not prove rendered contact in this diagnostic. The CPU skeleton/weapon advances while preview skin rendering appears stale; this is an inference from the combined image/transform evidence, not a proven engine cause.

Images reviewed: BAT and HAMMER, each early-windup / late-windup / active, each front / side / rear (all 18 exact filenames in receipt.json). Early poses show readable original shapes and apparent contact, but later poses invalidate the series as a synchronized-body comparison. Front/rear screen-space overlap near a cheek cannot be judged against a stale body pose.

Do not use this run to conclude that the actual Windows windup penetrates the head or safely clears it. Earlier upright Windows evidence has connected rendered hand/weapon poses and remains separate. First force fresh skin-matrix evaluation on owned preview renderers and verify the body follows each sampled pose; if that is unreliable, render clearly labeled owned static snapshots of the compensated baked skin meshes at those exact poses. Preserve originals and the failed evidence. Only after the render/measurement poses agree can the front/side/rear comparison distinguish occlusion from visible intersection.

No runtime animation or model correction is justified from this diagnostic failure alone. Owner visual acceptance remains pending.
