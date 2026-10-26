# Independent responsive navigation fixture review

STATUS: SOURCE REVIEW COMPLETE — no source blocker for native execution. Reviewer `/root/asset_intake`; no native or live changes.

Patch `a4b47416c7e30a45d9593a1633196d826b0e628b0432b65edd4f7f9136a9c906`; candidate `62c2c58699b9c54f4cb665aa463b48a076abdcbf322d993e035fb8f120e9c4ce`. Hashes match manifest; live fixture already existed during review and normalizes exactly to candidate. Independent C#9 parse: zero errors. A forward apply check correctly reports the already-existing live test; that is not an unapplied source conflict.

The three tests inspect the actual LayoutBar output at wide, compact and both threshold-adjacent widths, returning to wide to catch stale layout. They require all nine real navigation targets, nonoverlapping safe-area/bar rectangles, top raycast ownership, page/party/feed/countdown offsets, selected object identity, unchanged local mode/map/queue/party count and current visible labels. Shop coin text is restored in finally. The fixture snapshots and restores string-backed career/outfit/turn-hint preferences and live audio/V-sync, using the existing scene-test reset convention.

WithLayout operates synchronously with no yields: the owned canvas scaler is disabled temporarily, safe-area coordinates are fitted to the physical window, then its anchors/pivot/position/size, scale factor and enabled state are restored in finally and LayoutBar recomputed. It makes no Screen.SetResolution call and does not change scene assets or saved layout. Current root LayoutBar changes position only when crossing the compact breakpoint; anchors account for width changes within each region.

These are UI geometry/input-target tests. Text labels additionally check overflow, but rectangle containment does not prove every TMP glyph is rendered or readable; native screenshots remain required. They do not certify physical controller navigation beyond retaining the selected object, nor maps/online routes. No additional tests or runtime changes were requested by this review.
