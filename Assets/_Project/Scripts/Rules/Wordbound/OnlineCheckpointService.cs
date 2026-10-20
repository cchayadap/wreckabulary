using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Wreckabulary.Rules.Wordbound
{
    static class OnlineCheckpointJson
    {
        internal static void Fields(JsonNode node, params string[] fields)
        {
            if (!node.IsObject || node.Keys.Count() != fields.Length || fields.Any(f => !node.Keys.Contains(f)))
                throw new FormatException("Unexpected checkpoint fields.");
        }
        internal static string OptionalId(JsonNode node) => node.IsNull ? null : OnlineJson.Text(
            OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> { ["id"] = node.String() })), "id", OnlineJson.HashPattern);
        internal static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
    public sealed class OnlineCheckpointWriter
    {
        public string PlayerId { get; }
        public string ClaimId { get; }
        public string Term { get; }
        public long Epoch { get; }
        public long ExpiresAtMilliseconds { get; }
        public string BaseHeadId { get; }
        public bool AdmissionOpen { get; }
        internal OnlineCheckpointWriter(JsonNode node)
        {
            OnlineCheckpointJson.Fields(node, "playerId", "claimId", "term", "epoch", "expiresAt", "baseHeadId", "admissionOpen");
            PlayerId = OnlineJson.Text(node, "playerId", OnlineJson.Uuid);
            ClaimId = OnlineJson.Text(node, "claimId", OnlineJson.Uuid);
            Term = OnlineJson.Text(node, "term", OnlineJson.Counter);
            if (OnlineSnapshotMark.Counter(node, "term") < 1) throw new FormatException("Writer term must be positive.");
            Epoch = OnlineSnapshotMark.Counter(node, "epoch");
            ExpiresAtMilliseconds = OnlineJson.Integer(node, "expiresAt");
            BaseHeadId = OnlineCheckpointJson.OptionalId(node["baseHeadId"]);
            AdmissionOpen = node["admissionOpen"].Bool();
        }
    }
    public sealed class OnlineCheckpointManifest
    {
        public string UploadId { get; }
        public string Term { get; }
        public string PreviousHeadId { get; }
        public OnlineSnapshotMark Mark { get; }
        public string WireHash { get; }
        public int RawLength { get; }
        public int TotalBytes { get; }
        public int ChunkCount { get; }
        internal OnlineCheckpointManifest(JsonNode node)
        {
            OnlineCheckpointJson.Fields(node, "uploadId", "term", "previousHeadId", "stamp", "wireHash", "rawLength", "totalBytes", "chunkCount");
            UploadId = OnlineJson.Text(node, "uploadId", OnlineJson.HashPattern);
            Term = OnlineJson.Text(node, "term", OnlineJson.Counter);
            if (OnlineSnapshotMark.Counter(node, "term") < 1) throw new FormatException("Writer term must be positive.");
            PreviousHeadId = OnlineCheckpointJson.OptionalId(node["previousHeadId"]);
            var stamp = node["stamp"];
            OnlineJson.Fields(stamp, "matchId", "identityHash", "epoch", "sequence", "tick", "stateHash");
            Mark = new OnlineSnapshotMark(stamp["matchId"].String(), stamp["identityHash"].String(),
                OnlineSnapshotMark.Counter(stamp, "epoch"), OnlineSnapshotMark.Counter(stamp, "sequence"),
                OnlineSnapshotMark.Counter(stamp, "tick"), stamp["stateHash"].String());
            WireHash = OnlineJson.Text(node, "wireHash", OnlineJson.HashPattern);
            RawLength = (int)OnlineJson.Integer(node, "rawLength", 1, OnlineSnapshotPacket.MaximumRawBytes);
            TotalBytes = (int)OnlineJson.Integer(node, "totalBytes", 41, OnlineSnapshotPacket.MaximumCompressedBytes);
            ChunkCount = (int)OnlineJson.Integer(node, "chunkCount", 1, OnlineSnapshotPacket.MaximumChunks);
            if (ChunkCount != (TotalBytes + OnlineSnapshotPacket.ChunkBytes - 1) / OnlineSnapshotPacket.ChunkBytes)
                throw new FormatException("Checkpoint chunk count mismatch.");
            var unsigned = ToValue(); unsigned.Remove("uploadId");
            if (UploadId != OnlineJson.Hash(unsigned)) throw new FormatException("Checkpoint identifier does not commit its manifest.");
        }
        internal OnlineCheckpointManifest(OnlineCheckpointWriter writer, OnlineCheckpointManifest previous,
            OnlineSnapshotMark mark, OnlineSnapshotPacket packet)
            : this(CreateValue(writer, previous, mark, packet)) { }
        static JsonNode CreateValue(OnlineCheckpointWriter writer, OnlineCheckpointManifest previous, OnlineSnapshotMark mark, OnlineSnapshotPacket packet)
        {
            var value = Value(null, writer.Term, previous?.UploadId, mark, packet.WireHash, packet.RawLength, packet.Length, packet.ChunkCount);
            value.Remove("uploadId"); string id = OnlineJson.Hash(value); value.Add("uploadId", id);
            return OnlineJson.Parse(OnlineJson.Encode(value));
        }
        static Dictionary<string, object> Value(string id, string term, string previous, OnlineSnapshotMark mark, string hash, int raw, int bytes, int count) =>
            new Dictionary<string, object> {
                ["uploadId"] = id, ["term"] = term, ["previousHeadId"] = previous, ["wireHash"] = hash,
                ["rawLength"] = raw, ["totalBytes"] = bytes, ["chunkCount"] = count,
                ["stamp"] = new Dictionary<string, object> {
                    ["matchId"] = mark.MatchId, ["identityHash"] = mark.IdentityHash, ["stateHash"] = mark.StateHash,
                    ["epoch"] = OnlineCheckpointJson.Number(mark.Epoch), ["sequence"] = OnlineCheckpointJson.Number(mark.Sequence),
                    ["tick"] = OnlineCheckpointJson.Number(mark.Tick) } };
        public Dictionary<string, object> ToValue() => Value(UploadId, Term, PreviousHeadId, Mark, WireHash, RawLength, TotalBytes, ChunkCount);
        public bool Same(OnlineCheckpointManifest other) => other != null && OnlineJson.Hash(ToValue()) == OnlineJson.Hash(other.ToValue());
    }
    public sealed class OnlineCheckpointState
    {
        public OnlineCheckpointWriter Writer { get; }
        public OnlineCheckpointManifest Head { get; }
        public OnlineCheckpointManifest Previous { get; }
        internal OnlineCheckpointState(JsonNode node)
        {
            OnlineCheckpointJson.Fields(node, "writer", "head", "previous");
            Writer = node["writer"].IsNull ? null : new OnlineCheckpointWriter(node["writer"]);
            Head = node["head"].IsNull ? null : new OnlineCheckpointManifest(node["head"]);
            Previous = node["previous"].IsNull ? null : new OnlineCheckpointManifest(node["previous"]);
            if ((Head == null && Previous != null) || (Head != null && Head.PreviousHeadId != Previous?.UploadId))
                throw new FormatException("Checkpoint retention chain mismatch.");
        }
    }
    public sealed class OnlineCheckpointUpload
    {
        public OnlineCheckpointManifest Manifest { get; }
        public OnlineSnapshotPacket Packet { get; }
        internal OnlineCheckpointUpload(OnlineCheckpointManifest manifest, OnlineSnapshotPacket packet)
        { Manifest = manifest; Packet = packet; }
    }
    public sealed class OnlineCheckpointService
    {
        readonly OnlineCoordinatorClient client;
        readonly OnlineMatchExpectation expected;
        readonly string identityHash;
        public string LocalPlayerId => client.Credentials?.PlayerId;
        public OnlineCheckpointService(OnlineCoordinatorClient client, OnlineMatchExpectation expected, string identityHash)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.expected = expected ?? throw new ArgumentNullException(nameof(expected));
            if (expected.Settings.ProtocolVersion != "wordbound-v2") throw new ArgumentException("Canonical checkpoint schema2 requires wordbound-v2.");
            this.identityHash = OnlineJson.Text(OnlineJson.Parse(OnlineJson.Encode(
                new Dictionary<string, object> { ["hash"] = identityHash })), "hash", OnlineJson.HashPattern);
        }
        void Bind(OnlineCheckpointManifest manifest)
        {
            if (manifest != null && (manifest.Mark.MatchId != expected.MatchId || manifest.Mark.IdentityHash != identityHash))
                throw new FormatException("Checkpoint belongs to another match or content binding.");
        }
        OnlineCheckpointState State(JsonNode node)
        {
            var state = new OnlineCheckpointState(node); Bind(state.Head); Bind(state.Previous); return state;
        }
        public async Task<OnlineCheckpointState> Claim(string claimId, CancellationToken ct = default)
        {
            var state = State(await client.CheckpointAction(expected, "checkpoint-claim", new Dictionary<string, object> {
                ["claimId"] = claimId, ["reservationGeneration"] = expected.Generation, ["rosterHash"] = expected.RosterHash }, ct).ConfigureAwait(false));
            if (state.Writer == null || state.Writer.PlayerId != LocalPlayerId || state.Writer.ClaimId != claimId)
                throw new FormatException("Coordinator did not grant this writer claim.");
            if (state.Writer.BaseHeadId != state.Head?.UploadId || state.Writer.Epoch != (state.Head == null ? 0 : checked(state.Head.Mark.Epoch + 1)))
                throw new FormatException("Writer claim must restore the exact committed head.");
            return state;
        }
        public async Task<OnlineCheckpointState> Renew(OnlineCheckpointWriter writer, CancellationToken ct = default)
        {
            var state = State(await client.CheckpointAction(expected, "checkpoint-renew",
                new Dictionary<string, object> { ["term"] = writer.Term }, ct).ConfigureAwait(false));
            SameWriter(writer, state.Writer);
            return state;
        }
        void SameWriter(OnlineCheckpointWriter writer, OnlineCheckpointWriter next)
        {
            if (next == null || next.PlayerId != LocalPlayerId || next.PlayerId != writer.PlayerId || next.ClaimId != writer.ClaimId ||
                next.Term != writer.Term || next.Epoch != writer.Epoch || next.BaseHeadId != writer.BaseHeadId ||
                next.ExpiresAtMilliseconds < writer.ExpiresAtMilliseconds)
                throw new FormatException("Writer renewal changed its authority fence.");
        }
        public async Task<OnlineCheckpointState> OpenAdmission(OnlineCheckpointWriter writer, CancellationToken ct = default)
        {
            var state = State(await client.CheckpointAction(expected, "checkpoint-open",
                new Dictionary<string, object> { ["term"] = writer.Term, ["headId"] = writer.BaseHeadId }, ct).ConfigureAwait(false));
            SameWriter(writer, state.Writer);
            if (!state.Writer.AdmissionOpen) throw new FormatException("Coordinator did not open writer admission.");
            return state;
        }
        public async Task<OnlineCheckpointState> Head(CancellationToken ct = default) =>
            State(await client.CheckpointAction(expected, "checkpoint-head", new Dictionary<string, object>(), ct).ConfigureAwait(false));
        public OnlineCheckpointUpload Prepare(OnlineCheckpointWriter writer, OnlineCheckpointManifest previous, OnlineSnapshotMark mark, string json)
        {
            if (writer == null || writer.PlayerId != LocalPlayerId || mark.Epoch != writer.Epoch)
                throw new InvalidOperationException("Publication requires the current writer epoch.");
            Bind(previous);
            if (previous != null && (mark.Sequence <= previous.Mark.Sequence || mark.Tick < previous.Mark.Tick))
                throw new InvalidOperationException("Checkpoint frontier cannot regress.");
            var packet = OnlineSnapshotPacket.Compress(json);
            var manifest = new OnlineCheckpointManifest(writer, previous, mark, packet);
            Bind(manifest); return new OnlineCheckpointUpload(manifest, packet);
        }
        public async Task<OnlineCheckpointManifest> Commit(OnlineCheckpointUpload upload, CancellationToken ct = default)
        {
            var manifest = upload.Manifest; Bind(manifest);
            var begun = await client.CheckpointAction(expected, "checkpoint-begin", manifest.ToValue(), ct).ConfigureAwait(false);
            OnlineJson.Fields(begun, "manifest");
            if (!manifest.Same(new OnlineCheckpointManifest(begun["manifest"]))) throw new FormatException("Checkpoint upload manifest changed.");
            await ParallelChunks(manifest.ChunkCount, async index => {
                var response = await client.CheckpointAction(expected, "checkpoint-chunk", new Dictionary<string, object> {
                    ["term"] = manifest.Term, ["uploadId"] = manifest.UploadId, ["index"] = index,
                    ["data"] = OnlineJson.Base64Url(upload.Packet.Chunk(index)) }, ct).ConfigureAwait(false);
                OnlineJson.Fields(response, "uploadId", "index");
                if (response["uploadId"].String() != manifest.UploadId || OnlineJson.Integer(response, "index") != index)
                    throw new FormatException("Checkpoint chunk receipt mismatch.");
            }).ConfigureAwait(false);
            var result = await client.CheckpointAction(expected, "checkpoint-finalize", new Dictionary<string, object> {
                ["term"] = manifest.Term, ["uploadId"] = manifest.UploadId }, ct).ConfigureAwait(false);
            OnlineJson.Fields(result, "head");
            var committed = new OnlineCheckpointManifest(result["head"]);
            if (!manifest.Same(committed)) throw new FormatException("Coordinator committed a different checkpoint.");
            return committed;
        }
        public async Task<string> Download(OnlineCheckpointManifest manifest, CancellationToken ct = default)
        {
            Bind(manifest);
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var chunks = new byte[manifest.ChunkCount][];
            await ParallelChunks(chunks.Length, async index => {
                var response = await client.CheckpointAction(expected, "checkpoint-read", new Dictionary<string, object> {
                    ["uploadId"] = manifest.UploadId, ["index"] = index }, ct).ConfigureAwait(false);
                OnlineJson.Fields(response, "uploadId", "index", "data");
                if (response["uploadId"].String() != manifest.UploadId || OnlineJson.Integer(response, "index") != index)
                    throw new FormatException("Checkpoint read belongs to another upload.");
                chunks[index] = OnlineJson.DecodeBase64Url(response["data"].String(), OnlineSnapshotPacket.ChunkBytes);
            }).ConfigureAwait(false);
            string json = OnlineSnapshotPacket.Restore(chunks, manifest.TotalBytes, manifest.WireHash);
            if (System.Text.Encoding.UTF8.GetByteCount(json) != manifest.RawLength) throw new FormatException("Checkpoint raw length mismatch.");
            return json;
        }
        static async Task ParallelChunks(int count, Func<int, Task> action)
        {
            int next = -1;
            async Task Worker()
            {
                while (true) { int index = Interlocked.Increment(ref next); if (index >= count) return; await action(index).ConfigureAwait(false); }
            }
            await Task.WhenAll(Enumerable.Range(0, Math.Min(4, count)).Select(_ => Worker())).ConfigureAwait(false);
        }
    }
}
