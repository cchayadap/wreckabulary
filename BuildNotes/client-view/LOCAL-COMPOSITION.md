# Workshop and flagship composition

STATUS: IN PROGRESS — COMBINED SOURCE CHECKS PASS; NATIVE VERIFICATION PENDING

The integration now contains the offline session (`226aa9e`), authored physical sites and canonical evidence (`720eb60`), full flagship content (`2277270`, `0c0dcbe`) and independent diagnostic review (`e05a3ba`).

Root's current Client changes connect an immutable workshop snapshot to its exact resolved manifest. The handoff checks the document hash, seed, explicit local mode and play requirements, strict manifest roundtrip, physical plan, actual item catalogue and effective rules. It never resolves a replacement course or inserts another player. The offline match uses the same native body controller, puzzle runtime, presentation, input and HUD. Return awaits the local controller shutdown, then reveals the retained workshop history.

The launcher uses the workshop lane's `ConfigureLocalPlaytest`, `OpenFlagship` and `ShowStatus` APIs, integrated as `091630e`. Root added four native Client test cases for exact manifest retention, seed/document/mode mismatch and competitive-roster refusal.

The public/private content builder now chooses the authored flagship for race modes and Freestyle. FreeForAll retains its combat arena. Public registration remains empty until tested exact bindings are approved for the service. The full flagship's three-lap body-growth blocker remains open; startup counts alone do not establish whole-match capacity.

Local startup callbacks use a generation fence so returning and starting another playtest cannot let an old completion hide the new editor. The local HUD has an explicit Return to workshop action and a visible presentation-failure path. The shared controller's concurrent Stop repair is integrated as `6dda92a`. Independent launch review caught premature destruction after failed shutdown; the launcher now retains that context unless shutdown has actually completed. A terminal shutdown failure does not promise that another click will repair the same failed operation.

The HUD shows the current unsolved puzzle's plain-language objective and a one-based checkpoint count. Actual world markers are a separate presentation follow-up.

Combined source verification: R0 PASS, 117.1 seconds (`Logs/gate_check_all_20261020T163256Z.log`), followed by affected Wordbound/workshop core PASS 346/346, 53.7 seconds (`TestResults/core_20261020T163444Z.trx`). The core filter explicitly excludes `FlagshipSeeds`, whose frozen dedicated run continues separately. Stage-1 lifecycle cleanup is integrated as `f917425`; the full capacity policy is not yet integrated.

Next sequence:

1. Run native local TimeRun/Freestyle, controller Stop, workshop handoff and party content tests.
2. Preserve the ongoing frozen seed results independently of newer lifecycle changes.
3. Exercise the actual launcher/editor/playtest/return UI, then finish world markers, body-budget acceptance and unresolved four/eight-peer gameplay, physical interactions and host recovery.
