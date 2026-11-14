using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class OnlineSnapshotPacket
    {
        public const int MaximumRawBytes = 8388608;
        public const int MaximumCompressedBytes = 262144;
        public const int ChunkBytes = 4096;
        public const int MaximumChunks = MaximumCompressedBytes / ChunkBytes;
        const uint Magic = 0x31534257;
        const int HeaderBytes = 4 + 4 + 32;
        readonly byte[] packet;
        public int Length => packet.Length;
        public int ChunkCount => (packet.Length + ChunkBytes - 1) / ChunkBytes;
        public string WireHash { get; }
        public int RawLength { get; }
        OnlineSnapshotPacket(byte[] bytes, int rawLength)
        {
            packet = bytes; RawLength = rawLength;
            using var sha = SHA256.Create();
            WireHash = string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }
        public byte[] Chunk(int index)
        {
            if (index < 0 || index >= ChunkCount) throw new ArgumentOutOfRangeException(nameof(index));
            int offset = index * ChunkBytes;
            var result = new byte[Math.Min(ChunkBytes, Length - offset)];
            Array.Copy(packet, offset, result, 0, result.Length); return result;
        }
        public static OnlineSnapshotPacket Compress(string json)
        {
            if (json == null || json.Length > MaximumRawBytes) throw new FormatException("Snapshot exceeds the raw limit.");
            byte[] raw = new UTF8Encoding(false, true).GetBytes(json);
            if (raw.Length == 0 || raw.Length > MaximumRawBytes) throw new FormatException("Snapshot exceeds the raw limit.");
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic); writer.Write(raw.Length);
                using var sha = SHA256.Create(); writer.Write(sha.ComputeHash(raw));
            }
            using (var zip = new DeflateStream(stream, CompressionLevel.Optimal, true)) zip.Write(raw, 0, raw.Length);
            if (stream.Length > MaximumCompressedBytes) throw new FormatException("Snapshot exceeds the migration packet limit.");
            return new OnlineSnapshotPacket(stream.ToArray(), raw.Length);
        }
        public static string Restore(byte[][] chunks, int totalBytes, string expectedWireHash)
        {
            if (totalBytes <= HeaderBytes || totalBytes > MaximumCompressedBytes ||
                chunks == null || chunks.Length != (totalBytes + ChunkBytes - 1) / ChunkBytes)
                throw new FormatException("Incomplete snapshot packet.");
            var bytes = new byte[totalBytes];
            for (int i = 0; i < chunks.Length; i++)
            {
                int length = Math.Min(ChunkBytes, totalBytes - i * ChunkBytes);
                if (chunks[i] == null || chunks[i].Length != length) throw new FormatException("Incomplete snapshot chunk.");
                Array.Copy(chunks[i], 0, bytes, i * ChunkBytes, length);
            }
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            if (reader.ReadUInt32() != Magic) throw new FormatException("Unknown snapshot packet format.");
            int rawLength = reader.ReadInt32();
            if (rawLength < 1 || rawLength > MaximumRawBytes) throw new FormatException("Snapshot inflation exceeds its limit.");
            byte[] rawHash = reader.ReadBytes(32);
            var packet = new OnlineSnapshotPacket(bytes, rawLength);
            if (packet.WireHash != expectedWireHash) throw new FormatException("Snapshot packet hash mismatch.");
            var raw = new byte[rawLength];
            using var zip = new DeflateStream(stream, CompressionMode.Decompress, true);
            int read = 0;
            while (read < raw.Length)
            {
                int count = zip.Read(raw, read, raw.Length - read);
                if (count == 0) throw new FormatException("Truncated snapshot payload.");
                read += count;
            }
            if (zip.ReadByte() != -1) throw new FormatException("Snapshot inflated beyond its declared size.");
            using var sha = SHA256.Create();
            if (!sha.ComputeHash(raw).SequenceEqual(rawHash)) throw new FormatException("Snapshot payload hash mismatch.");
            return new UTF8Encoding(false, true).GetString(raw);
        }
    }
}
