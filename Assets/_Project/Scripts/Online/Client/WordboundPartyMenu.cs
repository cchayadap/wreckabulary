using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu : MonoBehaviour
    {
        static readonly string[] Modes = { "TimeRun", "TeamRace", "BrawlRace", "FreeForAll", "Freestyle" };
        static readonly string[] ModeLabels = { "Time Run", "Team Race", "Brawl Race", "Free-for-All", "Freestyle" };
        static readonly string[] Regions = { "asia", "eu", "us" };
        static readonly string[] RegionLabels = { "Asia", "Europe", "US" };
        static readonly string[] MapSourceLabels = { "Public maps", "Private seed" };
        static readonly string[] Formats = { "Solo", "Duo", "Squad" };
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly WordboundUiStyles ui = new WordboundUiStyles();
        public WordboundUiTheme Theme;
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
        Rect partyRect;
        bool compactPartyLayout, compactPartyScroll;
        Rect guestFirstContentRect, guestLastContentRect;
        float guestHeight = -1, guestWidth = -1;
        int guestStyleGeneration = -1;
        IReadOnlyList<WordboundPublicCourse> mapChoices;
        string[] mapLabels;
        int cachedMode = -1, cachedFormat = -1;

        public Func<WordboundPartyLaunchContext, CancellationToken, Task> BeginMatchRequested { get; set; }
        public Func<CancellationToken, Task> ReturnRequested { get; set; }
        public bool Visible { get; set; } = true;
        public event Action WorkshopRequested;
        public event Action CloseRequested;
        public WordboundPartyCoordinator Coordinator => coordinator;
#if UNITY_EDITOR || GSK_QA
        public int QaCompletedRepaintCount { get; private set; }
        public int QaLastRepaintFrame { get; private set; } = -1;
#endif
        public string PhotonAppId { get; private set; }
        public string SelectedRegion => Regions[region];
        public bool SelectedFill => fill;
        public bool CanOpenAuthoredPrivate => coordinator != null && coordinator.Authenticated && coordinator.Party == null &&
            !coordinator.Busy && !coordinator.RetryRequired && !launching && !returning;
        public async Task OpenAuthoredPrivate(WordboundClientContent content, string selectedRegion, bool fillParty)
        {
            if (coordinator == null || !coordinator.Authenticated) throw new InvalidOperationException("Connect with a guest name before sharing your workshop map.");
            if (launching || returning || coordinator.Busy || coordinator.Party != null)
                throw new InvalidOperationException("Finish the pending action and leave your current party before sharing a new map.");
            if (content?.WorkshopBundle == null) throw new ArgumentException("Provide the exact validated workshop draft and resolved preview.");
            var settings = coordinator.Settings(content.Mode.Id, content.Mode.Format.ToString(), content.Mode.SupportsFill && fillParty, selectedRegion);
            customSeedMode = true; seed = privateSeed = content.Seed;
            await coordinator.CreateAuthoredParty(settings, content);
            if (!string.IsNullOrEmpty(coordinator.Error)) throw new InvalidOperationException(coordinator.Error);
            Visible = true;
        }

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
            ProcessPartyInput(Application.isFocused);
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
            BeginPartyGui(Application.isFocused);
            try { DrawPartyGui(new Vector2(Screen.width, Screen.height)); }
            finally { EndPartyGui(); }
#if UNITY_EDITOR || GSK_QA
            if (Event.current.type == EventType.Repaint && QaLastRepaintFrame != Time.frameCount)
            {
                QaLastRepaintFrame = Time.frameCount;
                QaCompletedRepaintCount++;
            }
#endif
        }
        void DrawPartyGui(Vector2 viewport)
        {
            using var themeScope = ui.Apply(Theme);
            title = ui.Heading; hint = ui.Wrapped;
            DrawOnlineBackground(viewport);
            Rect available = ui.MenuRect(viewport.x, viewport.y);
            if (Event.current.type == EventType.Layout || partyRect.width == 0)
            {
                if (guestStyleGeneration != ui.Generation || guestWidth != available.width)
                { guestHeight = -1; guestWidth = available.width; guestStyleGeneration = ui.Generation; }
                compactPartyLayout = !coordinator.MatchActive && (!coordinator.Authenticated || coordinator.RetryRequired ||
                    !(coordinator.Party == null ? entryPage == 0 && showCreateSettings : showPartySettings || showPrivateActions));
                compactPartyScroll = compactPartyLayout && guestHeight > available.height;
                partyRect = available;
                if (compactPartyLayout && guestHeight > 0)
                    partyRect.height = Mathf.Clamp(guestHeight, ui.ChromeHeight, available.height);
                if (compactPartyLayout && !compactPartyScroll) scroll = Vector2.zero;
            }
            GUILayout.BeginArea(partyRect, ui.Panel);
            bool scrolling = !compactPartyLayout || compactPartyScroll;
            if (scrolling) scroll = GUILayout.BeginScrollView(scroll, false, compactPartyLayout);
            if (compactPartyLayout) GUILayout.BeginVertical(GUILayout.ExpandHeight(false));
            DrawPartyContents();
            if (compactPartyLayout)
            {
                if (Event.current.type == EventType.Repaint) guestLastContentRect = GUILayoutUtility.GetLastRect();
                GUILayout.EndVertical();
                if (Event.current.type == EventType.Repaint)
                {
                    Rect content = GUILayoutUtility.GetLastRect();
                    float bottom = Mathf.Max(content.yMax, guestLastContentRect.yMax);
                    guestHeight = Mathf.Ceil(scrolling ? bottom - content.yMin + ui.Panel.padding.vertical +
                        GUI.skin.scrollView.margin.vertical + GUI.skin.scrollView.padding.vertical : bottom + ui.Panel.padding.bottom);
                }
            }
            if (scrolling) GUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) partyViewportHeight = scrolling ?
                Mathf.Max(60, GUILayoutUtility.GetLastRect().height - 16) : Mathf.Max(1, partyRect.height - ui.Panel.padding.vertical);
            GUILayout.EndArea();
        }
        void DrawPartyContents()
        {
            GUILayout.Label("ONLINE PLAY", title);
            if (compactPartyLayout && Event.current.type == EventType.Repaint) guestFirstContentRect = GUILayoutUtility.GetLastRect();
            GUILayout.Label("Wordbound · Human players", hint);
            if (partyUsesPad) GUILayout.Label("D-pad Select · A Confirm · B Back · Right stick Scroll", hint);
            if (coordinator.MatchActive)
            {
                GUILayout.Label(localError ?? "Joining your match…", hint);
                GUI.enabled = !returning && !launching;
                PartyControl("return", returning ? "Returning…" : "Return to party", () => { _ = Return(); });
                GUI.enabled = true; return;
            }
            if (coordinator.RetryRequired)
            {
                DrawRetryState();
                return;
            }
            GUI.enabled = !coordinator.Busy && !launching && !returning;
            if (!coordinator.Authenticated)
            {
                GUILayout.Space(12); GUILayout.Label("Guest name");
                displayName = PartyText("name", displayName, 32);
                PartyControl("connect", "Connect", () => Invoke(() => coordinator.Connect(displayName)));
                if (partyUsesPad) GUILayout.Label("Keyboard: edit your name.", hint);
            }
            else
            {
                if (coordinator.Party == null)
                {
                    PartyChoices("entry", entryPage, EntryLabels, 2, next => { entryPage = next; scroll = Vector2.zero; });
                    if (entryPage == 0)
                    {
                        DrawCreateSettings();
                        bool enabled = GUI.enabled;
                        GUI.enabled = enabled && (customSeedMode || publicCourses.Find(Modes[mode], Formats[format], seed) != null);
                        PartyControl("create-party", "Create party", () => Invoke(CreateParty));
                        GUI.enabled = enabled;
                    }
                    else DrawJoinForms();
                }
                else
                {
                    if (!coordinator.Party.HasAttempt)
                    {
                        PartyControl("party-settings", showPartySettings ? "Hide match settings" : "Match settings",
                            () => showPartySettings = !showPartySettings);
                        if (showPartySettings) DrawSettings();
                    }
                    DrawParty();
                }
            }
            GUI.enabled = true;
            if (coordinator.Busy) GUILayout.Label("Connecting…", hint);
            DrawPartyError();
            if (BeginMatchRequested == null)
                GUILayout.Label("Gameplay launcher is not connected in this scene.", hint);
            if (!coordinator.MatchActive && !coordinator.Busy && !coordinator.RetryRequired &&
                coordinator.Party?.HasAttempt != true && !launching && !returning)
            {
                GUILayout.Space(12); GUILayout.BeginHorizontal();
                if (WorkshopRequested != null) PartyControl("workshop", "Workshop", () => WorkshopRequested?.Invoke());
                if (CloseRequested != null) PartyControl("back", "Back", () => CloseRequested?.Invoke());
                GUILayout.EndHorizontal();
            }
        }
        void DrawSettings(bool includeMode = true)
        {
            GUILayout.Space(10);
            bool previous = GUI.enabled;
            var party = coordinator.Party;
            GUI.enabled = previous && (party == null || coordinator.IsLeader && !party.HasAttempt);
            if (includeMode) DrawModeChoices();
            GUILayout.Label("Region");
            PartyChoices("region", region, RegionLabels, 3, next => region = next);
            if (Modes[mode] == "TeamRace" || Modes[mode] == "BrawlRace")
            {
                GUILayout.Label("Players per team");
                PartyChoices("format", format, Formats, 3, next => format = next);
            }
            else format = 0;
            if (Modes[mode] != "FreeForAll")
            {
                PartyControl("fill", "Find human teammates", () => fill = !fill, true, fill);
                GUILayout.Label(fill ? "Open seats may be filled before play." : "Your party only · smaller teams allowed.", hint);
            }
            else fill = false;
            if (party == null)
                DrawSeedSelection();
            else
            {
                GUILayout.Label("Map: " + (coordinator.PublicAvailable ? "Public layout" : "Private layout"));
                if (coordinator.IsLeader) PartyControl("apply-settings", "Apply settings · ready resets",
                    () => Invoke(() => coordinator.ChangeSettings(SelectedSettings())));
            }
            GUI.enabled = previous;
        }
        void DrawSeedSelection()
        {
            {
                PartyChoices("map-source", customSeedMode ? 1 : 0, MapSourceLabels, 2, next => {
                    bool nextCustom = next == 1;
                    if (nextCustom != customSeedMode) { customSeedMode = nextCustom; if (customSeedMode) seed = privateSeed; }
                });
            }
            if (customSeedMode)
            {
                GUILayout.Label("Private map seed");
                privateSeed = PartyText("seed", privateSeed, 16).ToLowerInvariant();
                PartyControl("new-seed", "New private seed", () => privateSeed = Guid.NewGuid().ToString("N").Substring(0, 16));
                seed = privateSeed;
                GUILayout.Label("Private rooms only. Use Public maps to find a match.", hint);
                return;
            }
            int selected = EnsurePublicMapSelection();
            var choices = mapChoices;
            if (choices.Count == 0)
            {
                GUILayout.Label("No public map available. Choose Private seed.", hint);
                return;
            }
            PartyChoices("public-map", selected, mapLabels, 2, choice => seed = choices[choice].Seed);
            if (choices.Count > 1) PartyControl("random-map", "Shuffle layout",
                () => seed = choices[UnityEngine.Random.Range(0, choices.Count)].Seed);
        }
        void RefreshMapChoices()
        {
            if (mapChoices != null && cachedMode == mode && cachedFormat == format) return;
            mapChoices = publicCourses.Choices(Modes[mode], Formats[format]);
            mapLabels = new string[mapChoices.Count];
            for (int i = 0; i < mapChoices.Count; i++) mapLabels[i] = "Layout " + (i + 1);
            cachedMode = mode; cachedFormat = format;
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
            GUILayout.Space(ui.Gap); GUILayout.Label("Party · " + party.Members.Count + "/" + Math.Min(4, party.Settings.TeamCapacity), title);
            GUILayout.Label(Composition(party.Settings.Mode, party.Settings.Format) + " · " +
                RegionLabels[Array.IndexOf(Regions, party.Settings.Region)], hint);
            if (party.HasAttempt && party.Assignment == null)
                GUILayout.Label(party.Channel == "PrivateJoin" ? "Joining room…" :
                    party.Channel == "PrivateCreate" ? "Creating room…" : "Finding players…", hint);
            if (coordinator.IsLeader && party.HasAttempt)
                PartyControl("cancel-queue", party.Assignment == null ? "Cancel search" : "Cancel reservation", () => Invoke(() => coordinator.CancelQueue()));
            foreach (var member in party.Members)
                GUILayout.Label((member.PlayerId == coordinator.PlayerId ? "You" : "Player " + member.PlayerId.Substring(0, 6)) +
                    (member.PlayerId == party.LeaderId ? " · leader" : "") + (member.Ready ? " · ready" : " · not ready"));
            GUILayout.BeginHorizontal();
            bool canAct = GUI.enabled;
            GUI.enabled = canAct && coordinator.Content != null;
            PartyControl("copy-party", "Copy party invite", () => Invoke(() => {
                GUIUtility.systemCopyBuffer = coordinator.CopyInvite(); return Task.CompletedTask;
            }));
            GUI.enabled = canAct;
            bool ready = party.Members.Single(m => m.PlayerId == coordinator.PlayerId).Ready;
            if (!party.HasAttempt) PartyControl("ready", ready ? "Unready" : "Ready", () => Invoke(() => coordinator.SetReady(!ready)));
            GUILayout.EndHorizontal();
            var assignment = party.Assignment;
            if (assignment != null)
            {
                GUILayout.Space(ui.Gap); GUILayout.Label(MatchStatus(assignment["state"].String(), party.Channel), title);
                if (assignment.Has("privateCode"))
                {
                    string code = assignment["privateCode"].String();
                    GUILayout.Label("Private code: " + code);
                    GUI.enabled = canAct && coordinator.Content != null;
                    PartyControl("copy-room", "Copy room invite", () =>
                        Invoke(() => { GUIUtility.systemCopyBuffer = WordboundRoomInvite.Encode(code, party.Settings, coordinator.Seed); return Task.CompletedTask; }));
                    GUI.enabled = canAct;
                }
                if (assignment.Has("roster"))
                    foreach (var seat in assignment["roster"].Items)
                        GUILayout.Label("Team " + (seat["teamId"].Int() + 1) + " · " +
                            (seat["playerId"].String() == coordinator.PlayerId ? "You" : "Player " + seat["playerId"].String().Substring(0, 6)));
                if (party.Channel == "CustomFill" && assignment.Has("matchId"))
                {
                    GUILayout.Label("Host map · verify your installed map and seed before joining.", hint);
                    PartyControl("accept-custom", "Verify and accept map", () => Invoke(() => coordinator.AcceptCustomContent()));
                }
                if (coordinator.IsLeader && party.Channel == "PrivateCreate" && assignment["state"].String() == "FORMING" && assignment.Has("privateCode"))
                    PartyControl("start-private", "Start private match", () => Invoke(() => coordinator.StartPrivate()));
                if (assignment["state"].String() == "STARTED")
                    PartyControl("return-reservation", "Return from reservation", () => { _ = Return(); });
            }
            if (coordinator.IsLeader && !party.HasAttempt)
            {
                GUILayout.Space(ui.Gap);
                bool previous = GUI.enabled;
                GUI.enabled = previous && coordinator.PublicAvailable;
                PartyControl("quick-play", "Find match", () => Invoke(() => coordinator.Queue("QuickPlay")));
                GUI.enabled = previous;
                if (!coordinator.PublicAvailable)
                    GUILayout.Label(publicCourses.Choices(party.Settings.Mode, party.Settings.Format).Count == 0 ?
                        "No public map available. Use a private room." :
                        "Private layout. Leave this party and choose Public maps to find a match.", hint);
                PartyControl("private-actions", showPrivateActions ? "Hide private rooms" : "Private rooms",
                    () => showPrivateActions = !showPrivateActions);
                if (showPrivateActions) DrawPrivateActions(previous);
            }
            GUILayout.Space(12); GUILayout.BeginHorizontal();
            PartyControl("leave-party", "Leave party", () => Invoke(() => coordinator.LeaveParty()));
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
            privateSeed = invite.Seed; customSeedMode = true; entryPage = 0; scroll = Vector2.zero;
            return Task.CompletedTask;
        }
        void OnDestroy()
        {
            try
            {
                partyInput?.Dispose();
                lifetime.Cancel(); coordinator?.Dispose(); transport?.Dispose();
            }
            finally { ui.Dispose(); }
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
