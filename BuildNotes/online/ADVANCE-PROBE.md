# Actual flagship Advance probe

STATUS: SOURCE READY; compile/run and comparative evidence NOT RUN. Root owns execution after its current native/Windows jobs. No production change is included. The default frozen-validation and existing `--capture` paths retain their workloads/output; `--advance` is explicit opt-in.

## Scope and source identity

Only `Tools/RulesHarness/ValidationProbe/Program.cs` changes. Baseline-only commit `79a11061` copies that one file from root and MUST NOT be integrated. Its SHA256 `E5385C8BE93089745FB7AD59AF20CCAB7BA30E92178216DC97B7D8BAA9D0E582` matches root's existing capture-capable probe. The Rules sources and csproj are untouched, including the lane's unrelated dirty ModeDefinition. Root must run its actual integrated source, not this older lane's Rules baseline.

The new mode reuses the actual item/rule files, flagship generator/geometry, seed0123456789abcdef, effective mode rules, two-seat TimeRun/TeamRace Solo bindings and existing input/hash output. There is no Unity/Photon/service work and no simplified course. It deliberately supplies no host poses, damage, progress/physical observations or commands: this measures idle canonical Advance on real content, not a native physical match, puzzle traversal or transport workload.

Each mode performs32 warmup advances followed by three sequential60-step batches,360 measured advances total. Ordinary exact `PublishSnapshot`/`Acknowledge` happens before warmup and each batch and after the last batch, outside measurements. The120-tick guard remains live; a paused/partial batch fails. Wall-clock input is deterministic `(current tick + 1) * 17` milliseconds. Measured tick ranges are32→92,92→152,152→212; this17ms input is a deterministic probe input, not a change to the60Hz game.

Measurement covers each complete existing `AuthoritativeMatch.Advance`, including rollback capture, synchronization and all existing internal checks, plus trivial result-count accumulation. Full snapshot validation, canonical capture/hash, publication/ACK and65536-byte allocation controls are outside each measured loop. Every batch reports raw current-thread allocated bytes and Stopwatch elapsed, then requires positive calibrated counters, exactly60 advanced ticks, zero command receipts, unchanged body/item/tile/resource/mechanism counts, full valid ending state, retained immutable original/start snapshots, unchanged epoch and no result. Start/end hashes, tick/wall clock and counts are printed for exact cross-run comparison. This is not isolated Synchronize CPU or Unity FPS.

## Root execution and acceptance

From the integration checkout, once the shared CPU slot is free:

```powershell
dotnet run --project Tools/RulesHarness/ValidationProbe/ValidationProbe.csproj -c Release -- --advance .
```

Save complete stdout and exit status to a new before artifact before any production optimization. Expected success is six `WORDBOUND_ADVANCE_BATCH` records and `WORDBOUND_ADVANCE_RESULT: modes=2; batches=6; measuredAdvances=360; warmupAdvances=64; failed=0`; no result exists yet. Save source revision and Program/Rules/content hashes alongside output. The existing default and capture command forms remain available for their own prior workloads.

After a separately reviewed synchronization change, run the identical command with identical source inputs/environment and compare every mode/batch start/end hash, tick, epoch, body/item/tile/resource/mechanism count and calibration. Compare all raw allocation and elapsed samples; do not silently discard slower batches or interpret different terminal hashes as comparable. The baseline and candidate retain the same probe source and warmup. This small deterministic probe does not establish statistical significance, physical-world cost, public movement correctness or sustained frame rate. Use the unchanged native public phase workload for those separate evidence boundaries.

No SDK, kit, configuration, authority, publication cadence, deadline or validation bypass is introduced. No CPU jobs were run in the online lane.

## Independent source review

Recover_input reviewed exact source commit `a709edf6`: bounded SOURCE PASS. The review checked the real root public Advance/Capture/PublishSnapshot/Acknowledge APIs and120-tick guard, monotonic17ms inputs from default0, unchanged default/capture workloads, real flagship setup, positive controls and raw evidence preceding strict assertions. No compile or execution was performed. Root has the source delta and the excluded baseline `79a11061`; measurement remains pending.
