STATUS: COMPLETE — read-only native setup diagnosis, 2026-10-26. Began with an IN PROGRESS checkpoint; confirmed evidence is appended below.

Initial task context: root identified setup gate053311 as live, normal Burst, gate PID43256 and Unity PID26240 with initial start05:33:11Z. At the initial checkpoint these were parent-reported identities pending verification against the exact owned-process records. Scope: current log tail, existing ILPP diagnosis, exact PID/start-time process samples if useful, and this report only. No process/cache/settings/Assets changes, heavy jobs, command-line/environment/credential output, or production-fix claims.

## Confirmed outcome

STATUS: COMPLETE — source/log/receipt diagnosis only. Gate R3 completed FAIL after303.7s at05:38:15.808Z; gate exit1, crashAfterQuit=false, recorded refresh209.4s. The requested method reached application code and failed with `InvalidOperationException: Cannot create a new scene additively with an untitled scene unsaved.` The exception originates at WorkspaceAssetCopies.Create:27, called by WorkspaceStudioSetup.CreateOrVerify:42 (`Logs/gate_exec_CreateOrVerify_20261026T053311Z.log:1256-1274`). It is a concrete cold-editor scene-state incompatibility, not the earlier terminal ILPP connectivity timeout.

`Logs/exec/20261026T053311Z.json` changed from the reserved empty file at05:37:47Z to234bytes with ok=false, the exact CreateOrVerify method and the same error at05:38:01Z. Unity subsequently invoked normal batch shutdown. Its exit-code0 log does not override the failed exec receipt or the gate's FAIL/exit1 result. Shutdown also reported one import worker force-killed after its shutdown grace, Mono abort-thread warnings and debugger-port listen failure; these occurred after the application exception and do not explain that exception.

Final observed log:122236bytes, modified05:38:14.8235059Z, SHA256 `1F4B057E1129AD815C7B72DE350E4428256BE99B228CF6180F77904ED7697197`. Root-owned end and stdout receipts are `.harness/delivery/workspace-setup-2026-10-26T05-33-11-476Z-end.json` and the matching `-stdout.txt`.

## Startup evidence and prior comparison

| Current053311 observation | Verified evidence |
| --- | --- |
| Compiler prewarm |253.22ms, log:3 |
| Licensing |Connected after11.63s, log:94; validation/access-token warnings preceded successful entitlement/license update. No credential values were inspected. |
| First domain reset |10.857s, log:181 |
| Build graph refresh |Exit4 requested another DAG pass; following build program exited0, log:297-310. |
| Actual compilation |Tundra build success30.51s,101items updated/1612evaluated, log:743. |
| Initial refresh |203.399s, log:890; reported compile83.199s, native Asset DB104.987s, untracked92.649s. These are profiling buckets, not independent causal measurements to sum. |
| Later startup |Initial refresh ended and shader compiler connected in0.14s. Typography font, preset and profile imports then appeared before the prefab-copy exception. This does not establish visual/text correctness. |

The current log contains no `Connectivity with IL Post Processor runner cannot be established yet` marker or Kestrel heartbeat warning. Unlike the earlier034659 attempt described in `BuildNotes/experience-refresh/ilpp26-diagnosis.md`, this attempt completed compilation and entered the requested callback. Startup is still slow, but the present terminal failure does not support additional ILPP/cache/runtime-install probes. The earlier diagnosis remains unchanged: its historical cause was not proven.

## Owned scheduling records

The start receipt anchors gate PID43256 at05:33:11.478Z. Owned records identify the inspected toolchain children by PID, parent PID26240 and exact recorded start time. Records preserve these changes performed by root's owner monitor:

| Record UTC | Child PID/name | Recorded start UTC | Priority before→after |
| --- | --- | --- | --- |
|05:33:44.5426502Z |54160 dotnet.exe |05:33:15.074712Z |BelowNormal→Normal |
|05:33:44.5426502Z |17608 dotnet.exe |05:33:15.252821Z |BelowNormal→Normal |
|05:33:44.5426502Z |21120 Unity.DataStore.exe |05:33:28.377038Z |BelowNormal→Normal |
|05:36:15.1330837Z |48804 Unity.ILPP.Runner.exe |05:35:56.493266Z |BelowNormal→Normal |

The same first/sixth records also show owned UnityAutoQuitter children47480,27352 and41236 restored from BelowNormal to Normal. Record10 at05:38:17.5996872Z lists zero owned descendants. This is what that monitor recorded, not an independent whole-host process census. No optional live CPU/thread/memory sample was taken because the job had completed by the time the record chain was inspected. No process arguments or environment were requested, and no process was changed by this lane.

Successful compiler progress after these measured priority changes is compatible with the scheduling hypothesis, but one run with changed source/workload and unmeasured host contention cannot isolate causation. No statement that priority restoration fixes ILPP, proves starvation, or establishes a production fix is supported.

## Recoverable next action

Root should repair the bounded asset-copy staging path for a cold editor's untitled scene without saving/discarding user authoring work, retain this failure and the partial generated assets, then schedule its chosen source/native checks. No additional infrastructure retry or global setting/cache mutation is indicated by this failure. Only this report was written; all Assets, logs, receipts, processes and configuration remain untouched by this lane.

## Repaired setup and generated-file audit

STATUS: COMPLETE — read-only serialized scene, prefab, font, material and metadata audit. The earlier 053311 failure above remains unchanged. Root owns native testing; this audit does not establish visual quality, live Play behavior or a native-test result.

The repaired setup receipts independently confirm R3 PASS for 054612 in 74s (refresh 49.6s) and 054859 in 46.4s (refresh 27.7s), both exit 0 and crashAfterQuit=false. Their exec receipts have ok=true and error=null: `Logs/exec/20261026T054612Z.json` and `Logs/exec/20261026T054859Z.json`. Gate evidence is in the matching `.harness/delivery/workspace-setup-2026-10-26T05-46-11-987Z-stdout.txt` and `workspace-setup-2026-10-26T05-48-58-727Z-stdout.txt`. Root reports 77 output files byte-identical across the repeat; this lane did not reconstruct the earlier byte snapshot.

### Serialized inventory and references

The audited set contains 35 asset files: 24 prefab variants, 2 scenes, 3 font assets, 5 material presets and 1 typography profile. Every file has its paired `.meta`. Across 3,053 GUID-bearing Assets metadata files, no duplicate GUID was found. The generated set contains 72 distinct serialized GUID references. Every non-builtin reference resolves in Assets except TMP's script GUID `71c1514a6bd24e1e882cebbe1904ce04`, which resolves to `Library/PackageCache/com.unity.ugui@0727b404f3a0/Runtime/TMP/TMP_FontAsset.cs.meta`; that installed package identifies itself as 2.6.0, matching the manifest. No unresolved nonzero local YAML fileID reference was found in the 35 files. This text check does not validate every imported FBX subobject fileID through Unity.

All 24 variants under `Assets/_Project/Prefabs/Equipment` and `Assets/_Project/Prefabs/Furniture` have one source and link to the matching original `Assets/_Project/Art/Imported/Items/{ID}.fbx`. Their removed/added component and GameObject lists are empty. Inventory is 8 weapons (BALL, BAT, BLADE, BROOM, HAMMER, PLATE, SHIELD, SPEAR), 12 tools (APPLE, BOMB, CAKE, CLOCK, FAN, FOAM, LAMP, MAT, PIE, SOAP, SODA, WATER), and 4 furniture items (BED, SOFA, STOOL, TABLE). For example, BAT's source GUID is `93e906069a474aadcaacf0f260c6d1c7`; its variant serializes a nonidentity root rotation.

`Assets/_Project/Scenes/Authoring/EquipmentStudio.unity` contains exactly 24 distinct source-prefab references matching the 24 generated variant GUIDs, with no omitted or extra variant. `CharacterStudio.unity` contains one source-prefab reference to original `Assets/_Project/Art/Imported/Avatar/Avatar.fbx` (GUID `135c19a8edb14d98112c58a9f53fb0f4`), under an EditorOnly preview. Each scene contains exactly one Camera and one Light: an orthographic `Studio Camera` tagged MainCamera (size 12 for Equipment, 1.65 for Character), and a directional `Studio Key` with intensity 1.15. No file named WorkspaceStudioTemp remains anywhere under Assets, including metadata.

### Saved typography dependencies

`Assets/_Project/Resources/UI/LobbyTypography.asset` (GUID `0cc48b4c1e663824cb208ca6e8a467a0`) resolves its script, three fonts and five material presets. Each saved font under `Assets/_Project/UI/Fonts` contains exactly one TMP font object, one base Material and one Texture2D atlas, with valid local references and a source reference to the matching original TTF under `Assets/_Project/Resources/Fonts`. Each is dynamic, multi-atlas enabled, 2048×2048, bilinear, and retains clearDynamicDataOnBuild=false.

The five presets under `Assets/_Project/UI/Materials` reference persisted atlas subassets: Display Stroke/Drop/Foreground use Lilita GUID `25e877e6c39414b4692517c5b67b96c5`, atlas fileID `8147884665497194368`; Body Foreground uses ExtraBold GUID `0aa78122de5f6cc428e7383880dc47d9`, atlas fileID `-5005243527057112160`; Black Foreground uses Black GUID `c912aa71f08918840a51dbf4e35a7f07`, atlas fileID `-4432686748892503319`. Preset dimensions are 2048×2048 and gradient scale matches each base material: 13 for Display (padding 12), 11 for Body/Black (padding 10).

No concrete missing/duplicate reference or inventory blocker was found by this bounded serialized audit. The repaired setup success is separate from the preserved initial failure. Native loading assertions, actual rendering and Play remain root-owned validation; no Unity, native test, source edit, service call, process mutation or asset write was performed by this audit.
