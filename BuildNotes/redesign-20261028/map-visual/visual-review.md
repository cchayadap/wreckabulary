# Map visual correction — 28 October 2026

STATUS: IN PROGRESS. This is an agent review and correction; James has not approved the final appearance.

Actual matched first-revision frames: `../map-capture/captures/20261028T151024Z-de46349f`. Root opened arrival baseline/styled, Kitchen styled and Attic styled. The map reviewer also reported repeated picture panels, gold trim and the existing upper-wall gap. These files remain unchanged.

Confirmed problems: the styled floor replaced the detailed plank texture with flat plaster; repeated oversized blue panels and townhouse pictures looked artificial; gold baseboard formed a decorative band. The large rooms still need designed furnishing, destination landmarks and route/pacing review. Passing collider tests did not establish visual quality.

Root correction: retain the canonical plank albedo, normal map and UV scale with editable palette tint; remove the gold baseboard; default wall insets to zero. Explicit material assignments remain authoritative. Existing Dispose cleanup remains intact. The Unity correction command backs up the saved room profile outside Resources, changes only the inset count once and preserves later edits on repeat.

Parcel Hall V2 has 18 usable furniture pieces, rather than the six in the first comparison. Its exact map ID and physical layout are versioned separately from public courses. The first images do not show V2.

Next: native material/collider/lifetime checks, fresh matched V2 images, then the new Windows build and real player captures. No claim of finished world design or owner acceptance.`nNative154226: PASS24/24. Fresh matched V2 capture154322: fourteen images, source/memory/scene/global preservation PASS. Root viewed arrival/Kitchen/Attic styled frames. Corrected material regressions, but the rooms remain sparse and generic; complete environment-design acceptance is open.
