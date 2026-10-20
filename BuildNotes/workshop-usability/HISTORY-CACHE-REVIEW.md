# Workshop input cache identity

STATUS: SOURCE AND INSTALLED-REFERENCE COMPILE GREEN — NATIVE REGRESSION NOT RUN

During a bounded read-only review of the online owner's new route editor, confirmed a stale input-cache condition: key + revision does not distinguish two different WorkshopHistory objects with identical authored IDs and revision. Open a second course after leaving unsaved recovery fields in the first; the old editor fields survive and Apply can overwrite the new course. The online owner confirmed and repaired its route partial by including History identity, with a new component regression.

Sibling inspection found the same condition in this lane's physical-site editor. `LoadSiteEditor` now includes ReferenceEquals(siteEditHistory, History), clears unsaved numeric inputs and the open selector on a new history, and retains the existing key/revision behavior for normal edits. A native editor regression opens two actual histories with the same site IDs/revision and different positions, leaves unsaved fields in the first, then verifies the second history reloads its own site without mutating the document. Reflection only invokes the same private loader used by the IMGUI panel; no synthetic history revision or runtime state injection is used.

Own source paths: Workshop/WordboundWorkshopSites.cs and Tests/Editor/WordboundWorkshopModeTests.cs. No online-owner source or shared ResetView edits.

Coordinated installed-Unity reference compilation passed with **0 errors, 23 known CS0436 warnings**, 8.65 seconds, exit 0. The warnings arise because this additive check deliberately overlays source workshop classes over the previously compiled main assembly; source definitions win. Three extra warnings relative to the previous check are the regression's reflection type references to the same overlaid component. No warning was suppressed.

```powershell
dotnet build BuildNotes/workshop-mode-sites/ReferenceCompile.csproj --nologo --verbosity minimal -m:1 -p:UseSharedCompilation=false -p:RulesSourceRoot=../../../wordbound-integration
```

Evidence: `history-cache-reference.log`, SHA-256 `265ADD206519EDBFA3EC3095949F463A56F1098A5D89F555F20ED94460327321`. The check links installed Unity `6000.6.3f1` references and current integration Rules source; integration HEAD at evidence capture was `20fc3bed52b75e2a3115d737420e4a3a6fa5b311`. The referenced main DLL SHA-256 was `5FB78A26C8A3596E6DAF4C3FDA4A7D9498CF645F00864472DE6623579EC98892`.

Compiled source SHA-256 pins: `WordboundWorkshopSites.cs` = `A7C74F335041BCEB31D365DB28D2FA22D4B1036CFBAC15E1B62C4950E4F2D1BB`; `WordboundWorkshopModeTests.cs` = `194AAFBF03CC2832CFB320D8E509CBF01004E7D3920F1514F90C1249FDE7910C`.

No Unity ran in this lane. Next: root native execution of `OpeningAnotherHistoryWithMatchingSiteIdsCannotReuseUnsavedPhysicalFields` (the fourth case in WordboundWorkshopModeTests). Compilation does not establish the actual component/history behavior or human authoring usability.
