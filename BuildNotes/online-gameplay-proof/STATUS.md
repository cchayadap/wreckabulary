# Native online gameplay proof

STATUS: IN PROGRESS

Owner: online lane. Branch: `sethypagna/online`, starting checkpoint `8517152`. Exclusive new paths: `Assets/_Project/Tests/OnlineGameplay/` and `BuildNotes/online-gameplay-proof/`. No service changes, Unity launch, native metadata generation or other-lane source edits.

Prepare a gated native Unity fixture that uses the real deployed coordinator, signed content/roster/admissions/connection leases, actual Fusion 2.1.3, and James's gameplay composition commit `9d69fa141bcd88419dbe2f0c63cace5610983bcc`. Initial acceptance: host/client authenticated identity, real scene/static world, exact canonical dynamic-body identities, active lease input mapping, durable snapshot replication, host progress and no client domain advancement. Cleanup must stop both sessions, return test seats and leave parties without leaking credentials.

Native generated prefab contract: `Assets/_Project/Prefabs/Wordbound/Network/Wordbound{Player,Item,Platform}Body.prefab`. Root creates/weaves these prefabs. Verify the existing proof scene path against the integrated fixtures. Read public `OnlineServices/production-client.json`; private RSA/provider/access secrets are neither required nor read.

Native execution: **NOT RUN**. Root alone launches Unity. Explicit environment opt-in and configured Photon custom authentication are required. Source compilation against actual SDK/Unity/test assemblies is a separate gate and does not establish runtime success.

Next recoverable action: resolve the real coordinator workflow, pinned content fixture and native prefab/scene loading contracts, implement host/client fixture, then compile and checkpoint the coherent slice.

Source checkpoint: implemented parameterized 2/4/8 authenticated gameplay cases and a separate two-peer host-departure recovery case. ReferenceCheck.csproj compiled the actual fixture/composition against installed SDK and native Unity/NUnit assemblies: PASS, zero warnings/errors (compile.log). No Unity or live service requests were executed by this lane.

Critical execution gate: root's native investigation proved that Fusion PushHostMigrationSnapshot(true) neither confirms cloud durability nor transfers every carrier. The current fixture deliberately asserts the intended complete committed state and must not be presented as passing. Canonical coordinator durability and schema2 protocol integration must land before native execution. Photon provider setup is still required. The committed fixture is a recoverable source checkpoint, not a gameplay acceptance result.

Next action: implement the coordinator's exclusive writer and complete checkpoint CAS, wire the Session acknowledgement/restore boundary, update fixture for the new service dependency and schema2, then root executes authenticated native tests.
