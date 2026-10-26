# Workshop range controls — independent native audit

STATUS: PASS within the source, rule, Editor-handler and authored-file preservation scope. Windows rendered/input acceptance is still pending. No native launches, source changes or service writes by this reviewer.

## Exact verification

| Gate | Independent result | Gate duration |
| --- | --- | ---: |
| R0 230250 | PASS; Runtime and Editor checks | 14.1 s |
| Focused core 230139 | PASS, **89/89**, zero failed/skipped; raw TRX verified | 36.1 s |
| Editor 230329 | PASS, **35/35**, zero failed/skipped; raw XML verified | 115.7 s |

The Editor total is **26 `WordboundWorkshopPlacementTests` + 9 `WordboundWorkshopTests`**. Its actual test XML duration is 1.6324547 s; the larger gate time includes cold import/startup, with 92.8 s refresh reported by the gate. Gate stdout names the exact log and XML audited and records normal exit, no infrastructure failure and no crash-after-quit. Actual command arguments, wrapper start/end receipts, stdout/stderr, gate logs and XML/TRX are SHA-256 pinned in `workshop-range-native.json`. The core selection is the six recorded Workshop rule/bundle fixture filters, not every project rule test.

## Source identity

The run used `547f258f31dc0acf5c9e1dadfcfcf419c9fc4bf3` plus frozen revision 1 `afdd790155e1310ca8fa23f8167d0c58f0a00d16ed9388809ade52ae0d8c8b1f` and incremental revision 2 `f8cf0beb007b0d52280fd107dbf85c187ffd6e7286f19c273d37cda50179a963`. Root saved the four proven files afterward as `359047caf5af3beb96160520eef2d471ab526eb6`. The latter is the post-run commit, not the pre-run HEAD; the wrapper itself does not record a source revision.

Both patch hashes, both manifests' baseline/candidate hashes and the final four live files were independently checked. Final live contents are byte-identical to the selected frozen candidates:

| Live file | Frozen candidate |
| --- | --- |
| `WordboundWorkshop.cs` | revision 1, `638499c1…` |
| `WordboundWorkshopPlacement.cs` | revision 1, `ace186e1…` |
| `WordboundWorkshopPlacementPanel.cs` | revision 2, `6ae6b696…` |
| `WordboundWorkshopPlacementTests.cs` | revision 2, `c0dc8bf6…` |

Git readback confirms the saved commit changes exactly these three runtime files and the existing Editor fixture. Their saved Git blobs match live files after normal line-ending normalization; all full raw hashes are retained in the JSON. No hand-authored `.meta`, scene, material, catalogue or schema change is part of this patch.

## What the passing cases establish

- World-space object-base height bounds, supported quarter-turn rotations and permitted-floor lists round-trip through canonical export/import and one Undo/Redo edit. Unrelated zone state, owning room, grid/purpose and omitted-override compatibility are preserved; list inputs are copied defensively.
- Eight invalid height/yaw/floor cases preserve the exact snapshot object, revision and usable redo. Earlier count/range/weight/lock/owner checks remain in the fixture.
- An owned real `WordboundWorkshop` component stages the existing zone into its panel fields. Applying unfinished height text `-` rejects the edit without replacing history or losing redo. Cancel restores all staged fields. An unchanged Apply reports **No changes** and retains snapshot/revision/redo. A changed Apply reports **Applied**, changes only the intended Y/yaw/floor values, and produces one reversible history edit.
- The nine companion cases retain template/autosave and playtest-dispatch/history contracts, reject stale valid playtests after an invalid draft, and exercise viewport projection/picking and owned camera/render-target disposal.

The panel test invokes the production private handlers through reflection and uses isolated temporary storage; it does not click the on-screen IMGUI controls or exercise physical keyboard/controller navigation. The nine companion cases are Editor contracts, not a full Windows traversal.

## Authored assets remained unchanged

Independently rehashed all **4,208** files from `.harness/delivery/ui-authored-before-2026-10-26T23-03-16-737Z.json`. **Zero changed or missing files.** Repeating the original read-only scope enumeration also yields exactly 4,208 paths, with no added paths in that scope. The baseline itself retained SHA-256 `9e571c052cf074abf43628a4902d68f97a0b77c7193f4c61793878191fe205d1` before and after the audit.

That denominator comprises non-ignored Assets `.asset`, `.unity`, `.prefab`, `.mat`, `.ttf`, `.otf` files and their existing adjacent metadata, plus `ProjectSettings/EditorBuildSettings.asset` and `ProjectSettings/ProjectSettings.asset`. It is not a claim about every image/model/source file, ignored SDK file, Library cache or other user's workspace. The exact source changes are verified separately above.

## Remaining acceptance

The range is tied to its owning room. **Allowed floors is permitted-floor metadata, not cross-room or cross-floor spawning.** Height is the object's base Y in world metres; full-object fit, collision and resolved preview validation remain separate checks. Structurally valid but geometrically unsuitable drafts can still need correction in the existing validator.

Next verify the actual runtime Workshop at narrow and normal Windows sizes and maximum text scale: expanded Range help / Rotation & floors, all controls reachable, malformed input feedback, Cancel, unchanged and changed Apply, real Undo/Redo, resolved placement preview and export/import. Keep physical keyboard/controller and visual acceptance separate from these passing handler/rule tests. No current Windows capture, final art quality or blanket workshop/production completion is claimed by this receipt.
