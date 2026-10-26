# Independent review: borrowed local Wordbound input

STATUS: COMPLETE — no blocking source finding in frozen patch `8f84a1af7f976554dee9ccc7439179fb8dda350992de92e8dc9b1da141ef0ef7`. Clear for root integration and native tests. Read-only source review; no live changes or native jobs by reviewer. This is not Windows/input acceptance.

Review contracts: retain the exact original Session binding, use only its fixed desktop/device, preserve Wordbound menu/aim/cycle/release semantics, never dispose or remap borrowed controls, never select another local player's device, and keep the existing exact-source assertions meaningful.

Baseline confirmed: `WordboundPlayerView` currently passes an explicit legacy binding straight through, while menu commands and HUD hints require `WordboundInputBinding`; the original desktop/gamepad paths lack several Wordbound-specific held/cycle semantics. Existing automatic online binding must retain its behavior.

## Integrity and verification

- Independently checked all six exact baseline and candidate hashes against manifest `4f9a97773bd4b6b6f7a5b0843b1000ec50a0af22c82fd30c1d3f3162200697b9`. All six live files still matched their baselines when checked. Both strict live forward apply and owned-baseline forward apply passed. Details: `independent-integrity.json`.
- The pinned actual failed-run TimeRun screenshot hash also matches. The draft fixes the source path behind duplicated labels and missing borrowed-input behavior; it does not relabel the prior failed Windows scenario as passed.
- Read the producer's in-memory semantic receipt: Online.Client23 sources/300 references and Input.Tests14 sources/311 references, zero errors. That compile was performed by the producer; this reviewer did not run a second compiler or a native test.

## Ownership, device identity and behavior

`PlayerView.binding` retains the exact caller-supplied object. Only known original DesktopBinding/GamepadBinding instances gain a separate effective adapter, whose `BorrowedSource` remains that exact object. Null input still creates the original automatic owned binding. An explicitly supplied Wordbound/custom binding remains caller-owned. Only the newly owned wrapper is disposed; its disposal skips borrowed desktop maps, and does not remove/disable a controller. No Session assignment, key override, settings or asset write is introduced.

The borrowed branch returns before automatic device scanning. Desktop remains desktop even if another pad has input. The original gamepad remains selected even when disabled/removed, and never falls back to another pad or keyboard. Read, menu, Start/Join and hints guard unavailable devices. Availability transitions suspend held actions; one loss event opens the actual view menu, while recovery requires release before resuming actions. The parameterless automatic scan path remains intact.

All PlayerView input/cursor/menu/hint routes now use the effective adapter; retaining only the old identity field would not have been sufficient. This restores the existing Wordbound desktop equipment-cycle behavior and gamepad aim/attack-held and menu-command handling. No new control mapping or fake production testing hook is introduced.

HUD changes combine the optional real key label and action exactly once. Unknown/custom bindings retain concise action-only text. Throw help avoids inventing a key when either constituent hint is unavailable. Every Hint callsite was checked; gameplay/error/reconnect state remains unchanged.

## Regression coverage and remaining native checks

The previous source-identity expectation is preserved and strengthened: the view's original field must be identical, and the adapter must separately identify the same borrowed source. Added tests cover exact rebound action labels/commands, action map and override preservation after view destruction, pad aim/held/cycle, actual HUD craft/back handlers, release/repeat/confirm/back, disabled/removed-device neutrality, one loss notification, no other-seat selection and usable original controls after wrapper disposal. Unknown fallback wording is covered separately. Existing tests and Solo/Creator exact-source assertions are not deleted or relaxed.

Root should run the Input.Tests suite plus the retained actual Solo/Creator native entry cases, then rebuild and repeat the complete Windows Solo/lobby scenarios. Actual controller menu navigation, held-action release after focus/device loss, and new HUD screenshots still require native/Windows observation. The source review does not claim physical hardware input, full gameplay, larger-map art or public-match acceptance.

STATUS: SOURCE DRAFT PREPARED — confirmed the screenshot fallback defect and broader borrowed-input omissions. Runtime/tests, preserved baselines, patch and hashes are in this folder; see README.md for exact ownership and acceptance boundaries. The room image was actually opened and is sparse; no art-complete claim is supported.

Current patch SHA-256: 8f84a1af7f976554dee9ccc7439179fb8dda350992de92e8dc9b1da141ef0ef7. Strict apply and in-memory semantic compile pass; native input/UI/gameplay verification remains root-owned. No live Assets changes or image/evidence replacement were made.
