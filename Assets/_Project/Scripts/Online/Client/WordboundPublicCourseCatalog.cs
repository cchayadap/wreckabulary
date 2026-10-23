using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundPublicCourse
    {
        public string Mode { get; }
        public string Format { get; }
        public string Seed { get; }
        public OnlineContent Content { get; }
        internal string Key => Mode + "/" + Format + "/" + Seed;

        internal WordboundPublicCourse(string mode, string format, string seed, OnlineContent content)
        {
            _ = new OnlineSettings(mode, format, false, "asia", MatchSnapshotCodec.ProtocolVersion, "validation");
            var definition = ModeDefinition.For(mode, (RaceFormat)Enum.Parse(typeof(RaceFormat), format));
            if (!definition.PublicQueue || content == null || content.Kind != "BuiltIn" || content.ModeHash != definition.Hash)
                throw new FormatException("Public course metadata does not match its public mode binding.");
            if (seed == null || seed.Length != 16 || seed.Any(c => !"0123456789abcdef".Contains(c)))
                throw new FormatException("A public course requires an exact lowercase hexadecimal seed.");
            Mode = mode; Format = format; Seed = seed; Content = content;
        }

        internal Dictionary<string, object> ToValue() => new Dictionary<string, object> {
            ["mode"] = Mode, ["format"] = Format, ["seed"] = Seed, ["content"] = Content.ToValue()
        };
    }

    public sealed class WordboundPublicCourseCatalog
    {
        public const int Schema = 1;
        public const int MaximumCourses = 128;
        public const int PublicVariantsPerSeed = 9;
        public string ProtocolVersion => MatchSnapshotCodec.ProtocolVersion;
        public string MapPoolVersion { get; }
        public IReadOnlyList<WordboundPublicCourse> Courses { get; }
        public bool IsClosed => Courses.Count == 0;
        public string Json => IsClosed && MapPoolVersion == null ? "[]" : OnlineJson.Encode(ToValue());
        public string BuiltinContentJson => OnlineJson.Encode(Courses.Select(c => c.Content.ToValue()).ToArray());

        WordboundPublicCourseCatalog(string poolVersion, IEnumerable<WordboundPublicCourse> courses)
        {
            var ordered = courses.OrderBy(c => c.Mode, StringComparer.Ordinal).ThenBy(c => c.Format, StringComparer.Ordinal)
                .ThenBy(c => c.Seed, StringComparer.Ordinal).ToArray();
            if (ordered.Length > MaximumCourses || ordered.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() != ordered.Length ||
                ordered.Select(c => OnlineJson.Hash(c.Content.ToValue())).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
                throw new FormatException("Public courses must have unique selections and bindings within the catalogue limit.");
            if (poolVersion != null)
                _ = new OnlineSettings("TimeRun", "Solo", false, "asia", ProtocolVersion, poolVersion);
            else if (ordered.Length != 0) throw new FormatException("A public catalogue needs a release version.");
            MapPoolVersion = poolVersion;
            Courses = new ReadOnlyCollection<WordboundPublicCourse>(ordered);
            _ = OnlineJson.Parse(Json);
        }

        Dictionary<string, object> ToValue() => new Dictionary<string, object> {
            ["schema"] = Schema, ["protocolVersion"] = ProtocolVersion,
            ["mapPoolVersion"] = MapPoolVersion, ["courses"] = Courses.Select(c => c.ToValue()).ToArray()
        };

        public static WordboundPublicCourseCatalog Parse(string json)
        {
            var root = OnlineJson.Parse(json);
            if (root.IsArray && root.Items.Count == 0) return new WordboundPublicCourseCatalog(null, Array.Empty<WordboundPublicCourse>());
            WordboundPartyJson.Fields(root, "schema", "protocolVersion", "mapPoolVersion", "courses");
            if (WordboundPartyJson.Integer(root, "schema", Schema, Schema) != Schema ||
                root["protocolVersion"].String() != MatchSnapshotCodec.ProtocolVersion)
                throw new FormatException("Unsupported public catalogue protocol.");
            string version = WordboundPartyJson.Text(root, "mapPoolVersion", "^[A-Za-z0-9_-]{1,24}$");
            var courses = root["courses"];
            if (!courses.IsArray || courses.Items.Count > MaximumCourses) throw new FormatException("Invalid public course list.");
            return new WordboundPublicCourseCatalog(version, courses.Items.Select(entry => {
                WordboundPartyJson.Fields(entry, "mode", "format", "seed", "content");
                return new WordboundPublicCourse(entry["mode"].String(), entry["format"].String(), entry["seed"].String(),
                    OnlineContent.Read(entry["content"]));
            }));
        }

        public static WordboundPublicCourseCatalog FromTestedContent(string mapPoolVersion,
            IEnumerable<WordboundClientContent> testedCourses)
        {
            if (testedCourses == null) throw new ArgumentNullException(nameof(testedCourses));
            return new WordboundPublicCourseCatalog(mapPoolVersion, testedCourses.Select(content => {
                if (content == null) throw new ArgumentException("A tested public course is missing.");
                return new WordboundPublicCourse(content.Mode.Id, content.Mode.Format.ToString(), content.Seed, content.PublicBinding);
            }));
        }

        public static WordboundPublicCourseCatalog ResolveRelease(string itemsJson, string rulesJson,
            string mapPoolVersion, IEnumerable<string> approvedSeeds)
        {
            if (approvedSeeds == null) throw new ArgumentNullException(nameof(approvedSeeds));
            var seeds = approvedSeeds.ToArray();
            if (seeds.Length == 0 || seeds.Length > MaximumCourses / PublicVariantsPerSeed ||
                seeds.Distinct(StringComparer.Ordinal).Count() != seeds.Length)
                throw new ArgumentException("Choose one to fourteen unique approved public seeds within the JSON size limit.");
            var variants = new[] {
                ("TimeRun", "Solo"), ("TeamRace", "Solo"), ("TeamRace", "Duo"), ("TeamRace", "Squad"),
                ("BrawlRace", "Solo"), ("BrawlRace", "Duo"), ("BrawlRace", "Squad"), ("FreeForAll", "Solo"), ("Freestyle", "Solo")
            };
            return FromTestedContent(mapPoolVersion, seeds.OrderBy(s => s, StringComparer.Ordinal).SelectMany(seed =>
                variants.Select(variant => new WordboundClientContent(itemsJson, rulesJson, variant.Item1, variant.Item2, seed))));
        }

        public void RequireRelease(string poolVersion)
        {
            if (MapPoolVersion != null && MapPoolVersion != poolVersion)
                throw new FormatException("The public catalogue belongs to a different map release.");
        }

        public IReadOnlyList<WordboundPublicCourse> Choices(string mode, string format) =>
            Array.AsReadOnly(Courses.Where(c => c.Mode == mode && c.Format == format).ToArray());

        public WordboundPublicCourse Find(string mode, string format, string seed) =>
            Courses.SingleOrDefault(c => c.Mode == mode && c.Format == format && c.Seed == seed);

        public WordboundClientContent ResolveSelection(string itemsJson, string rulesJson, string mode, string format, string seed)
        {
            var selected = Find(mode, format, seed) ?? throw new InvalidOperationException("Choose an approved map for this mode and format.");
            var resolved = new WordboundClientContent(itemsJson, rulesJson, mode, format, seed);
            if (!resolved.Matches(selected.Content))
                throw new InvalidOperationException("This approved map differs from the installed content. Use a matching game release.");
            return resolved;
        }
    }
}
