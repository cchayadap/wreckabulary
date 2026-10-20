using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundPublicCourseCatalogTests
    {
        const string Seed = "0123456789abcdef";
        const string OtherSeed = "fedcba9876543210";
        const string Release = "public-test-v2";
        string items, rules;
        WordboundPublicCourseCatalog catalogue;

        [OneTimeSetUp]
        public void LoadRealContent()
        {
            string root = Environment.GetEnvironmentVariable("WORDBOUND_PROJECT_ROOT") ?? Directory.GetCurrentDirectory();
            items = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/items.json"));
            rules = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/rules.json"));
            catalogue = WordboundPublicCourseCatalog.ResolveRelease(items, rules, Release, new[] { Seed });
        }

        [Test]
        public void ClosedLegacyArrayRemainsClosedWithoutInventingARelease()
        {
            var closed = WordboundPublicCourseCatalog.Parse("[]");
            Assert.IsTrue(closed.IsClosed);
            Assert.IsNull(closed.MapPoolVersion);
            Assert.AreEqual("[]", closed.Json);
            Assert.AreEqual("[]", closed.BuiltinContentJson);
            Assert.DoesNotThrow(() => closed.RequireRelease(Release));
            Assert.AreEqual(0, new WordboundPublicCourseRegistry(closed.Json).Count);
            Assert.IsEmpty(closed.Choices("TimeRun", "Solo"));
        }

        [Test]
        public void RealReleaseContainsExactlyEightPublicVariantsAndNoFreestyle()
        {
            CollectionAssert.AreEquivalent(new[] { "TimeRun/Solo", "TeamRace/Solo", "TeamRace/Duo", "TeamRace/Squad",
                "BrawlRace/Solo", "BrawlRace/Duo", "BrawlRace/Squad", "FreeForAll/Solo" },
                catalogue.Courses.Select(c => c.Mode + "/" + c.Format));
            Assert.AreEqual(8, catalogue.Courses.Count);
            Assert.IsTrue(catalogue.Courses.All(c => c.Seed == Seed && c.Content.Kind == "BuiltIn"));
            Assert.IsNull(catalogue.Find("Freestyle", "Solo", Seed));
            Assert.IsNull(catalogue.Find("TimeRun", "Duo", Seed));
        }

        [Test]
        public void RoundTripAndWorkerProjectionPreserveEveryExactBinding()
        {
            var parsed = WordboundPublicCourseCatalog.Parse(catalogue.Json);
            Assert.AreEqual(catalogue.Json, parsed.Json);
            var projection = OnlineJson.Parse(parsed.BuiltinContentJson);
            Assert.IsTrue(projection.IsArray);
            Assert.AreEqual(8, projection.Items.Count);
            CollectionAssert.AreEquivalent(parsed.Courses.Select(c => OnlineJson.Hash(c.Content.ToValue())),
                projection.Items.Select(row => OnlineJson.Hash(OnlineContent.Read(row).ToValue())));
            Assert.AreEqual(8, new WordboundPublicCourseRegistry(catalogue.Json).Count);
            Assert.AreEqual(8, new WordboundPublicCourseRegistry(parsed.BuiltinContentJson).Count);
        }

        [Test]
        public void InputOrderingDoesNotChangePublishedBytes()
        {
            var reversed = Record(catalogue.Courses.Reverse().Select(Row).ToArray());
            Assert.AreEqual(catalogue.Json, WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(reversed)).Json);
        }

        [Test]
        public void SelectionResolvesActualContentAndRegistryUsesExactSeed()
        {
            var content = catalogue.ResolveSelection(items, rules, "TimeRun", "Solo", Seed);
            Assert.IsTrue(new WordboundPublicCourseRegistry(catalogue.Json).Allows(content));
            Assert.AreEqual(Seed, content.Seed);
            Assert.Throws<InvalidOperationException>(() => catalogue.ResolveSelection(items, rules, "TimeRun", "Solo", OtherSeed));
            Assert.IsNull(catalogue.Find("TimeRun", "Solo", OtherSeed));
        }

        [Test]
        public void PlausibleSeedMetadataCannotSubstituteForActualResolvedManifest()
        {
            var row = Row(catalogue.Find("TimeRun", "Solo", Seed));
            row["seed"] = OtherSeed;
            var altered = WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(Record(row)));
            Assert.Throws<InvalidOperationException>(() =>
                altered.ResolveSelection(items, rules, "TimeRun", "Solo", OtherSeed));
        }

        [Test]
        public void AlteredCatalogueHashIsRejectedBeforePublicSelectionCanLaunch()
        {
            var row = Row(catalogue.Find("TimeRun", "Solo", Seed));
            ((Dictionary<string, object>)row["content"])["catalogHash"] = new string('a', 64);
            var altered = WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(Record(row)));
            Assert.Throws<InvalidOperationException>(() => altered.ResolveSelection(items, rules, "TimeRun", "Solo", Seed));
        }

        [TestCase("mode", "TimeRun")]
        [TestCase("format", "Solo")]
        [TestCase("mode", "Freestyle")]
        [TestCase("format", "Duo\n")]
        [TestCase("seed", "0123456789ABCDEF")]
        [TestCase("seed", "0123456789abcdef\n")]
        public void MetadataMustMatchTheBoundPublicModeAndExactSeed(string field, string value)
        {
            var row = Row(catalogue.Find("TeamRace", "Squad", Seed)); row[field] = value;
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(Record(row))));
        }

        [Test]
        public void DuplicateSelectionsAndDuplicateBindingsAreBothRejected()
        {
            var a = Row(catalogue.Courses[0]); var b = Row(catalogue.Courses[0]);
            ((Dictionary<string, object>)b["content"])["catalogHash"] = new string('a', 64);
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(Record(a, b))));
            b = Row(catalogue.Courses[0]); b["seed"] = OtherSeed;
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(Record(a, b))));
        }

        [Test]
        public void PrivateBindingsAndUnversionedNonemptyCataloguesAreRejected()
        {
            var row = Row(catalogue.Courses[0]);
            ((Dictionary<string, object>)row["content"])["kind"] = "Workshop";
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(Record(row))));
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(catalogue.BuiltinContentJson));
        }

        [Test]
        public void UnknownFieldsDuplicateKeysAndProtocolOrReleaseMismatchFailClosed()
        {
            var record = Record(Row(catalogue.Courses[0])); record["unexpected"] = true;
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(record)));
            var row = Row(catalogue.Courses[0]); row["url"] = "https://invalid.example";
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(Record(row))));
            record = Record(Row(catalogue.Courses[0])); record["protocolVersion"] = "wordbound-v1";
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(record)));
            record["protocolVersion"] = MatchSnapshotCodec.ProtocolVersion; record["schema"] = 2;
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(OnlineJson.Encode(record)));
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(catalogue.Json.Insert(1, "\"schema\":1,")));
            Assert.Throws<FormatException>(() => catalogue.RequireRelease("another-release"));
            Assert.DoesNotThrow(() => catalogue.RequireRelease(Release));
        }

        [Test]
        public void CourseCountJsonSizeAndExportSeedListAreBounded()
        {
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(
                OnlineJson.Encode(Record(Enumerable.Range(0, 129).Select(_ => new Dictionary<string, object>()).ToArray()))));
            Assert.Throws<FormatException>(() => WordboundPublicCourseCatalog.Parse(new string(' ', OnlineJson.MaximumBytes) + "[]"));
            Assert.Throws<ArgumentException>(() => WordboundPublicCourseCatalog.ResolveRelease(items, rules, Release, Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => WordboundPublicCourseCatalog.ResolveRelease(items, rules, Release, new[] { Seed, Seed }));
        }

        Dictionary<string, object> Record(params Dictionary<string, object>[] courses) => new Dictionary<string, object> {
            ["schema"] = 1, ["protocolVersion"] = MatchSnapshotCodec.ProtocolVersion,
            ["mapPoolVersion"] = Release, ["courses"] = courses
        };
        static Dictionary<string, object> Row(WordboundPublicCourse course) => new Dictionary<string, object> {
            ["mode"] = course.Mode, ["format"] = course.Format, ["seed"] = course.Seed,
            ["content"] = course.Content.ToValue()
        };
    }
}
