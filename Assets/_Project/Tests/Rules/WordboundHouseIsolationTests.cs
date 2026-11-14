using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundHouseIsolationTests
    {
        static readonly string[] Trees = { "Online", "Rules/Wordbound", "Workshop" };
        static readonly string[] Forbidden = { "GameConfig", "HouseTuning", "items_house", ".Expand(", "BELT" };

        static bool IsTest(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals("Tests", StringComparison.Ordinal));

        [Test]
        public void WordboundRuntimeSourcesNeverReachHouseItemsOrTuning()
        {
            string scripts = Path.Combine(TestData.Root, "Assets", "_Project", "Scripts");
            var hits = new List<string>();
            foreach (string tree in Trees)
            {
                string folder = Path.Combine(scripts, tree.Replace('/', Path.DirectorySeparatorChar));
                var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
                    .Where(file => !IsTest(Path.GetRelativePath(scripts, file))).ToArray();
                Assert.IsNotEmpty(files, tree);
                foreach (string file in files)
                {
                    string text = File.ReadAllText(file);
                    foreach (string token in Forbidden.Where(token => text.Contains(token)))
                        hits.Add(Path.GetRelativePath(scripts, file) + ": " + token);
                }
            }
            Assert.IsEmpty(hits, "Wordbound and online code must read items.json and rules.json only:\n" + string.Join("\n", hits));
        }

        static IEnumerable<int> Arguments(string text, string call)
        {
            for (int at = text.IndexOf(call, StringComparison.Ordinal); at >= 0; at = text.IndexOf(call, at + call.Length, StringComparison.Ordinal))
            {
                int depth = 1, commas = 0, start = at + call.Length, index = start;
                for (; index < text.Length && depth > 0; index++)
                {
                    char c = text[index];
                    if (c == '(' || c == '[' || c == '{') depth++;
                    else if (c == ')' || c == ']' || c == '}') depth--;
                    else if (c == ',' && depth == 1) commas++;
                }
                yield return text.Substring(start, Math.Max(0, index - 1 - start)).Trim().Length == 0 ? 0 : commas + 1;
            }
        }

        [Test]
        public void WordboundDressingAlwaysPassesItsOwnWardrobe()
        {
            Assert.AreEqual(new[] { 0, 2, 3 }, Arguments("DefaultPresentationOutfit() PlayerAppearance.Dress(a, f(b, c)) PlayerAppearance.Dress(a, b, new[] { c, d })",
                "DefaultPresentationOutfit(").Concat(Arguments("DefaultPresentationOutfit() PlayerAppearance.Dress(a, f(b, c)) PlayerAppearance.Dress(a, b, new[] { c, d })",
                "PlayerAppearance.Dress(")).ToArray());
            string scripts = Path.Combine(TestData.Root, "Assets", "_Project", "Scripts");
            var hits = new List<string>();
            int calls = 0;
            foreach (string tree in Trees)
                foreach (string file in Directory.GetFiles(Path.Combine(scripts, tree.Replace('/', Path.DirectorySeparatorChar)), "*.cs", SearchOption.AllDirectories)
                    .Where(file => !IsTest(Path.GetRelativePath(scripts, file))))
                {
                    string text = File.ReadAllText(file), name = Path.GetRelativePath(scripts, file);
                    foreach (int count in Arguments(text, "DefaultPresentationOutfit("))
                    { calls++; if (count != 1) hits.Add(name + ": DefaultPresentationOutfit without the Wordbound wardrobe"); }
                    foreach (int count in Arguments(text, "PlayerAppearance.Dress("))
                    { calls++; if (count != 3) hits.Add(name + ": PlayerAppearance.Dress without the Wordbound wardrobe"); }
                }
            Assert.Greater(calls, 0, "The scan must see the Wordbound dressing calls.");
            Assert.IsEmpty(hits, "Wordbound must dress avatars from its own wardrobe, never GameConfig:\n" + string.Join("\n", hits));
        }
    }
}
