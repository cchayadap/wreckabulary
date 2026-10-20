# Canonical physical/flight interop refresh

STATUS: COMPLETE — REAL C# EXPORT, WORKER/SQLITE CHECKPOINT TESTS AND TYPECHECK PASS

Dependency: physical source `17c30b4` is present in this lane as `7ad6ce8`; root already has the equivalent `720eb60`. Existing BALL source includes the required thrown-flight ledger. Do not cherry-pick that dependency into integration again.

The exporter now uses the real BrawlRace authority, actual item/rule JSON, a resolved PlaceObject site and an added BALL letter supply. Domain commands collect/craft/deploy BED, complete the authored hold through trusted host observations, then collect/craft/throw BALL and resolve a host contact. The final complete C# snapshot must contain nonempty Complete physical evidence and a flight with one retained hit. The appearance control-character/Unicode compatibility case remains.

This is codec/domain interop evidence with an explicit host-query fixture, not a native physics or network gameplay proof. No handwritten canonical JSON substitutes for Export/Import. The current C# codec also checks rejection when required physical/flight keys are replaced.

The real exporter passed using James's current `929240a` Rules assembly at `wordbound-puzzle-race/Logs/entity-lifecycle/rules-bin/Wreckabulary.Rules.dll`, SHA-256 `B5708A7CEEBB6A6AFAA23B345BF45C11BF18460C9254440B473299F017E60D0D`. Output: 24,779 raw UTF-8 bytes, one compressed chunk, canonical tick 290, Complete physical evidence and one BALL hit. Export→Import→Export was byte exact. This assembly includes the subsequent entity cleanup while preserving physical and flight references; it is newer than the source dependency used to prepare the exporter.

Worker coverage passed: exact canonical/hash/Deflate acceptance, explicit new-field assertions, unchanged binding rejection, and a complete immutable fixture upload/finalize/readback across actual SQLite Durable Object eviction. The new fixed-identity fixture uses trusted internal RPC after seeding the test match; authenticated HTTP and writer-fence cases remain separate tests in the same file.

Verification:

- `dotnet run --project BuildNotes/online/CheckpointInterop/Export.csproj --no-restore -- . OnlineServices/test/fixtures/schema2.ts`: PASS; real C# Export/Import and required-field rejection.
- `npm test -- test/checkpoints.test.ts` in `OnlineServices`: PASS 19/19, 39.67 seconds. Negative authorization/fence cases emitted expected rejection traces; the test process exited zero.
- `npm run check` in `OnlineServices`: PASS, no TypeScript diagnostics.
- Independent read-only review by Sethy found no concrete false-pass in the exporter or new SQLite eviction case; review explicitly retains the trusted-host/internal-RPC limits.

The updated fixture does not require service code changes or deployment. No Unity, native physics, live Photon or external API test was run for this increment. The separate workshop route source is outside this checkpoint.

Next action: root can integrate the fixture/test commit alone, then continue native recovery acceptance with the current canonical schema. The physical dependency is already present there.
