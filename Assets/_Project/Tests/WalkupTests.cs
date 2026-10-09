using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class WalkupTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static readonly Vector3 InTheLobby = new(1.5f, 0f, -5.2f);

        static IEnumerator BuildHouse(System.Action<RoomBuilder> built)
        {
            Match.ModeOverride = "Dibs";
            Session.SelectMap("walkup");
            var room = new GameObject("Walk-up test house").AddComponent<RoomBuilder>();
            foreach (var prop in room.Originals) prop.gameObject.SetActive(false);
            built(room);
            yield return null;
        }

        static PlayerController Player(int seat, ScriptedBinding input, Vector3 at)
        {
            var p = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            p.Setup(seat, input);
            var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
            p.Health.UseRules(rules);
            p.Inventory.Collects = false;
            p.Respawn(at);
            return p;
        }

        static IEnumerator LoadBattle(string mode, InputBinding you = null)
        {
            Session.SelectMap("walkup");
            Match.ModeOverride = mode;
            Session.Remember(you ?? new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
        }

        static IEnumerator BeginWithBotsStopped()
        {
            RoundManager.Instance.CountdownTime = .05f;
            yield return TestScenes.WaitUntil(() => RoundManager.Instance.Phase == Phase.Playing, 2f, "battle start");
            foreach (var p in Object.FindAnyObjectByType<PlayerJoinManager>().Players)
            {
                if (p.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
                if (p.Binding is BotBinding binding) binding.Commands = default;
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
                p.Health.UseRules(rules);
            }
        }

        static Renderer[] StoreyParts(RoomBuilder room, int storey) =>
            room.GetComponentsInChildren<Transform>(true).First(t => t.name == RoomBuilder.StoreyName(storey))
                .GetComponentsInChildren<Renderer>(true).Where(r => !r.GetComponentInParent<CutawaySurface>()).ToArray();

        static IEnumerator Walk(ScriptedBinding input, PlayerController p, Vector2 move, System.Func<Vector3, bool> arrived, float[] peak)
        {
            input.Next.move = move;
            float until = Time.time + 6f;
            while (!arrived(p.transform.position) && Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                peak[0] = Mathf.Max(peak[0], p.transform.position.y);
            }
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(.4f);
        }

        [UnityTest]
        public IEnumerator APlayerClimbsBothFlightsToTheTopFloor()
        {
            RoomBuilder room = null;
            yield return BuildHouse(r => room = r);
            var layout = room.Layout;
            var input = new ScriptedBinding();
            var p = Player(0, input, InTheLobby);
            yield return new WaitForSeconds(.3f);
            var cutaway = StoreyCutaway.Instance;
            var roofParts = room.GetComponentsInChildren<CutawaySurface>(true).SelectMany(s => s.Renderers).ToArray();
            Assert.IsNotEmpty(roofParts, "The authored house includes ceiling and roof sections.");
            Assert.IsTrue(roofParts.All(r => !cutaway.Draws(r)), "Overhead play lifts ceilings while retaining the floor geometry under each player.");
            Assert.AreEqual(0, cutaway.TopStorey);
            Assert.IsTrue(StoreyParts(room, 1).Concat(StoreyParts(room, 2)).All(r => !cutaway.Draws(r)), "in the lobby, both floors above are lifted off");

            var peak = new[] { float.MinValue };
            yield return Walk(input, p, Vector2.up, at => at.z > 2f, peak);
            var landing = p.transform.position;
            Debug.Log($"WALKUP_FIRST ({landing.x:F2}, {landing.y:F3}, {landing.z:F2}) peak {peak[0]:F3}");
            Assert.AreEqual(3f, landing.y, .2f, "on the first-floor landing");
            Assert.AreEqual("Landing1", layout.RoomAt(landing.x, landing.y, landing.z));
            Assert.Less(peak[0], 3.3f, "no launch off the top");
            Assert.AreEqual(1, cutaway.TopStorey);
            Assert.IsTrue(StoreyParts(room, 1).All(cutaway.Draws) && StoreyParts(room, 2).All(r => !cutaway.Draws(r)), "the first floor is drawn, the second still lifted");

            yield return Walk(input, p, Vector2.left, at => at.x < -1.5f, peak);
            Assert.Less(p.transform.position.x, -1.3f, "crossed to the foot of the second flight");
            Assert.AreEqual(3f, p.transform.position.y, .2f);

            peak[0] = float.MinValue;
            yield return Walk(input, p, Vector2.down, at => at.z < -4.4f, peak);
            var top = p.transform.position;
            Debug.Log($"WALKUP_SECOND ({top.x:F2}, {top.y:F3}, {top.z:F2}) peak {peak[0]:F3}");
            Assert.AreEqual(6f, top.y, .2f, "on the second-floor landing");
            Assert.AreEqual("Landing2", layout.RoomAt(top.x, top.y, top.z));
            Assert.Less(peak[0], 6.3f);
            Assert.AreEqual(2, cutaway.TopStorey);
            Assert.AreEqual(6f, cutaway.FocusY, .01f);
            Assert.IsTrue(StoreyParts(room, 2).All(cutaway.Draws));
        }

        [UnityTest]
        public IEnumerator TheStairwellIsSolidRailedAndOpenWhereItShouldBe()
        {
            RoomBuilder room = null;
            yield return BuildHouse(r => room = r);
            Physics.SyncTransforms();
            float Down(float x, float fromY, float z)
            {
                Assert.IsTrue(Physics.Raycast(new Vector3(x, fromY, z), Vector3.down, out var hit, fromY + 1f, World.GroundMask), $"something under ({x}, {fromY}, {z})");
                return hit.point.y;
            }
            Assert.AreEqual(1.5f, Down(1.5f, 5.5f, -1.25f), .25f, "the first flight, under the first-floor opening");
            Assert.AreEqual(4.5f, Down(-1.5f, 8.5f, -1.25f), .25f, "the second flight, under the second-floor opening");
            Assert.AreEqual(6f, Down(1.5f, 8.5f, -1.25f), .05f);
            Assert.AreEqual(6f, Down(0f, 8.5f, -1.25f), .05f);
            Assert.AreEqual(3f, Down(0f, 5.5f, -1.25f), .05f, "the walkway on the first-floor landing");
            Assert.AreEqual(3f, Down(0f, 5.5f, -5f), .05f, "the first-floor landing's far end is floor you can reach");
            Assert.IsTrue(Physics.Raycast(new Vector3(0f, 3.5f, -1.25f), Vector3.right, 1f, World.GroundMask), "the railing round the first opening");
            Assert.IsTrue(Physics.Raycast(new Vector3(0f, 3.5f, -1.25f), Vector3.left, 1f, World.GroundMask), "the side of the second flight");
            Assert.IsFalse(Physics.Raycast(new Vector3(1f, 4f, 1.15f), Vector3.left, 3f, World.GroundMask), "the rails end over the bottom step");
            foreach (float y in new[] { 0f, 3f, 6f })
            {
                Assert.IsFalse(Physics.Raycast(new Vector3(-2f, y + .5f, 3.5f), Vector3.left, 1.2f, World.GroundMask), $"west doorway at {y} m");
                Assert.IsTrue(Physics.Raycast(new Vector3(-2f, y + .5f, 5.2f), Vector3.left, 1.2f, World.GroundMask), $"west wall at {y} m");
                Assert.IsFalse(Physics.Raycast(new Vector3(2f, y + .5f, 3.5f), Vector3.right, 1.2f, World.GroundMask), $"east doorway at {y} m");
                Assert.IsTrue(Physics.Raycast(new Vector3(2f, y + .5f, 5.2f), Vector3.right, 1.2f, World.GroundMask), $"east wall at {y} m");
            }
        }

        [UnityTest]
        public IEnumerator TheViewCutsAwayEveryFloorAboveYou()
        {
            RoomBuilder room = null;
            yield return BuildHouse(r => room = r);
            var you = Player(0, new ScriptedBinding(), new Vector3(-5f, 0f, -1f));
            yield return new WaitForSeconds(.5f);
            var cutaway = StoreyCutaway.Instance;
            var first = StoreyParts(room, 1);
            var second = StoreyParts(room, 2);
            Assert.AreEqual(0, cutaway.TopStorey);
            Assert.IsTrue(first.Concat(second).All(r => !cutaway.Draws(r)));
            you.Respawn(new Vector3(-5f, 3f, -1f));
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(1, cutaway.TopStorey);
            Assert.AreEqual(3f, cutaway.FocusY, .01f);
            Assert.IsTrue(first.All(cutaway.Draws) && second.All(r => !cutaway.Draws(r)));
            you.Respawn(new Vector3(-5f, 6f, -1f));
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(2, cutaway.TopStorey);
            Assert.IsTrue(first.Concat(second).All(cutaway.Draws));

            var below = Player(1, new ScriptedBinding(), new Vector3(5.5f, 0f, -3f));
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(2, cutaway.TopStorey);
            Assert.AreEqual(3f, cutaway.FocusY, .01f, "centred between the ground and the top floor");
            CollectionAssert.AreEquivalent(new[] { "RedFlat", "YellowFlat" }, cutaway.LiftedRooms);
            Assert.IsTrue(first.Where(r => r.name == "BlueFlat floor").All(cutaway.Draws), "the flats on the other side keep their floors");
        }

        [UnityTest]
        public IEnumerator EveryoneStartsInAFlatAndTheMapCountsFloors()
        {
            yield return LoadBattle("Dibs", DesktopBinding.Shared);
            yield return new WaitForSeconds(.5f);
            var layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            Assert.AreEqual("Walk-up Apartments", layout.Name);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(4, joins.Players.Count);
            var flats = new[] { "BlueFlat", "RedFlat", "GreenFlat", "YellowFlat" };
            foreach (var p in joins.Players)
            {
                var at = p.transform.position;
                CollectionAssert.Contains(flats, layout.RoomAt(at.x, at.y, at.z), p.name + " starts in a flat");
            }
            Assert.AreEqual(2, joins.Players.Count(p => Mathf.Abs(p.transform.position.y - 3f) < .3f), "two on the first floor");
            Assert.AreEqual(2, joins.Players.Count(p => Mathf.Abs(p.transform.position.y - 6f) < .3f), "two on the second");

            var hud = Object.FindAnyObjectByType<GameHud>();
            yield return TestScenes.WaitUntil(() => hud.LocalPlayer, 2f, "the HUD follows someone");
            TextMeshProUGUI Badge() => hud.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "Storey" && t.isActiveAndEnabled);
            var you = hud.LocalPlayer;
            you.Respawn(new Vector3(7f, 0f, 0f));
            yield return TestScenes.WaitUntil(() => Badge() && Badge().text == "GROUND FLOOR", 2f, "the map says ground floor");
            you.Respawn(new Vector3(7f, 3f, 0f));
            yield return TestScenes.WaitUntil(() => Badge().text == "1ST FLOOR", 2f, "the map says first floor");
            you.Respawn(new Vector3(7f, 6f, 0f));
            yield return TestScenes.WaitUntil(() => Badge().text == "2ND FLOOR", 2f, "the map says second floor");
        }

        [UnityTest]
        public IEnumerator ABotTakesBothFlightsUpToReviveItsPartner() =>
            BotRevives(downedAt: new Vector3(-4f, 6f, 2f), botAt: new Vector3(5f, 0f, 0f), "GreenFlat");

        [UnityTest]
        public IEnumerator ABotTakesBothFlightsDownToReviveItsPartner() =>
            BotRevives(downedAt: new Vector3(-5f, 0f, 0f), botAt: new Vector3(4f, 6f, 2f), "Cafe");

        [UnityTest]
        public IEnumerator ABotGoesDownTheTopFlightFromBesideIt() =>
            BotRevives(downedAt: new Vector3(0f, 3f, 4f), botAt: new Vector3(-.8f, 6f, -4.2f), "Landing1", trips: new[] { 0, 1 });

        [UnityTest]
        public IEnumerator ABotGoesDownTheBottomFlightFromBesideIt() =>
            BotRevives(downedAt: new Vector3(-1.5f, 0f, 4f), botAt: new Vector3(.8f, 3f, 1.7f), "Lobby", trips: new[] { 1, 0 });

        static IEnumerator BotRevives(Vector3 downedAt, Vector3 botAt, string room, int[] trips = null)
        {
            yield return LoadBattle("Duos");
            yield return BeginWithBotsStopped();
            var layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var human = joins.Players[0];
            var partner = joins.Players[2];
            Assert.AreEqual(human.Team, partner.Team);
            var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f; rules.BleedOutSeconds = new[] { 120f };
            human.Health.UseRules(rules);
            human.Respawn(downedAt);
            partner.Respawn(botAt);
            yield return new WaitForSeconds(.3f);
            human.Health.ApplyDamage(HitInfo.Hazard(1000f));
            yield return null;
            Assert.IsTrue(human.IsDowned);
            Assert.AreEqual(room, layout.RoomAt(human.transform.position.x, human.transform.position.y, human.transform.position.z));
            partner.GetComponent<BotController>().enabled = true;
            float started = Time.time, until = Time.time + 45f, nextTrace = 0f;
            var trace = new System.Text.StringBuilder();
            var flights = layout.Stairs;
            var wasOn = new bool[flights.Count];
            var timesOn = new int[flights.Count];
            while (!human.Health.IsAlive && Time.time < until)
            {
                var now = partner.transform.position;
                for (int i = 0; i < flights.Count; i++)
                {
                    var s = flights[i];
                    bool on = s.Covers(now.x, now.z) && now.y > layout.Room(s.Lower).FloorY - .3f && now.y < layout.Room(s.Upper).FloorY + .3f;
                    if (on && !wasOn[i]) timesOn[i]++;
                    wasOn[i] = on;
                }
                if (Time.time >= nextTrace)
                {
                    nextTrace = Time.time + .5f;
                    trace.Append($"\n  {Time.time - started:F1}s ({now.x:F1}, {now.y:F2}, {now.z:F1}) {layout.RoomAt(now.x, now.y, now.z)}");
                }
                yield return null;
            }
            float took = Time.time - started;
            var at = partner.transform.position;
            Debug.Log($"WALKUP_BOT to {room} took {took:F1}s, flights {string.Join("/", timesOn)}x, ended at {at} in {layout.RoomAt(at.x, at.y, at.z)}{trace}");
            Assert.IsTrue(human.Health.IsAlive, "the bot took the stairs and revived its partner");
            Assert.AreEqual(room, layout.RoomAt(at.x, at.y, at.z), "it revived from the same floor");
            CollectionAssert.AreEqual(trips ?? new[] { 1, 1 }, timesOn, "one trip along each flight it needs");
            Assert.Less(took, Match.Rules.ReviveSeconds + 10f, "about 40 m of walking and the revive");
        }

        [UnityTest]
        public IEnumerator TheVanWaitsInTheGarageAndKeepsakesAreOnEveryFloor()
        {
            yield return LoadBattle("MovingOut");
            var director = Object.FindAnyObjectByType<MovingOutDirector>();
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Playing, 5f, "evacuation start");
            Assert.AreEqual(3, director.KeepsakeCount);
            var layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            var van = director.ExtractionPoint;
            Assert.AreEqual("Garage", layout.RoomAt(van.x, van.y, van.z));
            Assert.IsTrue(director.AtVan(van + new Vector3(1f, 0f, 0f)));
            Assert.IsFalse(director.AtVan(van + new Vector3(1f, 3f, 0f)), "the flat above the garage is not at the van");
            var heights = Object.FindObjectsByType<Keepsake>().Select(k => layout.StoreyAt(k.transform.position.y)).OrderBy(s => s);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, heights, "one keepsake on each floor");
        }

        [UnityTest]
        public IEnumerator MovingDayFurnishesAllThreeFloors()
        {
            Session.SelectMap("walkup");
            yield return TestScenes.Load(Session.MovingDayScene);
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            var director = Object.FindAnyObjectByType<MovingDayDirector>();
            yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Playing, 5f, "level start");
            Assert.AreEqual("Walk-up Apartments", director.CurrentLevel.name);
            Assert.AreEqual(9, director.Rooms.Count);
            Assert.AreEqual("GreenFlat", director.CurrentLevel.items.First(i => i.word == "BED").room);
            var cafe = director.Rooms.First(r => r.name == "Cafe");
            var blue = director.Rooms.First(r => r.name == "BlueFlat");
            var green = director.Rooms.First(r => r.name == "GreenFlat");
            Vector3 ground = new(-5f, .2f, 0f), first = new(-5f, 3.2f, 0f), second = new(-5f, 6.2f, 0f);
            Assert.IsTrue(cafe.Contains(ground) && !cafe.Contains(first) && !cafe.Contains(second));
            Assert.IsTrue(blue.Contains(first) && !blue.Contains(ground) && !blue.Contains(second));
            Assert.IsTrue(green.Contains(second) && !green.Contains(first) && !green.Contains(ground));
        }

        [UnityTest]
        public IEnumerator LetterBoxesDropOnEveryFloorClearOfBothFlights()
        {
            RoomBuilder room = null;
            yield return BuildHouse(r => room = r);
            var layout = room.Layout;
            var floors = layout.StoreyFloors();
            var spawner = new GameObject("Test deliveries").AddComponent<DeliverySpawner>();
            spawner.Running = false;
            spawner.Layout = layout;
            var storeys = new HashSet<int>();
            for (int i = 0; i < 60; i++)
            {
                var box = spawner.Drop(false);
                var at = box.transform.position;
                string inRoom = layout.RoomAt(at.x, at.y, at.z);
                Assert.IsNotNull(inRoom, $"box {i} at {at} is in a room");
                int storey = layout.StoreyOf(layout.Room(inRoom));
                storeys.Add(storey);
                if (storey + 1 < floors.Count)
                    Assert.Less(at.y + .81f, floors[storey + 1] - .24f, $"box {i} in the {inRoom} starts under the floor above");
                Assert.IsFalse(layout.Stairs.Any(s => at.x > s.MinX - .8f && at.x < s.MaxX + .8f && at.z > s.MinZ - .8f && at.z < s.MaxZ + .8f), $"box {i} at {at} is clear of the stairs");
                Object.Destroy(box.gameObject);
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, storeys, "boxes land on all three floors");

            var stairwell = new[] { "Lobby", "Landing1", "Landing2" };
            spawner.RoomOpen = name => stairwell.Contains(name);
            for (int i = 0; i < 100; i++)
            {
                var box = spawner.Drop(false);
                var at = box.transform.position;
                CollectionAssert.Contains(stairwell, layout.RoomAt(at.x, at.y, at.z), $"box {i} at {at} lands in the stairwell");
                Assert.IsFalse(layout.Stairs.Any(s => at.x > s.MinX - .8f && at.x < s.MaxX + .8f && at.z > s.MinZ - .8f && at.z < s.MaxZ + .8f), $"box {i} at {at} is clear of the stairs");
                Object.Destroy(box.gameObject);
            }
            Object.Destroy(spawner.gameObject);
        }
    }
}
