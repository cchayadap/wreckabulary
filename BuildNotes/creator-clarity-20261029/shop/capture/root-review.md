# Shop capture review

STATUS: REVIEWED WITH ART CORRECTION REQUIRED. Native execution224315 passed1/1 and produced14 original PNGs. Hashes/roster match receipt.json; preferences, globals and bounded source inputs restored, zero cleanup errors.

Root opened all seven1280×720 originals from runs/20261028T224340103Z-9176dda5: 00 default Looks,02 equipped Looks,04 locked preview,06 SHIELD Gear,08 Colours,10 Rooms and12 Extras. The separate reviewer opened all seven854×480 counterparts. These are actual native captures from a temporary zero-coin profile, not physical-input or Windows evidence.

The selected footer is outside the scrolling cards, readable and changes from owned preview/EQUIP to EQUIPPED after the explicit action. Locked Sugar Rush previews on the character while Sunny-side Club retains its equipped check. Sky previews the top colour, Candy Carnival previews the corresponding room, and Extras restores the committed yellow outfit and original Sunroom. No clipping of the selected footer or missing character was observed in these seven views.

Confirmed mismatch: Gear cards show ornate round SHIELD artwork while the stage presents the original kite shield with a star. Source inspection identifies LobbyKit.ItemImage → ItemArt.TryGet preferring GeneratedItemArt over the installed original UI/Items/SHIELD.png. The original PNG was opened and matches the actual kite silhouette. FinishDisc also embeds a BAT under the recipe image. This confuses what is being previewed; prefer the matching original item image and a separate restrained finish indication, preserving explicitly selected generated collections.

Remaining visual limits: the lower Winter Gear card action reaches the scroll boundary and there is little scroll affordance. Extras retains considerable unused space. Colours are circles rather than actual cloth thumbnails. Small-window secondary labels need the compact review's readability assessment. The rich illustrated lobby still overpromises the detail of the playable maps. No owner style acceptance or production-ready claim follows from these technical passes.
