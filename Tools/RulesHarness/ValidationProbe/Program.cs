using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.ValidationProbe
{
    static class Program
    {
        const string Seed = "0123456789abcdef";
        const int Iterations = 20, Batches = 3, ControlBytes = 65536;
        const int AdvanceWarmup = 32, AdvanceIterations = 60;
        sealed class Query : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => true;
            public bool CanDeploy(string actor, string recipe, WorldVector at) => true;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState item) => false;
        }

        public static int Main(string[] args)
        {
            try
            {
                bool capture = args.Length > 0 && args[0] == "--capture";
                bool advance = args.Length > 0 && args[0] == "--advance";
                int rootIndex = capture || advance ? 1 : 0;
                if (args.Length > rootIndex + 1) throw new ArgumentException("Use optional --capture or --advance followed by an optional checkout root path.");
                string root = Path.GetFullPath(args.Length == rootIndex ? Directory.GetCurrentDirectory() : args[rootIndex]);
                string itemSource = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/items.json"));
                string ruleSource = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/rules.json"));
                Console.WriteLine("WORDBOUND_VALIDATION_RUNTIME: framework=" + RuntimeInformation.FrameworkDescription +
                    "; architecture=" + RuntimeInformation.ProcessArchitecture + "; os=" + RuntimeInformation.OSDescription +
                    "; measurement=current-thread-managed-bytes; scope=" + (advance ? "idle-flagship-authoritative-advance" :
                        capture ? "unchanged-flagship-capture" : "frozen-flagship-validation") + "; unity-runtime=false");
                foreach (string modeId in new[] { "TimeRun", "TeamRace" }) Run(modeId, itemSource, ruleSource, capture, advance);
                Console.WriteLine(advance ? "WORDBOUND_ADVANCE_RESULT: modes=2; batches=6; measuredAdvances=360; warmupAdvances=64; failed=0" :
                    capture ? "WORDBOUND_CAPTURE_RESULT: modes=2; batches=6; captures=120; failed=0" :
                    "WORDBOUND_VALIDATION_RESULT: modes=2; batches=6; validations=120; failed=0");
                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("WORDBOUND_VALIDATION_FAILURE: error=" + exception.GetType().Name + "; message=" + exception.Message);
                return 1;
            }
        }

        static void Run(string modeId, string itemSource, string ruleSource, bool capture, bool advance)
        {
            var mode = ModeDefinition.For(modeId, RaceFormat.Solo);
            var items = ItemCatalogue.FromJson(itemSource);
            var book = RuleBook.FromJson(ruleSource);
            Require(items.Validate(64).Count == 0 && book.Validate().Count == 0, "Actual item/rule source is invalid.");
            var rules = mode.CreateGameplayRules(book.For("Dibs"));
            Require(rules.Validate().Count == 0, "Effective gameplay rules are invalid.");
            var flagship = WordboundFlagshipContent.Create(items);
            var resolved = flagship.Resolve(Seed, MatchStateValidation.Requirements(mode));
            Require(resolved.Success, string.Join("\n", resolved.Issues));
            var course = resolved.Manifest;
            var seats = Enumerable.Range(0, 2).Select(i => new WordboundSeat(((char)('a' + i)).ToString(),
                i % mode.TeamCount, i / mode.TeamCount)).ToArray();
            string roster = string.Join(",", seats.Select(s => s.PlayerId + ":" + s.Team + ":" + s.Slot));
            string itemHash = WordboundIdentity.Sha256(itemSource), ruleHash = WordboundIdentity.Sha256(ruleSource);
            string effectiveHash = WordboundRulesFingerprint.Compute(rules);
            string catalogueHash = WordboundIdentity.Sha256("validation-probe|" + flagship.Geometry.Hash + "|" + itemHash + "|" + ruleHash + "|" + effectiveHash);
            var binding = new MatchBinding("validation-probe", MatchSnapshotCodec.ProtocolVersion, mode.Hash,
                course.SourceHash, course.Hash, WordboundIdentity.Sha256(roster), catalogueHash, seats);
            var match = new AuthoritativeMatch(binding, mode, course, flagship.Geometry, items, rules, new Query());
            var frame = match.Capture(1);
            string snapshotHash = MatchSnapshotCodec.ComputeHash(frame);
            Console.WriteLine("WORDBOUND_VALIDATION_INPUT: mode=" + modeId + "; format=Solo; seed=" + Seed +
                "; roster=" + roster + "; tick=" + frame.Progress.Tick + "; epoch=" + frame.Epoch +
                "; bodies=" + frame.Bodies.Count + "; items=" + frame.Economy.Items.Count +
                "; tiles=" + frame.Economy.Tiles.Count + "; resources=" + frame.Resources.Count +
                "; mechanisms=" + frame.Mechanisms.Count + "; itemSourceHash=" + itemHash +
                "; ruleSourceHash=" + ruleHash + "; effectiveRulesHash=" + effectiveHash +
                "; geometryHash=" + flagship.Geometry.Hash + "; modeHash=" + mode.Hash +
                "; courseHash=" + course.SourceHash + "; manifestHash=" + course.Hash +
                "; bindingHash=" + binding.Hash + "; snapshotHash=" + snapshotHash +
                "; bindingScope=local-probe-not-public-admission");
            Require(MatchStateValidation.Validate(frame, binding, mode, course, items, rules) == "", "Warm validation failed.");
            if (advance)
            {
                Advance(modeId, match, frame, snapshotHash, course, items, rules);
                return;
            }
            if (capture)
            {
                Capture(modeId, match, frame, snapshotHash);
                return;
            }
            Control();
            for (int batch = 1; batch <= Batches; batch++)
            {
                long beforeControl = Control();
                int errors = 0;
                string firstError = "";
                long beforeBytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
                for (int i = 0; i < Iterations; i++)
                {
                    string error = MatchStateValidation.Validate(frame, binding, mode, course, items, rules);
                    if (error != "") { errors++; if (firstError == "") firstError = error; }
                }
                long elapsed = Stopwatch.GetTimestamp() - started;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
                long afterControl = Control();
                bool calibrated = beforeControl >= ControlBytes && afterControl >= ControlBytes;
                Console.WriteLine(FormattableString.Invariant(
                    $"WORDBOUND_VALIDATION_BATCH: mode={modeId}; batch={batch}; iterations={Iterations}; bytes={bytes}; milliseconds={elapsed * 1000d / Stopwatch.Frequency:F3}; errors={errors}; controlBeforeBytes={beforeControl}; controlAfterBytes={afterControl}; controlPayloadBytes={ControlBytes}; calibrated={calibrated}; firstError={firstError}"));
                Require(calibrated && bytes > 0, "Current-thread allocation counter is unavailable or an allocating workload was not observed.");
                Require(errors == 0, "Repeated validation failed: " + firstError);
                Require(snapshotHash == MatchSnapshotCodec.ComputeHash(frame), "Validation changed the frozen canonical snapshot.");
            }
        }

        static void Advance(string modeId, AuthoritativeMatch match, MatchSnapshot original, string originalHash,
            ResolvedCourse course, ItemCatalogue items, GameRules rules)
        {
            Acknowledge(match);
            for (int i = 0; i < AdvanceWarmup; i++)
                Require(match.Advance(checked((match.Tick + 1) * 17)).Advanced, "Warm Advance was unexpectedly paused.");
            Control();
            for (int batch = 1; batch <= Batches; batch++)
            {
                Acknowledge(match);
                var before = match.Capture(1);
                string beforeHash = MatchSnapshotCodec.ComputeHash(before);
                Require(MatchStateValidation.Validate(before, match.Binding, match.Mode, course, items, rules) == "",
                    "Before-batch Advance state is invalid.");
                long startTick = match.Tick;
                int advanced = 0, receipts = 0;
                long beforeControl = Control();
                long beforeBytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
                for (int i = 0; i < AdvanceIterations; i++)
                {
                    var step = match.Advance(checked((match.Tick + 1) * 17));
                    if (step.Advanced) advanced++;
                    receipts += step.Receipts.Count;
                }
                long elapsed = Stopwatch.GetTimestamp() - started;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
                long afterControl = Control();
                var after = match.Capture(1);
                string afterHash = MatchSnapshotCodec.ComputeHash(after);
                string error = MatchStateValidation.Validate(after, match.Binding, match.Mode, course, items, rules);
                bool calibrated = beforeControl >= ControlBytes && afterControl >= ControlBytes;
                bool retained = originalHash == MatchSnapshotCodec.ComputeHash(original) && beforeHash == MatchSnapshotCodec.ComputeHash(before);
                bool shape = after.Bodies.Count == original.Bodies.Count && after.Resources.Count == original.Resources.Count &&
                    after.Mechanisms.Count == original.Mechanisms.Count && after.Economy.Items.Count == original.Economy.Items.Count &&
                    after.Economy.Tiles.Count == original.Economy.Tiles.Count;
                Console.WriteLine(FormattableString.Invariant(
                    $"WORDBOUND_ADVANCE_BATCH: mode={modeId}; batch={batch}; iterations={AdvanceIterations}; advanced={advanced}; receipts={receipts}; startTick={startTick}; endTick={after.Progress.Tick}; startWallClockMs={before.Progress.WallClockMs}; endWallClockMs={after.Progress.WallClockMs}; epoch={after.Epoch}; bytes={bytes}; milliseconds={elapsed * 1000d / Stopwatch.Frequency:F3}; controlBeforeBytes={beforeControl}; controlAfterBytes={afterControl}; controlPayloadBytes={ControlBytes}; calibrated={calibrated}; bodies={after.Bodies.Count}; items={after.Economy.Items.Count}; tiles={after.Economy.Tiles.Count}; resources={after.Resources.Count}; mechanisms={after.Mechanisms.Count}; startHash={beforeHash}; endHash={afterHash}; retainedSnapshots={retained}; unchangedCounts={shape}; validationError={error}; measuredScope=whole-Advance-including-rollback-capture; publicationAndFullSnapshotValidation=outside-measurement; physicalSimulation=false"));
                Require(calibrated && bytes > 0, "Advance allocation counter is unavailable.");
                Require(advanced == AdvanceIterations && receipts == 0 && after.Progress.Tick == startTick + AdvanceIterations,
                    "Idle Advance did not complete the exact measured workload.");
                Require(error == "" && retained && shape, "Advance validation, retained snapshot or idle workload counts changed: " + error);
                Require(after.Progress.WallClockMs == after.Progress.Tick * 17 && after.Epoch == original.Epoch && after.Progress.Result == null,
                    "Idle Advance changed deterministic time, epoch or terminal state.");
            }
            Acknowledge(match);
        }

        static void Acknowledge(AuthoritativeMatch match)
        {
            var stamp = match.PublishSnapshot(out _);
            Require(match.Acknowledge(stamp), "Ordinary exact publication acknowledgement failed.");
        }

        static void Capture(string modeId, AuthoritativeMatch match, MatchSnapshot baseline, string hash)
        {
            for (int i = 0; i < 32; i++) GC.KeepAlive(match.Capture(1));
            Control();
            for (int batch = 1; batch <= Batches; batch++)
            {
                long beforeControl = Control();
                MatchSnapshot captured = null;
                long beforeBytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
                for (int i = 0; i < Iterations; i++)
                {
                    captured = match.Capture(1);
                    GC.KeepAlive(captured);
                }
                long elapsed = Stopwatch.GetTimestamp() - started;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
                long afterControl = Control();
                bool calibrated = beforeControl >= ControlBytes && afterControl >= ControlBytes;
                bool unchanged = MatchSnapshotCodec.ComputeHash(captured) == hash && MatchSnapshotCodec.ComputeHash(baseline) == hash;
                Console.WriteLine(FormattableString.Invariant(
                    $"WORDBOUND_CAPTURE_BATCH: mode={modeId}; batch={batch}; iterations={Iterations}; bytes={bytes}; milliseconds={elapsed * 1000d / Stopwatch.Frequency:F3}; controlBeforeBytes={beforeControl}; controlAfterBytes={afterControl}; controlPayloadBytes={ControlBytes}; calibrated={calibrated}; unchangedHash={unchanged}"));
                Require(calibrated && bytes > 0, "Capture allocation counter is unavailable.");
                Require(unchanged, "Capturing unchanged authority changed its canonical snapshot.");
            }
        }

        static long Control()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(new byte[ControlBytes]);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
