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

        readonly HashSet<int> hooked = new();
        readonly List<Smashable> props = new();
        readonly HashSet<string> visited = new(StringComparer.Ordinal);
        readonly StringBuilder hudBuffer = new(256);

        static int runSerial;
        float began, phaseEnd, hudNext, slowUntil, roomCheck, nextSupply, nextMini, cueGoUntil;
        int layer, nextLayer;
        bool won, deliveryPicked, miniSpotPicked;
        Vector3 pendingDelivery, pendingMiniSpot;
        string deliveryRoom = "", miniRoom = "", clue = "";
        readonly List<MiniCalculator> minis = new();

        public State Current { get; private set; }
        public BossController Boss => boss;
        public float TimeLeft => Mathf.Max(0f, rules.RoundTimeLimitSeconds - (Time.time - began));

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
                SpawnWordCaches(layer);
                // @trace AMD-007, AMD-008
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
                    if (Time.time >= began + rules.RoundTimeLimitSeconds) Finish(false);
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
                    else if (Time.time >= began + rules.RoundTimeLimitSeconds) Finish(false);
                    break;
                // @trace AMD-007, AMD-008, AMD-010

                case State.Result:
                    hud.SetInstruction(won ? "BOSS DEFEATED" : "THE CALCULATOR WINS",
                        "Press START to fight again");
                    if (joins.AnyStartPressed()) Restart();
                    break;
            }

            TickEventCues();
            // @trace AMD-013

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
            string resultText = Current == State.Result
                ? (won ? "YOU WIN!\n" + names.Name + " is smashed.\nPress START to retry"
                      : "YOU LOSE\nThe Calculator is still shielded.\nPress START to retry")
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

        void Finish(bool victory)
        {
            won = victory;
            aoe.CancelAll();
            ClearMinis();
            hud.HideCountdown();
            Time.timeScale = 1f;
            slowUntil = 0f;
            GameFeedback.Play(victory ? GameCue.Protect : GameCue.Out);
            Debug.Log("[BossFight] finish win=" + victory + " hp=" + boss.Health);
            Advance(State.Result);
        }

        Vector3 ArenaCentre()
        {
            var box = room && room.Layout != null && room.Layout.Rooms.Count > 0 ? room.Layout.Rooms[0] : null;
            if (box == null) return Vector3.up * .2f;
            return new Vector3((box.MinX + box.MaxX) * .5f, box.FloorY + .2f, (box.MinZ + box.MaxZ) * .5f);
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
        /// box falls. A fresh random stream each restart keeps deliveries different every game.
        /// </summary>
        // @trace AMD-007, AMD-013, AMD-015
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
            var box = deliveries.DropAt(false, pendingDelivery);
            deliveryPicked = false;
            if (!box) return;
            props.Add(box);
            Go("SUPPLY DROP - " + deliveryRoom);
            overlay.Flash("SUPPLY DROP!", new Color(.55f, .9f, .45f), .9f);
            Popup.Show("SUPPLY DROP - " + deliveryRoom, box.transform.position + Vector3.up,
                new Color(.55f, .9f, .45f), 1.2f);
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

        Vector3 MiniSpot(Vector3 at)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle;
            if (dir.sqrMagnitude < .0001f) dir = Vector2.up;
            dir.Normalize();
            return new Vector3(at.x + dir.x * 4.5f, ArenaCentre().y, at.z + dir.y * 4.5f);
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

        // Ordinary furniture/items whose own letters can supply CALC / ULA / TOR.
        // Short words are preferred so the full drops fit the 16-letter bag.
        static readonly string[] CacheProps =
        {
            "CLOCK", "CAKE", "BAT", "BALL", "TABLE", "LAMP", "BED", "SOFA",
            "BOOK", "APPLE", "MUG", "SPEAR", "SOAP", "SODA", "PIE", "BOMB", "BROOM", "PLATE",
        };
        static readonly string[] DecoyProps = { "BOOK", "BED", "SOFA", "BAT" };

        /// <summary>
        /// Greedy set cover: picks a few ordinary props whose natural letters contain
        /// every letter of the shield word, including duplicates (CALC needs two C's).
        /// </summary>
        // @trace AMD-005
        public static List<string> CachePropsFor(string word)
        {
            var need = new Dictionary<char, int>();
            foreach (char c in (word ?? "").ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z') continue;
                need.TryGetValue(c, out int count);
                need[c] = count + 1;
            }

            var chosen = new List<string>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            while (need.Count > 0)
            {
                string best = null;
                int bestScore = int.MinValue;
                foreach (string prop in CacheProps)
                {
                    if (used.Contains(prop)) continue;
                    var left = new Dictionary<char, int>(need);
                    int covered = 0;
                    foreach (char c in prop)
                    {
                        if (!left.TryGetValue(c, out int count) || count <= 0) continue;
                        covered++;
                        if (count == 1) left.Remove(c); else left[c] = count - 1;
                    }
                    if (covered == 0) continue;
                    int score = covered * 100 - prop.Length;
                    if (score > bestScore) { bestScore = score; best = prop; }
                }
                if (best == null) break; // curated pool always covers CALC/ULA/TOR
                chosen.Add(best);
                used.Add(best);
                foreach (char c in best)
                {
                    if (!need.TryGetValue(c, out int count)) continue;
                    if (count == 1) need.Remove(c); else need[c] = count - 1;
                }
            }
            return chosen;
        }

        /// <summary>
        /// Shield letters hide inside ordinary items (CLOCK, CAKE, BAT, ...). A prop
        /// keeps its look and releases ALL of its own letters - never a single fake letter.
        /// </summary>
        // @trace MECH-002, MECH-004, AMD-003, AMD-004, AMD-005
        void SpawnWordCaches(int index)
        {
            string word = names.WordFor(index);
            var box = RoomFor(index);
            var caches = CachePropsFor(word);
            var used = new HashSet<string>(caches, StringComparer.Ordinal);

            for (int d = 0; d < 2; d++)
            {
                for (int t = 0; t < DecoyProps.Length; t++)
                {
                    string candidate = DecoyProps[(index * 2 + d + t) % DecoyProps.Length];
                    if (!used.Add(candidate)) continue;
                    caches.Add(candidate);
                    break;
                }
            }

            for (int i = 0; i < caches.Count; i++)
            {
                string propWord = caches[i];
                var prop = FurnitureCatalog.Spawn(propWord, SpotInRoom(box, i, caches.Count), i * 53f, transform);
                if (!prop) continue;
                prop.Drops = null; // no override: smashing releases every letter in the prop's own word
                props.Add(prop);
            }
            clue = word + " is hidden in the furniture in " + (box != null ? box.Name : "the house") + " - smash them!";
            Debug.Log("[BossFight] caches for '" + word + "' in " + (box != null ? box.Name : "house") +
                      ": " + string.Join(", ", caches));
        }

        void ClearProps()
        {
            for (int i = 0; i < props.Count; i++)
                if (props[i]) Destroy(props[i].gameObject);
            props.Clear();
        }
    }
}
