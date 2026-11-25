using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class ItemPaletteTests
    {
        const string MaterialsFile = "Assets/_Project/Data/Generated/materials.json";
        const string Classic = "_Classic";
        const float MainSaturationLow = 0.60f, MainSaturationHigh = 0.85f;
        const float MainValueLow = 0.70f, MainValueHigh = 0.92f;
        const float NeutralSaturation = 0.20f, NeutralValue = 0.30f, NeutralShareCap = 0.25f;
        const float AccentSaturation = 0.50f, AccentShare = 0.01f, AccentHue = 40f, AccentValue = 0.25f;
        const float RoughnessLow = 0.40f, RoughnessHigh = 0.55f;
        const float SimilarHue = 25f, SimilarValue = 0.15f;
        const float ShareTolerance = 0.02f;

        static ItemPalette palette;
        static Dictionary<string, (PaletteColour colour, float roughness)> library;

        static ItemPalette Palette() => palette ??= ItemPalette.FromJson(File.ReadAllText(Path.Combine(TestData.Root, ItemPalette.SourceFile)), "item_palette.json");

        static Dictionary<string, (PaletteColour colour, float roughness)> Library()
        {
            if (library != null) return library;
            var root = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, MaterialsFile)), "materials.json");
            library = new Dictionary<string, (PaletteColour, float)>(StringComparer.Ordinal);
            foreach (var m in root["materials"].Items)
            {
                var c = m["baseColor"].Floats(4);
                library[m["name"].String()] = (PaletteColour.FromLinear(c[0], c[1], c[2]), m["roughness"].Float());
            }
            return library;
        }

        static PaletteColour ColourOf(string family)
        {
            if (Palette().Families.TryGetValue(family, out var own)) return own.Colour;
            Assert.IsTrue(Library().TryGetValue(family + Classic, out var shared), family + Classic + " is not in materials.json");
            return shared.colour;
        }

        static bool Neutral(string family)
        {
            var c = ColourOf(family);
            return c.Saturation < NeutralSaturation || c.Value < NeutralValue;
        }

        static IEnumerable<string> ModelledItems()
        {
            var catalogue = TestData.Catalogue();
            catalogue.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return catalogue.All.Where(item => !string.IsNullOrEmpty(item.Model) && item.Model == "Items/" + item.Id).Select(item => item.Id);
        }

        [Test]
        public void EveryModelledItemHasALookAndOnlyItemsDo()
        {
            var modelled = ModelledItems().ToList();
            Assert.GreaterOrEqual(modelled.Count, 47);
            CollectionAssert.AreEquivalent(modelled, Palette().Items.Keys);
        }

        [Test]
        public void SlotsAreRealLibraryFamiliesAndSharesAddUp()
        {
            foreach (var entry in Palette().Items)
            {
                Assert.AreEqual(1f, entry.Value.Sum(p => p.Share), ShareTolerance, entry.Key);
                foreach (var part in entry.Value)
                {
                    StringAssert.DoesNotStartWith(ItemPalette.FamilyPrefix, part.Family, entry.Key + " slots are the model's own library families");
                    Assert.IsTrue(Library().ContainsKey(part.Family + Classic), $"{entry.Key} {part.Family}");
                    if (part.Target != null)
                        Assert.IsTrue(Palette().Families.ContainsKey(part.Target) || Library().ContainsKey(part.Target + Classic), $"{entry.Key} {part.Family} -> {part.Target}");
                }
            }
        }

        [Test]
        public void EachTargetComesFromOneSlotSoOtherFinishesFindTheirWayBack()
        {
            foreach (var entry in Palette().Items)
            {
                var native = new HashSet<string>(entry.Value.Select(p => p.Family));
                var targets = entry.Value.Where(p => p.Target != null).Select(p => p.Target).ToList();
                CollectionAssert.AllItemsAreUnique(targets, entry.Key);
                foreach (string target in targets)
                {
                    Assert.IsFalse(native.Contains(target), $"{entry.Key} already has a {target} slot");
                    Assert.IsTrue(Palette().TryMap(entry.Key, Palette().SourceOf(entry.Key, target), out string back) && back == target, entry.Key + " " + target);
                }
            }
        }

        [Test]
        public void ItemFamiliesAreGlossyEnoughAndBuiltIntoTheLibrary()
        {
            foreach (var family in Palette().Families.Values)
            {
                Assert.That(family.Roughness, Is.InRange(RoughnessLow, RoughnessHigh), family.Id);
                foreach (string finish in new[] { "Classic", "Candy", "Arcade" })
                    Assert.IsTrue(Library().ContainsKey(family.Id + "_" + finish), $"{family.Id}_{finish}: run Tools/AssetPipeline/finish_materials.py");
                var built = Library()[family.Id + Classic];
                Assert.AreEqual(family.Colour.Hex, built.colour.Hex, family.Id);
                Assert.AreEqual(family.Roughness, built.roughness, 1e-4f, family.Id);
                Assert.IsTrue(Library().ContainsKey(family.CandyFrom + "_Candy"), family.Id + " candy source");
                Assert.IsTrue(Library().ContainsKey(family.ArcadeFrom + "_Arcade"), family.Id + " arcade source");
            }
        }

        [Test]
        public void MainColoursAreVividAndNeutralsStayTrims()
        {
            var wrong = new List<string>();
            foreach (string item in Palette().Items.Keys)
            {
                if (Palette().NeutralByRole.ContainsKey(item)) continue;
                string main = Palette().MainFamily(item);
                var c = ColourOf(main);
                if (c.Saturation < MainSaturationLow || c.Saturation > MainSaturationHigh) wrong.Add($"{item} {main} {c.Hex} saturation {c.Saturation:F2}");
                if (c.Value < MainValueLow || c.Value > MainValueHigh) wrong.Add($"{item} {main} {c.Hex} value {c.Value:F2}");
                float neutral = Palette().FinalShares(item).Where(s => Neutral(s.Key)).Sum(s => s.Value);
                if (neutral > NeutralShareCap) wrong.Add($"{item} is {neutral:P0} white, grey or black");
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void EveryItemCarriesAContrastingAccent()
        {
            var wrong = new List<string>();
            foreach (string item in Palette().Items.Keys)
            {
                string main = Palette().MainFamily(item);
                var body = ColourOf(main);
                bool found = Palette().FinalShares(item).Any(s => s.Key != main && s.Value >= AccentShare && ColourOf(s.Key).Saturation >= AccentSaturation &&
                    (PaletteColour.HueDistance(body, ColourOf(s.Key)) >= AccentHue || Math.Abs(body.Value - ColourOf(s.Key).Value) >= AccentValue));
                if (!found) wrong.Add(item + " has no contrasting accent against " + main);
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void SimilarItemsKeepDifferentMainColours()
        {
            var wrong = new List<string>();
            foreach (var group in Palette().Similar)
            {
                foreach (string id in group) Assert.IsTrue(Palette().Items.ContainsKey(id), id);
                for (int i = 0; i < group.Count; i++)
                    for (int j = i + 1; j < group.Count; j++)
                    {
                        string a = Palette().MainFamily(group[i]), b = Palette().MainFamily(group[j]);
                        var ca = ColourOf(a); var cb = ColourOf(b);
                        if (a == b || PaletteColour.HueDistance(ca, cb) < SimilarHue && Math.Abs(ca.Value - cb.Value) < SimilarValue)
                            wrong.Add($"{group[i]} ({a}) and {group[j]} ({b}) look alike");
                    }
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void NeutralByRoleItemsAreFewAndExplained()
        {
            Assert.LessOrEqual(Palette().NeutralByRole.Count, 3);
            foreach (var entry in Palette().NeutralByRole)
            {
                Assert.IsTrue(Palette().Items.ContainsKey(entry.Key), entry.Key);
                Assert.IsNotEmpty(entry.Value, entry.Key);
            }
        }

        [Test]
        public void MappingRoundTripsForFinishes()
        {
            Assert.IsTrue(Palette().TryMap("LAMP", "cream", out string target));
            Assert.AreEqual("toy_yellow", target);
            Assert.AreEqual("cream", Palette().SourceOf("LAMP", target));
            Assert.IsFalse(Palette().TryMap("LAMP", "gold", out _));
            Assert.IsFalse(Palette().TryMap("SOFA_FURNITURE", "green", out _));
            Assert.AreEqual("gold", Palette().SourceOf("LAMP", "gold"));
        }

        [Test]
        public void ColourMathMatchesKnownValues()
        {
            var red = PaletteColour.FromHex("#FF0000");
            Assert.AreEqual(0f, red.Hue, 1e-3f);
            Assert.AreEqual(1f, red.Saturation, 1e-3f);
            Assert.AreEqual(240f, PaletteColour.FromHex("#0000FF").Hue, 1e-3f);
            Assert.AreEqual(120f, PaletteColour.HueDistance(red, PaletteColour.FromHex("#00FF00")), 1e-3f);
            Assert.AreEqual(40f, PaletteColour.HueDistance(PaletteColour.FromHex("#FF0055"), PaletteColour.FromHex("#FF5500")), 0.2f);
            Assert.AreEqual("#E8402A", PaletteColour.FromHex("#e8402a").Hex);
            Assert.AreEqual("#BCBCBC", PaletteColour.FromLinear(0.5f, 0.5f, 0.5f).Hex);
        }
    }
}
