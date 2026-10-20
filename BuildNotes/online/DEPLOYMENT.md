# Selected-account deployment package

STATUS: DEPLOYED AND LIVE API VERIFIED; PHOTON DASHBOARD CONFIGURATION PENDING

The user's selected account is `d105a82bc26b6913575355352c2d1bb1`, matching the existing website. The deployment configuration is `OnlineServices/wrangler.production.jsonc`. It created the separate `wreckabulary-wordbound-coordinator` Worker and its own SQLite Durable Object namespaces `Identity`, `Party` and `MatchPool`. It has no website route, custom domain, Pages binding or shared database. Verified origin: `https://wreckabulary-wordbound-coordinator.pagna.workers.dev`. See `DEPLOYMENT-AUDIT.md` for exact version, public-key and live API evidence.

The user supplied a new connector with verified access to the selected Apps account; that connector performed this deployment. The inspected CLI account `743e5b727d139e85ed11679097f6f99e` and previous connector account `4e185f32352a5f1a5965ea16965e8a79` remain unrelated and unchanged. Do not use them for updates. No global credential, billing plan or Photon provider setting was changed.

## Local preparation

Run from `OnlineServices`:

```text
npm ci
npm run verify
npm run production:preview
```

The last command uses the installed pinned Wrangler to validate and bundle the production configuration with `--dry-run`; it does not upload. Output is ignored under `.wrangler/production-preview`. Local `wrangler.jsonc` and test configuration remain unchanged. If dependency installation is already current, do not repeat it.

The production variables are concrete:

| Variable | Value |
|---|---|
| `TOKEN_ISSUER` | `wreckabulary-wordbound` |
| `SIGNING_KEY_ID` | `wordbound-rs256-20261020-01` |
| `PHOTON_APP_ID` | `f6fb7f4c-b8a9-44a3-ac63-bee1c5f23552` |
| `BUILTIN_CONTENT_JSON` | `[]`, intentionally no Quick Play approval yet |

An approved content export must replace the empty allowlist before Quick Play release. It is a JSON array serialized into this string variable. Each entry has exactly `{kind:"BuiltIn",courseId,courseHash,manifestHash,modeHash,catalogHash}`; all four hashes are 64-character lowercase SHA-256 strings from the exact client content. Do not copy test hashes or infer them from filenames. The empty registry still permits guest/Photon authentication and explicit private workshop acceptance; it is not a complete gameplay rollout.

## Secrets and publish operation

Provision these three values through protected secret storage after selected-account access is established:

| Secret | Required material |
|---|---|
| `ACCESS_SECRET` | Independent cryptographically random 32-byte value encoded as 43-character base64url, or stronger. |
| `PHOTON_PROVIDER_SECRET` | A different random 32-byte base64url value; shared only with the Photon provider's static dashboard parameter. |
| `ADMISSION_PRIVATE_JWK` | A JSON-serialized RSA private JWK for RS256 with a 2048-bit or larger modulus and exponent 65537. Keep all private CRT parameters; use the public counterpart for client verification. |

Production secrets were generated once into ignored `.harness/online-secrets/production.json` with current-user-only Windows directory permissions and uploaded atomically with code. Do not store secrets in source, Unity assets, shell arguments, tool output or command history. The protected JSON file's three values are strings; `ADMISSION_PRIVATE_JWK` is itself a serialized JSON string. The published endpoint `/v1/keys` returns only the public JWK with the configured key ID. Changing the signing key currently replaces the single advertised key; coordinate rotation with all clients and existing certificates.

After access and the external publish action are authorized, verify the selected profile's account inventory before upload. Use that same profile on every subsequent command. Replace `SELECTED_ACCOUNT_PROFILE` with the verified profile name and the secrets path with the protected file's real path:

```text
npx wrangler whoami --profile SELECTED_ACCOUNT_PROFILE
npx wrangler deploy --config wrangler.production.jsonc --profile SELECTED_ACCOUNT_PROFILE --secrets-file "ABSOLUTE_PROTECTED_SECRETS_JSON_PATH"
```

Before this command, inspect any process-scoped `CLOUDFLARE_ACCOUNT_ID` override and require it to be absent or the exact selected account. Do not mutate global environment settings. If the Worker name already exists, inspect ownership and current code before updating it. A successful upload must record the actual account, Worker version/deployment ID and HTTPS origin, with no secret values.

Wrangler supports uploading secrets and code together via `--secrets-file`; this avoids publishing an intentionally half-configured provider. The command and config fields were checked against the installed 4.151.1 help/schema and current official [Worker commands](https://developers.cloudflare.com/workers/wrangler/commands/workers/), [secret configuration](https://developers.cloudflare.com/workers/configuration/secrets/) and [Wrangler configuration](https://developers.cloudflare.com/workers/wrangler/configuration/).

SQLite Durable Objects are available on Workers Free; usage limits and the account's current plan still need inspection before any paid rollout. This preparation does not upgrade a plan or authorize charges. See the official [Durable Objects pricing](https://developers.cloudflare.com/durable-objects/platform/pricing/).

## Photon provider configuration

Configure only the already-approved Fusion 2 app above after the live coordinator is verified:

1. Custom provider URL: the actual deployed HTTPS origin plus `/v1/photon/auth`.
2. Static query parameter `appId`: the approved app UUID.
3. Static query parameter `providerKey`: the protected `PHOTON_PROVIDER_SECRET`. Keep this out of clients and recorded URLs.
4. Disallow anonymous clients and disallow connections when custom authentication is unavailable.
5. The client uses `CustomAuthenticationType.Custom` with POST data `{ticket}` from authenticated `POST /v1/photon/tickets`. It never submits a trusted user ID or the provider secret.

The service accepts exactly those two static query values and one JSON body field. The authoritative Photon `UserId` must equal the server-minted coordinator UUID. Invalid, expired, reused or wrong-app tickets must fail. Dashboard setup remains NOT RUN. Official contracts: [Fusion 2 custom authentication](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/authentication/custom-authentication), [Realtime 5 custom authentication](https://doc.photonengine.com/realtime/v5/connection-and-authentication/authentication/custom-authentication).

Keep automatic invocation logs disabled: provider secrets are static query values. Do not enable a tail/proxy/request capture that records full provider URLs, tokens, authorization headers or admission messages.

## Verification after an authorized deployment

- Read `/v1/keys` over the pinned HTTPS origin; verify the expected key ID, RSA modulus size and public-key fingerprint against the independently prepared public key.
- Configure the client bootstrap with that exact HTTPS origin, issuer `wreckabulary-wordbound`, key ID `wordbound-rs256-20261020-01` and independently verified public key. Construct `OnlineAdmissionVerifier` from those trusted values. A successful upload does not configure client trust; never accept an origin or signing key supplied in an unverified match/JWT.
- Create two test guests and obtain fresh one-use Photon tickets. Preserve credentials only in protected process/storage state; output only pass/fail and non-secret evidence.
- Join the approved private test room through actual Photon custom authentication. On the host compare `GetPlayerUserId(peer)` with each guest's coordinator UUID. A client-supplied arbitrary ID must not replace it.
- Reject a replayed/expired ticket, wrong app/provider value and missing authentication. Run a negative provider-unavailable connection check under the integrator's controlled test settings.
- Exercise the exact start certificate, all-seat readiness, per-Runner host proof, lease renewal/expiry and Return revocation through the real adapter. A successful HTTP request or basic Photon room join is not this gate.
- Run the native carrier/cloud migration cases in `NATIVE-MIGRATION-REVIEW.md` and the full dynamic-body test. Only then enable gameplay claims.

Current boundaries: approved built-in registry, configured Photon provider, actual Photon custom-auth handshake and full native gameplay/migration remain pending. The live HTTP provider and eight-seat private coordinator API are verified separately.

Local evidence: production dry-run exits 0 with an 858.34 KiB bundle (141.22 KiB gzip), exact three Durable Object bindings and configured public variables. Production type generation also exits 0 into ignored `.wrangler/production-preview`. Logs: `verify-production-preview.log` and `verify-production-types.log`. No cloud upload or account validation is implied by this local result.

Independent read-only review found no concrete account, website routing, secret or binding boundary defect. Its client-bootstrap pinning requirement is included above. This review does not establish access or live service behavior.
