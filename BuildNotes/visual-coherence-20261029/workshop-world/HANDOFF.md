# Workshop viewport original artwork handoff

STATUS: FROZEN FOR ROOT INTEGRATION — candidate only; independent source review found no remaining blocker. No Unity/native execution or live source/asset writes by this lane.

## Bounded result

The authoring viewport previously used raw `Items/<id>` clones without fitting them to canonical placement dimensions or consulting the assigned course's visual profile. It also deliberately draws low floor slabs and edges, not the complete playable room. The white cluster in the opened baseline screenshot contains editing handles; it is not evidence that every white square is missing artwork.

This draft binds the launcher's existing original/profile-aware visual resolver without replacing an explicitly supplied factory. The viewport fits each owned visual clone to its canonical placement bounds and base, keeps preview geometry nonphysical, and preserves borrowed source models/materials. Null or degenerate artwork retains an explicit canonical placeholder. Foreign factory results are rejected without reparenting, disabling or destroying the borrowed object.

Selected/hovered room, zone and physical-site labels replace the persistent label clutter. Error zone outlines remain visible. Move/rotate and site handles are smaller, with short tooltips; their existing picking radii remain unchanged and are reserved against label overlap. Their cached styles reset with the UI style generation.

Resolved fixed placement IDs differ from source zone IDs. The draft registers a resolved fixed visual using its ZoneId so transient drag/cancel can continue after Preview, without changing the course or resolved hash.

Two white preview lights remain disabled between renders. URP's beginCameraRendering callback enables them only for the Workshop lens before culling and disables them before every other camera; endCameraRendering disables them after that lens. Disposal disables, unregisters and destroys only owned preview objects. Culling mask remains layer31, shadows off. No RenderSettings globals, unrelated lights, cameras or shared materials are modified. This mechanism needs native image verification; property assertions alone do not establish render quality.

## Exact source scope

The manifest contains nine files: WorkshopViewport, LauncherWorkshopSource, WorkshopInteraction, WorkshopOverlay, WorkshopSites; new Editor WorkshopViewportArtTests; plus Workshop.cs, WorkshopRecovery.cs and the existing Online.Client.Input WorkshopSourceBindingTests. Root approved the last three as a necessary source-binding repair after independent review.

The public Open signature remains unchanged. It delegates to private OpenSource with assigned=false, while OpenAuthored uses assigned=true before ResetView/Frame/Refresh. This prevents the initial assigned-course render from using global catalogue/profile data. Profile BaseArtwork changes also invalidate the cached resolver; profile overrides are honored normally.

## Evidence and root steps

- `source-check.json`: Roslyn C#9 semantic compile against current Unity response files. Five assemblies, zero errors; no Unity process was launched. Main/client substitutions are emitted to memory for dependent checks.
- `freeze-check.json`: every existing live source must match its baseline hash; the new fixture must be absent; each candidate must match its manifest hash. Integration should repeat these checks immediately before exact copy.
- Seven new Editor cases cover real original model bounds, fixed picking and deterministic preview hash before/after resolution; transient drag/cancel without history changes; foreign-object ownership; scoped light callbacks and disposal; explicit missing-art fallback.
- Two new source-binding cases cover visibly distinct assigned/default STOOL profile overrides from the very first refresh through source switches, retained history, and preservation of an explicit custom factory.
- Root should run those fixtures together with the existing Workshop source/gesture/overlay/input suites, then obtain fresh actual Windows Workshop images with original art and the new theme. Compare default overview, selected object, Preview, moved/cancelled fixed prop, source switch and return to lobby. Confirm no lobby lighting change and no labels obscuring handles at small viewport/large font.

## Limits retained

No full wall/ceiling/room architecture is added to the editor view: it would obscure the working floor from the authoring camera. Map architecture/ramp/door improvements remain owned by the map lane and appear in actual playtest. Random zones show their placement geometry until Preview resolves them; this draft does not invent unseeded objects. Items still use canonical geometry dimensions; detailed original textures and mesh shapes are retained within those dimensions.

This slice does not complete the Workshop inspector/layout simplification, custom gameplay rule authoring, larger detailed maps, public custom-map admission, or owner visual acceptance. Tests and source checks are technical evidence, not a claim that this design is accepted or production-ready.
