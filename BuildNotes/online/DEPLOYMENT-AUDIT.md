# Coordinator deployment audit

STATUS: DEPLOYED; LIVE API VERIFIED; PHOTON DASHBOARD/NATIVE HANDSHAKE PENDING ROOT

2026-10-20 authorization: root confirmed that the user connected the selected Apps account and authorized deployment of the prepared separate Wordbound coordinator. This lane owns the service deployment and live API checks; root owns Unity and Photon dashboard changes.

Preflight through the newly selected Cloudflare connection:

- Exact account: `d105a82bc26b6913575355352c2d1bb1`.
- Worker inventory returned HTTP 200. `wreckabulary-wordbound-coordinator` is absent.
- Existing website Worker `wreckabulary` was last modified `2026-10-09T06:39:10.260712Z`; it will not be updated by this deployment.
- Account workers.dev subdomain returned HTTP 200: `pagna`.
- Source checkpoint: `58a18d4`; production config was locally dry-run validated.
- Planned write: create only `wreckabulary-wordbound-coordinator` with the bundled module, exact config, three new SQLite classes and atomic secret bindings. Then enable only that Worker's workers.dev endpoint with preview URLs disabled.
- Secret material will be generated into ignored `.harness/online-secrets`, restricted to the current Windows user, and never printed or committed.

Next recoverable action: prepare the protected upload payload, create the Worker through the selected connector, inspect the result before any retry, then verify the live public key and private-session API.

## Applied write and read-back

At `2026-10-20T12:51:07.297206Z`, the selected connector returned HTTP 200 for the new module upload with all public variables, three secret bindings and the `v1` SQLite migration applied together. Only the new Worker's workers.dev endpoint was enabled, with preview URLs disabled.

- Origin: `https://wreckabulary-wordbound-coordinator.pagna.workers.dev`
- Deployment ID from the deployment listing: `971d0df1-256b-4c10-ba65-b52077c209a3`
- Version ID: `cf457313-a6e7-4482-a6cc-26a2ff853ac4`, at 100%
- Exact bundled module SHA-256: `7ece61c9f933b5560621a065e1f69472bf2e1d04a29f471bd1191d2d19f5d122`
- Signing key: RSA 3072, key ID `wordbound-rs256-20261020-01`
- Public JWK thumbprint input SHA-256: `43ea939c715a181912177bff3e98c647d5690ec2b0b40085a8bb6c047ce41dce`

The upload response's legacy `deployment_id` held the version identifier without hyphens; the deployment listing above supplies the distinct canonical deployment ID.

Settings read-back confirms the expected compatibility date/flag, `secret_text` for all three secrets, disabled invocation logs and disabled traces. Namespace IDs: Identity `24340c3b888f4e378f55ab8cf4c94eef`, Party `3efc2e405746416ab8474c1d2965417c`, MatchPool `5e33c688204e44cc816d165d119ddf35`. Website `wreckabulary` retains its original modification timestamp.

## Live evidence and handoff

The real HTTPS test ran from `12:53:46Z` to `12:54:36Z`, with all seven stages passing:

- Live public key exactly matches the independently generated public key.
- Guests receive server identities; caller identity fields fail; refresh rotates and revokes the old access token.
- The real Photon provider HTTP endpoint accepts one valid ticket and rejects replay, wrong app and caller-supplied identity.
- Two private parties of four remain intact on separate teams; the empty public allowlist rejects unapproved Quick Play content.
- All eight members accept the same content/roster before a signed match-start certificate becomes available.
- Connection tickets are single-use, exact retries are idempotent, outsiders fail, and Return revokes the lease permanently.
- All eight test seats returned and both parties left. No active test match or queued test party remains.

The private workshop content was explicitly an API fixture; this does not claim a validated playable course. Evidence is `LIVE-API-CHECK.json` and `verify-live-coordinator.log`; public deployment/key data is in `LIVE-SERVICE.json`. `OnlineServices/production-client.json` is the committed nonsecret client trust configuration.

Only current-user-protected ignored files contain secrets. The requested `photon-provider-setup.txt` contains the provider URL, app ID and provider secret plus exact dashboard steps, without RSA/access secrets. It instructs the operator to wait for root's anonymous native probes before changing Photon settings.

Next recoverable action: root completes its current native probes, configures the Photon Custom provider, then runs the actual authenticated Runner identity/admission tests. Approved built-in content bindings and full native gameplay/migration gates remain pending. No Photon dashboard setting or global Wrangler credential changed in this lane.
