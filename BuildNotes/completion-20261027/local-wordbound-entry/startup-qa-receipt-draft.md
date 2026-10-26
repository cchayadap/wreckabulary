# Startup controls and Windows QA receipt draft

STATUS: CHECKED LOCAL RECEIPT DRAFT. No staging, commit, build or native execution by this lane.

The standalone startup-failure surface now has deferred mouse Back, release-gated Escape and neutral-gated controller Back. Initialized party/game surfaces retain their own input owner. Root's original six-case run 194030 passed 5/6; its lease test advanced iterator turns without actual player-loop frames. The bounded test correction requests ordinary frames and asserts the production quiet interval expires naturally. No production lease state or timeout is bypassed.

Independently read the actual gate stdout/end/XML:

- **194330 startup input: 6/6 PASS**, zero skipped, gate exit 0, 31.6 seconds including 11.6-second refresh. The earlier 5/6 failure remains recorded.
- **194544 combined solo/starter: 4/4 PASS**, zero skipped, gate exit 0, 153.9 seconds including 6.6-second refresh. Cases cover TimeRun and Freestyle installed offline launch/return, safe missing-body return, and the exact Race Circuit starter's three physical laps. Logs contain three solo cleanup reports and one starter cleanup report, all failures=0.

These combined tests prove the documented local routes and starter fixture; they do not finish the full flagship or prove physical device input in a Windows player. The startup decision tests do not load scenes; the separate native missing-body case exercises the actual return handler.

The pending Windows QA source is revision2 `20b0119d...`; both live files match its candidates exactly. Its scenario-owned nested iterator disposal and earlier work budgets address the independently reported cleanup gaps without changing the protected runner. Actual `wordbound_solo` Windows execution/capture review remains pending. Process kills, shorter external deadlines or a synchronous stall across the outer deadline remain cleanup limits; do not call this guaranteed failure cleanup.

`startup-qa-receipt-draft.json` records exact current source/meta hashes, gate facts, XML hashes, cases and preserved failure. A future commit should keep these five startup/QA source files and their required generated metadata separate from the online UI commit. Root should verify hashes again before staging. No BuildNotes folder-wide add, unrelated asset work or protected harness change is proposed.
