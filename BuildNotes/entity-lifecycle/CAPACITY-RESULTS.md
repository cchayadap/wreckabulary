# Stage 2 flagship capacity acceptance

STATUS: SOURCE ACCEPTANCE PASS; NATIVE PERFORMANCE AND COMPLETE PLAYTHROUGH NOT RUN

The fixture uses current Stage 2 Rules, the unchanged flagship factory from `70723cd`, the actual integration `items.json`, Dibs-derived rules through `ModeDefinition.CreateGameplayRules`, and full-capacity rosters. The host world-query fixture refuses interactions. Each case resolves the supplied seed and, for races, constrains each section to its first then last authored alternative. Across those runs every one of the 22 authored alternatives is covered. Geometry, authored site persistence and body generation still use the real resolver.

All eight configurations passed construction, 512-body preflight, retained-state bounds, canonical publication and strict import. The two constrained choices have the same budget values as the seeded selection because their required physical puzzle set is unchanged. Freestyle has no active race route or physical mechanisms.

| Mode / format | Fixed K | Pools × body share | Recovery C | Receiver H | Actor/burst B | Optional O per pool | Startup bodies | Canonical UTF-8 bytes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| TimeRun / 4 humans | 74 | 1 × 438 | 11 | 12 | 149 | 266 | 96 | 150,294 |
| TeamRace / Solo | 142 | 2 × 185 | 11 | 12 | 41 | 121 | 186 | 293,587 |
| TeamRace / Duo | 144 | 2 × 184 | 11 | 12 | 77 | 84 | 188 | 295,531 |
| TeamRace / Squad | 148 | 2 × 182 | 11 | 12 | 149 | 10 | 180 | 294,427 |
| BrawlRace / Solo | 85 | 2 × 213 | 11 | 12 | 41 | 149 | 129 | 257,125 |
| BrawlRace / Duo | 87 | 2 × 212 | 11 | 12 | 77 | 112 | 131 | 259,069 |
| BrawlRace / Squad | 91 | 2 × 210 | 11 | 12 | 149 | 38 | 135 | 262,957 |
| Freestyle / 8 humans | 65 | 1 × 447 | 0 | 0 | 288 | 159 | 102 | 73,120 |

The conservative retained-item allowance is 641 for TimeRun, 770 for TeamRace, 713 for BrawlRace and 569 for Freestyle, all below 8,192. These are admission bounds, not observed maximum item counts.

TeamRace Squad starts with fewer loose bodies than Duo because its smaller optional allowance defers extra general resource batches. Critical puzzle recovery retains its own reservation. Fixed roots and player allowance use the selected mode's full capacity, so an undersized party does not gain extra capacity.

The 21 race resolves plus one Freestyle resolve ran in eight NUnit cases: 8/8 PASS. Evidence is `TestResults/entity-lifecycle/flagship-stage2-capacity.trx`; the reproducible fixture source is `BuildNotes/entity-lifecycle/FlagshipCapacityTests.cs`. The isolated local project links the full Rules and GSK Determinism sources, `WordboundFlagship*.cs`, `TestData.cs`, this fixture and the actual item JSON as `FlagshipCatalogue.json`, using NUnit 3.14 / NUnit3TestAdapter 5.0 / Microsoft.NET.Test.Sdk 17.14.1 on .NET 9. Its output is isolated under `Logs/entity-lifecycle/flagship-*`.

The historical 8,000-seed sweep predates Stage 2 and remains separate generator evidence. This bounded acceptance establishes that the new mandatory reservations fit every supplied flagship mode and authored route choice. It does not claim a complete three-lap physical playthrough, eight-peer performance, or that a larger arbitrary workshop map fits; such content must pass the same exact preflight.
