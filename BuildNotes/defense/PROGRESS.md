# Held defense verification

- [x] Practice `27e0c53` imported as the exact Rules baseline; production delta `1cea56e` + `1e48142` preserves schema/canonical/protocol 3.
- [x] Focused Rules gate: 57/57 real NUnit tests passed, 0 failed/skipped, 27 seconds. Includes 13 defense policy, 25 authority and 19 FFA acquisition cases. `results/defense-first.trx`.
- [x] Installed Fusion SDK policy gate: 34/34 passed, 0 failed/skipped, 376 ms. Includes 12 new facing cases and 22 existing physics policy cases. `results/facing.trx`.
- [x] Full changed physics and rules-fingerprint sources compiled against Unity 6000.6.3f1 and installed Fusion assemblies: 0 warnings, 0 errors, 12.52 seconds. `reference.log`.
- [x] Compiler slot released directly to Online; no Unity process launched by this lane.
- [~] Independent source review by James found no confirmed blocker in the inspected defense paths. He requested a separate rollback-after-shield-break regression; it is not included in the 57-test result above.
- [ ] Combined final mode/public-v3 golden exporter and wire/service parity. Online owns the later PublicQueue change; this DLL predates that mode change.
- [ ] Root HUD/PlayerView input binding, native facing/block/break/recovery and actual player acceptance.
- [ ] Queued 1000-seed arena-only sweep remains separate.

Executed sequentially on 2026-10-21 (Hong Kong):

```text
dotnet test BuildNotes/defense/DefenseTests.csproj -p:UseSharedCompilation=false --logger "trx;LogFileName=defense-first.trx" --results-directory BuildNotes/defense/results
dotnet test BuildNotes/defense/FacingTests.csproj -p:UseSharedCompilation=false --logger "trx;LogFileName=facing.trx" --results-directory BuildNotes/defense/results
dotnet build BuildNotes/defense/ReferenceCheck.csproj -p:UseSharedCompilation=false -v:minimal
```

| Artifact | SHA-256 |
| --- | --- |
| `bin/Rules/netstandard2.1/Wreckabulary.Rules.dll` | `A4AD108A82A44F79C23A527AC39C7D8416C72CA856F985369FBF236C1ACA9716` |
| `results/defense-first.trx` | `E37A75360AF1A756BCE572FF87FC2F1199C1296DF586F9357F5C2D65E296E87A` |
| `results/facing.trx` | `E32B5A5296927628A42D420B55408E6F9EBC05FD1E01C7A21F06C6E2DF1A40E6` |
| `bin/Reference/netstandard2.1/ReferenceCheck.dll` | `6FF3962B8A96B46BA3D7AAC4B643F7C09DFB1B9235F038321DA5CF4F598EE690` |
| Installed `Fusion.Runtime.dll` | `D213B95E4694C167F5F6FAFFCBD75CA1E47F19A9249C91A4964F8342FAC9B560` |

These are source/runtime-policy checks, not native simulation, rendered appearance or production delivery evidence. No equipment stats were changed.
