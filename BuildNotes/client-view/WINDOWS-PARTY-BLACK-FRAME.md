# Windows party black frame

Status: source/artifact investigation COMPLETE; underlying black-frame cause NOT PROVEN. Concrete QA observation gap and bounded proposal below. No new player, compiler, capture or Unity run. Root owns execution and production changes. The game visual debugging skill was used to separate decoded raster evidence from causal claims; this checkout has no `.game-dev/adapter.json`, so no adapter was installed or executed.

Observed build `20261023T170613Z`, smoke `20261023T171517Z-wordbound_workshop_flow`. Direct inspection confirms the saved 1280×720 `wordbound_party.png` appears entirely black. `checks.json` marks `open_wordbound` PASS after coordinator availability, three frames and a screen capture; this is not an appearance assertion. The screenshot is recorded at 17:15:57Z. The later `edit_workshop` shader exception is a separate established failure: it occurs after the party capture and does not, by itself, explain the earlier black image.

The player log confirms Direct3D 11 on Intel Arc and normal engine initialization. It contains no party OnGUI/Repaint, focus/window visibility, camera-render or pixel diagnostic. Causes remain under review.

## Decoded evidence

Read-only Pillow decoding confirms 1280×720 RGBA, exactly one unique value: **all 921,600 pixels are `(0,0,0,0)`**. This is an entirely zero capture, including alpha, rather than an opaque dark scene. Direct image inspection agrees that there is no visible party UI.

| Artifact | SHA-256 |
| --- | --- |
| `docs/qa/runs/20261023T171517Z-wordbound_workshop_flow/wordbound_party.png` | `9FF2A7E523A17224D386443B5761330A43016430B524A6D028FCE6CB560D4356` |
| `Logs/player/20261023T171517Z-wordbound_workshop_flow.log` | `BB4414C96539455C8387B8193D0E5B8DCC026D2902FC62AA79E0062E52839FA8` |

## Source trace

1. `WordboundWorkshopQa.OpenWordbound` calls the real `LobbyMenu.OpenWordbound`, then waits for a launcher and non-null party coordinator. `Session.Load` loads `WordboundLobby`; coordinator availability establishes successful menu initialization, not rendering. The scenario waits three Update frames, then captures. It does not observe `OnGUI`, splash completion, camera completion or pixels.
2. `WordboundLobby.unity` has an enabled, active, full-viewport lobby camera targeting display 0 with no target texture. Its clear flag is SolidColor and background is `(0.075,0.105,0.14,1)`, deliberately nonblack. Culling mask 0 is intentional for this background-only party scene, not proof that camera clearing is disabled. The camera is assigned to the launcher. The inspected initial launcher path does not disable it; that occurs when a match starts.
3. `WordboundPartyMenu.OnGUI` draws when coordinator exists, `Visible` is true, and no successfully active match suppresses the menu. Initial `Visible` is true and this scenario does not connect/join a match. `DrawPartyGui` draws a box, title, descriptive text, guest name and controls. Focus loss suspends input, but does **not** suppress drawing. No source-confirmed branch deliberately paints the whole party frame transparent black.
4. `QaContext.CaptureScreen` delegates to `StoryCamera.CaptureScreen`, which waits for `WaitForEndOfFrame`, calls `ScreenCapture.CaptureScreenshotAsTexture`, and writes the PNG. `QaRunner.Drive` passes this yield instruction to Unity; it is not accidentally consumed as a nested enumerator. It reports write/readback exceptions, but has no nonblank pixel assertion. Thus PASS means the action/capture completed, not that pixels prove a visible party.
5. `Tools/kit/gate/lib/player.mjs` launches with the configured windowed 1280×720 arguments and no `-batchmode`. `Tools/kit/lib/proc.mjs:153` sets `windowsHide: true`. The outer delivery wrapper is a separate background process; its saved status records arguments and PID, not child-window visibility. No evidence in this run establishes whether the player window was hidden, minimized, foregrounded or actually presented. The flag is a lead, not a proven cause. No kit files were changed.
6. Player settings enable `runInBackground` and `visibleInBackground`; they also enable the Unity splash screen/logo. The QA step does not wait for `UnityEngine.Rendering.SplashScreen.isFinished`. A splash/render-readiness race remains possible, but the old log cannot prove it.
7. The profile's `m_Subtarget: 2` is **Player**, not Dedicated Server. Read-only reflection of the installed Unity 6000.6.3f1 `UnityEditor.CoreModule.dll` enum returned `Default=0`, `Player=2`, `Server=1`. This possible explanation is refuted. No Unity process or compiler was used for the reflection.

## Supported conclusion and bounded next change

The capture contains none of the expected nonzero camera background or menu output. The saved evidence cannot distinguish a hidden/unpresented-window effect, a startup/splash rendering race, or another backbuffer/capture failure. The later stripped shader failure is independently real and is being repaired by root; do not label it the cause of the earlier party image.

Recommend a **QA observation change**, not a speculative production camera/style change:

- Under `UNITY_EDITOR || GSK_QA`, record a party-menu repaint counter/frame only after `DrawPartyGui` returns successfully during `EventType.Repaint`. Do not set readiness from Update, initialization or a manually invoked drawing method.
- In the existing `open_wordbound` step, retain the actual lobby→scene→launcher route. Within its existing timeout, wait for splash completion, the active/visible initialized party, and at least two actual completed repaint frames. Observe the assigned lobby camera through `RenderPipelineManager.endCameraRendering` (with `Camera.onPostRender` only for the built-in path) and require a completed render from that camera. Register before observation and remove callbacks in a guaranteed cleanup path.
- Record scene, screen dimensions, `Application.isFocused` (diagnostic, not a required foreground claim), splash state, current/rendered frame counts, repaint counts/frame and camera active/enabled/target-display/target-texture facts before capture. If no repaint/render arrives, fail with those facts instead of presenting a blank PNG as a passed visual step.
- Keep the kit's end-of-frame capture. Decode the captured PNG in the game-owned scenario and record RGB/alpha extrema and nonzero-pixel counts; reject the actual observed all-zero/all-black frame. This is a minimum blank-frame check, not an art-quality or full menu-layout acceptance claim. Preserve the failed PNG rather than retrying until a favorable frame hides the first result.
- If the observed paint/camera gates complete yet the screenshot is still zero, root should make one separately recorded Windows run with a visible player launch, preserving the same executable/arguments/scene and recording its window state. That discriminates the launch path without editing the shared kit. A visible launch is diagnostic and does not retroactively prove why this old run failed.

The next Windows run must still be inspected by a human or image review. Repaint callbacks and nonblank pixels cannot prove readable text, usable navigation or original-art appearance. No production, scene, kit or QA source was changed in this report-only lane.
