using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class BotBinding : InputBinding
    {
        static int nextId;
        readonly string id = $"bot-{nextId++}";
        public PlayerCommands Commands;
        public override string Id => id;
        public override bool HoldingAttackRaisesShield => false;
        public override void Read(ref PlayerCommands commands) => commands = Commands;
        public override bool JoinPressed() => false;
        public override bool StartPressed() => false;
    }

    [DefaultExecutionOrder(-100)]
    public sealed class BotController : MonoBehaviour
    {
        enum Quarry { None, Opponent, Tile, Gear, Furniture, Hold }

        const float ThinkSeconds = .3f, CraftSeconds = 1f, DropSeconds = 1.2f, JunkSeconds = 8f, ScanSeconds = 1f, GearPreference = 3f, GearPatience = 4f;
        const float GoalUseSeconds = .5f, NeedlessFurniture = 15f, HoldGuard = 6f, HoldDetour = 10f;
        static readonly ProfilerMarker UpdateMarker = new ProfilerMarker(ProfilerCategory.Scripts, "Bot.Update");

        PlayerController player;
        BotBinding binding;
        Transform target;
        Rigidbody gearBody;
        HeldWeapon ignoredGear;
        float gearSince, ignoreGearUntil;
        Quarry quarry;
        float drawUntil = -1f;
        float nextThink, nextAttack, nextCraft, nextEscape, nextDrop, guardUntil, reactAt = -1f, threatEnds;
        Vector3 lastPosition, threatFrom;
        float stuckTime;
        ClearOutController clearOut;
        HouseLayout layout;
        HouseRoutes routes;
        Func<string, bool> closed;
        char junk;
        float junkUntil;
        int planSignature = int.MinValue;
        IReadOnlyList<WordEntry> planWords;
        WordEntry craftPick;
        string planGoal, spelledGoal;
        bool goalFights;
        float nextGoalUse;
        Vector3 holdAt;
        readonly List<string> wanted = new();
        readonly bool[] useful = new bool[26];

        public void Configure(PlayerController owner, BotBinding input)
        {
            Unhook();
            player = owner;
            binding = input;
            lastPosition = transform.position;
            clearOut = FindAnyObjectByType<ClearOutController>();
            layout = clearOut ? clearOut.Layout : GameConfig.Current.HouseFor(Session.MapId);
            routes = new HouseRoutes(layout);
            closed = Closed;
            if (player && player.Health) player.Health.Damaged += OnDamaged;
            if (player && player.Summoner) player.Summoner.Summoned += OnSummoned;
        }

        void OnEnable() => HeldWeapon.MeleeStarted += OnSwing;
        void OnDisable() => HeldWeapon.MeleeStarted -= OnSwing;
        void OnDestroy() => Unhook();

        void Unhook()
        {
            if (player && player.Health) player.Health.Damaged -= OnDamaged;
            if (player && player.Summoner) player.Summoner.Summoned -= OnSummoned;
        }

        void OnSummoned(string word) { if (player && word == player.Summoner.WantedWord) spelledGoal = word; }

        bool Closed(string room) => clearOut && clearOut.Running && clearOut.Schedule.PhaseOf(room, clearOut.Elapsed) == RoomPhase.Closed;

        void Update()
        {
            if (binding == null || !player) return;
            using (UpdateMarker.Auto())
            {
                var c = Think();
                if (Time.time < drawUntil && player.CanAct) c.attackHeld = true;
                binding.Commands = c;
            }
        }

        PlayerCommands Think()
        {
            var c = new PlayerCommands();
            if (!player.CanAct) return c;

            var at = player.transform.position;
            string here = layout.RoomAt(at.x, at.y, at.z);
            Vector3 destination = at;

            var downed = DownedTeammate(at);
            bool evacuating = clearOut && clearOut.Running && here != null &&
                clearOut.Schedule.PhaseOf(here, clearOut.Elapsed) >= RoomPhase.Warning;

            if (evacuating)
            {
                var safe = SafeRoom(at);
                if (safe != null) destination = Centre(safe);
            }
            else if (downed)
            {
                destination = downed.transform.position;
                if ((destination - at).magnitude <= player.Health.Rules.ReviveRange * .9f && SameLevel(destination, at) && Reachable(layout, at, destination))
                {
                    c.grabHeld = true;
                    c.drop = player.Combat.IsHolding;
                    c.grab = !player.Combat.IsReviving && !player.Combat.IsHolding;
                    destination = at;
                }
            }
            else
            {
                if (Time.time >= nextThink)
                {
                    nextThink = Time.time + ThinkSeconds;
                    Choose(at, here);
                }
                if (target) destination = Engage(ref c, at, here);
                else if (quarry == Quarry.Hold) destination = holdAt;
                Spell(at);
                DropJunk(at);
            }

            var waypoint = Waypoint(layout, routes, here, at, destination, closed);
            var delta = World.Flat(waypoint - at);
            bool final = World.Flat(waypoint - destination).sqrMagnitude < .0001f && SameLevel(waypoint, destination);
            var direction = delta.sqrMagnitude > (final ? .16f : .01f) ? delta.normalized : Vector3.zero;
            if (target && !evacuating && !downed) c.look = new Vector2(target.position.x - at.x, target.position.z - at.z);
            else c.look = new Vector2(direction.x, direction.z);
            if (direction.sqrMagnitude > 0f)
            {
                if (Physics.SphereCast(at + Vector3.up * .4f, .25f, direction, out var hit, .85f, World.GroundMask,
                    QueryTriggerInteraction.Ignore) && hit.normal.y < .6f && hit.rigidbody != player.Body && (!target || !hit.transform.IsChildOf(target)))
                {
                    float turn = 55f + player.Index * 12f;
                    var usual = Quaternion.Euler(0f, turn, 0f) * direction;
                    var other = Quaternion.Euler(0f, -turn, 0f) * direction;
                    direction = !NearStairs(layout, waypoint, .8f) && NearStairs(layout, at + usual * .85f, .4f)
                        && !NearStairs(layout, at + other * .85f, .4f) ? other : usual;
                }
                c.move = new Vector2(direction.x, direction.z);
            }
            if (!evacuating && !downed)
            {
                React(ref c);
                ServeGoal(ref c);
            }
            stuckTime = c.move.sqrMagnitude > .2f && World.Flat(at - lastPosition).sqrMagnitude < .0004f
                ? stuckTime + Time.deltaTime : 0f;
            if (stuckTime > .7f && Time.time >= nextEscape)
            {
                c.jump = true;
                c.dodge = player.Health.CanDodge;
                nextEscape = Time.time + 1.5f;
                stuckTime = 0f;
            }
            lastPosition = at;
            return c;
        }

        PlayerController DownedTeammate(Vector3 at)
        {
            PlayerController best = null;
            float bestSq = float.MaxValue;
            var players = World.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!p || !p.IsDowned || !Teams.AreTeammates(player.Team, p.Team) || !WithinReach(layout, p.transform.position)) continue;
                float d = (p.transform.position - at).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = p; }
            }
            return best;
        }

        RoomBox SafeRoom(Vector3 at)
        {
            RoomBox best = null;
            float bestSq = float.MaxValue;
            var rooms = layout.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                if (clearOut.Schedule.PhaseOf(r.Name, clearOut.Elapsed) != RoomPhase.Safe) continue;
                float d = (Centre(r) - at).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = r; }
            }
            return best;
        }

        void Choose(Vector3 at, string here)
        {
            Scan();
            Plan();
            bool armed = planGoal != null ? goalFights && GoalSlot() >= 0 : player.Summoner.Needs.Armed;
            var opponent = World.NearestOpponent(player, at);
            bool close = opponent && Vector3.Distance(opponent.transform.position, at) < BotTactics.CloseQuarters;
            if (armed && HoldTheMiddle(at, here, opponent)) return;
            if (opponent && (armed || close)) { Hunt(opponent.transform, Quarry.Opponent); return; }
            if (!armed)
            {
                var tile = NearestTile(at);
                var gear = LooseGear(at, out float gearCost);
                if (gear && quarry == Quarry.Gear && target == gear.transform && Time.time - gearSince > GearPatience)
                {
                    ignoredGear = gear;
                    ignoreGearUntil = Time.time + GearPatience * 3f;
                    gear = null;
                }
                if (gear && (!tile || gearCost <= Cost(tile.transform.position, at) + GearPreference))
                {
                    if (quarry != Quarry.Gear || target != gear.transform) gearSince = Time.time;
                    Hunt(gear.transform, Quarry.Gear);
                    gear.TryGetComponent(out gearBody);
                    return;
                }
                if (tile) { Hunt(tile.transform, Quarry.Tile); return; }
            }
            var furniture = NearestFurniture(at);
            if (furniture) Hunt(furniture.transform, Quarry.Furniture);
            else if (opponent) Hunt(opponent.transform, Quarry.Opponent);
            else Hunt(null, Quarry.None);
        }

        bool HoldTheMiddle(Vector3 at, string here, PlayerController opponent)
        {
            var war = WarDirector.Active;
            var room = war ? Room(layout, war.HoldRoom) : null;
            if (room == null) return false;
            float distance = opponent ? Vector3.Distance(opponent.transform.position, at) : float.MaxValue;
            if (distance <= (here == room.Name ? HoldGuard : HoldDetour)) return false;
            float side = player.Index % 2 == 0 ? 1f : -1f, depth = (player.Index / 2 % 2 == 0 ? 1f : -1f) * .8f;
            holdAt = Beside(room, Centre(room), new Vector3(side * 1.2f, 0f, depth));
            Hunt(null, Quarry.Hold);
            return true;
        }

        void Hunt(Transform next, Quarry kind)
        {
            target = next;
            quarry = kind;
        }

        Vector3 Engage(ref PlayerCommands c, Vector3 at, string here)
        {
            var goal = target.position;
            var to = World.Flat(goal - at);
            float distance = to.magnitude;
            bool level = SameLevel(goal, at) && Reachable(layout, at, goal);
            var gear = player.Combat.Weapon;
            var held = gear ? gear.Definition : null;
            switch (quarry)
            {
                case Quarry.Tile:
                    return goal;
                case Quarry.Gear:
                    if (level && distance <= player.Combat.GrabDistance && gearBody && player.Combat.GrabTarget() == gearBody) c.grab = true;
                    return goal;
                case Quarry.Opponent when BotTactics.Shoots(held):
                    if (distance > BotTactics.ShootRange || !level) return goal;
                    if (Aimed(to, distance, held) && InSight(at, goal)) Press(ref c);
                    return at;
                case Quarry.Opponent when BotTactics.Throws(held):
                    return Throw(ref c, at, here, goal, to, distance, level, held);
                default:
                    if (distance >= BotTactics.StrikeRange(held) || !level) return goal;
                    if (Aimed(to, distance, held)) Press(ref c);
                    return at;
            }
        }

        Vector3 Throw(ref PlayerCommands c, Vector3 at, string here, Vector3 goal, Vector3 to, float distance, bool level, ItemDefinition held)
        {
            var window = BotTactics.Window(held.Thrown, -Physics.gravity.y);
            if (distance > window.Far || !level) return goal;
            if (distance < window.Near)
            {
                var room = Room(layout, here);
                var back = room != null ? Beside(room, at - to.normalized * (window.Near - distance + .5f), Vector3.zero) : at;
                if (World.Flat(back - at).sqrMagnitude > .09f) return back;
            }
            if (Aimed(to, distance, held) && InSight(at, goal)) Press(ref c);
            return at;
        }

        bool Aimed(Vector3 to, float distance, ItemDefinition held) =>
            distance < BotTactics.PointBlank || Vector3.Angle(player.Facing, to) <= BotTactics.AimDegrees(held, player.Health.Rules.Unarmed.ArcDegrees);

        void Press(ref PlayerCommands c)
        {
            if (Time.time < nextAttack) return;
            c.attack = true;
            var gear = player.Combat.Weapon;
            if (gear && gear.Definition?.Ranged != null) drawUntil = Time.time + gear.Definition.Ranged.DrawSeconds;
            nextAttack = Time.time + Mathf.Max(.3f, gear ? gear.Cooldown : player.Health.Rules.Unarmed.Cycle);
        }

        static readonly RaycastHit[] sight = new RaycastHit[8];

        bool InSight(Vector3 at, Vector3 goal)
        {
            var from = at + Vector3.up * BotTactics.LaunchHeight;
            var line = goal + Vector3.up - from;
            float length = line.magnitude;
            if (length < .01f) return true;
            int n = Physics.RaycastNonAlloc(from, line / length, sight, length, World.GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hit = sight[i].transform;
                if (hit && !hit.IsChildOf(transform) && !hit.IsChildOf(target)) return false;
            }
            return true;
        }

        void React(ref PlayerCommands c)
        {
            var combat = player.Combat;
            int shieldSlot = SlotHolding(true), weaponSlot = SlotHolding(false);
            if (reactAt >= 0f && Time.time >= reactAt)
            {
                reactAt = -1f;
                if (shieldSlot >= 0) guardUntil = Mathf.Max(guardUntil, threatEnds + .2f);
                else if (player.Health.CanDodge)
                {
                    var side = Vector3.Cross(Vector3.up, threatFrom) * (player.Index % 2 == 0 ? 1f : -1f);
                    var away = (side - threatFrom).normalized;
                    c.move = new Vector2(away.x, away.z);
                    c.dodge = true;
                    c.attack = false;
                    return;
                }
            }
            if (Time.time < guardUntil && shieldSlot >= 0)
            {
                c.attack = false;
                c.look = new Vector2(threatFrom.x, threatFrom.z);
                if (combat.ActiveSlot != shieldSlot) c.slot = shieldSlot + 1;
                else c.blockHeld = true;
                return;
            }
            if (weaponSlot >= 0 && combat.ActiveSlot != weaponSlot && (!combat.Weapon || combat.Weapon.Shield != null)) c.slot = weaponSlot + 1;
        }

        int SlotHolding(bool shield)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                var gear = player.Combat.GearIn(slot);
                if (gear && (gear.Shield != null) == shield && (shield || Fights(gear))) return slot;
            }
            return -1;
        }

        static bool Fights(HeldWeapon gear) => gear.Definition == null || SpellChoice.Fights(SpellChoice.RoleOf(gear.Definition));

        void OnDamaged(PlayerHealth _, HitInfo hit, HitResult result)
        {
            if (!player || !hit.Blockable || hit.Source is not (HitSource.Melee or HitSource.Thrown or HitSource.Unarmed)) return;
            var attacker = Hits.Attacker(hit);
            if (!attacker || attacker == player || !Teams.AreHostile(player.Team, attacker.Team)) return;
            var toward = new Vector3(-hit.DirX, 0f, -hit.DirZ);
            threatFrom = toward.sqrMagnitude > 1e-4f ? toward.normalized : player.Facing;
            guardUntil = Mathf.Max(guardUntil, Time.time + BotTactics.GuardSeconds);
        }

        void OnSwing(HeldWeapon weapon, PlayerController owner)
        {
            if (!player || !owner || owner == player || !weapon || !Teams.AreHostile(player.Team, owner.Team)) return;
            var swing = weapon.Stats;
            Vector3 mine = player.transform.position, theirs = owner.transform.position;
            var offset = World.Flat(mine - theirs);
            if (!SameLevel(mine, theirs) || !BotTactics.Threatens(swing, offset.magnitude, Vector3.Angle(owner.Facing, offset))) return;
            threatFrom = offset.sqrMagnitude > 1e-4f ? -offset.normalized : player.Facing;
            reactAt = Time.time + BotTactics.ReactionSeconds;
            threatEnds = Time.time + swing.Windup + swing.Active;
        }

        void Plan()
        {
            var summoner = player.Summoner;
            var letters = player.Inventory.Letters;
            var needs = summoner.Needs;
            var words = summoner.Words;
            string goal = summoner.WantedWord;
            int signature = Signature(letters, needs);
            if (signature == planSignature && ReferenceEquals(words, planWords) && goal == planGoal) return;
            planSignature = signature;
            planWords = words;
            if (goal != planGoal) goalFights = Fights(goal);
            planGoal = goal;
            var objectives = summoner.ChecklistPlacementWords;
            craftPick = null;
            foreach (var option in summoner.Options())
                if (SpellChoice.BotWants(WordSolver.RoleOf(option, objectives), needs, option.word, goal)) { craftPick = option; break; }
            wanted.Clear();
            int capacity = player.Inventory.Capacity;
            if (goal != null && goal.Length <= capacity) wanted.Add(goal);
            else if (goal == null)
                for (int i = 0; i < words.Count; i++)
                    if (words[i] != null && !string.IsNullOrEmpty(words[i].word) && words[i].word.Length <= capacity
                        && SpellChoice.BotWants(WordSolver.RoleOf(words[i], objectives), needs)) wanted.Add(words[i].word);
            for (int i = 0; i < 26; i++) useful[i] = BotTactics.Helps(letters, (char)('A' + i), wanted);
        }

        static bool Fights(string word) =>
            word != null && GameConfig.Current.Items.TryGet(word, out var item) && SpellChoice.Fights(SpellChoice.RoleOf(item));

        int GoalSlot()
        {
            if (planGoal == null) return -1;
            for (int slot = 0; slot < 2; slot++)
            {
                var gear = player.Combat.GearIn(slot);
                if (gear && gear.word == planGoal) return slot;
            }
            return -1;
        }

        void ServeGoal(ref PlayerCommands c)
        {
            if (planGoal == null || player.Summoner.IsCrafting) return;
            var combat = player.Combat;
            int slot = GoalSlot();
            if (slot < 0)
            {
                if (craftPick != null && !combat.HasFreeGearSlot) FreeAHand(ref c);
                return;
            }
            if (combat.ActiveSlot != slot)
            {
                c.slot = slot + 1;
                c.attack = c.blockHeld = false;
                return;
            }
            if (goalFights || combat.IsChanneling || Time.time < nextGoalUse) return;
            nextGoalUse = Time.time + GoalUseSeconds;
            c.attack = true;
            c.blockHeld = false;
        }

        void FreeAHand(ref PlayerCommands c)
        {
            var combat = player.Combat;
            if (combat.IsHolding && (!combat.Weapon || combat.Weapon.word != planGoal)) { c.drop = true; return; }
            for (int slot = 0; slot < 2; slot++)
            {
                var gear = combat.GearIn(slot);
                if (gear && gear.word != planGoal) { c.slot = slot + 1; return; }
            }
        }

        static readonly int[] counted = new int[26];

        static int Signature(IReadOnlyList<char> letters, SpellNeeds needs)
        {
            Array.Clear(counted, 0, counted.Length);
            for (int i = 0; i < letters.Count; i++)
            {
                int k = char.ToUpperInvariant(letters[i]) - 'A';
                if (k >= 0 && k < 26) counted[k]++;
            }
            int hash = needs.Bits + 1;
            for (int i = 0; i < 26; i++) hash = unchecked(hash * 31 + counted[i]);
            return hash;
        }

        void Spell(Vector3 at)
        {
            if (craftPick == null || Time.time < nextCraft || player.Summoner.IsCrafting || !player.Combat.HasFreeGearSlot) return;
            nextCraft = Time.time + CraftSeconds;
            if (player.Summoner.Needs.Armed && World.NearestOpponent(player, at, BotTactics.SafeToSpell)) return;
            player.Summoner.BeginCraft(craftPick);
        }

        void DropJunk(Vector3 at)
        {
            var bag = player.Inventory;
            if (Time.time < nextDrop || !bag.IsFull || craftPick != null || wanted.Count == 0 || player.Summoner.IsCrafting) return;
            nextDrop = Time.time + DropSeconds;
            char letter = BotTactics.Junk(bag.Letters, wanted);
            if (letter == '\0' || !bag.DropLetter(letter, at, -player.Facing)) return;
            junk = letter;
            junkUntil = Time.time + JunkSeconds;
        }

        bool Useful(char letter)
        {
            int i = char.ToUpperInvariant(letter) - 'A';
            return i >= 0 && i < 26 && useful[i];
        }

        bool Yields(string word)
        {
            if (word == null) return false;
            for (int i = 0; i < word.Length; i++) if (Useful(word[i])) return true;
            return false;
        }

        LetterTile NearestTile(Vector3 at)
        {
            var bag = player.Inventory;
            if (!TilePool.Instance || bag.Count >= bag.Capacity) return null;
            bool picky = planGoal != null || bag.Count >= bag.Capacity - 2;
            LetterTile best = null;
            float bestCost = float.MaxValue;
            var active = TilePool.Instance.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                if (!t || (t.Letter == junk && Time.time < junkUntil) || (picky && !Useful(t.Letter))) continue;
                float cost = Cost(t.transform.position, at);
                if (cost < bestCost) { bestCost = cost; best = t; }
            }
            return best;
        }

        HeldWeapon LooseGear(Vector3 at, out float bestCost)
        {
            HeldWeapon best = null;
            bestCost = float.MaxValue;
            if (!player.Combat.HasFreeGearSlot) return null;
            foreach (var g in loose)
            {
                if (!g || g.IsHeld || g.IsSpent || !g.isActiveAndEnabled || g.Definition == null
                    || (planGoal != null ? g.word != planGoal || spelledGoal != planGoal : !SpellChoice.Fights(SpellChoice.RoleOf(g.Definition)))
                    || (g == ignoredGear && Time.time < ignoreGearUntil)) continue;
                if (!g.TryGetComponent(out Rigidbody body) || body.isKinematic || !WithinReach(layout, g.transform.position)) continue;
                float cost = Cost(g.transform.position, at);
                if (cost < bestCost) { bestCost = cost; best = g; }
            }
            return best;
        }

        Smashable NearestFurniture(Vector3 at)
        {
            Smashable best = null;
            float bestCost = float.MaxValue;
            foreach (var s in smashables)
            {
                if (!s || !s.isActiveAndEnabled || s.IsBroken || s.Invulnerable || !WithinReach(layout, s.transform.position)) continue;
                float cost = Cost(s.transform.position, at);
                if (planGoal != null && !Yields(s.Word)) cost += NeedlessFurniture;
                if (cost < bestCost) { bestCost = cost; best = s; }
            }
            return best;
        }

        static Smashable[] smashables = Array.Empty<Smashable>();
        static HeldWeapon[] loose = Array.Empty<HeldWeapon>();
        static float scannedAt = float.NegativeInfinity;
        static HouseLayout indexed;
        static int indexedRooms;
        static readonly Dictionary<string, RoomBox> roomIndex = new(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            smashables = Array.Empty<Smashable>();
            loose = Array.Empty<HeldWeapon>();
            scannedAt = float.NegativeInfinity;
            indexed = null;
            roomIndex.Clear();
        }

        static void Scan()
        {
            if (Time.time >= scannedAt && Time.time < scannedAt + ScanSeconds) return;
            scannedAt = Time.time;
            smashables = FindObjectsByType<Smashable>();
            loose = FindObjectsByType<HeldWeapon>();
        }

        static RoomBox Room(HouseLayout layout, string name)
        {
            if (name == null) return null;
            if (!ReferenceEquals(layout, indexed) || layout.Rooms.Count != indexedRooms)
            {
                roomIndex.Clear();
                foreach (var r in layout.Rooms)
                    if (r.Name != null && !roomIndex.ContainsKey(r.Name)) roomIndex.Add(r.Name, r);
                indexed = layout;
                indexedRooms = layout.Rooms.Count;
            }
            return roomIndex.TryGetValue(name, out var room) ? room : null;
        }

        static Vector3 Centre(RoomBox room) => new((room.MinX + room.MaxX) * .5f, room.FloorY, (room.MinZ + room.MaxZ) * .5f);

        static bool SameLevel(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < 1.2f;

        static bool WithinReach(HouseLayout layout, Vector3 p)
        {
            if (FlightUnder(layout, p) != null) return true;
            string room = layout.RoomAt(p.x, p.y, p.z);
            return room == null || p.y - Room(layout, room).FloorY < 1.2f;
        }

        static float Cost(Vector3 thing, Vector3 at) => World.Flat(thing - at).magnitude + Mathf.Abs(thing.y - at.y) * 2f;

        static Vector3 Waypoint(HouseLayout layout, HouseRoutes routes, string start, Vector3 at, Vector3 goal, Func<string, bool> closed)
        {
            if (World.Flat(goal - at).sqrMagnitude < .01f && SameLevel(goal, at)) return goal;
            var goalFlight = FlightUnder(layout, goal);
            foreach (var s in layout.Stairs)
                if (OnFlight(layout, s, at))
                    return s == goalFlight ? goal : StairsWaypoint(layout, s, at, goal.y > (Room(layout, s.Lower).FloorY + Room(layout, s.Upper).FloorY) * .5f);
            string end = layout.RoomAt(goal.x, goal.y, goal.z);
            if (start == null || end == null) return goal;
            if (start == end)
                return goalFlight != null && (goalFlight.Lower == start || goalFlight.Upper == start)
                    ? StairsWaypoint(layout, goalFlight, at, goalFlight.Lower == start)
                    : Around(layout, start, at, goal);
            if (!routes.TryFirstStep(start, at.x, at.z, end, goal.x, goal.z, closed, out var route))
            {
                var middle = Centre(Room(layout, start));
                return World.Flat(middle - at).sqrMagnitude < .16f ? at : Around(layout, start, at, middle);
            }
            string step = route.Room;
            var door = route.Door;
            if (door == null) return StairsWaypoint(layout, route.Stairs, at, route.Stairs.Lower == start);
            var here = Room(layout, start);
            var doorAt = new Vector3(door.X, here.FloorY, door.Z);
            bool inSideWall = (Mathf.Abs(door.X - here.MinX) < .01f || Mathf.Abs(door.X - here.MaxX) < .01f)
                && door.Z > here.MinZ + .01f && door.Z < here.MaxZ - .01f;
            var normal = inSideWall ? new Vector3(Mathf.Sign(door.X - (here.MinX + here.MaxX) * .5f), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(door.Z - (here.MinZ + here.MaxZ) * .5f));
            var offset = at - doorAt;
            float before = -Vector3.Dot(offset, normal), aside = Mathf.Abs(inSideWall ? offset.z : offset.x);
            bool lined = before < .25f || aside < Mathf.Max(.15f, door.Width * .5f - .45f);
            var through = new Vector3(door.X, Room(layout, step).FloorY, door.Z) + normal * .7f;
            return Around(layout, start, at, lined ? through : doorAt - normal * .8f);
        }

        static Vector3 StairsWaypoint(HouseLayout layout, Stairway s, Vector3 at, bool up)
        {
            RoomBox lower = Room(layout, s.Lower), upper = Room(layout, s.Upper), here = up ? lower : upper;
            s.Along(0f, out float fx, out float fz);
            s.Along(s.Run, out float tx, out float tz);
            var foot = new Vector3(fx, lower.FloorY, fz);
            var top = new Vector3(tx, upper.FloorY, tz);
            var forward = World.Flat(top - foot).normalized;
            var side = Vector3.Cross(Vector3.up, forward);
            var offset = World.Flat(at - foot);
            float along = Vector3.Dot(offset, forward), across = Vector3.Dot(offset, side);
            bool inLine = Mathf.Abs(across) < s.Width * .5f;
            Vector3 onto = up ? foot - forward * Stairway.StepOff : top + forward * Stairway.StepOff;
            Vector3 off = up ? top + forward * Stairway.StepOff : foot - forward * Stairway.StepOff;
            if (up)
            {
                float ramp = lower.FloorY + Mathf.Max(0f, along) * (upper.FloorY - lower.FloorY) / s.Run;
                if (inLine && along > -1.2f && along < s.Run + .5f && at.y > ramp - .6f) return off;
                if (along > -.3f) return Around(layout, here.Name, at, Beside(here, onto, side * Mathf.Sign(across) * (s.Width * .5f + .7f)));
                return Around(layout, here.Name, at, onto);
            }
            if (inLine && along < s.Run + 1.2f && (along > s.Run - 1f || OnFlight(layout, s, at)))
                return OnFlight(layout, s, at) ? off : top - forward * .5f;
            if (along < s.Run + .3f) return Around(layout, here.Name, at, Beside(here, onto, side * Mathf.Sign(across) * (s.Width * .5f + .7f)));
            return Around(layout, here.Name, at, onto);
        }

        static Stairway FlightUnder(HouseLayout layout, Vector3 p)
        {
            foreach (var s in layout.Stairs)
                if (OnFlight(layout, s, p)) return s;
            return null;
        }

        static bool Reachable(HouseLayout layout, Vector3 a, Vector3 b)
        {
            var onA = FlightUnder(layout, a);
            var onB = FlightUnder(layout, b);
            if (onA == onB) return true;
            return (onA == null || InLine(onA, b)) && (onB == null || InLine(onB, a));
        }

        static bool InLine(Stairway s, Vector3 p)
        {
            s.Along(0f, out float fx, out float fz);
            s.Along(s.Run, out float tx, out float tz);
            var forward = new Vector2(tx - fx, tz - fz) / s.Run;
            var offset = new Vector2(p.x - fx, p.z - fz);
            float along = Vector2.Dot(offset, forward), across = forward.x * offset.y - forward.y * offset.x;
            return Mathf.Abs(across) < s.Width * .5f && along > -1.5f && along < s.Run + 1.5f;
        }

        const float Skirt = .4f, Rounding = .75f;

        static readonly List<Rect> skirts = new(), blocks = new();
        static readonly List<Vector2> nodes = new();
        static readonly List<int> path = new();
        static readonly Vector2[] corners = new Vector2[4];
        static float[] pathCost = new float[16];
        static int[] pathVia = new int[16];
        static bool[] pathDone = new bool[16];

        static bool Open(Vector2 p, Vector2 q)
        {
            for (int i = 0; i < blocks.Count; i++) if (Crosses(p, q, blocks[i])) return false;
            return true;
        }

        static bool Blocked(Vector2 point)
        {
            for (int i = 0; i < blocks.Count; i++) if (blocks[i].Contains(point)) return true;
            return false;
        }

        static Vector3 Around(HouseLayout layout, string roomName, Vector3 at, Vector3 target)
        {
            var room = Room(layout, roomName);
            var a = new Vector2(at.x, at.z);
            var b = new Vector2(target.x, target.z);
            skirts.Clear();
            blocks.Clear();
            foreach (var s in layout.Stairs)
            {
                if (s.Lower != roomName && s.Upper != roomName) continue;
                var bare = Rect.MinMaxRect(s.MinX, s.MinZ, s.MaxX, s.MaxZ);
                if (bare.Contains(a) || bare.Contains(b)) continue;
                var skirt = Rect.MinMaxRect(s.MinX - Skirt, s.MinZ - Skirt, s.MaxX + Skirt, s.MaxZ + Skirt);
                skirts.Add(skirt);
                blocks.Add(skirt.Contains(a) || skirt.Contains(b) ? bare : skirt);
            }
            if (blocks.Count == 0 || Open(a, b)) return target;
            nodes.Clear();
            nodes.Add(a);
            nodes.Add(b);
            foreach (var r in skirts)
            {
                corners[0] = new Vector2(r.xMin, r.yMin);
                corners[1] = new Vector2(r.xMin, r.yMax);
                corners[2] = new Vector2(r.xMax, r.yMin);
                corners[3] = new Vector2(r.xMax, r.yMax);
                foreach (var corner in corners)
                {
                    var c = corner + new Vector2(corner.x < r.center.x ? -1f : 1f, corner.y < r.center.y ? -1f : 1f) * (Rounding - Skirt);
                    if (c.x > room.MinX + .35f && c.x < room.MaxX - .35f && c.y > room.MinZ + .35f && c.y < room.MaxZ - .35f && !Blocked(c))
                        nodes.Add(c);
                }
            }
            int n = nodes.Count;
            if (pathCost.Length < n)
            {
                pathCost = new float[n * 2];
                pathVia = new int[n * 2];
                pathDone = new bool[n * 2];
            }
            for (int i = 0; i < n; i++)
            {
                pathCost[i] = float.PositiveInfinity;
                pathVia[i] = -1;
                pathDone[i] = false;
            }
            pathCost[0] = 0f;
            for (int k = 0; k < n; k++)
            {
                int u = -1;
                for (int i = 0; i < n; i++) if (!pathDone[i] && (u < 0 || pathCost[i] < pathCost[u])) u = i;
                if (u < 0 || float.IsPositiveInfinity(pathCost[u])) break;
                pathDone[u] = true;
                for (int v = 0; v < n; v++)
                {
                    if (pathDone[v] || !Open(nodes[u], nodes[v])) continue;
                    float c = pathCost[u] + Vector2.Distance(nodes[u], nodes[v]);
                    if (c < pathCost[v]) { pathCost[v] = c; pathVia[v] = u; }
                }
            }
            if (pathVia[1] < 0) return target;
            path.Clear();
            for (int i = 1; i > 0; i = pathVia[i]) path.Add(i);
            path.Reverse();
            int next = path[0];
            if (path.Count > 1 && Vector2.Distance(a, nodes[next]) < .6f) next = path[1];
            return next == 1 ? target : new Vector3(nodes[next].x, room.FloorY, nodes[next].y);
        }

        static bool Crosses(Vector2 p, Vector2 q, Rect r)
        {
            float t0 = 0f, t1 = 1f;
            var d = q - p;
            bool Clip(float den, float num)
            {
                if (Mathf.Abs(den) < 1e-6f) return num > 0f;
                float t = num / den;
                if (den < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
                return true;
            }
            return Clip(-d.x, p.x - r.xMin) && Clip(d.x, r.xMax - p.x) && Clip(-d.y, p.y - r.yMin) && Clip(d.y, r.yMax - p.y) && t1 - t0 > 1e-4f;
        }

        static bool OnFlight(HouseLayout layout, Stairway s, Vector3 at) =>
            s.Covers(at.x, at.z) && at.y > Room(layout, s.Lower).FloorY - .5f && at.y < Room(layout, s.Upper).FloorY + .5f;

        static bool NearStairs(HouseLayout layout, Vector3 p, float margin)
        {
            foreach (var s in layout.Stairs)
                if (p.y > Room(layout, s.Lower).FloorY - .5f && p.y < Room(layout, s.Upper).FloorY + .5f
                    && p.x > s.MinX - margin && p.x < s.MaxX + margin && p.z > s.MinZ - margin && p.z < s.MaxZ + margin) return true;
            return false;
        }

        static Vector3 Beside(RoomBox room, Vector3 onto, Vector3 sideways)
        {
            var p = onto + sideways;
            return new Vector3(Mathf.Clamp(p.x, room.MinX + .5f, room.MaxX - .5f), room.FloorY, Mathf.Clamp(p.z, room.MinZ + .5f, room.MaxZ - .5f));
        }
    }
}
