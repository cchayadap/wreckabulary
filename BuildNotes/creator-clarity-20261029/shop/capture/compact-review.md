# Compact Shop visual review

STATUS: COMPLETE — all seven untouched 854×480 images opened directly with view_image. Visual issues remain; not final aesthetic approval.

Evidence: run `20261028T224340103Z-9176dda5`, native capture gate224315. Receipt SHA256 `19382b1017e299aec2f178e04d66662c6f138ea4327f633f186555a62089ebdd`; capture source SHA256 `d028739ab792ab83635d1171cfaa2a84ba3a4cdb4b854c8970295b5870b8878a`, frozen capture manifest `13302a09b4212b498e825d298ec083156a85f78b9f7b0d6d63a755d0eeedb67e`. Receipt records Unity6000.6.4f1, all preferences/files/explicit globals restored, zero cleanup errors, focused=true for each compact frame. Its status remains CAPTURED_NOT_VISUALLY_APPROVED. Each image SHA below was independently checked against its exact bytes.

## Observations by original frame

- **01 looks-default:** all six outfit tiles fit, selected cyan outline and Owned/Preview footer are visible; full avatar is unobstructed. Small tabs and footer metadata are difficult to read at the native compact resolution.
- **03 looks-equipped:** Sunny-side card and footer both change to EQUIPPED; no panel/content overlap. Tiny confirmation sentence adds little readable information.
- **05 looks-locked-preview:** Sugar Rush character colours visibly preview while Sunny-side remains marked EQUIPPED. Separate preview/ownership states are visible. BUY400 is offered with zero coins; insufficient-funds response is outside this visual evidence.
- **07 gear-shield:** selected Arcade card and price are visible, and gear stage appears beside the avatar. **Product identity mismatch:** circular, ornate shield thumbnails do not match the actual kite-shaped original shield with a star. The selected preview is pale blue/gold; this image cannot demonstrate that its finish matches the advertised card. Replace misleading imagery using the actual original model/finish or explicitly correct the finish binding after source diagnosis. Do not call these assets coherent yet. The Winter tile is cut at the scroll viewport, hiding its lower action; scrollability may allow access, but this still image provides no clear scroll cue.
- **09 colours:** six swatches/prices fit, Sky selection changes the avatar torso to blue, gear stage has cleared. Clearer visual relationship than gear. Metadata remains too small.
- **11 rooms-preview:** selected Candy Carnival thumbnail matches the changed background; Sunroom stays EQUIPPED, selected Candy shows BUY300. All four tiles fit and the avatar remains clearly visible. These are lobby room backdrops, not evidence of playable map detail.
- **13 extras:** accessory states use concise Equip/Remove labels and matching icons; gear preview and room preview are cleared, original Sunroom returns. Move/Shuffle/Stop is visible. Large empty lower region and very small Free accessories text remain; not a functional blocker in this frame.

## Priorities

1. Correct gear thumbnail-to-model/finish identity before claiming visual coherence. This is the strongest confirmed defect in the compact set.
2. Set practical minimum type sizes/weights for compact tabs, ownership/state and confirmation text; the current condensed heavy headings plus tiny metadata do not fully meet the requested cleaner typography.
3. Provide an evident scroll affordance or compact gear layout so the clipped Winter action is discoverable. Keep the selected-item footer clear as it is now.

No borders/footers visibly overlap content in the other six frames. The Shop has clear category grouping, cyan selection, price/equip hierarchy and persistent character preview. The richly illustrated lobby and understated 3D character/item still differ substantially in detail. This report does not accept that stylistic gap on the owner's behalf.

## Exact reviewed roster

| Image | SHA256 |
|---|---|
| 01-looks-default-854x480.png | `b10f95299c33a1928235e4dbcbda7462cb89ea8456ddbfde6b0e0e672a7f5094` |
| 03-looks-equipped-854x480.png | `6355913afdf7230cd1461aa635db45f6be0ceca50c59ab23e6b58df66f621114` |
| 05-looks-locked-preview-854x480.png | `3ac958c6f78e83e70f6a093b0cda6ed193afdddb193070f8d64d3f13e4748147` |
| 07-gear-shield-854x480.png | `2f68c2cad677ede8aec19765897b8b5e89f97e0b494996ce772a1c5a05708de0` |
| 09-colours-854x480.png | `2e0f1c6828f4be8a727feb6b503254a51ac6da200e3be810e58a0a50d79fca1c` |
| 11-rooms-preview-854x480.png | `a8a558434102d11f1020972992a6e217f3bc8bc82407414727788ff5ab83f7c1` |
| 13-extras-854x480.png | `45c9ae4cab0a8139f4d02056efcda0e3e6beb4a6ce8b4fb1e1c5ab7b4d1bca27` |

## Captured input source hashes

The full exact asset/source roster is in the immutable receipt identified above; relevant Shop code entries follow if present.

| Captured source | SHA256 |
|---|---|
| Assets/_Project/Scripts/UI/Lobby/LobbyShopOfferView.cs | `b9ec6df6a11e28662e57bdcfe8faf805894e272482c1d87bfeec66747cc89451` |
| Assets/_Project/Scripts/UI/Lobby/LobbyShopPageView.cs | `14722ecb9b1a83773992e4d531f8744c1ac677327622a56306119cee800183da` |
| Assets/_Project/Scripts/UI/Lobby/ShopPage.cs | `de645c1a2e5cbbf3c19ef4e528b2a94de59e2da02bf1e5a1575af0cb8ec16ced` |

Limits: this is isolated native GameView evidence at854×480 with a synthetic zero-coin profile and normal UI handlers. It is not Windows-player, physical-input, purchase persistence, held-equipment animation, multiplayer or whole-goal acceptance. Root reviews the seven1280×720 originals separately. No production assets, source, screenshots or receipts were changed by this review.
