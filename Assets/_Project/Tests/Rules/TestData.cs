using System;
using System.IO;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public static class TestData
    {
        const string ConfigDir = "Assets/_Project/Data/Config";
        static string root;

        public static string Root
        {
            get
            {
                if (root != null) return root;
                foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
                {
                    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                        if (Directory.Exists(Path.Combine(dir.FullName, ConfigDir))) return root = dir.FullName;
                }
                throw new DirectoryNotFoundException($"Can't find {ConfigDir} above {Environment.CurrentDirectory}.");
            }
        }

        public static string Read(string file) => File.ReadAllText(Path.Combine(Root, ConfigDir, file));

        public static ItemCatalogue Catalogue() => ItemCatalogue.FromJson(Read("items.json"), "items.json");

        public static RuleBook LoadRuleBook() => RuleBook.FromJson(Read("rules.json"), "rules.json");

        public static GameRules RulesFor(string mode) => LoadRuleBook().For(mode);

        public static HouseLayout House() => HouseLayout.FromJson(Read("house_pinwheel.json"), "house_pinwheel.json");

        public static WardrobeCatalogue Wardrobe() => WardrobeCatalogue.FromJson(Read("wardrobe.json"), "wardrobe.json");

        public static Economy NewEconomy(string mode = "Dibs", int players = 2, int[] teams = null)
        {
            var e = new Economy(Catalogue(), RulesFor(mode));
            for (int i = 0; i < players; i++) e.AddPlayer(i, teams != null ? teams[i] : i);
            return e;
        }

        public static void GiveLetters(Economy e, int player, string letters)
        {
            foreach (var tile in e.MintTiles(letters).Tiles)
            {
                var r = e.CollectTile(player, tile.TileId);
                if (!r.Ok) throw new InvalidOperationException($"Player {player} couldn't collect {tile.Letter}: {r.Refusal}");
            }
        }

        public static ItemInstance Craft(Economy e, int player, string word, double now = 0)
        {
            GiveLetters(e, player, word);
            var begun = e.BeginCraft(player, word, now);
            if (!begun.Ok) throw new InvalidOperationException($"Player {player} couldn't start {word}: {begun.Refusal}");
            var done = e.CompleteCraft(player, now + 100);
            if (!done.Ok) throw new InvalidOperationException($"Player {player} couldn't finish {word}: {done.Refusal}");
            return done.Item;
        }

        public static void AssertConserved(Economy e, string context = "")
        {
            var audit = e.Audit();
            Assert.IsTrue(audit.Balanced, $"{context} letters don't balance: {audit}");
            Assert.IsEmpty(e.CheckOwnership(), $"{context} ownership problems");
        }
    }
}
