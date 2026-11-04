using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class TerraceTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static readonly Vector3 BelowTheFlight = new(.8f, 0f, -.6f), AboveTheFlight = new(.8f, 3f, 6.7f);

        static IEnumerator BuildHouse(System.Action<RoomBuilder> built)
        {
            Match.ModeOverride = "Dibs";
            Session.SelectMap("terrace");
            var room = new GameObject("Terrace test house").AddComponent<RoomBuilder>();
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
            Session.SelectMap("terrace");
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

        [UnityTest]
        public IEnumerator APlayerClimbsToTheLandingAndComesBackDown()
        {
            RoomBuilder room = null;
            yield return BuildHouse(r => room = r);
            var layout = room.Layout;
            var input = new ScriptedBinding();
            var p = Player(0, input, BelowTheFlight);
            yield return new WaitForSeconds(.3f);
            var cutaway = StoreyCutaway.Instance;
            var roofParts = room.GetComponentsInChildren<CutawaySurface>(true).SelectMany(s => s.Renderers).ToArray();
            Assert.IsNotEmpty(roofParts, "The authored house includes ceiling and roof sections.");
            Assert.IsTrue(roofParts.All(r => !cutaway.Draws(r)), "Overhead play lifts ceilings while retaining the floor geometry under each player.");
            Assert.IsNotNull(cutaway, "a house with an upstairs gets the cutaway view");
            var upstairs = StoreyParts(room, 1);
            Assert.AreEqual(0, cutaway.TopStorey);
            Assert.IsTrue(upstairs.All(r => !cutaway.Draws(r)), "downstairs, the floor above is lifted off");

            float peak = float.MinValue;
            input.Next.move = Vector2.up;
            float until = Time.time + 6f;
            while (p.transform.position.z < AboveTheFlight.z && Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, p.transform.position.y);
            }
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(.5f);
            var top = p.transform.position;
            Debug.Log($"TERRACE_UP z {top.z:F2} y {top.y:F3} peak {peak:F3}");
            Assert.Greater(top.z, 6.2f, "walked off the top of the flight");
            Assert.AreEqual(3f, top.y, .2f, "stands on the landing");
            Assert.Less(peak, 3.3f, "no launch off the top");
            Assert.AreEqual("Landing", layout.RoomAt(top.x, top.y, top.z));
            Assert.AreEqual(1, cutaway.TopStorey, "upstairs is drawn while you're on it");
            Assert.AreEqual(3f, cutaway.FocusY, .01f, "the camera looks at the floor you're on");
            Assert.IsTrue(upstairs.All(cutaway.Draws));

            int onFlight = 0, airborne = 0;
            input.Next.move = Vector2.down;
            until = Time.time + 6f;
            while (p.transform.position.z > BelowTheFlight.z + .2f && Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                float z = p.transform.position.z;
                if (z > 1.5f && z < 5f) { onFlight++; if (!p.Grounded) airborne++; }
            }
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(.5f);
            var bottom = p.transform.position;
            Debug.Log($"TERRACE_DOWN z {bottom.z:F2} y {bottom.y:F3} flight steps {onFlight} airborne {airborne}");
            Assert.Less(bottom.z, 0f, "walked back down into the hall");
            Assert.AreEqual(0f, bottom.y, .2f, "stands on the hall floor");
            Assert.AreEqual("Hall", layout.RoomAt(bottom.x, bottom.y, bottom.z));
            Assert.Greater(onFlight, 0, "came down the stairs");
            Assert.LessOrEqual(airborne, onFlight / 4, "the feet stay on the stairs going down");
            Assert.AreEqual(0, cutaway.TopStorey, "back downstairs, the upstairs is lifted off again");
            Assert.IsTrue(upstairs.All(r => !cutaway.Draws(r)));
        }

        [UnityTest]
        public IEnumerator NobodyWalksUnderTheStairsOrThroughTheFloor()
        {
            RoomBuilder room = null;
            yield return BuildHouse(r => room = r);
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(new Vector3(.8f, 5.5f, 3f), Vector3.down, out var stairs, 7f, World.GroundMask));
            Assert.AreEqual(1.33f, stairs.point.y, .25f, "the flight's halfway point");
            Assert.IsTrue(Physics.Raycast(new Vector3(-1.2f, 5.5f, 3f), Vector3.down, out var landing, 7f, World.GroundMask));
            Assert.AreEqual(3f, landing.point.y, .05f, "the landing floor");
            Assert.IsTrue(Physics.Raycast(new Vector3(-.7f, .5f, 4f), Vector3.right, 2f, World.GroundMask), "under the flight is solid");
            Assert.IsFalse(Physics.Raycast(new Vector3(-1.2f, 1f, .85f), Vector3.right, 2.5f, World.GroundMask), "the rails end over the bottom step");
            Assert.IsFalse(Physics.Raycast(new Vector3(-5.2f, .5f, 6f), Vector3.left, 1.6f, World.GroundMask), "Hall–LivingRoom doorway");
            Assert.IsTrue(Physics.Raycast(new Vector3(-5.2f, .5f, 8f), Vector3.left, 1.6f, World.GroundMask), "the wall beside it");
            Assert.IsFalse(Physics.Raycast(new Vector3(-5.2f, 3.5f, 8f), Vector3.left, 1.6f, World.GroundMask), "Landing–Bedroom doorway");
            Assert.IsTrue(Physics.Raycast(new Vector3(-5.2f, 3.5f, 6f), Vector3.left, 1.6f, World.GroundMask), "the wall beside it upstairs");
            Assert.IsTrue(Physics.Raycast(new Vector3(-1.2f, 3.5f, 3f), Vector3.right, 1.6f, World.GroundMask), "the railing round the stairwell");
        }

        [UnityTest]
        public IEnumerator TheFloorShieldsABlastFromBelow()
        {
            yield return BuildHouse(_ => { });
            var upstairs = Player(0, new ScriptedBinding(), new Vector3(-14f, 3f, 3f));
            var downstairs = Player(1, new ScriptedBinding(), new Vector3(-13f, 0f, 3f));
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(3f, upstairs.transform.position.y, .2f);
            Projectile.ExplodeAt(new Vector3(-14f, 2.3f, 3f), null, "BOMB", 45f, 15f, 3f, 0f, 0f, true);
            yield return null;
            Assert.Less(downstairs.Health.Current, downstairs.Health.Max, "the blast hurts the room it went off in");
            Assert.AreEqual(upstairs.Health.Max, upstairs.Health.Current, "the floor shields the player standing on it");
        }

        [UnityTest]
        public IEnumerator ABombLyingOnTheFloorUpstairsDoesntReachDownstairs()
        {
            yield return BuildHouse(_ => { });
            var upstairs = Player(0, new ScriptedBinding(), new Vector3(-15.5f, 3f, 3f));
            var downstairs = Player(1, new ScriptedBinding(), new Vector3(-14f, 0f, 2.5f));
            yield return new WaitForSeconds(.3f);
            Projectile.ExplodeAt(new Vector3(-14f, 2.99f, 3f), null, "BOMB", 45f, 15f, 3f, 0f, 0f, true);
            yield return null;
            Assert.Less(upstairs.Health.Current, upstairs.Health.Max, "it hurts whoever is beside it on that floor");
            Assert.AreEqual(downstairs.Health.Max, downstairs.Health.Current, "the floor it lies on shields the room below");
        }

        [UnityTest]
        public IEnumerator CouchViewLiftsTheRoomOverAPlayerDownstairs()
        {
            RoomBuilder room = null;
            yield return BuildHouse(r => room = r);
            var up = Player(0, new ScriptedBinding(), new Vector3(-16f, 3f, 3f));
            var down = Player(1, new ScriptedBinding(), new Vector3(10f, 0f, 10f));
            yield return new WaitForSeconds(.5f);
            var cutaway = StoreyCutaway.Instance;
            Assert.AreEqual(1, cutaway.TopStorey, "someone is upstairs, so it's drawn");
            Assert.AreEqual(1.5f, cutaway.FocusY, .01f, "the camera centres between the two floors");
            CollectionAssert.AreEquivalent(new[] { "KidsRoom" }, cutaway.LiftedRooms, "only the room over the kitchen player comes off");
            var parts = StoreyParts(room, 1);
            Assert.IsTrue(parts.Where(r => r.name == "KidsRoom floor").All(r => !cutaway.Draws(r)));
            Assert.IsTrue(parts.Where(r => r.name == "Bedroom floor").All(cutaway.Draws), "the bedroom player still has a floor");
            Assert.IsTrue(up.GetComponentsInChildren<Renderer>().Concat(down.GetComponentsInChildren<Renderer>()).All(cutaway.Draws), "players are always drawn");

            down.Respawn(new Vector3(-14f, 0f, 8f));
            yield return new WaitForSeconds(.5f);
            CollectionAssert.AreEquivalent(new[] { "Bedroom" }, cutaway.LiftedRooms, "the lifted room follows the player below");
        }

        [UnityTest]
        public IEnumerator TheTopOfTheFlightComesOffForSomeoneJustPastIt()
        {
            RoomBuilder room = null;
            yield return BuildHouse(r => room = r);
            var you = Player(0, new ScriptedBinding(), new Vector3(.8f, 0f, 6f));
            yield return new WaitForSeconds(.5f);
            var cutaway = StoreyCutaway.Instance;
            var flight = room.Layout.Stairs.First(s => s.Lower == "Hall");
            var steps = StoreyParts(room, 0).Where(r => r.name == "Step" && flight.Covers(r.bounds.center.x, r.bounds.center.z)).ToList();
            Assert.IsNotEmpty(steps, "the main flight is built from steps");
            Assert.IsTrue(steps.Where(r => r.bounds.max.y > 2.3f).All(r => !cutaway.Draws(r)), "the tall steps between you and the camera come off");
            Assert.IsTrue(steps.Where(r => r.bounds.max.y < 1f).All(cutaway.Draws), "the low steps stay");
            Assert.IsTrue(StoreyParts(room, 0).Where(r => r.name == "Hall floor").All(cutaway.Draws));
            you.Respawn(new Vector3(-14f, 0f, 6f));
            yield return new WaitForSeconds(.5f);
            Assert.IsTrue(steps.All(cutaway.Draws), "from the living room the whole flight is back");
        }

        [UnityTest]
        public IEnumerator TwoStartUpstairsAndTheMapShowsYourFloor()
        {
            yield return LoadBattle("Dibs", DesktopBinding.Shared);
            yield return new WaitForSeconds(.5f);
            var layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            Assert.AreEqual("Terrace House", layout.Name);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(4, joins.Players.Count);
            var seats = layout.Spawns.Select(s => s.Room).ToList();
            foreach (var p in joins.Players)
            {
                var at = p.transform.position;
                CollectionAssert.Contains(seats, layout.RoomAt(at.x, at.y, at.z), p.name + " starts in a seat room");
            }
            Assert.AreEqual(2, joins.Players.Count(p => Mathf.Abs(p.transform.position.y) < .3f), "two start downstairs");
            Assert.AreEqual(2, joins.Players.Count(p => Mathf.Abs(p.transform.position.y - 3f) < .3f), "two start upstairs");

            var hud = Object.FindAnyObjectByType<GameHud>();
            yield return TestScenes.WaitUntil(() => hud.LocalPlayer, 2f, "the HUD follows someone");
            TextMeshProUGUI Badge() => hud.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "Storey" && t.isActiveAndEnabled);
            var you = hud.LocalPlayer;
            you.Respawn(new Vector3(-13f, 0f, 8f));
            yield return TestScenes.WaitUntil(() => Badge() && Badge().text == "GROUND FLOOR", 2f, "the map says you're on the ground floor");
            you.Respawn(new Vector3(-13f, 3f, 6f));
            Assert.AreEqual("1ST FLOOR", layout.StoreyLabel(1), "with the attic above, upstairs is the first of two upper floors");
            yield return TestScenes.WaitUntil(() => Badge().text == layout.StoreyLabel(1), 2f, "the map follows you upstairs");
        }

        [UnityTest]
        public IEnumerator ABotClimbsTheStairsToReviveItsPartner() =>
            BotRevives(downedAt: new Vector3(5f, 3f, 8f), botAt: new Vector3(-9f, 0f, 3f), "KidsRoom");

        [UnityTest]
        public IEnumerator ABotComesDownTheStairsToReviveItsPartner() =>
            BotRevives(downedAt: new Vector3(-9f, 0f, 8f), botAt: new Vector3(5f, 3f, 7f), "LivingRoom");

        [UnityTest]
        public IEnumerator ABotComesDownTheStairsAndRoundThemIntoTheKitchen() =>
            BotRevives(downedAt: new Vector3(15f, 0f, 11f), botAt: new Vector3(-.5f, 3f, 8f), "Kitchen");

        [UnityTest]
        public IEnumerator ABotWalksRoundTheStairwellOnTheLanding() =>
            BotRevives(downedAt: new Vector3(0f, 3f, -.5f), botAt: new Vector3(6f, 3f, 9f), "Landing", trips: 0);

        [UnityTest]
        public IEnumerator ABotRevivesAPartnerDownOnTheStairs() =>
            BotRevives(downedAt: new Vector3(.8f, 1.1f, 2.5f), botAt: new Vector3(-9f, 0f, 3f), "Hall", pinned: true);

        [UnityTest]
        public IEnumerator ABotStepsThroughADoorFromJustBesideIt() =>
            BotRevives(downedAt: new Vector3(-1f, 3f, 12.2f), botAt: new Vector3(-1.7f, 3f, 9.2f), "Bathroom", trips: 0);

        [UnityTest]
        public IEnumerator ABotGoesDownFromBesideTheTopOfTheFlight() =>
            BotRevives(downedAt: new Vector3(-1.7f, 0f, 0f), botAt: new Vector3(.1f, 3f, 6.2f), "Hall");

        static IEnumerator BotRevives(Vector3 downedAt, Vector3 botAt, string room, int trips = 1, bool pinned = false)
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
            if (pinned) human.Body.constraints = RigidbodyConstraints.FreezeAll;
            Assert.AreEqual(room, layout.RoomAt(human.transform.position.x, human.transform.position.y, human.transform.position.z));
            partner.GetComponent<BotController>().enabled = true;
            float highest = 0f, started = Time.time, until = Time.time + 40f, nextTrace = 0f;
            var trace = new System.Text.StringBuilder();
            bool wasOn = false;
            int timesOn = 0;
            while (!human.Health.IsAlive && Time.time < until)
            {
                var now = partner.transform.position;
                highest = Mathf.Max(highest, now.y);
                bool on = layout.Stairs.Any(s => s.Covers(now.x, now.z) && now.y > layout.Room(s.Lower).FloorY - .5f && now.y < layout.Room(s.Upper).FloorY + .5f);
                if (on && !wasOn) timesOn++;
                wasOn = on;
                if (Time.time >= nextTrace)
                {
                    nextTrace = Time.time + .5f;
                    trace.Append($"\n  {Time.time - started:F1}s ({now.x:F1}, {now.y:F2}, {now.z:F1}) {layout.RoomAt(now.x, now.y, now.z)}");
                }
                yield return null;
            }
            float took = Time.time - started;
            var at = partner.transform.position;
            Debug.Log($"TERRACE_BOT to {room} took {took:F1}s, on the flight {timesOn}x, ended at {at} in {layout.RoomAt(at.x, at.y, at.z)}, highest {highest:F2}{trace}");
            Assert.IsTrue(human.Health.IsAlive, "the bot took the stairs and revived its partner");
            Assert.AreEqual(room, layout.RoomAt(at.x, at.y, at.z), "it revived from the same floor");
            Assert.AreEqual(trips, timesOn, "one trip along the stairs, no going back");
            Assert.Less(took, Match.Rules.ReviveSeconds + 7f, "about 25 m of walking (5 s) and the revive");
        }

        [UnityTest]
        public IEnumerator TheVanOnlyCountsPeopleOnItsOwnFloor()
        {
            yield return LoadBattle("MovingOut");
            var director = Object.FindAnyObjectByType<MovingOutDirector>();
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Playing, 5f, "evacuation start");
            Assert.AreEqual(3, director.KeepsakeCount);
            var layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            var van = director.ExtractionPoint;
            Assert.AreEqual("LivingRoom", layout.RoomAt(van.x, van.y, van.z));
            Assert.IsTrue(director.AtVan(van + new Vector3(1f, 0f, 0f)));
            Assert.IsFalse(director.AtVan(van + new Vector3(1f, 3f, 0f)), "the bedroom above the van is not at the van");
            Assert.IsTrue(Object.FindObjectsByType<Keepsake>().Any(k => k.transform.position.y > 2.5f), "a keepsake waits upstairs");
        }

        [UnityTest]
        public IEnumerator LetterBoxesDropIntoRoomsUnderTheFloorAboveAndClearOfTheStairs()
        {
            RoomBuilder room = null;
            yield return BuildHouse(r => room = r);
            var layout = room.Layout;
            var floors = layout.StoreyFloors();
            var spawner = new GameObject("Test deliveries").AddComponent<DeliverySpawner>();
            spawner.Running = false;
            spawner.Layout = layout;
            for (int i = 0; i < 40; i++)
            {
                var box = spawner.Drop(false);
                var at = box.transform.position;
                string inRoom = layout.RoomAt(at.x, at.y, at.z);
                Assert.IsNotNull(inRoom, $"box {i} at {at} is in a room");
                int storey = layout.StoreyOf(layout.Room(inRoom));
                if (storey + 1 < floors.Count)
                    Assert.Less(at.y + .81f, floors[storey + 1] - .24f, $"box {i} in the {inRoom} starts under the floor above");
                Assert.IsFalse(layout.Stairs.Any(s => at.x > s.MinX - .8f && at.x < s.MaxX + .8f && at.z > s.MinZ - .8f && at.z < s.MaxZ + .8f), $"box {i} at {at} is clear of the stairs");
                Object.Destroy(box.gameObject);
            }
            Object.Destroy(spawner.gameObject);
        }

        [UnityTest]
        public IEnumerator OnceYoureOutTheViewFollowsWhoeverIsStillFighting()
        {
            yield return LoadBattle("Dibs");
            yield return BeginWithBotsStopped();
            var players = Object.FindAnyObjectByType<PlayerJoinManager>().Players;
            players[1].Respawn(new Vector3(13f, 0f, 7.5f));
            players[2].Respawn(new Vector3(14f, 0f, 7.5f));
            players[3].Respawn(new Vector3(-14f, 3f, 6f));
            players[0].Health.Eliminate();
            yield return new WaitForSeconds(.5f);
            var cutaway = StoreyCutaway.Instance;
            Assert.AreEqual(1, cutaway.TopStorey, "someone is still upstairs");
            Assert.AreEqual(1.5f, cutaway.FocusY, .01f);
            CollectionAssert.Contains(cutaway.LiftedRooms, "KidsRoom", "the room over the kitchen comes off");
            CollectionAssert.DoesNotContain(cutaway.LiftedRooms, "Bedroom");
            Assert.IsTrue(players.Skip(1).SelectMany(p => p.GetComponentsInChildren<Renderer>()).All(cutaway.Draws), "all three are drawn");
        }

        [UnityTest]
        public IEnumerator ClearingTheBathroomOnlyHurtsWhoeverIsUpThere()
        {
            yield return LoadBattle("Dibs");
            yield return BeginWithBotsStopped();
            var clear = Object.FindAnyObjectByType<ClearOutController>();
            var players = Object.FindAnyObjectByType<PlayerJoinManager>().Players;
            PlayerController up = players[0], below = players[1];
            up.Respawn(new Vector3(-2f, 3f, 12f));
            below.Respawn(new Vector3(-2f, 0f, 12f));
            var began = typeof(ClearOutController).GetField("began", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            began.SetValue(clear, Time.time - (Match.Rules.ClearOutFirstAt - Match.Rules.ClearOutWarnSeconds + 1f));
            yield return null;
            var layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            Assert.AreEqual("Bathroom", layout.ClearOutOrders["Dibs"][0], "the bathroom upstairs is packed first");
            StringAssert.Contains(layout.WithStorey("Bathroom"), clear.Message);
            began.SetValue(clear, Time.time - (Match.Rules.ClearOutFirstAt + 1f));
            yield return new WaitForSeconds(.6f);
            Assert.Less(up.Health.Current, up.Health.Max, "the bathroom hurts whoever is in it");
            Assert.AreEqual(below.Health.Max, below.Health.Current, "the hall underneath is fine");
        }

        [UnityTest]
        public IEnumerator MovingDayFurnishesBothFloors()
        {
            Session.SelectMap("terrace");
            yield return TestScenes.Load(Session.MovingDayScene);
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            var director = Object.FindAnyObjectByType<MovingDayDirector>();
            yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Playing, 5f, "level start");
            Assert.AreEqual("Terrace House", director.CurrentLevel.name);
            Assert.AreEqual(12, director.Rooms.Count);
            Assert.AreEqual("KidsRoom", director.CurrentLevel.items.First(i => i.word == "LAMP").room);
            var kids = director.Rooms.First(r => r.name == "KidsRoom");
            var kitchen = director.Rooms.First(r => r.name == "Kitchen");
            var upstairs = new Vector3(10f, 3.2f, 9f);
            var downstairs = new Vector3(10f, .2f, 9f);
            Assert.IsTrue(kids.Contains(upstairs) && !kids.Contains(downstairs), "the kids' room is upstairs");
            Assert.IsTrue(kitchen.Contains(downstairs) && !kitchen.Contains(upstairs), "the kitchen is under it");

            var layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            var firstSeen = new System.Collections.Generic.Dictionary<Transform, float>();
            for (float until = Time.time + 4f; Time.time < until; )
            {
                foreach (var box in World.Transient.GetComponentsInChildren<Smashable>().Where(s => s.name.StartsWith("Box (")))
                    if (!firstSeen.ContainsKey(box.transform)) firstSeen[box.transform] = box.transform.position.y;
                yield return null;
            }
            yield return new WaitForSeconds(2f);
            Debug.Log($"TERRACE_DELIVERIES {string.Join(", ", firstSeen.Select(b => $"{b.Key.name} from {b.Value:F2} to {b.Key.position}"))}");
            Assert.GreaterOrEqual(firstSeen.Count, 3, "boxes were delivered");
            foreach (var (box, from) in firstSeen.Select(b => (b.Key, b.Value)))
            {
                Assert.Less(from, 2f, box.name + " starts well under the bedroom floor");
                Assert.AreEqual("LivingRoom", layout.RoomAt(box.position.x, box.position.y, box.position.z), box.name + " lands in the living room");
            }
        }
    }
}
