# Windows review-build readiness — 2026-10-26

STATUS: READINESS REVIEW COMPLETE. The next Windows build is **NOT-RUN**. This is a bounded, local, read-only source/evidence review, not production acceptance. No build, native test, player, cloud request or live source change was performed by this reviewer.

Snapshot: **2026-10-26T11:15:07Z**, HEAD `1c91fd417542da89dd5a3b3f8761480d6c7d9ca3`, with ongoing root-owned changes. Avatar correction source was present; its saved mesh/profile, `HouseholdStudio.unity` and the Household capture receipt were absent at that observation. Root is continuing serial native work, so later receipts may supersede this snapshot. Preserve all tracked/untracked work and original rejected-art evidence.

Post-snapshot blocker: root identified that the Windows equipment QA passes an outer Wordbound artwork wrapper into a helper expecting the inner avatar root. A correctly corrected player would therefore fail its identity check. The [avatar review](avatar-surface-fix/review.md) has been reopened; a bounded follow-up must pass a test using the actual resolver-created wrapper before Windows equipment smoke is attempted.

## Actual evidence versus pending work

| Area | Evidence opened | Current boundary |
| --- | --- | --- |
| Existing Windows executable | [034544 official receipt](../client-view/official-short-editor-pass-20261024.json), [equipment diagnostic](../client-view/windows-original-equipment-pass-20261024.json) | Historical build `20261024T034544Z`, source `9e013dc9`, Unity 6000.6.4f1. Official Workshop 5/5 PASS; separate equipment 5/5 PASS is explicitly a visible diagnostic. Neither verifies current changes or the new BAT-motion step. |
| Saved Home / combined runtime | [Play XML 104015](../../TestResults/playmode_20261026T104015Z.xml) | 70/70 PASS, zero skipped. Predates the avatar correction; it is not current Windows evidence. |
| Saved Loadout / camera | [Play XML 091621](../../TestResults/playmode_20261026T091621Z.xml) | 82/82 PASS, zero skipped. Same scope limit. |
| Story/presentation | [Editor XML 081750](../../TestResults/editmode_20261026T081750Z.xml) | 144/144 PASS, zero skipped; current source-backed epilogues are implemented. Human story/pacing acceptance remains open. |
| Household importer | [Editor XML 110408](../../TestResults/editmode_20261026T110408Z.xml) | **4/4 PASS**, zero skipped. Earlier 105053/105601/110009 failures remain preserved: untitled-scene isolation and FBX-versus-GLB unit assumptions. This pass tests isolated fixture outputs, not creation/rendering of the final default studio. |
| Avatar correction | [source review](avatar-surface-fix/review.md), [manifest](avatar-surface-fix/manifest.json) | Final 11-file draft syntax/hashes/apply PASS. Saved correction generation, 10 Editor cases, one Play case, repeated setup and actual corrected render remain pending in this snapshot. |
| Household review capture | [capture manifest](household-capture/manifest.json), [run instructions](household-capture/README.md) | Reviewed revision 2 passes syntax/hash/apply checks. Actual four-view capture, preservation receipt and image review remain pending. |

## Required freeze before the build

1. Finish the already scoped avatar and Household work, including Unity-created `.meta` files and saved assets; keep one integrator/native runner. Do not regenerate unrelated profiles, public catalogues, fonts or original art to make this build pass.
2. Avatar: save `Assets/_Project/Art/Corrections/Avatar/SK_Head_Surface.asset` and `Assets/_Project/Resources/Art/AvatarSurfaceCorrection.asset`; verify source-avatar/source-mesh/exact corrected-mesh binding, enabled profile, original FBX/materials/clips unchanged, repeat setup and saved readback. A missing resource silently leaves original appearance by design; successful source compilation alone does not prove correction inclusion.
3. Household: create and repeat `WordboundHouseholdSetup.CreateOrVerify`; preserve all existing authored assets, save the eight categorized prefabs/materials/settings and `HouseholdStudio.unity`, and inspect four actual-prefab capture images plus their source/global/cleanup receipt. The source unit guard must remain intact. Seven furniture pieces have no collision; only the parcel has a static box. These are **optional Unity authoring assets**, not yet registered gameplay/map content.
4. Keep build startup scene order unchanged: Hub, LivingRoom, Tutorial, MovingDay, WordboundLobby, WordboundGameplay. Authoring studios are not startup scenes. Use the existing nondevelopment **Win-QA** profile with `GSK_QA`, global scene list and pinned `C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe`.
5. Record the exact intended HEAD, dirty patch/untracked inventory, authored asset/meta hashes and source freeze. Include pending WorkspaceWindow and generated Shop resource work; do not discard it. Stop source/assets changing through build and comparisons. Capture both gate `head` and freshness `tree`, plus executable hash, build log, `build.json` and `packed_assets.json`. HEAD alone does not identify uncommitted inputs.

## Smallest meaningful checks after integration

Run through the official gate only, cheapest first, with fresh receipts; no kit edits or timeout relaxation is proposed.

- Fresh **R0 `check all`** and required **R1 `core`** on the frozen integration. Earlier passes do not cover later changes. Then native generation/compile and focused tests below; preserve failures before any repair.
- `Wreckabulary.Editor.Tests`: `Wreckabulary.EditorTests.AvatarSurfaceCorrectionTests` (**10 cases**), `NativeAnimationBindingTests`, `PlayerAuthoringTests`; retain strict original FBX/clip and existing-artist no-op assertions. The 10 cases include **27** clip/expression combinations with unchanged deformed positions and corrected surface checks.
- `Wreckabulary.Tests`: `AvatarSurfaceRuntimeTests` (**one case**), `PlayerAppearanceTests`, `NativeAnimationStateTests`. Confirm actual authored-player initialization, idle/walk/swing/blink/brow, held equipment, custom mesh preservation and shared saved mesh identity. Capture continuous transitions as well as sampled poses; static passes cannot prove motion quality.
- Household final-source fixture: `Wreckabulary.Tests.Editor.WordboundHouseholdSetupTests` (**four cases**). The observed 110408 pass can serve this unchanged source; repeat it only if importer/tests/dependencies change. Separately run default creation/repeat/capture, because isolated fixture success is not default-asset creation.
- Relevant UI combined regression: Home/Shop/Loadout runtime layout, lobby camera, runtime typography and ShopCollections tests. Verify saved choices, profile wiring, hover/selection restoration and no authored font mutation. Retain/reuse the already passing checks if their source/dependency set is unchanged; broaden only for a new failure or changed shared hook.
- Verify source/corrected asset bytes and scene/font preservation after native Play. Raw portrait/original-art diagnostic A intentionally bypasses correction; **do not use its unchanged dark original as the production corrected-avatar result**. Capture the corrected production spawn in Shop/Lobby and gameplay separately, under matched lighting.

## One Windows build, then three serial smoke runs

The current [Windows runbook](../developer-workspace/windows-next.md) still gives the correct gate sequence. Use a fresh build ID and preserve 034544:

```powershell
node Tools/kit/gate/game_gate.mjs build --profile release-qa --timeout 1800 --json
node Tools/kit/gate/game_gate.mjs smoke --build <returned-build-id> --scenario smoke_frontend --seconds 90 --seed 1 --json
node Tools/kit/gate/game_gate.mjs smoke --build <same-build-id> --scenario wordbound_workshop_flow --seconds 90 --seed 1 --json
node Tools/kit/gate/game_gate.mjs smoke --build <same-build-id> --scenario wordbound_original_equipment --seconds 90 --seed 1 --json
```

Substitute the verified ID; never use `latest`. Review each result before starting the next. Current gate launches a visible 1280×720 player without batchmode and adds 30 seconds to the 90-second smoke watchdog. Preserve any timeout instead of increasing limits to conceal it.

| Scenario | Required steps and observations |
| --- | --- |
| `smoke_frontend` | **3**: boot, front_end_idle, front_end_screenshot. Verify current Home screenshot, camera/frame progress and clean startup. Its minimum average-FPS assertion is **1**, not a performance acceptance target. |
| `wordbound_workshop_flow` | **5**: open_wordbound, edit_workshop, exact_playtest, return_to_workshop, undo_redo_preserved. Exact primary autosave/recovery, manifest/local bodies, actual images, camera/client teardown and preserved draft/history. No network session is proved. |
| `wordbound_original_equipment` | **6**, not historical five: equipment_open, equipment_world, equipment_bat, **equipment_bat_motion**, equipment_hammer, equipment_return. Real collect/craft/equip/Attack receipt, canonical weapon/timing, natural phase frames, motion JSON, grounded character and unchanged returned draft. |

For corrected avatar equipment evidence, require exact original identity for all other meshes and exactly the reviewed saved head replacement: `original_mesh_identity=false`, `bound_head_surface_correction=true`, correct source fingerprint. The bounded allowance must reject any second changed mesh. Do not restore the inverted head merely to satisfy the actual-player smoke.

Acceptance for each QA smoke requires **gate PASS + checks.json PASS + every expected step + exit 0 + timed_out=false**, no unexpected console errors/exceptions/warnings, actual nonblank current captures and clean normal shutdown. The gate's nonstripped final expression currently does not independently require exit 0, so inspect `smoke.exit` explicitly. Confirm framebuffer images and reported source/build identity, not just log presence.

## Manual Windows inspection and honest delivery limits

- Use that same executable normally for Home → Shop → Loadout, keyboard/controller focus, category/selection/hover, ownership/equip/cost state, cosmetic persistence, portrait lighting, camera comfort and concise text at 1280×720. Existing automated scenarios do **not** navigate these new pages. Check face/eye/brow/skin shading and seams in motion, not only a tiny Home portrait.
- Test Workshop edit/save/close/reopen/play/return once in the built player without clearing user preferences or overwriting an existing course. Existing native persistence tests and an in-process smoke do not prove new-process Windows recovery.
- Household art will be available in the Unity studio; it will **not appear in Windows maps** until explicitly wired into reviewed map content. FAN V2, richer destination kits, fully painted skins, all equipment motion/collision/LOD work and owner art approval remain separate. Do not describe this QA build as including those unfinished designs.
- No registered Windows public-match scenario exists in the inspected project. Registered project scenarios are Workshop, original equipment and `wordbound_synthetic8_low`; the latter is a local 1080p/60-second synthetic workload with zero remote peers. It is not eight-player online proof. Full five-mode multiplayer/full-match/recovery acceptance, TimeRun4/Freestyle8 retries and historic temporary-party cleanup remain open in the [separate online runbook](../developer-workspace/online-next.md). This review made no service calls.
- The 60 FPS target remains unresolved. The synthetic scenario has a 16.6 ms p95 assertion, but no new controlled result exists here. Other heavy jobs remain running at James's request; record contention and do not make causal performance claims from unlike runs. Synthetic timing and presented gameplay performance remain distinct.
- A stripped `release` build and its own `--stripped` smoke are later separate artifacts, not prerequisites for labeling this explicitly a **tested review QA build**. Neither is full production acceptance.

Before handing over a new executable, update the user-facing delivery pointer to its exact path and scope. `WINDOWS-DELIVERY.md` still leads with an older 005254 build, and `TRY-WORDBOUND.md` correctly points to 034544 but incorrectly says the new epilogues are unimplemented. Keep those historical results; supersede their delivery summary with fresh evidence. `docs/KNOWN_ISSUES.md` currently has no issue rows, which is not proof that the outstanding risks in `progress.md` and this review are resolved.

Source anchors inspected: `Tools/kit/gate/lib/player.mjs` (build provenance, visible launch, checks/exit semantics); `Tools/kit/gate/gate.config.json`; `ProjectSettings/{ProjectVersion.txt,EditorBuildSettings.asset}`; `Assets/Settings/Build Profiles/Win-QA.asset`; `WordboundOriginalEquipmentQa`, `WordboundOriginalEquipmentView`, `WordboundMeleeCapture`, `WordboundWorkshopQa`, `WordboundPerformanceQa`; kit `SmokeFrontendScenario`; current Avatar/Household setup and test files. No live instructions, code, progress or ledger were changed.
