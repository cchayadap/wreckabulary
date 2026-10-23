# Metadata approval verification

STATUS: BOUNDED SOURCE VERIFICATION COMPLETE — native catalogue, deployment, live Photon and Windows acceptance remain pending.

Date: 2026-10-23, Asia/Hong_Kong. Worktree: `wordbound-online`, metadata implementation `2742c47`, documentation baseline `e6cef49`, plus the checkpoint test repair committed with this report. Root owns deployment and native execution. The heavy slot was released to root after the targeted runner exited 0; this lane started no further compiler, Worker or Unity jobs.

## Results and limits

| Check | Result |
| --- | --- |
| Metadata binding/config Node checks | 6/6 PASS, 758.8846ms |
| Main TypeScript check | PASS, before the final test-only repair |
| Public TypeScript check | PASS in the sequential gate run, before the final test-only repair; no separate public-check log file retained |
| Complete Worker suite | 95 PASS / 5 FAIL, six files, exit 1; start 18:19:54, duration 489.35s |
| Exact five failed cases, after test repair | 5 PASS / 67 skipped by explicit selection, two files, exit 0; start 18:31:23, duration 254.23s |
| Actual native four-seed catalogue Node/Worker cases | NOT RUN; native export not yet available |
| Deployment/readback, live API, native public/workshop, Windows | NOT RUN by this lane |

All 100 unique baseline Worker cases now have passing evidence across the two Worker runs. This is not a clean 100-case suite pass. The two new actual Worker wrong-mode/wrong-format rejection cases passed in the complete run. Production service files did not change between the complete run and the selective rerun. The only repair was in `test/checkpoints.test.ts`; its revised TypeScript was transpiled and executed by the passing rerun, but a separate post-repair typecheck was not repeated after returning the heavy slot.

The complete run's failures were:

- `bounds orphan upload count and removes uncommitted chunks on the actual alarm`: two rows correctly remained because `setup` time plus 61 seconds could precede the actual uploads' persisted expiry on a slow run. The test now reads the real maximum `expiresAt` from SQLite, advances to that exact inclusive boundary, requires the real Durable Object alarm to run, and still requires zero upload and chunk rows.
- `packs whole Fill On parties after solo-first arrival would fragment both teams`: 20-second harness timeout.
- `starts TimeRun with the whole Fill Off party or fills to four with consent`: 20-second harness timeout.
- `does not allocate random opponents into a private empty team`: 20-second harness timeout.
- `preserves grace across repeated disconnect reports and rejects reclaim at 120s`: 20-second harness timeout.

The targeted command used one Worker and a 60-second test harness allowance. No production expiry, reconnect, allocation, retention or request deadline changed. The other four tests were unchanged. The run spent 61.26 seconds transforming and 98.46 seconds importing; passing test execution totaled 60.55 seconds. No unrelated machine workload was stopped.

Vite printed its nonfatal future native-loader warning for the extensionless `./test/fixtures/public-content` import. The current pinned tooling compiled and ran it. Expected denied-request diagnostics also remain in the raw logs; the final runner results above determine these checks' outcomes.

## Commands

Run sequentially from `wordbound-online/OnlineServices`; the logs below are local ignored evidence. Stop on a nonzero command unless explicitly investigating that failure.

```powershell
node --test checks/public/bindings.nodecase.mjs
npm run check
node node_modules/typescript/bin/tsc --noEmit --project tsconfig.public.json
node node_modules/vitest/vitest.mjs run
```

The complete Worker run failed as retained above. After the bounded alarm test repair, the authorized selective rerun was:

```powershell
node node_modules/vitest/vitest.mjs run test/checkpoints.test.ts test/service.test.ts --maxWorkers 1 --testTimeout 60000 --testNamePattern 'bounds orphan upload count|packs whole Fill On parties after solo-first|starts TimeRun with the whole Fill Off party|does not allocate random opponents into a private empty team|preserves grace across repeated disconnect' *> ../.harness/metadata-gates/20261023-final/focused-rerun.log
```

## Retained raw evidence

Paths are relative to the online worktree. Hashes are SHA-256 of the complete files after their processes exited.

| File under `.harness/metadata-gates/20261023-final/` | SHA-256 |
| --- | --- |
| `node.log` | `4ec5204f932c05ec51dba61771ea230f226432c39d2822ea71b25117f1f54351` |
| `typescript.log` | `00cef6d1f0c445d6bbdd58cdfed9000d8f22857276e60cc91eafdef604e1e39f` |
| `worker.log` | `9b80aaa50b6e313f3e76c4cbe032e2352b0cef469bfb6b668d11058f155c7eca` |
| `focused-rerun.log` | `ebc05b017b659d38a0f41afaa15556839590bfedd6956aecc3f0c9c894aac783` |

The earlier 98-case pre-metadata suite is separately retained in [PUBLIC-BINDING-SHARDS.md](PUBLIC-BINDING-SHARDS.md). It does not substitute for these newer metadata checks.

## Next recoverable action

Root integrates this test/evidence commit, runs the affected final assembled checks and exports the actual four-seed / 36-row catalogue through the Unity gate. Follow [FINAL-LIVE-CHECKLIST.md](FINAL-LIVE-CHECKLIST.md) for the exact native Node/Worker checks, schema-3 activation / schema-2 shards, staging, preserved-secret update, independent deployment readback, live canary, five-mode native public checks, authored transfer, Unity editing and exact QA/stripped-release smoke commands. No production-ready or final-build claim is supported yet.
