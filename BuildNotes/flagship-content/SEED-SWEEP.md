# Flagship seed sweep evidence

STATUS: PASS — 8,000 SEEDED GENERATION CASES

The frozen real NUnit run completed with exit 0 and eight parameterized cases passed, each looping over 1,000 seeds (`0000000000000000` through `00000000000003e7`). TRX start: `2026-10-21T00:16:20.9267668+08:00`; finish: `2026-10-21T01:16:07.8638610+08:00`. Console total: 59.7763 minutes. TRX counters: total/executed/passed 8; failed/error/timeout/aborted/notExecuted 0.

Command from this worktree, with output redirected to `seeds-schema2.log`:

```powershell
dotnet test BuildNotes/flagship-content/FlagshipTests.csproj --no-build --no-restore --nologo --verbosity minimal -p:RulesSourceRoot=../../../wordbound-puzzle-race -m:1 --filter 'TestCategory=FlagshipSeeds' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=seeds-schema2.trx' --results-directory BuildNotes/flagship-content/TestResults
```

| Configuration | Seeds | Result | Case duration |
| --- | ---: | --- | --- |
| TimeRun / Solo | 1,000 | PASS | 7m26.346s |
| TeamRace / Solo | 1,000 | PASS | 7m30.487s |
| TeamRace / Duo | 1,000 | PASS | 8m21.159s |
| TeamRace / Squad | 1,000 | PASS | 7m03.870s |
| BrawlRace / Solo | 1,000 | PASS | 8m16.632s |
| BrawlRace / Duo | 1,000 | PASS | 8m38.910s |
| BrawlRace / Squad | 1,000 | PASS | 8m18.349s |
| Freestyle | 1,000 | PASS | 4m07.563s |

Every race configuration measured 751.2666153055679–799.2666153055679 metres across the three required checkpoint routes. Both seeded lap-2 main/repair alternatives appeared in each configuration. The run repeats resolution at every 31st seed and the final seed and checks equal hashes; two-team isolated ownership also checks exact equivalent placement opportunities. Freestyle validates complete reachable world generation. The previously recorded focused 24/24 tests cover exact profile/source serialization and additional adversarial constraints; this sweep does not repeat every focused assertion for every seed.

The test assembly was built against physical Rules commit **17c30b4** and flagship factory/test commit **70723cd**. The three factory files and `WordboundFlagshipTests.cs` remain unchanged from that content commit, verified with Git after completion. No assembly rebuild occurred during the run. Later Stage1/Stage2 recovery, retirement and bounded body-budget policies are outside this frozen run; their owner is testing those separately against the same content factory.

| Local artifact | SHA-256 |
| --- | --- |
| `bin/Debug/net9.0/FlagshipTests.dll` | `598C12DEA6A8E6682ADDC074CB0CA67CFA4EDD6D86A99449CD7659CD026D76DF` |
| `bin/Debug/net9.0/FlagshipCatalogue.json` | `79363C8F1F14E7ACA553718B552468D34B3140FE4BA88688FD4DED8330CF2416` |
| `TestResults/seeds-schema2.trx` | `B04FAF2CBA6244A7CAE7B59BE48917E761C839F07304F17D163BBB57BDD32F5F` |
| `seeds-schema2.log` | `53BC132B80CD27F95522C5DEA500A6E2CD13E391C0190F84D423A595BEBA89AC` |
| `WordboundFlagshipContent.cs` | `C14DB0149677C8BB002C8EF35F610B9E41457BB7FEF30C215C07B9E3FED311EC` |
| `WordboundFlagshipLayout.cs` | `66C9185F70488CB21DFC1C38C22C8CD4AA33C2436AA93EB6AF24EDDCD99089CA` |
| `WordboundFlagshipRoutes.cs` | `CB066FDBE9A02BBED2E7043C4664825C69DE1E1C8F7B27C907F93282E254947A` |
| `WordboundFlagshipTests.cs` | `8278D82F753833FD1FC266A2972CFEBA6942A69A1E948F60653C799C283DFA2D` |

These are exact local-file byte hashes; source/log line endings may normalize in Git. The compiled assembly and TRX remain local ignored evidence; the complete console result is tracked alongside this report.

This evidence establishes deterministic validated generation and authored route distances. It does not establish a full native physical three-lap completion, player-camera readability, human puzzle difficulty or the target 18–25 minute completion time. No Unity, external service or paid generation was used by this run.
