using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class PartyCameraTests
    {
        const float Wide = 1280f / 720f, Tick = 1f / 60f;
        static readonly PartyArea Pinwheel = new(-20f, 20f, -20f, 20f);
        static readonly PartyArea OldTerrace = new(-10f, 10f, -7f, 7f);

        static PartySeat Human(float x, float z, float y = 0f) => new(x, y, z);
        static PartySeat Bot(float x, float z) => new(x, 0f, z, human: false);
        static PartySeat Out(float x, float z) => new(x, 0f, z, eliminated: true);

        static void InSafeArea(PartyShot shot, PartySeat seat, float aspect, float focusY = 0f)
        {
            float z = seat.Z + (seat.Y - focusY) * PartyCamera.Lean;
            Assert.LessOrEqual(Math.Abs(seat.X - shot.X) + PartyCamera.Margin,
                PartyCamera.HalfWidth(shot.Size, aspect) * PartyCamera.SafeWidth + 1e-3f, "across");
            Assert.LessOrEqual(Math.Abs(z - shot.Z) + PartyCamera.Margin,
                PartyCamera.HalfDepth(shot.Size) * PartyCamera.SafeHeight + 1e-3f, "along");
        }

        [Test]
        public void TheClosestFramingIsTheOldPinwheelAndTheWidestIsTodaysWholeHouse()
        {
            Assert.AreEqual(PartyCamera.HouseSize(20f, 20f, Wide), PartyCamera.MinSize, 1e-4f);
            Assert.AreEqual(Math.Max(48f / Wide, 32f * .85f) * .5f + .8f, PartyCamera.HouseSize(48f, 32f, Wide), 1e-4f);
            Assert.AreEqual(PartyCamera.HouseSize(20f, 20f, .5f), PartyCamera.HouseSize(20f, 20f, .1f), 1e-4f, "very tall screens are treated as 1:2");
        }

        [Test]
        public void SeatsSideBySideStopAtTheClosestFraming()
        {
            var shot = PartyCamera.Aim(new[] { Human(-1f, 2f), Human(1f, 3f) }, Pinwheel, Wide, 0f);
            Assert.AreEqual(PartyCamera.MinSize, shot.Size, 1e-4f);
            Assert.AreEqual(0f, shot.X, 1e-4f);
            Assert.AreEqual(2.5f, shot.Z, 1e-4f);
            Assert.IsFalse(shot.Solo);
            Assert.AreEqual(2, shot.Framed);
        }

        [Test]
        public void SmallHousesKeepTheirWholeHouseView()
        {
            float whole = PartyCamera.HouseSize(OldTerrace.Width, OldTerrace.Depth, Wide);
            Assert.Less(whole, PartyCamera.MinSize);
            var shot = PartyCamera.Aim(new[] { Human(-6f, -3f), Human(-5f, -3f) }, OldTerrace, Wide, 0f);
            Assert.AreEqual(whole, shot.Size, 1e-4f, "the closest framing never exceeds the whole house");
            Assert.AreEqual(OldTerrace.CentreX, shot.X, 1e-4f);
            Assert.AreEqual(OldTerrace.CentreZ, shot.Z, 1e-4f);
        }

        [Test]
        public void SpreadingSeatsZoomOutUpToTheWholeHouse()
        {
            float whole = PartyCamera.HouseSize(Pinwheel.Width, Pinwheel.Depth, Wide);
            float last = 0f;
            bool between = false;
            for (float apart = 0f; apart <= 18f; apart += 1f)
            {
                var seats = new[] { Human(-apart, -apart), Human(apart, apart) };
                var shot = PartyCamera.Aim(seats, Pinwheel, Wide, 0f);
                Assert.GreaterOrEqual(shot.Size, last - 1e-4f, $"never zooms in as they spread ({apart} m)");
                Assert.LessOrEqual(shot.Size, whole + 1e-4f);
                if (shot.Size < whole - 1e-3f) foreach (var seat in seats) InSafeArea(shot, seat, Wide);
                between |= shot.Size > PartyCamera.MinSize + .5f && shot.Size < whole - .5f;
                last = shot.Size;
            }
            Assert.IsTrue(between, "some spreads sit between the closest and widest framings");
            var corners = PartyCamera.Aim(new[] { Human(-18f, -18f), Human(18f, 18f) }, Pinwheel, Wide, 0f);
            Assert.AreEqual(whole, corners.Size, 1e-4f);
            Assert.AreEqual(Pinwheel.CentreX, corners.X, 1e-4f, "the whole house is centred like today");
            Assert.AreEqual(Pinwheel.CentreZ, corners.Z, 1e-4f);
        }

        [Test]
        public void OneSeatIsFollowedAtAFixedFraming()
        {
            foreach (var at in new[] { (0f, 0f), (12f, -8f), (-19f, 19f) })
            {
                var shot = PartyCamera.Aim(new[] { Human(at.Item1, at.Item2), Bot(-15f, -15f) }, Pinwheel, Wide, 0f);
                Assert.IsTrue(shot.Solo);
                Assert.AreEqual(1, shot.Framed, "bots are left out while a human plays");
                Assert.AreEqual(PartyCamera.MinSize, shot.Size, 1e-4f);
                Assert.AreEqual(at.Item1, shot.X, 1e-4f);
                Assert.AreEqual(at.Item2, shot.Z, 1e-4f);
            }
        }

        [Test]
        public void EliminatedSeatsAndBotsAreIgnoredUntilNoHumanIsLeft()
        {
            var playing = PartyCamera.Aim(new[] { Human(-2f, 0f), Human(2f, 1f), Out(18f, 18f), Bot(-18f, -18f) }, Pinwheel, Wide, 0f);
            Assert.AreEqual(2, playing.Framed);
            Assert.AreEqual(PartyCamera.MinSize, playing.Size, 1e-4f);
            Assert.AreEqual(0f, playing.X, 1e-4f);
            Assert.AreEqual(.5f, playing.Z, 1e-4f);

            var watching = PartyCamera.Aim(new[] { Out(-2f, 0f), Out(2f, 1f), Bot(-10f, -10f), Bot(10f, 10f) }, Pinwheel, Wide, 0f);
            Assert.AreEqual(2, watching.Framed, "with every human out, the camera watches whoever is still fighting");
            Assert.IsFalse(watching.Solo);
            Assert.Greater(watching.Size, PartyCamera.MinSize);

            var lastBot = PartyCamera.Aim(new[] { Out(0f, 0f), Bot(6f, 4f) }, Pinwheel, Wide, 0f);
            Assert.IsFalse(lastBot.Solo, "a lone bot is watched from the overhead view, not followed up close");
            Assert.AreEqual(PartyCamera.MinSize, lastBot.Size, 1e-4f);

            var empty = PartyCamera.Aim(new[] { Out(0f, 0f) }, Pinwheel, Wide, 0f);
            Assert.AreEqual(0, empty.Framed);
            Assert.AreEqual(PartyCamera.HouseSize(40f, 40f, Wide), empty.Size, 1e-4f);
            Assert.AreEqual(0f, empty.X, 1e-4f);
            Assert.AreEqual(0f, empty.Z, 1e-4f);
        }

        [Test]
        public void TheCentreStaysInsideTheHouseWithoutCuttingSeatsOff()
        {
            var west = PartyCamera.Aim(new[] { Human(-12f, 0f), Human(-11f, 1f) }, Pinwheel, Wide, 0f);
            Assert.AreEqual(PartyCamera.MinSize, west.Size, 1e-4f);
            Assert.AreEqual(Pinwheel.MinX - PartyCamera.HalfWidth(PartyCamera.HousePad, Wide), west.X - PartyCamera.HalfWidth(west.Size, Wide), 1e-3f,
                "near a wall the view shows no more yard than the whole-house view does");
            Assert.AreEqual(.5f, west.Z, 1e-4f);

            var nook = new[] { Human(-18f, 17f), Human(-17f, 18f) };
            var shot = PartyCamera.Aim(nook, Pinwheel, Wide, 0f);
            Assert.AreEqual(PartyCamera.MinSize, shot.Size, 1e-4f);
            Assert.Greater(shot.X, -17.5f + 5f, "a corner pair is framed leaning into the house");
            Assert.Less(shot.Z, 17.5f - 3f);
            foreach (var seat in nook) InSafeArea(shot, seat, Wide);

            var outside = PartyCamera.Aim(new[] { Human(30f, -35f), Human(32f, -36f) }, Pinwheel, Wide, 0f);
            Assert.LessOrEqual(outside.X, Pinwheel.MaxX);
            Assert.GreaterOrEqual(outside.Z, Pinwheel.MinZ);

            var alone = PartyCamera.Aim(new[] { Human(25f, 0f) }, Pinwheel, Wide, 0f);
            Assert.AreEqual(Pinwheel.MaxX, alone.X, 1e-4f);
        }

        [Test]
        public void SeatsOnAnUpperStoreyAreFramedWhereTheyAppearOnScreen()
        {
            var shot = PartyCamera.Aim(new[] { Human(0f, 0f, 0f), Human(4f, 0f, 3f) }, Pinwheel, Wide, 1.5f);
            Assert.AreEqual(-1.5f * PartyCamera.Lean, shot.MinZ, 1e-4f);
            Assert.AreEqual(1.5f * PartyCamera.Lean, shot.MaxZ, 1e-4f);
            Assert.AreEqual(0f, shot.Z, 1e-4f);
            Assert.AreEqual(1.5f, shot.Y, 1e-4f);
        }

        [Test]
        public void EasingIsSmoothFrameRateIndependentAndSettles()
        {
            var seats = new[] { Human(10f, 8f), Human(12f, 9f) };
            var start = PartyView.At(0f, 0f, 0f, PartyCamera.HouseSize(40f, 40f, Wide));
            var target = PartyCamera.Aim(seats, Pinwheel, Wide, 0f);
            PartyView Run(float step, float seconds)
            {
                var view = start;
                for (int i = 0; i < (int)Math.Round(seconds / step); i++) PartyCamera.Frame(seats, Pinwheel, Wide, 0f, ref view, step);
                return view;
            }
            var slow = Run(1f / 30f, 1f);
            var fast = Run(1f / 144f, 1f);
            Assert.AreEqual(slow.X, fast.X, 1e-3f);
            Assert.AreEqual(slow.Z, fast.Z, 1e-3f);
            Assert.AreEqual(slow.Size, fast.Size, 1e-3f);

            var eased = start;
            float lastX = eased.X, lastSize = eased.Size;
            for (int i = 0; i < 240; i++)
            {
                PartyCamera.Frame(seats, Pinwheel, Wide, 0f, ref eased, Tick);
                Assert.GreaterOrEqual(eased.X, lastX - 1e-4f, "pans one way without overshoot");
                Assert.LessOrEqual(eased.X, target.X + 1e-3f);
                Assert.LessOrEqual(eased.Size, lastSize + 1e-4f, "zooms in one way without overshoot");
                Assert.Less(Math.Abs(eased.X - lastX), .5f, "no jumps between frames");
                lastX = eased.X; lastSize = eased.Size;
            }
            Assert.AreEqual(target.X, eased.X, .01f);
            Assert.AreEqual(target.Z, eased.Z, .01f);
            Assert.AreEqual(target.Size, eased.Size, .02f);

            var still = eased;
            PartyCamera.Frame(seats, Pinwheel, Wide, 0f, ref still, 0f);
            Assert.AreEqual(eased.X, still.X, 1e-5f, "a paused frame does not move the view");

            var fresh = default(PartyView);
            PartyCamera.Frame(seats, Pinwheel, Wide, 0f, ref fresh, Tick);
            Assert.IsTrue(fresh.Placed);
            Assert.AreEqual(target.Size, fresh.Size, 1e-4f, "an unplaced view starts on its framing");
        }

        [Test]
        public void ASeatThatDashesAwayIsNeverCutOff()
        {
            var seats = new[] { Human(0f, 0f), Human(1f, 0f) };
            var view = PartyView.At(.5f, 0f, 0f, PartyCamera.MinSize);
            seats[1] = Human(19f, -19f);
            PartyCamera.Frame(seats, Pinwheel, Wide, 0f, ref view, Tick);
            foreach (var seat in seats)
            {
                Assert.LessOrEqual(Math.Abs(seat.X - view.X), PartyCamera.HalfWidth(view.Size, Wide) + 1e-3f);
                Assert.LessOrEqual(Math.Abs(seat.Z - view.Z), PartyCamera.HalfDepth(view.Size) + 1e-3f);
            }
            Assert.LessOrEqual(view.Size, PartyCamera.HouseSize(40f, 40f, Wide) + 1e-4f);
        }

        [Test]
        public void FramingAFrameAllocatesNothing()
        {
            var seats = new List<PartySeat> { Human(-3f, 2f), Human(5f, -4f), Bot(9f, 9f), Out(0f, 0f) };
            var view = PartyView.At(0f, 0f, 0f, PartyCamera.MinSize);
            for (int i = 0; i < 8; i++) PartyCamera.Frame(seats, Pinwheel, Wide, 0f, ref view, Tick);
            long before = Allocated();
            for (int i = 0; i < 600; i++) PartyCamera.Frame(seats, Pinwheel, Wide, 0f, ref view, Tick);
            long after = Allocated();
            if (before < 0 || after < 0) Assert.Ignore("This runtime does not count allocations.");
            Assert.AreEqual(0L, after - before);
        }

        static long Allocated()
        {
            try { return GC.GetAllocatedBytesForCurrentThread(); }
            catch (NotSupportedException) { return -1; }
        }
    }
}
