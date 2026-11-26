using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class RiddleRushDirector : HouseRoundDirector, IHouseWordGoal
    {
        public static ulong? SeedOverride { get; set; }
        public static RiddleRushDirector Active { get; private set; }

        readonly Dictionary<PlayerController, Action<string>> listening = new();
        readonly HashSet<Smashable> pulsing = new();
        readonly List<Smashable> furniture = new();
        readonly List<string> words = new();
        readonly LetterBag supply = new();
        Func<PlayerController, int> score;
        Func<Riddle, bool> usable;
        RiddleBook book;
        int suppliedFrame = -1;

        public RiddleRace Race { get; private set; }
        public IReadOnlyList<Riddle> HouseRiddles { get; private set; } = Array.Empty<Riddle>();
        public RiddleRushHud Panel { get; private set; }
        public int Target { get; set; }
        public float HintSeconds { get; set; }
        public float PassSeconds { get; set; }
        public float BeatSeconds { get; set; }
        public float KnowsMin { get; set; }
        public float KnowsMax { get; set; }
        public bool Playing => Current == Stage.Playing;
        public IReadOnlyCollection<Smashable> Pulsing => pulsing;
        public bool Pulses(Smashable s) => s && pulsing.Contains(s);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = null; SeedOverride = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Enlist() => HouseModeDirectors.Register(HouseModes.RiddleRush, c => c.Host.AddComponent<RiddleRushDirector>().Configure(c));

        protected override void Configured()
        {
            var rules = Context.Rules;
            Target = Mathf.Max(1, rules.Setting("pointsToWin", RiddleRace.PointsToWin));
            HintSeconds = rules.Setting("hintSeconds", RiddleRace.HintSeconds);
            PassSeconds = rules.Setting("passSeconds", RiddleRace.PassSeconds);
            BeatSeconds = rules.Setting("beatSeconds", RiddleRace.BeatSeconds);
            KnowsMin = rules.Setting("botKnowsMin", RiddleRace.BotKnowsMin);
            KnowsMax = rules.Setting("botKnowsMax", RiddleRace.BotKnowsMax);
            score = p => Race != null && p ? Race.Points(p.Index) : 0;
            usable = r => Supply().Contains(r.Letters);
            book = Load();
            Context.WordGoal = this;
            Active = this;
            Smashable.AddDropGoals(AnswerNeeded);
            Joins.Joined += Listen;
            Panel = gameObject.AddComponent<RiddleRushHud>();
            Panel.Configure(Hud, this);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            Smashable.RemoveDropGoals(AnswerNeeded);
            StopPulsing();
            foreach (var entry in listening)
                if (entry.Key && entry.Key.Summoner) entry.Key.Summoner.Summoned -= entry.Value;
            listening.Clear();
            if (Context != null && Joins) Joins.Joined -= Listen;
            if (Context != null && Context.Rounds && ReferenceEquals(Context.WordGoal, this)) Context.WordGoal = null;
        }

        static RiddleBook Load()
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var file = data ? data.HouseRiddlesFile() : null;
#if UNITY_EDITOR
            if (!file) file = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(GameData.HouseRiddlesPath);
#endif
            if (file && !string.IsNullOrEmpty(file.text)) return RiddleBook.FromJson(file.text, file.name + ".json");
            Debug.LogError($"Riddle Rush has no riddles: {GameData.HouseRiddlesPath} is not in the game data. Run Wreckabulary > Data > Set Up Game Data.");
            return new RiddleBook(Array.Empty<Riddle>());
        }

        static HashSet<string> Recipes() => new(GameConfig.Current.Items.Enabled.Select(i => i.Id), StringComparer.Ordinal);

        protected override void OnMatchStart()
        {
            HouseRiddles = book.For(Room.Layout, Context.Rules.MaxLetters, Recipes());
            ulong seed = SeedOverride ?? unchecked((ulong)DateTime.UtcNow.Ticks);
            int seats = Mathf.Max(Joins.MaxPlayers, Players.Count);
            Race = new RiddleRace(new RiddleDeck(HouseRiddles, seed), Mathf.Max(1, seats), seed, Target, HintSeconds, BeatSeconds)
            {
                KnowsMin = KnowsMin, KnowsMax = Mathf.Max(KnowsMin, KnowsMax), PassAfter = Mathf.Max(0f, PassSeconds),
            };
            pulsing.Clear();
            foreach (var p in Players) Listen(p);
        }

        protected override void OnPlay()
        {
            Joins.RespawnKnockedOut = true;
            SmashableLabel.Pulse = Pulses;
            Asked(Race.Start(usable));
            Hud.Toast(RiddleText.Opening(Target));
            if (Race.Current == null) Finish(Result(false));
        }

        protected override void OnFinish()
        {
            Joins.RespawnKnockedOut = false;
            pulsing.Clear();
            StopPulsing();
        }

        void StopPulsing()
        {
            if (SmashableLabel.Pulse == (Predicate<Smashable>)Pulses) SmashableLabel.Pulse = null;
        }

        protected override void Tick()
        {
            if (Race == null) return;
            int passes = Race.Passes;
            var happened = Race.Tick(Time.deltaTime, usable);
            if (Race.Passes != passes) Hud.Toast(RiddleText.Passed(Race.LastPassed));
            if (happened == RiddleEvent.Hint) Pulse();
            else if (happened != RiddleEvent.None) Asked(happened);
            if (TimeLimit > 0f && TimeLeft <= 0f) Finish(Result(true));
        }

        protected override void Scoreboard() => Hud.SetScoreboard(Players, score, Target, Race != null);

        public IEnumerable<string> AnswerNeeded() => Playing && Race != null && Race.Current != null ? new[] { Race.Current.Answer } : null;

        public string WantedWord(PlayerController player) =>
            Playing && Race != null && player && player.Binding is BotBinding && Race.Knows(player.Index) ? Race.Current.Answer : null;

        void Listen(PlayerController p)
        {
            if (!p || !p.Summoner || listening.ContainsKey(p)) return;
            Action<string> heard = word => Spelled(p, word);
            p.Summoner.Summoned += heard;
            listening[p] = heard;
        }

        void Spelled(PlayerController p, string word)
        {
            if (!Playing || Race == null || !Race.Solve(p.Index, word)) return;
            pulsing.Clear();
            if (Race.Stage == RiddleStage.Won) { Finish(Result(false)); return; }
            if (Race.Points(p.Index) == Target - 1) Hud.Toast(RiddleText.OneMore(SeatName(p.Index)));
        }

        void Asked(RiddleEvent happened)
        {
            pulsing.Clear();
            if (happened != RiddleEvent.Refurnish) return;
            Room.ResetRoom();
            Hud.Toast(RiddleText.Refurnished);
        }

        LetterBag Supply()
        {
            if (suppliedFrame == Time.frameCount) return supply;
            suppliedFrame = Time.frameCount;
            supply.TakeAll();
            foreach (var s in FindObjectsByType<Smashable>(FindObjectsSortMode.None))
                if (s && !s.IsBroken && LetterBag.IsWord(s.Word)) supply.Add(LetterBag.FromWord(s.Word));
            if (TilePool.Instance)
                foreach (var tile in TilePool.Instance.Active)
                    if (tile && LetterBag.IsLetter(tile.Letter)) supply.Add(tile.Letter);
            foreach (var p in Players)
                if (p) foreach (char c in p.Inventory.Letters) if (LetterBag.IsLetter(c)) supply.Add(c);
            return supply;
        }

        void Pulse()
        {
            pulsing.Clear();
            furniture.Clear();
            foreach (var s in FindObjectsByType<Smashable>(FindObjectsSortMode.None))
                if (s && !s.IsBroken && !s.Invulnerable && LetterBag.IsWord(s.Word)) furniture.Add(s);
            furniture.Sort((a, b) => Nearest(a).CompareTo(Nearest(b)));
            words.Clear();
            foreach (var s in furniture) words.Add(s.Word);
            foreach (int i in RiddleHints.Sources(Race.Current.Answer, words)) pulsing.Add(furniture[i]);
        }

        float Nearest(Smashable s)
        {
            float best = float.MaxValue;
            var at = s.transform.position;
            foreach (var p in Players)
                if (p && !p.IsKnockedOut) best = Mathf.Min(best, (p.transform.position - at).sqrMagnitude);
            return best;
        }

        public string SeatName(int seat)
        {
            var p = Players.FirstOrDefault(x => x && x.Index == seat);
            return p ? SeatTags.Name(p.Name, p == Hud.LocalPlayer, Humans()) : "";
        }

        int Humans() => Players.Count(p => p && p.Binding != null && p.Binding is not BotBinding);

        HouseRoundResult Result(bool timeUp)
        {
            int seat = Race == null ? -1 : Race.Winner >= 0 ? Race.Winner : timeUp ? Race.Leader() : -1;
            var winner = Players.FirstOrDefault(p => p && p.Index == seat);
            string name = winner ? SeatName(seat) : "";
            bool you = winner && winner == Hud.LocalPlayer && Humans() == 1;
            var lines = Race == null ? Array.Empty<string>() : Race.Standings()
                .Select(s => Players.FirstOrDefault(p => p && p.Index == s)).Where(p => p)
                .Select(p => RiddleText.Line(SeatName(p.Index), Race.Points(p.Index), p == winner)).ToArray();
            return new HouseRoundResult
            {
                Winner = winner,
                Draw = !winner,
                WinnerName = name,
                Heading = Race?.Current == null ? RiddleText.NoRiddles : RiddleText.Heading(name, you),
                Eyebrow = RiddleText.Eyebrow(name, you),
                Flavour = RiddleText.Flavour(Race?.Solved ?? 0),
                Lines = lines,
            };
        }
    }
}
