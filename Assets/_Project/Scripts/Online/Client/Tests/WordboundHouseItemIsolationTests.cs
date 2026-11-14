using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundHouseItemIsolationTests
    {
        const string Seed = "0123456789abcdef";
        string items, rules, house;
        CourseDocument draft;
        ResolvedCourse resolved;
        CourseCatalogue geometry;

        [OneTimeSetUp]
        public void Content()
        {
            string root = Environment.GetEnvironmentVariable("WORDBOUND_PROJECT_ROOT") ?? Directory.GetCurrentDirectory();
            items = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/items.json"));
            rules = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/rules.json"));
            house = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/items_house.json"));
            var builtin = new WordboundClientContent(items, rules, "Freestyle", "Solo", Seed);
            geometry = builtin.Geometry;
            draft = builtin.Course.CopyDocument(); draft.Id = "house-isolation-fixture"; draft.Title = "House isolation fixture";
            var result = CourseResolver.Resolve(draft, geometry,
                new CourseResolveRequest("fedcba9876543210", MatchStateValidation.Requirements(builtin.Mode)));
            Assert.IsTrue(result.Success, string.Join("; ", result.Issues));
            resolved = result.Manifest;
        }

        WordboundClientContent Authored(string itemJson) => WordboundClientContent.FromAuthored(CourseJson.ExportDocument(draft),
            CourseJson.ExportManifest(resolved), geometry, itemJson, rules, "Freestyle", "Solo", resolved.Seed);

        [Test]
        public void ExpansionFixtureActuallyContainsHouseOnlyRecipes()
        {
            var expanded = ItemCatalogue.FromJson(Expand(true));
            Assert.IsFalse(ItemCatalogue.FromJson(items).TryGet("TOASTER", out _));
            Assert.IsTrue(expanded.TryGet("TOASTER", out var toaster) && toaster.Enabled);
            Assert.Greater(expanded.All.Count, ItemCatalogue.FromJson(items).All.Count);
            StringAssert.Contains("\"tuning\"", Expand(true));
            StringAssert.DoesNotContain("\"tuning\"", Expand(false));
        }

        [TestCase("Freestyle", "Solo"), TestCase("TimeRun", "Solo"), TestCase("FreeForAll", "Solo"), TestCase("TeamRace", "Squad")]
        public void HouseFileAndTunedExpansionAreRejectedAsWordboundContent(string mode, string format)
        {
            Assert.Throws<ArgumentException>(() => new WordboundClientContent(house, rules, mode, format, Seed));
            Assert.Throws<ArgumentException>(() => new WordboundClientContent(Expand(true), rules, mode, format, Seed));
            Assert.DoesNotThrow(() => new WordboundClientContent(items, rules, mode, format, Seed));
        }

        [Test]
        public void AuthoredContentRejectsTunedHouseItems()
        {
            Assert.Throws<ArgumentException>(() => Authored(Expand(true)));
            Assert.Throws<ArgumentException>(() => Authored(house));
            Assert.Throws<ArgumentException>(() => WordboundItemSource.Require(house));
            Assert.DoesNotThrow(() => WordboundItemSource.Require(items));
        }

        [TestCase(true), TestCase(false)]
        public void HouseExpandedItemsCannotCreateAnAuthoredParty(bool tuned)
        {
            using var rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            var key = rsa.ExportParameters(false);
            var verifier = new OnlineAdmissionVerifier("test", OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["keys"] = new[] { new Dictionary<string, object> { ["kty"] = "RSA", ["kid"] = "test", ["use"] = "sig", ["alg"] = "RS256",
                    ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } } })));
            var http = new Http();
            using var coordinator = new WordboundPartyCoordinator(new OnlineCoordinatorClient(http), verifier, items, rules, "house-tests", clock: () => 1000);
            var settings = coordinator.Settings("Freestyle", "Solo", false, "asia");
            string expanded = Expand(tuned);
            Assert.That(() => coordinator.CreateAuthoredParty(settings, Authored(expanded)),
                Throws.InstanceOf<ArgumentException>().Or.InstanceOf<InvalidOperationException>());
            Assert.IsNull(coordinator.Party);
            Assert.IsEmpty(http.Requests);
        }

        string Expand(bool tuning)
        {
            var known = ItemCatalogue.FromJson(items);
            int houseItems = house.IndexOf('[', house.IndexOf("\"items\"", StringComparison.Ordinal));
            var additions = Objects(Slice(house, houseItems))
                .Where(entry => !known.TryGet(Json.Parse(entry, "items_house.json")["id"].String(), out _)).ToArray();
            Assert.IsNotEmpty(additions);
            int close = items.LastIndexOf(']');
            string merged = items.Substring(0, close).TrimEnd() + ",\n" + string.Join(",\n", additions) + "\n" + items.Substring(close);
            if (!tuning) return merged;
            int tuningStart = house.IndexOf('{', house.IndexOf("\"tuning\"", StringComparison.Ordinal));
            int end = merged.LastIndexOf('}');
            return merged.Substring(0, end).TrimEnd() + ",\n  \"tuning\": " + Slice(house, tuningStart) + "\n}\n";
        }

        static string Slice(string text, int open)
        {
            int depth = 0; bool quoted = false, escaped = false;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
                if (c == '"') quoted = true;
                else if (c == '[' || c == '{') depth++;
                else if ((c == ']' || c == '}') && --depth == 0) return text.Substring(open, i - open + 1);
            }
            throw new FormatException("Unbalanced JSON fixture.");
        }

        static IEnumerable<string> Objects(string array)
        {
            for (int i = 1; i < array.Length - 1; i++)
                if (array[i] == '{') { string entry = Slice(array, i); yield return entry; i += entry.Length - 1; }
        }

        sealed class Http : IOnlineHttpTransport
        {
            public readonly List<OnlineRequest> Requests = new List<OnlineRequest>();
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct)
            { Requests.Add(request); return Task.FromResult(new OnlineResponse(500, "{\"error\":{\"code\":\"UNEXPECTED\"}}")); }
        }
    }
}
