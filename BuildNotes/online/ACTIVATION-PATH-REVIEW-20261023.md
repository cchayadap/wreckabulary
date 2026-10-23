# Final catalogue activation source review

STATUS: COMPLETE — bounded source/artifact review, not service or gameplay acceptance. No tests, Unity launch, account query, upload or deployment were performed by this lane.

Reviewed integration at `aac57c0` during native preparation and confirmed the imported/exported checkpoint `0424395`. Root records preparation PASS in 871 seconds, completed 2026-10-23T12:17:48Z, in `Logs/gate_exec_PrepareFromEnvironment_20261023T120315Z.log`. This audit did not independently run that gate. Existing remote secrets, settings and deployment state were not queried. Prior procedures/evidence remain in [FINAL-LIVE-CHECKLIST.md](FINAL-LIVE-CHECKLIST.md), [DEPLOYMENT-SOURCE-REVIEW.md](DEPLOYMENT-SOURCE-REVIEW.md), [NETWORK-ACCEPTANCE-GAPS.md](NETWORK-ACCEPTANCE-GAPS.md) and [RECOVERY-TIMING.md](RECOVERY-TIMING.md).

## Findings

1. **The staged Worker is stale and must be regenerated before upload.** Observed `OnlineServices/.wrangler/production-preview/index.js`: 898,299 bytes, modified UTC `2026-10-20T20:19:43.4861556Z`, SHA-256 `ebff4528a09a832a473ec54ad4a7d8b2e60340422948096d753615ccd9f08a7d`. It predates the metadata approval/shard changes. `prepare-connector-upload.mjs` reads that path without checking source freshness. Its reported module hash accurately identifies the staged text, but does not prove the text was built from current source. Root confirmed that a fresh sequential `production:preview` will replace this historical artifact after the actual-catalogue gates. No additional freshness framework is needed for this bounded release.
2. **No further source blocker was found for the current activation configuration.** The checked-in production config still has explicit `BUILTIN_CONTENT_JSON: "[]"` and no shards; this is a local closed configuration, not a fresh assertion about the remote deployment. Exact metadata projection, fail-closed decoding, update-mode secret preservation and omission of migrations are coherent. These are source findings; authenticated deployment readback remains required.
3. **Four seeds are a release requirement, not a parser invariant.** Export accepts 1–14 distinct seeds; the actual-catalogue gates require all nine variants per seed but do not require exactly four seeds. Root must retain the explicit four-seed/count check and native assignment evidence below. A valid smaller catalogue must not accidentally replace this release's 36 rows.
4. **All-mode admission coverage is broader than native gameplay coverage.** The 36 positive actual-catalogue Worker cases queue each approved binding; they do not perform 36 signed starts or play 36 matches. The five native public wrappers cover only one selected format/Fill path per mode. The missing combinations/outcomes below remain release acceptance work, even if every currently written fixture passes.

## Actual export observed

- `Assets/_Project/Resources/Wordbound/public-catalogue.json`: 16,202 bytes; SHA-256 `4633207d08f8acce18ca3463bb86a71b1f3eaba41a2adfd475b46e9e5826ea23`; release `wordbound-20261023-v1`; 36 rows.
- Seeds: `0000000000000001`, `0000000000000002`, `0000000000000003`, `0123456789abcdef`.
- Each seed has TimeRun/Solo; TeamRace and BrawlRace Solo/Duo/Squad; FreeForAll/Solo; Freestyle/Solo.
- `Resources/Wordbound/LaunchAssets.asset` assigns that TextAsset through GUID `cdfa532f45853204b89bfb7f848964c8`, matching the catalogue metadata, and sets the same release. Observed asset SHA-256: `2f4bb5fc45afffb03a4bb861898771be1f887c250b8edb9948ac4f1f73e6d0fb`.
- These hashes were read during the preparation job, before the final commit. Root's successful preparation/commit now establishes its own export/import record. Recheck the frozen candidate bytes before staging; do not use merely the existence or timestamp of an in-progress file as a gate verdict.

## Required activation order and preservation boundary

1. Freeze the imported candidate and preserve the exact catalogue bytes. `WordboundDeliverySetup.PrepareFromEnvironment` builds/imports detailed assets and authoring defaults, then exports from installed item/rule JSON. `WordboundPublicCourseSetup` rejects changed bytes under a reused release, synchronously imports the JSON, checks the TextAsset text and assigns/saves LaunchAssets. Native public fixtures additionally compare the actual player-assigned catalogue JSON/release with the test input.
2. In the next authorized heavy window, run the actual-catalogue 4 Node cases, 6 binding Node cases, main/public TypeScript checks and expected 41 Worker cases using that exact file. Require observed results, with no skipped cases; do not infer counts as passes. The baseline 100-case evidence remains 95 full-run passes plus the exact five-case repaired rerun, as already documented.
3. Project to a fresh activation artifact and write the production config, then independently verify parity. `public-catalogue.mjs` keeps the original content-only identity while approving exact `{content,format,mode}` rows; `pool.ts` checks mode/format as well as complete content identity. Activation schema is 3; sharded manifest schema is 2. The canonical ordered metadata digest, complete shard set, duplicate rejection and 4,096-byte value limit are shared by deployment and Worker decoding. Projection removes only stale public vars and preserves unrelated config. Review its diff: only the public approval vars should change.
4. Run `npm run production:preview` **after** the final source/config parity check. This script is `wrangler deploy --dry-run --outdir .wrangler/production-preview`; it stages a local bundle, not a deployed preview environment. Stop if it fails. Do not reuse the old artifact above.
5. Prepare with `--mode update --catalogue <exact export> --private-dir <existing protected production record>`. The helper checks exact target account/worker, no routes, variable constraints and catalogue/config parity before opening the record. Update mode never generates replacement keys, emits no `secret_text` values, uses `keep_bindings: ["secret_text"]`, and omits migrations. Payload and handoff module hash derive from the same staged UTF-8 module text.
6. Before upload, root compares public handoff fields to the frozen export/projection and new module hash, and the pinned signing fingerprint `43ea939c715a181912177bff3e98c647d5690ec2b0b40085a8bb6c047ce41dce`. The helper does not prove that the local record matches remote keys; this comparison and authenticated readback are necessary. Never print the stored secret record or private upload payload.
7. Record remote version/deployment, secret names and Durable Object namespace IDs first, then update the existing coordinator once. Current config has only `IDENTITIES/Identity`, `PARTIES/Party`, `POOLS/MatchPool`; the helper emits these same local class bindings. It does not independently pin their namespace IDs or prohibit arbitrary future class edits. Therefore retain the config-diff review and compare the remote namespace IDs before/after; no migration or namespace replacement is part of activation.
8. Read back actual downloaded module bytes, settings, deployment/current version, secret names and workers.dev/preview state. Decode remote plain-text primary/shards with the shared decoder and compare exact canonical metadata rows/digest with activation. Require the new module SHA, unchanged secret-name set and DO IDs, expected compatibility flags/date, disabled logs/traces, query redaction, retained workers.dev and disabled preview. An upload response or local payload is not independent readback evidence.
9. Run the live HTTP canary, then actual Photon/native acceptance. `verify-public-entry.mjs` verifies pinned trust, exact admission/start for two whole TeamRace/Duo parties, and cleanup of every guest's actual membership even after uncertain creation. Require both `success` and `cleanup`. It intentionally uses an isolated `pub-*` pool and the first canonical TeamRace/Duo entry; this is neither a test of production population nor all modes/seeds. Preserve the separate public and private-authored native paths.

Exact command forms, environment gates, 3,600-second cold preparation/build allowance and final stripped-build smoke are in `FINAL-LIVE-CHECKLIST.md`. Root requested the following bounded service sequence next; all remain NOT RUN by this audit:

```powershell
# Run sequentially from integration/OnlineServices; stop at every nonzero exit.
# WORDBOUND_PUBLIC_CATALOGUE is the frozen absolute native export path.
node --test checks/public/catalogue.nodecase.mjs
node --test checks/public/bindings.nodecase.mjs
npm run check
node node_modules/typescript/bin/tsc --noEmit --project tsconfig.public.json
node node_modules/vitest/vitest.mjs run --config public-entry.vitest.config.ts
node scripts/project-public-catalogue.mjs --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --output "$wordboundEvidence/activation.json" --write-config wrangler.production.jsonc
node scripts/project-public-catalogue.mjs --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --output "$wordboundEvidence/activation.json" --verify-config wrangler.production.jsonc
npm run production:preview
node scripts/prepare-connector-upload.mjs --mode update --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --private-dir "$wordboundPrivate"
```

`$wordboundEvidence` must be the designated existing nonsecret release evidence directory, with a fresh activation output. `$wordboundPrivate` must be the verified existing protected production record directory. These are command templates, not authorization for this lane to run deployment or inspect credentials.

## Coverage gaps beyond existing pending native cases

| Required behavior | Existing scope | Missing native/player evidence |
| --- | --- | --- |
| Public TeamRace and BrawlRace Solo/Duo/Squad | Public native wrappers use Duo/4 peers; Worker actual-catalogue checks all approved formats. An eight-peer private TeamRace/Squad fixture exists. | Public Solo and Squad start/play paths, complete format-specific team isolation, and public eight-human behavior. Private Squad is not public allocation evidence. |
| Fill policies | Worker fixtures cover undersized Fill Off, whole-party packing, TimeRun Fill On 3+1, Freestyle Fill Off and Fill On capacity. Native wrappers use race full Duo/Fill Off, TimeRun existing two-person party/Fill Off, Freestyle independent humans/Fill On. | Native mixed-party TimeRun Fill On, existing-party Freestyle Fill Off, and undersized race Fill Off without added teammates or format change. These are missing native combinations, not missing service tests. |
| Four-seed physical play | Worker admission matrix covers all 36 bindings; public native wrappers select `0123456789abcdef`. | Representative physics/route acceptance on the other three exported seeds. Catalogue identity alone cannot prove playability. |
| Complete race outcomes | Generic public fixture exercises canonical bodies, movement, CancelCraft receipt/dedup and checkpoints. | Complete three-lap TimeRun/TeamRace/BrawlRace, correct team finish/isolation, actual Brawl enemy damage/friendly-fire behavior and full puzzle progression. |
| FFA outcomes | Public fixture connects two individual humans and exercises replication. | Native knockouts, respawns, scoring, 10-point finish, eight-minute timeout and draw behavior. |
| Cooperative Freestyle | Public fixture verifies cooperative flags, no timed result and replicated gameplay transport. | Actual attempted-damage rejection and shared station/practice completion, reset and persistent state with multiple humans. Flags alone are not action-level enforcement evidence. |
| Authored maps after host departure | Exact source/SourceHash/Course.Hash transfer fixture uses a private two-person Freestyle map. Separate built-in graceful migration/reconnect fixtures exist. | Combined real authored transfer/play followed by host departure with exact authored content retained; representative authored compatibility in other multiplayer modes. |
| Delivered-player connectivity | Native fixtures use multiple peers in one Editor process and isolated pools. | Actual released Windows player public session and separate-device connectivity; no inference about production population or hardware performance. |

The root plan already tracks full-match outcomes, 2/4/8 clients, RTT/packet loss, hard-loss/repeated migration, recovery timing, eight-player performance, regression and owner usability/visual acceptance. This audit adds no claim that those are newly discovered source defects. It distinguishes absent coverage from already written but unexecuted fixtures. Recovery timing remains measured-from-detection evidence with explicit stimulus labels; injected timeout or graceful shutdown is not packet-blackout acceptance. No production-ready claim is justified by preparation, service admission or source review alone.
