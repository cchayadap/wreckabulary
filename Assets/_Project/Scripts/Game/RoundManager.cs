using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public enum Phase { Lobby, Countdown, Playing, RoundOver, MatchOver }

    /// <summary>Dibs and Duos use tick-end team outcomes, a shared score and clear-out schedule.</summary>
    public class RoundManager : MonoBehaviour
    {
        public static RoundManager Instance { get; private set; }
        [SerializeField] PlayerJoinManager joins;
        [SerializeField] RoomBuilder room;
        [SerializeField] DeliverySpawner deliveries;
        [SerializeField] GameHud hud;
        [SerializeField] float countdownTime = 3f;
        [SerializeField] float roundOverTime = 3f;
        MatchScore score;
        ClearOutController clearOut;
        ModeActions actions;
        float phaseStarted, roundStarted;
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
        float PhaseTime => Time.unscaledTime - phaseStarted;
        string ModeName => Match.Mode == "Duos" ? "DUOS" : "DIBS!";

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
            actions = ModeActions.Create(transform, "START WITH AI", StartMatch);
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
            phaseStarted = Time.unscaledTime;
            joins.RespawnKnockedOut = phase == Phase.Lobby;
        }

        void Update()
        {
            switch (Phase)
            {
                case Phase.Lobby:
                    hud.SetTitle(ModeName, joins.HumanCount == 0 ? ControlHints.Join("join") : "Press START — solo seats get AI opponents");
                    hud.SetTimer("");
                    actions.Show(joins.HumanCount > 0, "START WITH AI");
                    if (joins.HumanCount > 0 && joins.AnyStartPressed()) StartMatch();
                    break;
                case Phase.Countdown:
                    int left = Mathf.CeilToInt(countdownTime - PhaseTime);
                    hud.SetTitle(left > 0 ? left.ToString() : ModeName, $"Round {Round} • {room.Layout.Name}");
                    if (PhaseTime >= countdownTime) BeginPlay();
                    break;
                case Phase.Playing:
                    if (PhaseTime > .8f) hud.SetTitle("", "");
                    hud.SetTimer(FormatTime(TimeLeft));
                    hud.SetInstruction(clearOut.Message.Length > 0 ? clearOut.Message : Match.Mode == "Duos"
                        ? "Last team standing • hold grab beside a downed teammate to revive" : "Last roommate standing",
                        "Smash furniture for letters • craft gear • escape the Movers");
                    break;
                case Phase.RoundOver:
                    if (PhaseTime >= roundOverTime)
                    {
                        if (score.IsOver) EnterMatchOver();
                        else StartRound();
                    }
                    break;
                case Phase.MatchOver:
                    if (joins.AnyStartPressed()) StartMatch();
                    break;
            }
            hud.SetScoreboard(Players, WinsOf, Match.Rules.RoundsToWin, Phase != Phase.Lobby);
        }

        void LateUpdate()
        {
            if (Phase != Phase.Playing) return;
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
            if (joins.HumanCount == 0) return;
            StopAllCoroutines();
            joins.EnsureOpponents();
            joins.AssignTeams();
            score = new MatchScore(Match.Rules.RoundsToWin);
            Round = 0;
            joins.AllowJoining = false;
            actions.Show(false);
            StartRound();
        }

        void StartRound()
        {
            Round++;
            Time.timeScale = 1f;
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
            hud.SetTitle(ModeName, "");
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
            if (!lastWinner) { hud.SetTitle("DRAW!", "Nobody gets dibs • next round shortly"); return; }
            hud.SetTitle($"{WinnerName()} WINS THE ROUND", $"{score.Wins(lastWinner.Team)}/{score.RoundsToWin}");
            StartCoroutine(SlowMo());
            RoundWon?.Invoke(lastWinner);
        }

        string WinnerName() => Match.Mode == "Duos" ? $"TEAM {lastWinner.Team + 1}" : lastWinner.Name;

        void EnterMatchOver()
        {
            SetPhase(Phase.MatchOver);
            hud.SetTitle($"{WinnerName()} CALLS DIBS!", "Play again or head home");
            actions.Show(true, "PLAY AGAIN");
            MatchWon?.Invoke(lastWinner);
        }

        IEnumerator SlowMo()
        {
            Time.timeScale = .35f;
            yield return new WaitForSecondsRealtime(.9f);
            Time.timeScale = 1f;
        }

        internal static string FormatTime(float seconds)
        {
            int value = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{value / 60}:{value % 60:00}";
        }
    }
}
