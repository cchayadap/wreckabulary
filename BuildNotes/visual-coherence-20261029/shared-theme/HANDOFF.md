# Shared neutral interface — integration handoff

STATUS: FROZEN SOURCE-CHECKED DRAFT. Exact 13-file manifest and freeze-check.json saved. No live sources/assets changed, no native tests run by this lane.

The scope is palette propagation and required contrast fixes. Home/Shop layout redesign, map/world detail, workshop layout and owner acceptance remain open.

## Runtime contract

- `LobbyInterfaceTheme` lives at `Assets/_Project/Resources/UI/LobbyInterfaceTheme.asset`, resource key `UI/LobbyInterfaceTheme`. Its semantic `Colours` and Online Roboto TTF references are editable in Unity.
- `LobbyKit` resolves Panel/Card/Raised/Text via that resource. Existing Navy/Cream aliases now mean light interface text; fixed `ArtInk` retains the original dark ink for bright art and diagram backing. Wood colours, finish gradients, colour swatches, ModeColour and ItemImage pixel tints are preserved.
- New getters: `Card`, `Raised`, `Selection`, `OnSelection`, `PrimaryFill`, `PrimaryInk`, `Focus`, `ArtInk`.
- `LobbyInterfaceFrame` saves explicit Graphic-to-role bindings on the seven existing frame/card prefabs. It applies those roles at Awake or its Inspector context action. RawImage artwork cannot be bound. Selection visibility, callbacks, hierarchy geometry and source images are untouched. Reopen a page after editing the palette; no continuous editor retint/save loop is introduced.
- `WordboundUiTheme.SharedTheme` is optional, preserving legacy standalone themes. `WordboundUiStyles` resolves shared colours/fonts into an owned temporary clone at Layout, copies values to owned styles/textures, then destroys only the clone. It retains local spacing/backdrop settings and does not mutate the borrowed saved theme.
- Shared palette edits participate in the existing GUI fingerprint and Layout rebuild. Invalid palette data falls back through the existing ConfigurationError path.

## Root-owned Play integration required

PlayPage/LobbyPlayLayout are excluded from this manifest.

1. MapCard surfaces use Card/Raised, not a colour-to-white mix. Keep ModeColour only for a small accent/actual original art.
2. Selected border/check uses Selection; check glyph uses OnSelection.
3. Tutorial-note surface uses Card/Raised with normal light text.
4. FloorPlan borders, flight shading and storey-label backing use fixed ArtInk. Storey text stays white. Legacy Navy now resolves to light text, so retaining it for a diagram backing would hide white labels.
5. Keep actual preview images white-tinted, aspect-preserved and non-raycast.

Top-bar title and shop labels already resolve via LobbyKit. Header-only navigation underlines explicitly passed Sun remain amber; root may change those specific LobbyMenu calls to Selection for one selection colour. No LobbyMenu source changes in this lane.

LobbyMenu's account-initial and local-seat badge text sits on bright art colours. Root should keep those glyphs on fixed ArtInk rather than the now-light Cream alias. This is a remaining contrast dependency outside this lane. Shared source integration and the asset migration belong in the same validation cycle; running light labels over unmigrated cream prefabs is not a reviewable intermediate build.

## Exact saved-asset migration

After root integrates/imports the candidate sources, run:
`Wreckabulary.EditorTools.LobbySharedThemeSetup.Apply`
(Menu: Wreckabulary → Authoring → Apply Shared Neutral Interface).

First-run guards reject Play mode, dirty target assets/components, open target Prefab Mode, pre-existing palette/archive/backup paths, missing Roboto fonts, invalid current typography/online themes and unclassified visible prefab graphics.

Before target mutations it saves exact original bytes AND meta files for seven frames plus the online Interface asset under:
`BuildNotes/visual-coherence-20261029/shared-theme/before-assets/Assets/...`
with checked SHA256SUMS. It also creates editable Unity copies under:
`Assets/_Project/UI/DesignArchive/BeforeNeutral20261029`.

Only Unity APIs create/update the shared asset, prefab colour bindings and saved Online theme; no hand-written YAML/meta. Roboto Regular and Medium reference existing imported TTF assets. Existing fonts/textures are not recopied or destroyed.

A completed repeat verifies backups and shared bindings and retains current user edits; it does not reapply defaults. A partial failure leaves backups intact and rejects blind retry. There is no automatic rollback. Inspect failure evidence and restore the exact scoped backups deliberately before retrying.

## Verification and limits

Roslyn C#9 semantic check passed six assemblies with zero errors: runtime 180 sources, Editor 63, ShopLayout.Editor.Tests 7, Editor.Tests 43, Online.Client.Input.Tests 14, runtime Tests 69. This is source validation, not a Unity or Windows pass.

Focused native fixtures prepared:
- LobbyInterfaceThemeTests: 9 cases covering invalid/transparent palette channels, exact role application, original artwork/UV and selected visibility preservation, validation before mutation, unknown graphic rejection and Online local-setting preservation.
- WordboundUiThemeTests: 2 new cases for borrowed-theme/font preservation, Layout rebuild and resolved-copy disposal, plus invalid shared-palette fallback.

Root should run those fixtures plus existing lobby/settings/shop/controls and UI-theme input suites, then capture all relevant current Windows screens, including selected Shop preview, disabled states, keyboard focus and display Keep/Revert. Test source/fixture success cannot establish visual preference. Existing owner rejection remains until a reviewed candidate is accepted.

Required contrast changes beyond the central adapter are limited to neutral structural surfaces in SettingsPage/LobbyPages/ShopPage, one key-button surface in ControlsPanel and one shared-selection underline in LobbyTab. Bright podium/finish/letter art keeps fixed dark ink where needed. No catalogue, price, ownership, purchase, matchmaking, save/share or selection behavior is changed.
