using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public enum Phase { Lobby, Countdown, Playing, RoundOver, MatchOver, Warmup, Showcase }

    public class RoundManager : MonoBehaviour
    {
        public const float BeatScale = .35f, BeatSeconds = .5f;
        const string FirstMove = "Break furniture → collect letters → spell items.";
        public static RoundManager Instance { get; private set; }
        [SerializeField] PlayerJoinManager joins;
        [SerializeField] RoomBuilder room;
        [SerializeField] DeliverySpawner deliveries;
        [SerializeField] GameHud hud;
        [SerializeField] float countdownTime = 3f;
        MatchScore score;
        ClearOutController clearOut;
        float phaseElapsed, roundStarted;
        PlayerController lastWinner;
        string joinLine, timerLine;
        int joinVersion = -1, timerSeconds = -1;
        HouseModeContext directed;
        bool warmupAsked, hosted, beats;
        float beatRestore = 1f;
        public static bool WarmupRequested { get; set; }
        public static bool FinishBeatsRequested { get; set; }
        public bool FinishBeats => beats;
        public bool FinishBeatPlaying { get; private set; }
        public HouseWarmup Warmup { get; private set; }
        public HouseShowcase Showcase { get; private set; }
        public float ShowcaseTime { get; set; } = MatchShowcase.DefaultSeconds;
        public Phase Phase { get; private set; } = Phase.Lobby;
        public bool Directed => directed != null;
        public IHouseWordGoal WordGoal { get; set; }
        public PlayerController LastWinner => lastWinner;
        public RoundOutcome LastOutcome { get; private set; }
        public int Round { get; private set; }
        public float CountdownTime { get => countdownTime; set => countdownTime = value; }
        public int WinsOf(PlayerController p) => p ? score?.Wins(p.Team) ?? 0 : 0;
        public float TimeLeft => Mathf.Max(0f, Match.Rules.RoundTimeLimitSeconds - (Time.time - roundStarted));
        public event Action<PlayerController> RoundWon;
        public event Action<PlayerController> MatchWon;
        public event Action CollapseStarted;
        IReadOnlyList<PlayerController> Players => joins.Players;
        float PhaseTime => phaseElapsed;
        bool AcceptsStartInput => !hud || !hud.StateController ||
            (hud.StateController.CurrentState == UIState.GameplayHUD && !hud.StateController.IsTransitioning);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetWarmupRequest()
        {
            WarmupRequested = false;
            FinishBeatsRequested = false;
            SceneManager.sceneLoaded -= DropWarmupRequest;
            SceneManager.sceneLoaded += DropWarmupRequest;
        }

        static void DropWarmupRequest(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || FindAnyObjectByType<RoundManager>()) return;
            WarmupRequested = false;
            FinishBeatsRequested = false;
        }

        void Awake()
        {
            Instance = this;
            warmupAsked = WarmupRequested;
            beats = WarmupRequested || FinishBeatsRequested;
            WarmupRequested = false;
            FinishBeatsRequested = false;
        }

        internal static RoundManager Host(GameObject owner, PlayerJoinManager players, GameHud display)
        {
            var rounds = owner.AddComponent<RoundManager>();
            rounds.hosted = true;
            rounds.joins = players;
            rounds.hud = display;
            rounds.enabled = false;
            return rounds;
        }

        internal void EnterCrewPhase(Phase phase)
        {
            Phase = phase;
            phaseElapsed = 0f;
            enabled = phase == Phase.Warmup || phase == Phase.Showcase;
        }

        void Start()
        {
            if (hosted) return;
            if (!joins) joins = GetComponent<PlayerJoinManager>();
            if (!room) room = GetComponent<RoomBuilder>();
            if (!deliveries) deliveries = GetComponent<DeliverySpawner>();
            if (!hud) hud = FindAnyObjectByType<GameHud>();
            var context = new HouseModeContext(this, joins, room, deliveries, hud);
            if (HouseModeDirectors.TryAttach(context))
            {
                directed = context;
                return;
            }
            clearOut = gameObject.AddComponent<ClearOutController>();
            clearOut.Configure(room.Layout, Match.Mode);
            clearOut.ClosureStarted += () => CollapseStarted?.Invoke();
            deliveries.Layout = room.Layout;
            deliveries.RoomOpen = r => !clearOut.Running || clearOut.Schedule.PhaseOf(r, clearOut.Elapsed) == RoomPhase.Safe;
            EnterLobby();
            if (joins.RestoredFromSession && joins.HumanCount > 0) StartMatch();
        }

        void OnDestroy()
        {
            CloseWarmup();
            CloseShowcase();
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void SetPhase(Phase phase)
        {
            if (phase == Phase.Countdown || phase == Phase.Lobby) FinishBeatPlaying = false;
            if (phase != Phase.Playing) LetterInventory.ClearAllBonusRows();
            Phase = phase;
            phaseElapsed = 0f;
            joins.RespawnKnockedOut = phase == Phase.Lobby;
        }

        void Update()
        {
            if (Time.timeScale <= 0f || Directed || hosted) return;
            float step = FrameStep;
            phaseElapsed += step;
            switch (Phase)
            {
                case Phase.Warmup:
                    if (TickWarmup(step)) EndWarmup();
                    break;
                case Phase.Showcase:
                    if (TickShowcase(step)) EndShowcase();
                    break;
                case Phase.Lobby:
                    hud.SetInstruction(joins.HumanCount == 0 ? JoinLine() : "Press START — solo seats get AI opponents", "");
                    hud.SetTimer("");
                    hud.ShowModeActions(joins.HumanCount > 0, "START WITH AI →", StartMatch);
                    if (AcceptsStartInput && joins.HumanCount > 0 && joins.AnyStartPressed()) StartMatch();
                    break;
                case Phase.Countdown:
                    int left = Mathf.CeilToInt(countdownTime - PhaseTime);
                    hud.ShowCountdown(Mathf.Max(1, left), $"ROUND {Round} · {room.Layout.Name.ToUpperInvariant()}");
                    if (PhaseTime >= countdownTime) BeginPlay();
                    break;
                case Phase.Playing:
                    hud.SetTimer(TimerLine());
                    hud.SetInstruction(clearOut.Message, "");
                    break;
                case Phase.RoundOver:
                case Phase.MatchOver:
                    if (AcceptsStartInput && hud.ResultShown && PhaseTime > 1.6f && joins.AnyStartPressed()) hud.ConfirmResult();
                    break;
            }
            hud.SetScoreboard(Players, WinsOf, Match.Rules.RoundsToWin, Phase != Phase.Lobby);
        }

        string JoinLine()
        {
            if (joinLine == null || joinVersion != KeyBindings.Version) { joinLine = ControlHints.Join("join"); joinVersion = KeyBindings.Version; }
            return joinLine;
        }

        string TimerLine()
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(TimeLeft));
            if (seconds != timerSeconds || timerLine == null) { timerSeconds = seconds; timerLine = FormatTime(seconds); }
            return timerLine;
        }

        void LateUpdate()
        {
            if (Time.timeScale <= 0f || Phase != Phase.Playing || Directed || hosted) return;
            var combatants = Players.Select(p => new Combatant(p.Index, p.Team, p.Health.State)).ToArray();
            foreach (int id in WinCheck.Unrevivable(combatants)) World.PlayerById(id)?.Health.Eliminate();
            var outcome = WinCheck.Evaluate(Players.Select(p => new Combatant(p.Index, p.Team, p.Health.State)));
            if (outcome.State == RoundState.Ongoing && Match.Rules.RoundTimeLimitSeconds > 0f && TimeLeft <= 0f)
                outcome = new RoundOutcome(RoundState.Draw, Teams.NoTeam);
            if (outcome.State != RoundState.Ongoing) FinishRound(outcome);
        }

        void EnterLobby()
        {
            SetPhase(Phase.Lobby);
            Time.timeScale = 1f;
            joins.AllowJoining = true;
            deliveries.Running = true;
            deliveries.ResetDrops();
            foreach (var p in Players) p.Frozen = false;
        }

        public void StartMatch()
        {
            if (hosted) return;
            if (Directed) { directed.Restart?.Invoke(); return; }
            if (Time.timeScale <= 0f || joins.HumanCount == 0) return;
            StopAllCoroutines();
            joins.EnsureOpponents();
            joins.AssignTeams();
            score = new MatchScore(Match.Rules.RoundsToWin);
            Round = 0;
            joins.AllowJoining = false;
            hud.ShowModeActions(false);
            hud.HideResult();
            CloseWarmup();
            CloseShowcase();
            if (TakeWarmup()) EnterWarmup();
            else BeginMatch();
        }

        void BeginMatch()
        {
            MatchTally.BeginMatch();
            StartRound();
        }

        void EnterWarmup()
        {
            Time.timeScale = 1f;
            SetPhase(Phase.Warmup);
            deliveries.Running = true;
            deliveries.ResetDrops();
            OpenWarmup();
        }

        void EndWarmup()
        {
            CloseWarmup();
            if (WantsShowcase) EnterShowcase();
            else BeginMatch();
        }

        void EnterShowcase()
        {
            Time.timeScale = 1f;
            SetPhase(Phase.Showcase);
            deliveries.Running = false;
            OpenShowcase();
        }

        void EndShowcase()
        {
            CloseShowcase();
            BeginMatch();
        }

        internal static float FrameStep => Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);

        internal bool TakeWarmup()
        {
            bool wanted = warmupAsked && Rules.Warmup.Applies(Match.Mode);
            warmupAsked = false;
            return wanted;
        }

        internal void OpenWarmup()
        {
            CloseWarmup();
            Warmup = new HouseWarmup(joins, hud, Match.Mode);
            Warmup.Begin();
        }

        internal bool TickWarmup(float seconds) => Warmup != null && Warmup.Tick(seconds);

        internal void CloseWarmup()
        {
            var open = Warmup;
            Warmup = null;
            open?.End();
        }

        internal bool WantsShowcase => ShowcaseTime > 0f;

        internal void OpenShowcase()
        {
            CloseShowcase();
            Showcase = new HouseShowcase(joins, hud, Match.Mode, ShowcaseTime);
            Showcase.Begin();
        }

        internal bool TickShowcase(float seconds) => Showcase != null && Showcase.Tick(seconds);

        internal void CloseShowcase()
        {
            var open = Showcase;
            Showcase = null;
            open?.End();
        }

        void StartRound()
        {
            Round++;
            Time.timeScale = 1f;
            hud.HideResult();
            MatchTally.BeginRound();
            room.ResetRoom();
            clearOut.ResetSchedule();
            deliveries.Running = false;
            deliveries.ResetDrops();
            foreach (var p in Players) { joins.Place(p); p.Frozen = true; }
            SetPhase(Phase.Countdown);
            hud.SetInstruction("", "");
            hud.ShowBanner(HouseGoals.BannerTitle(Round, GameHud.MatchHeading(Match.Mode).name), HouseGoals.Banner(Match.Mode, Match.Rules));
        }

        void BeginPlay()
        {
            SetPhase(Phase.Playing);
            roundStarted = Time.time;
            clearOut.Begin();
            hud.ShowGo();
            if (Round == 1) hud.ToastAfterBanner(FirstMove);
            foreach (var p in Players) p.Frozen = false;
            deliveries.Running = true;
            deliveries.ResetDrops();
        }

        void FinishRound(RoundOutcome outcome)
        {
            score.Record(outcome);
            SetPhase(Phase.RoundOver);
            deliveries.Running = false;
            clearOut.Running = false;
            foreach (var p in Players) p.Frozen = true;
            World.FreezeTransient();
            LastOutcome = outcome;
            lastWinner = outcome.State == RoundState.Won ? Players.First(p => p.Team == outcome.WinningTeam) : null;
            var you = hud.LocalPlayer;
            bool won = lastWinner && you && lastWinner.Team == you.Team;
            bool timeUp = !lastWinner && Match.Rules.RoundTimeLimitSeconds > 0f && TimeLeft <= 0f;
            var summary = Summary(outcome, you, timeUp);
            string heading = HouseResultText.Heading(summary);
            if (lastWinner)
            {
                foreach (var player in Players)
                    if (player && player.Health && player.Health.IsAlive && player.Team == outcome.WinningTeam)
                        player.GetComponent<PlayerAppearance>()?.Play("Celebrate", 1.5f);
                PlayFinishBeat();
                RoundWon?.Invoke(lastWinner);
            }
            if (score.IsOver)
            {
                SetPhase(Phase.MatchOver);
                var record = MatchTally.FinishFor(lastWinner);
                var card = Result(summary, won, heading, record);
                card.Board = EndBoard(lastWinner ? lastWinner.Team : Teams.NoTeam);
                hud.ShowResultSoon(card, StartMatch);
                MatchWon?.Invoke(lastWinner);
            }
            else hud.ShowResultSoon(Result(summary, won, heading, null), StartRound);
        }

        internal void EnterDirectedPhase(Phase phase)
        {
            if (phase == Phase.Countdown) { Round = directed?.Round ?? 1; StopAllCoroutines(); Time.timeScale = 1f; }
            if (phase == Phase.Playing) roundStarted = Time.time;
            SetPhase(phase);
        }

        internal void FinishDirected(HouseRoundResult r, Action again, bool final = true)
        {
            SetPhase(final ? Phase.MatchOver : Phase.RoundOver);
            deliveries.Running = false;
            foreach (var p in Players) p.Frozen = true;
            World.FreezeTransient();
            lastWinner = r.Winner ? r.Winner : r.WinningTeam != Teams.NoTeam ? Players.FirstOrDefault(p => p && p.Team == r.WinningTeam) : null;
            if (r.Draw) lastWinner = null;
            LastOutcome = lastWinner ? new RoundOutcome(RoundState.Won, lastWinner.Team) : new RoundOutcome(RoundState.Draw, Teams.NoTeam);
            var you = hud.LocalPlayer;
            bool won = lastWinner && you && lastWinner.Team == you.Team;
            if (lastWinner)
            {
                foreach (var player in Players)
                    if (player && player.Health && player.Health.IsAlive && player.Team == lastWinner.Team)
                        player.GetComponent<PlayerAppearance>()?.Play("Celebrate", 1.5f);
                PlayFinishBeat();
                RoundWon?.Invoke(lastWinner);
            }
            var record = final ? MatchTally.FinishFor(lastWinner) : null;
            var result = HudResult.Of(Round, final, won, r.Heading, record);
            result.Eyebrow = r.Eyebrow ?? "";
            result.Flavour = r.Flavour ?? "";
            result.Lines = r.Lines;
            result.Winner = lastWinner ? r.WinnerName ?? "" : "";
            result.Draw = !lastWinner;
            result.Stats = r.Stats;
            if (final) result.Board = EndBoard(lastWinner ? lastWinner.Team : Teams.NoTeam);
            else result.Standings = DirectedStandings(you, final);
            hud.ShowResultSoon(result, again);
            if (final) MatchWon?.Invoke(lastWinner);
        }

        internal void FeedScores(MatchStats stats)
        {
            if (stats == null || !joins || hosted || Rules.Warmup.Crew(Match.Mode)) return;
            var seats = Players;
            for (int i = 0; i < seats.Count; i++)
            {
                var p = seats[i];
                if (!p) continue;
                stats.SetScore(p.Index, !Directed ? WinsOf(p) : hud ? hud.MatchScoreOf(p) : 0);
                if (!Directed && score != null && p.Team != Teams.NoTeam) stats.SetSideScore(p.Team, score.Wins(p.Team));
            }
            var battle = Directed ? WarDirector.Active : null;
            if (!battle || battle.Score == null) return;
            for (int side = 0; side < WarDirector.SideCount; side++) stats.SetSideScore(side, battle.Score.Lives(side));
        }

        MatchEndBoard EndBoard(int winningTeam)
        {
            try
            {
                var stats = MatchStats.Current;
                if (stats.Lines.Count == 0) MatchTally.EnterSeats();
                FeedScores(stats);
                if (stats.Lines.Count == 0) return null;
                return MatchEnd.Build(stats, winningTeam, Match.Mode, room && room.Layout != null ? room.Layout.Name : null);
            }
            catch (Exception problem)
            {
                Debug.LogException(problem);
                return null;
            }
        }

        RoundSummary Summary(RoundOutcome outcome, PlayerController you, bool timeUp)
        {
            var seats = Players.Where(p => p).Select(p => new SeatResult(p.Name, WinsOf(p), Mathf.CeilToInt(p.Health.Current),
                outcome.State == RoundState.Won && p.Team == outcome.WinningTeam, p.IsEliminated, p.IsDowned, p == you,
                p.Binding != null && p.Binding is not BotBinding, p.Index)).ToArray();
            return new RoundSummary(Round, Match.Rules.RoundsToWin, score.IsOver, timeUp, seats);
        }

        RoundSummary DirectedStandings(PlayerController you, bool final)
        {
            var shopping = ShoppingListDirector.Active;
            if (!shopping) return null;
            var seats = Players.Where(p => p).Select(p => new SeatResult(p.Name, shopping.WinsOf(p), Mathf.CeilToInt(p.Health.Current),
                lastWinner && p.Team == lastWinner.Team, p.IsEliminated, p.IsDowned, p == you,
                p.Binding != null && p.Binding is not BotBinding, p.Index)).ToArray();
            return new RoundSummary(Round, shopping.RoundsToWin, final, false, seats);
        }

        static HudResult Result(RoundSummary summary, bool won, string heading, MatchRecord record)
        {
            var result = HudResult.Of(summary.Round, summary.Final, won, heading, record);
            result.Eyebrow = HouseResultText.Eyebrow(summary);
            result.Flavour = HouseResultText.Flavour(summary);
            result.Lines = HouseResultText.Lines(summary);
            result.Winner = HouseResultText.Winner(summary);
            result.Draw = summary.Draw;
            if (!summary.Final) result.Standings = summary;
            return result;
        }

        void PlayFinishBeat()
        {
            if (!beats || FinishBeatPlaying || Time.timeScale <= 0f || (hud && hud.Paused)) return;
            StartCoroutine(FinishBeat());
        }

        IEnumerator FinishBeat()
        {
            FinishBeatPlaying = true;
            beatRestore = Time.timeScale;
            float slowed = beatRestore * BeatScale;
            Time.timeScale = slowed;
            GameSoundBank.Sting();
            float elapsed = 0f;
            while (elapsed < BeatSeconds)
            {
                yield return null;
                if (Time.timeScale > 0f) elapsed += Time.unscaledDeltaTime;
            }
            if (Mathf.Approximately(Time.timeScale, slowed)) Time.timeScale = beatRestore;
            FinishBeatPlaying = false;
        }

        internal static string FormatTime(float seconds)
        {
            int value = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{value / 60}:{value % 60:00}";
        }
    }
}
