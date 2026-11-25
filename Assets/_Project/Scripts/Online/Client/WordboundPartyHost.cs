using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    public sealed class WordboundPartyHost : MonoBehaviour, IWordboundLobbyParty
    {
        public const float LeaveWaitSeconds = 15f;
        public const string PartyEnded = "Your party has ended.";
        public const string LeftForOtherPlay = "You left your party to play something else.";
        public const string MatchLost = "The match closed, so you left your party.";
        public const string MatchUnavailable = "The match could not open in this build, so you left your party.";
        WordboundPartyMenu menu;
        MonoBehaviour adopter;
        bool closing, sawParty, entering;
        float giveUpAt = -1;
        string closeReason;
        Func<bool> inHub = () => SceneManager.GetActiveScene().name == Session.HubScene;
        Action enterMatch = () => Session.Load(Session.WordboundScene);
        Func<float> clock = () => Time.unscaledTime;

        public static WordboundPartyHost Current => WordboundLobbyParty.Current as WordboundPartyHost;

        public WordboundPartyMenu Menu => menu;
        public bool Adopted => adopter;
        public bool Closing => closing || !menu;
        public bool ReadyForLobby => !Closing && menu.LobbyIdle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            WordboundLobbyParty.Register(FromLaunchAssets);
            LobbyServiceCheck.Register(CheckService);
        }

        static async Task CheckService(CancellationToken cancellation)
        {
            using var transport = new WordboundHttpTransport(new Uri(WordboundClientTrust.ProductionOrigin));
            await new OnlineCoordinatorClient(transport).Keys(cancellation);
        }

        static IWordboundLobbyParty FromLaunchAssets()
        {
            if (!Session.CanLoad(Session.WordboundScene)) return null;
            var assets = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            if (!assets || !assets.PublicTrust || !assets.SnapshotChunk.IsValid || !data || !data.items || !data.rules) return null;
            return Create(menu =>
            {
                menu.Theme = assets.UiTheme;
                menu.Initialize(assets.PublicTrust.text, data.items.text, data.rules.text, assets.MapPoolVersion, assets.PublicCoursesJson);
            });
        }

        public static WordboundPartyHost Create(Action<WordboundPartyMenu> initialize)
        {
            if (initialize == null) throw new ArgumentNullException(nameof(initialize));
            var root = new GameObject("Wordbound lobby party");
            if (Application.isPlaying) DontDestroyOnLoad(root);
            var host = root.AddComponent<WordboundPartyHost>();
            try
            {
                host.menu = root.AddComponent<WordboundPartyMenu>();
                host.menu.Visible = false;
                host.menu.HeldByLobby = true;
                initialize(host.menu);
                if (host.menu.Coordinator == null) throw new InvalidOperationException("The lobby party has no coordinator.");
            }
            catch
            {
                host.closing = true;
                Discard(root);
                throw;
            }
            host.menu.CloseRequested += host.MenuClosed;
            SceneManager.sceneLoaded += host.SceneLoaded;
            return host;
        }

        public bool Active => !Closing && menu.InFriendsParty;
        public bool Leader => Active && menu.Coordinator.IsLeader;
        public bool Searching => Active && (menu.LobbySearching || menu.LobbyFound);
        public bool Found => Active && menu.LobbyFound;
        public string Heading => Active ? menu.LobbyHeading : "";
        public string Status => Active ? menu.LobbyStatus : "";
        public string Problem => Active ? menu.LobbyProblem : null;
        public string SearchTitle => Active ? menu.LobbySearchLine : "";
        public IReadOnlyList<string> Members => Active ? menu.LobbyMembers : Array.Empty<string>();
        public bool Copied => Active && menu.LobbyCopied;
        public bool CanCopy => Active && menu.LobbyCanCopy;
        public bool CanLeave => Active && giveUpAt < 0 && menu.LobbyCanLeave;
        public bool CanFind => Active && !Adopted && menu.LobbyCanFind;
        public bool CanCancel => Active && !Adopted && menu.LobbyCanCancel;
        public bool CanRetry => Active && menu.LobbyCanRetry;

        public void Invite() { if (!Closing) menu.InviteFriends(); }
        public void Join(string code) { if (!Closing) menu.JoinFriendsParty(code); }
        public void Copy() { if (Active) menu.LobbyCopy(); }
        public void Leave() { if (CanLeave) LeaveAndClose(null); }
        public void FindMatch() { if (CanFind) menu.LobbyFind(); }
        public void Cancel() { if (CanCancel) menu.LobbyCancel(); }
        public void Retry() { if (CanRetry) menu.LobbyRetry(); }

        public bool TakeEntering()
        {
            bool result = entering && !Closing;
            entering = false;
            return result;
        }

        public WordboundPartyMenu Adopt(MonoBehaviour launcher)
        {
            if (Closing) throw new InvalidOperationException("The lobby party has closed.");
            if (!launcher) throw new ArgumentNullException(nameof(launcher));
            if (Adopted && adopter != launcher) throw new InvalidOperationException("Another scene already holds the lobby party.");
            adopter = launcher;
            entering = false;
            menu.HeldByLobby = false;
            menu.Visible = true;
            return menu;
        }

        public void Release(MonoBehaviour launcher)
        {
            if (!ReferenceEquals(adopter, launcher)) return;
            adopter = null;
            if (Closing) return;
            menu.BeginMatchRequested = null;
            menu.ReturnRequested = null;
            menu.LeaveRequested = null;
            menu.LeaveAvailable = null;
            menu.Visible = false;
            menu.HeldByLobby = true;
            if (menu.LobbyInMatch) Close(MatchLost);
        }

        public void LeaveAndClose(string reason)
        {
            if (Closing) return;
            closeReason = reason;
            if (menu.Coordinator.Party == null && !menu.LobbyWorking || menu.LobbyInMatch) { Close(reason); return; }
            if (giveUpAt < 0) giveUpAt = clock() + LeaveWaitSeconds;
            menu.LobbyLeave();
        }

        void Update() => Tick();

        internal void Tick()
        {
            if (Closing) return;
            if (giveUpAt >= 0 && clock() >= giveUpAt) { Close(closeReason); return; }
            var coordinator = menu.Coordinator;
            if (coordinator.Party != null) sawParty = true;
            if (Adopted || giveUpAt >= 0) return;
            menu.EndStoppedLobbySearch();
            if (!menu.InFriendsParty && !menu.LobbyWorking) { Close(closeReason); return; }
            if (coordinator.Party == null && !menu.LobbyWorking && !coordinator.RetryRequired && !menu.LobbyInMatch)
            {
                string problem = menu.LobbyProblem;
                if (sawParty) { Close(problem ?? PartyEnded); return; }
                if (problem != null) { Close(problem); return; }
            }
            if (!entering && menu.LobbyMatchDue && inHub()) Enter();
        }

        void Enter()
        {
            entering = true;
            try
            {
                var lobby = LobbyMenu.Instance;
                if (lobby) { Session.LobbyQueue = lobby.Queue; Session.LobbyMode = lobby.Mode; }
                enterMatch();
            }
            catch (Exception error)
            {
                entering = false;
                Debug.LogWarning("[Wreckabulary] The lobby party could not open the match scene: " + error.Message);
                LeaveAndClose(MatchUnavailable);
            }
        }

        void SceneLoaded(Scene next, LoadSceneMode mode)
        {
            if (Closing || mode != LoadSceneMode.Single) return;
            if (next.name == Session.HubScene) { entering = false; return; }
            if (next.name == Session.WordboundScene || Adopted || menu.LobbyInMatch) return;
            LeaveAndClose(LeftForOtherPlay);
        }

        void MenuClosed() => Close(closeReason);

        void Close(string reason)
        {
            if (closing) return;
            closing = true;
            SceneManager.sceneLoaded -= SceneLoaded;
            if (menu) menu.CloseRequested -= MenuClosed;
            WordboundLobbyParty.Closed(this, reason);
            Discard(gameObject);
        }

        static void Discard(GameObject root)
        {
            if (!root) return;
            if (Application.isPlaying) Destroy(root);
            else DestroyImmediate(root);
        }

        void OnDestroy()
        {
            if (closing) return;
            closing = true;
            SceneManager.sceneLoaded -= SceneLoaded;
            WordboundLobbyParty.Closed(this, null);
        }
    }
}
