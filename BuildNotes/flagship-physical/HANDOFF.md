# Physical course checkpoint

The public composition seam is:

```csharp
IWordboundPuzzleContent content = new WordboundAuthoredPuzzleContent(
    selectedMode, geometryCatalogue, exactResolvedCourse, itemJson, ruleBookJson);
```

It retains the supplied manifest and seed. `Plan`, `Mode`, `Geometry`, `Course`, defensive `Items`/`Rules`, `CatalogueHash`, `BindsRules` and `Validate(binding,actualRules)` are available. `WordboundPuzzleRuntime(controller, content)` retains the existing BeforeHostPhysics, ObserveHost, Evaluate and EvaluateMovementEnvironment hooks. No session, controller lifecycle or client hook changed.

Course schema1 with no physical profiles keeps its canonical serialization. Site-bearing documents export schema2; schema2 resolved manifests also use canonicalVersion2. `CourseDocument.PhysicalSites` contains `CoursePhysicalSite` and optional actual `CourseMovingFixture` definitions. Resolved profiles are immutable and each carries a canonical SHA256. Draft structural validation permits temporary missing graph links; playable resolution validates references, family-specific sensors, recovery sources and fixture bounds. The generic race plan requires exact selected puzzle coverage. Non-race manifests receive an empty active plan while their complete source remains hashed.

Match snapshot schema2/canonicalVersion2 now requires a `physical` object for each mechanism: `stage`, `actorId`, `auxiliaryBodyId`, decimal-string `stageTick`, `siteHash`, decimal-string `motionUnits`. This remains the pre-release wordbound-v2 protocol; old schema2 payloads missing the field are rejected. Refresh real C# cross-language fixtures after integration.

Actual sorting arms are canonical `Platform` bodies named `fixture:<fixtureId>:t<team>`, protected and owned by their team. Existing reconciliation spawns and registers them; GameplayPlan resolves their exact authored dimensions. Body pose must match persisted periodic motion phase. Each completed host tick moves the body and commits its phase atomically. Active catalogue CLOCK fields slow actual arm motion; solved arms stop at their last phase.

Smash requires the authored destroyed Props body plus the actual crafted recipe. It does not assert the source was first destroyed during the current lap: all future-lap geometry/props exist from match start. Protected renewable supplies and HAMMER recovery remain authored by the content lane. Wind requires a free authored or crafted replacement receiver and host-applied FAN force; balance checks actual furniture collider coverage of both floor pads; spring and keepsake crossings use host-observed physical arrival stages. Unsupported or incomplete profiles fail closed.

Native filters: `WordboundAuthoredPuzzleContentTests`, `WordboundPhysicalEvidenceTests`, and existing `WordboundMechanismTests` / `WordboundPuzzlePolicyTests` / snapshot / deployment regression suites. Source evidence is in progress.md. Native collision, presentation, six-family traversal and full flagship performance remain pending; no claim of a successful full-map playthrough is made.
