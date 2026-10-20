# Photon setup and verification

STATUS: IN PROGRESS

James supplied the app identifier locally. On 2026-10-20 the signed-in dashboard showed the Wreckabulary app with a Fusion 2 plugin, 20 total concurrent users, no CCU burst and all regions allowed. No custom authentication provider was configured. The identifier is held in ignored `.harness/wordbound/photon-app.json`; it is not embedded in the proof fixtures.

**Live transport checks PASS for 2, 4 and 8 peers**, including byte-exact 64 KiB messages in both directions and clean shutdown. All peers ran in one Unity Editor on this PC using Photon Asia. This proves the app and local SDK connection path; it does not close the gameplay, migration or performance milestone. Sanitized evidence: [connectivity report](evidence/photon-connectivity-2026-10-20.json).

## Pinned packages

The owner downloaded both packages from the official Photon pages. Archive path inspection found only `Assets/Photon/` contents. These proprietary dependencies remain local under an ignored SDK directory; restore them from the official vendor rather than redistributing the SDK in the public source repository.

| Package | Bytes | SHA-256 |
| --- | ---: | --- |
| `photon-fusion-2.1.3-stable-2390.unitypackage` | 6694676 | `375e21bc771ce5f7cea942b5f804e7c28c8f4d858f7e9aab96725214564bc52e` |
| `fusion-physics-for-fusion-2.1.x-2.1.2.unitypackage` | 8428 | `1fbfccdbab0e9b00a8c1317d02a489ba17e38a3badd6ae10a59e751c603b1aaa` |

Sources: [Fusion SDK](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download), [Physics Addon](https://doc.photonengine.com/fusion/v2/addons/physics-addon-2.1), [Photon licence terms](https://www.photonengine.com/terms/licenseterms).

## Import procedure

For a fresh checkout, restore both SDK packages **before** opening Unity, because the online assemblies reference the SDK. Run this from the project directory, substituting the two download paths:

```powershell
python Tools/Wordbound/restore_photon.py --project . --fusion "PATH/photon-fusion-2.1.3-stable-2390.unitypackage" --physics "PATH/fusion-physics-for-fusion-2.1.x-2.1.2.unitypackage"
```

The script checks both pinned hashes and the full archive inventory before writing, preserves original vendor asset/metadata bytes, refuses linked destinations, refuses a project with a Unity lock, and never overwrites differing existing files. `--verify-only` inspects the downloads without writing. A partial interruption can be resumed; existing identical files are skipped. Unity still owns native asset import, API updating and compilation afterward. Do not run the restoration over an SDK that Unity has already API-updated.

Scratch verification passed: 554 vendor files restored byte-for-byte; a second run wrote zero files; differing existing source, an editor lock and a wrong package hash were rejected. Evidence: `Logs/wordbound/photon-bootstrap.json`. This verifies restoration, not fresh-project native compilation.

For an already compilable checkout, the native import alternative is:

`WordboundPhotonSetup.ImportFusion` verifies the package hash and requests the native Unity package import. Unity 6000.6 queues that operation: a successful synchronous method invocation does **not** prove the package finished importing. Use the `WordboundSdkImportTests.ImportInspectedPackageToCompletion` editor coroutine, which waits for its completion event, checks an expected file and explicitly yields through compilation.

Set these process environment variables before the gate:

- `WORDBOUND_FUSION_PACKAGE`: absolute path to one of the inspected packages.
- `WORDBOUND_FUSION_SHA256`: the matching hash above.
- `WORDBOUND_FUSION_VERIFY_PATH`: `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll` for Fusion, or `Assets/Photon/FusionAddons/Physics/Runtime/NetworkRigidbody/NetworkRigidbody.cs` for the addon.

Run `node Tools/kit/gate/game_gate.mjs test editmode --filter Wreckabulary.EditorTests.WordboundSdkImportTests --json`. Repeat for the other package. Keep production networking code in its own SDK-dependent assembly.

The first cold synchronous calls returned successfully but produced no files; they are not import evidence. The first coroutine produced the Fusion files but failed because Unity's assembly updater tried resolving `Photon.Realtime` before that source assembly had compiled. Its failure is preserved in `TestResults/editmode_20261020T111020Z.xml`; a compilation reload then prevented the test process from exiting, so the exact owned editor PID was stopped after results were saved. This is not a passing compatibility result.

The addon coroutine subsequently imported and compiled `Fusion.Addons.Physics.dll`, but its fixture failed because `RecompileScripts` expected another compilation after compilation had already completed (`TestResults/editmode_20261020T112218Z.xml`). The helper now permits an already completed compilation. The addon's `physics_build_info.txt` identifies 2.1.2 build 1246; its bundled `package.json` still says 2.1.0.

## Connection proof

`WordboundPhotonConnectionTests` contains explicit external tests for 2, 4 and 8 peers. They require `WORDBOUND_PHOTON_APP_ID`, create an unlisted random room, validate a random connection token, use relayed Host/Client sessions, close admission after all peers arrive, and check a byte-exact 64 KiB reliable transfer from every client and back. Test cleanup shuts down the owned runners.

Proof reports go under `Logs/wordbound/photon-proof-<count>.json`. They contain no app identifier or connection secret. The final native run passes 3/3: `TestResults/editmode_20261020T120201Z.xml`, SHA-256 `ed40dfa62f55f50b66799ece859046df52830177fafaaa1d8bfbec7dec8d68df`, 252.3 seconds including startup. Read the NUnit/gate result alongside the payload reports: a payload report alone does not verify teardown.

The first two-peer attempt failed because the multi-peer test had no explicit starting scene (`TestResults/editmode_20261020T113351Z.xml`). That run also recorded a Burst compiler queue timeout during editor initialization. Creating the scene inside the test conflicted with Unity's populated temporary scene (`editmode_20261020T114749Z.xml`, `editmode_20261020T115222Z.xml`). The native empty scene is now a saved asset, created separately through `game_gate exec Wreckabulary.EditorTools.WordboundPhotonSetup.CreateProofScene`; its setup passed (`Logs/gate_exec_CreateProofScene_20261020T115540Z.log`).

The first successful two-peer payload exchange still failed teardown because host-first shutdown disconnected the remaining client (`editmode_20261020T115923Z.xml`). Cleanup now disconnects clients before the host and restores the previous build-scene list. The final 2/4/8 run above verifies both the transport and this cleanup.

These transport probes do not prove production guest authentication, physics quality, migration, packet-loss tolerance or eight-player performance. The separate coordinator's custom authentication provider, pinned issuer/JWKS and exact signed start/admission checks must be configured before public play. See [ONLINE.md](ONLINE.md).

## Native migration setup

After restoring the SDK, apply configuration, explicitly rebuild the network assemblies, and then bake the prefab:

```text
node Tools/kit/gate/game_gate.mjs exec Wreckabulary.EditorTools.WordboundFusionSetup.Configure --json
node Tools/kit/gate/game_gate.mjs test editmode --filter WordboundFusionWeavingTests --json
node Tools/kit/gate/game_gate.mjs exec Wreckabulary.EditorTools.WordboundFusionSetup.CreateSnapshotPrefab --json
```

The helper uses Fusion's native configuration writer and prefab baker. It registers `Wreckabulary.Online.Fusion` and the planned `Wreckabulary.Online.Physics` assembly for weaving, disables automatic migration uploads, and permits client-to-server reliable transfer without client-to-client forwarding. The carrier prefab contains a NetworkObject and the bounded snapshot chunk behaviour, with no renderer or collider.

Configuration/prefab creation passed natively on 2026-10-20: `gate_exec_Configure_20261020T122120Z.log` and `gate_exec_CreateSnapshotPrefab_20261020T122321Z.log`. The first runtime attempt exposed that Fusion deliberately disables automatic reweaving on configuration changes in batch mode (`FusionWeaverTriggerImporter.OnImportAsset`). The added weaving coroutine requests a clean native script compilation, survives reload, and verifies the carrier's generated weaving attribute.

The first clean rebuild finished, but its test exceeded Unity's default 180-second timeout (`TestResults/editmode_20261020T123501Z.xml`), so that run is FAIL. The fixture now permits a bounded fifteen minutes for initial compilation. Normal follow-up uses `--filter ConfiguredCarrierHasNativeGeneratedNetworkState` to inspect the generated attribute without rebuilding every package. Prefab setup also checks the actual attribute before baking. The read-only generated-state check passes in `TestResults/editmode_20261020T125740Z.xml`.

The generated local SDK configuration stays ignored; the helper makes setup reproducible. `WordboundPhotonMigrationTests` checks complete canonical state and a larger chunked packet through cloud upload and host replacement. Its first attempt failed before connecting because the carrier was not yet woven (`TestResults/editmode_20261020T122803Z.xml`); recovery remains unverified.

The subsequent recovery probe reached a replacement host, but checked state before the asynchronous resume callback. A bounded callback wait exposed two further failures (`TestResults/editmode_20261020T130511Z.xml`): copying raw NetworkObject state into an unrelated prefab ID, and incomplete large-packet recovery after materializing temporary resume objects. The probe now recreates fresh carriers from validated application state, as the production session does on its next commit. Restore captures chunk values and payload bytes while the SDK iterator is positioned on each temporary object; callers must keep that enumeration lazy. The new native run records temporary-object reuse and retains exact byte, identity, completeness and rebuilt-carrier checks. Recovery acceptance remains pending that run.

The follow-up passes the complete single-carrier case but still fails the large packet. Its diagnostic run returns only carrier 0 of 33; reference reuse was not the cause. Installed SDK inspection confirms that a push queues a bounded incremental transfer, not a complete acknowledged application checkpoint. The coordinator-backed checkpoint correction is now required before accepting gameplay recovery. See [the native recovery evidence](evidence/native-recovery-2026-10-20.md) for exact results, hashes and limits.

## Production custom authentication

The owner completed the dashboard settings after the provider key was rotated. Native Unity checks now pass 3/3 in 128.6 seconds: `TestResults/editmode_20261020T135810Z.xml`. Valid host/client tickets use coordinator-issued identities; anonymous and invalid-ticket clients are rejected. Cleanup passes. See [native authentication evidence](evidence/native-photon-auth-2026-10-20.md) for the exact scope, hash and remaining limits. There is no further owner authentication setup step pending.
