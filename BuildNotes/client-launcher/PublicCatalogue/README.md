# Public catalogue source gates

STATUS: SOURCE GATES PASS — NATIVE EXPORT/ACTIVATION NOT RUN

Run only after the root integration owner releases the compiler slot. These projects reference the installed Unity 6000.6.3f1 assemblies and current integration checkout's compiled Rules, Puzzles, core and Fusion assemblies; they do not copy vendor source or launch Unity.

From the online worktree:

```powershell
$env:WORDBOUND_PROJECT_ROOT = (Get-Location).Path
dotnet test BuildNotes/client-launcher/PublicCatalogue/Tests.csproj --filter FullyQualifiedName~WordboundPublicCourseCatalogTests --logger "trx;LogFileName=public-catalogue.trx" --results-directory BuildNotes/client-launcher/PublicCatalogue/results -v minimal
dotnet build BuildNotes/client-launcher/PublicCatalogue/Editor.csproj -v minimal
```

Run the commands sequentially and inspect each result. The test project references the runtime DLL across an assembly boundary. The Editor project separately compiles the exporter against the runtime DLL and actual Unity Editor APIs. Native menu interaction/import/asset assignment and the production Worker projection remain separate gates.

`IntegrationRoot` and `UnityData` can be supplied as explicit MSBuild properties on another machine. The checked-in recipes default to this workspace's integration sibling and installed Unity version. No generated output is tracked.

Recorded result: 17/17 focused cases passed; runtime and Editor reference compilation passed, with zero warnings/errors in the Editor gate. See `verification.json` for exact dependency/output hashes and the unexecuted native/external steps.
