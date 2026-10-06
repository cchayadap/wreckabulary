using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Co-op "Moving Day": boxes labelled with the checklist arrive at the front door. Smash them for
    /// the letters, spell each item to build it, and put it in the right room before time runs out.
    /// Stars depend on how much time is left. 1–4 roommates.
    /// </summary>
    public class MovingDayDirector : MonoBehaviour
    {
        [Serializable]
        public class Room
        {
            public string name;
            public Vector2 xRange, zRange;
            public float floorY, ceilingY = float.PositiveInfinity;
            public bool Contains(Vector3 p) => p.x >= xRange.x && p.x <= xRange.y && p.z >= zRange.x && p.z <= zRange.y
                && p.y > floorY - 1f && p.y < ceilingY - 0.5f;
            public Vector3 Centre => new((xRange.x + xRange.y) * 0.5f, floorY, (zRange.x + zRange.y) * 0.5f);
        }

        [Serializable]
        public class Item
        {
            public string word;
            public string room;
        }

        [Serializable]
        public class Level
        {
            public string name;
            public float timeLimit = 150f;
            [Tooltip("Puddles appear now and then, making anyone standing in them slide about.")]
            public bool spills;
            public Item[] items;
        }

        public enum State { Countdown, Playing, Complete, OutOfTime }

        [SerializeField] PlayerJoinManager joins;
        [SerializeField] GameHud hud;
        [SerializeField] Transform deliverySpot;
        [SerializeField] Room[] rooms;
        [SerializeField] Level[] levels;
        [SerializeField] float countdownTime = 3f;
        [Tooltip("Fraction of the time limit left for 3 and 2 stars. Finishing at all is 1 star.")]
        [SerializeField] Vector2 starThresholds = new(0.5f, 0.25f);

        readonly List<Item> remaining = new();
        readonly HashSet<Smashable> placed = new();
        float deliveryDrop = 2.5f;
        readonly List<Transform> puddles = new();
        List<WordEntry> checklistWords = new();
        float stateStarted, nextCheck, nextResupply, nextSpill;
        readonly StringBuilder sb = new();
        const float DeliveryGap = 1.2f;
        RoomBuilder layoutBuilder;

        public State Current { get; private set; }
        public int LevelIndex { get; private set; }
        public Level CurrentLevel => levels[LevelIndex];
        public int LevelCount => levels.Length;
        public float TimeLeft { get; set; }
        public int Stars { get; private set; }
        public IReadOnlyList<Item> Remaining => remaining;
        public IReadOnlyList<Room> Rooms => rooms;
        public Room RoomNamed(string name) => rooms.First(r => r.name == name);

        /// <summary>Called by the scene builder.</summary>
        public void Configure(Room[] newRooms, Level[] newLevels)
        {
            rooms = newRooms;
            levels = newLevels;
        }

        void Start()
        {
            layoutBuilder = GetComponent<RoomBuilder>();
            if (!layoutBuilder) layoutBuilder = gameObject.AddComponent<RoomBuilder>();
            var house = layoutBuilder.Layout;
            var floors = house.StoreyFloors();
            rooms = house.Rooms.Select(r => new Room
            {
                name = r.Name, xRange = new Vector2(r.MinX, r.MaxX), zRange = new Vector2(r.MinZ, r.MaxZ), floorY = r.FloorY,
                ceilingY = house.StoreyOf(r) + 1 < floors.Count ? floors[house.StoreyOf(r) + 1] : float.PositiveInfinity,
            }).ToArray();
            levels = new[] { new Level { name = house.Name, timeLimit = Match.Rules.RoundTimeLimitSeconds,
                items = house.MovingDay.Select(i => new Item { word = i.Word, room = i.Room }).ToArray() } };
            if (!deliverySpot) deliverySpot = new GameObject("Delivery spot").transform;
            var deliveryRoom = house.Room(house.ExtractionRoom);
            deliverySpot.position = new Vector3(house.ExtractionX, deliveryRoom.FloorY, house.ExtractionZ);
            int deliveryStorey = house.StoreyOf(deliveryRoom);
            deliveryDrop = deliveryStorey + 1 < floors.Count ? Mathf.Min(2.5f, floors[deliveryStorey + 1] - .24f - 1f - deliveryRoom.FloorY) : 2.5f;
            joins.RespawnKnockedOut = true;
            joins.Joined += ConfigurePlayer;
            StartLevel(0);
        }

        void ConfigurePlayer(PlayerController p)
        {
            joins.AssignTeams();
            p.Health.ResetForRound();
            var objectives = new HashSet<string>(checklistWords.Select(w => w.word));
            p.Summoner.ChecklistPlacementWords = objectives;
            p.Summoner.WordsOverride = checklistWords.Concat(GameAssets.I.words.Words.Where(w => !objectives.Contains(w.word))).ToList();
            p.Inventory.Set("");
            p.Frozen = Current != State.Playing;
            if (joins.Players.Count == 1 && Current == State.Countdown) stateStarted = Time.time;
        }

        public void Retry() => StartLevel(LevelIndex);

        public void StartLevel(int index)
        {
            LevelIndex = index;
            StopAllCoroutines();
            layoutBuilder.ResetRoom(furnish: false);
            hud.HideResult();
            hud.HideCountdown();
            MatchTally.BeginMatch();
            foreach (var puddle in puddles) if (puddle) Destroy(puddle.gameObject);
            puddles.Clear();
            placed.Clear();

            remaining.Clear();
            remaining.AddRange(CurrentLevel.items);
            // Each word only once, even if the checklist wants two of something.
            checklistWords = CurrentLevel.items.Select(i => i.word.ToUpperInvariant()).Distinct()
                .Select(w => new WordEntry { word = w, category = WordCategory.Furniture }).ToList();

            foreach (var p in joins.Players)
            {
                joins.Place(p);
                ConfigurePlayer(p);
                p.Frozen = true;
            }
            TimeLeft = CurrentLevel.timeLimit;
            nextSpill = Time.time + countdownTime + 15f;
            SetState(State.Countdown);
        }

        void SetState(State s)
        {
            Current = s;
            stateStarted = Time.time;
        }

        void Update()
        {
            float t = Time.time - stateStarted;
            switch (Current)
            {
                case State.Countdown:
                    if (joins.Players.Count == 0)
                    {
                        hud.HideCountdown();
                        stateStarted = Time.time;
                        break;
                    }
                    int left = Mathf.CeilToInt(countdownTime - t);
                    hud.ShowCountdown(Mathf.Max(1, left), $"LEVEL {LevelIndex + 1} · {CurrentLevel.name.ToUpperInvariant()}");
                    if (t >= countdownTime) BeginPlay();
                    break;

                case State.Playing:
                    TimeLeft -= Time.deltaTime;
                    CheckPlacements();
                    Resupply();
                    if (CurrentLevel.spills) Spills();
                    if (remaining.Count == 0) Finish();
                    else if (TimeLeft <= 0f) OutOfTime();
                    break;

                case State.Complete:
                case State.OutOfTime:
                    if (hud.ResultShown && t > 1.6f && joins.AnyStartPressed()) hud.ConfirmResult();
                    break;
            }

            hud.SetTimer(Current == State.Playing || Current == State.Countdown ? FormatTime(TimeLeft) : "");
            hud.SetChecklist(ChecklistText());
            hud.SetInstruction(joins.Players.Count == 0 ? ControlHints.Join("join") : "", "");
            hud.SetScoreboard(joins.Players, _ => 0, 0, false);
        }

        void BeginPlay()
        {
            SetState(State.Playing);
            hud.ShowGo();
            hud.Toast("Spell the checklist furniture and put each piece in its room.");
            foreach (var p in joins.Players) p.Frozen = false;
            // Don't resupply until the first round of boxes has arrived.
            nextResupply = Time.time + CurrentLevel.items.Length * DeliveryGap + 3f;
            StartCoroutine(DeliverAll());
        }

        IEnumerator DeliverAll()
        {
            foreach (var item in CurrentLevel.items)
            {
                Deliver(item.word);
                yield return new WaitForSeconds(DeliveryGap);
            }
        }

        void Deliver(string word)
        {
            var spread = new Vector3(UnityEngine.Random.Range(-1.8f, 1.8f), 0f, UnityEngine.Random.Range(-1.8f, 1.8f));
            DeliverySpawner.CreateBox(word, deliverySpot.position + spread + Vector3.up * deliveryDrop);
        }

        /// <summary>Anything built, at rest, and inside its room gets ticked off and locked in place.</summary>
        void CheckPlacements()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;

            foreach (var s in FindObjectsByType<Smashable>())
            {
                if (placed.Contains(s) || !s.TryGetComponent(out LetterBuilt _)) continue;
                var rb = s.GetComponent<Rigidbody>();
                if (!rb || rb.isKinematic || rb.linearVelocity.sqrMagnitude > 0.25f) continue;

                var item = remaining.FirstOrDefault(i => i.word == s.Word && RoomNamed(i.room).Contains(rb.worldCenterOfMass));
                if (item == null) continue;

                remaining.Remove(item);
                placed.Add(s);
                rb.isKinematic = true;
                s.Invulnerable = true;
                Popup.Show($"{s.Word} placed!", rb.worldCenterOfMass + Vector3.up * 1.5f, new Color(0.56f, 0.82f, 0.55f), 4f);
                hud.Toast($"{s.Word} in its new home. Lovely!");
            }
        }

        /// <summary>If an item can't be made any more (letters lost, spent or broken), send its box again.</summary>
        void Resupply()
        {
            if (Time.time < nextResupply) return;
            nextResupply = Time.time + 2f;

            var letters = new List<char>();
            if (TilePool.Instance) letters.AddRange(TilePool.Instance.Active.Select(t => t.Letter));
            foreach (var p in joins.Players) letters.AddRange(p.Inventory.Letters);
            var smashables = FindObjectsByType<Smashable>();

            foreach (var word in remaining.Select(i => i.word).Distinct())
            {
                int needed = remaining.Count(i => i.word == word);
                int built = smashables.Count(s => s.Word == word && !placed.Contains(s)) +
                    joins.Players.Count(p => p.Summoner.IsCrafting && p.Summoner.CraftWord == word);
                for (int count = built; count < needed; count++)
                {
                    if (WordSolver.CanSpell(WordSolver.Count(letters), word))
                    {
                        foreach (char c in word) letters.Remove(c);
                        continue;
                    }
                    Deliver(word);
                }
            }
        }

        void Spills()
        {
            if (Time.time >= nextSpill)
            {
                nextSpill = Time.time + 20f;
                var room = rooms[UnityEngine.Random.Range(0, rooms.Length)];
                var at = new Vector3(UnityEngine.Random.Range(room.xRange.x + 2f, room.xRange.y - 2f), room.floorY + 0.012f,
                                     UnityEngine.Random.Range(room.zRange.x + 2f, room.zRange.y - 2f));
                if (layoutBuilder && layoutBuilder.Layout.Stairs.Any(s => s.Covers(at.x, at.z))) return;
                var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(puddle.GetComponent<Collider>());
                puddle.name = "Spill";
                puddle.transform.SetParent(World.Transient, false);
                puddle.transform.position = at;
                puddle.transform.localScale = new Vector3(3.2f, 0.01f, 2.4f);
                puddle.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(new Color(0.55f, 0.78f, 0.95f));
                puddles.Add(puddle.transform);
                Destroy(puddle, 8f);
                Popup.Show("SPILL!", at + Vector3.up * 1.5f, new Color(0.55f, 0.78f, 0.95f), 4f);
            }

            puddles.RemoveAll(p => !p);
            foreach (var p in joins.Players)
                foreach (var puddle in puddles)
                {
                    var d = p.transform.position - puddle.position;
                    float rx = puddle.localScale.x * 0.5f, rz = puddle.localScale.z * 0.5f;
                    if ((d.x * d.x) / (rx * rx) + (d.z * d.z) / (rz * rz) <= 1f) p.MakeSlippery(0.3f);
                }
        }

        void Finish()
        {
            float fraction = TimeLeft / CurrentLevel.timeLimit;
            Stars = fraction >= starThresholds.x ? 3 : fraction >= starThresholds.y ? 2 : 1;
            Session.RecordStars(LevelIndex, Stars);
            SetState(State.Complete);
            foreach (var p in joins.Players) p.Frozen = true;
            World.FreezeTransient();
            CameraRig.Shake(0.2f);
            var result = HudResult.Of(1, true, true, "Home, sweet home!", MatchTally.Finish(true));
            result.Detail = $"{Stars} / 3 stars · {FormatTime(TimeLeft)} to spare";
            hud.ShowResult(result, Retry);
        }

        void OutOfTime()
        {
            Stars = 0;
            SetState(State.OutOfTime);
            foreach (var p in joins.Players) p.Frozen = true;
            World.FreezeTransient();
            hud.ShowResult(HudResult.Of(1, true, false, "The truck is leaving. Try a faster route!", MatchTally.Finish(false)), Retry);
        }

        string ChecklistText()
        {
            sb.Clear();
            sb.Append("<b>CHECKLIST</b>\n<size=75%>Level ").Append(LevelIndex + 1).Append(": ").Append(CurrentLevel.name).Append("</size>\n");
            foreach (var item in CurrentLevel.items)
            {
                bool done = !remaining.Contains(item);
                if (done) sb.Append("<color=#8FD18B><s>").Append(item.word).Append("</s>  ").Append(item.room).Append("</color>\n");
                else sb.Append(item.word).Append("  <color=#FFF4E0AA>in the ").Append(item.room).Append("</color>\n");
            }
            return sb.ToString();
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
