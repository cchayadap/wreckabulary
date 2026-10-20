STATUS: BOUNDED SOURCE REVIEW COMPLETE — OWNER SERVICE GATE PENDING

Bounded independent source review of Online's first automatic workshop bundle service slice. Read-only source in wordbound-online; no compiler, Unity, deployment or source edits. Scope is owner authorization, party/match binding, queued copy, expiry/finalize replay and transaction boundaries. Exact file hashes and confirmed findings follow as inspected. This is not a production acceptance claim.

## Confirmed from frozen source, runtime reproduction pending

**B1 / P2 — An exact committed bundle cannot be reselected after its party revision changes.** `BundleStore.begin` requires the stored upload revision to equal the new party revision even for committed immutable bytes. Finalize A at revision 1 (party becomes 2), change mode (clears selected workshop), then change back: beginning the same valid A with the current revision fails `CONTENT_UPLOAD_CONFLICT` until its stored 24-hour expiry. A→B→A has the same result. The C# uploader's current-bundle shortcut cannot help once A is no longer selected. Owner/current-party-revision authorization can safely permit rebinding those same verified bytes. Sent to Online; regression requested. Scope: bundles.ts begin, party.ts settings/finalize.

**B2 / P1 — Expiry can prevent recovery of a successfully queued match after a lost response.** `Party.enqueueParty` obtains source bytes before its idempotent Pool call. If Pool enqueue/install commits, then its response is lost before Party.finish, the saved operation has an attempt but no active assignment. The alarm deletes expired source bytes even when preserving the sealed workshop metadata for that operation. After expiry, retrying the same operation calls `bundles.transfer` first and fails `CONTENT_NOT_FOUND`; unlock drops the operation instead of recovering Pool's existing assignment. The party can then lack the attempt needed to cancel the still-active pool seat. A bounded fix must preserve the pending source or resolve the saved attempt before requiring another upload transfer. Sent to Online; deferred/lost-response + expiry regression requested. This is a source interleaving, not a claimed executed DO reproduction.

The first source also counts all retained committed bundles toward the three-upload scope limit. Repeated replacement A→B→C then D reaches `CONTENT_UPLOAD_LIMIT` for 24 hours. This shares the selection/retention lifecycle of B1 and was not expanded into a separate finding.

## Covered paths and limits

- HTTP derives actor from authenticated credentials. Party reads require current membership; upload mutations require current leader, no active queue/operation, and exact revision (with bounded same-selection finalize replay).
- Party bundle install/finalize/state mutation share synchronous SQL transactions. Match install occurs inside the same pool transaction as match creation/enrollment. Immutable chunk bytes reject conflicting replay.
- Match content reads require an assigned seat, no terminal Return, no closed match, and reconnect eligibility after start. Match-owned copies are separate from party-owned upload storage.
- Descriptor/chunk/packet sizes, canonical bytes, deflate output cap, wire/raw hashes, document hash, manifest hash and exact declared content/settings bindings were read. Domain geometry/recipe correctness remains the strict C# import responsibility; this review did not execute either importer or service.
- No test suite, live HTTP, compiler or native Unity process was run here. Final repair verification remains pending.

Frozen source hashes (SHA-256):

| File under OnlineServices/src | Hash |
| --- | --- |
| bundles.ts | 91B7E1C66F15209D5452A85117B33A3B9DAB1D809CD6C2D01FADEF6A31BE2E3F |
| party.ts | D9008A9C834C590EC808851DD63F56C34CEA47A8EC3A5A5DF5E466C65303E528 |
| pool.ts | DA24824608CE73B8E415562EAD10C77F95EE3401D038EACA9D9691EC530E3F5F |
| index.ts | E79CFE9128ED147631AD63C4527026E988089330876B07936164DBFE91B5E059 |
| model.ts | 1AF3B41AB8579BB4D88598D43644F1BF5DDED2EEC74A1D6A4F9D17452CEFB14F |

## Repair source reinspection

Online accepted B1/B2 and applied source repairs. Independent narrow reinspection confirms committed exact bytes may rebind only through the current leader/revision path; old completed records may be evicted while retaining selected/pending records and cap3. The saved server-derived Attempt.workshopId now survives a lost response. Pool assigned replay verifies the exact stored attempt digest before returning its existing match; fresh reservation creation with workshopId still requires the matching complete bundle. Expired Party bytes therefore do not prevent recovering an already committed exact Pool result or permit creating a new match without the bytes.

The new bundles.test.ts includes A→B→A and mode-reset/reselection cases, bounded retention, and a test reconstructing real SQLite post-Pool/pre-Party-finish state before retry. That is a deliberately reconstructed crash point, not an actual interrupted network request. Source tests had not run when inspected. Missing-bundle fresh attempts and altered assigned-attempt workshop IDs were suggested as explicit adversaries; no additional blocking source defect was confirmed in this bounded pass.

Repaired source SHA-256: bundles.ts6B734036DFD57DF894721B93920F0D812F94D3EFD008CA17E79F112C223D8082; party.tsA17FDCEB566AE0734E1BA6D04F430780C5496E8051FB158E92321E82C44E6BA0; pool.ts1AC27BC5ACA46A27DAF353599EEA368E0C98680A90040CBF29807B783EC6CC7C; model.tsB04E578F51F264005CC32C8D82198F9CE1277CAFD5452D916E492680CD5B2713. Broader client integration, final cross-language exporter, live service behavior and native workshop party gameplay were outside this review.
