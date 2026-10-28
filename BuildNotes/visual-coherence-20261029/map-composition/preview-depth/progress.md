# Map preview depth comparison

STATUS: FROZEN SOURCE CHECKED; independent review and native comparison pending.

- [x] Opened actual Windows211338 Time Run world and compared with previously reviewed static205930.
- [x] Inspected installed Preview capture/cleanup and local URP shadow gates, plus official Unity PreviewRenderUtility source.
- [x] One-file candidate: key soft shadows, fill0.7→0.3; camera/ambient/global tone/materials unchanged.
- [x] Semantic source check: Authoring.Editor24sources/314references, zero errors.
- [x] Baseline/candidate/patch/manifest frozen; live baseline hash verified.
- [~] Map lane independent bounded source review requested.
- [ ] Root-owned native matched three-map rendering and quality review; no acceptance claimed.

No live source, native job, saved asset, image pixels or current viewer was modified by this lane.

## Root comparison212401

Root opened all three actual images from capture20261028T212401Z-bc144015. Soft shadows ground the Parcel desks/chest and door framing, but fill0.3 darkens the Lost Address sofa and Parcel Hall wood noticeably. Independent image review agrees. This first lighting candidate is retained and not imported. Root changes only the owned fill to0.5 for a second comparison, retaining Soft key, camera, ambient and all content. No final image selection yet.

Independent source review completed by map lane with no blocker. Root-owned actual212401 three-map capture reviewed separately in render-review.md: contact shadows improve Parcel depth, but fill0.3 is too dim for the old maps. Recommended matched0.5 fill comparison; do not label0.3 visually accepted or import it as final. Frozen0.3 source and receipts remain preserved as the tested candidate.

## Root second comparison213235

The0.5-fill capture213235Z-e5b91064 passes with all six preservation flags true and no errors. Root opened all three images: Parcel retains useful desk/door shadows, Parcel Hall wood is brighter than0.3, but old-map shaded areas remain subdued. No image imported. The last comparison retains the original0.7 fill and changes only key shadows to Soft; this tests whether depth can improve without reducing the original fill. R0213450 passes. All three candidate receipts/images remain separate and unedited.

## Root final rendering selection213535

Root opened all three unchanged PNGs from213535Z-e1903b65 and selects this bounded candidate: original fill0.7 retained, key shadows Soft. ParcelHall timber is brighter than the0.3/0.5 attempts while Parcel desks/chest and doorway retain visible contact/occlusion depth. Lost Address still has weak sparse composition and a distant shaded sofa; lighting does not repair that content issue. All six native preservation flags are true and errors empty. Source stage SHA2569835eb7a90f4367263ad23919b569533a484eab2b2e1412bf2429e4072886f5b. Exact import follows with current-content guards and backups. This is a source/rendering selection, not owner approval of map art.
