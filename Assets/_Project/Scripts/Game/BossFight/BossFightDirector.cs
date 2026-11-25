using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.BossFight
{
    /// <summary>
    /// Owns the whole Boss Fight run: Preparing -> Appear -> Drop&amp;Prevent -> Hit -> Result.
    /// Mirrors EscapeDirector's lifecycle (RoundManager disables itself after Configure).
    /// </summary>
    // @trace MECH-001, MECH-004, MECH-010, PLAT-001, LEV-001
    public sealed class BossFightDirector : MonoBehaviour
    {
        public enum State { Preparing, Appear, DropAndPrevent, Stagger, Hit, Result }

        const float StaggerSeconds = 4f;
        const float SlowMotionScale = .35f;
        const float SlowMotionSeconds = .8f;
        const float HudInterval = .2f;

        PlayerJoinManager joins;
        RoomBuilder room;
        DeliverySpawner deliveries;
        GameHud hud;
        GameRules rules;
        BossNameCatalogue names;
        BossController boss;
        TelegraphedAoE aoe;
        BossFightHud overlay;

        /// <summary>A supply prop and the time at which it quietly despawns.</summary>
        // @trace AMD-022
        sealed class SupplyProp
        {
            public Smashable Prop;
            public float ExpiresAt;
        }

        readonly HashSet<int> hooked = new();
        readonly List<Smashable> props = new();
        readonly List<SupplyProp> supplies = new();
        readonly HashSet<string> visited = new(StringComparer.Ordinal);
        readonly StringBuilder hudBuffer = new(256);

        static int runSerial;
        float began, phaseEnd, hudNext, slowUntil, roomCheck, nextSupply, nextMini, cueGoUntil;
        float runStart, clearSeconds;
        int layer, nextLayer, stars;
        bool won, timedOut, deliveryPicked, miniSpotPicked;
        Vector3 pendingDelivery, pendingMiniSpot;
        string deliveryRoom = "", miniRoom = "", clue = "";
        bool boundsReady;
        float boundsMinX, boundsMaxX, boundsMinZ, boundsMaxZ, voidY;
        readonly List<MiniCalculator> minis = new();

        public State Current { get; private set; }
        public BossController Boss => boss;
        public int Stars => stars;
        public float ClearSeconds => clearSeconds;
        /// <summary>One run-wide clock: phase transitions must not reset the limit or the star timer.</summary>
        // @trace AMD-022
        public float TimeLeft => rules == null
            ? 0f : Mathf.Max(0f, rules.RoundTimeLimitSeconds - (Time.time - runStart));

        public void Configure(PlayerJoinManager players, RoomBuilder builder,
            DeliverySpawner boxes, GameHud display)
        {
            joins = players; room = builder; deliveries = boxes; hud = display;
        }

        void Start()
        {
            rules = Match.Rules;
            names = new BossNameCatalogue(rules);
            deliveries.Running = false;
            deliveries.Layout = room.Layout; // BossFight skips RoundManager's usual wiring
            // @trace AMD-007

            boss = gameObject.AddComponent<BossController>();
            boss.Configure(rules, names, ArenaCentre());
            aoe = gameObject.AddComponent<TelegraphedAoE>();
            aoe.Configure(rules, boss); // smash circles drive the boss's real leap
            aoe.SmashTelegraphed += OnSmashTelegraphed;
            // @trace AMD-012, AMD-013
            overlay = gameObject.AddComponent<BossFightHud>();
            overlay.Build(transform, rules, names);

            joins.Joined += OnJoined;
            joins.AllowJoining = false;
            joins.RespawnKnockedOut = true;
            foreach (var p in joins.Players) HookSummoner(p);

            Debug.Log("[BossFight] build v1 boss=" + names.Name +
                      " words=" + string.Join("/", rules.BossWords ?? Array.Empty<string>()) +
                      " humans=" + joins.HumanCount);
            if (joins.HumanCount > 0) Restart();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (aoe) aoe.SmashTelegraphed -= OnSmashTelegraphed;
            if (joins) joins.Joined -= OnJoined;
        }

        void OnJoined(PlayerController p)
        {
            HookSummoner(p);
            SetWords(p);
            joins.AssignTeams();
            if (Current != State.Preparing && Current != State.Result) p.Frozen = true;
        }

        void HookSummoner(PlayerController p)
        {
            if (!p || !p.Summoner || !hooked.Add(p.Index)) return;
            p.Summoner.Summoned += word => OnSummoned(p, word);
        }

        /// <summary>Keep every player's recipe list intact and add the shield words as signals.</summary>
        // @trace MECH-002, MECH-003
        void SetWords(PlayerController p)
        {
            if (!p || !p.Summoner) return;
            p.Summoner.WordsOverride = names.SpellWords();
            p.Summoner.SignalWords = names.SignalWords();
        }

        /// <summary>Spelling the current shield word cracks one layer. Chaos items still summon normally.</summary>
        // @trace MECH-003, MECH-005
        void OnSummoned(PlayerController p, string word)
        {
            if (Current != State.DropAndPrevent || string.IsNullOrEmpty(word)) return;
            if (!string.Equals(word, names.WordFor(layer), StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[BossFight] spelled '" + word + "' but layer " + (layer + 1) +
                          " needs '" + names.WordFor(layer) + "'");
                return;
            }
            CrackShield();
        }

        void CrackShield()
        {
            boss.CrackLayer();
            aoe.CancelAll();
            slowUntil = Time.unscaledTime + SlowMotionSeconds;
            Time.timeScale = SlowMotionScale;
            CameraRig.Shake(.35f);
            GameFeedback.Play(GameCue.Protect);
            if (boss.Vulnerable)
            {
                overlay.Flash("SHIELD BROKEN!", new Color(1f, .68f, .16f), 2.2f);
                Go("SHIELD DOWN - HIT NOW");
            }
            else overlay.Flash("LAYER " + boss.LayersRemaining + " DOWN!",
                new Color(1f, .84f, .42f), 1.1f);
            clue = boss.Vulnerable ? "SHIELD BROKEN - hit it now!" : "Nice! It is staggering.";
            // @trace AMD-009
            Debug.Log("[BossFight] layer cracked, remaining=" + boss.LayersRemaining);
            nextLayer = layer + 1;
            Advance(boss.Vulnerable ? State.Hit : State.Stagger);
        }

        public void Restart()
        {
            if (Time.timeScale <= 0f) Time.timeScale = 1f;
            slowUntil = 0f;
            if (joins.HumanCount == 0) return;
            // Fresh stream per run so delivery rooms and smoke spots are never the same game to game.
            long seed = DateTime.UtcNow.Ticks + (runSerial++) * 7919L;
            UnityEngine.Random.InitState(unchecked((int)(seed ^ (seed >> 32))));
            // @trace AMD-015
            // Boss Fight deliberately starts with a bare house. The authored level ships 35
            // smashables; keeping them plus the starter props made loot trivial to find.
            // Only the five props spawned below exist when the prepare phase begins.
            // @trace AMD-011
            room.ResetRoom(furnish: false);
            ClearProps();
            ClearMinis();
            boss.ResetBoss();
            boss.SetVisible(false);
            overlay.SetRevealed(false);
            aoe.CancelAll();
            visited.Clear();
            layer = 0;
            nextLayer = 0;
            won = false;
            timedOut = false;
            stars = 0;
            clearSeconds = 0f;
            runStart = Time.time;
            nextSupply = 0f;
            nextMini = 0f;
            deliveryPicked = false;
            miniSpotPicked = false;
            deliveryRoom = miniRoom = "";
            clue = "Smash furniture to collect letters.";
            hud.HideResult();
            hud.SetTitle("BOSS FIGHT", "???");
            // @trace AMD-006
            foreach (var p in joins.Players)
            {
                joins.Place(p);
                p.Respawn(ArenaCentre());
                p.Frozen = false;
                SetWords(p);
            }
            joins.AssignTeams();
            SpawnStarterProps();
            Advance(State.Preparing);
            Debug.Log("[BossFight] restart: preparing " + rules.PrepSeconds + "s");
        }

        void Advance(State next)
        {
            Current = next;
            began = Time.time;
            float duration = next switch
            {
                State.Preparing => rules.PrepSeconds,
                State.Appear => rules.AppearSeconds,
                State.DropAndPrevent => rules.LayerSeconds,
                State.Stagger => StaggerSeconds,
                State.Hit => rules.HitSeconds,
                _ => 0f,
            };
            phaseEnd = began + duration;

            if (next == State.Appear)
            {
                // Countdown is over: reveal the boss, its name and the shield HUD together.
                // @trace AMD-006
                hud.HideCountdown();
                Go("THE CALCULATOR WAKES");
                hud.SetTitle("BOSS FIGHT", names.Name);
                boss.SetVisible(true);
                overlay.SetRevealed(true);
                overlay.Flash(names.Name + "!", new Color(1f, .68f, .16f), 1.6f);
                CameraRig.Shake(.4f);
                GameFeedback.Play(GameCue.Blast);
                clue = "The Calculator wakes up. Read its name!";
            }
            else if (next == State.DropAndPrevent)
            {
                nextSupply = Time.time + rules.SupplyDropSeconds;
                nextMini = Time.time + rules.MiniFirstSeconds;
                // No per-shield cache: every prop now arrives through the random supply drop.
                // @trace AMD-007, AMD-008, AMD-018
                clue = "Smash props and catch supply drops to collect letters.";
            }
            else if (next == State.Hit)
            {
                clue = "Shield down! Hit it, dodge circles and jump smashes.";
                overlay.Flash("HIT THE CALCULATOR!", new Color(1f, .82f, .3f), 1.4f);
            }
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (slowUntil > 0f && Time.unscaledTime >= slowUntil)
            {
                Time.timeScale = 1f;
                slowUntil = 0f;
            }

            RescueStrays();
            // One run-wide limit. Reaching it ends the round with a star rating, never a defeat.
            // @trace AMD-022
            if (runStart > 0f && rules != null && Current != State.Result &&
                Time.time >= runStart + rules.RoundTimeLimitSeconds)
            {
                Finish(false, true);
            }
            Vector3 target = PlayerPosition();
            switch (Current)
            {
                case State.Preparing:
                    int left = Mathf.Max(1, Mathf.CeilToInt(phaseEnd - Time.time));
                    hud.ShowCountdown(left, "PREPARE - SMASH & CRAFT");
                    hud.SetInstruction("PREPARE  " + left + "s  -  smash props, craft a weapon",
                        "Q spells  /  E smashes  /  Space dodges");
                    if (Time.time >= roomCheck)
                    {
                        roomCheck = Time.time + .4f;
                        TrackRooms(target);
                    }
                    if (Time.time >= phaseEnd) Advance(State.Appear);
                    break;
                // @trace AMD-006

                case State.Appear:
                    hud.SetInstruction("THE CALCULATOR APPEARS", "Three words. Three shields.");
                    if (Time.time >= phaseEnd) Advance(State.DropAndPrevent);
                    break;

                case State.DropAndPrevent:
                    hud.SetInstruction("FIND AND SPELL  " + names.WordFor(layer), clue);
                    TickHazards(target, layer, true, false);
                    break;

                case State.Stagger:
                    hud.SetInstruction("STAGGERED", clue);
                    TickHazards(target, layer, false, false);
                    if (Time.time >= phaseEnd)
                    {
                        layer = nextLayer;
                        Advance(State.DropAndPrevent);
                    }
                    break;

                case State.Hit:
                    hud.SetInstruction("HIT THE CALCULATOR", clue);
                    TickHazards(target, 3, true, true);
                    if (boss.Health <= 0f) Finish(true);
                    break;
                // @trace AMD-007, AMD-008, AMD-010

                case State.Result:
                    hud.SetInstruction((timedOut ? "TIME'S UP" : "BOSS DEFEATED") + " - " + stars + "/3 STARS",
                        "Press START to fight again");
                    if (joins.AnyStartPressed()) Restart();
                    break;
            }

            TickEventCues();
            // @trace AMD-013
            TickSupplyLifetime();
            // @trace AMD-022

            if (Time.time >= hudNext)
            {
                hudNext = Time.time + HudInterval;
                RefreshHud();
            }
            hud.SetChecklist(Checklist());
            hud.SetTimer(RoundManager.FormatTime(TimeLeft));
        }

        void RefreshHud()
        {
            hudBuffer.Clear();
            int total = names.LayerCount;
            for (int i = 0; i < boss.LayersRemaining; i++) hudBuffer.Append('\u25cf');
            for (int i = boss.LayersRemaining; i < total; i++) hudBuffer.Append('\u25cb');

            string shieldText = "SHIELD  " + hudBuffer;
            hudBuffer.Clear();
            for (int i = 0; i < total; i++)
            {
                if (i > 0) hudBuffer.Append("   ");
                if (i == layer && Current == State.DropAndPrevent) hudBuffer.Append('<').Append(names.WordFor(i)).Append('>');
                else hudBuffer.Append(names.WordFor(i));
            }
            string starLine = StarGlyphs(stars) + "  " + stars + "/3 STARS";
            string resultText = Current == State.Result
                ? (!won
                    ? "TIME'S UP\n" + starLine + "\n" + RoundManager.FormatTime(clearSeconds) + " fought  -  START to retry"
                    : "BOSS DEFEATED!\n" + starLine + "\n" + RoundManager.FormatTime(clearSeconds) + " clear  -  START to retry")
                : "";
            overlay.Refresh(shieldText, hudBuffer.ToString(), clue,
                RoundManager.FormatTime(TimeLeft), boss.MaxHealth > 0f ? boss.Health / boss.MaxHealth : 0f,
                boss.Vulnerable, resultText);
        }

        string Checklist()
        {
            hudBuffer.Clear();
            hudBuffer.Append(visited.Count).Append("/3 rooms scouted   ");
            hudBuffer.Append(boss.LayersRemaining).Append(" shield layer");
            if (boss.LayersRemaining != 1) hudBuffer.Append('s');
            return hudBuffer.ToString();
        }

        /// <summary>Rating by clear time: fast runs get three stars, slow ones still finish with one.</summary>
        // @trace AMD-022
        public static int StarRating(float elapsedSeconds, GameRules gameRules)
        {
            if (gameRules == null) return 1;
            if (elapsedSeconds <= gameRules.StarThreeSeconds) return 3;
            if (elapsedSeconds <= gameRules.StarTwoSeconds) return 2;
            return 1;
        }

        static string StarGlyphs(int count)
        {
            int n = Mathf.Clamp(count, 0, 3);
            return new string('\u2605', n) + new string('\u2606', 3 - n);
        }

        /// <summary>Timeout is a rated finish, not a defeat: no loss wording anywhere in the result.</summary>
        // @trace AMD-022
        void Finish(bool victory, bool timeout = false)
        {
            won = victory;
            timedOut = timeout;
            clearSeconds = Mathf.Max(0f, Time.time - runStart);
            stars = StarRating(clearSeconds, rules);
            aoe.CancelAll();
            ClearMinis();
            hud.HideCountdown();
            Time.timeScale = 1f;
            slowUntil = 0f;
            GameFeedback.Play(victory ? GameCue.Protect : GameCue.Out);
            Debug.Log("[BossFight] finish win=" + victory + " timeout=" + timeout +
                      " stars=" + stars + " elapsed=" + clearSeconds.ToString("F1") + " hp=" + boss.Health);
            Advance(State.Result);
        }

        Vector3 ArenaCentre()
        {
            var box = room && room.Layout != null && room.Layout.Rooms.Count > 0 ? room.Layout.Rooms[0] : null;
            if (box == null) return Vector3.up * .2f;
            return new Vector3((box.MinX + box.MaxX) * .5f, box.FloorY + .2f, (box.MinZ + box.MaxZ) * .5f);
        }

        /// <summary>
        /// Safety net: if a knock or a moving hitbox ever puts someone outside the house, put them
        /// back at the arena instead of letting them fall into the void.
        /// </summary>
        // @trace MECH-006, AMD-019
        void RescueStrays()
        {
            if (!boundsReady) CacheBounds();
            if (!boundsReady || !joins) return;
            foreach (var p in joins.Players)
            {
                if (!p || p.IsEliminated) continue;
                var at = p.transform.position;
                bool outside = at.y < voidY || at.x < boundsMinX || at.x > boundsMaxX ||
                               at.z < boundsMinZ || at.z > boundsMaxZ;
                if (!outside) continue;
                p.Respawn(ArenaCentre());
                p.Frozen = false;
                overlay.Flash("BACK IN THE HOUSE!", new Color(.55f, .9f, .45f), .9f);
                Debug.Log("[BossFight] rescued a player from outside the house at " + at);
            }
        }

        void CacheBounds()
        {
            var rooms = room && room.Layout != null ? room.Layout.Rooms : null;
            if (rooms == null || rooms.Count == 0) return;
            boundsMinX = boundsMaxX = rooms[0].MinX;
            boundsMinZ = boundsMaxZ = rooms[0].MinZ;
            voidY = rooms[0].FloorY;
            foreach (var r in rooms)
            {
                boundsMinX = Mathf.Min(boundsMinX, r.MinX - 2f);
                boundsMaxX = Mathf.Max(boundsMaxX, r.MaxX + 2f);
                boundsMinZ = Mathf.Min(boundsMinZ, r.MinZ - 2f);
                boundsMaxZ = Mathf.Max(boundsMaxZ, r.MaxZ + 2f);
                voidY = Mathf.Min(voidY, r.FloorY - 5f);
            }
            boundsReady = true;
        }

        PlayerController Human()
        {
            foreach (var p in joins.Players)
                if (p && p.Binding is not BotBinding && !p.IsKnockedOut) return p;
            return null;
        }

        Vector3 PlayerPosition()
        {
            var p = Human();
            return p ? p.transform.position : ArenaCentre();
        }

        /// <summary>One place for all phase hazards: circles, supply drops, minis and jump smash.</summary>
        // @trace AMD-007, AMD-008, AMD-010
        void TickHazards(Vector3 target, int layer, bool circles, bool smash)
        {
            aoe.Tick(circles, layer, target, Time.deltaTime);
            aoe.TickSmash(smash, target, Time.deltaTime);
            TickSupplies();
            TickMinis(target);
        }

        /// <summary>
        /// The landing spot is picked during the 3-2-1 cue and its room is announced before the
        /// props fall. Each drop is a random handful of ordinary items - no letter targeting.
        /// </summary>
        // @trace AMD-007, AMD-013, AMD-015, AMD-018
        void TickSupplies()
        {
            if (nextSupply <= 0f) return;
            float left = nextSupply - Time.time;
            if (left <= rules.EventWarnSeconds && !deliveryPicked)
            {
                pendingDelivery = deliveries.PickDropPoint();
                deliveryRoom = RoomNameAt(pendingDelivery);
                deliveryPicked = true;
                clue = "Supply drop incoming in " + deliveryRoom + " - get there!";
                overlay.Flash("SUPPLY INCOMING - " + deliveryRoom.ToUpperInvariant(),
                    new Color(.55f, .9f, .45f), .9f);
            }
            if (left > 0f) return;

            nextSupply = Time.time + rules.SupplyDropSeconds;
            var dropped = deliveries.DropPropsAt(pendingDelivery, rules.SupplyPropsPerDrop, SupplyProps);
            deliveryPicked = false;
            if (dropped.Length == 0) return;
            props.AddRange(dropped);
            // Supplies are temporary; starter props stay until smashed. A long run no longer
            // buries the house in accumulated furniture.
            // @trace AMD-022
            float expires = Time.time + rules.SupplyPropLifetimeSeconds;
            foreach (var prop in dropped) supplies.Add(new SupplyProp { Prop = prop, ExpiresAt = expires });
            Go("SUPPLY DROP - " + deliveryRoom);
            overlay.Flash("SUPPLY DROP!", new Color(.55f, .9f, .45f), .9f);
            Popup.Show("SUPPLY DROP - " + deliveryRoom, pendingDelivery + Vector3.up,
                new Color(.55f, .9f, .45f), 1.2f);
            clue = "Supply items landed in " + deliveryRoom + " - smash them!";
        }

        /// <summary>Supply props quietly expire after their lifetime; smashed ones just leave the list.</summary>
        // @trace AMD-022
        void TickSupplyLifetime()
        {
            if (rules == null || rules.SupplyPropLifetimeSeconds <= 0f) return;
            for (int i = supplies.Count - 1; i >= 0; i--)
            {
                var entry = supplies[i];
                if (entry.Prop && Time.time < entry.ExpiresAt) continue;
                if (entry.Prop)
                {
                    var at = entry.Prop.transform.position + Vector3.up * .5f;
                    GameFeedback.Burst("Wood_Splinter", at, .3f, new Color(.8f, .82f, .86f), .3f);
                    Destroy(entry.Prop.gameObject);
                }
                supplies.RemoveAt(i);
            }
        }

        /// <summary>Mini calculators appear on a timer, chase the human, and can be smashed for letters.</summary>
        // @trace AMD-008, AMD-013
        void TickMinis(Vector3 at)
        {
            var human = Human();
            for (int i = minis.Count - 1; i >= 0; i--)
            {
                var mini = minis[i];
                if (!mini || mini.Broken) { minis.RemoveAt(i); continue; }
                mini.Tick(human, Time.deltaTime);
            }

            if (nextMini <= 0f || !human) return;
            float left = nextMini - Time.time;
            if (left <= rules.EventWarnSeconds && !miniSpotPicked)
            {
                pendingMiniSpot = MiniSpot(at);
                miniRoom = RoomNameAt(pendingMiniSpot);
                miniSpotPicked = true;
                clue = "Mini calculators inbound near " + miniRoom + "!";
            }
            if (left > 0f) return;

            nextMini = Time.time + rules.MiniIntervalSeconds;
            if (minis.Count >= rules.MiniMaxAlive) { miniSpotPicked = false; return; }
            var spot = miniSpotPicked ? pendingMiniSpot : MiniSpot(at);
            miniSpotPicked = false;

            var go = new GameObject("Mini calculator");
            var miniCalc = go.AddComponent<MiniCalculator>();
            miniCalc.Configure(rules, spot);
            minis.Add(miniCalc);
            Go("MINIS - " + miniRoom);
            overlay.Flash("MINI CALCULATORS!", new Color(1f, .55f, .2f), .9f);
        }

        /// <summary>
        /// Mini spawns stay inside a room and near the player. A far random-room fallback made
        /// waves look as if they never arrived; clamping the offset keeps them in view.
        /// </summary>
        // @trace AMD-020, AMD-021
        Vector3 MiniSpot(Vector3 at)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle;
            if (dir.sqrMagnitude < .0001f) dir = Vector2.up;
            dir.Normalize();
            var spot = new Vector3(at.x + dir.x * 4.5f, at.y, at.z + dir.y * 4.5f);
            var rooms = room && room.Layout != null ? room.Layout.Rooms : null;
            if (rooms == null || rooms.Count == 0) return spot;
            foreach (var r in rooms)
                if (r.Contains(spot.x, spot.z) && Mathf.Abs(r.FloorY - at.y) < RoomBox.StoreyTolerance)
                    return spot;

            var currentName = room.Layout.RoomAt(at.x, at.y, at.z);
            var current = !string.IsNullOrEmpty(currentName) ? room.Layout.Room(currentName) : null;
            if (current != null)
            {
                float insetX = Mathf.Min(1f, (current.MaxX - current.MinX) * .25f);
                float insetZ = Mathf.Min(1f, (current.MaxZ - current.MinZ) * .25f);
                return new Vector3(
                    Mathf.Clamp(spot.x, current.MinX + insetX, current.MaxX - insetX),
                    current.FloorY + .2f,
                    Mathf.Clamp(spot.z, current.MinZ + insetZ, current.MaxZ - insetZ));
            }
            var home = rooms[UnityEngine.Random.Range(0, rooms.Count)];
            return new Vector3((home.MinX + home.MaxX) * .5f, home.FloorY + .2f,
                (home.MinZ + home.MaxZ) * .5f);
        }

        string RoomNameAt(Vector3 at)
        {
            string name = room && room.Layout != null ? room.Layout.RoomAt(at.x, at.z) : null;
            return string.IsNullOrEmpty(name) ? "the house" : name;
        }

        /// <summary>The smash marker itself is the boss's landing spot; announce it as soon as it appears.</summary>
        // @trace AMD-012, AMD-013
        void OnSmashTelegraphed(Vector3 at)
        {
            Go("JUMP SMASH - DODGE!");
            overlay.Flash("JUMP SMASH - " + RoomNameAt(at).ToUpperInvariant(),
                new Color(1f, .55f, .12f), .9f);
            clue = "Jump smash! Get out of the circle.";
        }

        /// <summary>One top-chip 3-2-1 countdown for whichever timed event is closest.</summary>
        // @trace AMD-013
        void TickEventCues()
        {
            if (Current != State.DropAndPrevent && Current != State.Hit) return;
            if (Time.unscaledTime < cueGoUntil) return;
            float warn = rules != null ? rules.EventWarnSeconds : 3f;
            float soonest = float.MaxValue;
            string label = null;
            Offer("SUPPLY DROP IN", nextSupply, ref soonest, ref label);
            Offer("MINIS INCOMING", nextMini, ref soonest, ref label);
            if (Current == State.Hit && aoe)
                Offer("JUMP SMASH IN", Time.time + aoe.SmashIn, ref soonest, ref label);
            if (label == null) return;
            float left = soonest - Time.time;
            if (left < 0f || left > warn) return;
            hud.ShowCountdown(Mathf.Max(1, Mathf.CeilToInt(left)), label);
        }

        static void Offer(string label, float at, ref float soonest, ref string chosen)
        {
            if (at <= 0f || at >= soonest) return;
            soonest = at;
            chosen = label;
        }

        /// <summary>A GO line owns the chip briefly so a following countdown cannot stomp it.</summary>
        // @trace AMD-013
        void Go(string line)
        {
            hud.ShowGo(line);
            cueGoUntil = Time.unscaledTime + .8f;
        }

        void ClearMinis()
        {
            for (int i = 0; i < minis.Count; i++)
                if (minis[i]) Destroy(minis[i].gameObject);
            minis.Clear();
            nextMini = 0f;
        }

        bool HasWeapon()
        {
            foreach (var p in joins.Players)
                if (p && p.Combat && p.Combat.Weapon) return true;
            return false;
        }

        void TrackRooms(Vector3 at)
        {
            string name = room.Layout.RoomAt(at.x, at.y, at.z);
            if (!string.IsNullOrEmpty(name)) visited.Add(name);
        }

        /// <summary>Spreads props across a room instead of stacking them in one obvious pile.</summary>
        Vector3 SpotInRoom(RoomBox box, int slot, int count)
        {
            if (box == null) return ArenaCentre();
            float insetX = Mathf.Min(1.1f, (box.MaxX - box.MinX) * .25f);
            float insetZ = Mathf.Min(1.1f, (box.MaxZ - box.MinZ) * .25f);
            float t = count <= 1 ? .5f : (slot + 1f) / (count + 1f);
            float along = slot % 2 == 0 ? t : 1f - t;
            return new Vector3(
                Mathf.Lerp(box.MinX + insetX, box.MaxX - insetX, along),
                box.FloorY + .35f,
                Mathf.Lerp(box.MinZ + insetZ, box.MaxZ - insetZ, t));
        }

        RoomBox RoomFor(int index)
        {
            var rooms = room && room.Layout != null ? room.Layout.Rooms : null;
            return rooms != null && rooms.Count > 0 ? rooms[(index + 1) % rooms.Count] : null;
        }

        void SpawnStarterProps()
        {
            // Exactly five ordinary props in the WHOLE house at the start; authored furniture
            // was cleared in Restart, so this is the complete starting loot. Every prop drops
            // its own letters - no fake one-letter or cross-word loot.
            // @trace AMD-004, AMD-005, AMD-006, AMD-011
            string[] starter = { "BAT", "LAMP", "BALL", "ZAP", "TABLE" };
            int count = Mathf.Clamp(rules.StarterProps, 1, starter.Length);
            for (int i = 0; i < count; i++)
            {
                var prop = FurnitureCatalog.Spawn(starter[i], SpotInRoom(RoomFor(i), i, count), i * 37f, transform);
                if (prop) props.Add(prop);
            }
        }

        // General pool of recognisable items. No word is picked to satisfy a shield; drops are
        // random each run, and each prop still releases its own complete word when smashed.
        // Historical AMD-003/004/005 caches are replaced by this general pool.
        // @trace AMD-003, AMD-004, AMD-005, AMD-018
        public static readonly string[] SupplyProps =
        {
            "CLOCK", "CAKE", "BAT", "BALL", "TABLE", "LAMP", "BED", "SOFA",
            "BOOK", "APPLE", "MUG", "SPEAR", "SOAP", "SODA", "PIE", "BOMB", "BROOM", "PLATE",
        };

        void ClearProps()
        {
            for (int i = 0; i < props.Count; i++)
                if (props[i]) Destroy(props[i].gameObject);
            props.Clear();
            supplies.Clear();
        }
    }
}
