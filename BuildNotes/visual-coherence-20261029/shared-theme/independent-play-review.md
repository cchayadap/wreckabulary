# Independent shared-theme Play review

STATUS: BOUNDED SOURCE REVIEW COMPLETE — ROOT REPAIRS OBSERVED; NATIVE RECHECK PENDING

Scope: frozen shared-theme manifest and central LobbyKit, WordboundUiTheme/Styles changes. Check contrast over bright artwork and legacy callers, semantic colours, Layout-event cache/resource ownership, focus/disabled cues and composition. Migration/frame review is owned by map lane. No live/native changes; this report is the only write.

Inspected the frozen central candidates and relevant existing callers, then the root's bounded live-source repairs. No source/native edits by this reviewer. Findings were sent immediately to root. Native output reported by root is attributed separately below.

## Findings and disposition

1. **P1 — Constructor/deserialization-unsafe resource load in the frozen candidate.** `candidate/.../LobbyInterfaceTheme.cs:79` lazily calls Resources.Load from Palette. Existing MonoBehaviour field initializers read LobbyKit colours, including LobbyShopActionView:16, LobbyLoadoutPageView:18, LobbyShopPageView:21, LobbyShopOfferView:20, LobbyRecipeTileView:15 and LobbyTab:13. Root independently reported forbidden Resources.Load errors during native migration181422 despite its gate returning PASS. A gate PASS therefore did not establish a clean native result. **Root source repair verified:** live Palette now returns managed activePalette only; LoadSaved is explicit at BeforeSceneLoad and LobbyMenu.Awake; SubsystemRegistration resets to managed fallback; OnValidate refreshes the currently loaded palette using ReferenceEquals/validation without loading resources. Frame.Apply already uses its serialized Theme directly. Native deserialization rerun remains pending in this review.
2. **P2 — XP fill hidden by identical semantic colour.** Frozen `candidate/.../LobbyPages.cs:734` changes Career fill to Raised→Raised while `candidate/.../LobbyKit.cs:55` changes Track to that same Raised value. Default fill/track contrast is exactly1:1, hiding the progress proportion. **Root source repair verified:** live fill now uses Selection and its numeric label has a Card backing, retaining text separation while the progress is visible. Actual Career image still needs review.
3. **P3 — Legacy generated typography changes shadow/outline to light ink.** Existing `Assets/_Project/Scripts/UI/Lobby/LobbyFonts.cs:124,130` used Navy for _OutlineColor and _UnderlayColor. With Navy now meaning light Text, the missing/invalid saved-profile fallback loses its original dark outline/shadow. Valid saved Roboto typography avoids this fallback. **Root source repair verified:** both assignments now use fixed ArtInk. No font or material asset was edited by this reviewer.

## Central runtime/contrast assessment

- Navy/Cream aliases now mean light text, so structural callers must change with the palette. The candidate neutralizes normal card/key/stepper/field/Keep-display surfaces and retains fixed ink for letter tiles, coins, podium text, win badges and bright statistic symbols. Root's live Play surfaces and account/seat badge glyph adaptations were observed. Root still needs actual complete Lobby/Shop/Settings/Play/Online captures; this source review does not prove every artwork overlap is attractive.
- Calculated from the default opaque RGB values: Text/Card13.27:1, Muted/Card7.41:1, DisabledText/Disabled4.17:1, OnPrimary/Primary9.45:1, OnSelection/Selection8.77:1. These are colour-pair calculations only, not an accessibility pass: rendered alpha, typography, artwork and user-selected colours are outside them. Palette validation checks finite opaque channels, not a minimum contrast ratio for arbitrary designer edits.
- Primary now has a filled button with dark OnPrimary text; the existing focus ring, input callbacks and disabled CanvasGroup mechanism remain. Selection marks use OnSelection against Selection. Stepper hover's Picked and idle Raised resolve to the same default colour, reducing colour-only hover feedback, but existing movement/focus remains; inspect actual hover if stronger feedback is needed.
- WordboundUiStyles retains its Layout-event fingerprint check. Shared colours/fonts are applied to a temporary owned theme clone; style/texture creation copies values, and finally releases only that clone. Shared saved theme, fonts and background remain borrowed. ReleaseSkin destroys the owned cloned GUISkin/textures, not borrowed source assets. The existing scoped GUI skin/colour/matrix restoration is preserved. Invalid shared data follows ConfigurationError/default fallback. No additional confirmed ownership or cache-lifetime blocker was found in this bounded source review.
- Plain fallback typography shadow was the concrete unresolved alias caller identified. Shop's portrait-missing fallback still draws a light Satchel over a colour blob (`ShopPage.cs:256`); that mixed-art fallback warrants visual checking if used, but this review has no captured evidence establishing a defect there. Do not treat the intact data/handler logic as owner approval of the new palette.

## Evidence boundary

Frozen manifest SHA-256: `f3402c9c2e4f67d27295364f9f77ed4448e18fa5a45059bd842c96710fa63f71`. Independently checked manifest parity for LobbyKit, LobbyInterfaceTheme, LobbyPages, WordboundUiTheme and WordboundUiStyles. Inspected relevant adjacent candidate diffs (Controls/Settings/Shop/Tab), existing LobbyPress/LobbyFonts/Hint/Pause-settings callers and the new theme tests. Migration/frame deep review is the map lane's separate report.

Root repair source hashes at the final read:

| Live file | SHA-256 |
| --- | --- |
| LobbyInterfaceTheme.cs | `22377b21f8e7a34a9d8573f064014b89b6fa7800d5d60b78d4651e2b28362726` |
| LobbyMenu.cs | `3298079828c93cdad9fd9b0fab8bf2f59c6daddba4c78d09733ec39bfe5ce315` |
| LobbyPages.cs | `9dd228f370cba0617928757cde43fe3a2e82d41d2e2a77b6af9dc816abf2847d` |
| LobbyFonts.cs | `10517bd5644b5fd35fa5b4117050f2eec838637dd4b58189046c6fae09ada986` |

No native tests, constructor execution, UI capture or asset migration ran in this reviewer lane. The frozen manifest remains evidence of the original candidate; root's subsequent repairs are live-source differences and must be included in root's new compile/native result. Native181422 error details were supplied by root, not independently parsed here. Next action: rerun the constructor/migration path with clean raw logs, then inspect actual UI states, including Career XP, keyboard focus, disabled primary actions, bright artwork and display Keep/Revert. No remaining source blocker was found in the three observed root repairs.
