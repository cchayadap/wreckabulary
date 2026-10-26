# Shop portraits: bounded visual acceptance

STATUS: AGENT VISUAL CHECK PASS — all six final PNGs and the actual1600×900 Shop screenshot were opened and independently inspected. No further change is recommended for this bounded portrait fix. Human style acceptance and Windows/player verification remain separate.

Final production evidence: `20261026T122401995Z-906b87b0/receipt.json`. Selected lighting is D, neutral front key0.8/fill0.4. Every look retains a shaded cream face, readable eyes/glasses and badge, and distinct original clothing: golden yellow, mint green, pink, grape, orange and red. Hair/black accessories remain dark without merging into the face. The broad white face highlights seen in B/C are absent. Hats, cuffs, straps and boots fit the frames.

Actual UI evidence: `BuildNotes/developer-workspace/lobby-capture-2026-10-26T12-24-26-146Z/lobby_5_shop.png`. All six former cream rectangles are gone; the mint card panel continues around each silhouette. No obvious cream fringe, dark halo, missing silhouette section or stretching is visible at this captured UI size. Card labels, prices and equip controls remain readable. This is a rendered-state check, not a new interaction test or proof at every resolution.

Screenshot SHA-256: `bef9b81816bdbd5a0ed16ba54920dfa1a943d6e9c5e1d85edcbb49cef8dd106e`. Production receipt SHA-256: `a18137e14a91bae2ffbc5ed7e9e3430d49ca4ce246bbce5942ebdb4e6ea7b857`.

Receipt checks independently read: six installed targets, six unchanged GUIDs and staged/installed PNG hash matches; every imported texture256×320, R8G8B8A8_SRGB, alpha-bearing and uncompressed. Every native alpha check passed with RGB maximum channel difference1, zero nontransparent border pixels and both transparent background/opaque foreground. All392 source file/meta records and92 memory records are unchanged; global state is preserved; all six previews closed with zero owned objects remaining. RGB reference images and prior PNG/meta backups remain in the versioned evidence.

No original textures, material colors, clothing geometry or further meshes were changed to obtain this result. This fixes thumbnail lighting and transparency; it does not establish new model detail, animation quality, broad art completion or multiplayer readiness. Continue the Windows build and its existing validation without additional portrait changes unless that build reveals a concrete regression.
