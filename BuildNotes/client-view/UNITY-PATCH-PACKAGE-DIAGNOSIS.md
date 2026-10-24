# Unity patch candidate package diagnosis

STATUS: COMPLETE — read-only package/reference diagnosis. Repair and native verification NOT RUN in this lane. No editor/job run, cache deletion, package patch, or manifest change.

Root candidate `6000.6.4f1 (12bfff696524)` failed the `WordboundBuildProfiles.Create` cold gate at `20261024T013000Z`, after 370 seconds, with 13 package compilation errors. Raw result: `.harness/delivery/candidate-profiles-0130-stdout.log`; editor log: `Logs/gate_exec_Create_20261024T013000Z.log`.

- Nine errors originate in cached `com.unity.2d.sprite@752945c75a6f`: missing `UnityEditor.U2D.Sprites.SpriteFrameEditor`, `SpriteFrameModeToolStripBase`, and `SpriteFrameModeUndoObject`.
- Four originate in cached `com.unity.render-pipelines.core@285af6b8c83f`: inaccessible `UnityEngine.ObjectDispatcher` and `TypeDispatchData`.
- Root reports package manifest/lock unchanged. Candidate editor selection is recorded as .4; this report will verify the actual compiler references and package provenance before recommending a resolution action.

## Confirmed cause of the compiler errors

The candidate installation itself is incomplete at long paths. Its cache reflects those missing files; this is not evidence that the two engine APIs became incompatible. All 13 reported errors have a source-level explanation below.

The actual log selects `.harness/toolchain/6000.6.4f1/Editor/Unity.exe`, registers the two packages as built-in, and reports package resolution completed in 49 seconds. The generated Bee response files reference this .4 installation's managed assemblies. It also explicitly replaced its .3 Bee compilation state after detecting the editor backend hash change. A stale .3 compiler-reference explanation is not supported.

### Sprite definitions are ignored because their metadata is missing

Log lines 279–282 explicitly report four immutable package `.cs` files without `.meta`, and say the assets are ignored. The four files exist, their contents match the installed .4 built-in copies, and their definitions are absent from the actual Sprite compiler response. Their consumers are included. The .3 installation contains both each identical source file and its metadata.

| File beneath `com.unity.2d.sprite/Editor/SpriteEditorModule/SpriteFrameModule/` | Candidate absolute path characters | Candidate state | .3 state |
| --- | ---: | --- | --- |
| `Overlay/SpriteFrameModeToolStripBase.cs` | 255 | Present | Present, identical SHA |
| `Overlay/SpriteFrameModeToolStripBase.cs.meta` | 260 | Absent in installation and cache | Present |
| `SpriteFameModuleDataProviderOverride.cs.meta` | 260 | Absent in installation and cache | Present |
| `SpriteFrameEditor/SpriteFrameEditorMode.cs.meta` | 263 | Absent in installation and cache | Present |
| `UndoObject/SpriteFrameModeUndoObject.cs.meta` | 260 | Absent in installation and cache | Present |

The last three `.cs` files are present and byte-identical between cache/.4/.3 as well. Their corresponding source hashes, in table order after ToolStrip, are `ebbc6a3196453f529b4e3cf223a2625bb3e3ac82abb1c65934db1771c6d128a7`, `b603242119baf8e460d02df9c5b54e775e9caf9b8c1deacbe1eda7758d457a41`, and `328643313c4a956f1bed87888fab3b633788bfd65bbb20bc6f56253170b2688e`. ToolStrip source hash is `f5604632295f99b4a7b1f4c1bfbb264c5e469b84291859d2be6049dc928b3a8f`.

The missing `SpriteFrameEditorMode` defines the reported missing namespace. The other ignored files define the reported ToolStrip/Undo types. Two additional longer Sprite source files present in .3, `SpriteFrameEditorModeToggle.cs` and `SpriteFrameScenePreview.cs`, are absent from the candidate; therefore supplying only four handmade metadata files would be an incomplete and unjustified repair.

### ObjectDispatcher lost its assembly boundary, not engine friendship

Read-only Mono.Cecil inspection of .3 and .4 `Data/Managed/UnityEngine/UnityEngine.CoreModule.dll` finds identical SHA-256 `a575473c494efdc06d6e066ced4eefd3368e5040e8199d7c5155611672151a2a`. Both `UnityEngine.ObjectDispatcher` and `UnityEngine.TypeDispatchData` are internal in both binaries. Both grant friendship to `Unity.InternalAPIEngineBridge.ObjectDispatcher.Runtime`; neither grants it to the ordinary `Unity.RenderPipelines.Core.Runtime` assembly.

The .3 package contains `Runtime/ObjectDispatcher/Unity.InternalAPIEngineBridge.ObjectDispatcher.Runtime.asmdef` and its metadata. Both are missing from the .4 installation and its cache. The .4 asmdef would be at a **270-character** path; the .3 path is 216 characters. The source file at 228 characters remains present. Without the nested asmdef, Bee includes `ObjectDispatcher.cs` in `Unity.RenderPipelines.Core.Runtime.rsp`, precisely the assembly without the needed friendship. This explains the four protection-level errors.

The wrapper `.cs`, its `AssemblyInfo.cs`, and the enclosing Core Runtime asmdef are byte-identical across all three locations. Wrapper SHA is `0ebd2cdc73a3ef803202f40403696192f4eb253c14304d0b92d857ddbcce65df`; this is not a hand-edited wrapper or different visibility declaration.

## Installation-boundary inference

The two bounded package inventories have maxima of **259 characters**, with zero existing files at 260 or above: Sprite 245 files, Core 2,895 files. Several absent required files begin exactly at 260, while their shorter companion source files exist. This strongly supports a Windows installer path-length truncation/omission in the long isolated destination. It does not by itself identify the installer's exact internal failing call or prove every other installed component complete.

Windows' traditional `MAX_PATH` includes the terminating null character, and long-path support requires application opt-in; changing only a system registry value is not a universal remedy. This matches the observed 259-character maximum. No registry or installer state was changed here. [Microsoft path-length documentation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation).

## Smallest supported recovery sequence (proposal only)

1. Preserve the long-path candidate, its raw log and both package fingerprints (`752945c75a6f`, `285af6b8c83f`) as failed-install evidence. Do not copy .3 metadata/assembly definitions into .4, embed edited packages, add friend assemblies, change asmdef references, or delete package/Bee caches.
2. Root can install the same verified official .4 payload into a **new short destination**, keeping the .3 installation and current project work intact. Merely moving/copying the incomplete tree does not supply files already omitted. No global long-path registry change is needed for this bounded repair.
3. Before an expensive import, inspect the fresh install for the four exact Sprite metadata files, both longer Sprite source files named above, and the ObjectDispatcher bridge asmdef/meta. Confirm its declared assembly name is `Unity.InternalAPIEngineBridge.ObjectDispatcher.Runtime`, then inspect its own CoreModule friendship. This establishes component integrity; it is not a complete installation audit.
4. Select that editor through the existing root-owned toolchain route and permit Unity's normal startup package resolution. This failed run already resolved automatically, so first inspect the new registered locations/fingerprints and required cache files rather than assuming a manifest or lock change is necessary. These dependencies are `source: builtin` in `packages-lock.json`; their payload comes from the selected editor installation despite unchanged version numbers.
5. If package state still needs an explicit supported refresh **after the installation is sound**, use the public `UnityEditor.PackageManager.Client.Resolve()` through an available editor context, with no other Client operation active. The .4 `UnityEditor.CoreModule.dll` contains the public static zero-argument method and its body calls `Resolve_Internal(true)`. Unity documents this as resolving the project and reinstalling altered/missing packages. It is not a repair for absent built-in installation files, and this report does not claim that a compile-blocked project can execute a new project helper or that a synchronous command's return proves resolution finished. [Unity Client.Resolve API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PackageManager.Client.Resolve.html).

## Acceptance and limits

The next actual compile must show the Sprite definition files included, no immutable missing-metadata errors for this set, and ObjectDispatcher compiled into its named internal bridge rather than Core Runtime. Then rerun the unchanged build-profile preparation and appropriate native/build gates under the exact short-path .4 editor. Preserve the original 13-error result; successful package resolution alone is not successful compilation, player compatibility or evidence that .4 fixes the earlier Windows smoke slowdown.

No Wreckabulary source correction or package-version override is justified by these errors. The source-level failure mechanism is established; the installer path-length cause is strongly supported by the exact 259/260 boundary and should be confirmed by the complete short-path installation. Root independently raised the path-length hypothesis and checked wrapper hashes; this lane independently verified missing-file lengths, actual compiler grouping and native assembly metadata. All filesystem inspection was limited to the named logs/configuration, these two package trees, compiler response files and the selected managed DLLs. No native code from the inspected Unity DLLs was executed.
