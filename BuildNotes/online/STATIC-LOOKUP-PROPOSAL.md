# Static validation lookup proposal

STATUS: DESIGN ONLY, pending completed native measurements and implementation assignment. No source edits or heavy jobs. Builds on the private weak course/mode cache already measured by the validation probe; does not propose the broader MatchProgress definition descriptor.

## Exact repeated work

- `MatchStateValidation.cs:356–379`: every platform searches all site fixtures, then searches sites again to recover the owner; connection platforms scan all connections. The final required-fixture pass repeats the flattened enumeration per team.
- `MatchStateValidation.cs:422–434`: rebuilds puzzle IDs each validation; practice lookup scans sites, then the physical validator repeats the same site lookup for every mechanism.
- `MatchStateValidation.cs:451–473` and `MatchPhysicalValidation.cs:29`: repeat puzzle and geometry searches for active mechanisms. `MatchPhysicalValidation.cs:11` scans sites even for waiting mechanisms. On the retained flagship workload this means 216 mechanisms repeatedly searching 72 sites per TimeRun validation; TeamRace Solo has 432 mechanisms. These are operation counts, not timing estimates.

## Minimal implementation boundary

Extend **private `StaticContent` only**, retaining its current weak exact-course/exact-mode lifetime and immutable public resolved-record inputs:

| Private index | Replacement |
| --- | --- |
| Site by value key `(lap, puzzleId)` | Practice and physical mechanism site lookup; no concatenated-string allocation per lookup |
| Fixture by ID, with its owning site | Both fixture search and owner-site search |
| Ordered fixture array | Required-fixture traversal; preserve existing site/fixture order |
| Puzzle by ID | Membership/count and existing puzzle lookups |
| Geometry by module ID | Existing module-bounds lookup |
| Connection platform-ID set | Existing `body.Id == "platform:" + connection.Id` membership |

Use ordinal string semantics and `Add`/`ToDictionary`; never silently overwrite or choose the first duplicate. Document validation already rejects duplicate lap/puzzle sites and globally duplicate fixture IDs (`CoursePhysicalSite.cs:90–108`, `:114–118`), and module/puzzle/connection IDs (`CourseValidation.cs:179`). Resolved wrappers and nested lists are immutable copies (`CourseManifest.cs:224–233`, `CoursePhysicalSite.cs:27–81`). Do not derive indexes from mutable `ResolvedCourse.Data`, an editable document, catalogue or rules.

Pass `StaticContent` to the existing mechanism validator and perform replacements at the same decision points. A missing fixture must still take the connection-platform branch; a missing site must still validate the unbound waiting-evidence rules; unknown puzzle/team/lap must still fail its existing check. Static eager duplicate failure is acceptable only for unsupported internally fabricated resolved courses; supported import/resolver duplicate rejection must remain unchanged.

Keep public `MatchStateValidationPhysical.Validate(snapshot, course, mechanism)` and its current lookup behavior. Factor its existing assertions into one shared implementation, with an **internal** overload receiving the exact cached site and puzzle. The public entry performs its current site search and retains its puzzle search at the existing point after early waiting/no-site returns. The indexed entry supplies the corresponding immutable puzzle; it must not expose a public trusted flag or skip evidence checks. Keep all body/item/seat, timing, motion, pose, recipe, progress, ownership and mutable rules/catalogue checks live. Do not cache snapshot validity, mechanisms, dynamic body/item indexes or permission results in this change.

## Focused verification

1. Retain all existing snapshot and physical-validation tests. Add real valid frozen/evolving race and Freestyle physical frames through the indexed full validator, with the public physical entry exercised against the same mechanism evidence.
2. After warming the cache, tamper fixture source/owner/pose, remove an authored fixture, and substitute a foreign connection platform ID. Each must retain rejection; restore the original valid frame afterward.
3. Cover waiting/no-site behavior and wrong site hash, physical phase, actor/team, lap and auxiliary source. Assert the same relevant error text/public exception before and after warm lookup use.
4. Resolve/import authored duplicate site, fixture, puzzle, module and connection IDs; rejection must remain upstream. Copy-document edits must not affect an already resolved course; separately resolved different courses must not share indexes. Keep the existing concurrent validation and mutable catalogue/rules regressions.
5. Root alone runs R0, the bounded affected core fixture(s), the unchanged actual-flagship ValidationProbe and the same three native profiler cases. Compare hashes, error outcomes, positive byte controls and actual timings. No changed lease/deadline/transport defaults; no Unity speed claim from .NET allocation alone.
