# Public catalogue verification

STATUS: PREPARED SOURCE; ACTUAL FINAL EXPORT AND EXECUTION PENDING.

All positive inputs come from the native `WordboundPublicCourseSetup.ExportAndAssignFromEnvironment` exporter. Do not create substitute hashes or edit the exported document. Each seed has nine variants on the current `wordbound-v3` protocol. Use a fresh release for changed content, and include seed `0123456789abcdef` for the native gameplay fixtures. The native fixture requires byte-for-byte parity with the catalogue assigned to the player launch assets. The Worker continues to use its existing flat allowlist; the projection preserves exact content fields and records source and projection SHA-256.

From `OnlineServices`, set `WORDBOUND_PUBLIC_CATALOGUE` to the absolute native export path, then run these gates sequentially when the root releases the compiler:

```powershell
node --test checks/public/catalogue.nodecase.mjs
npx tsc --noEmit --project tsconfig.public.json
npx vitest run --config public-entry.vitest.config.ts
node scripts/project-public-catalogue.mjs --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --output "<new activation artifact>"
```

The projection CLI optionally accepts `--verify-config <strict JSON config>` to compare that config's `vars.BUILTIN_CONTENT_JSON` against the exact projection. It neither modifies config nor deploys. A preexisting output is accepted only when its bytes already match; a differing release requires a new output.

The local Worker suite uses actual SQLite Durable Objects with temporary signing/access keys generated only in memory. It checks all nine exact bindings, altered/private binding rejection, two full Duo parties, eviction, signed start and cleanup. These tests exercise the installed Worker runtime, not Photon or native gameplay.

Only after the root approves and deploys the exact allowlist:

```powershell
node scripts/verify-public-entry.mjs --live --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --evidence "<new nonsecret evidence file>"
```

This creates four disposable guests in two Duo parties, queues the exact approved TeamRace binding, verifies the public signing key and signed admission/start, then returns, cancels and leaves. Tokens remain in memory. The pool is `pub-<runid>`, while the evidence separately records the actual approved release and catalogue hash. It never tests production population or cross-device gameplay. Main requests are bounded to 180 seconds, individual requests to 15 seconds, and cleanup to 90 seconds. Any unresolved cleanup makes the run fail and records only nonsecret party IDs.

The native category `ExternalPhotonPublicGameplay` contains five named cases on `WordboundNativeGameplayTests`: `PublicTeamRacePreservesWholeDuoParties`, `PublicBrawlRacePreservesWholeDuoParties`, `PublicTimeRunRequiresTwoRealHumans`, `PublicFreeForAllAllocatesTwoIndividuals`, and `PublicFreestyleFillsTwoRealHumansWithCoopRules`. Each has a600s NUnit timeout and the shared8min operation lifetime. They additionally require `WORDBOUND_EXTERNAL_GAMEPLAY=1` and `WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY=1`, real configured Photon custom authentication, installed native body prefabs and the same exported catalogue. Root alone runs Unity.

These cases exercise actual session/controller/body/input/command/checkpoint paths in isolated pools on one machine. TeamRace/Brawl use two whole Duo parties. TimeRun uses a two-human FillOff party; its independent FillOn3+1 packing is a separate Worker regression. Freestyle uses independently queued humans and verifies cooperative mode flags/result, without claiming attempted damage prevention or completed practice. Generic CancelCraft receipts prove transport/deduplication; successful gameplay actions and HUD selection are separate native fixtures. Teardown failures fail the run.

No command in this document has been executed for the pending final v3 catalogue.
