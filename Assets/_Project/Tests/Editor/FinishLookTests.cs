using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public class FinishLookTests
    {
        const float SlotBar = 12f, ItemBar = 18f;
        static readonly Dictionary<string, (string swatch, string accent)> DiscFamilies = new Dictionary<string, (string, string)>
        {
            ["Classic"] = ("wood_light", "wood_dark"), ["Candy"] = ("wood_light", "wood"), ["Arcade"] = ("wood", "cream"), ["Winter"] = ("blue", "gold"),
        };
        static readonly Dictionary<string, Color> means = new Dictionary<string, Color>();
        MaterialLibrary library;
        ModelLibrary models;
        List<ItemDefinition> items;

        [OneTimeSetUp]
        public void Load()
        {
            GameConfig.Use(null);
            library = MaterialLibrary.Load();
            models = ModelLibrary.Load();
            Assert.IsNotNull(library, "run Wreckabulary > Art > Finishes > Build materials");
            Assert.IsNotNull(models);
            items = FinishArt.SkinnedItems().ToList();
            Assert.Greater(items.Count, 40);
        }

        [OneTimeTearDown]
        public void Forget() => GameConfig.Use(null);

        IEnumerable<(ItemDefinition item, Material[] slots)> ItemSlots() => items.Select(item => (item,
            models.Find(item.Model).GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m && MaterialLibrary.FamilyOf(m.name) != null).Distinct().ToArray()));

        [Test]
        public void EveryListedFinishResolvesWithoutFallingBack()
        {
            var wrong = new List<string>();
            foreach (var (item, slots) in ItemSlots())
            {
                if (slots.Length == 0) wrong.Add(item.Id + ": no material has a finish family");
                wrong.AddRange(item.Skins.Where(s => Finishes.Find(s) == null).Select(s => $"{item.Id}: lists {s}, which is not in the Finishes registry"));
                foreach (var finish in Finishes.ForItem(item.Id, item.Skins))
                    foreach (var slot in slots)
                    {
                        string expected = Finishes.FamilyOf(slot.name) + finish.Suffix;
                        if (!library.Resolves(slot, finish.Id) || library.ForSkin(slot, finish.Id).name != expected)
                            wrong.Add($"{item.Id}: {finish.Id} needs {expected}");
                    }
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong.Take(30)));
        }

        [Test]
        public void EveryHouseItemOffersEveryFinishItLists()
        {
            foreach (string word in HouseItemsSetup.Words.Concat(HouseItemsV2Setup.Words))
            {
                var item = GameConfig.Current.Items.Get(word);
                CollectionAssert.AreEqual(new[] { "Classic", "Candy", "Arcade" }, Finishes.ForItem(word, item.Skins).Select(f => f.Id), word);
                var slots = ItemSlots().Single(entry => entry.item.Id == word).slots;
                Assert.IsNotEmpty(slots, word);
                foreach (var finish in Finishes.ForItem(word, item.Skins))
                    foreach (var slot in slots)
                        Assert.IsTrue(library.Resolves(slot, finish.Id), $"{word} {slot.name} {finish.Id}");
            }
        }

        [Test]
        public void FinishesDifferInEverySlotAndAcrossTheItem()
        {
            var wrong = new List<string>();
            float lowestSlot = float.MaxValue, lowestMean = float.MaxValue;
            foreach (var (item, slots) in ItemSlots())
            {
                var offered = Finishes.ForItem(item.Id, item.Skins).ToList();
                for (int a = 0; a < offered.Count; a++)
                    for (int b = a + 1; b < offered.Count; b++)
                    {
                        var distances = slots.Select(slot => (slot: Finishes.FamilyOf(slot.name),
                            de: DeltaE2000(Appearance(library.ForSkin(slot, offered[a].Id)), Appearance(library.ForSkin(slot, offered[b].Id))))).ToList();
                        float mean = distances.Average(d => d.de);
                        lowestMean = Mathf.Min(lowestMean, mean);
                        foreach (var d in distances)
                        {
                            lowestSlot = Mathf.Min(lowestSlot, d.de);
                            if (d.de < SlotBar) wrong.Add($"{item.Id} {d.slot}: {offered[a].Id} vs {offered[b].Id} differ by {d.de:F1}");
                        }
                        if (mean < ItemBar) wrong.Add($"{item.Id}: {offered[a].Id} vs {offered[b].Id} mean {mean:F1}");
                    }
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong.Take(30)));
            TestContext.WriteLine($"lowest slot dE2000 {lowestSlot:F1}, lowest item mean {lowestMean:F1}");
        }

        [Test]
        public void EveryNonClassicFinishCarriesItsSignature()
        {
            var wrong = new List<string>();
            foreach (var (item, slots) in ItemSlots())
                foreach (var finish in Finishes.ForItem(item.Id, item.Skins).Where(f => f != Finishes.Classic))
                {
                    var worn = slots.Select(slot => library.ForSkin(slot, finish.Id)).ToArray();
                    if (!worn.Any(m => Signature(m, finish.Id))) wrong.Add($"{item.Id}: {finish.Id} has no pattern or glow on any part");
                }
            Assert.IsEmpty(wrong, string.Join("\n", wrong.Take(30)));
        }

        [Test]
        public void ClassicKeepsItsLookWithoutFinishPatterns()
        {
            foreach (var material in library.Materials.Where(m => Finishes.FinishOf(m.name) == Finishes.Classic.Id))
            {
                Assert.IsFalse(IsPattern(material.GetTexture("_BaseMap")), material.name);
                Assert.IsFalse(IsPattern(material.GetTexture("_EmissionMap")), material.name);
            }
        }

        [Test]
        public void DiscColoursComeFromTheRealMaterials()
        {
            foreach (var finish in Finishes.All)
            {
                var (swatch, accent) = DiscFamilies[finish.Id];
                AssertHex(finish.Swatch, library.Find(finish.MaterialFor(swatch)), finish.Id + " swatch");
                AssertHex(finish.Accent, library.Find(finish.MaterialFor(accent)), finish.Id + " accent");
            }
        }

        [Test]
        public void WinterCoversExactlyTheSeasonalRecipes()
        {
            var winter = SeasonalCollection.Winter;
            Assert.IsNotNull(winter);
            Assert.AreEqual("Winter", winter.finish);
            CollectionAssert.AreEquivalent(winter.recipes, Finishes.Find("Winter").OnlyFor);
            foreach (var (spec, folder) in MaterialLibraryBuilder.ReadSeasonal())
            {
                var material = winter.FindMaterial(spec.Name);
                Assert.IsNotNull(material, spec.Name);
                Assert.AreEqual(folder + "/" + spec.Name + ".mat", AssetDatabase.GetAssetPath(material));
                var linear = material.GetColor("_BaseColor").linear;
                Assert.AreEqual(spec.LinearBaseColor.r, linear.r, .01f, spec.Name);
                Assert.AreEqual(spec.LinearBaseColor.g, linear.g, .01f, spec.Name);
                Assert.AreEqual(spec.LinearBaseColor.b, linear.b, .01f, spec.Name);
                Assert.AreSame(material, library.Find(spec.Name), spec.Name);
            }
        }

        [Test]
        public void PatternsAreSmallTileableAndSetUpToRepeat()
        {
            var paths = Directory.GetFiles(MaterialLibraryBuilder.PatternFolder, "*.png").Select(p => p.Replace('\\', '/')).ToArray();
            Assert.GreaterOrEqual(paths.Length, 4);
            foreach (string path in paths)
            {
                Assert.IsTrue(MaterialLibraryBuilder.IsPattern(AssetImporter.GetAtPath(path) as TextureImporter), path + " imports as a repeating pattern");
                var image = new Texture2D(2, 2);
                try
                {
                    Assert.IsTrue(image.LoadImage(File.ReadAllBytes(path)), path);
                    Assert.AreEqual(image.width, image.height, path);
                    Assert.That(image.width, Is.InRange(256, 512), path);
                    var pixels = image.GetPixels32();
                    int n = image.width;
                    float Column(int a, int b) => Enumerable.Range(0, n).Average(y => Diff(pixels[y * n + a], pixels[y * n + b]));
                    float Row(int a, int b) => Enumerable.Range(0, n).Average(x => Diff(pixels[a * n + x], pixels[b * n + x]));
                    float innerColumns = Enumerable.Range(0, n - 1).Max(x => Column(x, x + 1));
                    float innerRows = Enumerable.Range(0, n - 1).Max(y => Row(y, y + 1));
                    Assert.LessOrEqual(Column(n - 1, 0), innerColumns + 1f, path + " wraps left to right");
                    Assert.LessOrEqual(Row(n - 1, 0), innerRows + 1f, path + " wraps top to bottom");
                }
                finally { Object.DestroyImmediate(image); }
            }
        }

        [Test]
        public void EmblemsAndFinishIconsExist()
        {
            foreach (var finish in Finishes.All)
            {
                var emblem = Resources.Load<Texture2D>(finish.Emblem);
                Assert.IsNotNull(emblem, finish.Emblem);
                Assert.AreEqual(256, emblem.width, finish.Id);
                Assert.IsTrue(FinishArt.IsIcon(AssetImporter.GetAtPath(FinishArt.EmblemPath(finish.Id)) as TextureImporter), finish.Id);
            }
            var missing = new List<string>();
            foreach (var item in items)
                foreach (var finish in Finishes.ForItem(item.Id, item.Skins).Where(f => f != Finishes.Classic))
                {
                    var icon = Resources.Load<Texture2D>("UI/Items/Finishes/" + item.Id + "_" + finish.Id);
                    if (!icon || icon.width != FinishArt.IconSize || icon.height != FinishArt.IconSize) missing.Add(item.Id + "_" + finish.Id);
                }
            Assert.IsEmpty(missing, "run Wreckabulary > Art > Finishes > Render finish icons: " + string.Join(", ", missing.Take(20)));
        }

        [Test]
        public void AMissingFinishMaterialIsReportedNotSilent()
        {
            string family = "nosuch" + Guid.NewGuid().ToString("N");
            var stray = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = family + "_Classic" };
            try
            {
                Assert.IsFalse(library.Resolves(stray, "Candy"));
                LogAssert.Expect(LogType.Warning, new Regex(family + "_Candy"));
                Assert.AreSame(stray, library.ForSkin(stray, "Candy"));
                CollectionAssert.Contains(MaterialLibrary.MissingFinishes.ToList(), family + "_Candy");
                Assert.AreSame(stray, library.ForSkin(stray, "Gold"), "an unregistered skin keeps the material without a warning");
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(stray); }
        }

        static bool Signature(Material material, string finish) => finish switch
        {
            "Candy" => PatternName(material.GetTexture("_BaseMap")) == "candy_stripes",
            "Arcade" => material.IsKeywordEnabled("_EMISSION") && material.GetColor("_EmissionColor").maxColorComponent > .1f,
            "Winter" => PatternName(material.GetTexture("_BaseMap")) == "winter_frost" || PatternName(material.GetTexture("_EmissionMap")) == "winter_glints",
            _ => IsPattern(material.GetTexture("_BaseMap")) || IsPattern(material.GetTexture("_EmissionMap")) || material.IsKeywordEnabled("_EMISSION"),
        };

        static bool IsPattern(Texture texture) => PatternName(texture) != null;

        static string PatternName(Texture texture)
        {
            string path = texture ? AssetDatabase.GetAssetPath(texture) : null;
            return path != null && path.StartsWith(MaterialLibraryBuilder.PatternFolder + "/", StringComparison.Ordinal) ? Path.GetFileNameWithoutExtension(path) : null;
        }

        static void AssertHex(uint hex, Material material, string label)
        {
            Assert.IsNotNull(material, label);
            var colour = material.GetColor("_BaseColor");
            var expected = new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
            Color32 actual = colour;
            Assert.LessOrEqual(Math.Abs(actual.r - expected.r) + Math.Abs(actual.g - expected.g) + Math.Abs(actual.b - expected.b), 6, $"{label}: {material.name} is {ColorUtility.ToHtmlStringRGB(colour)}");
        }

        public static Color Appearance(Material material)
        {
            var albedo = material.GetColor("_BaseColor").linear * Mean(material.GetTexture("_BaseMap"));
            var glow = material.IsKeywordEnabled("_EMISSION") ? material.GetColor("_EmissionColor").linear * Mean(material.GetTexture("_EmissionMap")) : Color.black;
            return new Color(Mathf.Clamp01(albedo.r + glow.r), Mathf.Clamp01(albedo.g + glow.g), Mathf.Clamp01(albedo.b + glow.b));
        }

        static Color Mean(Texture texture)
        {
            string path = texture ? AssetDatabase.GetAssetPath(texture) : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Color.white;
            if (means.TryGetValue(path, out var known)) return known;
            var image = new Texture2D(2, 2);
            try
            {
                image.LoadImage(File.ReadAllBytes(path));
                double r = 0, g = 0, b = 0;
                var pixels = image.GetPixels32();
                foreach (var p in pixels) { r += Mathf.GammaToLinearSpace(p.r / 255f); g += Mathf.GammaToLinearSpace(p.g / 255f); b += Mathf.GammaToLinearSpace(p.b / 255f); }
                return means[path] = new Color((float)(r / pixels.Length), (float)(g / pixels.Length), (float)(b / pixels.Length));
            }
            finally { Object.DestroyImmediate(image); }
        }

        static float Diff(Color32 a, Color32 b) => (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b)) / 3f;

        static Vector3 Lab(Color linear)
        {
            double r = linear.r, g = linear.g, b = linear.b;
            double x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047, y = 0.2126 * r + 0.7152 * g + 0.0722 * b, z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
            double F(double t) => t > 216.0 / 24389 ? Math.Pow(t, 1.0 / 3) : (24389.0 / 27 * t + 16) / 116;
            return new Vector3((float)(116 * F(y) - 16), (float)(500 * (F(x) - F(y))), (float)(200 * (F(y) - F(z))));
        }

        public static float DeltaE2000(Color linearA, Color linearB) => DeltaE2000(Lab(linearA), Lab(linearB));

        public static float DeltaE2000(Vector3 p, Vector3 q)
        {
            double l1 = p.x, a1 = p.y, b1 = p.z, l2 = q.x, a2 = q.y, b2 = q.z;
            double c1 = Math.Sqrt(a1 * a1 + b1 * b1), c2 = Math.Sqrt(a2 * a2 + b2 * b2), cb = (c1 + c2) / 2;
            double g = 0.5 * (1 - Math.Sqrt(Math.Pow(cb, 7) / (Math.Pow(cb, 7) + Math.Pow(25, 7))));
            double a1p = (1 + g) * a1, a2p = (1 + g) * a2, c1p = Math.Sqrt(a1p * a1p + b1 * b1), c2p = Math.Sqrt(a2p * a2p + b2 * b2);
            double Deg(double y, double x) { double h = Math.Atan2(y, x) * 180 / Math.PI; return h < 0 ? h + 360 : h; }
            double h1p = Deg(b1, a1p), h2p = Deg(b2, a2p), dl = l2 - l1, dc = c2p - c1p, dh;
            if (c1p * c2p == 0) dh = 0;
            else if (Math.Abs(h2p - h1p) <= 180) dh = h2p - h1p;
            else dh = h2p - h1p > 180 ? h2p - h1p - 360 : h2p - h1p + 360;
            double dH = 2 * Math.Sqrt(c1p * c2p) * Math.Sin(dh * Math.PI / 360), lb = (l1 + l2) / 2, cbp = (c1p + c2p) / 2, hb;
            if (c1p * c2p == 0) hb = h1p + h2p;
            else if (Math.Abs(h1p - h2p) <= 180) hb = (h1p + h2p) / 2;
            else hb = h1p + h2p < 360 ? (h1p + h2p + 360) / 2 : (h1p + h2p - 360) / 2;
            double Rad(double d) => d * Math.PI / 180;
            double t = 1 - 0.17 * Math.Cos(Rad(hb - 30)) + 0.24 * Math.Cos(Rad(2 * hb)) + 0.32 * Math.Cos(Rad(3 * hb + 6)) - 0.20 * Math.Cos(Rad(4 * hb - 63));
            double dth = 30 * Math.Exp(-Math.Pow((hb - 275) / 25, 2)), rc = 2 * Math.Sqrt(Math.Pow(cbp, 7) / (Math.Pow(cbp, 7) + Math.Pow(25, 7)));
            double sl = 1 + 0.015 * (lb - 50) * (lb - 50) / Math.Sqrt(20 + (lb - 50) * (lb - 50)), sc = 1 + 0.045 * cbp, sh = 1 + 0.015 * cbp * t;
            double rt = -Math.Sin(Rad(2 * dth)) * rc;
            return (float)Math.Sqrt(Math.Pow(dl / sl, 2) + Math.Pow(dc / sc, 2) + Math.Pow(dH / sh, 2) + rt * (dc / sc) * (dH / sh));
        }

        [Test]
        public void DeltaE2000MatchesPublishedReferencePairs()
        {
            Assert.AreEqual(2.0425f, DeltaE2000(new Vector3(50f, 2.6772f, -79.7751f), new Vector3(50f, 0f, -82.7485f)), .001f);
            Assert.AreEqual(1.0000f, DeltaE2000(new Vector3(50f, -1.3802f, -84.2814f), new Vector3(50f, 0f, -82.7485f)), .001f);
            Assert.AreEqual(2.3669f, DeltaE2000(new Vector3(50f, 0f, 0f), new Vector3(50f, -1f, 2f)), .001f);
            Assert.AreEqual(0.9082f, DeltaE2000(new Vector3(2.0776f, .0795f, -1.135f), new Vector3(.9033f, -.0636f, -.5514f)), .001f);
            Assert.AreEqual(0f, DeltaE2000(Color.gray, Color.gray), 1e-4f);
        }
    }
}
