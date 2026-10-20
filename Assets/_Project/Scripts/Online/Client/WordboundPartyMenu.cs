using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundPartyMenu : MonoBehaviour
    {
        static readonly string[] Modes = { "TimeRun", "TeamRace", "BrawlRace", "FreeForAll", "Freestyle" };
        static readonly string[] ModeLabels = { "Time Run", "Team Race", "Brawl Race", "Free-for-All", "Freestyle" };
        static readonly string[] Regions = { "asia", "eu", "us" };
        static readonly string[] Formats = { "Solo", "Duo", "Squad" };
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        WordboundHttpTransport transport;
        WordboundPartyCoordinator coordinator;
        WordboundPartyLaunchContext currentLaunch;
        WordboundPublicCourseCatalog publicCourses;
        string itemSource, ruleSource, privateSeed = "0123456789abcdef";
        string displayName = "Player", partyInvite = "", privateCode = "", seed = "0123456789abcdef";
        string localError, knownPartyRevision;
        int mode, region, format;
        bool fill, launching, returning, customFillConsent, customSeedMode;
        float nextPoll;
        Vector2 scroll;
        GUIStyle title, hint;

        public Func<WordboundPartyLaunchContext, CancellationToken, Task> BeginMatchRequested { get; set; }
        public Func<CancellationToken, Task> ReturnRequested { get; set; }
        public bool Visible { get; set; } = true;
        public event Action WorkshopRequested;
        public event Action CloseRequested;
        public WordboundPartyCoordinator Coordinator => coordinator;
        public string PhotonAppId { get; private set; }

        public void Initialize(string publicTrustJson, string actualItemsJson, string actualRuleBookJson,
            string mapPoolVersion, string approvedPublicContentJson = "[]")
        {
            if (coordinator != null) throw new InvalidOperationException("The party menu is already initialized.");
            var trust = new WordboundClientTrust(publicTrustJson);
            publicCourses = WordboundPublicCourseCatalog.Parse(approvedPublicContentJson);
            publicCourses.RequireRelease(mapPoolVersion);
            itemSource = actualItemsJson; ruleSource = actualRuleBookJson;
            customSeedMode = publicCourses.IsClosed;
            transport = new WordboundHttpTransport(trust.Origin);
            coordinator = new WordboundPartyCoordinator(new OnlineCoordinatorClient(transport), trust.Verifier,
                actualItemsJson, actualRuleBookJson, mapPoolVersion, new WordboundPublicCourseRegistry(approvedPublicContentJson));
            PhotonAppId = trust.PhotonAppId;
        }
        void Update()
        {
            if (coordinator == null || lifetime.IsCancellationRequested) return;
            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + 1.5f;
                _ = coordinator.Poll();
            }
            if (coordinator.Party != null && knownPartyRevision != coordinator.Party.Id + ":" + coordinator.Party.Revision)
            {
                knownPartyRevision = coordinator.Party.Id + ":" + coordinator.Party.Revision;
                var settings = coordinator.Party.Settings;
                mode = Array.IndexOf(Modes, settings.Mode); region = Array.IndexOf(Regions, settings.Region);
                format = Array.IndexOf(Formats, settings.Format); fill = settings.Fill; seed = coordinator.Seed ?? "0123456789abcdef";
            }
            if (!launching && BeginMatchRequested != null)
            {
                var launch = coordinator.TakeLaunch();
                if (launch != null) _ = Begin(launch);
            }
        }
        async Task Begin(WordboundPartyLaunchContext launch)
        {
            launching = true; currentLaunch = launch; localError = null;
            try { await BeginMatchRequested(launch, lifetime.Token); if (!lifetime.IsCancellationRequested && currentLaunch == launch) Visible = false; }
            catch (OperationCanceledException) { if (!lifetime.IsCancellationRequested && currentLaunch == launch) { Visible = true; localError = "Match startup was cancelled. Return to the party before trying again."; } }
            catch { if (!lifetime.IsCancellationRequested && currentLaunch == launch) { Visible = true; localError = "The match could not start. Return to your party, then cancel the old reservation."; } }
            finally { launching = false; }
        }
        public void NotifyReturned() => NotifyReturned(currentLaunch?.Verified.Match.MatchId);
        public void NotifyReturned(string confirmedMatchId)
        {
            if (lifetime.IsCancellationRequested) return;
            if (currentLaunch == null || currentLaunch.Verified.Match.MatchId != confirmedMatchId) return;
            coordinator?.NotifyReturned(confirmedMatchId);
            currentLaunch = null; localError = null; returning = false; nextPoll = 0; Visible = true;
        }
        public void ShowFailure(string safeMessage)
        {
            if (lifetime.IsCancellationRequested) return;
            Visible = true;
            localError = string.IsNullOrWhiteSpace(safeMessage) ? "The match stopped. Return to your party." : safeMessage;
        }
        async Task Return()
        {
            if (returning || launching) return;
            returning = true; localError = null;
            try
            {
                if (currentLaunch != null)
                {
                    var context = currentLaunch;
                    if (context.NoClientInitialized)
                    {
                        await context.ReturnWithoutClient(lifetime.Token);
                        if (!lifetime.IsCancellationRequested) NotifyReturned(context.Verified.Match.MatchId);
                    }
                    else
                    {
                        if (ReturnRequested == null) throw new InvalidOperationException("The gameplay return handler is not connected.");
                        await ReturnRequested(lifetime.Token);
                        if (!lifetime.IsCancellationRequested && currentLaunch != null)
                            localError = "Return is not confirmed yet. Retry Return to party.";
                    }
                }
                else await coordinator.ReturnReservation();
            }
            catch { if (!lifetime.IsCancellationRequested) localError = "Return could not finish. Retry Return to party."; }
            finally { returning = false; }
        }
        async void Invoke(Func<Task> action)
        {
            if (launching || returning || coordinator.Busy) return;
            localError = null;
            try { await action(); }
            catch (Exception error) { if (!lifetime.IsCancellationRequested) localError = WordboundPartyCoordinator.Friendly(error); }
        }
        void OnGUI()
        {
            if (coordinator == null || !Visible) return;
            if (coordinator.MatchActive && string.IsNullOrEmpty(localError)) return;
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            hint ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            float width = Mathf.Min(680, Screen.width - 24), height = Mathf.Min(850, Screen.height - 24);
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, 12, width, height), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("WORDBOUND", title);
            GUILayout.Label("Build words. Make tools. Bring your team.", hint);
            if (coordinator.MatchActive)
            {
                GUILayout.Label(localError ?? "Joining your match…", hint);
                GUI.enabled = !returning && !launching;
                if (GUILayout.Button(returning ? "Returning…" : "Return to party")) _ = Return();
                GUI.enabled = true; GUILayout.EndScrollView(); GUILayout.EndArea(); return;
            }
            GUI.enabled = !coordinator.Busy && !launching && !returning && !coordinator.RetryRequired;
            if (!coordinator.Authenticated)
            {
                GUILayout.Space(12); GUILayout.Label("Guest name");
                displayName = GUILayout.TextField(displayName, 32);
                if (GUILayout.Button("Connect")) Invoke(() => coordinator.Connect(displayName));
                GUILayout.Label("Your guest session stays in memory while this menu is open.", hint);
            }
            else
            {
                DrawSettings();
                if (coordinator.Party == null)
                {
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && (customSeedMode || publicCourses.Find(Modes[mode], Formats[format], seed) != null);
                    if (GUILayout.Button("Create party")) Invoke(CreateParty);
                    GUI.enabled = enabled;
                    GUILayout.Space(12); GUILayout.Label("Join a friend's party");
                    partyInvite = GUILayout.TextField(partyInvite, 160);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Paste invite")) partyInvite = GUIUtility.systemCopyBuffer;
                    if (GUILayout.Button("Join party")) Invoke(() => coordinator.JoinParty(partyInvite));
                    GUILayout.EndHorizontal();
                    GUILayout.Label("Have a private room invite? Use its settings before creating your party.", hint);
                    privateCode = GUILayout.TextField(privateCode, 220);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Paste room invite")) privateCode = GUIUtility.systemCopyBuffer;
                    if (GUILayout.Button("Use room settings")) Invoke(ApplyRoomSettings);
                    GUILayout.EndHorizontal();
                }
                else DrawParty();
            }
            GUI.enabled = true;
            if (coordinator.Busy) GUILayout.Label("Connecting…", hint);
            string error = localError ?? coordinator.Error;
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error, hint);
            if (coordinator.RetryRequired)
            {
                GUILayout.Label("The result of “" + coordinator.PendingAction + "” is not yet confirmed.", hint);
                GUI.enabled = !coordinator.Busy;
                if (GUILayout.Button("Retry same action")) Invoke(() => coordinator.Retry());
                GUI.enabled = true;
            }
            if (BeginMatchRequested == null)
                GUILayout.Label("Gameplay launcher is not connected in this scene.", hint);
            if (!coordinator.MatchActive && !coordinator.Busy && !coordinator.RetryRequired &&
                coordinator.Party?.HasAttempt != true && !launching && !returning)
            {
                GUILayout.Space(12); GUILayout.BeginHorizontal();
                if (WorkshopRequested != null && GUILayout.Button("Workshop")) WorkshopRequested();
                if (CloseRequested != null && GUILayout.Button("Back")) CloseRequested();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        void DrawSettings()
        {
            GUILayout.Space(10);
            bool previous = GUI.enabled;
            var party = coordinator.Party;
            GUI.enabled = previous && (party == null || coordinator.IsLeader && !party.HasAttempt);
            int nextMode = GUILayout.SelectionGrid(mode, ModeLabels, 3);
            if (nextMode != mode) { mode = nextMode; format = 0; fill = false; }
            GUILayout.Label("Region");
            region = GUILayout.SelectionGrid(region, new[] { "Asia", "Europe", "US" }, 3);
            if (Modes[mode] == "TeamRace" || Modes[mode] == "BrawlRace")
            {
                GUILayout.Label("Players per team");
                format = GUILayout.SelectionGrid(format, Formats, 3);
            }
            else format = 0;
            if (Modes[mode] == "TimeRun" || Modes[mode] == "TeamRace" || Modes[mode] == "BrawlRace")
            {
                fill = GUILayout.Toggle(fill, "Fill open team seats with other players");
                GUILayout.Label(fill ? "Human players can join your team before the match starts." :
                    "Only your party joins your team. Smaller teams are allowed.", hint);
            }
            else fill = false;
            if (party == null)
                DrawSeedSelection();
            else
            {
                GUILayout.Label("Map seed: " + coordinator.Seed);
                if (coordinator.IsLeader && GUILayout.Button("Apply settings · resets readiness"))
                    Invoke(() => coordinator.ChangeSettings(SelectedSettings()));
            }
            GUI.enabled = previous;
        }
        void DrawSeedSelection()
        {
            bool privateOnly = Modes[mode] == "Freestyle";
            if (privateOnly)
            {
                customSeedMode = true;
                GUILayout.Label("Freestyle uses private sessions.", hint);
            }
            else
            {
                bool nextCustom = GUILayout.SelectionGrid(customSeedMode ? 1 : 0,
                    new[] { "Approved maps", "Custom / private seed" }, 2) == 1;
                if (nextCustom != customSeedMode)
                {
                    customSeedMode = nextCustom;
                    if (customSeedMode) seed = privateSeed;
                }
            }
            if (customSeedMode)
            {
                GUILayout.Label("Private map seed");
                privateSeed = GUILayout.TextField(privateSeed, 16).ToLowerInvariant();
                if (GUILayout.Button("New private seed")) privateSeed = Guid.NewGuid().ToString("N").Substring(0, 16);
                seed = privateSeed;
                if (!privateOnly) GUILayout.Label("Custom seeds use private rooms. Quick Play requires an approved map.", hint);
                return;
            }
            var choices = publicCourses.Choices(Modes[mode], Formats[format]);
            if (choices.Count == 0)
            {
                GUILayout.Label("No approved public map for this mode yet. Choose a custom/private seed to create a private room.", hint);
                return;
            }
            int selected = -1;
            for (int i = 0; i < choices.Count; i++) if (choices[i].Seed == seed) selected = i;
            int choice = GUILayout.SelectionGrid(selected,
                choices.Select((course, i) => "Map " + (i + 1) + " · " + course.Seed).ToArray(), 1);
            if (choice >= 0) seed = choices[choice].Seed;
            if (choices.Count > 1 && GUILayout.Button("Random approved map"))
                seed = choices[UnityEngine.Random.Range(0, choices.Count)].Seed;
            if (choice < 0) GUILayout.Label("Select an approved map before creating the party.", hint);
            else GUILayout.Label("This exact map and seed will be shared with your party.", hint);
        }
        Task CreateParty()
        {
            if (!customSeedMode)
                _ = publicCourses.ResolveSelection(itemSource, ruleSource, Modes[mode], Formats[format], seed);
            return coordinator.CreateParty(SelectedSettings(), seed);
        }
        OnlineSettings SelectedSettings() => coordinator.Settings(Modes[mode], Formats[format], fill, Regions[region]);
        void DrawParty()
        {
            var party = coordinator.Party;
            if (party == null) return;
            GUILayout.Space(12); GUILayout.Label("Your party · " + party.Members.Count + "/" + Math.Min(4, party.Settings.TeamCapacity));
            foreach (var member in party.Members)
                GUILayout.Label((member.PlayerId == coordinator.PlayerId ? "You" : "Player " + member.PlayerId.Substring(0, 6)) +
                    (member.PlayerId == party.LeaderId ? " · leader" : "") + (member.Ready ? " · ready" : " · not ready"));
            GUILayout.BeginHorizontal();
            bool canAct = GUI.enabled;
            GUI.enabled = canAct && coordinator.Content != null;
            if (GUILayout.Button("Copy party invite")) Invoke(() => {
                GUIUtility.systemCopyBuffer = coordinator.CopyInvite(); return Task.CompletedTask;
            });
            GUI.enabled = canAct;
            bool ready = party.Members.Single(m => m.PlayerId == coordinator.PlayerId).Ready;
            if (!party.HasAttempt && GUILayout.Button(ready ? "Unready" : "Ready")) Invoke(() => coordinator.SetReady(!ready));
            GUILayout.EndHorizontal();
            var assignment = party.Assignment;
            if (assignment != null)
            {
                GUILayout.Space(8); GUILayout.Label("Match: " + assignment["state"].String());
                if (assignment.Has("privateCode"))
                {
                    string code = assignment["privateCode"].String();
                    GUILayout.Label("Private code: " + code);
                    GUI.enabled = canAct && coordinator.Content != null;
                    if (GUILayout.Button("Copy code and match settings"))
                        Invoke(() => { GUIUtility.systemCopyBuffer = WordboundRoomInvite.Encode(code, party.Settings, coordinator.Seed); return Task.CompletedTask; });
                    GUI.enabled = canAct;
                }
                if (assignment.Has("roster"))
                    foreach (var seat in assignment["roster"].Items)
                        GUILayout.Label("Team " + (seat["teamId"].Int() + 1) + " · " +
                            (seat["playerId"].String() == coordinator.PlayerId ? "You" : "Player " + seat["playerId"].String().Substring(0, 6)));
                if (party.Channel == "CustomFill" && assignment.Has("matchId"))
                {
                    GUILayout.Label("Custom Fill uses the host's map. Accept only when your installed map and seed match.", hint);
                    if (GUILayout.Button("Verify and accept custom map")) Invoke(() => coordinator.AcceptCustomContent());
                }
                if (coordinator.IsLeader && party.Channel == "PrivateCreate" && assignment["state"].String() == "FORMING" && assignment.Has("privateCode"))
                    if (GUILayout.Button("Start private match")) Invoke(() => coordinator.StartPrivate());
                if (assignment["state"].String() == "STARTED")
                    if (GUILayout.Button("Return from reservation")) _ = Return();
            }
            if (coordinator.IsLeader && !party.HasAttempt)
            {
                GUILayout.Space(12); GUILayout.Label("Play");
                if (GUILayout.Button("Create private room")) Invoke(() => coordinator.Queue("PrivateCreate"));
                GUILayout.Label("Join private room · paste a shared code and settings");
                privateCode = GUILayout.TextField(privateCode, 220);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Paste room invite")) privateCode = GUIUtility.systemCopyBuffer;
                if (GUILayout.Button("Join private room")) Invoke(JoinRoom);
                GUILayout.EndHorizontal();
                bool previous = GUI.enabled;
                GUI.enabled = previous && coordinator.PublicAvailable;
                if (GUILayout.Button("Quick Play")) Invoke(() => coordinator.Queue("QuickPlay"));
                GUI.enabled = previous;
                if (!coordinator.PublicAvailable)
                    GUILayout.Label(party.Settings.Mode == "Freestyle" ? "Freestyle uses private sessions." :
                        publicCourses.Choices(party.Settings.Mode, party.Settings.Format).Count == 0 ?
                        "Quick Play has no approved maps for this mode yet. Use a private room." :
                        "This seed is for private rooms. To use Quick Play, leave the party and choose an approved map.", hint);
                if (party.Settings.Fill)
                {
                    customFillConsent = GUILayout.Toggle(customFillConsent, "Also search private custom rooms that allow Fill");
                    GUI.enabled = previous && customFillConsent;
                    if (GUILayout.Button("Find custom Fill room")) Invoke(() => coordinator.Queue("CustomFill"));
                    GUI.enabled = previous;
                }
            }
            GUILayout.Space(12); GUILayout.BeginHorizontal();
            if (coordinator.IsLeader && party.HasAttempt && GUILayout.Button("Cancel queue / reservation"))
                Invoke(() => coordinator.CancelQueue());
            if (GUILayout.Button("Leave party")) Invoke(() => coordinator.LeaveParty());
            GUILayout.EndHorizontal();
        }
        Task JoinRoom()
        {
            string code = privateCode.Trim();
            if (code.StartsWith("wb-room:", StringComparison.Ordinal))
            {
                var invite = new WordboundRoomInvite(code);
                invite.RequireCompatible(coordinator.Party.Settings, coordinator.Seed);
                code = invite.Code;
            }
            return coordinator.Queue("PrivateJoin", code);
        }
        Task ApplyRoomSettings()
        {
            var invite = new WordboundRoomInvite(privateCode);
            if (invite.PoolVersion != coordinator.PoolVersion) throw new InvalidOperationException("The room uses a different map release.");
            mode = Array.IndexOf(Modes, invite.Mode); region = Array.IndexOf(Regions, invite.Region);
            format = Array.IndexOf(Formats, invite.Format); seed = invite.Seed; fill = false;
            privateSeed = invite.Seed; customSeedMode = true;
            return Task.CompletedTask;
        }
        void OnDestroy()
        {
            lifetime.Cancel(); coordinator?.Dispose(); transport?.Dispose();
        }
    }

    public sealed class WordboundRoomInvite
    {
        public string Code { get; }
        public string Seed { get; }
        public string Mode { get; }
        public string Format { get; }
        public string Region { get; }
        public string PoolVersion { get; }
        public WordboundRoomInvite(string text)
        {
            var parts = (text ?? "").Trim().Split(':');
            if (parts.Length != 8 || parts[0] != "wb-room" ||
                !System.Text.RegularExpressions.Regex.IsMatch(parts[1], "^[a-f0-9]{16}$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(parts[6], "^[a-f0-9]{16}$"))
                throw new ArgumentException("Paste a complete private room invite.");
            Code = parts[1]; Mode = parts[2]; Format = parts[3]; Region = parts[4]; Seed = parts[6]; PoolVersion = parts[7];
            _ = new OnlineSettings(Mode, Format, false, Region, parts[5], PoolVersion);
            if (parts[5] != MatchSnapshotCodec.ProtocolVersion) throw new ArgumentException("The room uses a different game release.");
        }
        public static string Encode(string code, OnlineSettings settings, string seed)
        {
            string value = string.Join(":", "wb-room", code, settings.Mode, settings.Format, settings.Region,
                settings.ProtocolVersion, seed, settings.MapPoolVersion);
            _ = new WordboundRoomInvite(value); return value;
        }
        public void RequireCompatible(OnlineSettings settings, string seed)
        {
            if (Mode != settings.Mode || Format != settings.Format || Region != settings.Region ||
                PoolVersion != settings.MapPoolVersion || Seed != seed)
                throw new InvalidOperationException("Match the room's " + Mode + " / " + Format + " / " + Region +
                    " settings and seed " + Seed + " before joining. Create a new party to change its seed.");
        }
    }
}
