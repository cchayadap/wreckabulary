# Preview lighting actual comparison

STATUS: COMPLETE - three current full original images inspected; not accepted for import.

Opened all3 actual960x540 PNGs from map-previews/captures/20261028T212401Z-bc144015: ParcelService.png, LostAddress.png, ParcelHall.png. Compared visually against the previously opened same-pose205930 originals. This is the key-Soft/fill0.3 candidate; the parent reports all six preservation checks true/no errors.

- Parcel Service gains useful contact shadows around desks/chest and a clear doorway shadow across the floor, improving spatial depth. The existing red desks/gold hardware remain identifiable, but the foreground/right wall are darker.
- Lost Address loses much of its original pale-timber/green-sofa brightness in the shadowed area. The bright diagonal path and darker room now separate strongly, but the furniture feels more muted.
- Parcel Hall is mostly in shadow and uniformly dimmer. Table/stool construction and floor contact are visible, but the pale timber reads duller; this conflicts with the owner's vivid original-style goal.

Recommendation: compare fill0.5 with key Soft retained, keeping every other light/camera/ambient/material setting fixed. Do not import0.3 as the accepted common preview treatment. A brighter fill is a bounded experiment, not a guaranteed fix; if the shadowed furniture remains too dim, report that rather than changing global exposure or pretending the composition is finished.

No camera, image pixels, source or saved asset changed by this review. No performance/gameplay/visual acceptance claim. Parent owns any revised candidate and native capture.

## Fill0.5 actual review

Opened all3 full original images from213235Z-e5b91064. Parcel retains useful desk/chest contact and doorway-floor shadows. The old maps remain markedly darker than the no-shadow reference; Parcel Hall timber is somewhat brighter than0.3 but still muted. Lost Address's dark sofa and shadowed foreground tables still have weak colour presence. This0.5 result does not yet establish the desired vivid common treatment. Root's proposed original0.7 fill plus Soft key is the next controlled comparison; no import recommendation is made until its actual images are opened.

## Final0.7+Soft comparison selection

Opened all3 full original960x540 images from213535Z-e1903b65: ParcelService, LostAddress and ParcelHall. The original0.7 fill with the Soft key gives the most readable Parcel Hall timber of the three shadow candidates. Desk/chest and doorway contact shadows remain visible in Parcel Service, with warm red/wood/gold accents legible. Lost Address still has weak composition and a small dark sofa in the distance; lighting has not solved that design problem. None of these images establishes finished room richness or owner acceptance.

Root selected0.7+Soft with no further lighting iteration. This source change retains the original fill/ambient/camera settings and adds key shadows only. The final live stage hash is9835eb7a90f4367263ad23919b569533a484eab2b2e1412bf2429e4072886f5b. Candidate0.3/0.5 source/captures remain preserved as comparisons. No final imported-image pixel edit was performed by this lane.

The selected213535 receipt has no errors and all six preservation booleans true. Its Parcel Service seed/mode/source/manifest/geometry/catalogue/pose match the corrected-course before205623Z-fcb77310. These are the exact Before/Current viewer inputs, now described as wall detail and lighting rather than a fixture-only change. Root owns import/build; final Windows captures remain necessary.
