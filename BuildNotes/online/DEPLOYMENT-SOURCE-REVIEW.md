# Production update source review

STATUS: BOUNDED SOURCE REVIEW COMPLETE — live readback and deployment NOT RUN.

Reviewed 2026-10-23 against integration HEAD `6ec7e270f5d44dcbc50c7dff0ab72d0afe1b8e54`, with no OnlineServices source changes reported by its scoped status check. Scope: `scripts/prepare-connector-upload.mjs`, public catalogue projection CLI/model, shared public-binding decoder, production configuration, public Node/Worker checks and live canary. No private record was opened or printed, no service was deployed, and no compiler/test job was started for this review.

No additional source blocker was found for the current checked-in configuration and the explicit update procedure. These conclusions depend on the final runtime checks below.

## Confirmed source behavior

- Update mode requires the existing private record, emits no `secret_text` values and no migrations, and uses `keep_bindings: ["secret_text"]`. Plain variables cannot use any of the three protected secret names. Current target account/worker are checked and routes are rejected. The three local Durable Object bindings remain `IDENTITIES/Identity`, `PARTIES/Party`, and `POOLS/MatchPool`.
- Activation schema 3 retains the native content-only list while the approval list/digest binds complete `{content,format,mode}` rows. Sharded manifest schema 2 includes their exact ordered digest and count. Missing/stale/extra/noncanonical shards, invalid metadata and changed identities fail closed. Configuration projection replaces only the public primary/shard variables and preserves unrelated fields.
- The upload payload and `moduleSha256` both derive from the same UTF-8 staged `index.js` string. The enabled allowlist requires an actual native catalogue argument and full metadata parity before the script opens the private record. Update metadata retains current compatibility/observability settings from the checked-in configuration.
- The local binding checks cover 36 synthetic rows, strict decoding, metadata mutation, duplicate identities, size/count limits, stale shard removal, protected plain-variable names and projection CLI idempotence. The actual native catalogue checks separately exercise projection and real Worker approval for each exported binding, including wrong-mode/wrong-format denial.

Cloudflare documents `keep_bindings` as preserving binding types from the previous upload. It does not independently prove that the latest uploaded and currently deployed versions have identical secrets. Record version drift before uploading. The documented local Durable Object binding form uses a class name; no new migration is required by this source patch. [Worker upload API](https://developers.cloudflare.com/api/resources/workers/subresources/scripts/methods/update/), [multipart binding format](https://developers.cloudflare.com/workers/configuration/multipart-upload-metadata/#bindings).

## Required independent runtime evidence

1. Run a fresh successful production dry-run from the final service sources. The helper hashes the stage it reads; it does not establish that the stage is fresh. Stop if bundling fails, then prepare one explicit `--mode update` payload using the verified existing record.
2. Before upload, preserve current deployment/version metadata, exact DO namespace IDs, secret names, public settings and pinned public key. Check whether a newer uploaded version differs from the current deployment. Compare the prepared public handoff with the independently pinned client trust; do not infer key agreement from a local filename.
3. After upload, download the actual module and compare its SHA-256 to the staged `moduleSha256`. Hash the exact `index.js` part if the API returns multipart content, not the MIME wrapper, printed tool serialization, settings response or upload response.
4. Decode the returned `plain_text` primary/shard bindings using the same strict decoder; compare the complete canonical metadata list and exact primary/shard variable set to the activation artifact. Check all 36 entries and all four recorded seeds. A content-only digest is insufficient.
5. Compare pre/post DO namespace IDs, the three secret names, current deployment version, compatibility and logging settings, workers.dev and preview state. The live `/v1/keys` result must match the independently pinned trust. Neither preserved names nor `keep_bindings` alone proves secret values stayed consistent.
6. Execute the live public canary with both success and cleanup required, then the five actual Photon public-mode fixtures and authored transfer fixture. The canary validates one isolated TeamRace/Duo pool; it is not all-mode Photon acceptance.

There is currently no executable remote deployment-readback test in these sources. The six Node checks run the projection CLI, not the complete uploader against private records or Cloudflare. Their green results therefore do not establish secret/namespace preservation or deployed module parity. Root's authenticated independent readback is the required evidence for that boundary; no new implementation was added without root approval.

Use [FINAL-LIVE-CHECKLIST.md](FINAL-LIVE-CHECKLIST.md) for exact command order and bounded Unity/Windows gates. This review does not replace the pending native four-seed export, production readback or release player checks.
