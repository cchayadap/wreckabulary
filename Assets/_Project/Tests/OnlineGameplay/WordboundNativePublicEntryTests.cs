using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicTeamRacePreservesWholeSoloParties() => PublicEntry("TeamRace", RaceFormat.Solo, 2);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicTeamRacePreservesWholeDuoParties() => PublicEntry("TeamRace", RaceFormat.Duo, 4);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicTeamRacePreservesWholeSquadParties() => PublicEntry("TeamRace", RaceFormat.Squad, 8);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicBrawlRacePreservesWholeSoloParties() => PublicEntry("BrawlRace", RaceFormat.Solo, 2);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicBrawlRacePreservesWholeDuoParties() => PublicEntry("BrawlRace", RaceFormat.Duo, 4);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicBrawlRacePreservesWholeSquadParties() => PublicEntry("BrawlRace", RaceFormat.Squad, 8);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicTimeRunRequiresTwoRealHumans() => PublicEntry("TimeRun", RaceFormat.Solo, 2);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicFreeForAllAllocatesTwoIndividuals() => PublicEntry("FreeForAll", RaceFormat.Solo, 2);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicGameplay")]
        public IEnumerator PublicFreestyleFillsTwoRealHumansWithCoopRules() => PublicEntry("Freestyle", RaceFormat.Solo, 2);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicCapacityGameplay")]
        public IEnumerator PublicTimeRunAllocatesFourAuthenticatedCooperativePlayers() =>
            PublicEntry("TimeRun", RaceFormat.Solo, 4, maximumCapacity: true);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicCapacityGameplay")]
        public IEnumerator PublicFreeForAllAllocatesEightAuthenticatedIndividuals() =>
            PublicEntry("FreeForAll", RaceFormat.Solo, 8, maximumCapacity: true);
        [UnityTest, Timeout(600000), Category("ExternalPhotonPublicCapacityGameplay")]
        public IEnumerator PublicFreestyleFillsEightAuthenticatedCooperativePlayers() =>
            PublicEntry("Freestyle", RaceFormat.Solo, 8, maximumCapacity: true);

        IEnumerator PublicEntry(string mode, RaceFormat format, int count, Func<Peer, IEnumerator> afterStarted = null,
            bool maximumCapacity = false)
        {
            if (Environment.GetEnvironmentVariable("WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY") != "1")
                Assert.Ignore("Set WORDBOUND_EXTERNAL_PUBLIC_GAMEPLAY=1 only after the exact exported catalogue is approved on the Worker.");
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Environment.GetEnvironmentVariable("WORDBOUND_PUBLIC_CATALOGUE") ??
                Path.Combine(projectRoot, "Assets/_Project/Resources/Wordbound/public-catalogue.json");
            Assert.IsTrue(File.Exists(path), "Root must generate the native public catalogue first.");
            var catalogue = WordboundPublicCourseCatalog.Parse(File.ReadAllText(path));
            var launchAssets = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Assert.IsNotNull(launchAssets, "The public entry test requires the actual player launch assets.");
            catalogue.RequireRelease(launchAssets.MapPoolVersion);
            Assert.AreEqual(catalogue.Json, launchAssets.PublicCoursesJson,
                "The tested catalogue must be the exact catalogue assigned to the shipped player.");
            string folder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            var selected = catalogue.ResolveSelection(File.ReadAllText(Path.Combine(folder, "items.json")),
                File.ReadAllText(Path.Combine(folder, "rules.json")), mode, format.ToString(), "0123456789abcdef");
            Assert.IsTrue(new WordboundPublicCourseRegistry(catalogue.Json).Allows(selected));
            Assert.AreEqual(mode, selected.Mode.Id);
            Assert.AreEqual(format, selected.Mode.Format);
            if (maximumCapacity)
            {
                Assert.IsFalse(selected.Mode.HasRaceFormat, "The separate capacity cases cover the three modes without race formats.");
                Assert.AreEqual(selected.Mode.MaximumPlayers, count, "Capacity coverage must request the exact admitted mode maximum.");
            }
            else
                Assert.AreEqual(selected.Mode.HasRaceFormat ? 2 * selected.Mode.TeamCapacity : 2, count,
                    "Public race coverage must start two complete parties of the requested format; other cases use two authenticated clients.");
            var run = Run(count, false, productionMode: selected.Mode.Id,
                productionFormat: format, publicCatalogue: catalogue, afterStarted: afterStarted);
            try { while (run.MoveNext()) yield return run.Current; }
            finally { (run as IDisposable)?.Dispose(); }
            Assert.AreEqual(mode == "Freestyle", live.Settings.Fill,
                "Preserve the existing public test settings: full race parties and the cooperative Freestyle Fill case.");
            if (maximumCapacity) AssertPublicMaximumRoster(selected.Mode, count);
            Debug.Log("WORDBOUND_PUBLIC_MODE_OBSERVED: " + mode + "/" + format + "; authenticatedClients=" + count +
                "; fill=" + live.Settings.Fill + "; exact public allocation, whole-party roster and authenticated Fusion gameplay observed; cleanup pending.");
        }

        void AssertPublicMaximumRoster(ModeDefinition mode, int count)
        {
            Assert.IsTrue(live.PublicQueue);
            Assert.AreEqual(mode.MaximumPlayers, count);
            Assert.AreEqual(count, live.Clients.Count);
            Assert.AreEqual(count, peers.Count);
            var players = live.Clients.Select(client => client.Credentials.PlayerId).ToArray();
            var roster = live.Verified.Roster;
            Assert.AreEqual(count, players.Distinct(StringComparer.Ordinal).Count());
            CollectionAssert.AreEquivalent(players, roster.Select(seat => seat.PlayerId));
            CollectionAssert.AreEquivalent(Enumerable.Range(0, mode.TeamCount), roster.Select(seat => seat.TeamId).Distinct());
            foreach (var team in roster.GroupBy(seat => seat.TeamId))
                CollectionAssert.AreEquivalent(Enumerable.Range(0, mode.TeamCapacity), team.Select(seat => seat.Slot));
            var parties = roster.GroupBy(seat => seat.PartyId).ToArray();
            Assert.AreEqual(mode.Id == "TimeRun" ? 1 : count, parties.Length);
            Assert.IsTrue(parties.All(party => party.Count() == (mode.Id == "TimeRun" ? count : 1)),
                "TimeRun keeps its complete cooperative party; FFA and Fill Freestyle keep each individual party.");
            var seats = roster.Select(seat => (seat.PlayerId, seat.TeamId, seat.Slot)).ToArray();
            var host = peers.Single(peer => peer.Runner.IsServer);
            var nativePlayers = peers.Select(peer => peer.Runner.LocalPlayer).ToArray();
            Assert.AreEqual(count, nativePlayers.Distinct().Count(), "Every authenticated peer must own a distinct native PlayerRef.");
            CollectionAssert.AreEquivalent(players, nativePlayers.Select(host.Runner.GetPlayerUserId),
                "The host must resolve the exact authenticated roster in Fusion Client/Server mode.");
            foreach (var peer in peers)
            {
                Assert.IsTrue(peer.Runner.IsRunning);
                CollectionAssert.AreEquivalent(nativePlayers, peer.Runner.ActivePlayers,
                    "Every peer must observe the exact native PlayerRef roster.");
                Assert.AreEqual(peer.Actor, peer.Runner.GetPlayerUserId(peer.Runner.LocalPlayer));
                Assert.IsTrue(host.Session.TryGetAdmittedActor(peer.Runner.LocalPlayer, out var admittedActor),
                    "Every native player must retain its authenticated production admission.");
                Assert.AreEqual(peer.Actor, admittedActor);
                var frame = peer.Controller.Frame;
                Assert.AreEqual(live.Verified.Match.RosterHash, frame.Binding.RosterHash);
                CollectionAssert.AreEquivalent(seats, frame.Binding.Seats.Select(seat => (seat.PlayerId, seat.Team, seat.Slot)));
                CollectionAssert.AreEquivalent(seats, frame.Progress.Players.Select(player => (player.Id, player.Team, player.Slot)));
                Assert.IsTrue(frame.Progress.Players.All(player => player.Connected && !player.Returned));
            }
            Debug.Log("WORDBOUND_PUBLIC_CAPACITY_OBSERVED: mode=" + mode.Id + "; authenticatedClients=" + count +
                "; signedTeams=" + mode.TeamCount + "; signedParties=" + parties.Length +
                "; exactMaximumRoster=true; allNativeAndCanonicalRosters=true; cleanup pending.");
        }
    }
}
