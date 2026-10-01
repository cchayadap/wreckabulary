# Wreckabulary progress

## Resume here

Work on `feature/wreckabulary-production`, based on the latest laptop commit
`6030d6b` from `codex/latest-wreckabulary-2026-10-01`. Read [the production plan](../PLAN.md)
and [the current README](../../README.md). The laptop progress file was absent from
that commit; this record contains observed cloud work and checks.

The immediate gate is **Unity Personal activation through Unity Hub on the editor
machine**. The exact editor is installed, but it exits with code 198 before import
because no valid UnityEditor license/entitlements are available. After activation:

1. Open the project with Unity **6000.6.3f1**, revision `45d8eee7de74`.
2. Run **Wreckabulary → Set Up Art and Data** to populate imported animation clip
   references and refresh the generated libraries. Preserve existing scene edits.
3. Run `bash Tools/CloudSetup/verify-unity.sh`, then the same command with `--build`.
   Inspect the fresh EditMode/PlayMode XML and build logs; require nonzero real tests.
4. Inspect actual native gameplay, wardrobe/animation, wall/door collisions,
   touch/controller input and complete mode loops on both maps.
5. Install matching Windows/Android modules for those targets and profile actual
   target hardware. Linux and Unity Web modules are already installed here; iOS
   needs a supported Mac workflow.

Do not mark the complete Unity/platform release finished until those gates pass.
No account credentials or license contents belong in chat or this repository.

## Delivered implementation

- Shared contract: 100 HP, 18 loose letters including craft reservations, two
  carried gear slots and two deployed items. Exact-word crafting, interruption,
  one-time reusable refunds and spent consumables use the enabled catalogue.
- All twelve recipes: BALL, BAT, BED, BLADE, BOMB, FOAM, LAMP, MAT, PLATE, SOAP,
  SOFA and TABLE. Authored attack/use/place timings, durability, shield, bomb,
  recoverable throw, soap zone, jump pad, directional speed strip and cover.
- Connected Pinwheel House (20 × 20 m) and Garden Courtyard (32 × 32 m), with
  distinct room dressing, per-map objectives, keepsakes, spawns and extraction.
- Dibs, Duos, Moving Day and Moving Out, plus tutorial, solo AI, round/results,
  retry/home, clear-out warnings and physical rescue/placement objectives.
- Imported supplied furniture, tiles and modular avatar; outfit colours and
  accessories; Classic/Candy/Arcade item finishes. Native animation needs the
  licensed editor's library refresh above.
- Native health/bag/gear/craft HUD, independent move/aim joysticks, illustrated
  actions, contextual availability, accessible typewriter selection and HOME.
  Single-local-player camera follows the action; couch players share the map.
- Standalone HTML/Three.js edition in `Web`, with real supplied GLBs, shared JSON,
  typed recipes, pointer/keyboard/touch controls, AI, wardrobe, minimap, pause,
  cues, objectives, results and replay. It is separate from a Unity Web build.
- Pinned toolchain helpers, repeatable source checks, Unity test/build entry
  points, asset audit/export scripts and portable workflow-harness pointer.

## Verification, 2026-10-01 UTC

| Check | Observed outcome |
| --- | --- |
| Engine-free C# rules | **168 passed, 0 failed** |
| Separate C# assemblies | **6 compiled, 0 errors**; 34 serialized/unused-field warnings in runtime source |
| Browser mechanics | **40 passed, 0 failed**, including conservation, effects, hazards, interruption and AI co-op completion on both maps |
| Static HTML delivery | Nested-path HTTP load, map/start controls, keyboard movement, pause, asset atlas and conservation passed; ZIP CRC checked |
| Real Chromium interactions | **14 scenarios passed, 0 page/resource errors**; keyboard/pointer/touch, co-op results/retry, portrait and full-bag landscape |
| Interchange asset verifier | **99 checked, 0 failed**, including mobile derivative, normals/weights, material factors, texture hashes and exact animation streams |
| Unity metadata | No missing asset `.meta` files or duplicate GUIDs; 41 UI textures have explicit transparent 2D importers |
| Independent review | Scoped findings corrected; separate Chromium pause and BALL/cover reproductions pass |
| Unity editor setup | **Blocked: exit 198**, no Personal license; no project import completed |
| Real Unity EditMode/PlayMode/captures/builds | **NOT RUN** |
| Android/iOS/controller hardware and device performance | **NOT RUN** |
| Workflow harness | **74 passed, 0 failed, 3 intentional opt-in skips**; these are workflow checks, not game checks |

The source check compiles actual assembly boundaries against installed Unity
engine and editor-template package DLLs. Those template DLL versions may differ
from the project's pinned packages. This is useful C# evidence, not a successful
Unity import or native execution.

The browser interaction fixtures position/provision bounded scenarios; actual
keyboard, pointer, joystick and action inputs exercise them. They are not an
exhaustive manual playthrough or physical phone certification.

## Art assessment and optimization

Read the [independent source review](../reviews/PRODUCTION_SOURCE_REVIEW.md),
[Chromium evidence](../reviews/evidence/browser-report.json),
and the [critical supplied-asset audit](../art/SUPPLIED_ASSET_AUDIT.md) and actual
[40-item contact sheet](../art/SUPPLIED_ITEMS_CONTACT_SHEET.png). Cohesion is **7/10**;
object recognition **8/10**; surface realism **5/10**. The meshes have recognizable
rounded shapes, while finish variation and complete native lighting still need work.

The accepted browser mobile avatar has **17,660 default-outfit triangles** versus
31,696 original (**44.3% fewer**), preserving the head, 22 bones and exact 17-clip
animation data. A matching four-avatar renderer comparison reports 287,722 triangles
versus 343,866 (**16.3% fewer**). Material-slot count is unchanged; these are geometry
measurements, not phone FPS. Original FBX/texture assets are preserved.

## Environment and continuation

.NET SDK 9.0.318 is installed rootlessly under `/workspace/.cloud-setup/dotnet-root`.
Unity and WebGL support are under `/workspace/.cloud-setup/Unity6000.6.3f1`. Archive
sizes/integrity and Microsoft package SHA256 checks passed. System Chromium and
locked browser dependencies are available. [Cloud setup](../../Tools/CloudSetup/README.md)
contains the reproducible checks.

Reusable `install_script` and `start_skill` have been saved in the environment
configuration draft. Unity login/package redirect destinations were added there;
a draft save does not activate a license, change current networking, publish a
snapshot or prove a fresh machine works.

The workflow harness stays outside the repository, with private state in its own
folder. The production run is paused at the native license/device gate after the
reviewed branch delivery. Do not copy private writer identities or task history
into the game. Resume from this portable record when working on another machine.

Keep Git author and committer **SethyPagna**, using the latest laptop commit's user
email. Add no coauthor trailers. Online matchmaking, a saved creative room editor,
additional recipe families and iOS delivery are follow-on features, not completed
capabilities of this branch.

The LFS upload endpoint rejected the cloud authentication, although the normal Git
push route passed its check. Reviewed new GLBs/UI images/review renders (each below
8 MB) are therefore committed as regular Git blobs. Their bytes/hashes are unchanged;
the original supplied asset pack retains its upstream LFS objects. The commit adds
no new LFS object requirement.
