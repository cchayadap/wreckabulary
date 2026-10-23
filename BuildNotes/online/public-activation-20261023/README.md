# Public catalogue activation

STATUS: DEPLOYMENT, READBACK AND LIVE HTTP CANARY PASS. NATIVE PHOTON GAMEPLAY NOT RUN.

On 2026-10-23 the isolated Wordbound coordinator was updated with the exact Unity-exported `wordbound-20261023-v1` catalogue: 36 entries covering nine mode/format variants and four seeds. The existing Wreckabulary website was not changed.

- Worker version: `05384420-7e14-40c2-b965-0e8a91681df7`.
- Deployment: `3b87e4ba-1933-44f4-85c7-52d972d80a0f`, 100%.
- Catalogue SHA-256: `4633207d08f8acce18ca3463bb86a71b1f3eaba41a2adfd475b46e9e5826ea23`.
- Module SHA-256: `3329fc54ebe2373c1f50a776b6731f243127fdcb24a502376102af8ef0be55f5`.

`pre-deployment.json` and `post-deployment.json` record public deployment metadata. `verify-readback.mjs` compares the fetched module and canonical decoded bindings to the staged files; `readback-verification.json` records its successful check. Existing Durable Object namespace IDs, signing-key fingerprint and secret names were preserved. Secret values are absent. The settings endpoint omits observability, so readback does not independently prove those omitted settings; the submitted configuration explicitly disables logs/traces and redacts query strings.

Pre-deployment verification used the actual native catalogue: Node 10/10, Worker 41/41 (zero pending), both TypeScript checks and fresh production preview passed. `worker-results.json` preserves the raw Worker result. Root-local command logs are retained under `.harness/delivery/public-*` and `native-catalogue-worker.json`.

`public-live.json` records the live HTTP canary from 13:27:44Z to 13:28:13Z: four scripted authenticated guest identities in two intact Duo parties received exact public allocation, content admission and verified signed start certificates. The test used an isolated pool and successfully cleaned up every test party, with zero unresolved party IDs. It did not connect Photon clients or measure the public player population.

Next: execute the nine authenticated native public fixtures, then private custom-map transfer, recovery, full-match and Windows-player acceptance. Do not infer these outcomes from HTTP admission.
