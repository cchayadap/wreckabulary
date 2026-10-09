# Recipe and powers expansion

STATUS: COMPLETE

Baseline: `main` at `3e730e6`. Unity integration is serialized through the lead editor process; other lanes edit source or generate artwork only. Existing centered camera, authored worlds, couch multiplayer and letter ownership remain acceptance requirements.

## Playable scope

| Recipes | Behavior |
| --- | --- |
| APPLE / WATER / CAKE | Heal 30 / 18 / 50 health after a cancellable use channel; never exceed maximum health |
| SODA | 1.35× movement for six seconds, separate from slow effects |
| SHIELD | Raised protection from all directions, durability-limited; lowered shield provides no protection |
| FAN | Directional 90° wind field; pushes players and loose objects within range, respecting walls and storeys |
| CLOCK | Circular slowing field, independently composed with speed boosts |
| BROOM / HAMMER / SPEAR | Wide sweep / heavy smash / long narrow thrust |
| PIE | Single-use thrown projectile; no letter refund after spending |
| STOOL | Compact jump pad |
| MAT | Existing speed floor gains animated directional markers |

The catalogue adds `WindField`, `SlowField` and an optional `deploy.arc` (default 360°). Vault model, grip, scale, size, skin and consumable facts are preserved. Data generation must reproduce the new enabled set. Shared Unity/Web data requires corresponding Web mechanics and regression coverage.

## Art and animation

Six separate GPT-generated transparent inventory illustrations: SHIELD, SODA, APPLE, WATER, FAN and CLOCK. Source PNGs stay in `Art/Generated/Items`; a saved Sprite library supplies runtime UI with existing icon fallback. Prompts and generation provenance are recorded in `docs/art/GENERATED-POWERS-2026-10-09.json`. No Higgsfield.

Use animation must fit its channel, distinguish food from drinks, cancel cleanly, and preserve animated item grips. Raised shield, deployed fields and speed floor must visibly animate without changing gameplay timing. Native Unity captures will show in-world models and effects separately from generated inventory art.

## Delivery checks

- Catalogue validity, seed reproducibility and Unity/Web synchronization.
- Native tests for use cancellation, health cap, spending/refunds, shield directions and release, field range/walls/floors, expiry/pickup/reset, slow/boost composition and paused timing.
- Actual Unity frames for recipe UI, consumable use, shield, FAN/CLOCK, MAT and local multiplayer.
- Web behavior tests and production build; Unity build and startup smoke where affected.
- Commit/push verified work as SethyPagna with no co-author trailer.

## Evidence

- `a0970fc` pushed the six generated PNGs, import policy, runtime Sprite library and six passing art tests. Each generation has a recorded prompt and SHA-256 in the provenance manifest.
- RulesHarness: 227 passing. Catalogue seed check: 50 entries, 24 enabled, no problems. Original core recipes remain available in every spawn room; all 24 recipes are available across each complete house. The saved-world migration adds missing WATER supplies once and preserves artist transforms on repeat runs.
- Review corrections cover browser shield input/raise and FAN loose-prop movement, mobile recipe overlays, field re-enable lifecycle and paused cosmetic rotation feedback. The initial EditMode failure depended on previously hidden editor ceilings; the repaired fixture saves/restores user visibility. Final reruns are recorded below.
- First native journey: 26 actual Unity frames and a completed manifest. It verified UI art, consumable timing, shield/field effects and two-player layouts at 16:9 and 21:9. Visual inspection caught a detached raised shield and poor drink contact; authored-grip rotation and cosmetic arm contact were corrected. A field lifecycle regression also prompted a re-enable fix and test.
- The first render set remains in `Logs/checkpoints/2026-10-09-powers-first-render`; reviewed final evidence is versioned with feature checkpoint `a72605c`. Both commits use SethyPagna's identity without co-author trailers.

### Final regression results

- Full EditMode rerun: **305/305 passed** (`Logs/powers-editmode-final.xml`). The ceiling visibility fixture preserves and restores editor state.
- Full PlayMode: **289 passed, 0 failed, 14 skipped** (`Logs/powers-full-playmode.xml`).
- Final appearance follow-up after removing paused rotation feedback: **7/7 passed** (`Logs/powers-pose-final.xml`), including exact rendered item/grip freeze, upright raised shield, animated mitten contact, channel timing and cancellation.
- Browser: **122 unit tests**, production bundle, and **29/29 real-Chrome smoke steps** passed with zero captured errors. Mobile recipe navigation now closes overlapping composer/map surfaces and checks unobstructed cards plus last-card selection. Final browser screenshot/report are saved in `evidence/browser-powers-2026-10-09`.
- Final native **28-frame capture passed**, including an active single-human centered third-person match and 16:9/21:9 couch matches. The completed manifest is `evidence/unity-powers-2026-10-09/power-journey.json`. It checks the same live FAN/CLOCK/MAT renderers at both sampled frames before comparing motion; missing effects can no longer satisfy animation assertions through default probe values.

### Visual acceptance

Independent pixel review accepted the shield/drink contact and distinct field visuals. The raised shield stays upright over the torso and its aura clears the face. WATER's neck and SODA's rim reach the lower face; APPLE raises centrally. Consumables disappear only after their channels complete. Across 0.3833 seconds, retained FAN/MAT renderers move and the CLOCK hand rotates 10.73 degrees. Frame 28 confirms the standard single-human perspective remains centered behind the avatar with generated SHIELD art in the HUD. Two-local matches intentionally use the shared overview camera.

A transient onboarding banner crosses the P2 label in the ultrawide couch frame; the player and HUD remain readable. GPT images are transparent inventory illustrations; existing imported 3D item models remain the in-world assets. This expansion preserves local multiplayer; it does not add an online transport.

### Windows delivery

`ProductionBuild.Windows` succeeded with zero errors, 207,818,291 bytes (`Logs/powers-windows-build.log`). The packaged executable initialized Direct3D 11 and Windows input, remained responsive, and logged no exception or load/shader error during startup smoke (`Logs/powers-windows-player.log`). The smoke process was then stopped. This verifies packaged startup; gameplay visuals were verified in the native Unity capture above. Machine-readable counts and executable SHA-256 are in `evidence/unity-powers-2026-10-09/verification.json`.

Generated pipeline/build settings and material-name churn were preserved under `Logs/checkpoints/powers-build-side-effects` and restored to their pre-build source values. The build and authored scenes remain available for local use. Open `OPEN-LATEST-UNITY.cmd` beside the checkout, or use **Wreckabulary → Open Current Workspace**, to edit the current Hub and world prefabs.
