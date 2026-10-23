# Presentation allocation probe

Run from the checkout root:

```powershell
dotnet run --project Tools/RulesHarness/PresentationProbe/PresentationProbe.csproj --configuration Release
```

This .NET 9 console probe compiles the checkout's actual `WordboundGameplayPlan.cs` and references the existing Rules project. It measures 32, 128 and 256 synthetic item bodies, four iterations each, against the unchanged standalone presentation API. The indexed measurement includes construction of the complete frame context. Each case verifies presentation parity, equal consumed output, positive allocation calibration before and after, positive measured byte counts, and less than half the old allocated bytes. Failure returns nonzero; timing has no pass threshold.

Unity Mono returned unavailable zero values for the thread-byte counter in the original native probes. `GC.Alloc` event counts are not bytes, so they are not substituted. These separate .NET numbers describe this bounded presentation workload only; Unity's 25 behavioral cases and actual native CPU/network/player tests remain separate acceptance gates. No Unity stub or copied presentation algorithm is used. Generated bin/obj files are covered by the existing RulesHarness ignores.
