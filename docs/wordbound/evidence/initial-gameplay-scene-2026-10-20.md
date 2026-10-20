# Initial gameplay scene repair

STATUS: IN PROGRESS

The real two-peer case in `TestResults/editmode_20261020T144759Z.xml` failed at Fusion startup. Installed Fusion 2.1.3 source (`Assets/Photon/Fusion/Runtime/NetworkSceneManagerDefault.cs`, Initialize and LoadSceneCoroutine) requires an initial scene in Multiple mode and creates the Runner's isolated physics scene itself. Explicit `LocalPhysicsMode.Physics3D` on a loaded scene is rejected in this mode.

`WordboundFusionSession.ConfigureScene` now records an additive initial scene before startup and passes it to every Runner, including replacement Runners. Multiple mode refuses to start without it before making network requests. `WordboundGameplayController.AttachSession` supplies the selected gameplay scene; scene preparation waits for that scene and its isolated physics world instead of requesting a second load. Installed SDK files are unchanged.

Source lifecycle regression: **31/31 PASS**, 7.05 seconds, using the existing `BuildNotes/online/FusionCompile/SessionLifetimeTests.csproj` and its explicitly identified SDK doubles. Three new cases cover missing/invalid scenes and the same configured scene reaching initial and replacement Runner startup. This does not prove real scene loading.

The initial `dotnet run` invocation incorrectly forwarded `-m:1` as the test-name filter and matched no tests. The built assembly was then run explicitly with `dotnet BuildNotes/online/FusionCompile/bin/Debug/net9.0/SessionLifetimeTests.dll SessionLifetimeTests`, producing the 31/31 result above.

Aggregate runtime/editor R0 passes in 82.8 seconds (`Logs/gate_check_all_20261020T150340Z.log`). This includes the newly integrated workshop source and interaction fixture. No engine-free production rules changed after the last 228/228 core pass. Native loading and gameplay retry remain pending.
