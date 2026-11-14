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
                await WorkshopStep(i, "connect", () => coordinator.Connect("NativeWorkshop" + i));
            }
            var host = workshopCoordinators[0]; var remote = workshopCoordinators[1];
            workshopTransports[0].ResetObservation();
            await host.CreateAuthoredParty(Settings, ProductionContent);
            if (host.Party != null)
            {
                var group = new PartyGroup { Id = host.Party.Id };
                group.Members.AddRange(Clients); parties.Add(group);
            }
            CheckWorkshop(0, "create-authored");
            await WorkshopStep(1, "join-authored", () => remote.JoinParty(host.CopyInvite()));
            if (ReferenceEquals(host.Content, remote.Content) || remote.Content?.WorkshopBundle == null ||
                remote.Content.Course.Hash != Course.Hash || remote.Content.Course.SourceHash != Course.SourceHash ||
                remote.Content.WorkshopBundle.Descriptor.BundleId != ProductionContent.WorkshopBundle.Descriptor.BundleId ||
                !remote.Content.Matches(Content))
                throw new InvalidOperationException("The second peer did not independently download the exact authored map.");
            for (int i = 0; i < workshopCoordinators.Count; i++)
            {
                var coordinator = workshopCoordinators[i];
                await WorkshopStep(i, "poll-before-ready", coordinator.Poll);
                await WorkshopStep(i, "set-ready", () => coordinator.SetReady(true));
            }
            await WorkshopStep(0, "poll-ready-roster", host.Poll);
            if (host.Party.Members.Count != count || host.Party.Members.Any(member => !member.Ready))
                throw new InvalidOperationException("Every real authored-map peer must be ready before creating the private room.");
            await WorkshopStep(0, "queue-private", () => host.Queue("PrivateCreate"));
            await WorkshopStep(0, "poll-assignment", host.Poll);
            var assignment = host.Party.Assignment;
            if (assignment == null || !assignment.Has("matchId"))
                throw new InvalidOperationException("The private room did not expose its assignment after the party refresh.");
            string phase = assignment["state"].String();
            if (phase != "FORMING" && phase != "ADMITTING" && phase != "STARTED")
                throw new InvalidOperationException("The authored private allocation is not available for launch or admission.");
            if (assignment["roster"].Items.Count != count)
                throw new InvalidOperationException("The authored private allocation changed its original peer count.");
            Expected = new OnlineMatchExpectation(assignment["matchId"].String(),
                assignment["reservationGeneration"].String(), RosterHash(assignment), Settings, Content);
            UnityEngine.Debug.Log("WORDBOUND_WORKSHOP_ALLOCATION: phase=" + phase + "; peers=" + count +
                "; explicitLaunch=" + (phase == "FORMING"));
            if (phase == "FORMING") await WorkshopStep(0, "start-private", host.StartPrivate);
            long deadline = Now() + 45000;
            while (workshopLaunches.Any(launch => launch == null) && Now() < deadline)
            {
                ct.ThrowIfCancellationRequested();
                for (int i = 0; i < count; i++)
                {
                    if (workshopLaunches[i] != null) continue;
                    await WorkshopStep(i, "poll-admission", workshopCoordinators[i].Poll);
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
        async Task WorkshopStep(int peer, string stage, Func<Task> action)
        {
            workshopTransports[peer].ResetObservation();
            try { await action(); }
            catch (Exception error)
            {
                ReportServiceFailure("prepare-" + stage, peer, error);
                throw;
            }
            CheckWorkshop(peer, stage);
        }
        void CheckWorkshop(int peer, string stage)
        {
            var coordinator = workshopCoordinators[peer];
            if (coordinator.RetryRequired || !string.IsNullOrEmpty(coordinator.Error))
            {
                var observed = workshopTransports[peer];
                UnityEngine.Debug.Log("WORDBOUND_WORKSHOP_PREPARE_FAILURE: stage=" + stage + "; peer=" + peer +
                    "; retry=" + coordinator.RetryRequired + "; coordinatorCode=" + CoordinatorFailureCode(coordinator.Error) +
                    "; lastHttpStatus=" + observed.LastStatus + "; observedServiceError=" + observed.ServiceCode +
                    "; transportType=" + observed.FailureType + "; observations=current-stage-only");
                throw new InvalidOperationException("Authored party preparation failed at " + stage + ".");
            }
        }
        static string CoordinatorFailureCode(string message) => message switch
        {
            "The response or game content could not be verified." => "verification",
            "The request timed out. Retry the same pending action." => "request-cancelled",
            "Connection interrupted. Retry the pending action without creating another reservation." => "connection-interrupted",
            "Retry the pending action before starting another." => "pending-action",
            null => "none",
            "" => "none",
            _ => "unclassified"
        };
        sealed class WorkshopPreparationTransport : IOnlineHttpTransport
        {
            readonly WordboundHttpTransport transport;
            readonly CancellationToken preparation;
            volatile bool preparing = true;
            public int LastStatus { get; private set; }
            public string ServiceCode { get; private set; } = "none";
            public string FailureType { get; private set; } = "none";
            public WorkshopPreparationTransport(WordboundHttpTransport transport, CancellationToken preparation)
            { this.transport = transport; this.preparation = preparation; }
            public void CompletePreparation() => preparing = false;
            public void ResetObservation() { LastStatus = 0; ServiceCode = "none"; FailureType = "none"; }
            public async Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellation)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation,
                    preparing ? preparation : CancellationToken.None);
                OnlineResponse response;
                try { response = await transport.Send(request, linked.Token); }
                catch (Exception error) { FailureType = SafeFailureType(error); throw; }
                LastStatus = response.Status;
                if (response.Status < 200 || response.Status >= 300)
                {
                    ServiceCode = "unreadable";
                    try
                    {
                        if (response.Json != null && response.Json.Length <= 4096)
                            ServiceCode = SafeServiceCode(OnlineJson.Parse(response.Json)["error"]["code"].String());
                    }
                    catch { }
                }
                return response;
            }
        }
    }
}
