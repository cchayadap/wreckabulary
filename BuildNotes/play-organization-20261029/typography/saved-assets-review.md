# Saved Roboto verification

STATUS: PASS for the bounded saved-asset review. No new blocking source or reference defect found. No Unity/native execution by this reviewer.

- Active profile GUID remains 0cc48b4c1e663824cb208ca6e8a467a0. Its complete .meta bytes equal the exact pre-migration backup.
- Profile Display/Body/Black references resolve to the saved Medium/Regular/Bold TMP assets. Regular weight-table entry 7 resolves to saved Bold GUID 95a0bbd2b09b82348aec50689cef986c; Medium and Bold entry 7 are local self references.
- Each font is dynamic, retains the expected imported source TTF GUID, and contains its own persistent atlas and base-material subassets. Base materials reference the same local atlas file ID.
- All five saved presets reference the correct role font's atlas. GradientScale=11 and 2048 texture dimensions match the generated fonts. The fallback GUID resolves to the existing LiberationSans SDF asset, and matches the original Nunito fallback.
- All four imported source files match the pinned official provenance hashes. StreamingAssets/ThirdParty/Roboto/OFL.txt matches the exact upstream OFL hash.
- Twelve original Lilita/Nunito source/TMP files and metadata match the supplied post-creation preservation baseline; git diff against checkpoint 811f08516db7b1d12029c669e62f4066d7992013 also reports no legacy-font changes. This independently supports that old fonts remain intact.
- Root's root-import-verification.json records the successful exact sixteen-file pre-migration backup check. This review inspected that receipt instead of duplicating the full project sweep.

Machine-readable bounded evidence: saved-assets-verification.json. Source skim by menu lane also found no new blocking defect after final persistent weight-table reassignment.

This verifies serialized references and bytes. Runtime glyph generation, fresh-domain loading, screen readability and Windows rendering remain covered by root's native/visual gates; these static checks are not a claim of visual acceptance.
