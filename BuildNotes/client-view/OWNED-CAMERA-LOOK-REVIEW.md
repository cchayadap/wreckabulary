# Runtime-owned camera look review

STATUS: SOURCE PASS — reviewed root commit `f4c7af48`; no compiler, native test or player run by this reviewer.

The one-line production change calls the existing `GraphicsOptions.ApplyTo(view)` immediately after creating a runtime-owned camera. The supplied-camera path does not call it. `ApplyTo` enables HDR/post processing/dithering, applies the current smoothing/shadow options and ensures the original shared ACES look. It does not call `ApplyLights`, save preferences, alter the camera profile or change input handling. Existing profile application still follows this initialization.

The two added native Editor cases exercise the actual `WordboundPlayerView.Initialize` path. The owned-camera test checks real URP camera data, the shared ACES volume and current smoothing/shadow settings; the old implementation lacks that camera data and fails this regression. The supplied-camera test starts with explicit contrasting render settings and checks they survive initialization. Cleanup destroys the owned camera/root and only removes the shared look/profile if this test created them. It leaves existing look objects and preferences intact. The existing profile-edit/invalid-edit case remains, for **3 cases total** in `Wreckabulary.OfflineGameplayTests.WordboundCameraProfileTests`.

The added direct URP/Core Runtime assembly references match the test's public camera/volume APIs. No concrete source blocker found. Root reported R0 pass `205646Z`; actual native execution and rendered appearance/performance remain separate gates. This review is not visual acceptance of the Windows build.
