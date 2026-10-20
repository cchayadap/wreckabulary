# Online coordinator lane

STATUS: IN PROGRESS

Base: 12842c75608694f36819edd3bd4dc104ece7f79c
Branch: sethypagna/online
Scope: OnlineServices/, docs/wordbound/ONLINE.md, BuildNotes/online/.

2026-10-20: Read PLAN and updated CONTRACTS. Selected separate SQLite Durable Objects with strict HTTP schemas, authenticated guests, durable party transitions and pool-local atomic admission. Read Cloudflare skills and current official runtime/testing documentation. Current Cloudflare testing uses @cloudflare/vitest-plugin; use actual local Workers runtime tests.

No deployment, account changes, Photon installation or writes to the original checkout.

2026-10-20 first green increment: implemented HTTP schemas, server-minted guests, rotating refresh/access credentials, durable party transitions, SQLite pool transactions, Fill allocation, private/CustomFill routing, signed content/roster/seat grants, staging rollback and reconnect deadlines. `npm run verify` passes TypeScript and 14 Workers-runtime tests. Evidence: verify-first.log. No zero-test or pure-mock substitute.

Dependency policy: selected @cloudflare/vitest-plugin 1.4.4, wrangler 4.151.1 and jose 6.2.12 because newer releases were inside the machine's three-day minimum release age. Kept that policy intact. Local workerd supports compatibility date 2026-10-19, so used that verified date.

Next recoverable action: align team-local slots/non-race format with C# model; extend lifecycle/expiry/durable-retry tests, add pure allocation tests and actual storage/alarm checks. Expected rejected RPCs currently produce noisy runtime diagnostics; investigate result disposal before final handoff.
