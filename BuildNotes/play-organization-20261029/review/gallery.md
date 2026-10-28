# Gallery generator

STATUS: PREPARED — AWAITING VERIFIED NEW RUNS

`generate-review.mjs` creates a new `REVIEW.html` and `review-manifest.json` in this folder after all inputs validate. The old gallery remains untouched. It has seven review areas, defaults to preferred build232801 versus the actual new build, and can show rejected155755 as an optional third comparison. It uses no external assets and never processes screenshot bytes.

```powershell
node BuildNotes/play-organization-20261029/review/generate-review.mjs --build-id NEW_BUILD_ID --lobby EXACT_LOBBY_RUN_ID --solo EXACT_SOLO_RUN_ID --workshop EXACT_WORKSHOP_RUN_ID --include-rejected --exe PATH_TO_NEW_EXE
```

Run IDs must be the exact folder names beneath `Docs/qa/runs`. Supply the new build's verified run IDs; placeholders are intentionally not runnable. `--exe` and `--include-rejected` are optional. A later gallery can use a fresh `--out BuildNotes/.../revision2/REVIEW.html`; existing HTML/manifests are never overwritten.

Validation requires matching run/build/scenario identity, player platform, completed timestamps, overall PASS, every step PASS with no assertion failures, unique declared screen PNGs, all original image files inside the correct run, valid PNG signatures/dimensions, and every step capture reference declared. Every available declared new state is included; no hard-coded45-frame roster hides additional views or invents missing ones. Baseline/rejected runs receive the same validation. Each input check/image, linked file, generator and generated HTML has a SHA256 record in the manifest. Scenario counts are shown with exact `checks.json` links.

Default historical inputs are the verified232801 lobby233250/solo233412/workshop233042 runs; optional rejected inputs are155755 lobby155953/solo160051/workshop160232. Images match only on scenario plus capture name. No matching historical image produces an explicit missing-view message. New states still appear. Full screenshots retain all labels and remain clickable originals.

Only JavaScript syntax and `--help` are checked during preparation. No new gallery is generated until root supplies the actual verified new runs. An automated PASS is not aesthetic acceptance or production readiness. The optional executable link is hashed for identity but does not independently prove that binary's embedded build ID; root's delivery audit supplies that binding.
