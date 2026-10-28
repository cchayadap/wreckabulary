# Shop art-coherence peer review

STATUS: COMPLETE — no blocking source finding in the bounded five-file review. No production/native actions.

Exact frozen manifest SHA256: `13fb1df2d10daf6b831286d70cf8248159282fc701de5eab573bdab36ef43465`. All five candidate and baseline hashes matched the manifest. Reviewed the complete whitespace-normalized diff for each file and the actual OriginalItemArt, GeneratedItemArt, LobbyKit face/image and Shop preview/action contracts. The source-check receipt reports runtime182/Editor.Tests44/Tests70 sources, zero errors; that is source compilation, not native acceptance.

The common ItemArt lookup now calls the existing uppercase-ID OriginalItemArt cache first, returns full UVs for originals, then retains the generated sprite/atlas fallback. Empty/unknown IDs fail safely. Explicit generated and Wordbound-library APIs remain unchanged. Original textures are borrowed, not mutated or reimported. Removing the second cache does not remove subsystem cache reset: OriginalItemArt owns its reset. Canonical production recipe IDs use uppercase letters; the explicit lowercase generated fallback remains separately covered.

Finish cards render exactly one original image for the selected recipe, untinted and non-raycast. The former FinishDisc BAT overlay is removed only from Shop finish cards; other users of that helper are untouched. A separate small gradient swatch uses the existing finish-family palette and does not intercept pointer input. Actual selected-item stage preview still calls PreviewItem(recipe, offer.Value); this correction does not change purchase/equip handlers, ownership, prices, preferences or material application.

The five-file delta changes only the selected-footer source default and legacy fallback132→96, preserving explicit authored overrides and96–180 allowed range. This increases list space but is not proof that Winter's full action fits or that compact metadata is readable. New actual captures must establish that.

Tests meaningfully check six real competing original/generated textures, exact original paths/full UVs, unchanged explicit library access, atlas fallback, unknown IDs and two actual Shop recipe shelves. Shop assertions require one original image per card, a separate swatch, no raycast interference, retained stage/recipe and unchanged outfit/coins/preferences. Existing native layout/equip/purchase tests remain necessary; this source review ran none.

Honesty limit: every finish card intentionally shares the actual original silhouette, and the corner swatch is a generic finish-family cue. It is not a photographed/rendered per-item skin thumbnail. The tooltip directs players to the actual preview, but only fresh native frames can verify that the live finish matches its advertised name/materials. Do not describe the selected Arcade skin as visually validated by these source checks. Original-first selection intentionally affects common HUD/recipe callers for the six competing icons, so those contexts need focused visual regression checks too.

Next evidence: rerun Shop/art/layout gates and the same fourteen untouched capture frames; inspect actual kite-shaped SHIELD identity, no duplicate BAT, full Winter action/scroll affordance, footer fit and live finish differences. Compact type-size concerns from the preceding review remain open.

Also opened the untouched original Assets/_Project/Resources/UI/Items/SHIELD.png directly: its kite-shaped blue face, gold rim and star match the actual native preview silhouette, unlike the prior circular generated card art. This verifies source-art identity, not the selected finish's materials.
