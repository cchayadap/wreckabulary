using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    sealed partial class WordboundLiveMatch
    {
        readonly List<WordboundPartyCoordinator> workshopCoordinators = new List<WordboundPartyCoordinator>();
        readonly List<WorkshopPreparationTransport> workshopTransports = new List<WorkshopPreparationTransport>();
        readonly string installedItems, installedRules;
        WordboundPartyLaunchContext[] workshopLaunches;

        public WordboundClientContent ContentFor(int index) => workshopLaunches == null ? ProductionContent : workshopLaunches[index].Content;

        async Task PrepareWorkshop(int count, CancellationToken ct)
        {
            try { await PrepareWorkshopCore(count, ct); }
            finally { foreach (var transport in workshopTransports) transport.CompletePreparation(); }
        }
        async Task PrepareWorkshopCore(int count, CancellationToken ct)
        {
            if (PublicQueue || count != 2 || Mode.Id != "Freestyle" && Mode.Id != "TimeRun")
                throw new InvalidOperationException("The authored transfer fixture requires two private Freestyle or TimeRun peers.");
            workshopLaunches = new WordboundPartyLaunchContext[count];
            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var transport = new WordboundHttpTransport(new Uri(Origin)); transports.Add(transport);
                var cancellable = new WorkshopPreparationTransport(transport, ct); workshopTransports.Add(cancellable);
                var client = new OnlineCoordinatorClient(cancellable); Clients.Add(client);
                var coordinator = new WordboundPartyCoordinator(client, Verifier, installedItems, installedRules, Settings.MapPoolVersion);
                workshopCoordinators.Add(coordinator);
                await coordinator.Connect("NativeWorkshop" + i); CheckWorkshop(coordinator);
            }
            var host = workshopCoordinators[0]; var remote = workshopCoordinators[1];
            await host.CreateAuthoredParty(Settings, ProductionContent);
            if (host.Party != null)
            {
                var group = new PartyGroup { Id = host.Party.Id };
                group.Members.AddRange(Clients); parties.Add(group);
            }
            CheckWorkshop(host);
            await remote.JoinParty(host.CopyInvite()); CheckWorkshop(remote);
            if (ReferenceEquals(host.Content, remote.Content) || remote.Content?.WorkshopBundle == null ||
                remote.Content.Course.Hash != Course.Hash || remote.Content.Course.SourceHash != Course.SourceHash ||
                remote.Content.WorkshopBundle.Descriptor.BundleId != ProductionContent.WorkshopBundle.Descriptor.BundleId ||
                !remote.Content.Matches(Content))
                throw new InvalidOperationException("The second peer did not independently download the exact authored map.");
            foreach (var coordinator in workshopCoordinators)
            {
                await coordinator.Poll(); CheckWorkshop(coordinator);
                await coordinator.SetReady(true); CheckWorkshop(coordinator);
            }
            await host.Poll(); CheckWorkshop(host);
            if (host.Party.Members.Count != count || host.Party.Members.Any(member => !member.Ready))
                throw new InvalidOperationException("Every real authored-map peer must be ready before creating the private room.");
            await host.Queue("PrivateCreate"); CheckWorkshop(host);
            await host.Poll(); CheckWorkshop(host);
            var assignment = host.Party.Assignment;
            if (assignment == null || !assignment.Has("matchId"))
                throw new InvalidOperationException("The private room did not expose its assignment after the party refresh.");
            Expected = new OnlineMatchExpectation(assignment["matchId"].String(),
                assignment["reservationGeneration"].String(), RosterHash(assignment), Settings, Content);
            await host.StartPrivate(); CheckWorkshop(host);
            long deadline = Now() + 45000;
            while (workshopLaunches.Any(launch => launch == null) && Now() < deadline)
            {
                ct.ThrowIfCancellationRequested();
                for (int i = 0; i < count; i++)
                {
                    if (workshopLaunches[i] != null) continue;
                    await workshopCoordinators[i].Poll(); CheckWorkshop(workshopCoordinators[i]);
                    workshopLaunches[i] = workshopCoordinators[i].TakeLaunch();
                }
                if (workshopLaunches.Any(launch => launch == null)) await Task.Delay(250, ct);
            }
            if (workshopLaunches.Any(launch => launch == null)) throw new TimeoutException("Authored party admission did not finish.");
            Verified = workshopLaunches[0].Verified; Expected = Verified.Match;
            if (Expected.Content.Kind != "Workshop" || !ProductionContent.Matches(Expected.Content) ||
                Verified.Roster.Count != count || Verified.Roster.Any(seat => seat.PartyId != host.Party.Id || seat.TeamId != 0) ||
                !new HashSet<string>(Verified.Roster.Select(seat => seat.PlayerId), StringComparer.Ordinal)
                    .SetEquals(Clients.Select(client => client.Credentials.PlayerId)) ||
                workshopLaunches.Any(launch => launch.Verified.Match.MatchId != Expected.MatchId ||
                    launch.Verified.Match.RosterHash != Expected.RosterHash || !launch.Content.Matches(Expected.Content)))
                throw new InvalidOperationException("The authored signed launch changed content or authenticated party membership.");
        }
        static void CheckWorkshop(WordboundPartyCoordinator coordinator)
        {
            if (coordinator.RetryRequired || !string.IsNullOrEmpty(coordinator.Error))
                throw new InvalidOperationException("Authored party preparation failed: " + coordinator.Error);
        }
        sealed class WorkshopPreparationTransport : IOnlineHttpTransport
        {
            readonly WordboundHttpTransport transport;
            readonly CancellationToken preparation;
            volatile bool preparing = true;
            public WorkshopPreparationTransport(WordboundHttpTransport transport, CancellationToken preparation)
            { this.transport = transport; this.preparation = preparation; }
            public void CompletePreparation() => preparing = false;
            public async Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellation)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation,
                    preparing ? preparation : CancellationToken.None);
                return await transport.Send(request, linked.Token);
            }
        }
    }
}
