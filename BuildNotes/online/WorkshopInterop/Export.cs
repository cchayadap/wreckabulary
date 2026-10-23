using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

static class Export
{
    static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Provide integration root and generated fixture destination.");
        string config = Path.Combine(args[0], "Assets/_Project/Data/Config");
        string items = File.ReadAllText(Path.Combine(config, "items.json"));
        string rules = File.ReadAllText(Path.Combine(config, "rules.json"));
        var flagship = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items));
        var initial = flagship.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("Freestyle")));
        if (!initial.Success) throw new InvalidOperationException("Actual flagship source failed to resolve.");
        var document = initial.Manifest.CopyDocument(); document.Id = "authored-interop-course";
        var exact = CourseResolver.Resolve(document, flagship.Geometry,
            new CourseResolveRequest("fedcba9876543210", MatchStateValidation.Requirements(ModeDefinition.For("Freestyle"))));
        if (!exact.Success) throw new InvalidOperationException("Edited arbitrary workshop draft failed to resolve.");
        var content = WordboundClientContent.FromAuthored(CourseJson.ExportDocument(document), CourseJson.ExportManifest(exact.Manifest),
            flagship.Geometry, items, rules, "Freestyle", "Solo", exact.Manifest.Seed);
        var bundle = content.WorkshopBundle;
        var chunks = Enumerable.Range(0, bundle.Packet.ChunkCount).Select(bundle.Packet.Chunk).ToArray();
        var restored = OnlineWorkshopBundle.Restore(bundle.Descriptor, chunks, flagship.Geometry, items, rules);
        var remote = WordboundClientContent.FromDownloaded(restored, flagship.Geometry, items, rules);
        if (remote.Course.Hash != content.Course.Hash || remote.Course.SourceHash != content.Course.SourceHash ||
            remote.Seed != content.Seed || !remote.Matches(content.PrivateBinding) ||
            restored.DocumentJson != bundle.DocumentJson || restored.ManifestJson != bundle.ManifestJson ||
            new WordboundPublicCourseRegistry().Allows(remote))
            throw new InvalidOperationException("Actual client bundle roundtrip changed its authored identity.");
        var value = new Dictionary<string, object> { ["descriptor"] = bundle.Descriptor.ToValue(),
            ["chunks"] = chunks.Select(OnlineJson.Base64Url).ToArray(),
            ["documentSha256"] = WordboundIdentity.Sha256(bundle.DocumentJson),
            ["manifestSha256"] = WordboundIdentity.Sha256(bundle.ManifestJson) };
        File.WriteAllText(args[1], "export const workshopFixture = " + OnlineJson.Encode(value) + " as const;\n");
        Console.WriteLine("REAL_AUTHORED_CSHARP_BUNDLE_PASS raw=" + bundle.Packet.RawLength + " packet=" + bundle.Packet.Length +
            " chunks=" + bundle.Packet.ChunkCount + " source=" + content.Course.SourceHash + " resolved=" + content.Course.Hash);
        return 0;
    }
}
