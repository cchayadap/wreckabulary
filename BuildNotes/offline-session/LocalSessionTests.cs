using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OfflineSchedulingTests
{
    public sealed class LocalSessionTests
    {
        const string Actor = "local-workshop-player";
        sealed class World : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => false;
            public bool CanDeploy(string actor, string recipe, WorldVector at) => false;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState item) => false;
        }
        sealed class Fixture
        {
            public ModeDefinition Mode;
            public ResolvedCourse Course;
            public CourseCatalogue Geometry;
            public ItemCatalogue Items;
            public GameRules Rules;
            public WordboundFusionAuthority Authority;
            public NetworkRunner Runner;
            public WordboundLocalSession Session;
        }
        static string Config(string file)
        {
            string root = Environment.GetEnvironmentVariable("WORDBOUND_PROJECT_ROOT") ?? Environment.CurrentDirectory;
            return File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config", file));
        }
        static Fixture Create(string id = "Freestyle")
        {
            var f = new Fixture { Mode = ModeDefinition.For(id), Geometry = CourseTemplates.ProofCatalogue() };
            f.Items = ItemCatalogue.FromJson(Config("items.json"), "local-test-items");
            f.Rules = f.Mode.CreateGameplayRules(RuleBook.FromJson(Config("rules.json"), "local-test-rules").For("Dibs"));
            var resolved = CourseResolver.Resolve(id == "Freestyle" ? CourseTemplates.FreestyleWorld() : CourseTemplates.RaceCircuit(),
                f.Geometry, new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(f.Mode)));
            Assert.IsTrue(resolved.Success);
            f.Course = resolved.Manifest;
            var binding = WordboundLocalSession.CreateBinding(f.Mode, f.Course, new string('a', 64), Actor);
            f.Authority = WordboundFusionAuthority.CreateLocal(new AuthoritativeMatch(binding, f.Mode, f.Course, f.Geometry,
                f.Items, f.Rules, new World()), f.Course, f.Items, f.Rules, Actor);
            f.Runner = new NetworkRunner { GameMode = GameMode.Single };
            f.Runner.ActivePlayers.RemoveAt(1);
            f.Session = new WordboundLocalSession(() => f.Runner,
                new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple }, f.Authority, Actor);
            f.Session.ConfigureScene(SceneRef.FromIndex(0));
            return f;
        }
        static Dictionary<string, object> Command(long sequence = 1) => new Dictionary<string, object> {
            ["sequence"] = sequence.ToString(), ["epoch"] = "0", ["action"] = "CancelCraft",
            ["target"] = "", ["revision"] = 0, ["slot"] = -1, ["value"] = ""
        };

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public async Task LocalModeUsesExactCourseAndStrictInitialCheckpoint(string mode)
        {
            var f = Create(mode);
            Assert.AreEqual(WordboundLocalSession.ProtocolVersion, f.Authority.Match.Binding.ProtocolVersion);
            Assert.AreEqual(f.Course.Hash, f.Authority.Match.Binding.ManifestHash);
            await f.Session.StartLocal();
            Assert.IsFalse(f.Session.GameplayReady);
            Assert.IsNull(f.Session.Committed);
            f.Session.MarkLocalReady();
            Assert.IsTrue(f.Session.GameplayReady);
            Assert.IsTrue(f.Session.TryGetAdmittedActor(f.Runner.LocalPlayer, out var actor));
            Assert.AreEqual(Actor, actor);
            Assert.IsFalse(f.Session.TryGetAdmittedActor(new PlayerRef(2), out _));
            var imported = MatchSnapshotCodec.Import(f.Session.CommittedJson, f.Authority.Match.Binding,
                f.Mode, f.Course, f.Items, f.Rules);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.AreEqual(imported.Hash, f.Session.Committed.StateHash);
            Assert.AreEqual(1, imported.Snapshot.Binding.Seats.Count);
            await f.Session.Stop();
        }

        [TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll")]
        public void CompetitiveModesDoNotInventHumansOrRewriteCourse(string mode)
        {
            var f = Create();
            string hash = f.Course.Hash;
            Assert.Throws<ArgumentException>(() => WordboundLocalSession.CreateBinding(ModeDefinition.For(mode),
                f.Course, new string('a', 64), Actor));
            Assert.AreEqual(hash, f.Course.Hash);
        }

        [Test]
        public void LocalFactoryRejectsAnOnlineIdentityAndDifferentActor()
        {
            var f = Create();
            var local = f.Authority.Match.Binding;
            var online = new MatchBinding(local.MatchId, MatchSnapshotCodec.ProtocolVersion, local.ModeHash,
                local.CourseHash, local.ManifestHash, local.RosterHash, local.CatalogueHash, local.Seats);
            var match = new AuthoritativeMatch(online, f.Mode, f.Course, f.Geometry, f.Items, f.Rules, new World());
            Assert.Throws<ArgumentException>(() => WordboundFusionAuthority.CreateLocal(match, f.Course, f.Items, f.Rules, Actor));
            Assert.Throws<ArgumentException>(() => new WordboundLocalSession(() => f.Runner,
                new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple }, f.Authority, "other"));
        }

        [Test]
        public void BindingWithAChangedManifestFailsStrictInitialImport()
        {
            var f = Create();
            var local = f.Authority.Match.Binding;
            var mismatch = new MatchBinding(local.MatchId, local.ProtocolVersion, local.ModeHash,
                local.CourseHash, new string('b', 64), local.RosterHash, local.CatalogueHash, local.Seats);
            Assert.Throws<ArgumentException>(() => {
                var match = new AuthoritativeMatch(mismatch, f.Mode, f.Course, f.Geometry, f.Items, f.Rules, new World());
                WordboundFusionAuthority.CreateLocal(match, f.Course, f.Items, f.Rules, Actor);
            });
        }

        [Test]
        public async Task CompletedStartIsRequiredEvenWhenSdkAlreadyReportsRunning()
        {
            var f = Create();
            var completion = new TaskCompletionSource<StartGameResult>();
            StartGameArgs seen = null;
            f.Runner.Startup = args => { seen = args; return completion.Task; };
            var start = f.Session.StartLocal();
            Assert.IsTrue(f.Runner.IsRunning);
            Assert.IsFalse(f.Session.RunnerStarted);
            Assert.Throws<InvalidOperationException>(() => f.Session.MarkLocalReady());
            Assert.AreEqual(GameMode.Single, seen.GameMode);
            Assert.AreEqual(1, seen.PlayerCount);
            Assert.IsTrue(seen.Scene.HasValue);
            Assert.IsNull(seen.AuthValues);
            Assert.IsNull(seen.ConnectionToken);
            Assert.IsNull(seen.CustomPhotonAppSettings);
            Assert.IsNull(seen.SessionName);
            completion.SetResult(new StartGameResult());
            await start;
            Assert.IsTrue(f.Session.RunnerStarted);
            await f.Session.Stop();
        }

        [Test]
        public async Task StopFencesLateStartupAndIsIdempotent()
        {
            var f = Create();
            int shutdowns = 0;
            f.Runner.ShutdownCompletion = () => { shutdowns++; return Task.CompletedTask; };
            var completion = new TaskCompletionSource<StartGameResult>();
            f.Runner.Startup = _ => completion.Task;
            var start = f.Session.StartLocal();
            var stop = f.Session.Stop();
            Assert.AreSame(stop, f.Session.Stop());
            completion.SetResult(new StartGameResult());
            Assert.ThrowsAsync<OperationCanceledException>(async () => await start);
            await stop;
            Assert.AreEqual(1, shutdowns);
            Assert.IsFalse(f.Session.RunnerStarted);
            Assert.IsFalse(f.Session.GameplayReady);
            Assert.IsNull(f.Session.Committed);
            Assert.AreEqual("", f.Session.Failure);
        }

        [Test]
        public async Task ExactLocalActorUsesRealCommandJournalAndCheckpointWindow()
        {
            var f = Create();
            await f.Session.StartLocal();
            f.Session.MarkLocalReady();
            f.Session.SubmitCommand(Command());
            var receipt = f.Authority.Advance(17).Receipts.Single();
            Assert.AreEqual(Actor, receipt.ActorId);
            Assert.AreEqual(1, receipt.Sequence);
            for (int tick = 2; tick <= 120; tick++) Assert.IsTrue(f.Authority.Advance(tick * 17).Advanced);
            Assert.IsFalse(f.Authority.Advance(121 * 17).Advanced);
            Assert.IsTrue(await f.Session.PublishCheckpoint());
            Assert.AreEqual(120, f.Session.Committed.Tick);
            Assert.IsTrue(f.Authority.Advance(122 * 17).Advanced);
            await f.Session.Stop();
        }

        [Test]
        public async Task UnexpectedPeerOrShutdownFreezesInput()
        {
            var f = Create();
            await f.Session.StartLocal();
            f.Session.MarkLocalReady();
            f.Runner.ActivePlayers.Add(new PlayerRef(2));
            f.Session.Poll();
            Assert.AreEqual("InvalidOperationException", f.Session.Failure);
            Assert.IsFalse(f.Session.GameplayReady);
            Assert.IsFalse(f.Session.TryGetAdmittedActor(f.Runner.LocalPlayer, out _));
            Assert.Throws<InvalidOperationException>(() => f.Session.SubmitCommand(Command()));
            Assert.Throws<InvalidOperationException>(() => f.Authority.Advance(17));
            Assert.IsFalse(await f.Session.PublishCheckpoint());
            await f.Session.Stop();
        }

        [Test]
        public async Task CanceledPublicationDoesNotReplaceTheMemoryCheckpoint()
        {
            var f = Create();
            await f.Session.StartLocal();
            f.Session.MarkLocalReady();
            var mark = f.Session.Committed;
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            Assert.Throws<OperationCanceledException>(() => f.Session.PublishCheckpoint(cancel.Token));
            Assert.AreSame(mark, f.Session.Committed);
            Assert.IsTrue(f.Session.GameplayReady);
            await f.Session.Stop();
        }

        [Test]
        public async Task FailedStartupAndLateShutdownCannotPublishOrAdvance()
        {
            var f = Create();
            f.Runner.Startup = _ => Task.FromResult(new StartGameResult { Ok = false });
            Assert.ThrowsAsync<InvalidOperationException>(async () => await f.Session.StartLocal());
            Assert.IsFalse(f.Session.RunnerStarted);
            Assert.IsFalse(f.Session.GameplayReady);
            Assert.IsNull(f.Session.Committed);
            Assert.Throws<InvalidOperationException>(() => f.Authority.Advance(17));
            Assert.IsFalse(await f.Session.PublishCheckpoint());
            await f.Session.Stop();
            f.Session.OnShutdown(f.Runner, ShutdownReason.Ok);
            Assert.IsFalse(f.Session.GameplayReady);
        }

        [Test]
        public async Task SceneAndIsolationAreRequiredBeforeSdkStartup()
        {
            var f = Create();
            Assert.Throws<ArgumentException>(() => new WordboundLocalSession(() => f.Runner,
                new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Single }, f.Authority, Actor));
            int creations = 0;
            var session = new WordboundLocalSession(() => { creations++; return f.Runner; },
                new NetworkProjectConfig { PeerMode = NetworkProjectConfig.PeerModes.Multiple }, f.Authority, Actor);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await session.StartLocal());
            Assert.AreEqual(0, creations);
            session.ConfigureScene(SceneRef.FromIndex(0));
            await session.StartLocal();
            Assert.Throws<InvalidOperationException>(() => session.ConfigureScene(SceneRef.FromIndex(1)));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await session.StartLocal());
            Assert.AreEqual(1, creations);
            await session.Stop();
        }

        [Test]
        public async Task ACommandCannotSupplyItsOwnActorAndDuplicateReceiptStaysSingle()
        {
            var f = Create();
            await f.Session.StartLocal();
            f.Session.MarkLocalReady();
            var spoofed = Command();
            spoofed["actorId"] = "outside-player";
            Assert.Throws<FormatException>(() => f.Session.SubmitCommand(spoofed));
            Assert.AreEqual(0, f.Authority.Match.Capture(1).Commands.Frontiers[Actor]);
            f.Session.SubmitCommand(Command());
            f.Session.SubmitCommand(Command());
            f.Authority.Advance(17);
            var state = f.Authority.Match.Capture(1);
            Assert.AreEqual(1, state.Commands.Frontiers[Actor]);
            Assert.AreEqual(1, state.Commands.Receipts.Count(r => r.ActorId == Actor && r.Sequence == 1));
            await f.Session.Stop();
        }
    }
}
