# Compact Workshop toolbar source review

STATUS: COMPLETE — source review only; one intentional-layout test mismatch addressed in a separate draft. No production blocker confirmed. Native GUI and actual visual checks remain required.

Scope: read-only review of exact baseline/candidate WordboundWorkshop.cs, live consumers and IMGUI hit protection. No source edits, native launches, extra tests or visual acceptance claims. Parent owns integration and native checks.

Exact baseline SHA256: `944c6e8c3762024c4e6568c0bf66cd14c61164444d60a5165923b6d25eeef765`.
Exact candidate/live SHA256: `2f7b9cedd46592c2adc85bb161e62f8cb888fc2c24225a7172324c6b241cea23`. Hashes matched during review. The diff has line-ending noise; content comparison used `--ignore-space-at-eol`.

## Confirmed

- The new getter keeps the original viewport clamp and position. Closed height is `ChromeHeight` (one measured row + padding/gap); open height is the unchanged two-row style rectangle. Closed width reserves three square icon buttons plus a flexible labelled grid control. No extra fixed-width seed control remains in the closed row.
- `ToolbarReserved` derives from that same getter. `HandlePointer`, physical-site picking, hover selection and overlay label placement all use the shared exclusion. They do not retain an expanded invisible hit-blocking rectangle after collapse.
- Grid values still cycle 500 → 1000 → 100 → 500 mm through History. Seed entry, Set seed, lock and guarded reroll use the original commands. Focus-selection/floor actions and tooltips remain. No save/history/undo/selection semantics changed.
- The seed text control keeps its `workshop-text:Seed` name and existing text-focus suppression. No controller action binds to the old `Grid` control name. Pointer processing happens before the toolbar, using the current rectangle; the dice remains within both rectangle states, so its activation is protected from world picking.
- One existing `WordboundUiThemeTests` assertion always required the expanded two-row rectangle. That is stale for the intentionally collapsed default. The separate `test-fix` candidate modifies that existing method only: checks initial closed state, open → closed restoration, both height/width contracts, existing resize/custom-row containment checks and pointer access below the restored compact row. Two existing TestCase inputs retained, no new fixture/framework.

## Fit and interaction limits

The closed width formula has room for three fixed icon widths and one flexible text button at the existing 420px viewport / 52px-row / 24px-padding test size. Expanded seed-row content is unchanged from the prior layout. The formula alone does not prove actual glyph fit or GUILayout transition behaviour. Open and close Randomness in the live window, repeat at the smallest supported resolution and maximum font/row settings, and inspect Grid label wrapping and seed-entry focus. Specifically watch for GUILayout warnings when the conditional second row appears/disappears during a button event. No such warning was observed by this read-only review because it did not run native GUI.

The dice has a tooltip but no persistent pressed colour; the visible open seed row conveys state. This is a possible polish preference, not a confirmed input defect. Do not expand this patch into a new toolbar framework.

