# Recipe expansion implementation evidence

Status: source complete; native Unity verification is coordinated by the lead agent.

The catalogue now enables the original twelve recipes plus APPLE, WATER, CAKE, SODA, SHIELD, FAN, CLOCK, BROOM, HAMMER, SPEAR, PIE and STOOL. Original Vault model paths, dimensions, grip offsets, held scales, skins, categories and consumed-on-use facts were preserved. Unity and Web item JSON are synchronized.

`Catalogue.cs` parses the appended WindField and SlowField effects and optional deploy `arc` (360 degrees by default). Validation rejects invalid finite values, effect timings, arcs, footprints, damage reductions, unavailable handling families and contradictory consumed/recoverable throws. The seed tool retains the completed expansion on rebuild, writes both data mirrors and checks gameplay contracts, icon presence and mirror equality. Supplying `--recipes` additionally checks original Vault asset facts.

Unity recipe cards and HUD icons now share `ItemArt`, preferring the generated Sprite library and falling back to supplied item textures. Sprite UV regions are preserved. Descriptions distinguish food, drinks, shields, wind, slowing fields and single-use throws. A fourth HUD effect chip allows slow, speed, bubble and carried-object indicators to coexist.

The browser implements matching use channels, independent speed-boost expiry, combined speed/slow movement, shield raise timing and full-circle protection, directional wind, neutral slow fields with obstruction/storey checks, wind movement for loose letters, original props and dropped gear, reusable-field pickup/expiry accounting and single-use PIE impacts. Wind and slow fields have distinct previews; effect descriptions and the slow chip identify their behavior. Browser physics remains the existing lightweight simulation; Unity owns the Rigidbody implementation.

Whole-house availability checks found missing W supplies on three maps. One WATER prop was appended to each, preserving all existing furniture indices:

| Map | Furniture index | Room | X | Z | Yaw |
| --- | ---: | --- | ---: | ---: | ---: |
| Pinwheel | 34 | Kitchen | 5.9 | 9.3 | 0 |
| Courtyard | 30 | Kitchen | 13.65 | 4 | 0 |
| Walkup | 36 | Cafe | -7.7 | 5.4 | 0 |

All use the ground-floor default height. Their Web mirrors were updated. The lead agent owns additive saved-world asset migration. Tests retain the original Core guarantee in every spawn room and add availability for all 24 recipes across each complete house.

Validation completed:

- `dotnet run --project tools/RulesHarness`: 227 passed, 0 failed. Conservation fuzz uses three deterministic seeds with 30,000 operations each and unchanged minimum operation-coverage assertions.
- `python tools/DataTools/seed_items.py --check`: 50 catalogue entries, 24 enabled, no problems. External Vault manifest comparison was not run because no manifest path was supplied.
- `npm test` in Web: 122 passed, 0 failed, including 21 focused power tests.
- `npm run build` in Web: succeeded, 18 modules transformed. Missing local build dependencies were restored with `npm ci --ignore-scripts` before the successful build; dependency versions were unchanged.

No Unity processes or Git mutations were performed by this implementation lane. Native screenshots and PlayMode verification are separate lead-owned evidence.

Final browser review correction:

- Shields now use LMB or RMB to guard. A shared readiness query controls damage reduction, the avatar block pose and the held-item raised transform. Guard intent prevents punches throughout the raise; releasing, dodging or switching shields resets it. The primary action label reads BLOCK.
- FAN applies bounded acceleration and damped movement to unanchored world props and dropped gear. Anchored/deployed objects block the field, while the source and target cannot occlude themselves. Drift checks the whole object footprint against walls and surrounding objects, then stops on collision. Pickup clears drift. It changes neither ownership nor spent/minted letters.
- Added actual animator and held-item transform checks around the raise threshold, combined-input and shield-switch regressions, loose-prop/gear accounting and pickup coverage, anchored/deployed obstruction checks and a wall-footprint regression. All 122 browser tests and the production build passed after these corrections.

Rendered browser verification:

- Existing `Web/test/browser.mjs` ran against the latest production bundle at `http://127.0.0.1:4173`, using installed Chrome headless. The Browser plugin was unavailable, so the repository Playwright path was used. Outputs stayed outside the repository under the temporary `wreckabulary-browser-smoke-2026-10-09-fixed/playwright-results` directory.
- Final result: 29 browser checks passed, zero captured page errors or unexpected HTTP response errors. Desktop (1440×900) and landscape phone (844×390) covered real imported models, workshop editing/roundtrips, gameplay input, touch movement and simultaneous actions, crafting, co-op objectives, pause/resume and retry flows.
- Screenshot review exposed an existing phone Spell → Recipe book overlap despite the old smoke passing: the composer remained behind the bag and the enlarged minimap covered cards. The narrow correction opens a standalone recipe view, dismisses the composer/map, hides underlying gameplay controls and preserves the normal desktop Tab bag/map. Recipe cards use four readable columns with vertical scrolling.
- The browser regression now verifies the composer is absent, no expanded map remains, gameplay controls are hidden, visible card centers are unobstructed and at least 100 pixels tall, and the final scrolled recipe opens a correctly prefilled composer. Capture waits for the actual fade animation to finish. The final mobile capture was visually inspected.
- `recipes-mobile.png` is the corrected phone evidence; `browser-report.json` contains the 29 checks. The desktop `game-desktop.png` is a near-wall, cropped-avatar harness frame and should not be used as camera-quality evidence. Camera work remained out of scope. The preview server was stopped after verification.
