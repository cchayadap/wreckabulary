# Workshop selection treatment review

STATUS: COMPLETE — no blocking source defect found. Actual post-change appearance remains pending.

Scope: root's bounded follow-up replacing the selected room's full-floor tint with a thin unlit mint edge, while preserving the authored Lit floor. No production/native writes by this reviewer.

Before image directly opened, unchanged at original size: `Docs/qa/runs/20261028T214134Z-wordbound_parcel_workshop/parcel_workshop_sorting_close.png`, from render-candidate build `20261028T213901Z`. The selected Sorting room is covered by a very bright mint floor while its neighboring rooms retain brown wood. The cue is unmistakable but overwhelms the scene and masks the room's intended floor color. Furnishings and handles remain visible; the image does not establish any selection/picking failure. This candidate is retained evidence, not the final five-scenario audit or owner-approved result.

Reviewed the exact two-file live diff against root's stated checkpoint `29e5da9a`; both file hashes match root's freeze:

| File | SHA256 |
|---|---|
| `Assets/_Project/Scripts/Workshop/WordboundWorkshopViewport.cs` | `0c661f865ab721c0ca614550649deb7bd53daa0266c5be183e1e482402f022db` |
| `Assets/_Project/Scripts/Online/Presentation/Tests/WordboundCourseMaterialTests.cs` | `380b90cbe0499b8c747c126f7acc9e8f2c614067c82f204160b3dc9e71229833` |

## Source findings

- **Floor appearance is retained:** the module loop always selects `FloorFor(module.LayerId, style)` for an active room. Selection changes only `edgeMaterial`; the floor no longer receives the mint material. Explicit borrowed FloorMaterial and derived Lit palette variants retain their previous ownership and source-switch cleanup.
- **Cue material is owned:** construction calls `WordboundRenderMaterials.Create(false, "Workshop selection")`, which clones its resource template. Only the clone is tinted and it is added to `ownedMaterials`, so disposal releases it without changing the shared template. The installed `Surface.mat` uses GUID `650dd9526735d5b46b79224bc6e94025`, resolving to the package's `Shaders/Unlit.shader`; its empty base texture, opaque surface and normal depth writing suit a solid border cue. The prior common teardown remains unchanged.
- **No additional geometry or picking side effect:** the four existing north/south/east/west edge boxes retain their exact positions, dimensions, transforms and layer. No collider, enlarged selection bounds or extra mesh is introduced. The floor, canonical module geometry, fixed-object mapping and drag/picking paths are untouched.
- **Adjacent ghosts remain unchanged:** adjacent floors still receive `ghost`, and `edgeMaterial` can become mint only for a selected non-adjacent room. Camera/light callbacks, layer isolation, translucent blend settings and source/profile data remain untouched.
- **Regression coverage meaningfully targets the failure:** the existing material lifetime test now explicitly selects an actual active room. It checks that selected room keeps the exact authored FloorMaterial, then checks its derived Lit palette clone and the existing shader/texture/keyword/source-lifetime assertions. This prevents reintroducing a full-floor selection replacement. It does not claim to measure border visibility or subjective brightness; those require the new screenshot.

## Remaining verification

Root reported R0 `214521` PASS and targeted native `214636` running at closure; I did not invoke native processes. Open the next unchanged Sorting close-up to verify the mint border is easy to see without dominating the room, wood remains visible across the selected floor, props and handles remain readable, and neighboring room/ghost treatment still works. This review does not approve the retained 213901 render candidate, full Workshop usability or the final five-scenario audit. Only this review file was written.

## Bounded precision follow-up

Native `TestResults/editmode_20261028T214636Z.xml` is retained unchanged: 12/13 passed, with the material test stopping at its new exact Color assertion (line 153). Both formatted values are `RGBA(0.640, 0.430, 0.280, 1.000)`. The preceding selected-room borrowed-material identity assertion passed; the later assertions were not reached in that failed case.

Reviewed root's one-line follow-up: `Assert.That(Vector4.Distance(derived.color, style.Palettes[0].Floor), Is.LessThan(.00001f));`. This allows only a tiny combined RGBA difference from float property round-tripping, still rejecting a substantive tint change. It keeps the explicit selected-room identity, exact shader/texture references, texture scale/offset, source JSON and clone-lifetime checks intact. No production or picking code is altered by this follow-up. No source blocker found; a new native run is still required to execute the previously unreached assertions.

Updated test-file SHA256: `7120e9e5a6235290b235b79f0f8fcd560cba0a60e5d09241a9082e9f00a67eca`. The earlier `380b90...` hash above remains the exact first-reviewed version that produced the preserved failure receipt. Root's R0 `214814` and native rerun were pending at this follow-up's closure.
