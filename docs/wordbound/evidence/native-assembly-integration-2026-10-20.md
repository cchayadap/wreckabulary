# Native fixture assembly integration

STATUS: IN PROGRESS

`Logs/gate_test_editmode_20261020T144219Z.log` records a native compile failure before tests ran: the existing gameplay policy tests and newly integrated native fixture both declared `Wreckabulary.Online.Gameplay.Tests`. The gate returned FAIL after 40.5 seconds. Source repair `890ea23` names the new fixture assembly `Wreckabulary.Online.NativeGameplay.Tests`, leaving its filename and Unity-generated metadata intact.

The subsequent `check asmdef` returned FAIL with 112 errors (`Logs/gate_check_asmdef_20261020T144520Z.log`) and no layer violations. Inspection of the generated project `Tools/kit/gate/Check/asmdef/Wreckabulary.Online.Fusion.csproj` shows references to Fusion.Unity and Photon.Realtime but no Fusion.Runtime/Common/Sockets references, whereas the real Unity assembly uses automatic plugin references. Other errors concern EditorWindow and editor-conditional SetEntries in pre-existing capture/import code. This per-assembly harness result is not a native Unity verdict and has not been marked PASS. Kit-generated files were not edited.

The required aggregate runtime/editor R0 check passes after the repair (`Logs/gate_check_all_20261020T144716Z.log`, 23 seconds). The latest full Wordbound engine-free suite passes 228/228 (`TestResults/core_20261020T144107Z.trx`, 30.3 seconds).

Native retry completed: `Logs/gate_test_editmode_20261020T144759Z.log` and `TestResults/editmode_20261020T144759Z.xml`, FAIL after 248.2 seconds, 43 tests total, 42 passed and one failed. Unity compiled the combined assembly definitions and client composition successfully. All selected checkpoint, field, release and rules-fingerprint cases passed, including the previously deadlocked deferred HTTP case.

The real authenticated two-peer gameplay case failed because `StartGameArgs.Scene` was empty with Multiple peer mode. A subsequent deferred load also requested `LocalPhysicsMode.Physics3D`, which the installed scene manager rejects in Multiple mode. The SDK creates its own isolated physics scene per Runner. Root is correcting the initial scene contract in the session and controller, retaining isolation. Native gameplay acceptance is still pending; this result is compilation and focused-policy evidence only.
