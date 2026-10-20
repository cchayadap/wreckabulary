# Workshop lane

STATUS: IN PROGRESS

Owner: James / SethyPagna. Worktree: `wordbound-map-workshop`, branch `sethypagna/map-workshop`. Clean base: `12842c75608694f36819edd3bd4dc104ece7f79c`.

Scope: new Rules/Wordbound/Workshop*.cs, Scripts/Workshop/Wordbound* runtime files, matching tests and workshop documentation. Root owns scene/menu entry, shared hooks, metadata and native Unity gates.

Course contracts are supplied by the map-core lane. Draft transactions preserve structurally valid course data while allowing incomplete topology; full validation gates preview/playtest. Selection, groups, floor visibility/locking, grid and preview seed live in a separate `wreckabulary.workshop` recovery envelope. Shared JSON is the strict `wreckabulary.course` document, never HomeLayout.

Implemented immutable snapshots, bounded undo/redo, atomic command execution, module/group movement and rotation with attached point transforms, layer commands, duplicate subgraphs, delete, random zone and puzzle/gate link commands, and atomic autosave/share storage with a previous-save backup.

First rules increment passed 14/14 engine-free tests against map-core commit `2ce1906`: alias isolation, transaction rollback preserving redo, attached-point transforms, floor changes, lock/group behavior, duplicate subgraph references, bounded history, recovery/share separation, seed locking, backup recovery/path rejection, all three template resolution checks and fixed-placement transforms.

The initial template checks exposed an invalid free-text default seed; switched snapshots to the core's required sixteen lowercase hexadecimal digits and reran green. Fixed zones now transform their authored position/yaw with their owning module.

Next: build the additive workshop interaction surface and compile it against Unity reference assemblies. Native Unity NOT RUN.
