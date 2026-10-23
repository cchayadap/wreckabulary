# Workshop recovery source review

STATUS: COMPLETE — bounded independent source review, not native or user-flow acceptance.

Reviewed delta `27042b5`, its exact committed workshop/storage/recovery tests, and current integration launcher/local/private-online composition. No source edits, tests, Unity jobs, deployment or account queries were performed. The asset lane confirmed the separate template-versus-assigned-source follow-up remains design only and was paused before edits; it cannot receive a code-review verdict yet.

## Verdict

No new blocker found within the recovery delta. The source supports preserving authored drafts until an explicit choice:

- Pending recovery returns early from Update and OnGUI; Open, template replacement and Preview require a choice. Disable cancels queued decisions and skips autosave. Back hides/closes while preserving pending state and both files. Focus loss and input suspension discard queued decisions.
- IMGUI only queues a decision. Update processes it after focus/visibility checks, preventing history/view replacement during an IMGUI layout/repaint pass. Controller decisions pass the existing focus/neutral gate.
- Recover rereads the selected ID and compares complete recovery JSON and primary/backup origin. A changed draft updates the displayed choice and requires another confirmation. Read/parse failures leave the choice pending and do not write the files.
- Storage now rejects recovery whose embedded course ID differs from the requested filename ID, including fallback candidates. A valid backup remains an available recovery path after primary corruption. Recovery itself is read-only.
- “Use saved course” copies both existing primary and backup into a fresh archive before replacing the in-memory history or enabling ordinary saves. A failed copy/archive leaves the choice pending; original files are not removed. Partial archives may remain, preserving any successfully copied data. Subsequent attempts use a new directory.
- The authored seed is installed in the initial snapshot with no artificial edit/undo step. Both accepted choices reset the undo boundary and saved revision. View/validation failure during application remains inside the catch with pending state retained; no autosave is enabled until application completes.

## Same-ID source and catalogue boundary

The recovery format binds the course ID and the entire editable draft, not a historical source-asset GUID or catalogue hash. Therefore a draft with the same ID from an older/different authored source can be recovered **only after the explicit choice**, using the currently assigned catalogue/play requirements. The panel states this. `Refresh` validates against that catalogue; preview/local/private-online creation revalidates the actual manifest and its current source/content identities. Missing catalogue modules/assets are skipped by the viewport and surface as validation issues; they do not obtain an unchecked play path.

This policy permits deliberate recovery and correction of an incompatible older draft. It does not prove the old dependencies are available or that a same-ID draft belongs to the currently assigned source. The added fixture covers a different embedded ID, not a same-ID changed catalogue or source. Add focused native evidence for the latter before claiming complete compatibility UX: changed same-ID authored content retains the choice; recovery uses current dependencies; incompatible entries remain visible as validation failures and cannot play; Back and failed Apply preserve both files.

## Known composition issue outside this delta

The launcher retains `workshopSource` from the initially assigned authored asset. Switching the workshop to a template changes history/catalogue but does not clear that source selection. Local/private-online play then still chooses that asset's item/rule JSON and camera/visual profiles. The current UI also has no explicit return to the assigned authored history after template selection. Root/asset lane already identified this; recovery `27042b5` does not repair it and this review does not approve that composition. The paused design proposes a separate retained assigned history/settings and an explicit current-source indicator consulted by the launcher. Review exact code and tests when that work resumes.

## Evidence limits and next check

The eleven added `Wreckabulary.EditorTests.WordboundWorkshopRecoveryTests` cases exercise deferred choice, save gating, primary/backup preservation, changed-file reconfirmation, focus cancellation, file-ID mismatch and failed archive. They remain NOT RUN in this lane. They invoke production methods but do not prove actual rendered/controller usability, platform filesystem behavior, same-ID compatibility handling, or successful local/online play after recovery.

Root can include the class in its next authorized native editor batch:

```powershell
node Tools/kit/gate/game_gate.mjs test editmode --filter 'Wreckabulary.EditorTests.WordboundWorkshopRecoveryTests' --cold --json --timeout 660
```

Require actual XML counts and zero skipped cases; preserve failures. This is a source-review verdict only, with the known source-binding issue still open.
