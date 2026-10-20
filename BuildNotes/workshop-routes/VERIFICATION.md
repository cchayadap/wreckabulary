# Route and spawn source verification

STATUS: PASS — focused behavior and compilation; native execution NOT RUN

Integration source base: `20fc3bed52b75e2a3115d737420e4a3a6fa5b311`. Integrated Rules and Workshop paths were clean when checked after the serial runs. The recipes overlay only the two new production files and new tests.

| Check | Result |
| --- | --- |
| `dotnet test BuildNotes/workshop-routes/RoutesTests.csproj --nologo --verbosity minimal --logger "trx;LogFileName=routes.trx" --results-directory BuildNotes/workshop-routes/TestResults` | PASS, 15/15, 791 ms test duration |
| `dotnet build BuildNotes/workshop-routes/ReferenceCompile.csproj --nologo --verbosity minimal` | PASS, zero errors, five known CS0436 overlay/core duplicate-type warnings, 27.82 s |
| `dotnet build BuildNotes/workshop-routes/NativeTestsCompile.csproj --nologo --verbosity minimal` | PASS, zero warnings/errors, 5.09 s |

The Editor fixture references the compiled production overlay as a separate assembly. It does not link the production source into the test assembly. This establishes assembly accessibility and compile compatibility; it does not claim that the component or UI ran in Unity.

The native regression opens two real workshop histories with identical entity IDs and revision, edits pending recovery input, and invokes the actual recovery form loader after the second open. It checks that the new document's values replace old input and invalidate the branch cache.

| Source | SHA-256 |
| --- | --- |
| WorkshopRoutes.cs | 7B03CBF8C9B955CFC90FCD0DBAFAB87C45C271C9949C4C88197E998D56C0016D |
| WordboundWorkshopRoutes.cs | 81699C5AF7F5FC192C9B7E41FAE19AEBCBB352B1C0F3BAC7EDCF07BAB6ABA282 |
| WorkshopRoutesTests.cs | 1BAE2B185144925FFFEEE5BBFE7776244937C66991375DDB47D91B0320F0A084 |
| WordboundWorkshopRoutesTests.cs | 871D9E63DAFA83894727DF5C9906F15EA569DAB83C00C0D3F0EEA176AF0F17CE |
| Integrated WordboundWorkshop.cs | 31571878F5B8D25C9AAA983455CD2B3C62A69D7C6A804E1578AF5EDC531F4C50 |
| Integrated WorkshopDocument.cs | 06861DAACE0E49F83CBF865E284329D70DE5A63BEA81C3D8070C0226B669D095 |

The shared inspector insertion and Unity-generated metadata are intentionally left to root. No native launch, scene mutation, credential use or external deployment was part of these checks.
