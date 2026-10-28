# Roboto migration handoff

STATUS: SOURCE CHECK PASSED. Native creation, preservation and visible review remain root-owned.

Official stable release: googlefonts/roboto-3-classic v3.016, commit 5166f3d07889bf7d3732fb72e09623d7e52f862b. Original hinted static faces and exact pinned OFL are at ArtSource/Fonts/Roboto/v3.016; provenance.json closes the four-file hash roster. The full archive matches the SHA256 published by GitHub. No converted or renamed font bytes.

## Integration

1. Hash-check manifest.json and copy its one candidate C# file to its exact new Assets path. Do not copy draft directories into Assets. Keep the source intake folder in place.
2. Compile in Unity, then execute Wreckabulary.EditorTools.LobbyRobotoTypographySetup.Apply (menu: Wreckabulary > Authoring > Apply Roboto Typography). Run only with saved assets and no affected prefab open in Prefab Mode.
3. This backs up seven saved UI prefabs plus the live profile, including exact original bytes and .meta files outside Assets, and editable AssetDatabase copies under Assets/_Project/UI/DesignArchive/BeforeRoboto20261029. The active profile GUID is retained; old Nunito/Lilita files are untouched.
4. New font source: Assets/_Project/Resources/Fonts/Roboto/v3.016. Editable TMP fonts/profile: Assets/_Project/UI/Fonts/Roboto/v3.016. Editable presets: Assets/_Project/UI/Materials/Roboto/v3.016. Active profile stays Resources/UI/LobbyTypography.asset. Exact OFL also goes to Assets/StreamingAssets/ThirdParty/Roboto/OFL.txt for player distribution.
5. API remains Body=Regular, Display=Medium, Black=Bold. Saved Title/Header roles use Medium; action labels and explicit bold roles use Bold; other labels use Regular. Body rich bold maps to the saved Bold face; Medium/Bold map bold to themselves. Fonts stay dynamic with owned runtime cloning unchanged. New foreground outlines are light and face dilation is zero.
6. Only objects named Panel receive the warm PanelTop/PanelBottom surface. Card gradients, colours, references and layout are retained. Play's ArrangeOptions() intentionally saves its new full-width layout; the component's serialized defaults are persisted by SaveAsPrefabAsset.

## Verification

Roslyn semantic compilation of current Wreckabulary and Wreckabulary.Editor with the candidate substitution passed with zero errors; source-check.json is the receipt. This is not native Unity or visual proof.

Root native gates: Apply once; reopen/reload saved assets and verify profile validity, three source faces, Body bold mapping, persistent atlases/materials; Apply again and compare asset bytes/GUIDs. Existing LobbyTypographyTests and LobbyRuntimeTypography tests still cover original setup compatibility/runtime clone preservation. Smoke Home, Play, Customize, Shop and Settings at compact and desktop sizes; inspect clipping, contrast, light emphasis, and full-width saved Play in Prefab Mode. Confirm player output contains exact ThirdParty/Roboto/OFL.txt. No old font, public map or online configuration change is intended.

If creation stops part-way, partial new assets are preserved and retry fails on occupied output folders. Review exact backups and restore deliberately; do not delete the archive or claim a completed migration. Once the active profile references the new roles, repeated Apply verifies and keeps authored font/material edits. It does not re-style a later designer edit.

The game-dev CLI was unavailable, so this lane does not claim package admission or ledger approval. Owner authorization covers this official OFL source intake; root owns project admission through Unity and required checks. No licence ledger was changed.
