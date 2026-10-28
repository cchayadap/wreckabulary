# Inspector Windows evidence verifier

STATUS: FROZEN PREPARATION; no new inspector Windows run audited yet.

Run the new flavor from the exact fresh Windows build, then generate a final authored-file preservation receipt and the unchanged strict 45-image audit using that receipt. The preservation receipt must be later than the inspector run as well as the three original runs. Keep the independent 18-image Parcel audit unchanged.

```powershell
node BuildNotes/creator-clarity-20261029/workshop/extra-capture/verify-inspector-windows.mjs `
  --root C:/Users/mrkl6/Downloads/Projects/Wreckabulary/wordbound-integration `
  --build EXACT_BUILD_ID `
  --source-commit EXACT_FULL_BUILT_COMMIT `
  --base-audit EXACT_SUCCESSFUL_STRICT_45_IMAGE_AUDIT_JSON `
  --run Docs/qa/runs/EXACT_TIMESTAMP-wordbound_parcel_workshop_inspector `
  --gate EXACT_SINGLE_GATE_STDOUT_PATH `
  --out BuildNotes/creator-clarity-20261029/workshop/extra-capture/NEW_RESULT.json
```

All seven CLI arguments are mandatory and exact. Output is opened with exclusive create (`wx`) only after every check; parent directory must already exist. Existing evidence is never overwritten. Run files and isolated Temp/wbqa GUID autosaves must remain available for current-primary comparison. The verifier only reads evidence, runs read-only Git queries and creates the explicitly named new receipt; it launches no Unity/player process and changes no Tools or existing audits.

The strict base audit supplies build/packed-file/original-45-image evidence. This verifier rebinds its verifier, build receipt/gate, executable, packed manifest and scenario source hashes, checks the exact build/source IDs, and checks tracked/nonignored production paths are clean against the built commit before and after verification. It rebinds the final preservation receipt/baseline, requires actual PASS/empty changes/matching baseline hash and file count, and compares the actual receipt UTC with the new run end.

The new run must be terminal gate PASS/exit 0, correct release-QA build, normal scenario, no timeout/stripping, clean console, exact five PASS steps, eleven captures in exact step/aggregate order and no extra PNG. Each image must have advancing rendered frame, focus, dimensions equal to recorded window dimensions, full PNG chunk CRCs, expected RGB/RGBA header, valid bounded zlib scanline bytes/filter tags, IEND and SHA256. Original camera, saved-draft, Single playtest, return/history, source/preferences/device preservation probes remain required. New inspection probes require Puzzles/New puzzle and Room/Routes & checkpoints/Safe, Sorting selection, exact course ID, in-window inspector rectangle and successful UI/history/source/autosave restoration with no cleanup errors.

Limits: section state and normal window/cleanup assertions are from the source-bound game scenario, not a physical pointer/controller trace or an independent OS observation. Inspector reference IDs are recorded, not proof that every selectable recipe/route works. PNG validation does not make a taste/fit or full pixel-decoding claim. Visual acceptance requires opening these unchanged full screenshots. Historic per-run executable hashes are unavailable and embedded run commit/tree may be null; ignored production files are outside Git comparison. Full route completion, online/public matchmaking, performance and production approval are outside scope.

Preparation evidence: node --check and ESM import passed. Roster is 5 steps/11 unique images. An unchanged old 1280x720 PNG passed the structure/CRC/zlib checks. Five negative checks rejected a corrupted PNG CRC, truncated PNG, missing CLI arguments, overwrite of an existing sentinel (bytes preserved), and an old four-step scenario run ID. No synthetic game PASS receipt was created. See verifier-check.json and verifier-manifest.json for frozen scope/bytes.
