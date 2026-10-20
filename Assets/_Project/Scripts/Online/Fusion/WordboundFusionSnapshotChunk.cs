using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundFusionSnapshotChunk : NetworkBehaviour
    {
        [Networked] public int Index { get; set; }
        [Networked] public int Count { get; set; }
        [Networked] public int TotalBytes { get; set; }
        [Networked] public int PayloadBytes { get; set; }
        [Networked] public long Epoch { get; set; }
        [Networked] public long Sequence { get; set; }
        [Networked] public long MatchTick { get; set; }
        [Networked] public NetworkString<_64> MatchId { get; set; }
        [Networked] public NetworkString<_64> IdentityHash { get; set; }
        [Networked] public NetworkString<_64> StateHash { get; set; }
        [Networked] public NetworkString<_64> WireHash { get; set; }
        [Networked, Capacity(1024)] public NetworkArray<uint> Payload => default;

        public void Write(OnlineSnapshotMark mark, OnlineSnapshotPacket packet, int index)
        {
            if (!Object.HasStateAuthority) throw new InvalidOperationException("Only the host writes migration state.");
            byte[] data = packet.Chunk(index);
            Index = index; Count = packet.ChunkCount; TotalBytes = packet.Length; PayloadBytes = data.Length;
            Epoch = mark.Epoch; Sequence = mark.Sequence; MatchTick = mark.Tick;
            MatchId = mark.MatchId; IdentityHash = mark.IdentityHash; StateHash = mark.StateHash; WireHash = packet.WireHash;
            for (int word = 0; word < Payload.Length; word++)
            {
                uint value = 0;
                for (int b = 0; b < 4 && word * 4 + b < data.Length; b++) value |= (uint)data[word * 4 + b] << (8 * b);
                Payload.Set(word, value);
            }
        }
        public OnlineSnapshotMark Mark() => new OnlineSnapshotMark(MatchId.ToString(), IdentityHash.ToString(), Epoch, Sequence, MatchTick, StateHash.ToString());
        byte[] ReadPayload()
        {
            if (PayloadBytes < 1 || PayloadBytes > OnlineSnapshotPacket.ChunkBytes) throw new FormatException("Invalid carrier payload.");
            var bytes = new byte[PayloadBytes];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(Payload.Get(i / 4) >> (8 * (i % 4)));
            return bytes;
        }
        public static string Restore(IEnumerable<WordboundFusionSnapshotChunk> source, out OnlineSnapshotMark mark)
        {
            var chunks = source.Take(OnlineSnapshotPacket.MaximumChunks + 1).ToArray();
            if (chunks.Length == 0 || chunks.Length > OnlineSnapshotPacket.MaximumChunks || chunks.Any(c => c == null))
                throw new FormatException("Missing migration carrier.");
            var first = chunks[0]; mark = first.Mark(); var expected = mark;
            if (chunks.Length != first.Count || chunks.Any(c => c.Count != first.Count || c.TotalBytes != first.TotalBytes ||
                c.WireHash != first.WireHash || !c.Mark().Same(expected)) ||
                chunks.Select(c => c.Index).Distinct().Count() != chunks.Length ||
                chunks.Any(c => c.Index < 0 || c.Index >= chunks.Length))
                throw new FormatException("Mixed or incomplete migration carrier.");
            return OnlineSnapshotPacket.Restore(chunks.OrderBy(c => c.Index).Select(c => c.ReadPayload()).ToArray(),
                first.TotalBytes, first.WireHash.ToString());
        }
    }
}
