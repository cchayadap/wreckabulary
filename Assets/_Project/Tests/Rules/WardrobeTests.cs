using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class WardrobeTests
    {
        WardrobeCatalogue w;

        [SetUp]
        public void Load() => w = TestData.Wardrobe();

        [Test]
        public void ShippedWardrobeIsValid()
        {
            Assert.IsEmpty(w.Validate());
            Assert.IsTrue(w.Fits(w.Default));
            Assert.GreaterOrEqual(w.Palettes.Values.Sum(p => p.Count), 40, "plenty of colourways");
        }

        [Test]
        public void TheHoodNeedsTheHoodie()
        {
            var crewneck = w.Wear(w.Default, "Top", "Crewneck");
            Assert.IsNotNull(crewneck);
            CollectionAssert.DoesNotContain(w.Choices(crewneck, "Headwear"), "Hood");
            var forced = crewneck.Clone();
            forced.Pieces["Headwear"] = "Hood";
            Assert.IsFalse(w.Fits(forced));

            var hoodie = w.Wear(crewneck, "Top", "Hoodie");
            Assert.IsNotNull(hoodie);
            CollectionAssert.Contains(w.Choices(hoodie, "Headwear"), "Hood");
            var hooded = w.Wear(hoodie, "Headwear", "Hood");
            Assert.IsTrue(w.Fits(hooded));

            var back = w.Wear(hooded, "Top", "Crewneck");
            Assert.IsNotNull(back, "switching tops takes the Hood off rather than refusing");
            Assert.IsNull(back.PieceIn("Headwear"));
            Assert.IsTrue(w.Fits(back));
        }

        [Test]
        public void RequiredSlotsCantBeEmptied()
        {
            Assert.IsNull(w.Wear(w.Default, "Top", null));
            CollectionAssert.DoesNotContain(w.Choices(w.Default, "Top"), null);
            CollectionAssert.Contains(w.Choices(w.Default, "Headwear"), null, "headwear is optional");
            Assert.IsNotNull(w.Wear(w.Default, "Badge", null));
        }

        [Test]
        public void TheHoodTakesTheTopsColour()
        {
            var o = w.Wear(w.Wear(w.Default, "Top", "Hoodie"), "Headwear", "Hood");
            o.Colours["Top"] = "sunflower";
            o.Colours["Headwear"] = "mint";
            Assert.AreEqual("sunflower", w.ColourFor(o, "Headwear").Id);
            var capped = w.Wear(o, "Headwear", "Cap");
            Assert.AreEqual("mint", w.ColourFor(capped, "Headwear").Id);
            Assert.AreEqual("tomato", w.ColourFor(w.Default, "Top").Id);
            Assert.AreEqual("tomato", w.ColourFor(w.Default, "Headwear").Id, "the default hood matches the default hoodie");
        }

        [Test]
        public void OutfitsSurviveSavingAndSending()
        {
            var o = w.Wear(w.Wear(w.Default, "Top", "Hoodie"), "Headwear", "Hood");
            o.Colours["Top"] = "grape";
            o.ItemSkins["BAT"] = "Candy";
            o.ItemSkins["PLATE"] = "Arcade";
            var copy = Outfit.Deserialize(o.Serialize());
            Assert.AreEqual(o.Serialize(), copy.Serialize());
            Assert.AreEqual("Hood", copy.PieceIn("Headwear"));
            Assert.AreEqual("grape", copy.ColourOf("Top"));
            Assert.AreEqual("Candy", copy.SkinFor("BAT"));
            Assert.IsTrue(w.Fits(copy));
        }

        [Test]
        public void BadOrTamperedOutfitsFallBackToTheDefault()
        {
            var tampered = Outfit.Deserialize("Top=Tuxedo:gold;Headwear=Hood;Bottoms=Joggers:denim|BAT=Candy");
            Assert.IsFalse(w.Fits(tampered));
            var safe = w.Sanitize(tampered);
            Assert.IsTrue(w.Fits(safe));
            Assert.AreEqual("Hoodie", safe.PieceIn("Top"));
            Assert.AreEqual("denim", safe.ColourOf("Bottoms"), "valid choices are kept");
            Assert.AreEqual("Candy", safe.SkinFor("BAT"), "item skins are kept; unknown ones fall back when shown");
            Assert.IsTrue(w.Fits(w.Sanitize(null)));
            Assert.IsTrue(w.Fits(w.Sanitize(Outfit.Deserialize("garbage"))));
        }

        [Test]
        public void EveryTintedPieceHasAPalette()
        {
            foreach (var piece in w.Pieces.Where(p => p.TintMaterial != null))
                Assert.IsTrue(w.Palettes.ContainsKey(piece.ColourFrom ?? piece.Slot), piece.Id);
        }
    }
}
