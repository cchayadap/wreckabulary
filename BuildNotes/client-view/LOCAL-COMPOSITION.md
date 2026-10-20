# Workshop and flagship composition

STATUS: IN PROGRESS — NATIVE LOCAL COMPOSITION PASS; FULL DELIVERY PENDING

The integration now contains the offline session (`226aa9e`), authored physical sites and canonical evidence (`720eb60`), full flagship content (`2277270`, `0c0dcbe`) and independent diagnostic review (`e05a3ba`).

Root's current Client changes connect an immutable workshop snapshot to its exact resolved manifest. The handoff checks the document hash, seed, explicit local mode and play requirements, strict manifest roundtrip, physical plan, actual item catalogue and effective rules. It never resolves a replacement course or inserts another player. The offline match uses the same native body controller, puzzle runtime, presentation, input and HUD. Return awaits the local controller shutdown, then reveals the retained workshop history.

The launcher uses the workshop lane's `ConfigureLocalPlaytest`, `OpenFlagship` and `ShowStatus` APIs, integrated as `091630e`. Root added four native Client test cases for exact manifest retention, seed/document/mode mismatch and competitive-roster refusal.

The public/private content builder now chooses the authored flagship for race modes and Freestyle. FreeForAll retains its combat arena. Public registration remains empty until tested exact bindings are approved for the service. The full flagship's three-lap body-growth blocker remains open; startup counts alone do not establish whole-match capacity.

Local startup callbacks use a generation fence so returning and starting another playtest cannot let an old completion hide the new editor. The local HUD has an explicit Return to workshop action and a visible presentation-failure path. The shared controller's concurrent Stop repair is integrated as `6dda92a`. Independent launch review caught premature destruction after failed shutdown; the launcher now retains that context unless shutdown has actually completed. A terminal shutdown failure does not promise that another click will repair the same failed operation.

The HUD shows the current unsolved puzzle's plain-language objective and a one-based checkpoint count. Actual world markers are a separate presentation follow-up.

Combined source verification: R0 PASS, 117.1 seconds (`Logs/gate_check_all_20261020T163256Z.log`), followed by affected Wordbound/workshop core PASS 346/346, 53.7 seconds (`TestResults/core_20261020T163444Z.trx`). The core filter explicitly excludes `FlagshipSeeds`, whose frozen dedicated run continues separately. Stage-1 lifecycle cleanup is integrated as `f917425`; the full capacity policy is not yet integrated.

First combined native run: FAIL before test execution, 148.1 seconds (`Logs/gate_test_editmode_20261020T163615Z.log`). Unity's separate Rules.Tests assembly cannot access two internal lifecycle test seams: `Economy.RetireWordboundTile` and the internal `EconomySnapshot` constructor. The standalone core runner compiles those tests with Rules, which concealed that boundary. James is correcting the tests without widening the production API. All selected native objectives remain NOT RUN for this source checkpoint.

Repair integrated as `0c5a706`: lifecycle tests use the public authoritative checkpoint restoration path; only the deliberately malformed snapshot fixture uses precise constructor reflection. James verified these nine cases from a separate test assembly referencing the pinned Rules library. Independent shutdown review is integrated as `2a4e561`, and the updated real canonical checkpoint/SQLite fixture as `b296017` (19 affected Worker tests and TypeScript check PASS).

The next native batch also includes a new actual launcher composition fixture: open the flagship workshop, edit its title and seed, request the normal playtest, observe the exact native world/view/HUD/guide, await Return, and verify that the same history supports undo and redo. It uses an isolated Library storage directory. This does not claim mouse/controller interaction, movement, puzzle completion or HTTP instrumentation; independent source review found no confirmed false-pass in its stated scope.

The guide is integrated as `cbb7917`, with reproducible source checks in `49ca736`. Both client paths now bind it to the exact course, local player and player camera.

Final combined verification at `49ca736` plus the root client bindings and launcher fixture:

- R0 PASS, 37.9 seconds, `Logs/gate_check_all_20261020T165327Z.log`.
- Repaired lifecycle core PASS 9/9, 33.4 seconds, `TestResults/core_20261020T165403Z.trx`.
- Native PASS **93/93**, no skipped cases, clean exit, 535.9 seconds, `TestResults/editmode_20261020T165452Z.xml` and `Logs/gate_test_editmode_20261020T165452Z.log`.
- Native breakdown: workshop mode controls 3; controller shutdown 8; actual launcher/history 1; local native movement/jump/memory checkpoints 2; party 18; exact workshop handoff 4; guide plans 11 and renderer/scene ownership 7; authored content 14; lifecycle 9; physical evidence 16.

The native launcher fixture verifies an edited full flagship starts with its exact manifest, view, HUD and visible guide objects, then destroys its owned client/camera/guide on confirmed Return and retains the same undo/redo history. The two lower-level local fixtures separately verify movement, jump, landing and complete memory checkpoints without a cloud-ready Runner. The 251-second party fixture accounts for most of the test execution time. Input-authority warnings for non-player spawns remain a bounded SDK-call cleanup follow-up; no test errors or failed cleanup occurred.

No pixel inspection, human control usability, full physical puzzle playthrough, body-capacity guarantee, four/eight-peer gameplay, host recovery or Windows player delivery is inferred from this batch.

Next sequence:

1. Preserve this verified composition and Unity-generated metadata.
2. Preserve the ongoing frozen seed results independently of newer lifecycle changes.
3. Finish route/start authoring and body-budget acceptance, capture the actual UI/player, then resolve four/eight-peer gameplay, physical interactions and host recovery.
