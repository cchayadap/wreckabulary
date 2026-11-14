using System;
using System.Text.RegularExpressions;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public static class WordboundStartDeadline
    {
        public const long HostMilliseconds = 90000;
        public const long ClientGraceMilliseconds = 5000;
        public static bool HostExpired(long startedAtMilliseconds, long nowMilliseconds) =>
            nowMilliseconds >= checked(startedAtMilliseconds + HostMilliseconds);
        public static bool ClientExpired(long startedAtMilliseconds, long nowMilliseconds) =>
            nowMilliseconds >= checked(startedAtMilliseconds + HostMilliseconds + ClientGraceMilliseconds);
        public static bool ClientExpired(long matchStartedAtMilliseconds, long sessionStartedAtMilliseconds, long nowMilliseconds) =>
            ClientExpired(Math.Max(matchStartedAtMilliseconds, sessionStartedAtMilliseconds), nowMilliseconds);
    }

    public readonly struct WordboundSnapshotChunkHeader
    {
        public int Count { get; }
        public int Index { get; }
        public int Length { get; }
        public string WireHash { get; }
        WordboundSnapshotChunkHeader(int count, int index, int length, string wireHash)
        { Count = count; Index = index; Length = length; WireHash = wireHash; }

        public static WordboundSnapshotChunkHeader Read(JsonNode body)
        {
            if (body == null || !body.IsObject) throw new FormatException("Snapshot chunk header is missing.");
            int length = Bounded(body, "length", 1, OnlineSnapshotPacket.MaximumCompressedBytes);
            int count = Bounded(body, "count", 1, OnlineSnapshotPacket.MaximumChunks);
            int index = Bounded(body, "index", 0, count - 1);
            if (count != (length + OnlineSnapshotPacket.ChunkBytes - 1) / OnlineSnapshotPacket.ChunkBytes)
                throw new FormatException("Snapshot chunk count does not match its length.");
            string hash = body["wireHash"].String();
            if (hash == null || !Regex.IsMatch(hash, "\\A[a-f0-9]{64}\\z", RegexOptions.CultureInvariant))
                throw new FormatException("Snapshot chunk hash is invalid.");
            return new WordboundSnapshotChunkHeader(count, index, length, hash);
        }

        static int Bounded(JsonNode body, string name, int minimum, int maximum)
        {
            if (!body.Has(name)) throw new FormatException("Snapshot chunk " + name + " is missing.");
            double value = body[name].Number();
            if (double.IsNaN(value) || double.IsInfinity(value) || value != Math.Truncate(value) || value < minimum || value > maximum)
                throw new FormatException("Snapshot chunk " + name + " is out of range.");
            return (int)value;
        }
    }
}
