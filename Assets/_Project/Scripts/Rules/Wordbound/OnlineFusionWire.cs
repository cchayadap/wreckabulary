using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wreckabulary.Rules.Wordbound
{
    public enum OnlineWireKind : byte { Admission = 1, Ready = 2, Begin = 3, Command = 4, Snapshot = 5, SnapshotAck = 6, Admitted = 7 }

    public sealed class OnlineSnapshotMark
    {
        public string MatchId { get; }
        public string IdentityHash { get; }
        public long Epoch { get; }
        public long Sequence { get; }
        public long Tick { get; }
        public string StateHash { get; }
        public OnlineSnapshotMark(string matchId, string identityHash, long epoch, long sequence, long tick, string stateHash)
        {
            var node = OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["match"] = matchId, ["identity"] = identityHash, ["state"] = stateHash }));
            MatchId = OnlineJson.Text(node, "match", OnlineJson.Uuid);
            IdentityHash = OnlineJson.Text(node, "identity", OnlineJson.HashPattern);
            StateHash = OnlineJson.Text(node, "state", OnlineJson.HashPattern);
            if (epoch < 0 || epoch == long.MaxValue || sequence < 1 || sequence == long.MaxValue || tick < 0 || tick == long.MaxValue)
                throw new FormatException("Invalid snapshot counters.");
            Epoch = epoch; Sequence = sequence; Tick = tick;
        }
        public Dictionary<string, object> ToValue() => new Dictionary<string, object> {
            ["match"] = MatchId, ["identity"] = IdentityHash, ["state"] = StateHash,
            ["epoch"] = Epoch.ToString(CultureInfo.InvariantCulture), ["sequence"] = Sequence.ToString(CultureInfo.InvariantCulture),
            ["tick"] = Tick.ToString(CultureInfo.InvariantCulture) };
        public static OnlineSnapshotMark Read(JsonNode node)
        {
            OnlineJson.Fields(node, "match", "identity", "state", "epoch", "sequence", "tick");
            return new OnlineSnapshotMark(node["match"].String(), node["identity"].String(),
                Counter(node, "epoch"), Counter(node, "sequence"), Counter(node, "tick"), node["state"].String());
        }
        internal static long Counter(JsonNode node, string field)
        {
            string text = OnlineJson.Text(node, field, "^(0|[1-9][0-9]{0,18})$");
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long value) || value == long.MaxValue)
                throw new FormatException("Invalid wire counter.");
            return value;
        }
        public bool Same(OnlineSnapshotMark other) => other != null && MatchId == other.MatchId && IdentityHash == other.IdentityHash &&
            Epoch == other.Epoch && Sequence == other.Sequence && Tick == other.Tick && StateHash == other.StateHash;
    }

    public sealed class OnlineWireMessage
    {
        public OnlineWireKind Kind { get; }
        public JsonNode Body { get; }
        internal OnlineWireMessage(OnlineWireKind kind, JsonNode body) { Kind = kind; Body = body; }
    }
    public static class OnlineFusionWire
    {
        public const int MaximumControlBytes = 24576;
        public const int MaximumCommandBytes = 1024;
        public static byte[] Encode(OnlineWireKind kind, object body)
        {
            byte[] json = new UTF8Encoding(false, true).GetBytes(OnlineJson.Encode(body));
            var bytes = new byte[json.Length + 1]; bytes[0] = (byte)kind; Array.Copy(json, 0, bytes, 1, json.Length);
            Decode(bytes); return bytes;
        }
        public static OnlineWireMessage Decode(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 3 || bytes.Length > MaximumControlBytes) throw new FormatException("Wire frame exceeds its bounds.");
            var kind = (OnlineWireKind)bytes[0];
            var node = OnlineJson.Parse(OnlineJson.DecodeUtf8(bytes.Slice(1).ToArray()));
            switch (kind)
            {
                case OnlineWireKind.Admission:
                    OnlineJson.Fields(node, "certificate", "grant", "connectionTicket", "connectionId");
                    foreach (string field in new[] { "certificate", "grant" })
                        OnlineJson.Text(node, field, "^[A-Za-z0-9_.-]{1,16384}$");
                    OnlineJson.Text(node, "connectionTicket", "^[A-Za-z0-9_-]{43}$");
                    OnlineJson.Text(node, "connectionId", OnlineJson.Uuid);
                    break;
                case OnlineWireKind.Ready:
                case OnlineWireKind.Begin:
                    OnlineJson.Fields(node, "roster"); OnlineJson.Text(node, "roster", OnlineJson.HashPattern); break;
                case OnlineWireKind.Admitted:
                    OnlineJson.Fields(node, "roster", "authorization");
                    OnlineJson.Text(node, "roster", OnlineJson.HashPattern);
                    OnlineJson.Text(node, "authorization", "^[A-Za-z0-9_.-]{1,16384}$");
                    break;
                case OnlineWireKind.Command:
                    if (bytes.Length > MaximumCommandBytes) throw new FormatException("Command exceeds its wire budget.");
                    OnlineJson.Fields(node, "sequence", "epoch", "action", "target", "revision", "slot", "value");
                    if (OnlineSnapshotMark.Counter(node, "sequence") < 1) throw new FormatException("Invalid command sequence.");
                    OnlineSnapshotMark.Counter(node, "epoch");
                    OnlineJson.Text(node, "action", "^(CollectLetter|BeginCraft|CancelCraft|PickUp|Drop|Throw|Deploy|GiveLetter|Transfer|TakeKeepsake|DropKeepsake|UsePuzzle)$");
                    foreach (string field in new[] { "target", "value" }) OnlineJson.Text(node, field, "^[A-Za-z0-9_.:-]{0,96}$");
                    OnlineJson.Integer(node, "revision", 0, int.MaxValue); OnlineJson.Integer(node, "slot", -1, 7); break;
                case OnlineWireKind.Snapshot:
                    OnlineJson.Fields(node, "mark", "index", "count", "length", "wireHash", "data");
                    OnlineSnapshotMark.Read(node["mark"]);
                    int count = (int)OnlineJson.Integer(node, "count", 1, OnlineSnapshotPacket.MaximumChunks);
                    int index = (int)OnlineJson.Integer(node, "index", 0, count - 1);
                    int length = (int)OnlineJson.Integer(node, "length", 41, OnlineSnapshotPacket.MaximumCompressedBytes);
                    if (count != (length + OnlineSnapshotPacket.ChunkBytes - 1) / OnlineSnapshotPacket.ChunkBytes ||
                        OnlineJson.DecodeBase64Url(node["data"].String(), OnlineSnapshotPacket.ChunkBytes).Length !=
                        Math.Min(OnlineSnapshotPacket.ChunkBytes, length - index * OnlineSnapshotPacket.ChunkBytes))
                        throw new FormatException("Invalid snapshot chunk bounds.");
                    OnlineJson.Text(node, "wireHash", OnlineJson.HashPattern); break;
                case OnlineWireKind.SnapshotAck:
                    OnlineSnapshotMark.Read(node); break;
                default: throw new FormatException("Unknown wire message.");
            }
            return new OnlineWireMessage(kind, node);
        }
    }
}
