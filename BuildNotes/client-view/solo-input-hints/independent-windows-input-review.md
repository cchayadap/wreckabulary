# Independent Windows Solo input-lifecycle QA review

STATUS: COMPLETE — no blocking source finding after the explicit disposed-state observation was added. Final reviewed live WordboundSoloQa.cs SHA-256 `1998128669c66f76a32796d7f509e581c2f7c4c855d071fd9a5c780ad6623709`. No live edits or native jobs by reviewer; future Windows execution pending.

The new assertions retain the exact original invoking binding and require the effective Wordbound adapter to borrow that same source. The scenario still uses the real local entry and RequestReturn→Hub/object-teardown path; it does not call adapter.Dispose or invoke production lifecycle methods manually. Existing source/preferences/seat checks, deadlines and captures remain.

Concrete finding sent to root: an empty retained adapter ControlHint alone does not prove disposal for a borrowed gamepad, because PadHint is also empty while its device is disconnected. Keep the hint assertion and add a read-only assertion of the retained wrapper's private disposed state before recording wrapper_disposed=true. This strengthens observation without changing runtime behavior or the player lifecycle. The caller DesktopBinding.Map.enabled assertion should remain.

Resolution independently verified: root added Require(Read<bool>(adapted, "disposed"), ...) before the retained empty-hint assertion and released note. The original hint and caller-map checks remain. This closes the disconnected-pad false-positive path while observing the real player teardown; no manual lifecycle invocation or input-state mutation was added to the Windows scenario.

Future success would prove actual player composition and lifecycle disposal for the exercised binding. It would still not prove physical controller events, all device layouts or reconnect handling; the scenario explicitly labels that boundary.

## Integration compile failure and assembly correction

Independently read the205927 gate: FAIL after19.7 seconds before tests, CS0012 at WordboundSoloQa.cs line185 for InputActionMap from the new caller `keys.Map.enabled` observation. The gate emitted no test-results file. This is a compile failure, not a further failed input assertion and not a51-case rerun.

Root added only the direct `Unity.InputSystem` reference to the project-owned `Assets/_Project/Scripts/WordboundQa/Wreckabulary.Wordbound.Qa.asmdef`; no package, SDK, engine or harness file changed. The reference is required because the QA source now directly uses the public InputActionMap type exposed by DesktopBinding.Map. All prior references and the `UNITY_EDITOR || GSK_QA` constraint remain. Final asmdef SHA `708348ce71773425d8bf7fe559810a324af85372f2a1109bd0211367c0c99bd9`.

The addition is acyclic: the installed InputSystem package depends on its UI package, and the actual Editor/player compiler response files for InputSystem/UI contain no Wreckabulary or GameStudioKit back-reference. The QA assembly already depended on the project's input-using runtime; the added edge makes its direct type usage explicit. See unique evidence `independent-asmdef-210044.json`. The final WordboundSoloQa.cs hash remains exactly `1998128669c66f76a32796d7f509e581c2f7c4c855d071fd9a5c780ad6623709`.

Independently read R0 `completion-check-2026-10-26T21-00-44-369Z`: PASS Runtime and Editor checks,6.5 seconds. Controls210101 native rerun was still pending when this source review was recorded. No native/Windows result is inferred from R0 alone.
