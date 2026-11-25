using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundCourseSeatStripTests
    {
        const string Player = WordboundRecipeContextFixture.Player, Rival = "recipe-rival";
        const float Margin = 18f, Outline = 2f, Pad = 3f;

        static FieldInfo Field(string name) => typeof(WordboundMatchHud).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static T Get<T>(WordboundMatchHud hud, string name) => (T)Field(name).GetValue(hud);
        static MatchSnapshot Frame(WordboundRecipeContextFixture f) => (MatchSnapshot)f.Grid.Get("frame");

        static MatchSnapshot WithActors(MatchSnapshot s, params MatchActorState[] actors) => new MatchSnapshot(s.Binding, s.Epoch, s.Sequence,
            s.NextWorldEntityId, s.Progress, s.Economy, s.Commands, actors, s.Bodies, s.Resources, s.Mechanisms, s.RandomStreams,
            s.NarrativeFlags, s.ThrownFlights, s.Practice);

        static (int[] Order, int Split, bool Two) Arrange(int mine, bool divided, params int[] teams)
        {
            var seats = teams.Select((side, i) => new WordboundSeat("seat-" + i, side)).ToList();
            var order = new int[seats.Count];
            var args = new object[] { seats, mine, divided, order, false };
            int split = (int)typeof(WordboundMatchHud).GetMethod("ArrangeSeats", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            return (order, split, (bool)args[4]);
        }

        [Test]
        public void MyTeamSitsLeftAndOtherRoomsSplitAroundTheClock()
        {
            var relay = Arrange(0, true, 1, 0, 1, 0);
            CollectionAssert.AreEqual(new[] { 1, 3, 0, 2 }, relay.Order, "My team first, the rival after, each in seat order.");
            Assert.AreEqual(2, relay.Split); Assert.IsTrue(relay.Two);
            var rival = Arrange(1, true, 1, 0, 1, 0);
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3 }, rival.Order);
            var brawl = Arrange(2, true, 0, 1, 2, 3, 4);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, brawl.Order, "Free-for-All keeps seat order.");
            Assert.AreEqual(3, brawl.Split, "Five seats split three and two around the clock."); Assert.IsFalse(brawl.Two);
            var duel = Arrange(1, true, 0, 1);
            CollectionAssert.AreEqual(new[] { 1, 0 }, duel.Order, "Two players in Free-for-All are two sides with me on the left.");
            Assert.AreEqual(1, duel.Split); Assert.IsTrue(duel.Two);
            Assert.AreEqual(2, Arrange(0, true, 0, 0, 0).Split, "Co-op Time Trial splits around the clock.");
            Assert.AreEqual(3, Arrange(0, false, 0, 0, 0).Split, "Freeplay has no clock, so everyone is one centred group.");
        }

        [TestCase(1280, 720), TestCase(1920, 1080), TestCase(1248, 576)]
        public void TeamRelaySidesTouchTheClockWithHealthUnderEachSeat(int pixelsWide, int pixelsHigh)
        {
            using var f = new WordboundRecipeContextFixture("TeamRace", flagship: true);
            var me = new MatchActorState(Player, 100, 100);
            f.Grid.Set("healthMax", 100f);
            f.Grid.Set("actor", me);
            f.Grid.Set("frame", WithActors(Frame(f), me, new MatchActorState(Rival, 40, 100)));
            f.Grid.Call("PreparePanels");
            var hud = f.Grid.Hud;
            Assert.AreEqual(1, Get<int>(hud, "seatSplit")); Assert.IsTrue(Get<bool>(hud, "seatTwoTeams"));
            Assert.AreEqual(0, Get<int>(hud, "seatMine"));
            CollectionAssert.AreEqual(new[] { 0, 1 }, Get<int[]>(hud, "seatOrder"));
            var numbers = Get<GUIContent[]>(hud, "seatNumber");
            Assert.AreEqual("100", numbers[0].text); Assert.AreEqual("40", numbers[1].text);
            var bands = Get<HealthBand[]>(hud, "seatBand");
            Assert.AreEqual(HealthBand.Healthy, bands[0]); Assert.AreEqual(HealthBand.Hurt, bands[1]);
            var labels = Get<GUIContent[]>(hud, "seatLabel");
            Assert.AreEqual("1", labels[0].text); Assert.AreEqual("2", labels[1].text);
            Draw(f, pixelsWide, pixelsHigh, (drawn, size) => CheckStrip(drawn, size, 2, 46f));
        }

        [TestCase(1280, 720), TestCase(1920, 1080), TestCase(1248, 576)]
        public void EightSeatsStaySquareAndFitEveryWindow(int pixelsWide, int pixelsHigh)
        {
            using var f = new WordboundRecipeContextFixture("TeamRace", flagship: true);
            var mode = ModeDefinition.For("TeamRace", RaceFormat.Squad);
            string[] others = { "seat-a", "seat-b", "seat-c", "seat-d", "seat-e", "seat-f", "seat-g" };
            var seats = new List<WordboundSeat> { new WordboundSeat(Player, 0, 0) };
            for (int i = 0; i < others.Length; i++) seats.Add(new WordboundSeat(others[i], (i + 1) % 2, (i + 1) / 2));
            var progress = new MatchProgress(mode, seats, MatchStateValidation.Route(f.Course));
            var binding = new MatchBinding("seat-strip", MatchSnapshotCodec.ProtocolVersion, mode.Hash, f.Course.SourceHash,
                f.Course.Hash, new string('a', 64), new string('b', 64), seats);
            var items = ItemCatalogue.FromJson(File.ReadAllText("Assets/_Project/Data/Config/items.json"));
            var at = f.Course.Spawns[0].Position;
            var frame = new MatchSnapshot(binding, 0, 1, 1, progress.Capture(), new Economy(items, new GameRules()).Capture(),
                new CommandJournal(binding.Hash, seats.Select(s => s.PlayerId)).Capture(),
                seats.Select(s => new MatchActorState(s.PlayerId, 100, 100)),
                new[] { new WorldBodyState("player:0", WorldBodyKind.Player, new WorldVector(at.X / 1000f, at.Y / 1000f, at.Z / 1000f), sourceId: Player) },
                Array.Empty<MatchResourceState>(), Array.Empty<MatchMechanismState>(), Array.Empty<MatchRandomState>(), Array.Empty<string>());
            f.Grid.Set("mode", mode); f.Grid.Set("frame", frame); f.Grid.Set("healthMax", 100f);
            f.Grid.Call("PreparePanels");
            var hud = f.Grid.Hud;
            CollectionAssert.AreEqual(new[] { 0, 2, 4, 6, 1, 3, 5, 7 }, Get<int[]>(hud, "seatOrder"), "My team of four on the left.");
            Assert.AreEqual(4, Get<int>(hud, "seatSplit"));
            Assert.IsTrue(Get<GUIContent[]>(hud, "seatNumber").All(number => number.text == "100"));
            Draw(f, pixelsWide, pixelsHigh, (drawn, size) => CheckStrip(drawn, size, 8, 40f));
        }

        [TestCase(1280, 720), TestCase(1920, 1080), TestCase(1248, 576)]
        public void TheLeadingSideGetsASmallTrophyInsideTheClockBlock(int pixelsWide, int pixelsHigh)
        {
            using var f = new WordboundRecipeContextFixture("TeamRace", flagship: true);
            var me = new MatchActorState(Player, 100, 100);
            f.Grid.Set("healthMax", 100f);
            f.Grid.Set("actor", me);
            f.Grid.Set("frame", WithActors(Frame(f), me, new MatchActorState(Rival, 100, 100)));
            f.Grid.Call("PreparePanels");
            f.Grid.Call("RefreshScore");
            var hud = f.Grid.Hud;
            Get<GUIContent>(hud, "scoreMine").text = "2/6";
            Get<GUIContent>(hud, "scoreTheirs").text = "1/6";
            f.Grid.Set("scoreSidesShown", true);
            foreach (int lead in new[] { ScoreLine.Left, ScoreLine.Right, ScoreLine.NoLead })
            {
                f.Grid.Set("scoreLead", lead);
                Draw(f, pixelsWide, pixelsHigh, (drawn, size) =>
                {
                    var centre = Get<Rect>(drawn, "seatCentreRect");
                    var mark = Get<Rect>(drawn, "seatLeadRect");
                    if (lead == ScoreLine.NoLead) { Assert.AreEqual(default(Rect), mark, "A tie shows no trophy."); return; }
                    Assert.AreEqual(14f, mark.width, .5f); Assert.AreEqual(14f, mark.height, .5f, "The trophy is small and square.");
                    Assert.GreaterOrEqual(mark.xMin, centre.xMin + Outline - .5f, "The trophy stays inside the clock block.");
                    Assert.LessOrEqual(mark.xMax, centre.xMax - Outline + .5f);
                    Assert.GreaterOrEqual(mark.yMin, centre.yMin + Outline - .5f);
                    Assert.LessOrEqual(mark.yMax, centre.yMax - Outline + .5f);
                    Assert.Greater(mark.yMin, centre.center.y - .5f, "The trophy sits on the score row under the clock.");
                    if (lead == ScoreLine.Left) Assert.Less(mark.xMax, centre.center.x, "My lead marks my count on the left.");
                    else Assert.Greater(mark.xMin, centre.center.x, "The rival lead marks their count on the right.");
                });
            }
        }

        [Test]
        public void AKnockedOutSeatGoesGreyWithAnEmptyBar()
        {
            using var f = new WordboundRecipeContextFixture("TeamRace", flagship: true);
            f.Grid.Set("healthMax", 100f);
            Assert.IsTrue(f.Progress.RecoverPlayer(Rival));
            f.SetFrame();
            f.Grid.Set("frame", WithActors(Frame(f), new MatchActorState(Player, 100, 100), new MatchActorState(Rival, 0, 100)));
            f.Grid.Call("PreparePanels");
            var hud = f.Grid.Hud;
            var away = Get<bool[]>(hud, "seatOut");
            Assert.IsFalse(away[0]); Assert.IsTrue(away[1]);
            Assert.AreEqual("", Get<GUIContent[]>(hud, "seatNumber")[1].text);
            Assert.AreEqual(0f, Get<float[]>(hud, "seatShare")[1]);
        }

        static void CheckStrip(WordboundMatchHud hud, Vector2 size, int seats, float tile)
        {
            var strip = Get<Rect>(hud, "seatStripRect");
            var centre = Get<Rect>(hud, "seatCentreRect");
            var tiles = Get<Rect[]>(hud, "seatTileRects");
            int split = Get<int>(hud, "seatSplit");
            Assert.Greater(strip.width, 0f, "The seat strip is drawn.");
            Assert.GreaterOrEqual(strip.xMin, Margin - .5f); Assert.LessOrEqual(strip.xMax, size.x - Margin + .5f);
            Assert.GreaterOrEqual(strip.yMin, Margin - .5f);
            Assert.IsFalse(strip.Overlaps(Get<Rect>(hud, "plateRect")), "The strip clears the title plate.");
            Assert.IsFalse(strip.Overlaps(Get<Rect>(hud, "trayRect")), "The strip clears the bag.");
            for (int p = 0; p < seats; p++)
            {
                Assert.AreEqual(tile, tiles[p].width, .5f, "seat " + p);
                Assert.AreEqual(tile, tiles[p].height, .5f, "Seat tiles are square.");
                Assert.AreEqual(strip.yMin + Outline, tiles[p].yMin, .5f, "Every tile sits on one line.");
            }
            for (int p = 1; p < seats; p++)
                if (p != split) Assert.AreEqual(tiles[p - 1].xMax, tiles[p].xMin, .5f, "Teammates touch.");
            Assert.Greater(centre.width, 0f, "The clock sits between the sides.");
            Assert.AreEqual(tiles[split - 1].xMax + Outline, centre.xMin, .5f, "The left side meets the clock.");
            Assert.AreEqual(tiles[split].xMin - Outline, centre.xMax, .5f, "The right side meets the clock.");
            Assert.AreEqual(strip.height, centre.height, .5f);
            Assert.AreEqual(0f, Get<Rect>(hud, "vitalsRect").width, .01f, "Your health is on your tile, not bottom left.");
            Assert.AreEqual(default(Rect), Get<Rect>(hud, "healthBarRect"));
            var number = Get<GUIStyle>(hud, "kitSeatNumber");
            Assert.GreaterOrEqual(number.fontSize, 16);
            Assert.LessOrEqual(number.CalcSize(new GUIContent("100")).x, tile - Pad * 2f, "A full health number fits its bar.");
            Assert.LessOrEqual(Get<GUIStyle>(hud, "kitSeatLabel").CalcSize(new GUIContent("8")).x, tile, "The seat number fits its tile.");
        }

        static void Draw(WordboundRecipeContextFixture f, int pixelsWide, int pixelsHigh, Action<WordboundMatchHud, Vector2> check)
        {
            StripWindow window = null;
            try
            {
                f.Grid.Set("initialized", true);
                f.Grid.View.SetMenuOpen(false);
                f.Grid.Call("PreparePanels");
                float scale = Mathf.Clamp(Mathf.Min(pixelsWide / 1280f, pixelsHigh / 720f), .6f, 1.8f);
                window = ScriptableObject.CreateInstance<StripWindow>();
                window.Hud = f.Grid.Hud; window.Check = check; window.Size = new Vector2(pixelsWide / scale, pixelsHigh / scale);
                window.position = new Rect(100, 100, 640, 480); window.ShowUtility();
                window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure, window.Failure?.ToString()); Assert.Greater(window.Repaints, 0);
            }
            finally
            {
                if (window) window.Close();
                f.Grid.View.SetMenuOpen(true);
            }
        }

        sealed class StripWindow : EditorWindow
        {
            public WordboundMatchHud Hud;
            public Vector2 Size;
            public Action<WordboundMatchHud, Vector2> Check;
            public Exception Failure;
            public int Repaints;

            void OnGUI()
            {
                if (!Hud || Check == null) return;
                try
                {
                    typeof(WordboundMatchHud).GetMethod("DrawOverlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Hud, new object[] { Size.x, Size.y });
                    if (Event.current.type != EventType.Repaint) return;
                    Repaints++;
                    Check(Hud, Size);
                }
                catch (Exception failure) { Failure = failure is TargetInvocationException wrapped && wrapped.InnerException != null ? wrapped.InnerException : failure; }
            }
        }
    }
}
