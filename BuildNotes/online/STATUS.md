# Online coordinator lane

STATUS: LOCAL SERVICE VERIFIED; TRANSPORT INTEGRATION PENDING

Base: 12842c75608694f36819edd3bd4dc104ece7f79c
Branch: sethypagna/online
Scope: OnlineServices/, docs/wordbound/ONLINE.md, BuildNotes/online/.

2026-10-20: Read PLAN and updated CONTRACTS. Selected separate SQLite Durable Objects with strict HTTP schemas, authenticated guests, durable party transitions and pool-local atomic admission. Read Cloudflare skills and current official runtime/testing documentation. Current Cloudflare testing uses @cloudflare/vitest-plugin; use actual local Workers runtime tests.

No deployment, account changes, Photon installation or writes to the original checkout.

2026-10-20 first green increment: implemented HTTP schemas, server-minted guests, rotating refresh/access credentials, durable party transitions, SQLite pool transactions, Fill allocation, private/CustomFill routing, signed content/roster/seat grants, staging rollback and reconnect deadlines. `npm run verify` passes TypeScript and 14 Workers-runtime tests. Evidence: verify-first.log. No zero-test or pure-mock substitute.

Dependency policy: selected @cloudflare/vitest-plugin 1.4.4, wrangler 4.151.1 and jose 6.2.12 because newer releases were inside the machine's three-day minimum release age. Kept that policy intact. Local workerd supports compatibility date 2026-10-19, so used that verified date.

2026-10-20 second green increment: TypeScript and 27 tests pass (20 actual Workers-runtime tests plus seven pure rule tests). Added SQLite persistence and eviction checks, real Durable Object alarm execution, interrupted cross-object request recovery, concurrent identical queue replay, final-ready/cancel ordering and explicit return-to-lobby lifecycle. Team-local slots and non-race Solo format now match Unity. Evidence: verify-recovery.log. No RPC disposal warning remains in this run.

Read-only C# review identified an ambiguous route-hash encoding and an inclusive reconnect boundary. Root confirmed fixes and regression coverage for both in its separate lane.

2026-10-20 final local increment: TypeScript and 35 tests pass (28 actual Workers-runtime tests plus seven pure rule tests). Added signed match-start certificates with a distinct audience and cancellation regression, TimeRun/FFA/Freestyle paths, real FFA timer alarm, concurrent private-code lookup retry and durable leave replay after membership has changed. Explicit disposal of RPC results eliminates the intermittent disposal warning; verify-handoff.log contains no such warning. Expected rejected RPC domain errors are logged by the local runtime but are asserted HTTP errors; Vitest reports no unhandled test errors.

Validation command: `npm run verify` in OnlineServices; the PowerShell runner preserved npm's exit code. Exit 0; two test files, 35 passing tests. Evidence: verify-handoff.log. Keys exist only in the in-memory test configuration; no external service was called or deployed.

Additional read-only C# review: no blocking issue found in the current CommandJournal/SnapshotCommitTracker layers. Root was advised to hard-pause on target restore failure, require monotonic simulation ticks in the adapter, and validate receipt domains when exposing the snapshot wire decoder.

Next recoverable action for integration: implement the Unity adapter against docs/wordbound/ONLINE.md, verify authenticated Photon identity plus seat grant and start certificate, and exercise real 8-player sessions with disconnect/rejoin and host migration. Configure an actual approved built-in content registry and local ignored service keys for interactive testing. External deployment, production load validation and edge abuse limits have not been performed or claimed.
