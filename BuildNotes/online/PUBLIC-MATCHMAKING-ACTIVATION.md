# Public matchmaking activation review

STATUS: READ-ONLY REVIEW COMPLETE — ACTIVATION NOT PERFORMED

Read-only review of integration `6cd0b6777caf6435fdb04495afcd862db677baa7`. Scope: exact production content registration, client Quick Play availability, worker allowlist and bounded post-native activation checks. No source/config edits, compilation, Unity, deployment, secrets or external requests are authorized in this review. Root's pending native validation remains a prerequisite to content approval.

Public matchmaking already has the queue, whole-party allocation, Fill, admission and automatic launch plumbing. Registration is closed in both shipped inputs. Native private-room proofs do not establish public queue acceptance.

## Exact public set

Generate the following **eight bindings per approved seed**, using the final compiled `WordboundClientContent(actualItemsJson, actualRuleBookJson, mode, format, seed)` and `WordboundPublicCourseRegistry.RegistrationJson(...)`. Do not copy earlier hashes: `WordboundRulesFingerprint` includes `WordboundEntityBudget.Hash`, whose final catalogue policy is version 2.

| Mode | Public formats | Source content |
| --- | --- | --- |
| TimeRun | Solo | Resolved flagship with bound authored physical puzzles |
| TeamRace | Solo, Duo, Squad | Resolved flagship with bound authored physical puzzles |
| BrawlRace | Solo, Duo, Squad | Resolved flagship with bound authored physical puzzles |
| FreeForAll | Solo | CombatArena template |
| Freestyle | None | Explicitly private in mode policy and worker |

Each entry is exactly `{kind:"BuiltIn",courseId,courseHash,manifestHash,modeHash,catalogHash}`. Format changes mode/content hashes; resolved seed changes the manifest hash. Fill and region do not require duplicate entries. Settings still separate pools by region, protocol, map-pool version, mode and format.

The current UI/default/native flagship seed is `0123456789abcdef`. One approved seed means eight entries; additional approved seeds add eight each. The client registry caps 128 entries, allowing at most 16 complete eight-entry sets. The pending two flagship cases cover TeamRace Squad and BrawlRace Duo, not every listed public variant. Do not describe all eight as natively validated solely from those two results or the pure seed sweep.

## Required activation changes

1. **Generate one reviewed JSON artifact from final real content.** Preserve alongside it the exact item/rule source hashes, seed, mode/format, full content bindings and native evidence IDs. No handwritten/reused pre-policy-v2 hashes. The existing registration helper filters private-only Freestyle but is not currently called by an exporter or installer.
2. **Ship the registry to Unity.** Import that JSON as a TextAsset (suggested path `Assets/_Project/Resources/Wordbound/approved-public-courses.json`), then assign `WordboundLaunchAssets.ApprovedPublicCourses` through Unity APIs. `LaunchAssets.asset` currently has `{fileID:0}`; `WordboundLaunchSetup.Create()` never populates this field. Merely changing the registry constructor default or creating a loose file will not enable the shipped menu. Root owns native asset/meta generation.
3. **Set the worker allowlist to the identical array.** `OnlineServices/wrangler.production.jsonc` currently has `vars.BUILTIN_CONTENT_JSON="[]"`. Update that string from the same generated artifact, with a parity check to prevent client/worker drift. `MatchPool.approve` already enforces exact binding digest; no new endpoint, table or Durable Object migration is required for ordinary registration.
4. **Choose a release pool version and bind it in LaunchAssets.** Its current value is `"1"`. A distinct short release ID separates new queues from previous development clients; the worker derives pool names from the supplied settings, so there is no matching worker environment variable to update. Region/protocol behavior stays unchanged.
5. **Prepare an update deployment after local/native approval.** The current `prepare-connector-upload.mjs --mode update` reads production config vars, preserves existing `secret_text` bindings and omits initial DO migration. Use the selected existing account/service and current update workflow. Preserve all current secrets and signing/public trust. Deploy service approval before distributing a client that advertises it; read back the exact array and compare its canonical hash. None of these actions ran in this review.

## Concrete gaps and limits

- **Public seed selection is missing.** Before party creation, “New seed” generates arbitrary hexadecimal seeds; after creation, the seed is preserved and cannot be changed by Apply settings. Almost every generated seed will be outside a finite exact-manifest registry, so Quick Play stays disabled with no approved-pool chooser or reset. Minimal repair: before party creation offer explicit approved course/seed selection, show whether the selected seed supports Quick Play, and preserve that exact choice in invites. Do not silently change an existing party's map. Registering one default seed enables a fixed resolved public map; it does not implement a random public map rotation. The current registry stores hashes only, so a selector/rotation also needs a small trusted mode/format/seed catalogue that resolves to those bindings.
- **Private-only explanation is misleading.** Freestyle receives the generic “course has not been registered” hint although registration can never make it public. Display “Freestyle uses private sessions” for that policy.
- **No single-artifact publication/parity gate exists yet.** The launch asset, worker var and generated catalogue could drift. A small exporter/installer parity check is sufficient; a new content service is unnecessary for this bounded launch.
- **Server approval is content-only.** `approve(content)` does not relate an approved `modeHash` to requested settings.mode/format/protocol/release. Existing Worker tests intentionally reuse one synthetic binding across different modes. Honest clients independently reject a mismatched assignment, so this review does not demonstrate unauthorized gameplay. Before broad public rollout, add a negative test for a real approved TimeRun binding submitted under TeamRace settings; if server-side settings approval is required, register those metadata alongside the binding and check them before allocation. Keep this distinct from ordinary exact-binding registration.
- **Private built-in Fill is not exposed by this frontend.** Create private room always submits `PrivateBinding` (`Workshop`), so random teammates enter through explicit Custom Fill rather than ordinary Quick Play. The worker supports Quick Play filling compatible private BuiltIn rooms, but the current menu does not create such rooms. Existing explicit Custom Fill behavior remains valid; registration alone does not change that channel choice.

## Bounded verification plan

1. **Export/parity:** build the eight-entry matrix for each approved seed using final compiled content; instantiate the registry, require exact count/unique bindings, require `Allows` for all intended entries, reject Freestyle and one-field/seed mutations. Compare the shipped TextAsset and worker var canonically. Verify launch asset assignment and release pool version through an Editor asset test.
2. **Local SQLite Worker tests:** use the real exported array instead of synthetic hashes for a compact public activation suite. Cover each mode/format with minimal valid parties, exact returned content, unapproved/Workshop rejection, stale release separation, whole-party identity, Fill Off undersized teams, Fill On 3+1/3+1 allocation, concurrent final slots, cancel/requeue, admission rollback and no new strangers after start. Retain actual FFA 30-second alarm and TimeRun Fill On/Off cases. Test the settings/content mismatch above explicitly.
3. **Client contract/UI:** approved selection enables Quick Play; unapproved seed and Freestyle remain clearly unavailable. A public assignment must preserve exact content/party membership and automatically follow admission → certificate → launch once, without the private Start button. Check cancel and return lifecycle. These are separate from the already compiled private fixture.
4. **Live bounded canary after deployment:** use authenticated disposable guests in an explicitly isolated release pool ID and an approved binding. Exercise public QuickPlay → assignment → admission/certificate, verify exact content and whole parties, then return/cancel/leave every guest. A negative unapproved-binding request must still return `CONTENT_NOT_APPROVED`. The existing live script only proves unapproved Quick Play denial plus private sessions; it is not this positive canary.
5. **Native public entry:** one actual launcher/public-queue test must reach authenticated gameplay with the same final public binding. Then run the pending larger composition/recovery cases and preserve their stated same-PC/graceful-departure limits. Do not label public play complete from HTTP-only allocation.

No new source execution, tests, compilation, live requests or deployment occurred. Reviewed anchors: `WordboundClientContent.cs:72–91`, `WordboundLaunchAssets.cs:15–33`, `WordboundLaunchSetup.Create`, `WordboundPartyMenu.cs:198–298`, `WordboundPartyCoordinator.cs:214–224,340–375`, `ModeDefinition.For`, `OnlineServices/src/pool.ts:36–40,104–146`, `model.ts:124–151`, `party.ts:105–138`, production config and update-upload script. Existing tests were inspected, not rerun.

Next recoverable action: finish root's pending native validation, choose the approved seed set, implement the small registry exporter/asset wiring and seed-selection seam, then run the bounded parity/public tests before any service update.

## Bounded implementation handoff

Proposed owned paths for the follow-up:

- New `Client/WordboundPublicCourseCatalog.cs` and focused `Client/Tests/WordboundPublicCourseCatalogTests.cs`.
- Narrow `WordboundPartyMenu.cs` edits for approved seed selection versus explicit custom/private seeds, plus precise private-only/unregistered hints. Preserve current launch/return/lifecycle code.
- New `Editor/Online/WordboundPublicCourseSetup.cs` to export actual final content and bind the native TextAsset through Unity APIs.
- One generated catalogue artifact (schema, release pool version, entries containing mode/format/seed/exact BuiltIn binding). Its binding projection feeds the existing client registry and worker var; do not maintain two manually edited approvals.
- Narrow update-upload preparation to consume/validate that same artifact, with deployment still deferred.

The current menu Initialize signature and LaunchAssets field can remain; menu initialization parses the catalogue and passes its flat binding projection to `WordboundPublicCourseRegistry`. Preserve empty/default `[]` handling for closed builds/tests. Root's integration hook is one native exporter call after validation, then assignment of the generated TextAsset and matching MapPoolVersion. No MatchClient, FusionSession, coordinator authentication, shared lobby or secrets change is needed.
