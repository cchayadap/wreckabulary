# Visible Windows crash review

STATUS: CONFIRMED NATIVE CRASH; managed cause not yet established. Read-only review of the existing `release-qa` build `20261023T174755Z` and its visible workshop-flow run `175916Z`. No relaunch, debugger, package install, native DLL load, production edit or heavy job by this reviewer. The earlier official hidden-run FAIL remains unchanged.

## Observed run and failure

- `.harness/delivery/visible-player-status.json` records a diagnostic-only normal-window run at 1280×720, from 17:59:16.3226454Z to process completion 18:00:00.6286047Z. Exit `-1073741819` corresponds to `0xC0000005`. This run is not an official gate replacement.
- The executable SHA-256 is `96B492CB271111251FE42B8646E65370A1B7B566773A1E35B34C3F2D1AE70873`. Build metadata records non-development Mono, Unity 6000.6.3f1, source `a4f9c1fc195c3ec23536053cd9cc9c71ac5d0c10`. Preserve the whole build; the launcher hash alone does not identify all its data.
- The output directory has `wordbound_party.png` (17:59:29Z) and `wordbound_workshop.png` (17:59:37Z), but no final `checks.json`, local-world/menu/return captures, or finished scenario acceptance. I inspected the workshop PNG: it contains the real workshop UI/geometry, dense overlapping labels and a clipped autosave path error. Root inspected both UI captures. Rendering these earlier views does not establish playable-world acceptance.
- The actual player log uses Direct3D 11 on Intel Arc, driver 32.0.101.8860. Its last ordinary line is Fusion adding the local player, followed immediately by the crash report. There is no preceding managed exception stack. Earlier optional URP post-processing shader warnings remain evidence, but are not demonstrated crash causes.

## Minidump facts

Read only the standard minidump directory, exception and module metadata in `C:/Users/mrkl6/AppData/Local/Temp/Wreckabulary Team/Wreckabulary/Crashes/Crash_2026-10-23_175947680/crash.dmp` (1,316,406 bytes). No general memory/string dump was printed. The exception stream identifies the faulting thread and exception record; for access violations, the first parameter distinguishes read/write and the second is the inaccessible address. See [Microsoft's exception stream](https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ns-minidumpapiset-minidump_exception_stream) and [exception parameter definition](https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ns-minidumpapiset-minidump_exception).

| Field | Observed value |
| --- | --- |
| Dump timestamp | 2026-10-23 17:59:48Z |
| Exception thread | 8408 |
| Exception code / flags | `0xC0000005` / 0 |
| Access | Read (`ExceptionInformation[0] = 0`) from address `0x0` |
| Fault instruction | `0x7FF9B5BE059F` inside `UnityPlayer.dll` |
| Module base / fault RVA | `0x7FF9B4B40000` / `0x10A059F` |
| Module image size | 39,845,888 bytes |

The crash directory/log timestamps locate the fault around 17:59:47–48Z; 18:00:00Z is the launcher-observed process completion. The unresolved stack does not establish a graphics-driver defect, an autosave/native causal link, or a specific managed callback. Being in UnityPlayer is a location, not a complete diagnosis.

## Exact symbol capability

The matching symbols are already installed under:

`C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/win64_player_nondevelopment_mono/`

The build and installed `UnityPlayer.dll` both hash to `7C8127007131559A3DAA04AA3A3E8D1477975FE449F59917C5C75D86FA1F904F`. The installed `UnityPlayer_Win64_player_mono_x64.pdb` is 27,463,680 bytes. A bounded read of its MSF info stream gives GUID `742897d4-4d9e-4949-9886-86d4725f961e`, age 1, exactly matching the dump module's RSDS record. No debugger executable was found on PATH; that does not mean symbolization is unavailable. Root owns the proposed offline Windows DbgHelp step with this matching PDB. Unity documents [forensic crash debugging](https://docs.unity3d.com/6000.0/Documentation/Manual/WindowsDebugging-forensic.html).

Logged UnityPlayer RVAs in stack order:

`0x10A059F`, `0x146E38E`, `0x146E67E`, `0x14E76B5`, `0x14FF045`, `0x14FF3C7`, `0x12C780B`, `0x12CA59D`, `0x12CE4FB`, `0x12CE67B`.

The log's generic exported `UnityMain` labels are insufficient to name the internal functions. Resolve the exact fault and callers first; there is no need to modify SDK/packages or relaunch merely to obtain those symbols.

## Managed source boundary and separate usability evidence

The inspected QA, launcher, local-client, controller and workshop sources have no diff from the build source for this path. After the workshop screenshot, `WordboundWorkshopQa.Playtest` calls the real `RequestPlaytest`; the launcher creates the exact local content/client, hides the workshop and awaits `StartLocal`. That client initializes player camera, visuals, guide and HUD before starting the local Fusion session. Scene preparation, course-world construction and host body reconciliation occur during startup. The last player-added line places the failure after runner startup began; without symbols or completed phase observations, it cannot select among those engine-facing operations or prove which completed.

The workshop autosave error is caught and displayed by `WordboundWorkshop.Update`; `RequestPlaytest` validates the in-memory draft and does not require a successful autosave. This explains how the visible error and later player startup can coexist. It does not show that the storage error caused a native null read. The isolated draft directories exist but contain no saved files in this run; investigate that path separately after preserving the error. The crowded labels and clipped error also remain visible usability issues, independent of the crash.

Next acceptance step: use the existing matching PDB to resolve the fault and immediate callers, then select a bounded source/phase check based on those functions. Retain the full failed visible run and official hidden failure. A tested Windows delivery still requires a complete successful workshop→local playtest→return→Undo/Redo run and its captures, followed by the other stated multiplayer/performance gates.

Evidence SHA-256: crash dump `C51E32E6DFBA3EDC5DD2B08C5D8DE40D3BE687D14D5EC8349AA27EF09BFA5409`; player log `384F99851A40850521B200F877E59D12CBD7B55170F9E94B64B8EA5AB90D745A`.
## Offline public-symbol mapping follow-up

Root's DbgHelp attempt could not load the required symbol-helper component (Windows error 126). No download or install followed. I parsed the already matched PDB's MSF directory, DBI symbol-record stream and `S_PUB32` records directly, using the installed Python 3.14 runtime. The first PowerShell parser was stopped at its verified owned PID because its record loop was slow; the completed Python parse took 1.16 seconds. Script/output are local ignored evidence: `.harness/delivery/pdb-public-symbols.py` and `.json` in the puzzle-race worktree. No executable/native game library was loaded.

DBI version 19990903 identifies symbol stream 1147 and public hash stream 1146. The parse checked all 146,731 symbol record boundaries and mapped 128,425 section-addressed public records; 11 non-image-section public records were excluded. PDB section-header stream 7 is byte-identical to the exact build's PE headers, and both OMAP streams are absent. All ten queried addresses fall in `.text`. The method follows the [LLVM MSF format](https://llvm.org/docs/PDB/MsfFile.html), [DBI stream definition](https://llvm.org/docs/PDB/DbiStream.html), and [CodeView public symbol fields](https://github.com/llvm/llvm-project/blob/main/llvm/include/llvm/DebugInfo/CodeView/SymbolRecord.h).

| Logged RVA | Nearest preceding public symbol (readable form) | Symbol RVA | Displacement |
| --- | --- | --- | --- |
| `0x10A059F` | `core::vector<LocalKeywordInfo>::assign` | `0x10A0500` | `+0x9F` (159 bytes) |
| `0x146E38E` | `ShaderRuntime::Variants::VariantUploadData` copy constructor | `0x146E340` | `+0x4E` (78 bytes) |
| `0x146E67E` | `ShaderRuntime::Variants::ShaderVariantMonitor::FrameFinished` | `0x146E4F0` | `+0x18E` (398 bytes) |
| `0x14E76B5` | `PlayerRender` | `0x14E65E0` | `+0x10D5` (4,309 bytes) |
| `0x14FF045` | `ExecutePlayerLoop` | `0x14FEF90` | `+0xB5` (181 bytes) |
| `0x14FF3C7` | `ExecutePlayerLoop` | `0x14FEF90` | `+0x437` (1,079 bytes) |
| `0x12C780B` | `DoQuit` | `0x12C7160` | `+0x6AB` (1,707 bytes) |
| `0x12CA59D` | `PlayerMainWndProc` | `0x12C7E60` | `+0x273D` (10,045 bytes) |
| `0x12CE4FB` | `UnityMainImpl` | `0x12CCC90` | `+0x186B` (6,251 bytes) |
| `0x12CE67B` | `UnityMain` / `UnityMain2` aliases | `0x12CE670` | `+0xB` (11 bytes) |

The JSON preserves the exact decorated names, flags and next-public boundaries; all selected records have the function flag. At the fault, the next public symbol is another `assign` specialization at `0x10A05F0`, after the fault RVA. Nevertheless, a preceding public name is not proof of a function's exact end, inline frames or source line; this maps the existing crash-handler stack and is not a new unwind.

The coherent nearest-symbol chain narrows the lead to shader keyword/variant-copy monitoring during a render/player-loop/quit path. It does **not** establish who requested quit, whether a prior fault initiated shutdown, which shader supplied the data, or a supported workaround. No Fusion physics callback or Intel driver frame appears in this logged chain; absence is not a general exclusion of upstream causes. The last player-added line alone must no longer be treated as a sufficient physics-startup lead.

A bounded project search found no project runtime call to shader-variant tracing; URP shader variant log levels are zero, its export setting is enabled, and GraphicsSettings has no assigned GraphicsStateCollection. Build boot settings enable graphics jobs/threading mode 6. These are observed settings, not demonstrated causes, and none was changed. An official Unity issue search returned no exact matching `ShaderVariantMonitor::FrameFinished` / `LocalKeywordInfo` crash; no engine-version fix or configuration workaround is claimed. Root should use this narrowed engine path to choose the next isolated check while retaining the complete original failed build.
## Targeted editor patch candidate — 2026-10-23T18:29Z

The official [Unity6000.6.4f1 release notes](https://unity.com/releases/editor/whats-new/6000.6.4f1) list UUM-150584, a fix for a random crash during shader-variant uploads in players. This is consistent with the mapped VariantUploadData / ShaderVariantMonitor chain, but the public note has no exact stack or reproducer, so an exact issue match is not proven. No GPU, shader quality, SDK or kit setting has been changed.

Root is installing that one-patch candidate into ignored `.harness/toolchain`, separately from all registered editors. The CLI dry run reports3,851,126,776 bytes, changeset12bfff696524, MD5aKPa2Mx4GBqn0IjZdK3uzQ==. Initial CLI attempt waited for its elevation helper before downloading; root stopped only verified owned installer7808/helper7524. After the installer state cleared, the documented `--no-elevate` user-directory route started downloading (owned CLI39804). No administrator confirmation, terms acceptance override, global install path change, original checkout update or executable DLL substitution occurred. Installation and actual rebuilt-player verification remain pending.

## Patch-candidate compatibility audit

Read-only source/package audit completed against the integration checkout. No editor launch, installation, settings/code edit or native job by this reviewer. The earlier crash, failed official/visible runs and matching original symbols remain unchanged. The exact candidate is `6000.6.4f1 (12bfff696524)` according to the [official release notes](https://unity.com/releases/editor/whats-new/6000.6.4f1); its shader-upload fix is a targeted candidate, not proof this crash is resolved.

The project currently records `6000.6.3f1 (45d8eee7de74)` in `ProjectSettings/ProjectVersion.txt`. `WordboundBuildProfiles.Create` rejects every other `Application.unityVersion` before finding the private build-profile factory. That is a concrete blocker if the profile setup/validation method is used. Existing gate build profiles are already saved at `Assets/Settings/Build Profiles/Win.asset`, `Win-QA.asset` and `Win-Dev.asset`; no separate version rejection in their build route was found. The factory still needs the exact private `CreateInstance(BuildTarget, StandaloneBuildSubtarget)` method and reflected buildTarget/subtarget/platformBuildProfile/development properties, so patch compatibility of that reflection is not established by a successful package audit.

Minimal integration-only candidate changes recommended:

1. Use the existing process-local `GSK_UNITY` override for the isolated candidate executable. Gate `lib/editor.mjs` checks this before cached/registered/Hub paths. No kit edit, Hub registration or global environment change is needed.
2. Keep the integration checkout's ProjectVersion and revision truthful for the actual candidate run. Gate `lib/dotnet.mjs` uses that version for generated reference-check symbols, and `lib/player.mjs` writes it into build.json even when an executable override is used. Leaving the `.3` pin while running `.4` would mislabel evidence. Do not change the original checkout or other worktrees. The editor path/version form part of automatic gate generation freshness; do not hand-edit generated `Check/.gen.json` or caches.
3. If invoking `WordboundBuildProfiles.Create`, allow only the existing `.3` and exact candidate `.4` version, retaining every reflection/scene/profile/QA-define/development validation. Execute that existing validation under the candidate before claiming the private API is compatible. Do not remove the version guard or broadly accept all Unity 6 editors. No other production API change is identified by this audit.
4. Preserve the manifest and lock for initial import, inspect any actual resolver/serialization differences, then run the candidate compile/native/build and complete rendered Windows QA. A successful install or import is not delivery acceptance. Only successful candidate evidence should drive a later project-wide version/documentation decision.

The locked important packages are Input System 1.20.0 (registry), URP/Core/ShaderGraph 17.6.0 (built-in), Test Framework 1.8.0, Burst 2.0.0, Collections/Timeline 6.6.0 and UGUI 2.6.0. The current lock resolves Mono.Cecil 1.11.6 and Newtonsoft.Json 3.2.2; those are existing resolutions, not changes proposed here. Embedded Game Studio Kit declares minimum Unity 6000.3. The official patch's seven explicitly updated package names are all absent from this project's lock, so the notes give no reason to add or upgrade them. Built-in package internals can still differ between editors despite unchanged package versions; native verification remains necessary. Fusion 2.1.3/Physics Addon 2.1.2 are local SDK assets, and the inspected SDK conditionals select the same 6000.6 minor-version path for both patches; no patch-specific blocker was found.

Other pins are outside the immediate gate run: `Tools/CloudSetup/verify-unity.ps1` hard-checks both project and executable version `.3` and will reject the candidate. Its Linux shell/Python installers and `unity-packages.json` also name `.3` download/default paths. Preserve that historical/cloud setup during this isolated Windows investigation; do not reuse it unchanged as candidate verification. `ProductionBuild.cs:45` contains an old `.3` text label only in the missing-module error, not a version gate. README, CLAUDE.md and UNITY-WORKSPACE.md also describe the current `.3` baseline and should not be rewritten as though the patch were already accepted. `Directory.Build.targets` refers to the 6000.6 NUnit layout and discovered UnityData, with no exact patch guard.

Audited input SHA-256: ProjectVersion `A06AF1660C5D19D9BCCC2876B4C0ECBD4A403E08CAD483F3601B18D1BDD20C9C`; manifest `DB1C638BD54A4BB37306CCAEDED26E463FB1EF2A3FC249BE09348DFA435196E3`; lock `028AB0CCF1DA79ACC545B2BF9A55A9F26BF5EDAB950E664CC7D550E138D6A61C`; build-profile source `1C7708A9D01D885D6975D2807028A17747DBF91A270A236B68279C6A24E01005`.

## Visible 190422Z follow-up — completed QA timeout, no repeated crash in this log

Read the completed `docs/qa/runs/20261023T190422Z-visible-wordbound_workshop_flow/checks.json` and matching player log for build `qa-20261023T185900Z`, still Unity 6000.6.3f1. Result is **FAIL: four steps pass, exact_playtest fails**. The log ends with QA result and normal engine cleanup, rather than the earlier native crash stack. This single run does not prove the intermittent engine crash resolved, and no patch-editor acceptance is implied.

Party rendering passed splash/repaint/camera callbacks and varied nonblack 1280x720 capture. Workshop editing passed its screenshot and exact primary autosave recovery: 183,314 bytes, no backup, no leftover temporary files, unchanged history, 164-character temporary path. `exact_playtest` reports only the outer `timed out after 90 s`, with no probes or local-world/equipment capture. Its recorded cumulative time is 100.943695 s, following workshop completion at 9.670795 s; these are not separately measured startup durations. Return passed same history/draft, destroyed client/camera and visible editor; Undo/Redo passed. QA console reports zero captured errors/exceptions/warnings. The log's final Fusion message alone (`adding player`) cannot establish whether gameplay readiness was reached.

The original scenario's observation gap is concrete: its action waits up to 60 seconds for world + canonical frame + unpaused simulation + guide markers, then waits **90 Unity update frames** before taking the first local-world screenshot. The outer action budget remains 90 seconds and includes startup plus that frame wait. No phase/frame/tick evidence existed between those two waits. Thus a timeout and missing image do not distinguish a scene/initialization stall from slow updates after readiness. The local `WordboundLocalSession` uses GameMode.Single, an isolated physics scene and in-memory validated checkpoints; it does not wait for Photon Cloud or a writer lease.

Source comparison limits the passing native evidence:

- The eight local/atomic cases initialize small ProofCatalogue templates and use a no-op visual callback (except deliberate failure/reentry cases). They do not create the player view, artwork, puzzle guide or HUD. Their short passes cannot establish complete flagship player performance.
- The full physical fixture uses flagship content and real puzzle/physics callbacks, but also omits artwork/camera/guide/HUD. Its latest CLOCK trace demonstrated 60 native ticks per Unity frame and approximately eight wall seconds across five coroutine samples; this is a different workload and executable, not a measured Windows frame rate.
- The production client adds all presentation layers. Initial reconciliation spawns/configures every body synchronously; every canonical publication invokes guide-plan construction and presentation/HUD updates, while camera/presentation/guide also run Update/LateUpdate work. These are actual extra cost paths worth measuring, not proven dominant causes in this unprofiled player run.
- No permanent guide readiness cycle was found: the initial FrameChanged can occur before hostReady, so Refresh initially disposes its view, but BeginHost sets hostReady and subsequent LateUpdate can create the already-planned markers. `MenuOpen`/focus neutralize input; they are not inputs to the canonical IsPaused policy. A missing player/camera can prevent camera placement but is not sufficient evidence of a paused simulation. Scene-manager readiness is guarded by a 30-second controller timeout after RunnerStarted; controller/guide faults are checked by the scenario only during its readiness wait.

No production startup, pacing, deadline or SDK change is justified by this artifact alone. Next minimal evidence is phase/frame/tick progress and an immediate readiness image. The requested QA-only source delta implements that without changing acceptance budgets.

## QA-only progress checkpoint — compile/player execution pending

`WordboundWorkshopQa.cs` was aligned to exact integration SHA-256 `C1D923DDDB04522C81B3EFC25189104FAF02EC2EB5FECCA4AB7BF0971FC2680E` in **baseline-only commit 084c3016; exclude it from delivery**. The subsequent delta changes only that project-owned QA file and this report. No kit, scene, runtime startup API, config or other worktree was edited.

The diagnostic records explicit request/readiness/capture-ready/90-frame-wait/world-capture/equipment/completed phases; start/ready/current Unity frames; completed post-ready wait count; current canonical tick; elapsed monotonic time; existing world/frame/controller/guide/presentation/camera/fault/pause facts. It emits scalar probe values and one compact log at phase boundaries and each 15 observed readiness or wait frames, not per simulation tick. At readiness it saves the exact elapsed time/tick and takes `wordbound_local_ready.png` via the real StoryCamera callback, throws on capture error, and uses the existing PNG dimension/nonblank/variety checks. This is a visible artifact for manual inspection, not an image-quality assertion.

The 60-second readiness wait, 90-second outer budget, all required 90 post-ready null yields, original world/equipment captures and exact manifest/save/history/return checks remain. The new ready capture consumes additional work inside that same budget; failure still fails the scenario. The installed kit's `QaRunner.Drive` abandons its nested iterator stack at timeout without disposing it, so no diagnostic relies on a finally handler. Return entry records the last phase plus fresh pre-shutdown facts in the return step's probes/log, then explicitly throws QaFailException if no local client exists. This replaces the unrelated NullReferenceException cascade observed in the preserved hidden 190228Z run; it does not erase the preceding failure or skip a failed action.

Unavailable facts remain explicit: the existing QA assembly has no direct Fusion/Physics dependency and the local client's session is private, so no runner-start/session internals are exposed or reflected. Unity frame count is not proof every frame reached the screen. Source review by Assets is **bounded PASS**; `git diff --check` passed. **Compile/native/player execution NOT RUN** for this diagnostic delta. Root owns the next build/run.

Completed visible artifact SHA-256: checks `50B62E8D457673EC78AD4C67A45D7DB43E98A100C5A98277D4EACFC9A5E580C4`; player log `D38418D22F69BD0ABE428CB1F13EACDFC295FC9F1D17D7FFAC4C34A758BC41DF`.

### Additional source triage while awaiting the observed run

No production edits or new runtime measurements. `WordboundGameplayController.Update` calls Poll synchronously. Its Fusion driver also calls PrepareHostPhysics once per native tick; AdvanceHost performs canonical advance, reconciliation and FrameChanged subscribers synchronously after physics. There is no project coroutine yield or render-frame work budget inside those callbacks. A slow Unity update can therefore coincide with many sequential simulation ticks. The coordinator's native progression totals (including approximately 8,100 ticks in a 258-second full physical case) include setup and scripted operations; dividing those totals is not a clean tick-cost benchmark or Windows FPS measurement. Earlier observed 60-tick batches establish a scheduling possibility, not the cause of this player timeout.

Two concrete additional cost paths are absent or much smaller in the unrendered fixtures:

- `WordboundGameplayBody.Apply` runs from Configure, FixedUpdateNetwork and Render. It converts the network visual key, constructs/validates a WordboundBodyPresentation, and enumerates `VisualRoot.GetComponentsInChildren<Renderer>(true)` to set enabled state even if shape/key did not change. Actual artwork populates that hierarchy; the physics-only fixture leaves it empty. This is repeated allocation/traversal work, not demonstrated leaking or measured dominant cost.
- `WordboundPuzzleGuide.ApplyFrame` rebuilds the marker plan and Refreshes for every canonical publication; Refresh calls Show/FaceLabels, and LateUpdate repeats Refresh. `WordboundMatchHud.ApplyFrame` rebuilds its body lookup and reads a defensive inventory snapshot each publication. These add work during a catch-up batch even when only one screen frame follows. Their measured contribution is still unknown.

No unconditional object-growth loop was found in the bounded presentation paths inspected. GameplayPresentation.Refresh retains instances until body/parent/key/size changes; its Present removes dead views. Guide.Show compares a marker identity/readiness signature before rebuilding objects, and clears old children. StoryView only rebuilds signs when its narrative/progress signature changes. Recipe cards are already cached by letter-bag equality. This rules out those particular unconditional rebuild claims, not every possible leak or GPU/render cost.

The next probe interpretation should remain conditional: `wait_readiness` with no canonical frame localizes startup/scene composition; frame/world present but paused or guide count zero localizes the readiness predicate; `wait_90_frames` with a ready image, advancing ticks and few Unity frames localizes post-readiness update throughput. A capture phase with stalled output remains distinct. Tick/frame deltas alone cannot separate CPU simulation, callbacks, rendering/GPU waits or scheduling contention. Preserve the existing gate result and use a bounded profiler/counter follow-up only after that split is observed. Online's separately owned capacity-count optimization does not overlap these read-only presentation findings.

## Completed visible 192105Z run — post-readiness throughput failure

Build `qa-20261023T191831Z` ran the new diagnostic scenario under the original 6000.6.3f1 release-QA player. Final `checks.json` again records **four passing steps and exact_playtest FAIL at its unchanged 90-second outer budget**. There is no console error/exception, controller/guide/presentation fault, or native crash recorded in this completed run. Party, exact workshop autosave, return/cleanup and Undo/Redo pass. This time the missing phase evidence is resolved:

| Observation | Playtest elapsed s | Unity frame | Canonical tick | Completed 90-frame waits |
| --- | ---: | ---: | ---: | ---: |
| Request started | .002 | 9 | unavailable | 0 |
| Request returned | 4.110 | 9 | unavailable | 0 |
| Ready | 6.611 | 14 | 60 | 0 |
| Ready image captured; frame wait starts | 6.793 | 14 | 60 | 0 |
| Progress | 31.994 | 29 | 960 | 15 |
| Progress | 55.590 | 44 | 1860 | 30 |
| Progress | 76.847 | 59 | 2760 | 45 |
| Return entry after timeout | 91.086 | 68 | 3300 | 53 |

Every complete 15-frame observation interval advanced 900 canonical ticks. Readiness to return advanced 54 Unity frame counts, but the coroutine completed only 53 waits because the outer driver checks its deadline before resuming the next iterator continuation. Do not relabel that boundary as lost frames. At return entry the controller, world, frame and camera existed and were active; readiness remained true, simulation was unpaused, timeScale was one, the app was focused and menu closed. Visual count remained 93 and guide count one in the sampled records. The immediate ready PNG was written and passed 1280x720/nonblank/varied-pixel checks; root independently viewed its original character, world and HUD. This establishes successful startup and a real post-readiness throughput failure in this run, not a stalled scene, missing guide, input-focus pause or capture deadlock. It is still not CPU/GPU attribution or human play acceptance.

The first next comparison should use the same scenario, content/seed, artwork and acceptance budget after the independently reviewed count-only core optimization. Preserve all 90 required waits and do not substitute a longer deadline for responsiveness. Broad visual changes would confound that comparison. The current evidence also does not prove that the optional patch editor is needed for this throughput failure.

### Release-QA timing capability and smallest fallback design

Existing `WB.Match.*` scopes use standard `ProfilerMarker.Auto`. The exact [Unity 6000.6 manual](https://docs.unity3d.com/6000.6/Documentation/Manual/profiler-adding-information-code-intro.html) states that Begin/End are removed from non-development builds and Auto becomes a no-op result. The current QA profile adds GSK_QA, with development_build false. Therefore those scopes cannot be assumed to produce timings in this actual player. [ProfilerRecorder supports release players](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html), but only supported metrics; recorder existence or zero samples is not evidence of zero method cost. Installed CoreModule XML establishes the API signatures, not availability of those custom marker samples in this executable.

If the same player remains slow after the count optimization, the smallest proposed timing change is a project-owned, opt-in **GSK_QA-only** synchronous Stopwatch observer for Capture, Authority.Advance, Reconcile and the FrameChanged invocation inside Reconcile. Each struct scope records count, total elapsed timestamp ticks and maximum in fixed scalar slots, closes in finally, and preserves the original return/exception/control flow. Enable only for this QA action; take the summary at the existing progress/Return boundary, with OnDisable cleanup for interrupted execution. Do not log or allocate strings per canonical tick. Include Stopwatch frequency, start/end phase/frame/tick and scope counts; label durations as synchronous elapsed time, including scheduler/blocking effects, and do not add inclusive Reconcile plus its FrameChanged child as wall time. This would distinguish core reconciliation from subscriber work without enabling a development build, changing simulation settings or relying on stripped markers. It is a design only: no source change or timing measurement was made.

Artifact SHA-256: checks `E12FD34D3D7AA1C7379BB75BE95269B9569A47D5CD36201EA9BF6B296D1FD696`; player log `DEF182598ADDA2647B56A2503149102F124FADDBBDE69277314376D16D4E07AB`.
