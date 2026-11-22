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

        float began, phaseEnd, hudNext, slowUntil, roomCheck;
        int layer, nextLayer;
        bool won;
        string clue = "";

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

            boss = gameObject.AddComponent<BossController>();
            boss.Configure(rules, names, ArenaCentre());
            aoe = gameObject.AddComponent<TelegraphedAoE>();
            aoe.Configure(rules);
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
            if (joins) joins.Joined -= OnJoined;
        }

        void OnJoined(PlayerController p)
        {
            HookSummoner(p);
            joins.AssignTeams();
            if (Current != State.Preparing && Current != State.Result) p.Frozen = true;
        }

        void HookSummoner(PlayerController p)
        {
            if (!p || !p.Summoner || !hooked.Add(p.Index)) return;
            p.Summoner.Summoned += word => OnSummoned(p, word);
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
            CameraRig.Shake(.22f);
            GameFeedback.Play(GameCue.Protect);
            clue = boss.Vulnerable ? "SHIELD BROKEN - hit it now!" : "Nice! It is staggering.";
            Debug.Log("[BossFight] layer cracked, remaining=" + boss.LayersRemaining);
            nextLayer = layer + 1;
            Advance(boss.Vulnerable ? State.Hit : State.Stagger);
        }

        public void Restart()
        {
            if (Time.timeScale <= 0f) Time.timeScale = 1f;
            slowUntil = 0f;
            if (joins.HumanCount == 0) return;
            room.ResetRoom();
            ClearProps();
            boss.ResetBoss();
            aoe.CancelAll();
            visited.Clear();
            layer = 0;
            nextLayer = 0;
            won = false;
            clue = "Smash furniture to collect letters.";
            hud.HideResult();
            hud.SetTitle("BOSS FIGHT", names.Name);
            foreach (var p in joins.Players)
            {
                joins.Place(p);
                p.Respawn(ArenaCentre());
                p.Frozen = false;
                p.Summoner.WordsOverride = names.SpellWords();
                p.Summoner.SignalWords = names.SignalWords();
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
                CameraRig.Shake(.4f);
                GameFeedback.Play(GameCue.Blast);
                clue = "The Calculator wakes up. Read its name!";
            }
            else if (next == State.DropAndPrevent)
            {
                SpawnClueCrate(layer);
                clue = names.WordFor(layer) + " is hidden in " + ClueRoom(layer) + "!";
            }
            else if (next == State.Hit)
            {
                clue = "Shield down! Hit it with a weapon.";
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
                    hud.SetInstruction("PREP  -  smash props, craft a weapon, scout the rooms",
                        "Q spells  /  E smashes  /  Space dodges");
                    if (Time.time >= roomCheck)
                    {
                        roomCheck = Time.time + .4f;
                        TrackRooms(target);
                    }
                    if (Time.time >= phaseEnd ||
                        (Time.time - began >= 25f && visited.Count >= 3 && HasWeapon()))
                        Advance(State.Appear);
                    break;

                case State.Appear:
                    hud.SetInstruction("THE CALCULATOR APPEARS", "Three words. Three shields.");
                    if (Time.time >= phaseEnd) Advance(State.DropAndPrevent);
                    break;

                case State.DropAndPrevent:
                    hud.SetInstruction("FIND AND SPELL  " + names.WordFor(layer), clue);
                    aoe.Tick(true, layer, target, Time.deltaTime);
                    if (Time.time >= began + rules.RoundTimeLimitSeconds) Finish(false);
                    break;

                case State.Stagger:
                    hud.SetInstruction("STAGGERED", clue);
                    aoe.Tick(false, layer, target, Time.deltaTime);
                    if (Time.time >= phaseEnd)
                    {
                        layer = nextLayer;
                        Advance(State.DropAndPrevent);
                    }
                    break;

                case State.Hit:
                    hud.SetInstruction("HIT THE CALCULATOR", clue);
                    aoe.Tick(true, 3, target, Time.deltaTime);
                    if (boss.Health <= 0f) Finish(true);
                    else if (Time.time >= began + rules.RoundTimeLimitSeconds) Finish(false);
                    break;

                case State.Result:
                    hud.SetInstruction(won ? "BOSS DEFEATED" : "THE CALCULATOR WINS",
                        "Press START to fight again");
                    if (joins.AnyStartPressed()) Restart();
                    break;
            }

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

        Vector3 PlayerPosition()
        {
            foreach (var p in joins.Players)
                if (p && p.Binding is not BotBinding && !p.IsKnockedOut) return p.transform.position;
            return ArenaCentre();
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

        string ClueRoom(int index)
        {
            var rooms = room.Layout.Rooms;
            return rooms.Count == 0 ? "house" : rooms[(index + 1) % rooms.Count].Name;
        }

        Vector3 RoomSpot(int index)
        {
            var rooms = room.Layout.Rooms;
            if (rooms.Count == 0) return ArenaCentre();
            var box = rooms[(index + 1) % rooms.Count];
            return new Vector3((box.MinX + box.MaxX) * .5f, box.FloorY + .35f,
                               (box.MinZ + box.MaxZ) * .5f);
        }

        void SpawnStarterProps()
        {
            string[] words = { "BAT", "WALL", "ZAP", "ROPE" };
            for (int i = 0; i < words.Length; i++)
            {
                var spot = RoomSpot(i);
                var prop = FurnitureCatalog.Spawn(words[i], spot + new Vector3(i * .6f, 0f, 0f), 0f, transform);
                if (prop) props.Add(prop);
            }
        }

        void SpawnClueCrate(int index)
        {
            string word = names.WordFor(index);
            var prop = FurnitureCatalog.Spawn(word, RoomSpot(index), 0f, transform);
            if (!prop) return;
            prop.Init(word, (rules.FurnitureToughnessBase + rules.FurnitureToughnessPerLetter * word.Length)
                             * Smashable.HealthPerBreakPower * 1.5f);
            props.Add(prop);
            Debug.Log("[BossFight] crate '" + word + "' in " + ClueRoom(index));
        }

        void ClearProps()
        {
            for (int i = 0; i < props.Count; i++)
                if (props[i]) Destroy(props[i].gameObject);
            props.Clear();
        }
    }
}
