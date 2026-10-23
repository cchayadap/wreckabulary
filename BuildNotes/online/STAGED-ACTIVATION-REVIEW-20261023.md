# Staged public activation review

STATUS: SOURCE/STAGING REVIEW PASS — no blocking finding in the inspected candidate. No deployment, API call, private secret read or heavy test job in this lane. This is not live acceptance.

Root reports actual catalogue Node 10/10, both TypeScript checks and Worker 41/41 with zero skipped; fresh production preview passed. This review independently checks staged catalogue/configuration/bundle parity and update boundaries. Root owns upload preparation and deployment.

Reviewed integration HEAD `244e27e612bb5e8f8b2f6f249f1c81994f5f4f24` plus the uncommitted `OnlineServices/wrangler.production.jsonc` activation delta on 2026-10-23. The audit used read-only file parsing, hashes, source-map comparison and `git show`; it did not rebuild or run test suites. The unrelated art pilot was not inspected or changed.

## Exact local parity

| Artifact | Verified value |
| --- | --- |
| Native resource | `Assets/_Project/Resources/Wordbound/public-catalogue.json` |
| Release | `wordbound-20261023-v1`, protocol `wordbound-v3` |
| Native SHA-256 | `4633207d08f8acce18ca3463bb86a71b1f3eaba41a2adfd475b46e9e5826ea23` |
| Metadata bindings SHA-256 | `4aed331375f99caa0701209e4f783938d4baefe0de999202e2452ace1748af90` |
| Activation artifact | `BuildNotes/online/public-activation-20261023/activation.json`, schema 3, 47,747 bytes |
| Fresh service module | `OnlineServices/.wrangler/production-preview/index.js`, 918,845 bytes |
| Module SHA-256 | `3329fc54ebe2373c1f50a776b6731f243127fdcb24a502376102af8ef0be55f5` |
| Module timestamp | `2026-10-23T13:05:39.368Z` |
| Source-map SHA-256 | `4d97c31022085c98ace6d6eddd4ffce67d4b0fcbaba25d771f47f7e75fbd7ab5` |

The activation file is byte-for-byte equal to `canonical(activationProjection(readPublicCatalogue(nativePath)))`. `requireConfigParity` and strict `readPublicBindings` decode the staged config to exactly the same ordered metadata rows. The list contains all nine mode/format combinations, four rows each: TimeRun/Solo; TeamRace and BrawlRace in Solo, Duo and Squad; FreeForAll/Solo; Freestyle/Solo. Every variant has exactly these four seeds: `0000000000000001`, `0000000000000002`, `0000000000000003`, `0123456789abcdef`.

The schema-2 primary shard manifest declares 36 rows and four shards. UTF-8 lengths: primary 110 bytes; `_0` 3805; `_1` 3771; `_2` 3797; `_3` 3799. Each is below the approved 4096-byte threshold. There are no extra shard variables. `LaunchAssets.asset` references the actual resource GUID `cdfa532f45853204b89bfb7f848964c8` and the matching release string.

The retained Worker JSON `.harness/delivery/native-catalogue-worker.json` independently reports success, 41 total/41 passed/0 failed/0 pending. Node and TypeScript pass counts above are root-reported; this review did not rerun them.

## Configuration and upload boundary

After removing only `BUILTIN_CONTENT_JSON` and `_0` through `_7`, recursive canonical comparison of the working config against `git show HEAD:OnlineServices/wrangler.production.jsonc` is identical. Only the formerly closed `[]` primary and four new shard values differ. Other diff lines are formatting.

The target remains account `d105a82bc26b6913575355352c2d1bb1`, worker `wreckabulary-wordbound-coordinator`; no `route` or `routes` is present. Compatibility date `2026-10-19`, `nodejs_compat`, workers.dev enabled, preview URLs disabled and all observability/logging/tracing disabled with query redaction remain unchanged. Durable Object bindings remain `IDENTITIES/Identity`, `PARTIES/Party`, `POOLS/MatchPool`; the existing local v1 migration definition is unchanged. Token issuer, signing-key ID and Photon app ID are unchanged. No secret is stored in this public config.

Read-only inspection of `scripts/prepare-connector-upload.mjs` confirms update mode requires the existing secret record, cannot create a new signing key, excludes secret values from upload bindings, sets `keep_bindings: ["secret_text"]`, and emits no migration. It uses the exact staged module file and verifies public catalogue/config parity before preparation. The update payload keeps the existing Durable Object binding names/classes; remote namespace identity must still be checked by root's authenticated readback. This reviewer did not open `production.json`, `upload.json.gz` or any private directory.

Root subsequently reported its prepared update handoff has the same module hash/size, 36 bindings, native/binding hashes and unchanged pinned public fingerprint. Root also reported authenticated predeployment snapshots of the old module/version, three secret names, existing Durable Object IDs and unchanged website timestamp. These are root's remote observations, not a remote query by this reviewer.

## Current source in the fresh module

All ten first-party runtime TypeScript files (`model`, `auth`, `rpc`, `checkpoints`, `bundles`, `store`, `identity`, `party`, `pool`, `index`) and `shared/public-bindings.mjs` embedded in the fresh source map match current disk sources byte-for-byte. The only extra file in `src` is the generated type declaration, which is not runtime code. This closes the earlier stale-preview concern for this candidate.

The bundle itself contains the strict metadata/shard decoder and the pool's `approved.mode === settings.mode && approved.format === settings.format` admission check, in addition to full content-identity matching. Its exports retain `Identity`, `MatchPool`, `Party` and the fetch entry point. The fresh map/source parity plus root's successful preview identifies this module; no new bundle was generated by the review.

## Remaining release checks

- [x] Native catalogue, activation projection, shard configuration and Unity resource reference agree locally.
- [x] Unrelated configuration, update-mode secret policy and migration boundary remain unchanged.
- [x] Fresh staged module contains the current service sources, including metadata approval and authored-bundle handling.
- [ ] Root uploads this exact prepared update and independently downloads the deployed module/config.
- [ ] Root confirms module SHA, canonical metadata bindings, current version/deployment, secret names, Durable Object namespace IDs and unchanged website state after deployment.
- [ ] Root executes live HTTP acceptance, nine public mode/format native cases, authored workshop transfer and the remaining network/player acceptance.

Any later item/rule/catalogue-affecting source change needs a fresh native export and parity assessment before using this activation. A pass here does not establish deployed behavior, Photon availability, rendered editor usability, recovery timing or Windows player acceptance.

