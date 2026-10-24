# Short-install residual package warnings

STATUS: COMPLETE — bounded read-only source/file diagnosis. The current gate emitted the same Sprite/Core compiler errors after restoring incomplete resolved package caches. Root owns terminal gate classification and repair. No Unity, package/cache/meta mutation, restart, or process action by this lane.

The earlier 14-file verification covered Sprite source/metadata and the Core ObjectDispatcher bridge, not ShaderGraph or all URP files. Root reports residual immutable-metadata and outside-assembly warnings in `Logs/gate_exec_Create_20261024T030743Z.log`.

The current log explicitly restores package resolution from cache (line 213), then registers URP 17.6.0 at `Library/PackageCache/com.unity.render-pipelines.universal@b0678dfc9e21` (line 227) and ShaderGraph 17.6.0 at `Library/PackageCache/com.unity.shadergraph@58bcb150677d` (line 265). `Library/PackageManager/ProjectCache` independently maps the corresponding `Packages/...` asset paths to those exact directories. Those are the locations to compare with the fresh short installation; the warnings alone do not establish which copy is incomplete.

## Current files, not only historical warnings

The log initializes exact editor `6000.6.4f1 (12bfff696524)` at line 153. Comparisons were made against its completed installation at `C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Data/Resources/PackageManager/BuiltInPackages`. Every fresh source/asmdef and metadata file in the following bounded sample exists. The named resolved caches still omit the metadata or assembly boundary shown below.

| Package and relative file | Fresh short installation | Actually resolved cache | Evidence |
| --- | --- | --- | --- |
| ShaderGraph: `Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRGBufferPass.hlsl` | Source and `.meta` present | Identical source; `.meta` absent | Current immutable warning line 355 |
| Sprite: `Editor/SpriteEditorModule/SpriteFrameModule/Overlay/SpriteFrameModeToolStripBase.cs` | Source and `.meta` present | Identical source; `.meta` absent | Current warning line 277; previous source omission is still active in this cache |
| Core: `Runtime/ObjectDispatcher/Unity.InternalAPIEngineBridge.ObjectDispatcher.Runtime.asmdef` | Asmdef and `.meta` present | Both absent | Expected bridge assembly boundary remains missing in the actual resolved package |
| Core: `Editor-PrivateShared/Tools/Converter/RenderPipelineConverterAssetItem.cs` | Source and `.meta` present | Identical source; `.meta` absent | Current warning line 281 |
| URP: `Runtime/Unity.RenderPipelines.Universal.Runtime.asmdef` | Asmdef and `.meta` present | Both present, both hashes identical | This main assembly is not the missing boundary for the warned subdirectories |
| URP: `Editor/Unity.RenderPipelines.Universal.Editor.asmdef` | Asmdef and `.meta` present | Both present, both hashes identical | Same bounded conclusion |
| URP: `ShaderLibrary/Unity.RenderPipelines.Universal.ShaderLibrary.asmdef` | Asmdef and `.meta` present | Identical asmdef; `.meta` absent | Warned `Deprecated.cs`, `ShaderTypes.cs`, `Debug/DebugViewEnums.cs` lie under this boundary (lines 365–368) |
| URP: `Tests/Runtime/Unity.RenderPipelines.Universal.Runtime.Tests.asmdef` | Asmdef and `.meta` present | Identical asmdef; `.meta` absent | Runtime test scripts warned outside an assembly (lines 363–373) |

URP/Sprite/Core resolved directories are `com.unity.render-pipelines.universal@b0678dfc9e21`, `com.unity.2d.sprite@752945c75a6f`, and `com.unity.render-pipelines.core@285af6b8c83f`; ShaderGraph is `com.unity.shadergraph@58bcb150677d`. All are beneath this integration checkout's `Library/PackageCache`. Both `packages-lock.json` and the current registered-package log identify the relevant packages as built-in; installed version numbers alone do not distinguish complete from incomplete bytes.

The ShaderGraph example source is 3,707 bytes with SHA-256 `92d02cfc901165a51f977b918682cb48e827f5209b30aa9b904142384bb42cd2` in both locations. Its fresh metadata is 204 bytes, SHA-256 `e0555bb9b31f3c333fb150476b29b6619fe10a5fe539bb4e0465e28a70aea5f8`. The missing cache metadata path is 211 characters, so this observation does not indicate a new 260-character copy boundary in the current cache path.

URP ShaderLibrary asmdef SHA-256 is `abc54315cacf6aa2745122d8ce98fcd07b67126bb23e8896c8e74ba95aacf96e`; its fresh metadata SHA is `0d1e3b4e6fee5b24f4728603c6fd7eff75d9be13303588566d69fdf56637fa12`. Runtime.Tests asmdef SHA is `211cad0590a032845aad86075b5d8279d68a880fa1709881d1137bfe99074380`; fresh metadata SHA is `67bee6ea2f16e0b11a9b9174ff7235b04de0b595f61462ea10c46a773b945e43`. Both missing cache metadata paths are 211 characters. These exact matches plus absent metadata explain why the main URP asmdefs being intact does not close the subdirectory warnings.

## Interpretation and next boundary

These are current missing files in the directories actually registered by this run, not merely warnings repeated from an old log. The fresh short installation passes every named source/meta comparison, while normal startup restores the old incomplete package copies. The earlier 14-critical-file installation integrity PASS remains accurate within its stated scope; it did not verify or replace the project's resolved caches.

The current log subsequently emits nine distinct Sprite compiler errors at lines 2708–2724 and four Core access errors at lines 3479–3485, repeated again in its compiler summary. This matches the earlier failure mechanism: Sprite definitions ignored without metadata and ObjectDispatcher compiled without its own friend-assembly boundary. No project gameplay-source defect is established by these errors.

Root should preserve the current failed gate and select a supported package-resolution recovery that actually repopulates the affected built-in package copies from the verified short installation. Do not hand-author metadata or patch visibility/source/asmdefs. A package resolution call returning is insufficient: before the next expensive compile, check the exact resolved paths and source/meta/bridge pairs above. No cache deletion, replacement, quarantine or resolution operation is authorized or performed by this report.

This was a targeted comparison of eight named source/assembly-definition pairs and four short directory listings for URP assembly boundaries, not a whole-package completeness scan. It does not prove the cause of every warning, whole-install completeness, or any runtime/player result. Root received the current mismatch and compiler-error findings immediately; further actions remain root-owned.
