# Original Kitchen models — native inspection

STATUS: COMPLETE for source inspection only. Art acceptance, fitted bodies and full-course traversal remain pending.

Gate `20261029T003920Z` PASS in54.1s, Unity6000.6.4f1, normal exit. R0 `003812` PASS. Rules unchanged since preceding full core PASS. [Native receipt](authoring/native/inspections/20261029T003958Z-8295b6b6/inspection.json) records exact originals, dependencies, materials, dimensions and nine unchanged512×512 PNGs. Root opened all9images directly. Source files/dependencies and open scene state were preserved.

| Original | Native X / Y / Z metres | Observed details |
| --- | --- | --- |
| Fridge | .8000002 /1.82 /.799000144 | Ivory body and doors, gold handles, three small coloured star magnets. Front is +Z; rear is plain. |
| Counter |1.27 /.9250002 /.7370001 | Blue cabinet, lighter blue doors, ceramic top and gold handles. Front is +Z. Native minimumY=.009999871. |
| Stove |.820000052 /.942000151 /.7795001 | Ivory body, dark window surround, metal cooktop, gold knobs/handle. Front is +Z. |

All3 originals have one renderer, no collider, Rigidbody or Animator. Original source bounds are valid; physics must come from the canonical gameplay body. Keep existing Fit's floor alignment; do not use raw pivots. Canonical dimensions round to millimetres, including Stove depth780mm.

The north-wall run therefore needs yaw180 so the fronts face south into the room. This corrects provisional yaw0 before placement acceptance. Source inspection lighting is diagnostic; the plain back/side views are not room-composition approval. Fridge was incorrectly described as blue in the earlier proposal; actual material assignment and images establish ivory.

Next: native fitted-collider/contact/holding checks and matched same-camera room views. The full34-room route remains unchanged in the variant proposal.
