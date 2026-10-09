using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public enum Phase { Lobby, Countdown, Playing, RoundOver, MatchOver }

    public class RoundManager : MonoBehaviour
    {
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
        public Phase Phase { get; private set; } = Phase.Lobby;
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

        void Awake() => Instance = this;

        void Start()
        {
            if (!joins) joins = GetComponent<PlayerJoinManager>();
            if (!room) room = GetComponent<RoomBuilder>();
            if (!deliveries) deliveries = GetComponent<DeliverySpawner>();
            if (!hud) hud = FindAnyObjectByType<GameHud>();
            if (Match.Mode == "MovingOut")
            {
                gameObject.AddComponent<MovingOutDirector>().Configure(joins, room, deliveries, hud);
                enabled = false;
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
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void SetPhase(Phase phase)
        {
            Phase = phase;
            phaseElapsed = 0f;
            joins.RespawnKnockedOut = phase == Phase.Lobby;
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            phaseElapsed += Time.unscaledDeltaTime;
            switch (Phase)
            {
                case Phase.Lobby:
                    hud.SetInstruction(joins.HumanCount == 0 ? ControlHints.Join("join") : "Press START — solo seats get AI opponents", "");
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
                    hud.SetTimer(FormatTime(TimeLeft));
                    hud.SetInstruction(clearOut.Message, "");
                    break;
                case Phase.RoundOver:
                case Phase.MatchOver:
                    if (AcceptsStartInput && hud.ResultShown && PhaseTime > 1.6f && joins.AnyStartPressed()) hud.ConfirmResult();
                    break;
            }
            hud.SetScoreboard(Players, WinsOf, Match.Rules.RoundsToWin, Phase != Phase.Lobby);
        }

        void LateUpdate()
        {
            if (Time.timeScale <= 0f || Phase != Phase.Playing) return;
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
            if (Time.timeScale <= 0f || joins.HumanCount == 0) return;
            StopAllCoroutines();
            joins.EnsureOpponents();
            joins.AssignTeams();
            score = new MatchScore(Match.Rules.RoundsToWin);
            Round = 0;
            joins.AllowJoining = false;
            hud.ShowModeActions(false);
            hud.HideResult();
            MatchTally.BeginMatch();
            StartRound();
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
        }

        void BeginPlay()
        {
            SetPhase(Phase.Playing);
            roundStarted = Time.time;
            clearOut.Begin();
            hud.ShowGo();
            if (Round == 1) hud.Toast("Break furniture → collect its letters → spell new gear.");
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
            lastWinner = outcome.State == RoundState.Won ? Players.First(p => p.Team == outcome.WinningTeam) : null;
            var you = hud.LocalPlayer;
            bool won = lastWinner && you && lastWinner.Team == you.Team;
            bool timeUp = !lastWinner && Match.Rules.RoundTimeLimitSeconds > 0f && TimeLeft <= 0f;
            string heading = won ? "You called dibs!" : timeUp ? "Time’s up. A perfectly messy draw." : "One more word. One more chance.";
            if (lastWinner)
            {
                StartCoroutine(SlowMo());
                RoundWon?.Invoke(lastWinner);
            }
            if (score.IsOver)
            {
                SetPhase(Phase.MatchOver);
                var record = MatchTally.FinishFor(lastWinner);
                hud.ShowResultSoon(HudResult.Of(Round, true, won, heading, record), StartMatch);
                MatchWon?.Invoke(lastWinner);
            }
            else hud.ShowResultSoon(HudResult.Of(Round, false, won, heading, null), StartRound);
        }

        IEnumerator SlowMo()
        {
            Time.timeScale = .35f;
            float elapsed = 0f;
            while (elapsed < .9f)
            {
                yield return null;
                if (Time.timeScale > 0f) elapsed += Time.unscaledDeltaTime;
            }
            Time.timeScale = 1f;
        }

        internal static string FormatTime(float seconds)
        {
            int value = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{value / 60}:{value % 60:00}";
        }
    }
}
