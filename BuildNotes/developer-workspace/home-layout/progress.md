# Saved Home layout draft

STATUS: SOURCE DRAFT READY — 2026-10-26. Owner: collection26. Draft-only work outside Assets while root runs Loadout/camera validation. No native jobs, live shared-source changes, metadata, packages or commit.

Task: identify the smallest authoritative saved Home page/profile adapter, preserve its current game/party/preview controller behavior, retain concise existing UI, and propose meaningful binding/persistence checks. Do not expand into online menus or wardrobe internals. Root owns integration and verification.

## Baseline

Checkout: `C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration`; branch `sethypagna/wordbound-integration`; HEAD `8304fa9375688952faffa64ee0ecb1f5327e8961`.

| Source | SHA256 |
| --- | --- |
| `Assets/_Project/Scripts/UI/Lobby/LobbyPages.cs` | `1578593df65293cd56b81cb45afa1d316db4852797a485a3ec1d618c20b39da6` |
| `Assets/_Project/Scripts/UI/Lobby/LobbyMenu.cs` | `c302216371fc4f16d1133649aa055c050cd086c4f4455ac06467e1487dbb94a8` |
| `Assets/_Project/Scripts/UI/Lobby/LobbyKit.cs` | `bef8b2c3618158e542b920f4c21a316bbdc35d1f1a9d757fbea77572f3064e4c` |
| `Assets/_Project/Scripts/UI/Lobby/LobbyRuntimeTypography.cs` | `4cdcca15e359b80b0e16ad68cd8fd214c4e75e3b0a4d4b7585cee3b38927fdce` |
| `Assets/_Project/Editor/Authoring/LobbyShopLayoutSetup.cs` | `9c323bb5d3c884c4b68d903203f2bf22fcd5bc62d64e53acd88772a7e178f275` |

HomePage currently owns a turn hint and a small next-match dock containing Start, mode, optional house and optional Wordbound entry. LobbyMenu owns the separate shared top bar, party controls/feed, preview/avatar state and launching. The proposed boundary must keep those shared owners intact.

## Work

- [x] Read current HomePage and capture source identities.
- [x] One shared default factory supports missing-only setup and missing-profile compatibility, per root's steering; assigned malformed profiles fail. Only the Home controller's Build body changes.
- [x] Prepared saved profile/view and missing-only setup. Reuse the existing Shop persistence implementation through one `internal` visibility change; no duplicate texture code.
- [x] Prepared five Editor cases and two runtime cases for authoring/personal-state preservation, binding/visibility, party focus, hint and countdown cancellation.
- [x] Independent read-only review by story26: no concrete source blocker found in all eight proposed files and two baseline diffs. Factory/fallback, typography ownership, visibility/callbacks and cleanup inspected; native rendering remains unverified.
- [x] Patch application check with strict whitespace PASS; eight-file hash manifest and two exact baselines preserved.
- [ ] Root source/native checks and visual/interaction acceptance; NOT-RUN here.

The proposed static labels and geometry remain editable. Mode/house labels, availability, stable focus names and callbacks remain controller-owned. Home's shared top bar, party/feed and preview stay outside this slice. No online acceptance is implied; the existing legacy matchmaking gate remains unchanged.

Frozen patch SHA256: `07b0b96c53ff6cdb73c67ae2fafcc4814cab865428f226207e7dd7ac0b161f57`.

Next recoverable action: root reviews/applies the eight-file patch after its current native slice, runs its source gate, exercises compatibility without the optional Home profile, explicitly generates missing Home assets, then runs focused saved-view fixtures and existing Home regressions. Root may wire workspace shortcuts/setup separately. No commit or native PASS is claimed by this lane.
