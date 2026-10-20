# Workshop modes and physical-site authoring

STATUS: SOURCE GREEN — NATIVE ACCEPTANCE PENDING

Source authoring now includes six-family physical control creation, explicit lap/checkpoint/recovery bindings, socket yaw/radius/hold, arrival/receiver/switch volumes, moving-arm endpoints/size/period/phase, and per-control viewport handles. A drag uses a captured immutable draft and commits one undo transaction; stale gestures reject. Receiver dragging moves both its sensor and authored fixed source. Module move/rotation/layer/duplication/deletion carries or remaps owned profiles and fixture IDs.

Public launcher API added: `ConfigureFlagship(content)` makes the explicit `Flagship draft` palette choice available; `OpenFlagship(content, folder=null)` opens that factory's exact schema-2 document and geometry with TimeRun selected. Ordinary geometry-only race templates remain editable, but playtest refuses missing or mismatched selected lap/puzzle/checkpoint profiles with actionable status. A successful resolver preview alone is not advertised as complete physical authoring.

New tests cover all six default authoring families, full codec references, receiver source/sensor drag, one-step undo, stale/locked edits, owned module transformations and duplication/deletion. The previous generic playtest-history test now uses the Freestyle template; the new local-mode fixture separately checks geometry-only race refusal and exact flagship TimeRun dispatch. These follow-up tests and installed-Unity reference compile are **NOT RUN** until the compiler window is available.

The source-only UI includes keyboard-focusable controls and controller actions for next physical puzzle, next control and return to module editing. With a site selected, the D-pad cycles its controls, the left stick moves the selected control as one transaction, and X rotates the socket. Native pointer picking, panel usability, controller physical-site navigation, actual player solutions, and visual acceptance remain separate gates. No Unity or metadata writes occurred.

Parent approved the additive API and owned paths: pure `Workshop*.cs` helpers; `Scripts/Workshop/Wordbound*` UI/viewport; matching tests; this evidence folder. Root owns the launcher, Unity/native execution and scene integration. Schema-2 DTO/codec belongs to the puzzle lane. The flagship handoff remains the first compiler priority.

Public API: `SelectMode(modeId, format)`, `ConfigureLocalPlaytest(bool)`, `CanPlaytestLocally`, explicit `EnableFreestyleSandbox()` history edit, and `ShowStatus(string)`. The existing `PlaytestRequested` event, `LastPlaytestDraft`, `PlayRequirements` and `ResumeEditing` remain the launch/return contract. Root reads the immutable draft and requirements synchronously when the event supplies the exact manifest.

Mode choice precedes Preview/Playtest. One-human local sessions allow TimeRun/Freestyle only. Other modes may be previewed, with an explanation before local launch. A Combat Arena may explicitly enable Freestyle as an undoable `SupportedModes` edit before resolving; no competitive manifest is relabelled and no roster seats are invented.

Physical-site authoring uses the canonical course DTO: explicit selection and controls for socket, arrival, pads, receiver and fixture paths; concise family-specific fields. Module move/rotate/floor changes must transform profiles and fixture paths. Duplicate/delete must remap or remove owned profiles consistently. The codec accepts incomplete draft topology while full validation/playtest remains strict.

Source authoring only while root owns the compiler/native slot. Tests and native usability acceptance have not run for this follow-up.

The authorized focused gate is now running against the puzzle owner's frozen **929240a0ccf459d246609fb7d1e93495e4d82f72** Rules, on top of physical-site **17c30b4**. The long flagship sweep remains a separate already-built assembly pinned to 17c30b4; its results must not be presented as evidence for the later resource-lifecycle changes. Installed-Unity reference compilation will follow the focused tests serially.

Gate result: **58/58 PASS**, real NUnit, one second test execution, via `dotnet test BuildNotes/workshop-mode-sites/Tests.csproj --nologo --verbosity minimal -p:UseSharedCompilation=false -m:1 --logger trx;LogFileName=workshop-mode-sites.trx --results-directory BuildNotes/workshop-mode-sites/TestResults`. Includes all existing pure workshop/history/placement cases and the new mode and six-family physical-site cases. Evidence: `tests.log`, local ignored TRX.

Installed Unity 6000.6.3f1 reference compilation: **PASS, zero errors**, 9.87 seconds, via `dotnet build BuildNotes/workshop-mode-sites/ReferenceCompile.csproj --nologo --verbosity minimal -p:UseSharedCompilation=false -m:1`. The 20 CS0436 warnings identify new Workshop source classes also present in the previously compiled main `Wreckabulary.dll`, which is referenced for the existing real art API. Each warning explicitly selects the new source type. No warnings were suppressed. This reference gate compiles the actual UI and editor tests but does not run engine code. Root's integrated R0/native gates must establish the actual Unity assembly result.

Compiler ownership returned to root after the two serial gates. Next recoverable action: integrate this coherent source checkpoint after flagship/schema-2 Rules, run focused native workshop mode/site tests and visual/pointer/controller checks, then connect the root launcher through the exact APIs in `API.md`. The known long-session canonical-body budget is separately owned by the gameplay lifecycle lane.
