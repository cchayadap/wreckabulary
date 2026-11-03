using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    static class WordboundPartyJson
    {
        public const string Uuid = "^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$";
        public const string Counter = "^(0|[1-9][0-9]{0,18})$";
        public const string HashPattern = "^[a-f0-9]{64}$";
        public static string Text(JsonNode node, string name, string pattern)
        {
            string value = node[name].String();
            var match = System.Text.RegularExpressions.Regex.Match(value, pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!match.Success || match.Length != value.Length) throw new FormatException("Invalid party response field.");
            return value;
        }
        public static long Integer(JsonNode node, string name, long min, long max)
        {
            double value = node[name].Number();
            if (double.IsNaN(value) || double.IsInfinity(value) || value != Math.Truncate(value) || value < min || value > max)
                throw new FormatException("Invalid party response number.");
            return (long)value;
        }
        public static void Fields(JsonNode node, params string[] names)
        {
            if (!node.IsObject || node.Keys.Any(k => !names.Contains(k, StringComparer.Ordinal)) || names.Any(k => !node.Has(k)))
                throw new FormatException("Invalid party response fields.");
        }
    }
    public sealed class WordboundPartyInvite
    {
        public string PartyId { get; }
        public string Invite { get; }
        public string Seed { get; }
        public WordboundPartyInvite(string text)
        {
            var parts = (text ?? "").Trim().Split(':');
            if (parts.Length != 4 || parts[0] != "wb-party") throw new ArgumentException("Paste the complete copied party invite.");
            var data = OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["party"] = parts[1], ["invite"] = parts[2], ["seed"] = parts[3] }));
            PartyId = WordboundPartyJson.Text(data, "party", WordboundPartyJson.Uuid);
            Invite = WordboundPartyJson.Text(data, "invite", "^[a-f0-9]{16}$");
            Seed = WordboundPartyJson.Text(data, "seed", "^[a-f0-9]{16}$");
        }
        public static string Encode(string partyId, string invite, string seed)
        {
            string value = "wb-party:" + partyId + ":" + invite + ":" + seed;
            _ = new WordboundPartyInvite(value); return value;
        }
    }

    public sealed class WordboundPartyView
    {
        public string Id { get; }
        public string Revision { get; }
        public string LeaderId { get; }
        public string Invite { get; }
        public OnlineSettings Settings { get; }
        public IReadOnlyList<WordboundPartyMember> Members { get; }
        public string State { get; }
        public JsonNode Assignment { get; }
        public bool HasAttempt { get; }
        public string Channel { get; }
        public OnlineWorkshopDescriptor Workshop { get; }
        internal WordboundPartyView(JsonNode json, string player)
        {
            Id = WordboundPartyJson.Text(json, "id", WordboundPartyJson.Uuid);
            Revision = WordboundPartyJson.Text(json, "revision", WordboundPartyJson.Counter);
            LeaderId = WordboundPartyJson.Text(json, "leaderId", WordboundPartyJson.Uuid);
            Invite = WordboundPartyJson.Text(json, "invite", "^[a-f0-9]{16}$");
            Settings = OnlineSettings.Read(json["settings"]);
            State = WordboundPartyJson.Text(json, "state", "^(OPEN|QUEUED|UPDATING)$");
            Members = Array.AsReadOnly(json["members"].Items.Select(m => new WordboundPartyMember(
                WordboundPartyJson.Text(m, "playerId", WordboundPartyJson.Uuid), m["ready"].Bool())).ToArray());
            if (Members.Count < 1 || Members.Count > 4 || Members.Select(m => m.PlayerId).Distinct().Count() != Members.Count ||
                Members.All(m => m.PlayerId != player) || Members.All(m => m.PlayerId != LeaderId))
                throw new FormatException("The party membership is invalid.");
            HasAttempt = json.Has("activeAttempt");
            Channel = HasAttempt ? json["activeAttempt"]["channel"].String() : null;
            Assignment = json.Has("assignment") ? json["assignment"] : null;
            Workshop = json.Has("workshop") && !json["workshop"].IsNull ? new OnlineWorkshopDescriptor(json["workshop"]) : null;
            Workshop?.Bind(Settings);
        }
    }
    public sealed class WordboundPartyMember
    {
        public string PlayerId { get; }
        public bool Ready { get; }
        internal WordboundPartyMember(string player, bool ready) { PlayerId = player; Ready = ready; }
    }

    public sealed class WordboundPartyCoordinator : IDisposable
    {
        readonly OnlineCoordinatorClient client;
        readonly OnlineAdmissionVerifier verifier;
        readonly string items, rules, poolVersion;
        readonly WordboundPublicCourseRegistry registry;
        readonly Func<long> clock;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        readonly SemaphoreSlim authentication = new SemaphoreSlim(1, 1);
        readonly object taskGate = new object();
        readonly HashSet<string> returnedMatches = new HashSet<string>(StringComparer.Ordinal);
        readonly List<OnlineMatchExpectation> queuedReturns = new List<OnlineMatchExpectation>();
        Task pollTask;
        Func<CancellationToken, Task> pending;
        string pendingName;
        long credentialsExpire;
        int busy, disposed;
        string customAccepted, accepted, launched, selectedSeed;
        WordboundPartyLaunchContext launch;
        WordboundClientContent stagedWorkshop;
        string stagedPartyId;

        public WordboundPartyView Party { get; private set; }
        public WordboundClientContent Content { get; private set; }
        public string PlayerId => client.Credentials?.PlayerId;
        public bool Authenticated => client.Credentials != null;
        public bool Busy => Volatile.Read(ref busy) != 0;
        public string Error { get; private set; }
        public bool RetryRequired => pending != null;
        public string PendingAction => pendingName;
        public bool IsLeader => Party != null && Party.LeaderId == PlayerId;
        public bool PublicAvailable => Content != null && registry.Allows(Content);
        public bool MatchActive { get; private set; }
        public string Seed => selectedSeed;
        public string PoolVersion => poolVersion;
        public OnlineServerClock ServerClock { get; }
        public bool ReturnQueued { get { lock (taskGate) return queuedReturns.Count > 0; } }
        public const string QueuedReturnMessage = "The service is unreachable. You left the match here; the return is sent when the connection is back.";

        public WordboundPartyCoordinator(OnlineCoordinatorClient client, OnlineAdmissionVerifier verifier,
            string itemJson, string ruleBookJson, string mapPoolVersion,
            WordboundPublicCourseRegistry registry = null, Func<long> clock = null, OnlineServerClock serverClock = null)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            items = itemJson; rules = ruleBookJson; poolVersion = mapPoolVersion;
            _ = new OnlineSettings("Freestyle", "Solo", false, "asia", MatchSnapshotCodec.ProtocolVersion, poolVersion);
            this.registry = registry ?? new WordboundPublicCourseRegistry();
            ServerClock = serverClock;
            this.clock = clock ?? (serverClock != null ? serverClock.Now : (Func<long>)(() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }
        public OnlineSettings Settings(string mode, string format, bool fill, string region) =>
            new OnlineSettings(mode, format, fill, region, MatchSnapshotCodec.ProtocolVersion, poolVersion);
        WordboundClientContent Resolve(OnlineSettings settings, string seed)
        {
            if (settings.ProtocolVersion != MatchSnapshotCodec.ProtocolVersion || settings.MapPoolVersion != poolVersion)
                throw new InvalidOperationException("This party uses a different game release.");
            return new WordboundClientContent(items, rules, settings.Mode, settings.Format, seed);
        }
        void Check(CancellationToken ct) { ct.ThrowIfCancellationRequested(); if (Volatile.Read(ref disposed) != 0) throw new OperationCanceledException(); }
        static string Key() => Guid.NewGuid().ToString("N");
        void Apply(JsonNode json, string seed)
        {
            var next = new WordboundPartyView(json, PlayerId);
            var descriptor = AssignedWorkshop(next) ?? next.Workshop;
            var previous = Content;
            Party = next; selectedSeed = descriptor?.Seed ?? seed; Content = null;
            if (descriptor != null) {
                descriptor.Bind(next.Settings);
                if (previous?.WorkshopBundle?.Descriptor.BundleId == descriptor.BundleId) Content = previous;
            } else if (next.Id == stagedPartyId && stagedWorkshop != null) {
                stagedWorkshop.WorkshopBundle.Descriptor.Bind(next.Settings); Content = stagedWorkshop;
            } else Content = Resolve(next.Settings, seed);
        }
        static OnlineWorkshopDescriptor AssignedWorkshop(WordboundPartyView party) => party.Assignment != null &&
            party.Assignment.Has("workshop") && !party.Assignment["workshop"].IsNull ?
                new OnlineWorkshopDescriptor(party.Assignment["workshop"]) : null;
        async Task LoadContent(CancellationToken ct)
        {
            if (Party == null) return;
            var assigned = AssignedWorkshop(Party); var descriptor = assigned ?? Party.Workshop;
            if (descriptor == null || Content?.WorkshopBundle?.Descriptor.BundleId == descriptor.BundleId) return;
            descriptor.Bind(Party.Settings);
            CourseCatalogue geometry = WordboundClientContent.ResolveInstalledGeometry(descriptor.GeometryHash, items);
            var service = new OnlineWorkshopService(client);
            var bundle = assigned == null ? await service.DownloadParty(Party.Id, descriptor, geometry, items, rules, ct).ConfigureAwait(false) :
                await service.DownloadMatch(Party.Settings, Party.Assignment["matchId"].String(), descriptor, geometry, items, rules, ct).ConfigureAwait(false);
            Check(ct); Content = WordboundClientContent.FromDownloaded(bundle, geometry, items, rules); selectedSeed = descriptor.Seed;
        }
        public Task Connect(string displayName) => Action("Connect", async ct => {
            if (Authenticated) throw new InvalidOperationException("You are already connected.");
            var credentials = await client.CreateGuest(displayName, ct).ConfigureAwait(false);
            Check(ct); credentialsExpire = clock() + credentials.ExpiresIn * 1000;
        }, false);
        public Task ResumeGuest(string protectedRefreshToken, string partySeed = null) => Action("Reconnect guest", async ct => {
            if (Authenticated) throw new InvalidOperationException("A guest is already connected.");
            var credentials = await client.Refresh(protectedRefreshToken, ct).ConfigureAwait(false);
            Check(ct); credentialsExpire = clock() + credentials.ExpiresIn * 1000;
            var profile = await client.Profile(ct).ConfigureAwait(false); Check(ct);
            if (profile.Has("partyId"))
            {
                var existing = await client.Party(profile["partyId"].String(), ct).ConfigureAwait(false); Check(ct);
                Apply(existing, partySeed);
                await LoadContent(ct).ConfigureAwait(false); Check(ct);
            }
        }, false);
        async Task EnsureAuthentication(CancellationToken ct, bool rotate = false)
        {
            await authentication.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Check(ct);
                if (!Authenticated) throw new InvalidOperationException("Connect with a guest name first.");
                if (rotate || clock() >= credentialsExpire - 60000)
                {
                    var credentials = await client.Refresh(client.Credentials.RefreshToken, ct).ConfigureAwait(false);
                    Check(ct); credentialsExpire = clock() + credentials.ExpiresIn * 1000;
                }
            }
            finally { authentication.Release(); }
        }
        public Task CreateParty(OnlineSettings settings, string seed)
        {
            var content = Resolve(settings, seed); string key = Key();
            return Mutation("Create party", ct => client.CreateParty(settings, key, ct), json => {
                Apply(json, seed); Content = content;
            }, () => { if (Party != null) throw new InvalidOperationException("Leave your current party first."); });
        }
        public Task CreateAuthoredParty(OnlineSettings settings, WordboundClientContent content)
        {
            if (content?.WorkshopBundle?.Packet == null) throw new ArgumentException("Provide the exact locally validated workshop draft.");
            content.WorkshopBundle.Descriptor.Bind(settings);
            if (content.WorkshopBundle.Descriptor.ItemHash != WordboundIdentity.Sha256(items) ||
                content.WorkshopBundle.Descriptor.RulesHash != WordboundIdentity.Sha256(rules))
                throw new InvalidOperationException("Online workshop sharing requires this game's installed item and rule files. Use the installed release content before creating the party; custom item or rule files are not transferred.");
            if (Party != null) throw new InvalidOperationException("Leave your current party before sharing a workshop draft.");
            string key = Key(); WordboundPartyView created = null;
            return Action("Share workshop map", async ct => {
                await EnsureAuthentication(ct).ConfigureAwait(false);
                if (created == null) {
                    var json = await client.CreateParty(settings, key, ct).ConfigureAwait(false); Check(ct);
                    created = new WordboundPartyView(json, PlayerId);
                }
                Party = created; Content = stagedWorkshop = content; stagedPartyId = created.Id; selectedSeed = content.Seed;
                var result = await new OnlineWorkshopService(client).Upload(created.Id, created.Revision, content.WorkshopBundle, ct).ConfigureAwait(false);
                Check(ct); Apply(result, content.Seed); stagedWorkshop = null; stagedPartyId = null;
                await LoadContent(ct).ConfigureAwait(false);
            }, true);
        }
        public Task JoinParty(string copiedInvite)
        {
            var invite = new WordboundPartyInvite(copiedInvite); string key = Key();
            return Mutation("Join party", ct => client.JoinParty(invite.PartyId, invite.Invite, key, ct),
                json => Apply(json, invite.Seed),
                () => { if (Party != null) throw new InvalidOperationException("Leave your current party first."); });
        }
        public Task ChangeSettings(OnlineSettings settings)
        {
            var party = RequireParty(); string seed = Seed, key = Key();
            if (Content?.WorkshopBundle != null) {
                if (settings.Mode != Content.Mode.Id || settings.Format != Content.Mode.Format.ToString())
                    throw new InvalidOperationException("This workshop preview is bound to its selected mode. Leave this party and validate the new mode in the workshop before sharing it.");
                Content.WorkshopBundle.Descriptor.Bind(settings);
            } else _ = Resolve(settings, seed);
            return Mutation("Apply settings", ct => client.SetSettings(party.Id, party.Revision, settings, key, ct),
                json => Apply(json, seed), RequireLeader);
        }
        public Task SetReady(bool ready)
        {
            var party = RequireParty(); string key = Key(), seed = Seed;
            if (ready) RequireContent();
            return Mutation(ready ? "Ready" : "Unready", ct => client.Ready(party.Id, party.Revision, ready, key, ct),
                json => Apply(json, seed));
        }
        public Task Queue(string channel, string privateCode = null)
        {
            var party = RequireParty(); string key = Key(), seed = Seed;
            RequireContent();
            if (channel == "QuickPlay" && !PublicAvailable) throw new InvalidOperationException("Quick Play is unavailable for this unapproved course.");
            if (channel == "QuickPlay" && (party.Settings.Mode == "Freestyle" || party.Settings.Mode == "TimeRun") &&
                !party.Settings.Fill && party.Members.Count < 2)
                throw new InvalidOperationException("Invite another human player or turn Fill on for public co-op. Private rooms support solo play.");
            if (channel == "CustomFill" && !party.Settings.Fill) throw new InvalidOperationException("Custom Fill requires Fill on and separate map acceptance.");
            if (channel != "QuickPlay" && channel != "CustomFill" && channel != "PrivateCreate" && channel != "PrivateJoin")
                throw new ArgumentException("Choose a supported queue.");
            var binding = channel == "QuickPlay" ? Content.PublicBinding : channel == "PrivateCreate" ? Content.PrivateBinding : null;
            return Mutation("Enter queue", ct => client.Queue(party.Id, party.Revision, channel, key, binding, privateCode, ct),
                json => { customAccepted = null; accepted = null; Apply(json, seed); }, RequireLeader);
        }
        public Task CancelQueue()
        {
            var party = RequireParty(); string key = Key(), seed = Seed;
            return Mutation("Cancel queue", ct => client.Cancel(party.Id, party.Revision, key, ct),
                json => { accepted = null; Apply(json, seed); }, RequireLeader);
        }
        public Task LeaveParty()
        {
            var party = RequireParty(); string key = Key();
            return Mutation("Leave party", ct => client.Leave(party.Id, party.Revision, key, ct),
                _ => { Party = null; Content = null; stagedWorkshop = null; stagedPartyId = null; selectedSeed = null; launch = null; accepted = null; launched = null; },
                () => { if (MatchActive) throw new InvalidOperationException("Return from your match before leaving the party."); });
        }
        public Task StartPrivate() => Action("Start private match", async ct => {
            RequireLeader(); var expected = Expectation(RequireParty().Assignment);
            await EnsureAuthentication(ct).ConfigureAwait(false);
            await client.MatchAction(expected, "launch", ct).ConfigureAwait(false); Check(ct);
        }, false);
        public Task AcceptCustomContent() => Action("Accept custom map", ct => {
            var expected = Expectation(RequireParty().Assignment);
            customAccepted = expected.MatchId + ":" + expected.Generation + ":" + expected.RosterHash;
            return Task.CompletedTask;
        }, false);
        public Task ReturnReservation() => Action("Return to party", async ct => {
            var expected = Expectation(RequireParty().Assignment);
            await EnsureAuthentication(ct).ConfigureAwait(false);
            await client.MatchAction(expected, "return", ct).ConfigureAwait(false); Check(ct);
            NotifyReturned(expected.MatchId);
        }, false);
        public void NotifyReturned(string matchId)
        {
            lock (taskGate)
            {
                returnedMatches.Add(matchId); launch = null; MatchActive = false;
                queuedReturns.RemoveAll(match => match.MatchId == matchId);
            }
        }
        public void LeaveMatchLocally(OnlineMatchExpectation match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            lock (taskGate)
            {
                returnedMatches.Add(match.MatchId); launch = null; MatchActive = false;
                if (queuedReturns.All(queued => queued.MatchId != match.MatchId)) queuedReturns.Add(match);
            }
        }
        async Task SendQueuedReturns(CancellationToken ct)
        {
            OnlineMatchExpectation[] queued;
            lock (taskGate) queued = queuedReturns.ToArray();
            foreach (var match in queued)
            {
                try { await client.MatchAction(match, "return", ct).ConfigureAwait(false); }
                catch (OnlineServiceException error) when (error.Status >= 400 && error.Status < 500 &&
                    error.Status != 401 && error.Status != 408 && error.Status != 429) { }
                Check(ct);
                lock (taskGate) queuedReturns.Remove(match);
            }
        }
        public WordboundPartyLaunchContext TakeLaunch()
        {
            lock (taskGate) { var result = launch; launch = null; if (result != null) MatchActive = true; return result; }
        }
        public string CopyInvite() { RequireContent(); return WordboundPartyInvite.Encode(RequireParty().Id, Party.Invite, Seed); }
        WordboundPartyView RequireParty() => Party ?? throw new InvalidOperationException("Create or join a party first.");
        void RequireContent() {
            if (Content == null) throw new InvalidOperationException("This party's game content is unavailable. Wait for its map download or use the matching game release.");
            if (Content.WorkshopBundle != null && (AssignedWorkshop(Party) ?? Party.Workshop)?.BundleId != Content.WorkshopBundle.Descriptor.BundleId)
                throw new InvalidOperationException("Finish uploading the workshop map before becoming ready. Retry the pending sharing action.");
        }
        void RequireLeader() { if (!IsLeader) throw new InvalidOperationException("Only the party leader can do that."); }

        public Task Retry()
        {
            var action = pending;
            if (action == null) return Task.CompletedTask;
            return Action(pendingName, action, true, true);
        }
        Task Mutation(string name, Func<CancellationToken, Task<JsonNode>> send, Action<JsonNode> apply, Action validate = null)
        {
            return Action(name, async ct => {
                validate?.Invoke(); await EnsureAuthentication(ct).ConfigureAwait(false);
                await SendQueuedReturns(ct).ConfigureAwait(false); Check(ct);
                var json = await send(ct).ConfigureAwait(false); Check(ct);
                pending = null; pendingName = null;
                apply(json);
                await LoadContent(ct).ConfigureAwait(false); Check(ct);
            }, true);
        }
        Task Action(string name, Func<CancellationToken, Task> operation, bool retryable, bool retry = false)
        {
            if (pending != null && !retry) { Error = "Retry the pending action before starting another."; return Task.CompletedTask; }
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return Task.CompletedTask;
            return Run();
            async Task Run()
            {
                var ct = lifetime.Token;
                bool acquired = false;
                try
                {
                    await serial.WaitAsync(ct).ConfigureAwait(false); acquired = true;
                    Check(ct);
                    Error = null;
                    if (retryable) { pending = operation; pendingName = name; }
                    await operation(ct).ConfigureAwait(false); Check(ct);
                    pending = null; pendingName = null;
                }
                catch (Exception error)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        if (error is OnlineServiceException service && service.Status < 500 && service.Code != "CONTENT_UPLOAD_EXPIRED" ||
                            error is ArgumentException || error is FormatException || error is InvalidOperationException)
                        { pending = null; pendingName = null; }
                        Error = Friendly(error);
                    }
                }
                finally { if (acquired) serial.Release(); Volatile.Write(ref busy, 0); }
            }
        }
        public Task Poll()
        {
            lock (taskGate)
            {
                if (pollTask != null && !pollTask.IsCompleted) return pollTask;
                if (!Authenticated || Party == null || Busy || RetryRequired || disposed != 0) return Task.CompletedTask;
                pollTask = PollCore(); return pollTask;
            }
        }
        async Task PollCore()
        {
            var ct = lifetime.Token;
            bool acquired = false;
            try
            {
                await serial.WaitAsync(ct).ConfigureAwait(false); acquired = true;
                Check(ct); if (Busy || RetryRequired || Party == null) return;
                await EnsureAuthentication(ct).ConfigureAwait(false);
                if (MatchActive) return;
                await SendQueuedReturns(ct).ConfigureAwait(false); Check(ct);
                if (Error == QueuedReturnMessage) Error = null;
                var json = await client.Party(Party.Id, ct).ConfigureAwait(false); Check(ct);
                Apply(json, Seed);
                await LoadContent(ct).ConfigureAwait(false); Check(ct);
                if (!Busy) await ProcessAssignment(ct).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (!ct.IsCancellationRequested)
                    Error = ReturnQueued && !(error is OnlineServiceException) ? QueuedReturnMessage : Friendly(error);
            }
            finally { if (acquired) serial.Release(); }
        }
        async Task ProcessAssignment(CancellationToken ct)
        {
            var assignment = Party.Assignment;
            if (assignment == null || !assignment.Has("matchId")) return;
            string state = assignment["state"].String(), id = assignment["matchId"].String();
            lock (taskGate) if (returnedMatches.Contains(id)) return;
            if (state != "ADMITTING" && state != "STARTED") return;
            var expected = Expectation(assignment);
            string identity = id + ":" + expected.Generation + ":" + expected.RosterHash;
            if (Party.Channel == "CustomFill" && customAccepted != identity) return;
            if (state == "ADMITTING" && accepted != identity)
            {
                await client.MatchAction(expected, "admission", ct).ConfigureAwait(false);
                Check(ct); if (Busy) return;
                await client.AcceptMatch(expected, ct).ConfigureAwait(false); Check(ct); accepted = identity;
            }
            if (state != "STARTED" || launched == identity) return;
            var certificate = await client.MatchAction(expected, "certificate", ct).ConfigureAwait(false); Check(ct);
            var verified = verifier.VerifyStart(certificate["certificate"].String(), expected, clock() / 1000);
            if (verified.Roster.All(s => s.PlayerId != PlayerId)) throw new FormatException("Your authenticated seat is missing.");
            lock (taskGate)
            {
                Check(ct); if (returnedMatches.Contains(id)) return;
                if (Busy) return;
                launch = new WordboundPartyLaunchContext(client, verifier, verified, Content, token => FreshJoin(expected, token), ServerClock);
                launched = identity;
            }
        }
        OnlineMatchExpectation Expectation(JsonNode assignment)
        {
            if (assignment == null || !assignment.Has("matchId")) throw new InvalidOperationException("Waiting for a match assignment.");
            RequireContent();
            var settings = OnlineSettings.Read(assignment["settings"]);
            var content = OnlineContent.Read(assignment["content"]);
            if (settings.PoolKey != Party.Settings.PoolKey || !Content.Matches(content))
                throw new InvalidOperationException("This map differs from your installed course or seed. Cancel and use the host's exact invite and seed.");
            var seats = assignment["roster"].Items;
            if (seats.Count < 1 || seats.Count > 8 || seats.Count(s => s["playerId"].String() == PlayerId) != 1)
                throw new FormatException("The assigned roster is invalid.");
            string hash = OnlineJson.Hash(seats.Select(s => {
                WordboundPartyJson.Fields(s, "playerId", "partyId", "partyRevision", "attemptId", "teamId", "seat", "connectionSecretHash");
                return new Dictionary<string, object> {
                    ["playerId"] = WordboundPartyJson.Text(s, "playerId", WordboundPartyJson.Uuid), ["partyId"] = WordboundPartyJson.Text(s, "partyId", WordboundPartyJson.Uuid),
                    ["partyRevision"] = WordboundPartyJson.Text(s, "partyRevision", WordboundPartyJson.Counter), ["attemptId"] = WordboundPartyJson.Text(s, "attemptId", WordboundPartyJson.Uuid),
                    ["teamId"] = (int)WordboundPartyJson.Integer(s, "teamId", 0, 7), ["seat"] = (int)WordboundPartyJson.Integer(s, "seat", 0, 7),
                    ["connectionSecretHash"] = WordboundPartyJson.Text(s, "connectionSecretHash", WordboundPartyJson.HashPattern) };
            }).ToArray());
            if (assignment.Has("rosterHash") && hash != assignment["rosterHash"].String()) throw new FormatException("The assigned roster hash is invalid.");
            return new OnlineMatchExpectation(assignment["matchId"].String(), assignment["reservationGeneration"].String(), hash, settings, content);
        }
        async Task<WordboundFusionJoin> FreshJoin(OnlineMatchExpectation expected, CancellationToken caller)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, caller);
            var ct = linked.Token;
            lock (taskGate) if (returnedMatches.Contains(expected.MatchId)) throw new InvalidOperationException("You already returned from this match.");
            await EnsureAuthentication(ct, true).ConfigureAwait(false);
            var certificate = await client.MatchAction(expected, "certificate", ct).ConfigureAwait(false);
            var admission = await client.MatchAction(expected, "admission", ct).ConfigureAwait(false);
            Check(ct);
            var verified = verifier.VerifyStart(certificate["certificate"].String(), expected, clock() / 1000);
            verifier.VerifyAdmission(admission["grant"].String(), verified, PlayerId, admission["connectionSecret"].String(), clock() / 1000);
            var ticket = await client.PhotonTicket(ct).ConfigureAwait(false); Check(ct);
            if (ticket.AppId != WordboundClientTrust.ProductionAppId || ticket.ExpiresAtMilliseconds <= clock())
                throw new FormatException("The Photon authentication ticket is invalid.");
            return new WordboundFusionJoin(PlayerId, certificate["certificate"].String(), admission["grant"].String(),
                admission["connectionSecret"].String(), ticket);
        }
        public static string Friendly(Exception error)
        {
            if (error is OnlineServiceException service)
            {
                switch (service.Code)
                {
                    case "REVISION_CONFLICT": return "The party changed. Refresh and try again.";
                    case "PARTY_NOT_READY": return "Every party member must be ready.";
                    case "PUBLIC_COOP_REQUIRES_HUMANS": return "Invite another human player or turn Fill on for public co-op. Private rooms support solo play.";
                    case "CONTENT_NOT_APPROVED": return "This map is unavailable for public matchmaking. Leave the party and choose another public map. If all maps are unavailable, check for a game update.";
                    case "CONTENT_SETTINGS_MISMATCH": return "This map does not match the selected mode and team size. Leave the party and choose a matching map.";
                    case "CONTENT_UPLOAD_EXPIRED": return "The map upload expired. Retry the pending sharing action to upload it again.";
                    case "CONTENT_SOURCE_EXPIRED": return "This unqueued workshop map expired. Leave the party and share the validated draft again.";
                    case "AUTHORED_PRIVATE_CREATE_ONLY": return "Authored workshop parties use Create private room. Other parties can join with its code or Custom Fill.";
                    case "MATCH_NOT_FULL": return "Waiting for another team or more players. Fill off keeps an undersized team together.";
                    case "OWNER_REQUIRED": return "Only the private room creator can start this match.";
                    case "PRIVATE_SETTINGS_MISMATCH": return "Use the host's mode, team size, region and game release.";
                    case "RECONNECT_EXPIRED": return "The reconnect window ended. Cancel this reservation to play again.";
                    case "ALREADY_IN_PARTY": return "This guest already belongs to a party.";
                    case "INVALID_REFRESH": return "Guest reconnect expired. Reopen the menu to connect a new guest.";
                    default: return "The service could not complete this action (" + service.Code + ").";
                }
            }
            if (error is ArgumentException || error is InvalidOperationException) return error.Message;
            if (error is FormatException) return "The response or game content could not be verified.";
            if (error is OperationCanceledException) return "The request timed out. Retry the same pending action.";
            return "Connection interrupted. Retry the pending action without creating another reservation.";
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lifetime.Cancel(); client.ClearCredentials();
            lock (taskGate) launch = null;
        }
    }
}
