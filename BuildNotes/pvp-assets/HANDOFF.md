# PvP asset lane

STATUS: READY FOR ROOT UNITY IMPORT — NATIVE UNITY NOT RUN

Owner: James / SethyPagna. Branch: `sethypagna/pvp-assets`. Clean starting checkpoint: `12842c75608694f36819edd3bd4dc104ece7f79c`.

Scope: additive WordboundPvP source collection, four production tools, dedicated runtime collection data, editor importer, editor tests and documentation. Root owns runtime opt-in and icon routing. Original checkout and Lunar work are outside this lane.

The complete verified staging collection was copied into this worktree and the delivery validator passed again: twelve concepts/models, twenty-four exports, thirty-six views, twelve transparent icons. Original staging and generated images remain intact.

Importer entry point: `Wreckabulary.EditorTools.WordboundEquipmentImporter.Build`.

Frozen API: `WordboundEquipmentCollection.Load()`, `Entries`, `Find(itemId)`, `FindByModelKey(modelKey)`; each entry supplies `itemId`, `modelKey`, `model`, `icon`, `size`, `grip`, `heldScale`. Resource path: `Collections/WordboundPvP`.

No Unity launch or hand-authored metadata/YAML in this lane. Root will generate metadata, materials and the collection asset using AssetDatabase in the integration worktree, then run native import/editor checks.

Implemented the independent collection, validated importer and five native editor tests. Tests cover enabled catalogue mapping, original dimensions/grip/held scale, actual mesh bounds and forward marker, geometry budget, UVs, two material remaps, sprite/import settings, unchanged legacy skin calls, and the unchanged ninety-eight-model library.

Twenty-five Unity input files (twelve FBXs, twelve icons and one palette) were copied into the dedicated Assets path and independently hash checked. All twelve manifest measurements match the existing catalogue exactly. See `staged-unity-verification.json`.

The standard `game_gate check all --json` returned INFRA (`no_library`) in this fresh worktree because Unity has not imported it. Its diagnostic log is `Logs/gate_check_all_20261020T102622Z.log`. No Unity process was launched.

A focused .NET compile of the three new C# files against the root integration baseline's Unity 6000.6.3f1 assemblies and NUnit package passed with zero errors. Its local project is ignored; the command was `dotnet build BuildNotes/pvp-assets/WordboundTypecheck.csproj -nologo -v minimal` with intermediate output redirected to the ignored root `obj/wordbound-typecheck/`. `focused-typecheck.log` records the result. JSON-populated fields produce the usual CS0649 warnings. This verifies API/type compatibility, not native Unity serialization/import behavior. The installed NUnit package uses `net472/unity-custom/nunit.framework.dll`; the older gate template expects `net40`.

JavaScript syntax checks, the complete delivery validator and `git diff --check` passed. Root agent independently reviewed all twelve model previews. Original shared source files remain unchanged.

Next recoverable action: integrate this commit, run `WordboundEquipmentImporter.Build` through the gate in the root integration worktree, then run `WordboundEquipmentTests`. Commit Unity-generated metadata/materials/collection asset there. Root owns runtime opt-in and icon routing and should preserve normal skin selection for requests outside this collection.
