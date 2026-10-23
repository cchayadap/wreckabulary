# Native workshop setup refresh review

STATUS: SOURCE PASS — reviewed root `c40fe19`, no edits or execution by this reviewer.

The authored private fixture failed during preparation before creating its first Runner; the existing evidence is preserved in root `TestResults/editmode_20261023T163328Z.xml` and the matching log. This review concerns the three-line assignment refresh and bounded exception-origin diagnostic, not a claim that the native case now passes.

`OnlineServices/src/party.ts` returns its ordinary party view after enqueue; its `get` method separately queries pool status and adds `assignment`. `WordboundPartyCoordinator.Queue` applies the mutation response. Therefore immediate fixture dereference of `host.Party.Assignment` could receive null even after successful room creation. `PollCore` fetches the status-bearing party response and applies it. The fixture's awaited `host.Poll(); CheckWorkshop(host);` after completed queue aligns with that service contract, and the explicit assignment guard produces a clear failure before dereference if the refresh still supplies none. The fixture has no automatic menu polling race. The existing private start, independent bundle download and final signed exact-roster/content checks remain intact.

The `Wait` failure diagnostic retains its task completion/cancellation/deadline flow. Its best-effort stack inspection emits at most four method/type names, without file paths, arguments, exception messages or response values; unavailable stack information leaves the explicit fallback. It does not suppress the failed assertion.

The same root commit fully qualifies `NUnit.Framework.Assert.IsNotNull` in the tick aggregate after R0 exposed the new NUnit/Fusion `Assert` ambiguity. Future deltas must preserve that correction. Adapter attachment and workshop setup both still require native reruns. No production behavior, SDK settings, timeouts or test assertions were weakened.
