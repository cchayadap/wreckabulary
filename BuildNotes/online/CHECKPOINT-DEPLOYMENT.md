# Complete-checkpoint service update

STATUS: DEPLOYED; NATIVE CLIENT RECOVERY VERIFICATION PENDING

At 2026-10-20T14:26:23.612751Z the selected James Cloudflare connection updated only `wreckabulary-wordbound-coordinator` in account `d105a82bc26b6913575355352c2d1bb1`.

The reviewed service source is `43a5f21` (lane `9f23973`): TypeScript and 65 Worker tests passed, with 13 independent actual-source SQLite checks. These are local service checks. They do not prove native gameplay recovery.

The production dry run generated module SHA-256 `5fc20d9b27fb8c06f9ec05ceb598c1b3174c33e7965a55bb36a801c8238d2c40`. Upload returned HTTP 200. Read-back reports deployment `a036a966-7687-4ccb-b07c-ac59aa9ca34a`, version `58e4975a-ef81-440d-b88d-67ae7fa94abe` at 100%.

The update preserves existing secret bindings and omits the initial creation migration. The upload response retains migration tag `v1`; read-back confirms all three original Durable Object namespace IDs and all three secret names. The signing-key fingerprint remains `43ea939c715a181912177bff3e98c647d5690ec2b0b40085a8bb6c047ce41dce`. Logging and tracing are disabled, with query-string redaction enabled. The website Worker `wreckabulary` still has its original modification timestamp, `2026-10-09T06:39:10.260712Z`.

Sanitized API evidence is in `CHECKPOINT-DEPLOYMENT.json`. Private preparation files remain ignored and restricted to the current user. `prepare-connector-upload.mjs` now requires an explicit `initial` or `update` mode; update mode cannot silently generate replacement signing credentials or recreate the SQLite classes.

The first live API rerun passed the pinned public-key check, then encountered a fetch transport failure during guest validation. Subsequent read probes returned the expected 200 for `/v1/keys` and 401 for unauthenticated `/v1/me`. The fresh run passed all seven stages from 14:28:15Z to 14:29:40Z, including pinned keys, guest refresh, Photon one-use tickets, exact whole-party admission for eight players, signed certificates, connection lease/Return and cleanup. All test seats returned and both parties left. Evidence: `CHECKPOINT-API-CHECK.json`. No deployment retry was made.

The seven-stage live suite exercises the pre-existing service API. It does not yet exercise the new complete-checkpoint upload or native recovery. Next: integrate the reviewed C# session cutover, then run real authenticated two-player gameplay and host-loss cases against the new checkpoint routes.
