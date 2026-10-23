# Bounded public catalogue deployment

STATUS: SOURCE GATES PASS; ACTUAL NATIVE EXPORT / DEPLOYMENT PENDING

Verified on 2026-10-23 after commit `35e586d`: Node binding/config mechanics 6/6 PASS; `npm run check` PASS; `tsc --noEmit --project tsconfig.public.json` PASS; actual Worker suite 98/98 PASS in six files (86.65 seconds, start 17:39:11 Asia/Hong_Kong). The sequential command session exited 0. The Worker suite continues to include actual C# workshop fixture HTTP transfer and SQLite retention/retry tests. This is source verification, not a claim that the pending native 36-entry catalogue has been exported or deployed.

Cloudflare's current [Worker limits](https://developers.cloudflare.com/workers/platform/limits/#environment-variables) allow 5 KB per environment variable and 64 variables on Free / 128 on Paid (including secrets). This deployment uses a conservative 4096 UTF-8 byte cap per value and the 64-variable ceiling, reserving the three existing secret bindings. The checked-in production catalogue is still explicitly closed: `BUILTIN_CONTENT_JSON="[]"` (2 bytes). No production catalogue or secrets were changed by this source patch.

`shared/public-bindings.mjs` is the single decoder used by Worker admission and deployment parity checks. Small exact lists stay in `BUILTIN_CONTENT_JSON`. Larger lists replace that value with a canonical manifest `{count,schema,sha256,shards}` and add `BUILTIN_CONTENT_JSON_0` through at most `_7`. Each shard is a canonical array of whole bindings, at most 4096 bytes. The manifest records the exact ordered full-list SHA-256 and count. Decoder checks every full BuiltIn identity, duplicate bindings, shard set, count, digest and size. Missing/extra/noncanonical/mixed shards fail closed. `[]` is valid only without shard keys. Closing a release removes all previous shard keys.

After root's actual four-seed / 36-variant native export, run from `OnlineServices`:

```powershell
node --test checks/public/bindings.nodecase.mjs
node --test checks/public/catalogue.nodecase.mjs
node scripts/project-public-catalogue.mjs --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --output "<new activation artifact>" --write-config wrangler.production.jsonc
node scripts/project-public-catalogue.mjs --catalogue "$env:WORDBOUND_PUBLIC_CATALOGUE" --output "<same activation artifact>" --verify-config wrangler.production.jsonc
```

The activation artifact is schema 2, retaining full-list content and provenance hashes and adding `builtinContentVars`. Config writing replaces only the primary/shard catalogue keys; all other fields remain unchanged. The connector upload helper validates public binding integrity and variable limits before opening the private record. An enabled allowlist additionally requires `--catalogue <actual native export>` and exact parity; its public handoff records both hashes and the count. Update mode still uses `keep_bindings: ["secret_text"]`; it neither emits new secret bindings nor rotates existing keys. Existing `--mode update` remains required for updates. No new Durable Object migration, storage or endpoint is needed.

Native catalogue parity, real Worker admission for every exported binding, live canary, native all-mode and authored-transfer checks remain distinct required release gates. Synthetic binding-unit tests are not native approval evidence.
