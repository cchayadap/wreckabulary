# Bounded public catalogue deployment

STATUS: MODE/FORMAT APPROVAL REPAIR PREPARED; CURRENT GATES / NATIVE EXPORT / DEPLOYMENT PENDING

Historical gate evidence, before the mode/format approval repair: verified on 2026-10-23 after commit `35e586d`, Node binding/config mechanics 6/6 PASS; `npm run check` PASS; `tsc --noEmit --project tsconfig.public.json` PASS; actual Worker suite 98/98 PASS in six files (86.65 seconds, start 17:39:11 Asia/Hong_Kong). The sequential command session exited 0. This evidence is retained and does not validate the later metadata repair in `2742c47`. The Worker suite includes actual C# workshop fixture HTTP transfer and SQLite retention/retry tests. The final native 36-entry catalogue has not yet been exported or deployed by this lane.

Cloudflare's current [Worker limits](https://developers.cloudflare.com/workers/platform/limits/#environment-variables) allow 5 KB per environment variable and 64 variables on Free / 128 on Paid (including secrets). This deployment uses a conservative 4096 UTF-8 byte cap per value and the 64-variable ceiling, reserving the three existing secret bindings. The checked-in production catalogue is still explicitly closed: `BUILTIN_CONTENT_JSON="[]"` (2 bytes). No production catalogue or secrets were changed by this source patch.

`shared/public-bindings.mjs` is the single decoder used by Worker admission and deployment parity checks. Each approved row now contains exactly `{content,format,mode}`, sourced from the actual native catalogue. The nested six-field BuiltIn Content is unchanged. Worker admission first matches its full identity, then rejects a different request mode or format with `CONTENT_SETTINGS_MISMATCH`. This check preserves isolated test pool versions and does not copy production mode hashes into code.

Small canonical metadata lists stay in `BUILTIN_CONTENT_JSON`. Larger lists replace that value with a canonical schema-2 manifest `{count,schema,sha256,shards}` and add `BUILTIN_CONTENT_JSON_0` through at most `_7`. Each shard is a canonical array of whole metadata rows, at most 4096 bytes. The manifest records the exact ordered metadata-list SHA-256 and count. The decoder checks the permitted mode/format pair, every full BuiltIn identity, duplicate content identities even under different metadata, shard set, count, digest and size. Nonempty old content-only lists, old schema-1 manifests, missing/extra/noncanonical/mixed shards and tampered metadata fail closed. `[]` remains valid only without shard keys. Closing a release removes all previous shard keys.

After root's actual four-seed / 36-variant native export, run from `OnlineServices`:

```powershell
node --test checks/public/bindings.nodecase.mjs
node --test checks/public/catalogue.nodecase.mjs
node scripts/project-public-catalogue.mjs --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --output "<new activation artifact>" --write-config wrangler.production.jsonc
node scripts/project-public-catalogue.mjs --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --output "<same activation artifact>" --verify-config wrangler.production.jsonc
```

The current activation artifact is schema 3. `builtinContentJson` retains the content-only list for direct native identity comparison; `publicBindingsJson` contains the complete mode/format/content rows and `bindingsSha256` hashes that canonical metadata list. `builtinContentVars` is the exact bounded deployment projection. Config writing replaces only the primary/shard catalogue keys; all other fields remain unchanged. The connector upload helper validates public binding integrity and variable limits before opening the private record. An enabled allowlist additionally requires `--catalogue <actual native export>` and exact metadata parity; its public handoff records both hashes and the count. Update mode still uses `keep_bindings: ["secret_text"]`; it neither emits new secret bindings nor rotates existing keys. Existing `--mode update` remains required for updates. No new Durable Object migration, storage or endpoint is needed.

The metadata repair adds real Worker rejection cases for wrong mode and wrong format, with no admitted attempt, and projection/shard/parity coverage for metadata changes. Gates are pending while the asset lane holds the heavy slot. Native catalogue parity, real Worker admission for every exported binding, live canary, native all-mode and authored-transfer checks remain distinct required release gates. Synthetic binding-unit tests are not native approval evidence. Follow [FINAL-LIVE-CHECKLIST.md](FINAL-LIVE-CHECKLIST.md) for the current bounded command sequence and deployment/readback criteria.
