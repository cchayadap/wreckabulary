using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;
using Random = UnityEngine.Random;

namespace Wreckabulary.BossFight
{
    public sealed class BossFightDirector : MonoBehaviour, IHouseWordGoal
    {
        public enum State { Waiting, Preparing, Appear, DropAndPrevent, Stagger, Hit, Result }

        const float SlowMotionScale = .35f, SlowMotionSeconds = .8f, GoHoldSeconds = .8f;
        const float MiniReach = 4.5f, PropLift = .35f, PropInset = 1.1f, FloorLift = .2f, RoomProbe = .1f, BossClearance = 2.2f;
        const float StarterYaw = 37f, CacheYaw = 53f;
        const int MiniSpotTries = 12;

        PlayerJoinManager joins;
        RoomBuilder room;
        DeliverySpawner deliveries;
        GameHud hud;
        BossFightTuning tuning;
        BossNameCatalogue names;
        BossController boss;
        TelegraphedAoE aoe;
        BossFightHud panel;

        readonly HashSet<int> hooked = new();
        readonly List<Smashable> props = new();
        readonly List<MiniCalculator> minis = new();
        readonly List<PlayerController> alive = new();

        static int runSerial;
        float runStarted, phaseEnd, slowUntil, nextSupply, nextMini, cueGoUntil;
        int layer, nextLayer, clockSeconds = -1, keyVersion = -1, joinVersion = -1;
        bool won, deliveryPicked, miniSpotPicked;
        Vector3 pendingDelivery, pendingMiniSpot;
        string deliveryRoom = "", miniRoom = "", clockText = "", keyLine, joinLine;
        InputBinding keyFor;

        public static BossFightDirector Instance { get; private set; }
        public State Current { get; private set; } = State.Waiting;
        public BossController Boss => boss;
        public TelegraphedAoE Hazards => aoe;
        public BossFightHud Panel => panel;
        public BossFightTuning Tuning => tuning;
        public int Layer => layer;
        public string Clue { get; private set; } = "";
        public string ShieldWord => names != null ? names.WordFor(layer) : BossFightTuning.WordFor(layer);
        public bool Revealed => Current is State.Appear or State.DropAndPrevent or State.Stagger or State.Hit;
        public bool Spelling => Current == State.DropAndPrevent;
        public bool Won => won;
        public int MiniCount => minis.Count;
        public IReadOnlyList<Smashable> Props => props;
        public float Elapsed => Current == State.Waiting ? 0f : Time.time - runStarted;
        public float TimeLeft => Mathf.Max(0f, Match.Rules.RoundTimeLimitSeconds - Elapsed);

        public void Configure(HouseModeContext context)
        {
            joins = context.Joins; room = context.Room; deliveries = context.Deliveries; hud = context.Hud;
            context.WordGoal = this;
            context.Restart = Restart;
        }

        public string WantedWord(PlayerController player) => Spelling ? ShieldWord : null;

        void Awake() => Instance = this;

        void Start()
        {
            tuning = BossFightTuning.From(Match.Rules);
            names = new BossNameCatalogue();
            deliveries.Running = false;
            deliveries.Layout = room.Layout;
            deliveries.RoomOpen = null;

            boss = gameObject.AddComponent<BossController>();
            boss.Configure(tuning, names, ArenaCentre());
            boss.SetVisible(false);
            aoe = gameObject.AddComponent<TelegraphedAoE>();
            aoe.Configure(tuning, boss);
            aoe.PickTarget = PickTarget;
            aoe.Reachable = SameRoom;
            aoe.SmashTelegraphed += OnSmashTelegraphed;
            panel = BossFightHud.Attach(hud, this);

            joins.Joined += OnJoined;
            joins.RespawnKnockedOut = true;
            foreach (var p in joins.Players) HookSummoner(p);
            hud.SetChecklist("");
            Debug.Log("[BossFight] start boss=" + names.Name + " words=" + string.Join("/", BossFightTuning.Words) + " humans=" + joins.HumanCount);
            if (joins.HumanCount > 0) Restart();
            else Wait();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (Instance == this) Instance = null;
            if (aoe) aoe.SmashTelegraphed -= OnSmashTelegraphed;
            if (joins) joins.Joined -= OnJoined;
            if (panel) Destroy(panel);
        }

        void Wait()
        {
            Current = State.Waiting;
            joins.AllowJoining = true;
        }

        void OnJoined(PlayerController p)
        {
            HookSummoner(p);
            SetWords(p);
            joins.AssignTeams();
            if (Current != State.Waiting && Current != State.Preparing && Current != State.Result) p.Frozen = true;
        }

        void HookSummoner(PlayerController p)
        {
            if (!p || !p.Summoner || !hooked.Add(p.Index)) return;
            p.Summoner.Summoned += word => OnSummoned(word);
        }

        void SetWords(PlayerController p)
        {
            if (!p || !p.Summoner) return;
            p.Summoner.WordsOverride = names.SpellWords();
            p.Summoner.SignalWords = names.SignalWords();
            p.Summoner.ChecklistPlacementWords = Array.Empty<string>();
        }

        void OnSummoned(string word)
        {
            if (Current != State.DropAndPrevent || string.IsNullOrEmpty(word)) return;
            if (!string.Equals(word, ShieldWord, StringComparison.OrdinalIgnoreCase)) return;
            CrackShield();
        }

        public void CrackShield()
        {
            if (Current != State.DropAndPrevent || !boss.CrackLayer()) return;
            aoe.CancelAll();
            slowUntil = Time.unscaledTime + SlowMotionSeconds;
            Time.timeScale = SlowMotionScale;
            CameraRig.Shake(.35f);
            GameFeedback.Play(GameCue.Protect);
            if (boss.Vulnerable) hud.ShowBanner(BossText.ShieldBroken, BossText.HitNow);
            else hud.ShowBanner(BossText.LayerDown(boss.LayersRemaining), BossText.Staggering);
            Clue = boss.Vulnerable ? BossText.HitNow : BossText.Staggering;
            Debug.Log("[BossFight] layer cracked, remaining=" + boss.LayersRemaining);
            nextLayer = layer + 1;
            Advance(boss.Vulnerable ? State.Hit : State.Stagger);
        }

        public void Restart()
        {
            if (Time.timeScale <= 0f) Time.timeScale = 1f;
            slowUntil = 0f;
            if (joins.HumanCount == 0) { Wait(); return; }
            long seed = DateTime.UtcNow.Ticks + runSerial++ * 7919L;
            Random.InitState(unchecked((int)(seed ^ (seed >> 32))));
            room.ResetRoom(furnish: false);
            ClearProps();
            ClearMinis();
            boss.ResetBoss();
            boss.SetVisible(false);
            aoe.CancelAll();
            hud.HideResult();
            hud.HideCountdown();
            MatchTally.BeginMatch();
            layer = nextLayer = 0;
            won = false;
            nextSupply = nextMini = 0f;
            deliveryPicked = miniSpotPicked = false;
            deliveryRoom = miniRoom = "";
            clockSeconds = -1;
            Clue = BossText.Prepare;
            joins.AllowJoining = false;
            joins.RespawnKnockedOut = true;
            joins.AssignTeams();
            foreach (var p in joins.Players)
            {
                joins.Place(p);
                p.Frozen = false;
                SetWords(p);
            }
            SpawnStarterProps();
            runStarted = Time.time;
            Advance(State.Preparing);
            hud.SetInstruction(BossText.PrepareLine, KeyLine());
            Debug.Log("[BossFight] restart: preparing " + tuning.PrepSeconds + "s");
        }

        public void Advance(State next)
        {
            Current = next;
            float duration = next switch
            {
                State.Preparing => tuning.PrepSeconds,
                State.Appear => tuning.AppearSeconds,
                State.Stagger => tuning.StaggerSeconds,
                _ => 0f,
            };
            phaseEnd = Time.time + duration;
            if (next != State.Preparing) hud.SetInstruction("", "");
            if (next == State.Appear) Appear();
            else if (next == State.DropAndPrevent)
            {
                nextSupply = Time.time + tuning.SupplyDropSeconds;
                if (nextMini <= 0f) nextMini = Time.time + tuning.MiniFirstSeconds;
                SpawnWordCaches(layer);
            }
            else if (next == State.Hit) Clue = BossText.HitClue;
        }

        void Appear()
        {
            hud.HideCountdown();
            Go(BossText.Wakes);
            boss.MoveHome(BossHome());
            boss.SetVisible(true);
            hud.ShowBanner(names.Name + "!", BossText.ThreeShields);
            CameraRig.Shake(.4f);
            GameFeedback.Play(GameCue.Blast);
            Clue = BossText.ThreeShields;
        }

        void Update()
        {
            if (Time.timeScale <= 0f || tuning == null) return;
            if (slowUntil > 0f && Time.unscaledTime >= slowUntil)
            {
                Time.timeScale = 1f;
                slowUntil = 0f;
            }
            switch (Current)
            {
                case State.Waiting:
                    hud.SetInstruction(JoinLine(), "");
                    if (joins.HumanCount > 0) Restart();
                    return;
                case State.Preparing:
                    hud.ShowCountdown(Mathf.Max(1, Mathf.CeilToInt(phaseEnd - Time.time)), BossText.PrepareChip);
                    if (Time.time >= phaseEnd) Advance(State.Appear);
                    break;
                case State.Appear:
                    if (Time.time >= phaseEnd) Advance(State.DropAndPrevent);
                    break;
                case State.DropAndPrevent:
                    TickHazards(layer, true, false);
                    if (TimeLeft <= 0f) Finish(false);
                    break;
                case State.Stagger:
                    TickHazards(layer, false, false);
                    if (TimeLeft <= 0f) Finish(false);
                    else if (Time.time >= phaseEnd)
                    {
                        layer = nextLayer;
                        Advance(State.DropAndPrevent);
                    }
                    break;
                case State.Hit:
                    TickHazards(BossFightTuning.Phases - 1, true, true);
                    if (boss.Health <= 0f) Finish(true);
                    else if (TimeLeft <= 0f) Finish(false);
                    break;
                case State.Result:
                    return;
            }
            TickEventCues();
            SetClock();
        }

        void SetClock()
        {
            int shown = Mathf.CeilToInt(TimeLeft);
            if (shown == clockSeconds) return;
            clockSeconds = shown;
            clockText = RoundManager.FormatTime(shown);
            hud.SetTimer(clockText);
        }

        void Finish(bool victory)
        {
            if (Current == State.Result) return;
            won = victory;
            aoe.CancelAll();
            ClearMinis();
            hud.HideCountdown();
            hud.SetInstruction("", "");
            hud.SetTimer("");
            Time.timeScale = 1f;
            slowUntil = 0f;
            Current = State.Result;
            foreach (var p in joins.Players) p.Frozen = true;
            GameFeedback.Play(victory ? GameCue.Protect : GameCue.Out);
            Debug.Log("[BossFight] finish win=" + victory + " hp=" + boss.Health);
            var result = HudResult.Of(1, true, victory, BossText.Result(victory), MatchTally.Finish(victory));
            hud.ShowResult(result, Restart);
        }

        Vector3 ArenaCentre()
        {
            var box = room && room.Layout != null && room.Layout.Rooms.Count > 0 ? room.Layout.Rooms[0] : null;
            if (box == null) return Vector3.up * FloorLift;
            return new Vector3((box.MinX + box.MaxX) * .5f, box.FloorY + FloorLift, (box.MinZ + box.MaxZ) * .5f);
        }

        Vector3 BossHome()
        {
            var home = ArenaCentre();
            foreach (var p in AliveHumans())
            {
                var away = home - p.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude >= BossClearance * BossClearance) continue;
                if (away.sqrMagnitude < .0001f) away = Vector3.forward;
                var moved = p.transform.position + away.normalized * BossClearance;
                moved.y = home.y;
                if (SameRoom(home, moved)) home = moved;
            }
            return home;
        }

        List<PlayerController> AliveHumans()
        {
            alive.Clear();
            foreach (var p in joins.Players)
                if (p && p.Binding is not BotBinding && !p.IsKnockedOut) alive.Add(p);
            return alive;
        }

        PlayerController FirstHuman()
        {
            foreach (var p in joins.Players)
                if (p && p.Binding is not BotBinding) return p;
            return null;
        }

        Vector3 PickTarget()
        {
            var humans = AliveHumans();
            return humans.Count == 0 ? ArenaCentre() : humans[Random.Range(0, humans.Count)].transform.position;
        }

        PlayerController NearestHuman(Vector3 from)
        {
            PlayerController best = null;
            float bestSq = float.MaxValue;
            foreach (var p in AliveHumans())
            {
                float sq = (p.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            return best;
        }

        string RoomOf(Vector3 at) => room && room.Layout != null ? room.Layout.RoomAt(at.x, at.y + RoomProbe, at.z) : null;

        bool SameRoom(Vector3 from, Vector3 to)
        {
            string here = RoomOf(to);
            return here != null && here == RoomOf(from);
        }

        bool InHouse(Vector3 at) => RoomOf(at) != null;

        void TickHazards(int phase, bool circles, bool smash)
        {
            var target = PickTarget();
            aoe.Tick(circles, phase, target, Time.deltaTime);
            aoe.TickSmash(smash, target, Time.deltaTime);
            TickSupplies();
            TickMinis();
        }

        void TickSupplies()
        {
            if (nextSupply <= 0f) return;
            float left = nextSupply - Time.time;
            if (left <= tuning.EventWarnSeconds && !deliveryPicked)
            {
                pendingDelivery = deliveries.DropPoint();
                deliveryRoom = RoomName(pendingDelivery);
                deliveryPicked = true;
                Clue = BossText.SupplyComing(deliveryRoom);
                hud.Toast(BossText.SupplyComing(deliveryRoom));
            }
            if (left > 0f) return;
            nextSupply = Time.time + tuning.SupplyDropSeconds;
            if (!deliveryPicked) pendingDelivery = deliveries.DropPoint();
            var box = deliveries.DropAt(false, pendingDelivery);
            deliveryPicked = false;
            if (!box) return;
            props.Add(box);
            Go(BossText.SupplyChip(deliveryRoom));
        }

        void TickMinis()
        {
            for (int i = minis.Count - 1; i >= 0; i--)
            {
                var mini = minis[i];
                if (!mini || mini.Broken) { minis.RemoveAt(i); continue; }
                mini.Tick(NearestHuman(mini.Position), Time.deltaTime);
            }
            if (nextMini <= 0f) return;
            var near = PickTarget();
            float left = nextMini - Time.time;
            if (left <= tuning.EventWarnSeconds && !miniSpotPicked)
            {
                pendingMiniSpot = MiniSpot(near);
                miniRoom = RoomName(pendingMiniSpot);
                miniSpotPicked = true;
                Clue = BossText.MinisComing(miniRoom);
            }
            if (left > 0f) return;
            nextMini = Time.time + tuning.MiniIntervalSeconds;
            var spot = miniSpotPicked ? pendingMiniSpot : MiniSpot(near);
            miniSpotPicked = false;
            if (minis.Count >= tuning.MiniMaxAlive) return;
            var go = new GameObject("Mini calculator");
            go.transform.SetParent(transform, false);
            var spawned = go.AddComponent<MiniCalculator>();
            spawned.Configure(tuning, spot);
            spawned.Inside = InHouse;
            minis.Add(spawned);
            Go(BossText.MinisChip(miniRoom));
        }

        Vector3 MiniSpot(Vector3 at)
        {
            float y = at.y;
            for (int i = 0; i < MiniSpotTries; i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                if (dir.sqrMagnitude < .0001f) dir = Vector2.up;
                dir.Normalize();
                float reach = MiniReach * (1f - i / (float)MiniSpotTries);
                var spot = new Vector3(at.x + dir.x * reach, y, at.z + dir.y * reach);
                if (SameRoom(at, spot)) return spot;
            }
            var box = room && room.Layout != null ? room.Layout.Room(RoomOf(at)) : null;
            return box != null ? new Vector3((box.MinX + box.MaxX) * .5f, box.FloorY, (box.MinZ + box.MaxZ) * .5f) : at;
        }

        string RoomName(Vector3 at)
        {
            string name = RoomOf(at) ?? (room && room.Layout != null ? room.Layout.RoomAt(at.x, at.z) : null);
            return string.IsNullOrEmpty(name) ? BossText.TheHouse : GameHud.Spaced(name);
        }

        void OnSmashTelegraphed(Vector3 at)
        {
            Go(BossText.SmashChip);
            Clue = BossText.SmashClue;
        }

        void TickEventCues()
        {
            if (Current != State.DropAndPrevent && Current != State.Hit) return;
            if (Time.unscaledTime < cueGoUntil) return;
            float soonest = float.MaxValue;
            string label = null;
            Offer(BossText.SupplyIn, nextSupply, ref soonest, ref label);
            Offer(BossText.MinisIn, nextMini, ref soonest, ref label);
            if (Current == State.Hit) Offer(BossText.SmashIn, Time.time + aoe.SmashIn, ref soonest, ref label);
            if (label == null) return;
            float left = soonest - Time.time;
            if (left < 0f || left > tuning.EventWarnSeconds) { hud.HideCountdown(); return; }
            hud.ShowCountdown(Mathf.Max(1, Mathf.CeilToInt(left)), label);
        }

        static void Offer(string label, float at, ref float soonest, ref string chosen)
        {
            if (at <= 0f || at >= soonest) return;
            soonest = at;
            chosen = label;
        }

        void Go(string line)
        {
            hud.ShowGo(line);
            cueGoUntil = Time.unscaledTime + GoHoldSeconds;
        }

        string KeyLine()
        {
            var p = FirstHuman();
            var binding = p ? p.Binding : null;
            if (keyLine != null && keyVersion == KeyBindings.Version && ReferenceEquals(keyFor, binding)) return keyLine;
            keyVersion = KeyBindings.Version;
            keyFor = binding;
            keyLine = binding == null ? "" : BossText.Keys(
                ControlHints.For(binding, PlayerControl.Spell),
                ControlHints.For(binding, PlayerControl.Attack),
                ControlHints.For(binding, PlayerControl.Dodge));
            return keyLine;
        }

        string JoinLine()
        {
            if (joinLine == null || joinVersion != KeyBindings.Version) { joinLine = ControlHints.Join("join"); joinVersion = KeyBindings.Version; }
            return joinLine;
        }

        void ClearMinis()
        {
            for (int i = 0; i < minis.Count; i++)
                if (minis[i]) Destroy(minis[i].gameObject);
            minis.Clear();
            nextMini = 0f;
        }

        Vector3 SpotInRoom(RoomBox box, int slot, int count)
        {
            if (box == null) return ArenaCentre();
            float insetX = Mathf.Min(PropInset, (box.MaxX - box.MinX) * .25f);
            float insetZ = Mathf.Min(PropInset, (box.MaxZ - box.MinZ) * .25f);
            float t = count <= 1 ? .5f : (slot + 1f) / (count + 1f);
            float along = slot % 2 == 0 ? t : 1f - t;
            return new Vector3(
                Mathf.Lerp(box.MinX + insetX, box.MaxX - insetX, along),
                box.FloorY + PropLift,
                Mathf.Lerp(box.MinZ + insetZ, box.MaxZ - insetZ, t));
        }

        RoomBox RoomFor(int index)
        {
            var rooms = room && room.Layout != null ? room.Layout.Rooms : null;
            return rooms != null && rooms.Count > 0 ? rooms[(index + 1) % rooms.Count] : null;
        }

        void SpawnStarterProps()
        {
            var starters = BossFightCaches.StartersFor(tuning.StarterProps);
            for (int i = 0; i < starters.Count; i++)
            {
                var prop = FurnitureCatalog.Spawn(starters[i], SpotInRoom(RoomFor(i), i, starters.Count), i * StarterYaw, transform);
                if (prop) props.Add(prop);
            }
        }

        void SpawnWordCaches(int index)
        {
            string word = names.WordFor(index);
            var box = RoomFor(index);
            var caches = BossFightCaches.For(word, index);
            for (int i = 0; i < caches.Count; i++)
            {
                var prop = FurnitureCatalog.Spawn(caches[i], SpotInRoom(box, i, caches.Count), i * CacheYaw, transform);
                if (prop) props.Add(prop);
            }
            string where = box != null ? GameHud.Spaced(box.Name) : BossText.TheHouse;
            Clue = BossText.Hidden(word, where);
            Debug.Log("[BossFight] caches for '" + word + "' in " + where + ": " + string.Join(", ", caches));
        }

        void ClearProps()
        {
            for (int i = 0; i < props.Count; i++)
                if (props[i]) Destroy(props[i].gameObject);
            props.Clear();
        }
    }

    public static class BossText
    {
        public const string Prepare = "Smash furniture to collect letters.";
        public const string PrepareLine = "Get ready: smash furniture and spell a weapon";
        public const string PrepareChip = "GET READY · SMASH & SPELL";
        public const string Wakes = "THE CALCULATOR WAKES";
        public const string ThreeShields = "Three words, three shields.";
        public const string ShieldBroken = "SHIELD BROKEN!";
        public const string HitNow = "The shield is down. Hit it now!";
        public const string Staggering = "Nice! It is staggering.";
        public const string HitClue = "Hit it, and dodge the circles and jump smashes.";
        public const string SmashChip = "JUMP SMASH · DODGE!";
        public const string SmashClue = "Jump smash! Get out of the circle.";
        public const string SupplyIn = "SUPPLY DROP IN", MinisIn = "MINIS INCOMING", SmashIn = "JUMP SMASH IN";
        public const string TheHouse = "house";

        public static string LayerDown(int left) => "LAYER " + left + " DOWN!";
        public static string Hidden(string word, string room) => word + " hides in the furniture in the " + room + ". Smash it!";
        public static string SupplyComing(string room) => "Supply drop coming in the " + room + ".";
        public static string SupplyChip(string room) => "SUPPLY DROP · " + room.ToUpperInvariant();
        public static string MinisComing(string room) => "Mini calculators coming near the " + room + "!";
        public static string MinisChip(string room) => "MINIS · " + room.ToUpperInvariant();
        public static string Result(bool won) => won ? "The Calculator is smashed!" : "The Calculator wins this time";

        public static string Keys(string spell, string attack, string dodge)
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(spell)) parts.Add(spell + " spells");
            if (!string.IsNullOrEmpty(attack)) parts.Add(attack + " smashes");
            if (!string.IsNullOrEmpty(dodge)) parts.Add(dodge + " dodges");
            return string.Join(" · ", parts);
        }
    }
}
