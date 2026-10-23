# Runtime-owned camera look review

STATUS: SOURCE PASS — reviewed root commit `f4c7af48`; no compiler, native test or player run by this reviewer.

The one-line production change calls the existing `GraphicsOptions.ApplyTo(view)` immediately after creating a runtime-owned camera. The supplied-camera path does not call it. `ApplyTo` enables HDR/post processing/dithering, applies the current smoothing/shadow options and ensures the original shared ACES look. It does not call `ApplyLights`, save preferences, alter the camera profile or change input handling. Existing profile application still follows this initialization.

The two added native Editor cases exercise the actual `WordboundPlayerView.Initialize` path. The owned-camera test checks real URP camera data, the shared ACES volume and current smoothing/shadow settings; the old implementation lacks that camera data and fails this regression. The supplied-camera test starts with explicit contrasting render settings and checks they survive initialization. Cleanup destroys the owned camera/root and only removes the shared look/profile if this test created them. It leaves existing look objects and preferences intact. The existing profile-edit/invalid-edit case remains, for **3 cases total** in `Wreckabulary.OfflineGameplayTests.WordboundCameraProfileTests`.

The added direct URP/Core Runtime assembly references match the test's public camera/volume APIs. No concrete source blocker found. Root reported R0 pass `205646Z`; actual native execution and rendered appearance/performance remain separate gates. This review is not visual acceptance of the Windows build.

## Native context failure and source closure

Direct inspection of `TestResults/editmode_20261023T205848Z.xml` confirms **3 total, 2 passed, 1 failed, 0 skipped**. Only the new owned-camera case failed: it invoked `EnsureLook` in EditMode, where `DontDestroyOnLoad` is forbidden. The supplied-camera and profile-edit cases passed. This corrects the earlier appendix's misreading of a short status message. The fixture-context gap was missed by the initial source review; it does not justify a production workaround. The raw failure remains evidence.

Reviewed repair **`0ae86290`**: the owned-camera case is now a direct `UnityTest` yielding `EnterPlayMode` before initialization. A `UnityTearDown` yields `ExitPlayMode` whenever necessary, including assertion failures. The supplied-camera and profile-edit cases remain ordinary Editor cases. All rendering assertions and cleanup remain; production code is unchanged. Bounded source closure PASS; root reported R0 `210108Z` pass. Native rerun and visual acceptance still pending.
