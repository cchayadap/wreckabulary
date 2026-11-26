using System;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class ItemHoldTests
    {
        static ItemCatalogue Live()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return items;
        }

        static (float x, float y, float z) InBody(string id, float x, float y, float z)
        {
            var item = Live().Get(id);
            return (ItemHolds.Socket(item) * ItemHolds.InHand(item)).Rotate(x, y, z);
        }

        const float SwingTolerance = 35f, ShieldTolerance = 25f, MinHeldLongest = .25f, MaxHeldLongest = .8f;
        static readonly (float x, float y, float z) UpForward = (0f, .70710678f, .70710678f);

        static float Degrees((float x, float y, float z) a, (float x, float y, float z) b)
        {
            double dot = (a.x * b.x + a.y * b.y + a.z * b.z) / Math.Sqrt((a.x * a.x + a.y * a.y + a.z * a.z) * (b.x * b.x + b.y * b.y + b.z * b.z));
            return (float)(Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot))) * 180.0 / Math.PI);
        }

        static float HeldLongest(ItemDefinition item) => Math.Max(item.Size[0], Math.Max(item.Size[1], item.Size[2])) * item.HeldScale;

        [Test]
        public void HoldsNameLiveItemsAndLeaveTheGuardPoseAlone()
        {
            CollectionAssert.IsEmpty(ItemHolds.Validate(Live()));
            Assert.IsNull(ItemHolds.For(ItemHolds.GuardPosedItem), "the SHIELD guard pose is written in world space and would ignore a hold turn");
        }

        [TestCase("PADDLE", 0f, 1f, 0f), TestCase("RACKET", 0f, 1f, 0f), TestCase("SPOON", 0f, 1f, 0f), TestCase("RAKE", 0f, 1f, 0f)]
        [TestCase("PENCIL", 0f, 1f, 0f), TestCase("TORCH", 0f, 1f, 0f), TestCase("ROCKET", 0f, 1f, 0f), TestCase("CHILI", 1f, 0f, 0f)]
        [TestCase("ANCHOR", 0f, -1f, 0f), TestCase("MALLET", 0f, 1f, 0f), TestCase("FLIPPER", 0f, 1f, 0f)]
        public void SwungAndThrustItemsRiseUpAndForwardOutOfTheFist(string id, float x, float y, float z)
        {
            var along = InBody(id, x, y, z);
            Assert.Less(Degrees(along, UpForward), SwingTolerance, id + " points its business end up and forward, not flat or sideways");
            Assert.Greater(along.y, .65f, id + " stands up out of the fist");
        }

        [Test]
        public void TurnedShieldsStandUprightWithTheirFaceForward()
        {
            var items = Live();
            int turned = 0;
            foreach (var hold in ItemHolds.All)
            {
                var item = items.Get(hold.Id);
                if (item.Shield == null) continue;
                turned++;
                Assert.Less(Degrees(InBody(hold.Id, 0f, 0f, 1f), (0f, 0f, 1f)), ShieldTolerance, hold.Id + " shows its decorated +Z face to the front");
                Assert.Greater(InBody(hold.Id, 0f, 1f, 0f).y, .9f, hold.Id + " stands upright in front of the forearm");
                Assert.Less(ItemHolds.Grip(item)[2], 0f, hold.Id + " is held from behind its face");
            }
            Assert.GreaterOrEqual(turned, 3, "LID, HELMET and DOOR are posed");
        }

        [Test]
        public void HeldItemsStayInOneFamilyOfSizes()
        {
            var items = Live();
            foreach (var item in items.Enabled)
                Assert.That(HeldLongest(item), Is.InRange(MinHeldLongest, MaxHeldLongest), item.Id + " longest side in the hand");
            Assert.Greater(HeldLongest(items.Get("BOOT")), HeldLongest(items.Get("APPLE")), "a boot is bigger than an apple");
            Assert.GreaterOrEqual(HeldLongest(items.Get("PENCIL")), HeldLongest(items.Get("BAT")), "the pencil spear is at least as long as BAT");
            foreach (string id in new[] { "LEMON", "PEAR", "TONIC", "TOMATO" })
                Assert.GreaterOrEqual(HeldLongest(items.Get(id)), HeldLongest(items.Get("APPLE")) * .95f, id + " reads like an APPLE in the hand");
        }

        [TestCase("BOOT"), TestCase("BRICK"), TestCase("LEMON"), TestCase("MARBLE"), TestCase("PEAR"), TestCase("SLIME"), TestCase("SOCK")]
        [TestCase("TOMATO"), TestCase("TONIC"), TestCase("TENT"), TestCase("PIANO"), TestCase("LADDER")]
        [TestCase("POTION"), TestCase("LATTE"), TestCase("PAINT"), TestCase("FENCE"), TestCase("CASTLE"), TestCase("CRAB")]
        [TestCase("ROBOT"), TestCase("RABBIT"), TestCase("TERRIER"), TestCase("CHOMPER"), TestCase("CONE"), TestCase("BLOCKS")]
        [TestCase("SPINNER"), TestCase("SATCHEL")]
        public void LabelsAndFrontsFaceTheCameraBehindThePlayer(string id)
        {
            Assert.Less(InBody(id, 0f, 0f, 1f).z, -.95f, id + " shows its +Z label, door or keys to the camera");
        }

        [TestCase("BOOT"), TestCase("BRICK"), TestCase("MARBLE"), TestCase("PEAR"), TestCase("SLIME"), TestCase("SOCK"), TestCase("TOMATO")]
        [TestCase("TONIC"), TestCase("TENT"), TestCase("TEAPOT"), TestCase("OIL"), TestCase("HOSE"), TestCase("RAMP"), TestCase("LID")]
        [TestCase("HELMET"), TestCase("DOOR"), TestCase("PIANO"), TestCase("LADDER"), TestCase("CARPET")]
        [TestCase("POTION"), TestCase("LATTE"), TestCase("PAINT"), TestCase("FENCE"), TestCase("CASTLE"), TestCase("CRAB"), TestCase("CAMERA")]
        [TestCase("ROBOT"), TestCase("RABBIT"), TestCase("TERRIER"), TestCase("BLASTER"), TestCase("CHOMPER"), TestCase("PATROL"), TestCase("CONE"), TestCase("BLOCKS")]
        [TestCase("SPINNER"), TestCase("COMPASS"), TestCase("SATCHEL")]
        public void ThingsWithATopKeepItUp(string id) => Assert.Greater(InBody(id, 0f, 1f, 0f).y, .99f, id + " sits the right way up");

        [TestCase("TEAPOT", 1f, 0f, 0f), TestCase("OIL", 0f, 0f, 1f), TestCase("RAMP", 0f, 0f, 1f)]
        public void SpoutsAndSlopesShowTheirProfileOutward(string id, float x, float y, float z)
        {
            Assert.Greater(InBody(id, x, y, z).x, .95f, id + " points its spout or low lip out to the right, side-on to the camera");
        }

        [Test]
        public void HoseAimsItsNozzleForward() => Assert.Greater(InBody("HOSE", 0f, 0f, 1f).z, .95f);

        [Test]
        public void LemonStandsOnItsLowerEndAndRisesOutOfTheFist()
        {
            Assert.Greater(InBody("LEMON", 1f, 0f, 0f).y, .99f, "the stem end is on top");
            var item = Live().Get("LEMON");
            Assert.Less(ItemHolds.Grip(item)[0], -item.Size[0] * .2f, "held below its middle so most of it shows above the fist");
        }

        [Test]
        public void DiscIsHeldByItsRimLikeAFrisbee()
        {
            var item = Live().Get("DISC");
            Assert.Less(item.Grip[0], -item.Size[0] * .4f, "the hand is on the rim");
            var reach = InBody("DISC", 1f, 0f, 0f);
            Assert.Greater(reach.z, .5f, "the disc reaches forward from the hand");
            Assert.Greater(reach.x, .5f, "and out to the right, clear of the wrist");
            Assert.Less(Math.Abs(reach.y), .1f, "held flat like a frisbee");
            var top = InBody("DISC", 0f, 1f, 0f);
            Assert.Greater(top.y, .8f);
            Assert.Less(top.z, -.2f, "its starred top tips toward the camera");
        }

        [Test]
        public void MelonShowsItsCutFaceUpToTheCamera()
        {
            var cut = InBody("MELON", 0f, 0f, 1f);
            Assert.Greater(cut.y, .8f);
            Assert.Less(cut.z, -.4f);
        }

        [Test]
        public void RakeTinesReachForwardAndSpoonShowsItsStar()
        {
            Assert.Greater(InBody("RAKE", 0f, 0f, 1f).z, .85f, "the tines point forward, away from the camera");
            Assert.Less(InBody("SPOON", 0f, 0f, -1f).z, -.85f, "the starred back of the bowl faces the camera");
            foreach (string id in new[] { "PADDLE", "RACKET", "TORCH", "ROCKET" })
                Assert.Less(InBody(id, 0f, 0f, 1f).z, -.7f, id + " shows its +Z face, badge, switch or port to the camera");
        }

        [Test]
        public void AnchorLeadsWithItsFlukesAndShowsItsStarToTheCamera()
        {
            Assert.Less(InBody("ANCHOR", 0f, 0f, 1f).z, -.7f, "the starred crown faces the camera behind the player");
            var item = Live().Get("ANCHOR");
            Assert.Greater(ItemHolds.Grip(item)[1], item.Size[1] * .5f, "held high on the shank so the heavy crown and flukes lead the swing");
        }

        [Test]
        public void MalletShowsItsBadgeAndCameraAimsItsLens()
        {
            Assert.Less(InBody("MALLET", 0f, 0f, 1f).z, -.7f, "the mallet's star badge faces the camera");
            Assert.Greater(InBody("CAMERA", 0f, 0f, 1f).z, .95f, "the lens points where the flash goes off");
            Assert.Greater(ItemHolds.Grip(Live().Get("CAMERA"))[0], 0f, "the right hand holds the camera by its right-hand grip");
        }

        [Test]
        public void GliderFliesNoseFirstAndALittleUp()
        {
            var nose = InBody("PLANE", 0f, 0f, 1f);
            Assert.Greater(nose.z, .9f, "nose forward, ready to throw");
            Assert.That(nose.y, Is.InRange(.15f, .4f), "tipped up a little like a launched glider");
            Assert.Greater(InBody("PLANE", 0f, 1f, 0f).y, .9f, "wings level, top up");
        }

        [TestCase("BLASTER"), TestCase("PATROL")]
        public void TurretsAndBuggiesPointTheWayTheyWillGo(string id)
        {
            Assert.Greater(InBody(id, 0f, 0f, 1f).z, .95f, id + " aims its barrel or nose forward, the way it acts once put down");
            var item = Live().Get(id);
            Assert.Less(ItemHolds.Grip(item)[1], item.Size[1] * .2f, id + " sits on the palm by its base or wheels");
        }

        [TestCase("ROBOT"), TestCase("RABBIT"), TestCase("TERRIER"), TestCase("CHOMPER")]
        public void ToyHelpersSitOnThePalmAndLookAtTheCamera(string id)
        {
            var item = Live().Get(id);
            Assert.Less(ItemHolds.Grip(item)[1], item.Size[1] * .4f, id + " is held low so its face and body show above the fist");
            Assert.AreEqual(0f, ItemHolds.Grip(item)[0], 1e-4f, id + " sits centred on the hand");
        }

        [TestCase("SPIKES"), TestCase("TRAP")]
        public void TrapsLieOnThePalmLikeATrayTippedToTheCamera(string id)
        {
            var top = InBody(id, 0f, 1f, 0f);
            Assert.That(top.y, Is.InRange(.8f, .9f), id + " lies nearly flat");
            Assert.Less(top.z, -.4f, id + " tips its spikes or cheese toward the camera");
        }

        [Test]
        public void HoopStandsOnItsRimAboveTheFistFacingTheCamera()
        {
            var hoop = Live().Get("HOOP");
            Assert.Greater(Math.Abs(InBody("HOOP", 0f, 1f, 0f).z), .95f, "the hoop stands up like a ring, not flat like it lies on the floor");
            Assert.Less(InBody("HOOP", 0f, 0f, 1f).y, -.95f, "the gripped front rim is the lowest point, so the hoop rises above the hand");
            Assert.Greater(ItemHolds.Grip(hoop)[2], hoop.Size[2] * .4f, "it is held by the rim, where the white grip tape is");
        }

        [Test]
        public void FlipperBladeLeadsAndSpinnerSitsOnItsTip()
        {
            Assert.Less(ItemHolds.Grip(Live().Get("FLIPPER"))[1], Live().Get("FLIPPER").Size[1] * .2f, "the flipper is held by its foot pocket");
            Assert.Less(ItemHolds.Grip(Live().Get("SPINNER"))[1], Live().Get("SPINNER").Size[1] * .3f, "the top sits in the palm, knob up");
        }

        [TestCase("FENCE"), TestCase("CASTLE"), TestCase("CONE"), TestCase("BLOCKS")]
        public void BigCoverIsCarriedUpAboveTheHandsNotLow(string id)
        {
            var item = Live().Get(id);
            Assert.Less(ItemHolds.Grip(item)[1], item.Size[1] * .25f, id + " is held near its base so it stands up above the hands, flags and pickets showing");
            Assert.Greater(InBody(id, 0f, 1f, 0f).y, .99f, id + " stands upright");
            Assert.Less(InBody(id, 0f, 0f, 1f).z, -.95f, id + " shows its front to the camera");
        }

        [Test]
        public void TheHandSocketsTipAnUnturnedItemBackTowardTheCamera()
        {
            var held = ItemHolds.OneHandSocket.Rotate(0f, 1f, 0f);
            Assert.Less(held.z, -.8f);
            Assert.Greater(held.y, .4f);
            var carried = ItemHolds.TwoHandSocket.Rotate(0f, 1f, 0f);
            Assert.Less(carried.z, -.95f, "a carried item lies on its back with its front to the sky");
        }

        [TestCase("PILLOW", false), TestCase("BEAR", false), TestCase("ARROW", false), TestCase("PAN", false)]
        [TestCase("BENCH", true), TestCase("CABINET", true), TestCase("SPEAKER", true)]
        [TestCase("BARREL", true), TestCase("BED", true), TestCase("CHAIR", true), TestCase("CRATE", true)]
        [TestCase("DESK", true), TestCase("SHELF", true), TestCase("SOFA", true), TestCase("STOOL", true), TestCase("TABLE", true)]
        [TestCase("CARPET", true), TestCase("DOOR", true), TestCase("LADDER", true), TestCase("PIANO", true)]
        [TestCase("LID", false), TestCase("HELMET", false), TestCase("TENT", false), TestCase("RAMP", false), TestCase("TEAPOT", false)]
        [TestCase("CASTLE", true), TestCase("FENCE", false), TestCase("ANCHOR", false), TestCase("CAMERA", false)]
        [TestCase("BLOCKS", true), TestCase("CONE", false), TestCase("ROBOT", false), TestCase("SPIKES", false)]
        [TestCase("SPINNER", false), TestCase("FLIPPER", false), TestCase("HOOP", false), TestCase("COMPASS", false)]
        public void EachHoldUsesTheSocketOfItsHandling(string id, bool twoHanded)
        {
            var item = Live().Get(id);
            Assert.AreEqual(twoHanded, item.IsTwoHanded);
            Assert.AreEqual(twoHanded ? ItemHolds.TwoHandSocket : ItemHolds.OneHandSocket, ItemHolds.Socket(item));
        }

        [TestCase("BAT"), TestCase("HAMMER"), TestCase("BROOM"), TestCase("SHIELD"), TestCase("WATER"), TestCase("LAMP"), TestCase("MAT")]
        public void UnlistedItemsKeepTheHandSocketAndTheirCatalogueGrip(string id)
        {
            var item = Live().Get(id);
            var turn = ItemHolds.InHand(item);
            Assert.AreEqual((0f, 0f, 0f, 1f), (turn.X, turn.Y, turn.Z, turn.W));
            Assert.AreSame(item.Grip, ItemHolds.Grip(item));
            Assert.IsNull(ItemHolds.For(id));
        }

        [TestCase("BENCH"), TestCase("CABINET"), TestCase("SPEAKER"), TestCase("BARREL"), TestCase("BED"), TestCase("CHAIR")]
        [TestCase("CRATE"), TestCase("DESK"), TestCase("SHELF"), TestCase("SOFA"), TestCase("STOOL"), TestCase("TABLE")]
        [TestCase("CARPET"), TestCase("DOOR"), TestCase("LADDER"), TestCase("PIANO"), TestCase("CASTLE"), TestCase("BLOCKS")]
        public void CarriedFurnitureStandsUprightInTheTwoHandedCarry(string id)
        {
            Assert.Greater(InBody(id, 0f, 1f, 0f).y, .95f, id + " stands on its feet in the carry");
        }

        [TestCase("SOFA"), TestCase("DESK"), TestCase("SHELF"), TestCase("CHAIR")]
        public void SeatsDrawersAndShelvesFaceTheCarrier(string id)
        {
            Assert.Less(InBody(id, 0f, 0f, 1f).z, -.95f, id + " shows its +Z front to the camera behind the carrier");
        }

        [TestCase("TABLE", 1f, 0f), TestCase("DESK", 1f, 0f), TestCase("SOFA", 1f, 0f), TestCase("SHELF", 1f, 0f), TestCase("BED", 0f, 1f)]
        [TestCase("CARPET", 0f, 1f), TestCase("PIANO", 1f, 0f), TestCase("LADDER", 1f, 0f), TestCase("FENCE", 1f, 0f), TestCase("CASTLE", 1f, 0f)]
        [TestCase("CONE", 1f, 0f), TestCase("BLOCKS", 1f, 0f)]
        public void LongFurnitureRunsAcrossTheBody(string id, float x, float z)
        {
            Assert.Greater(Math.Abs(InBody(id, x, 0f, z).x), .95f, id + " carries its long side across the chest");
        }

        [Test]
        public void BedShowsItsSideProfileToTheCamera() => Assert.Greater(Math.Abs(InBody("BED", 1f, 0f, 0f).z), .95f);

        [TestCase("BARREL"), TestCase("BED"), TestCase("CHAIR"), TestCase("CRATE"), TestCase("DESK")]
        [TestCase("PAN"), TestCase("SHELF"), TestCase("SOFA"), TestCase("STOOL"), TestCase("TABLE")]
        public void CarryTurnsLeaveTheGripAndThePlacedModelAsAuthored(string id)
        {
            var hold = ItemHolds.For(id);
            Assert.IsNull(hold.Grip, id + " keeps its catalogue grip");
            Assert.AreEqual(0f, hold.ModelYaw, id + " is placed facing the way it was put down");
            Assert.IsNull(hold.ModelShift, id);
        }

        [Test]
        public void PanRisesFromTheFistWithItsFaceTowardTheCamera()
        {
            var length = InBody("PAN", 1f, 0f, 0f);
            Assert.Greater(length.y, .9f, "the pan head rises out of the fist like a raised BAT");
            Assert.Less(length.z, 0f, "and leans back over the hand");
            Assert.Less(InBody("PAN", 0f, 1f, 0f).z, -.9f, "its cooking face, not its rim, is toward the camera behind");
        }

        [Test]
        public void BenchRunsAcrossTheBodyAndReachesPastTheRightHand()
        {
            Assert.Greater(InBody("BENCH", 1f, 0f, 0f).x, .95f);
            Assert.Less(ItemHolds.Grip(Live().Get("BENCH"))[0], 0f, "more of the bench reaches out past the right hand where the camera sees it");
        }

        [TestCase("CABINET"), TestCase("SPEAKER")]
        public void CabinetDoorsAndSpeakerConesFaceTheCarrier(string id)
        {
            var front = HoldTurn.Euler(0f, ItemHolds.For(id).ModelYaw, 0f).Rotate(0f, 0f, 1f);
            Assert.Less(InBody(id, front.x, front.y, front.z).z, -.95f, id + " shows its front to the third-person camera");
        }

        [Test]
        public void PlacedCabinetShowsItsDoorsToThePlacerAndFillsItsCoverFootprint()
        {
            var item = Live().Get("CABINET");
            var hold = ItemHolds.For("CABINET");
            var turn = HoldTurn.Euler(0f, hold.ModelYaw, 0f);
            Assert.Less(turn.Rotate(0f, 0f, 1f).z, -.99f, "the doors face back toward the player who set it down");
            Assert.AreEqual(0f, turn.Rotate(0f, 0f, item.Grip[2]).z + hold.ModelShift[2], 1e-4f, "the deeper door side no longer pokes out of the box collider");
            Assert.AreEqual(0f, ItemHolds.Grip(item)[2], "the hand holds the centred cabinet at its middle");
        }

        [Test]
        public void ArrowTipLeadsForwardAndUpReadyToThrow()
        {
            var tip = InBody("ARROW", 0f, 1f, 0f);
            Assert.Greater(tip.z, .5f, "the tip points where it will fly");
            Assert.Greater(tip.y, .5f, "raised so the camera behind sees its length");
        }

        [Test]
        public void PillowRisesFromTheFistWithItsFaceTowardTheCamera()
        {
            var length = InBody("PILLOW", 1f, 0f, 0f);
            Assert.Greater(length.y, .5f, "held by one end, the pillow rises out of the fist");
            Assert.Greater(length.x, .5f, "and leans away from the head");
            Assert.Greater(Math.Abs(InBody("PILLOW", 0f, 0f, 1f).z), .75f, "its broad face, not its edge, is toward the camera behind");
        }

        [Test]
        public void TeddyBearSitsUpAndLooksBackAtItsThrower()
        {
            Assert.Greater(InBody("BEAR", 0f, 1f, 0f).y, .99f);
            Assert.Less(InBody("BEAR", 0f, 0f, 1f).z, -.95f, "the teddy faces the prefab's +Z");
        }
    }
}
