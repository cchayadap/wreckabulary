using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class OnlineWorkshopDescriptor
    {
        public string BundleId { get; }
        public OnlineContent Content { get; }
        public string Mode { get; }
        public string Format { get; }
        public string Seed { get; }
        public string ProtocolVersion { get; }
        public string GeometryHash { get; }
        public string ItemHash { get; }
        public string RulesHash { get; }
        public string WireHash { get; }
        public int RawLength { get; }
        public int TotalBytes { get; }
        public int ChunkCount { get; }
        public OnlineWorkshopDescriptor(JsonNode node)
        {
            OnlineJson.Fields(node, "bundleId", "content", "mode", "format", "seed", "protocolVersion", "geometryHash", "itemHash", "rulesHash", "wireHash", "rawLength", "totalBytes", "chunkCount");
            BundleId = OnlineJson.Text(node, "bundleId", OnlineJson.HashPattern);
            Content = OnlineContent.Read(node["content"]);
            Mode = OnlineJson.Text(node, "mode", "^(TimeRun|TeamRace|BrawlRace|FreeForAll|Freestyle)$");
            Format = OnlineJson.Text(node, "format", "^(Solo|Duo|Squad)$");
            Seed = OnlineJson.Text(node, "seed", "^[a-f0-9]{16}$");
            ProtocolVersion = node["protocolVersion"].String();
            GeometryHash = OnlineJson.Text(node, "geometryHash", OnlineJson.HashPattern);
            ItemHash = OnlineJson.Text(node, "itemHash", OnlineJson.HashPattern);
            RulesHash = OnlineJson.Text(node, "rulesHash", OnlineJson.HashPattern);
            WireHash = OnlineJson.Text(node, "wireHash", OnlineJson.HashPattern);
            RawLength = (int)OnlineJson.Integer(node, "rawLength", 1, OnlineSnapshotPacket.MaximumRawBytes);
            TotalBytes = (int)OnlineJson.Integer(node, "totalBytes", 41, OnlineSnapshotPacket.MaximumCompressedBytes);
            ChunkCount = (int)OnlineJson.Integer(node, "chunkCount", 1, OnlineSnapshotPacket.MaximumChunks);
            var value = ToValue(); value.Remove("bundleId");
            if (Content.Kind != "Workshop" || ProtocolVersion != MatchSnapshotCodec.ProtocolVersion ||
                ChunkCount != (TotalBytes + 4095) / 4096 || OnlineJson.Hash(value) != BundleId)
                throw new FormatException("The shared workshop descriptor is not valid for this release.");
        }
        public Dictionary<string, object> ToValue() => new Dictionary<string, object> {
            ["bundleId"] = BundleId, ["content"] = Content.ToValue(), ["mode"] = Mode, ["format"] = Format,
            ["seed"] = Seed, ["protocolVersion"] = ProtocolVersion, ["geometryHash"] = GeometryHash,
            ["itemHash"] = ItemHash, ["rulesHash"] = RulesHash, ["wireHash"] = WireHash,
            ["rawLength"] = RawLength, ["totalBytes"] = TotalBytes, ["chunkCount"] = ChunkCount };
        public void Bind(OnlineSettings settings)
        {
            if (settings.Mode != Mode || settings.Format != Format || settings.ProtocolVersion != ProtocolVersion)
                throw new FormatException("The shared workshop map uses different party settings.");
        }
    }

    public sealed class OnlineWorkshopBundle
    {
        public OnlineWorkshopDescriptor Descriptor { get; }
        public string DocumentJson { get; }
        public string ManifestJson { get; }
        public OnlineSnapshotPacket Packet { get; }
        OnlineWorkshopBundle(OnlineWorkshopDescriptor descriptor, string document, string manifest, OnlineSnapshotPacket packet)
        { Descriptor = descriptor; DocumentJson = document; ManifestJson = manifest; Packet = packet; }
        public static OnlineWorkshopBundle Create(CourseDocument document, ResolvedCourse course, CourseCatalogue geometry,
            ModeDefinition mode, OnlineContent content, string itemJson, string ruleJson)
        {
            if (document == null || course == null || geometry == null || mode == null || content == null) throw new ArgumentNullException();
            string source = CourseJson.ExportDocument(document), manifest = CourseJson.ExportManifest(course);
            if (content.Kind != "Workshop" || content.CourseHash != WordboundIdentity.Sha256(source) ||
                content.ManifestHash != course.Hash || content.ModeHash != mode.Hash || content.CourseId != document.Id ||
                CourseJson.ExportDocument(course.CopyDocument()) != source ||
                !CourseValidator.ValidateResolved(course, geometry, MatchStateValidation.Requirements(mode)).Ok)
                throw new ArgumentException("The exact authored document, resolved map and mode must match before sharing.");
            var root = Root(document, course, geometry.Hash, mode.Id, mode.Format.ToString(), course.Seed, content,
                WordboundIdentity.Sha256(itemJson), WordboundIdentity.Sha256(ruleJson));
            string json = CourseJson.Encode(root); CourseJson.Preflight(json, OnlineSnapshotPacket.MaximumRawBytes);
            OnlineSnapshotPacket packet;
            try { packet = OnlineSnapshotPacket.Compress(json); }
            catch (FormatException) { throw new ArgumentException("This map exceeds online sharing limits (8 MiB expanded or 256 KiB compressed). Reduce the draft before creating a party."); }
            var descriptor = new Dictionary<string, object> {
                ["content"] = content.ToValue(), ["mode"] = mode.Id, ["format"] = mode.Format.ToString(), ["seed"] = course.Seed,
                ["protocolVersion"] = MatchSnapshotCodec.ProtocolVersion, ["geometryHash"] = geometry.Hash,
                ["itemHash"] = WordboundIdentity.Sha256(itemJson), ["rulesHash"] = WordboundIdentity.Sha256(ruleJson),
                ["wireHash"] = packet.WireHash, ["rawLength"] = packet.RawLength, ["totalBytes"] = packet.Length, ["chunkCount"] = packet.ChunkCount };
            descriptor.Add("bundleId", OnlineJson.Hash(descriptor));
            return new OnlineWorkshopBundle(new OnlineWorkshopDescriptor(OnlineJson.Parse(OnlineJson.Encode(descriptor))), source, manifest, packet);
        }
        static Dictionary<string, object> Root(CourseDocument document, ResolvedCourse course, string geometry, string mode,
            string format, string seed, OnlineContent content, string items, string rules)
        {
            var doc = (Dictionary<string, object>)CourseJson.Value(document); doc.Add("format", CourseDocument.FormatId);
            return new Dictionary<string, object> {
                ["format"] = "wreckabulary.workshop-bundle", ["schema"] = 1, ["protocolVersion"] = MatchSnapshotCodec.ProtocolVersion,
                ["mode"] = mode, ["teamFormat"] = format, ["seed"] = seed, ["geometryHash"] = geometry,
                ["itemHash"] = items, ["rulesHash"] = rules, ["content"] = content.ToValue(), ["document"] = doc,
                ["manifest"] = CourseJson.Value(course.Data) };
        }
        public static OnlineWorkshopBundle Restore(OnlineWorkshopDescriptor descriptor, byte[][] chunks,
            CourseCatalogue geometry, string itemJson, string ruleJson)
        {
            if (descriptor.GeometryHash != geometry.Hash || descriptor.ItemHash != WordboundIdentity.Sha256(itemJson) ||
                descriptor.RulesHash != WordboundIdentity.Sha256(ruleJson))
                throw new FormatException("This workshop map requires a different installed content release.");
            string json = OnlineSnapshotPacket.Restore(chunks, descriptor.TotalBytes, descriptor.WireHash);
            if (new System.Text.UTF8Encoding(false, true).GetByteCount(json) != descriptor.RawLength)
                throw new FormatException("The shared map's expanded size differs from its descriptor.");
            CourseJson.Preflight(json, OnlineSnapshotPacket.MaximumRawBytes);
            var root = Json.Parse(json, "shared-workshop");
            CourseJson.Keys(root, new[] { "format", "schema", "protocolVersion", "mode", "teamFormat", "seed", "geometryHash", "itemHash", "rulesHash", "content", "document", "manifest" });
            var document = (CourseDocument)CourseJson.Read(root["document"], typeof(CourseDocument), true);
            var data = (CourseManifestData)CourseJson.Read(root["manifest"], typeof(CourseManifestData));
            var imported = CourseJson.ImportManifest(CourseJson.Encode(CourseJson.Value(data)), geometry);
            if (!imported.Success) throw new FormatException("The shared resolved map failed validation.");
            var course = imported.Manifest;
            var mode = ModeDefinition.For(descriptor.Mode, (RaceFormat)Enum.Parse(typeof(RaceFormat), descriptor.Format));
            if (root["format"].String() != "wreckabulary.workshop-bundle" || root["schema"].Number() != 1 ||
                json != CourseJson.Encode(Root(document, course, geometry.Hash, mode.Id, descriptor.Format, descriptor.Seed,
                    descriptor.Content, descriptor.ItemHash, descriptor.RulesHash)) ||
                !CourseValidator.ValidateResolved(course, geometry, MatchStateValidation.Requirements(mode)).Ok ||
                descriptor.Content.ModeHash != mode.Hash || descriptor.Content.CourseId != document.Id ||
                descriptor.Content.CourseHash != WordboundIdentity.Sha256(CourseJson.ExportDocument(document)) ||
                descriptor.Content.ManifestHash != course.Hash || course.Seed != descriptor.Seed ||
                CourseJson.ExportDocument(course.CopyDocument()) != CourseJson.ExportDocument(document))
                throw new FormatException("The shared map does not match its immutable descriptor.");
            return new OnlineWorkshopBundle(descriptor, CourseJson.ExportDocument(document), CourseJson.ExportManifest(course), null);
        }
    }

    public sealed class OnlineWorkshopService
    {
        readonly OnlineCoordinatorClient client;
        public OnlineWorkshopService(OnlineCoordinatorClient client) { this.client = client ?? throw new ArgumentNullException(nameof(client)); }
        static OnlineWorkshopDescriptor Info(JsonNode node)
        {
            if (!node.IsObject || !node.Keys.SequenceEqual(new[] { "descriptor" }))
                throw new FormatException("Unexpected or missing workshop content fields.");
            return node["descriptor"].IsNull ? null : new OnlineWorkshopDescriptor(node["descriptor"]);
        }
        public async Task<OnlineWorkshopDescriptor> PartyInfo(string partyId, CancellationToken ct = default) =>
            Info(await client.WorkshopPartyAction(partyId, "content-info", new Dictionary<string, object>(), ct).ConfigureAwait(false));
        public async Task<JsonNode> Upload(string partyId, string revision, OnlineWorkshopBundle bundle, CancellationToken ct = default)
        {
            if (bundle.Packet == null) throw new ArgumentException("Only a locally validated draft can be uploaded.");
            var current = await PartyInfo(partyId, ct).ConfigureAwait(false);
            if (current?.BundleId == bundle.Descriptor.BundleId) return await client.Party(partyId, ct).ConfigureAwait(false);
            var response = await client.WorkshopPartyAction(partyId, "content-begin", new Dictionary<string, object> {
                ["expectedRevision"] = revision, ["descriptor"] = bundle.Descriptor.ToValue() }, ct).ConfigureAwait(false);
            OnlineJson.Fields(response, "descriptor", "committed");
            if (new OnlineWorkshopDescriptor(response["descriptor"]).BundleId != bundle.Descriptor.BundleId)
                throw new FormatException("Upload descriptor changed.");
            if (!response["committed"].Bool())
                await Batches(bundle.Descriptor.ChunkCount, async i => {
                    var part = await client.WorkshopPartyAction(partyId, "content-chunk", new Dictionary<string, object> {
                        ["expectedRevision"] = revision, ["bundleId"] = bundle.Descriptor.BundleId, ["index"] = i,
                        ["data"] = OnlineJson.Base64Url(bundle.Packet.Chunk(i)) }, ct).ConfigureAwait(false);
                    OnlineJson.Fields(part, "bundleId", "index");
                    if (part["bundleId"].String() != bundle.Descriptor.BundleId || part["index"].Number() != i)
                        throw new FormatException("Upload chunk acknowledgement changed.");
                }).ConfigureAwait(false);
            return await client.WorkshopPartyAction(partyId, "content-finalize", new Dictionary<string, object> {
                ["expectedRevision"] = revision, ["bundleId"] = bundle.Descriptor.BundleId }, ct).ConfigureAwait(false);
        }
        static async Task Batches(int count, Func<int, Task> action)
        { for (int start = 0; start < count; start += 4) await Task.WhenAll(Enumerable.Range(start, Math.Min(4, count - start)).Select(action)).ConfigureAwait(false); }
        public Task<OnlineWorkshopBundle> DownloadParty(string partyId, OnlineWorkshopDescriptor descriptor, CourseCatalogue geometry,
            string items, string rules, CancellationToken ct = default) => Download(descriptor, geometry, items, rules,
                data => client.WorkshopPartyAction(partyId, "content-read", data, ct));
        public Task<OnlineWorkshopBundle> DownloadMatch(OnlineSettings settings, string matchId, OnlineWorkshopDescriptor descriptor,
            CourseCatalogue geometry, string items, string rules, CancellationToken ct = default) => Download(descriptor, geometry, items, rules,
                data => client.WorkshopMatchAction(settings, matchId, "content-read", data, ct));
        static async Task<OnlineWorkshopBundle> Download(OnlineWorkshopDescriptor d, CourseCatalogue geometry, string items, string rules,
            Func<Dictionary<string, object>, Task<JsonNode>> read)
        {
            var chunks = new byte[d.ChunkCount][];
            await Batches(chunks.Length, async i => {
                var part = await read(new Dictionary<string, object> { ["bundleId"] = d.BundleId, ["index"] = i }).ConfigureAwait(false);
                OnlineJson.Fields(part, "bundleId", "index", "data");
                if (part["bundleId"].String() != d.BundleId || part["index"].Number() != i) throw new FormatException("Downloaded workshop chunk changed.");
                chunks[i] = OnlineJson.DecodeBase64Url(part["data"].String(), OnlineSnapshotPacket.ChunkBytes);
            }).ConfigureAwait(false);
            return OnlineWorkshopBundle.Restore(d, chunks, geometry, items, rules);
        }
    }
}
