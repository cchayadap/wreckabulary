# Workshop starter independent source review

STATUS: COMPLETE — no blocking source finding for the bounded native test.

Reviewed revision 3, patch `647a3e95f252f454c1c099921561ef9b08f0da16f8bf6674bd0c8d4cbbbbead4`. This review made no live source changes and ran no Unity, gameplay or networking jobs.

## Verified independently

- All seven candidate hashes and six baseline hashes match the manifest. The original `CourseTemplates.cs` remains `f0e34865553ec7c3ca0a038843393b32e1c2cd9a98e1d4a1f0dc3c6df8cb8a09`.
- Plain `git apply --check --whitespace=error-all` passes against the current checkout. Roslyn C# 9 parsing of all seven candidates with Editor/test/QA symbols reports zero syntax errors. This is not semantic compilation.
- Only the Workshop Race Circuit preset gets the new three-lap STOOL course and exact TimeRun requirements. Core geometry proof, its catalogue, authored assigned courses and other presets are retained. Existing geometry-specific tests now request the unchanged core proof explicitly, preserving their prior assertions.
- `race-proof` deliberately remains the autosave lookup ID. Content hashes distinguish the new course. `OpenTemplate` still recovers the saved document before activating a fresh preset; the new recovery regression asserts exact prior recovery JSON and autosave bytes. Retained in-memory TeamRace/Duo requirements and Undo history survive template switching, and local competitive play remains refused. Arbitrary play selection is not serialized by WorkshopSnapshot; this does not claim such disk restoration.
- Each lap has its own physical binding to the required puzzle/checkpoint and renewable, protected STOOL supply. The eight spawns and socket lie inside the existing 20 m room. Entry and delivery recovery locations are distinct. The implementation does not relax `WorkshopPhysicalSites.PlaytestIssue`, `CanPlaytestLocally`, resolver validation or authoritative completion conditions.
- Meaningful added checks cover all seven offered mode/format combinations, exact draft dispatch, private bundle round-trip, edited-course Undo/Redo, and removal of one lap's socket blocking playtest. The actual-content match test installs a query that cannot satisfy a puzzle and verifies no initial completion/result; it does not fabricate physical success.

## Native acceptance still required

Compile the changed assemblies and run the focused Rules, Workshop Editor and online-client launch fixtures, then enter the fresh starter through the actual Workshop. Complete all three laps using movement, collect/craft/place and delivery, return to the exact editable draft, and verify old autosave recovery. Source coverage does not prove socket ergonomics, collision/placement, lap reset behaviour, controller usability, or a public multiplayer run. The one-room course is an immediately editable starter, not a finished larger map or narrative campaign.