# Practice selector independent review

STATUS: COMPLETE — SOURCE ONLY; NATIVE NOT RUN

Reviewed the two-file candidate: selector SHA256 `b4211e667a05254b11e594d83681c2441a45b9d9889990811f36692a987a4168`, fixture SHA256 `a625c8ff14f11f5f91a31775d3f321522530b986643dcb60cafeb8e72b73959d`.

No blocking behavior or fixture defect found. The selector retains the current course/mode/actor/manifest/range guards and filters range before sorting. It prioritizes unfinished stations, then actual distance, then ordinal hash, so unfinished co-located lap stations remain reachable. Completed stations remain a stable fallback only when every in-range station is complete. The 20-metre guide may now point toward a farther unfinished station while the 3-metre interaction selector still shows a nearby completed station; this follows their distinct existing ranges and is explicitly covered.

The added tests use real Parcel Service resolved site positions and stable identities while constructing read-model completion states. They exercise all three co-located SortingBench sites, completed fallback, strict range isolation, far unfinished preference and existing actor/source guards. They do not claim that their fabricated read-model mechanisms prove real authority progression; the separate native physical-play fixture is required for that evidence. No fixture bypass of the revised selector was found.
