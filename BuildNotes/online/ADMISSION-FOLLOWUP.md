# Native admission follow-up

STATUS: IN PROGRESS

Integration commit `deae592` preserves the previously passing online session implementation. The selected native run started at 2026-10-20T15:49:38Z, log `Logs/gate_test_editmode_20261020T154938Z.log`, with four/eight peers, host departure, interactions and the party suite.

During the still-running suite, the original host failed in `WordboundFusionSession.Admit` with `FormatException`. Existing callbacks expose only the exception type. This is insufficient to identify whether start-certificate verification, admission-grant verification, connection response parsing or signed connection-lease validation failed. The earlier two-peer actual gameplay pass remains evidence for that earlier run; it does not supersede these failures.

Read-only inspection found no oversized eight-seat token or integer-overflow evidence. One hypothesis is strict time validation: `VerifyConnectionCore` rejects expiry beyond local time plus 15 seconds while the service creates expiry from its own clock, and JWT issue times currently allow no forward skew. No validation has been weakened. The next diagnostic should record a fixed stage/code and exception method stack, never raw tokens, HTTP responses or secrets.

A bounded hardware sample while the suite ran observed 90.95% and 90.89% total CPU, with 2,669–2,675 MiB available memory. Both visible Unity processes belonged to this gate: the editor and its child import worker. Other processes belong to separate work and were left alone. A public unauthenticated HEAD probe took approximately 25 seconds; that observation cannot distinguish scheduling, DNS, transport or server delay and does not establish clock skew.

Final result: **FAIL**, 22 total, 18 passed and four failed, 444 seconds (`TestResults/editmode_20261020T154938Z.xml`). All 18 party tests pass inside Unity. Three gameplay cases fail during the original host's admission with `FormatException`; the eight-peer case fails a client HTTP request and records one service-cleanup failure. None of the four gameplay objectives is accepted. That failed cleanup request has an unknown server outcome; no native Runner process remains after the completed gate.

Root has added fixed admission stage labels, an allowlist of constant failure codes, and method-only exception origins. Verified-token failures attach only numeric issue/expiry/lifetime offsets; the session emits only known keys whose values are integers. Unknown messages and arbitrary exception data remain omitted. Existing exception types, validation conditions and public failure behavior are unchanged. Three lifecycle regression cases cover redaction and first-failure retention.

Independent review identified that a diagnostic accessor could throw before session invalidation. The original invalidation and authority freeze now precede guarded diagnostic extraction. A fourth regression uses an exception whose Message accessor throws, verifying that invalidation and notification still complete.

Focused lifecycle verification: PASS, 37/37, 5.36 seconds, using `dotnet run --project BuildNotes/online/FusionCompile/SessionLifetimeTests.csproj -p:PuzzleRoot=C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration -- SessionLifetimeTests`. The explicit Rules source is the integration checkout, not the physical lane's newer schema.

Aggregate R0: PASS, 21.3 seconds (`Logs/gate_check_all_20261020T160954Z.log`). Affected online core rules: PASS 22/22, 24.1 seconds (`TestResults/core_20261020T161027Z.trx`).

Native two-peer diagnostic rerun: PASS 1/1, 234.9 seconds (`TestResults/editmode_20261020T161115Z.xml`, `Logs/gate_test_editmode_20261020T161115Z.log`). Both actual authenticated peers composed bodies, moved, committed commands and acknowledged the exact durable state. The earlier admission failure did not recur in this case. No clock or admission guard was changed. Four/eight-peer gameplay, interactions and recovery remain unaccepted; this pass does not identify their prior failure's cause.

Next action: preserve diagnostics, integrate the separately verified local session and authored physical content, then verify their native composition and rerun the unresolved larger multiplayer cases.
