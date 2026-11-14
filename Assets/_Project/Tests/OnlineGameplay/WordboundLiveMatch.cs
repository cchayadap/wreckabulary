using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    sealed partial class WordboundLiveMatch
    {
        public const string Origin = "https://wreckabulary-wordbound-coordinator.pagna.workers.dev";
        public const string AppId = "f6fb7f4c-b8a9-44a3-ac63-bee1c5f23552";
        const string Fingerprint = "43ea939c715a181912177bff3e98c647d5690ec2b0b40085a8bb6c047ce41dce";
        public readonly ModeDefinition Mode;
        public readonly CourseCatalogue Geometry;
        public readonly List<OnlineCoordinatorClient> Clients = new List<OnlineCoordinatorClient>();
        readonly List<WordboundHttpTransport> transports = new List<WordboundHttpTransport>();
        readonly WordboundNativeCheckpoints checkpointObservations;
        public ItemCatalogue Items { get; }
        public GameRules Rules { get; }
        public ResolvedCourse Course { get; }
        public OnlineContent Content { get; }
        public OnlineSettings Settings { get; }
        public OnlineAdmissionVerifier Verifier { get; }
        public WordboundClientContent ProductionContent { get; }
        public bool PublicQueue { get; }
        public string ApprovedRelease { get; }
        public IWordboundPuzzleContent PhysicalPuzzles => ProductionContent?.PhysicalPuzzles;
        public OnlineMatchExpectation Expected { get; private set; }
        public VerifiedOnlineMatch Verified { get; private set; }
        sealed class PartyGroup
        {
            public string Id;
            public readonly List<OnlineCoordinatorClient> Members = new List<OnlineCoordinatorClient>();
        }
        readonly List<PartyGroup> parties = new List<PartyGroup>();

        public WordboundLiveMatch(string projectRoot, CourseDocument proofCourse = null,
            WordboundClientContent productionContent = null, WordboundPublicCourseCatalog publicCatalogue = null,
            WordboundNativeCheckpoints checkpointObservations = null)
        {
            this.checkpointObservations = checkpointObservations;
            if (proofCourse != null && productionContent != null)
                throw new ArgumentException("Choose an exact production course or the existing proof document.");
            ProductionContent = productionContent;
            PublicQueue = publicCatalogue != null;
            if (PublicQueue && (productionContent == null || !productionContent.Mode.PublicQueue ||
                !new WordboundPublicCourseRegistry(publicCatalogue.Json).Allows(productionContent) ||
                publicCatalogue.Find(productionContent.Mode.Id, productionContent.Mode.Format.ToString(), productionContent.Seed) == null))
                throw new ArgumentException("The public proof requires an exact native-exported approved course.");
            ApprovedRelease = publicCatalogue?.MapPoolVersion;
            var trust = OnlineJson.Parse(File.ReadAllText(Path.Combine(projectRoot, "OnlineServices/production-client.json")));
            if (trust["origin"].String() != Origin || trust["photonAppId"].String() != AppId ||
                trust["issuer"].String() != "wreckabulary-wordbound" || trust["keyId"].String() != "wordbound-rs256-20261020-01" ||
                trust["publicKeyFingerprint"].String() != Fingerprint || trust["jwks"]["keys"].Items.Count != 1)
                throw new InvalidOperationException("The native proof requires the independently pinned production trust.");
            var key = trust["jwks"]["keys"].Items.Single();
            if (OnlineJson.Hash(new Dictionary<string, object> {
                ["e"] = key["e"].String(), ["kty"] = key["kty"].String(), ["n"] = key["n"].String() }) != Fingerprint ||
                key["kid"].String() != trust["keyId"].String())
                throw new InvalidOperationException("The production public key fingerprint does not match.");
            Verifier = new OnlineAdmissionVerifier(trust["issuer"].String(), trust["jwks"]);
            string installedFolder = Path.Combine(projectRoot, "Assets/_Project/Data/Config");
            installedItems = File.ReadAllText(Path.Combine(installedFolder, "items.json"));
            installedRules = File.ReadAllText(Path.Combine(installedFolder, "rules.json"));
            if (productionContent != null)
            {
                Mode = productionContent.Mode; Geometry = productionContent.Geometry;
                Items = productionContent.Items; Rules = productionContent.Rules; Course = productionContent.Course;
                Content = PublicQueue ? productionContent.PublicBinding : productionContent.PrivateBinding;
                if (!productionContent.Matches(Content) || ((Mode.IsRace || Mode.Id == "Freestyle") &&
                    (PhysicalPuzzles == null || !PhysicalPuzzles.BindsRules || Course.PhysicalSites.Count == 0)))
                    throw new ArgumentException("The production fixture requires its exact installed content and physical plan where applicable.");
            }
            else
            {
                Mode = ModeDefinition.For("Freestyle"); Geometry = CourseTemplates.ProofCatalogue();
                string itemJson = installedItems;
                string ruleJson = installedRules;
                Items = ItemCatalogue.FromJson(itemJson, "native-proof-items");
                Rules = Mode.CreateGameplayRules(RuleBook.FromJson(ruleJson, "native-proof-rules").For("Dibs"));
                var document = proofCourse ?? CourseTemplates.FreestyleWorld();
                var resolved = CourseResolver.Resolve(document, Geometry,
                    new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(Mode)));
                if (!resolved.Success) throw new InvalidOperationException("The native proof course did not resolve.");
                Course = resolved.Manifest;
                string catalogueHash = OnlineJson.Hash(new Dictionary<string, object> {
                    ["geometry"] = Geometry.Hash, ["items"] = WordboundIdentity.Sha256(itemJson),
                    ["rules"] = WordboundIdentity.Sha256(ruleJson) });
                Content = new OnlineContent("Workshop", document.Id, Course.SourceHash, Course.Hash, Mode.Hash, catalogueHash);
            }
            Settings = new OnlineSettings(Mode.Id, Mode.Format.ToString(), PublicQueue && Mode.Id == "Freestyle", "asia",
                MatchSnapshotCodec.ProtocolVersion, (PublicQueue ? "pub-" : "native-") + Guid.NewGuid().ToString("N").Substring(0, 12));
        }

        public async Task Prepare(int count, CancellationToken ct)
        {
            if (ProductionContent?.WorkshopBundle != null) { await PrepareWorkshop(count, ct); return; }
            if (count != 2 && count != 4 && count != 8) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > Mode.MaximumPlayers) throw new ArgumentException("The requested peers exceed the selected mode.");
            for (int i = 0; i < count; i++)
            {
                var transport = new WordboundHttpTransport(new Uri(Origin));
                transports.Add(transport);
                IOnlineHttpTransport observed = new ReconnectFaultTransport(this, i, transport);
                if (checkpointObservations != null) observed = checkpointObservations.ForPeer(i).Wrap(observed);
                var client = new OnlineCoordinatorClient(observed);
                Clients.Add(client);
                await client.CreateGuest("NativeGameplay" + i, ct);
            }
            string privateCode = null;
            int partySize = Mode.HasRaceFormat ? count / 2 : Mode.Id == "FreeForAll" || (PublicQueue && Mode.Id == "Freestyle") ? 1 : 4;
            foreach (var offset in Enumerable.Range(0, (count + partySize - 1) / partySize).Select(i => i * partySize))
            {
                var members = Clients.Skip(offset).Take(partySize).ToArray();
                var party = await members[0].CreateParty(Settings, Key(), ct);
                var group = new PartyGroup { Id = party["id"].String() };
                group.Members.Add(members[0]); parties.Add(group);
                foreach (var member in members.Skip(1))
                {
                    party = await member.JoinParty(group.Id, party["invite"].String(), Key(), ct);
                    group.Members.Add(member);
                }
                foreach (var member in group.Members)
                    party = await member.Ready(group.Id, party["revision"].String(), true, Key(), ct);
                await members[0].Queue(group.Id, party["revision"].String(),
                    PublicQueue ? "QuickPlay" : privateCode == null ? "PrivateCreate" : "PrivateJoin", Key(),
                    PublicQueue || privateCode == null ? Content : null, privateCode, ct);
                if (!PublicQueue)
                    privateCode = (await members[0].Party(group.Id, ct))["assignment"]["privateCode"].String();
            }
            var assignment = (await Clients[0].Party(parties[0].Id, ct))["assignment"];
            if (PublicQueue) {
                long deadline = Now() + 45000;
                while (assignment["state"].String() == "FORMING" && Now() < deadline) {
                    await Task.Delay(500, ct);
                    assignment = (await Clients[0].Party(parties[0].Id, ct))["assignment"];
                }
            }
            string phase = assignment["state"].String();
            if ((phase != "FORMING" && phase != "ADMITTING") || assignment["roster"].Items.Count != count)
                throw new InvalidOperationException("The private native proof must reserve exactly its original seats.");
            if (PublicQueue && (phase != "ADMITTING" || assignment.Has("privateCode") ||
                OnlineJson.Hash(OnlineContent.Read(assignment["content"]).ToValue()) != OnlineJson.Hash(Content.ToValue()) ||
                OnlineJson.Hash(OnlineSettings.Read(assignment["settings"]).ToValue()) != OnlineJson.Hash(Settings.ToValue())))
                throw new InvalidOperationException("The public queue must lock the exact approved content and isolated pool without a private code.");
            string rosterHash = RosterHash(assignment);
            Expected = new OnlineMatchExpectation(assignment["matchId"].String(),
                assignment["reservationGeneration"].String(), rosterHash, Settings, Content);
            var launched = phase == "FORMING" ? await Clients[0].MatchAction(Expected, "launch", ct) : assignment;
            if (launched["state"].String() != "ADMITTING" || launched["rosterHash"].String() != rosterHash ||
                launched["reservationGeneration"].String() != Expected.Generation)
                throw new InvalidOperationException("Launching changed the expected private content or roster.");
            foreach (var client in Clients)
            {
                await client.MatchAction(Expected, "admission", ct);
                await client.AcceptMatch(Expected, ct);
            }
            var certificate = await Clients[0].MatchAction(Expected, "certificate", ct);
            Verified = Verifier.VerifyStart(certificate["certificate"].String(), Expected, Now() / 1000);
            if (Verified.Roster.Count != count ||
                !Verified.Roster.Select(s => s.PlayerId).OrderBy(x => x, StringComparer.Ordinal)
                    .SequenceEqual(Clients.Select(c => c.Credentials.PlayerId).OrderBy(x => x, StringComparer.Ordinal)))
                throw new InvalidOperationException("The signed roster does not contain exactly the authenticated guests.");
            foreach (var group in parties) {
                var signed = Verified.Roster.Where(seat => seat.PartyId == group.Id).ToArray();
                if (signed.Length != group.Members.Count || signed.Select(seat => seat.TeamId).Distinct().Count() != 1 ||
                    !new HashSet<string>(signed.Select(seat => seat.PlayerId), StringComparer.Ordinal)
                        .SetEquals(group.Members.Select(member => member.Credentials.PlayerId)))
                    throw new InvalidOperationException("The signed party membership differs from its original authenticated members.");
            }
            if (Mode.HasRaceFormat)
            {
                var teams = Verified.Roster.GroupBy(s => s.TeamId).OrderBy(g => g.Key).ToArray();
                if (teams.Length != 2 || teams[0].Key != 0 || teams[1].Key != 1 ||
                    teams.Any(g => g.Count() != count / 2 || g.Select(s => s.PartyId).Distinct().Count() != 1) ||
                    teams.Select(g => g.First().PartyId).Distinct().Count() != 2 || parties.Count != 2)
                    throw new InvalidOperationException("The signed race roster must preserve the two exact whole parties.");
                foreach (var group in parties)
                {
                    var signed = Verified.Roster.Where(seat => seat.PartyId == group.Id).ToArray();
                    if (signed.Length != group.Members.Count || signed.Select(seat => seat.TeamId).Distinct().Count() != 1 ||
                        !new HashSet<string>(signed.Select(seat => seat.PlayerId), StringComparer.Ordinal)
                            .SetEquals(group.Members.Select(member => member.Credentials.PlayerId)))
                        throw new InvalidOperationException("The signed race party membership differs from its original authenticated members.");
                }
            }
        }

        public async Task<WordboundFusionJoin> FreshJoin(int seat, CancellationToken ct)
        {
            if (workshopLaunches != null) return await workshopLaunches[seat].FreshJoin(ct);
            var client = Clients[seat];
            var certificate = await client.MatchAction(Expected, "certificate", ct);
            var admission = await client.MatchAction(Expected, "admission", ct);
            var ticket = await client.PhotonTicket(ct);
            return new WordboundFusionJoin(client.Credentials.PlayerId, certificate["certificate"].String(),
                admission["grant"].String(), admission["connectionSecret"].String(), ticket);
        }

        public OnlineConnectionService Connections(int seat) =>
            new OnlineConnectionService(Clients[seat], Verifier, Expected);

        static string RosterHash(JsonNode assignment) => OnlineJson.Hash(assignment["roster"].Items.Select(s =>
            new Dictionary<string, object> {
                ["playerId"] = s["playerId"].String(), ["partyId"] = s["partyId"].String(),
                ["partyRevision"] = s["partyRevision"].String(), ["attemptId"] = s["attemptId"].String(),
                ["teamId"] = (int)s["teamId"].Number(), ["seat"] = (int)s["seat"].Number(),
                ["connectionSecretHash"] = s["connectionSecretHash"].String() }).ToArray());

        CleanupExecution cleanupExecution;
        Task<int> cleanupTask;
        bool cleanupResourcesDisposed;
        int cleanupResourceFailures;
        CleanupExecution CleanupPlan => cleanupExecution ??= new CleanupExecution(Clients,
            parties.Select(group => new CleanupParty(group.Id, group.Members.Select(member => Clients.IndexOf(member)))), Expected,
            message => UnityEngine.Debug.Log(message));
        public TimeSpan CleanupWatchdog => CleanupPlan.Watchdog;
        public Task<int> Cleanup(CancellationToken ct) => cleanupTask ??= FinishCleanup(ct);
        public void AbortCleanup()
        {
            cleanupExecution?.Abort();
            DisposeCleanupResources();
        }
        async Task<int> FinishCleanup(CancellationToken ct)
        {
            int failures = 0;
            try { failures = await CleanupPlan.Run(ct); }
            catch (Exception error) { failures = 1 + (cleanupExecution?.FailureCount ?? 0); ReportServiceFailure("cleanup-plan", -1, error); }
            finally { DisposeCleanupResources(); }
            failures += cleanupResourceFailures;
            UnityEngine.Debug.Log("WORDBOUND_SERVICE_CLEANUP: failures=" + failures +
                "; return=" + (cleanupExecution?.Failures("return") ?? 0) +
                "; cancel=" + (cleanupExecution?.Failures("cancel") ?? 0) +
                "; leave=" + (cleanupExecution?.Failures("leave") ?? 0) +
                "; dispose=" + cleanupResourceFailures + "; clients=" + Clients.Count + "; parties=" + parties.Count);
            return failures;
        }
        void DisposeCleanupResources()
        {
            if (cleanupResourcesDisposed) return;
            cleanupResourcesDisposed = true;
            void Dispose(Action action)
            {
                try { action(); }
                catch (Exception error) { cleanupResourceFailures++; ReportServiceFailure("cleanup-dispose", -1, error); }
            }
            foreach (var client in Clients) Dispose(client.ClearCredentials);
            foreach (var coordinator in workshopCoordinators) Dispose(coordinator.Dispose);
            foreach (var transport in transports) Dispose(transport.Dispose);
        }

        internal sealed class CleanupParty
        {
            public string Id { get; }
            public IReadOnlyList<int> Members { get; }
            public CleanupParty(string id, IEnumerable<int> members)
            { Id = id; Members = Array.AsReadOnly(members.ToArray()); }
        }

        internal sealed class CleanupReceipt
        {
            public string MatchId { get; }
            public string Stage { get; }
            public int Peer { get; }
            public int Party { get; }
            public string Request { get; }
            public bool Started { get; }
            public string Outcome { get; }
            public bool RequestCancelled { get; }
            public bool OuterCancelled { get; }
            public bool Aborted { get; }
            public string FailureType { get; }
            public int HttpStatus { get; }
            public string Code { get; }
            public double StartMilliseconds { get; }
            public double EndMilliseconds { get; }
            public CleanupReceipt(string matchId, string stage, int peer, int party, string request, bool started, string outcome,
                bool requestCancelled, bool outerCancelled, bool aborted, Exception error, double start, double end)
            {
                MatchId = matchId; Stage = stage; Peer = peer; Party = party; Request = request; Started = started; Outcome = outcome;
                RequestCancelled = requestCancelled; OuterCancelled = outerCancelled; Aborted = aborted;
                FailureType = error == null ? "none" : SafeFailureType(error);
                HttpStatus = error is OnlineServiceException service ? service.Status : 0;
                Code = error is OnlineServiceException coded ? SafeServiceCode(coded.Code) : "none";
                StartMilliseconds = start; EndMilliseconds = end;
            }
            public string Line => FormattableString.Invariant($"WORDBOUND_SERVICE_REQUEST: match={MatchId}; stage=cleanup-{Stage}; peer={Peer}; party={Party}; request={Request}; attempt=1; started={Started}; outcome={Outcome}; startMs={StartMilliseconds:F3}; endMs={EndMilliseconds:F3}; budgetMs=15000; requestCancelled={RequestCancelled}; outerCancelled={OuterCancelled}; aborted={Aborted}; type={FailureType}; httpStatus={HttpStatus}; code={Code}");
        }

        internal sealed class CleanupExecution
        {
            sealed class Stage
            {
                public readonly string Kind;
                public readonly int Peer, Party;
                public string Outcome = "pending";
                public Stage(string kind, int peer, int party) { Kind = kind; Peer = peer; Party = party; }
            }
            sealed class IncompleteRequestException : TimeoutException { }
            readonly OnlineCoordinatorClient[] clients;
            readonly string[] playerIds;
            readonly CleanupParty[] groups;
            readonly OnlineMatchExpectation expected;
            readonly Action<string> log;
            readonly Action<CancellationTokenSource> scheduleDeadline;
            readonly TaskCompletionSource<bool> aborted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            readonly List<CleanupReceipt> receipts = new List<CleanupReceipt>();
            readonly Stage[] returns, cancels;
            readonly Stage[][] leaves;
            readonly System.Diagnostics.Stopwatch elapsed = new System.Diagnostics.Stopwatch();
            CancellationToken outer;
            bool started;
            public TimeSpan Watchdog { get; }
            public IReadOnlyList<CleanupReceipt> Receipts { get { lock (receipts) return receipts.ToArray(); } }
            public int FailureCount => AllStages().Count(stage => stage.Outcome != "success" && stage.Outcome != "already-left");
            public int Failures(string kind) => AllStages().Count(stage => stage.Kind == kind &&
                stage.Outcome != "success" && stage.Outcome != "already-left");
            IEnumerable<Stage> AllStages() => returns.Concat(cancels).Concat(leaves.SelectMany(group => group));

            public CleanupExecution(IEnumerable<OnlineCoordinatorClient> clients, IEnumerable<CleanupParty> groups,
                OnlineMatchExpectation expected, Action<string> log, Action<CancellationTokenSource> scheduleDeadline = null)
            {
                this.clients = clients.ToArray(); this.groups = groups.ToArray(); this.expected = expected;
                this.log = log ?? throw new ArgumentNullException(nameof(log));
                this.scheduleDeadline = scheduleDeadline ?? (source => source.CancelAfter(TimeSpan.FromSeconds(15)));
                if (this.clients.Length > 8 || this.clients.Any(client => client == null) ||
                    this.clients.Distinct().Count() != this.clients.Length || this.groups.Length > 8 ||
                    this.groups.Any(group => group == null || !Guid.TryParse(group.Id, out _) || group.Members.Count < 1 || group.Members.Count > 4) ||
                    this.groups.Select(group => group.Id).Distinct(StringComparer.Ordinal).Count() != this.groups.Length)
                    throw new ArgumentException("Cleanup must describe a bounded original fixture roster.");
                var members = this.groups.SelectMany(group => group.Members).ToArray();
                if (members.Any(peer => peer < 0 || peer >= this.clients.Length) || members.Distinct().Count() != members.Length)
                    throw new ArgumentException("Cleanup party membership must be unique and owned by the fixture.");
                playerIds = this.clients.Select(client => client.Credentials?.PlayerId).ToArray();
                returns = expected == null ? Array.Empty<Stage>() : this.clients.Select((_, peer) => new Stage("return", peer, -1)).ToArray();
                cancels = this.groups.Select((group, party) => new Stage("cancel", group.Members[0], party)).ToArray();
                leaves = this.groups.Select((group, party) => group.Members.Reverse().Select(peer => new Stage("leave", peer, party)).ToArray()).ToArray();
                int waves = returns.Length == 0 ? 0 : 1;
                int partyRequests = this.groups.Length == 0 ? 0 : this.groups.Max(group => 2 + 4 * group.Members.Count);
                Watchdog = TimeSpan.FromSeconds(15 * (waves + partyRequests) + 15);
            }

            public void Abort() => aborted.TrySetResult(true);
            public async Task<int> Run(CancellationToken cancellation)
            {
                if (started) throw new InvalidOperationException("Cleanup may run only once.");
                started = true; outer = cancellation; elapsed.Start();
                await Task.WhenAll(returns.Select(stage => Execute(stage, async () => {
                    var returned = await Request(stage, "return", token => clients[stage.Peer].MatchAction(expected, "return", token));
                    if (returned["state"].String() != "RETURNED" || returned["playerId"].String() != playerIds[stage.Peer])
                        throw new InvalidOperationException("Cleanup return did not confirm the original actor.");
                    return false;
                })));
                await Task.WhenAll(groups.Select((group, party) => CleanupGroup(group, party)));
                return FailureCount;
            }

            async Task CleanupGroup(CleanupParty group, int partyIndex)
            {
                var cancel = cancels[partyIndex];
                await Execute(cancel, async () => {
                    var party = await Request(cancel, "party-before-cancel", token => clients[cancel.Peer].Party(group.Id, token));
                    string key = Key();
                    await Request(cancel, "cancel", token => clients[cancel.Peer].Cancel(group.Id, party["revision"].String(), key, token));
                    return false;
                });
                foreach (var leave in leaves[partyIndex])
                    await Execute(leave, async () => {
                        var client = clients[leave.Peer];
                        var profile = await Request(leave, "profile-before-leave", client.Profile);
                        if (!profile.Has("partyId")) return true;
                        if (profile["partyId"].String() != group.Id) throw new InvalidOperationException("Cleanup membership changed.");
                        var party = await Request(leave, "party-before-leave", token => client.Party(group.Id, token));
                        string key = Key();
                        await Request(leave, "leave", token => client.Leave(group.Id, party["revision"].String(), key, token));
                        if ((await Request(leave, "profile-after-leave", client.Profile)).Has("partyId"))
                            throw new InvalidOperationException("Cleanup left a party membership.");
                        return false;
                    });
            }

            async Task Execute(Stage stage, Func<Task<bool>> operation)
            {
                if (outer.IsCancellationRequested || aborted.Task.IsCompleted)
                {
                    stage.Outcome = "blocked";
                    log($"WORDBOUND_SERVICE_STAGE: stage=cleanup-{stage.Kind}; peer={stage.Peer}; party={stage.Party}; outcome=blocked; outerCancelled={outer.IsCancellationRequested}");
                    return;
                }
                try { stage.Outcome = await operation() ? "already-left" : "success"; }
                catch (Exception error)
                {
                    stage.Outcome = error is IncompleteRequestException ? "incomplete" : "failed";
                    log("WORDBOUND_SERVICE_FAILURE: stage=cleanup-" + stage.Kind + "; peer=" + stage.Peer +
                        "; type=" + SafeFailureType(error) + "; httpStatus=" + (error is OnlineServiceException service ? service.Status : 0) +
                        "; code=" + (error is OnlineServiceException coded ? SafeServiceCode(coded.Code) : "none"));
                }
                log($"WORDBOUND_SERVICE_STAGE: stage=cleanup-{stage.Kind}; peer={stage.Peer}; party={stage.Party}; outcome={stage.Outcome}");
            }

            async Task<JsonNode> Request(Stage stage, string name, Func<CancellationToken, Task<JsonNode>> send)
            {
                using var request = CancellationTokenSource.CreateLinkedTokenSource(outer);
                double start = elapsed.Elapsed.TotalMilliseconds;
                bool sent = false;
                string outcome = "failed";
                Exception failure = null;
                try
                {
                    outer.ThrowIfCancellationRequested();
                    if (aborted.Task.IsCompleted) throw new IncompleteRequestException();
                    scheduleDeadline(request);
                    request.Token.ThrowIfCancellationRequested();
                    sent = true;
                    var task = send(request.Token);
                    await Task.WhenAny(task, aborted.Task);
                    if (!task.IsCompleted)
                    {
                        request.Cancel();
                        _ = task.ContinueWith(done => { _ = done.Exception; }, CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        throw new IncompleteRequestException();
                    }
                    var result = await task;
                    request.Token.ThrowIfCancellationRequested();
                    outcome = "success";
                    return result;
                }
                catch (Exception error)
                {
                    failure = error;
                    outcome = error is IncompleteRequestException ? "incomplete" : sent ? "failed" : "blocked";
                    throw;
                }
                finally
                {
                    var receipt = new CleanupReceipt(expected?.MatchId ?? "none", stage.Kind, stage.Peer, stage.Party, name, sent, outcome,
                        request.IsCancellationRequested && !outer.IsCancellationRequested && !aborted.Task.IsCompleted,
                        outer.IsCancellationRequested, aborted.Task.IsCompleted, failure, start, elapsed.Elapsed.TotalMilliseconds);
                    lock (receipts) receipts.Add(receipt);
                    log(receipt.Line);
                }
            }
        }
        static void ReportServiceFailure(string stage, int peer, Exception error) =>
            UnityEngine.Debug.Log("WORDBOUND_SERVICE_FAILURE: stage=" + stage + "; peer=" + peer +
                "; type=" + SafeFailureType(error) + "; httpStatus=" + (error is OnlineServiceException service ? service.Status : 0) +
                "; code=" + (error is OnlineServiceException coded ? SafeServiceCode(coded.Code) : "none"));
        static string SafeFailureType(Exception error) => error switch
        {
            OnlineServiceException => "OnlineServiceException",
            OperationCanceledException => "OperationCanceledException",
            TimeoutException => "TimeoutException",
            FormatException => "FormatException",
            ArgumentException => "ArgumentException",
            InvalidOperationException => "InvalidOperationException",
            IOException => "IOException",
            _ => "OtherException"
        };
        static string SafeServiceCode(string code) => code switch
        {
            "REVISION_CONFLICT" or "PARTY_NOT_READY" or "PUBLIC_COOP_REQUIRES_HUMANS" or "CONTENT_NOT_APPROVED" or
            "CONTENT_SETTINGS_MISMATCH" or "CONTENT_UPLOAD_EXPIRED" or "CONTENT_SOURCE_EXPIRED" or
            "AUTHORED_PRIVATE_CREATE_ONLY" or "MATCH_NOT_FULL" or "OWNER_REQUIRED" or "PRIVATE_SETTINGS_MISMATCH" or
            "RECONNECT_EXPIRED" or "ALREADY_IN_PARTY" or "INVALID_REFRESH" or "PARTY_FULL" or "PARTY_NOT_FOUND" or
            "MATCH_NOT_FOUND" or "MATCH_CLOSED" or "UNAUTHORIZED" or "FORBIDDEN" or "INVALID_REQUEST" or
            "NOT_FOUND" or "INTERNAL_ERROR" or "RATE_LIMITED" or "CONTENT_INVALID" or "CONTENT_HASH_MISMATCH" or
            "NOT_FORMING" or "MATCH_NOT_STARTED" or "NOT_ADMITTING" or "ROSTER_NOT_LOCKED" or
            "CONTENT_OR_ROSTER_MISMATCH" or "NOT_MATCH_MEMBER" => code,
            _ => "unlisted"
        };
        static string Key() => Guid.NewGuid().ToString("N");
        public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
