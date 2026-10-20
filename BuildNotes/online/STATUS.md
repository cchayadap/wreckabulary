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

2026-10-20 Photon provider increment: implemented authenticated 60-second, single-use app/audience-bound tickets, dashboard-secret verification and documented Photon response codes. TypeScript and 41 local tests pass, including concurrent consumption, expiry, refresh revocation, wrong app/audience, malformed data and supplied-identity rejection. Evidence: verify-photon.log. Official Fusion 2 and Realtime 5 docs read; no dashboard/account/deployment changes. Automatic invocation logs disabled to avoid recording dashboard query secrets.

Next recoverable action: finish the independent C# token verifier and coordinator request/client contracts; compile and exercise their cryptographic, canonical hash and HTTP boundary tests. New C# files are currently in progress and are not yet Unity-tested.

2026-10-20 C# client increment: added engine-free strict online JSON, immutable content/settings/seat contracts, RS256/JWK verifier, bounded authenticated coordinator client, and a separate HTTPS HttpClient adapter. Eleven focused C# regressions pass (verify-csharp.log), including fencing a late authentication response after clearing credentials. Rules and the real HTTP adapter compile for netstandard2.1 with zero warnings/errors (verify-transport-compile.log). Verification covers cryptographic tampering, wrong issuer/audience/time, malformed domains, expected content/roster, Photon identity/secret binding and idempotency-key preservation.

2026-10-20 interoperability proof: independent JavaScript JOSE-generated RSA tokens, canonical roster hash and connection-secret hash pass C# verification. Actual local HTTP client/server requests verify bearer headers, preserved operation keys and JSON body, HTTPS-by-default, redirect refusal and response size limits. Evidence: verify-interop.log. Generated vectors are ignored; no private key is written to disk.

Final C# gate: `dotnet run --project Tools/RulesHarness/RulesHarness.csproj` reports 260 passed, zero failed (verify-rules-all.log), including eleven new online tests. The interop runner remains green after the final credential-lifecycle changes. No Unity instance was launched by this lane.

Next recoverable action for root: integrate this lane, run native Unity tests, then attach the actual Fusion callbacks. Obtain a fresh auth ticket per Runner authentication, get the provider-verified player ID from the server-side Fusion API, and only assign gameplay/input authority after the signed match/seat/secret verification. Cloud deployment, dashboard provider configuration, native/IL2CPP and live authenticated Photon sessions remain NOT RUN.
