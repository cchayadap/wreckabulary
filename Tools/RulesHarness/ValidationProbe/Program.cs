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
                int rootIndex = capture ? 1 : 0;
                if (args.Length > rootIndex + 1) throw new ArgumentException("Use optional --capture followed by an optional checkout root path.");
                string root = Path.GetFullPath(args.Length == rootIndex ? Directory.GetCurrentDirectory() : args[rootIndex]);
                string itemSource = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/items.json"));
                string ruleSource = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/rules.json"));
                Console.WriteLine("WORDBOUND_VALIDATION_RUNTIME: framework=" + RuntimeInformation.FrameworkDescription +
                    "; architecture=" + RuntimeInformation.ProcessArchitecture + "; os=" + RuntimeInformation.OSDescription +
                    "; measurement=current-thread-managed-bytes; scope=" + (capture ? "unchanged-flagship-capture" : "frozen-flagship-validation") + "; unity-runtime=false");
                foreach (string modeId in new[] { "TimeRun", "TeamRace" }) Run(modeId, itemSource, ruleSource, capture);
                Console.WriteLine(capture ? "WORDBOUND_CAPTURE_RESULT: modes=2; batches=6; captures=120; failed=0" :
                    "WORDBOUND_VALIDATION_RESULT: modes=2; batches=6; validations=120; failed=0");
                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("WORDBOUND_VALIDATION_FAILURE: error=" + exception.GetType().Name + "; message=" + exception.Message);
                return 1;
            }
        }

        static void Run(string modeId, string itemSource, string ruleSource, bool capture)
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
