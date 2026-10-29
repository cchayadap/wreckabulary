# Arena import preservation repair

STATUS: FROZEN DRAFT / SEMANTIC PASS / ROOT NATIVE PENDING.

The failed gate033835 did import and assign the correct immutable PNG. The assertion failed because `EditorJsonUtility.ToJson(layout)` wraps the fields inside `MonoBehaviour`, but `OtherFields` removed `CombatArenaPreview` from the outer object. The before-import manifest therefore still contains the intentional preview field, initially null. This is a guard implementation error, not evidence of a changed unrelated field.

The actual saved delta is six schema-default values (`104`, `1160`, `52`, `264`, `340`, `3`) plus the intended Sprite reference. All six defaults already appear in the before-import in-memory JSON; they were materialized when Unity saved the newly assigned field. Root's independent `native/20261029T033724Z-be0375ba/saved-delta-independent.json` proves4325/4326 original inputs unchanged, only the layout changed, removing exactly those seven lines reproduces the original saved lines, layout metadata is identical, image bytes are identical and the Sprite GUID matches. Keep all partial outputs and the exact backup.

## Exact reviewed candidate

`candidate/WordboundArenaPreviewImport.cs` replaces the existing importer file only. The original exact bytes are retained in `baseline/WordboundArenaPreviewImport.cs`. `manifest.json` pins both, the original receipt, before-import manifest, retained layout and image metadata. Manifest SHA256: `87358d84ae8f3465b06bbf4b39ad6b4c2993a24e7863259eb967ce0a5b2eb1b8`.

Candidate SHA256: `180106c8b7dc3286ee2c4b9c31aae41de748dd57ddacd6cf658eebf1fb1ae7ea`.

- Correct the regular `OtherFields` guard by removing the field inside the validated `MonoBehaviour` wrapper.
- Add `FinalizeRetainedImport`, bounded to this exact failed attempt. It verifies output/backup hashes, persistent Sprite assignment/GUID, six already-loaded defaults and exact original saved lines; it never writes, restores or resaves any asset.
- Reconstruct the captured source fingerprint by substituting only this importer's retained original hash. The live helper must equal the exact reviewed candidate; all other source changes fail freshness. Every original dependency hash remains required except the intentional layout and importer bytes; the current dependency roster may add only the reviewed PNG and its metadata.
- Recompute the actual installed course/catalogue/geometry identity and validate retained Sprite settings, clean dependencies and read-only scene/layout preservation. Write only `import-receipt.json` after every check passes. Repeats verify completion and return without writes, preserving later owner layout changes.

## Root execution

1. Independently verify `manifest.json` and the candidate hash above. Copy the exact candidate onto `Assets/_Project/Scripts/Online/Authoring/Editor/WordboundArenaPreviewImport.cs`, retaining its existing `.meta`. Do not change any other captured source or dependencies before verification.
2. Run the normal serial native gate exec for `Wreckabulary.Online.Editor.WordboundArenaPreviewImport.FinalizeRetainedImport`. This method needs no environment variable; its receipt path is pinned to this attempt. Do not rerun the original first-import path on the partial attempt.
3. Check `import-receipt.json` has `IMPORTED_REQUIRES_MENU_REVIEW`, `recoveredRetainedImport: true`, `otherFieldsPreserved: true`, the original capture hash and current layout/image GUID/hash. Run the same finalizer again if repeat/no-write evidence is needed.
4. Continue the Windows build and actual menu screenshot review. The sparse image faithfully depicts installed static Arena geometry/props; it does not establish gameplay or visual acceptance.

Source-only check passes with29 sources,314 references and zero errors in `source-check.json`. No child native run, Assets edit, Git mutation, metadata write or rollback occurred.

The failed original importer did not record its before-scene state. This repair proves its own scene preservation and explicitly records that the earlier scene state cannot be retrospectively claimed. Exact saved-asset preservation is independently proven without relying on cross-session EditorJson instance IDs.
