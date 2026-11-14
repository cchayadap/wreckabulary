using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.PresentationProbe
{
    static class Program
    {
        const int Iterations = 4;
        const int ControlBytes = 65536;

        sealed class Query : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => true;
            public bool CanDeploy(string actor, string recipe, WorldVector at) => true;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState item) => false;
        }

        readonly struct Cost
        {
            public readonly long Bytes;
            public readonly double Milliseconds;
            public Cost(long bytes, double milliseconds) { Bytes = bytes; Milliseconds = milliseconds; }
        }

        public static int Main()
        {
            Console.WriteLine("WORDBOUND_PRESENTATION_RUNTIME: framework=" + RuntimeInformation.FrameworkDescription +
                "; architecture=" + RuntimeInformation.ProcessArchitecture + "; os=" + RuntimeInformation.OSDescription +
                "; measurement=current-thread-managed-bytes; scope=pure-presentation-only; unity-runtime=false");
            int passed = 0, failed = 0;
            foreach (int count in new[] { 32, 128, 256 })
            {
                try { Run(count); passed++; }
                catch (Exception exception)
                {
                    failed++;
                    Console.WriteLine("WORDBOUND_PRESENTATION_FAILURE: syntheticItemBodies=" + count +
                        "; error=" + exception.GetType().Name + "; message=" + exception.Message);
                }
            }
            Console.WriteLine("WORDBOUND_PRESENTATION_RESULT: passed=" + passed + "; failed=" + failed);
            return failed == 0 && passed == 3 ? 0 : 1;
        }

        static void Run(int count)
        {
            var mode = ModeDefinition.For("Freestyle", RaceFormat.Solo);
            var geometry = CourseTemplates.ProofCatalogue();
            var items = new ItemCatalogue();
            foreach (var asset in geometry.Assets.Values)
                items.Add(new ItemDefinition { Id = asset.Letters, Enabled = true, Durability = 100,
                    Size = new[] { 2f, 1f, 1f }, Deploy = new DeployStats { FootprintX = 2, FootprintZ = 1 } });
            var resolved = CourseResolver.Resolve(CourseTemplates.FreestyleWorld(), geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Require(resolved.Success, string.Join("\n", resolved.Issues));
            var course = resolved.Manifest;
            var seats = Enumerable.Range(0, 2).Select(i => new WordboundSeat(((char)('a' + i)).ToString(),
                i % mode.TeamCount, i / mode.TeamCount)).ToArray();
            var binding = new MatchBinding("match", "wordbound-v1", mode.Hash, course.SourceHash, course.Hash,
                new string('a', 64), new string('b', 64), seats);
            var match = new AuthoritativeMatch(binding, mode, course, geometry, items,
                mode.CreateGameplayRules(new GameRules()), new Query());
            var original = match.Capture(1);
            var economy = new Economy(items, new GameRules());
            var bodies = Enumerable.Range(0, count).Select(_ => {
                var item = economy.PlaceFurniture("STOOL");
                return new WorldBodyState("item:" + item.Id, WorldBodyKind.Item, default);
            }).ToArray();
            var frame = new MatchSnapshot(original.Binding, original.Epoch, original.Sequence, original.NextWorldEntityId,
                original.Progress, economy.Capture(), original.Commands, original.Actors, bodies,
                original.Resources, original.Mechanisms, original.RandomStreams, original.NarrativeFlags,
                original.ThrownFlights, original.Practice);
            var parity = new WordboundPresentationContext(frame, course, geometry, items);
            foreach (var body in bodies)
            {
                var oldValue = WordboundGameplayPlan.Presentation(body, frame, course, geometry, items);
                var newValue = parity.Presentation(body);
                Require(oldValue.VisualKey == newValue.VisualKey && oldValue.CarrierSeat == newValue.CarrierSeat &&
                    oldValue.Size.X == newValue.Size.X && oldValue.Size.Y == newValue.Size.Y && oldValue.Size.Z == newValue.Size.Z,
                    "The measured paths disagree about presentation.");
            }
            double legacySum = 0, indexedSum = 0;
            Action legacy = () => {
                foreach (var body in bodies)
                    legacySum += WordboundGameplayPlan.Presentation(body, frame, course, geometry, items).Size.X;
            };
            Action indexed = () => {
                var context = new WordboundPresentationContext(frame, course, geometry, items);
                foreach (var body in bodies) indexedSum += context.Presentation(body).Size.X;
            };
            Action control = () => GC.KeepAlive(new byte[ControlBytes]);
            control(); legacy(); indexed();
            legacySum = indexedSum = 0;
            var beforeControl = Measure(control, 1);
            var oldCost = Measure(legacy, Iterations);
            var newCost = Measure(indexed, Iterations);
            var afterControl = Measure(control, 1);
            bool calibrated = beforeControl.Bytes >= ControlBytes && afterControl.Bytes >= ControlBytes;
            Console.WriteLine(FormattableString.Invariant(
                $"WORDBOUND_PRESENTATION_PROBE: syntheticItemBodies={count}; iterations={Iterations}; legacyBytes={oldCost.Bytes}; indexedBytes={newCost.Bytes}; legacyMs={oldCost.Milliseconds:F3}; indexedMs={newCost.Milliseconds:F3}; controlBeforeBytes={beforeControl.Bytes}; controlAfterBytes={afterControl.Bytes}; controlPayloadBytes={ControlBytes}; calibrated={calibrated}; contextConstruction=included; unity-runtime=false"));
            Require(calibrated, "Current-thread byte measurement did not observe both positive control allocations.");
            Require(oldCost.Bytes > 0 && newCost.Bytes > 0, "A measured allocating path returned no allocated bytes.");
            Require(legacySum == indexedSum && legacySum == count * Iterations * 2d,
                "Measured loops must consume equal complete presentation output.");
            Require(newCost.Bytes < oldCost.Bytes / 2,
                "One frame context must use less than half the managed allocated bytes of the unchanged standalone path.");
        }

        static Cost Measure(Action action, int iterations)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), ticks = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterations; i++) action();
            long elapsed = Stopwatch.GetTimestamp() - ticks;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            return new Cost(allocated, elapsed * 1000d / Stopwatch.Frequency);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
