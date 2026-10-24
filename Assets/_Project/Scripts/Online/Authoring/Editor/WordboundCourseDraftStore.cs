using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundCourseDraftStore
    {
        const int MaximumBytes = 4 * 1024 * 1024;
        const int MaximumRetainedRecords = 8;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly Dictionary<string, string> unsavedRecovery = new Dictionary<string, string>();
        static readonly Dictionary<string, string> cleanupWarnings = new Dictionary<string, string>();
        const string RecordFormat = "wordbound.inspector-recovery-v2";
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MaxDepth = 32,
            Converters = { new PointConverter(), new BoxConverter() }
        };

        public sealed class PendingDraft
        {
            public string Format = "wordbound.inspector-draft-v1";
            public string AssetGuid;
            public string SavedSourceHash;
            public CourseDocument Document;
        }

        sealed class RecoveryRecord
        {
            public string Format = RecordFormat;
            public string AssetGuid;
            public long Revision;
            public string Kind;
            public PendingDraft Draft;
            public string PriorPrimaryHash;
            public string LegacyHash;
            public string Checksum;
        }

        sealed class DiskState
        {
            public RecoveryRecord Latest;
            public RecoveryRecord Primary;
            public readonly List<string> Files = new List<string>();
        }

        public sealed class LegacyDraftChoiceException : IOException
        {
            public LegacyDraftChoiceException() : base("A temporary draft is available, but its order cannot be verified. Choose which draft to keep.") { }
        }

        public static void Save(WordboundCourseAsset asset, string savedSourceHash, CourseDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            string guid = GuidFor(asset);
            var draft = new PendingDraft {
                AssetGuid = guid, SavedSourceHash = savedSourceHash, Document = document
            };
            SaveDraft(guid, draft, null);
        }

        static void SaveDraft(string guid, PendingDraft draft, string resolvedLegacyHash)
        {
            string json = JsonConvert.SerializeObject(draft, Settings), path = PathFor(guid);
            BoundedBytes(json);
            ValidateDraft(draft, guid);
            unsavedRecovery[guid] = json;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            DiskState state = ReadState(guid, resolvedLegacyHash != null);
            RecoveryRecord record = state.Latest;
            bool same = record != null && record.Revision > 0 && record.Kind == "draft" && DraftJson(record.Draft) == json &&
                (resolvedLegacyHash == null || record.LegacyHash == resolvedLegacyHash);
            if (!same)
            {
                if (state.Latest != null) ReclaimOlder(guid, state.Latest);
                record = new RecoveryRecord {
                    AssetGuid = guid, Revision = checked((record?.Revision ?? 0) + 1), Kind = "draft", Draft = draft,
                    LegacyHash = resolvedLegacyHash ?? record?.LegacyHash, PriorPrimaryHash = record?.PriorPrimaryHash
                };
                Commit(record);
                ReclaimOlder(guid, record);
            }
            byte[] bytes = Encode(record);
            if (!File.Exists(path) || !SameBytes(path, bytes))
            {
                if (!File.Exists(RecordPath(guid, record.Revision))) Commit(record);
                string temporary = path + ".publish";
                WriteFlushed(temporary, bytes, FileMode.Create);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                if (!SameBytes(path, bytes)) throw new IOException("The published recovery draft did not match its committed bytes.");
            }
            unsavedRecovery.Remove(guid);
            Cleanup(guid, record, false);
        }

        static bool SameBytes(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != bytes.Length) return false;
            var buffer = new byte[Math.Min(8192, bytes.Length)];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = stream.Read(buffer, 0, Math.Min(buffer.Length, bytes.Length - offset));
                if (count == 0) return false;
                for (int i = 0; i < count; i++)
                    if (buffer[i] != bytes[offset + i]) return false;
                offset += count;
            }
            return stream.ReadByte() == -1;
        }

        public static PendingDraft Load(WordboundCourseAsset asset)
        {
            string guid = GuidFor(asset);
            if (unsavedRecovery.TryGetValue(guid, out string json)) return ParseDraft(json, guid);
            RecoveryRecord latest = ReadState(guid, false).Latest;
            return latest?.Kind == "draft" ? latest.Draft : null;
        }

        static PendingDraft ParseDraft(string json, string guid)
        {
            if ((string)JObject.Parse(json)["Format"] != "wordbound.inspector-draft-v1")
                throw new ArgumentException("Unsupported pending draft format. The file has been preserved.");
            var saved = JsonConvert.DeserializeObject<PendingDraft>(json, Settings);
            ValidateDraft(saved, guid);
            return saved;
        }

        static void ValidateDraft(PendingDraft saved, string guid)
        {
            if (saved == null || saved.Format != "wordbound.inspector-draft-v1" || saved.AssetGuid != guid ||
                !IsHash(saved.SavedSourceHash) || saved.Document == null)
                throw new ArgumentException("Pending form recovery does not match this course asset.");
        }

        public static void Clear(WordboundCourseAsset asset)
        {
            string guid = GuidFor(asset), path = PathFor(guid);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                RecoveryRecord latest = ReadState(guid, true).Latest;
                if (latest != null) ReclaimOlder(guid, latest);
            }
            catch (ArgumentException) { }
            catch (JsonException) { }
            long revision = 0;
            foreach (string file in RecordFiles(guid)) revision = Math.Max(revision, RevisionFromPath(guid, file));
            if (File.Exists(path))
            {
                try { revision = Math.Max(revision, ReadRecord(path, guid).Revision); }
                catch (ArgumentException) { }
                catch (JsonException) { }
            }
            var discarded = new RecoveryRecord {
                AssetGuid = guid, Revision = checked(revision + 1), Kind = "discard",
                PriorPrimaryHash = File.Exists(path) ? HashFile(path) : null,
                LegacyHash = File.Exists(path + ".tmp") ? HashFile(path + ".tmp") : null
            };
            Commit(discarded);
            unsavedRecovery.Remove(guid);
            Cleanup(guid, discarded, true);
        }

        public static bool IsMemoryOnly(WordboundCourseAsset asset)
        {
            string guid = GuidFor(asset);
            if (!unsavedRecovery.TryGetValue(guid, out string json)) return false;
            try
            {
                RecoveryRecord record = ReadState(guid, true).Latest;
                if (record?.Kind == "draft" && DraftJson(record.Draft) == json) return false;
            }
            catch (Exception) { }
            return true;
        }

        public static string RecoveryNotice(WordboundCourseAsset asset)
        {
            string guid = GuidFor(asset);
            if (IsMemoryOnly(asset)) return "Newest edits are in memory only. Resolve the file error before closing Unity or reloading scripts. Saving will retry.";
            if (unsavedRecovery.ContainsKey(guid)) return "Your latest edits are backed up locally. Saving will retry.";
            DiskState state = ReadState(guid, false);
            if (state.Latest?.Kind == "discard") return "Your discard is saved. Older recovery files still need cleanup.";
            if (state.Latest?.Kind == "draft" && state.Files.Contains(RecordPath(guid, state.Latest.Revision)))
            {
                if (state.Primary == null || ContentHash(state.Primary) != ContentHash(state.Latest))
                    return "Recovered your backed-up edits. Saving will retry.";
            }
            return cleanupWarnings.TryGetValue(guid, out string warning) ? warning : "";
        }

        public static void ResolveLegacy(WordboundCourseAsset asset, bool restoreTemporary)
        {
            string guid = GuidFor(asset), temporary = PathFor(guid) + ".tmp";
            string hash = HashFile(temporary);
            PendingDraft legacy = ParseDraft(ReadText(temporary), guid);
            PendingDraft selected = restoreTemporary ? legacy : ReadState(guid, true).Latest?.Draft;
            if (selected == null) Clear(asset);
            else SaveDraft(guid, selected, hash);
        }

        static DiskState ReadState(string guid, bool ignoreLegacy)
        {
            var state = new DiskState();
            var records = new Dictionary<long, RecoveryRecord>();
            RecoveryRecord discarded = null;
            foreach (string file in RecordFiles(guid).OrderByDescending(file => RevisionFromPath(guid, file)))
            {
                long revision = RevisionFromPath(guid, file);
                state.Files.Add(file);
                if (discarded != null && revision < discarded.Revision) continue;
                RecoveryRecord record = ReadRecord(file, guid);
                if (record.Revision != revision) throw new ArgumentException("Recovery revision does not match its file.");
                AddRecord(records, record);
                if (state.Latest == null) state.Latest = record;
                if (record.Kind == "discard") discarded = record;
            }
            string path = PathFor(guid);
            if (File.Exists(path) && !(state.Latest?.PriorPrimaryHash != null && HashFile(path) == state.Latest.PriorPrimaryHash))
            {
                RecoveryRecord primary = ReadRecord(path, guid);
                state.Primary = primary;
                if (!(discarded != null && primary.Revision < discarded.Revision)) AddRecord(records, primary);
                if (state.Latest == null || primary.Revision > state.Latest.Revision) state.Latest = primary;
            }
            string legacy = path + ".tmp";
            if (!ignoreLegacy && File.Exists(legacy) && HashFile(legacy) != state.Latest?.LegacyHash)
            {
                PendingDraft orphan = ParseDraft(ReadText(legacy), guid);
                if (state.Latest?.Kind != "draft" || DraftJson(orphan) != DraftJson(state.Latest.Draft))
                    throw new LegacyDraftChoiceException();
            }
            return state;
        }

        static void AddRecord(Dictionary<long, RecoveryRecord> records, RecoveryRecord record)
        {
            if (records.TryGetValue(record.Revision, out RecoveryRecord existing) && ContentHash(existing) != ContentHash(record))
                throw new ArgumentException("Conflicting recovery records have the same revision. Both files have been preserved.");
            records[record.Revision] = record;
        }

        static RecoveryRecord ReadRecord(string path, string guid)
        {
            string json = ReadText(path);
            var fields = JObject.Parse(json);
            string format = (string)fields["Format"];
            if (format == "wordbound.inspector-draft-v1")
                return new RecoveryRecord { AssetGuid = guid, Revision = 0, Kind = "draft", Draft = ParseDraft(json, guid) };
            if (format != RecordFormat) throw new ArgumentException("Unsupported committed recovery format. The file has been preserved.");
            var record = JsonConvert.DeserializeObject<RecoveryRecord>(json, Settings);
            if (record == null || record.Format != RecordFormat || record.AssetGuid != guid || record.Revision <= 0 ||
                (record.Kind != "draft" && record.Kind != "discard") || !IsHash(record.Checksum) ||
                (record.PriorPrimaryHash != null && !IsHash(record.PriorPrimaryHash)) ||
                (record.LegacyHash != null && !IsHash(record.LegacyHash)) || record.Checksum != ContentHash(record))
                throw new ArgumentException("Committed recovery record is damaged or belongs to another course.");
            if (record.Kind == "draft")
            {
                if ((string)fields["Draft"]?["Format"] != "wordbound.inspector-draft-v1")
                    throw new ArgumentException("Unsupported pending payload format. The file has been preserved.");
                ValidateDraft(record.Draft, guid);
            }
            else if (record.Draft != null) throw new ArgumentException("A discard record cannot contain draft data.");
            return record;
        }

        static string ContentHash(RecoveryRecord record) => CourseJson.Sha256(JsonConvert.SerializeObject(new {
            record.Format, record.AssetGuid, record.Revision, record.Kind, record.Draft, record.PriorPrimaryHash, record.LegacyHash
        }, Settings));
        static string DraftJson(PendingDraft draft) => JsonConvert.SerializeObject(draft, Settings);
        static byte[] Encode(RecoveryRecord record) => BoundedBytes(JsonConvert.SerializeObject(record, Settings));
        static byte[] BoundedBytes(string json)
        {
            if (Utf8.GetByteCount(json) > MaximumBytes) throw new ArgumentException("Pending form exceeds the 4 MiB recovery limit.");
            return Utf8.GetBytes(json);
        }
        static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
        static string ReadText(string path)
        {
            byte[] data = ReadBytes(path);
            int start = data.Length >= 3 && data[0] == 0xef && data[1] == 0xbb && data[2] == 0xbf ? 3 : 0;
            return Utf8.GetString(data, start, data.Length - start);
        }
        static byte[] ReadBytes(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumBytes) throw new ArgumentException("Pending form exceeds the 4 MiB recovery limit.");
            using var bytes = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                if (bytes.Length + count > MaximumBytes) throw new ArgumentException("Pending form exceeds the 4 MiB recovery limit.");
                bytes.Write(buffer, 0, count);
            }
            return bytes.ToArray();
        }
        static string HashFile(string path)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(ReadBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        static void WriteFlushed(string path, byte[] bytes, FileMode mode)
        {
            using var stream = new FileStream(path, mode, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
        static void Commit(RecoveryRecord record)
        {
            if (RecordFiles(record.AssetGuid).Count() >= MaximumRetainedRecords)
                throw new IOException("Older recovery files could not be removed. Newest edits remain in memory until recovery storage is available.");
            record.Checksum = ContentHash(record);
            byte[] bytes = Encode(record);
            string target = RecordPath(record.AssetGuid, record.Revision);
            string staging = PathFor(record.AssetGuid) + ".stage";
            WriteFlushed(staging, bytes, FileMode.Create);
            RecoveryRecord verified = ReadRecord(staging, record.AssetGuid);
            if (verified.Checksum != record.Checksum || !SameBytes(staging, bytes)) throw new IOException("Recovery staging verification failed.");
            File.Move(staging, target);
        }
        static string RecordPath(string guid, long revision) => PathFor(guid) + ".pending." + revision.ToString("D20", CultureInfo.InvariantCulture);
        static IEnumerable<string> RecordFiles(string guid)
        {
            string path = PathFor(guid), directory = Path.GetDirectoryName(path);
            string[] files = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, Path.GetFileName(path) + ".pending.*")
                .Take(MaximumRetainedRecords + 1).ToArray() : Array.Empty<string>();
            if (files.Length > MaximumRetainedRecords) throw new IOException("Too many retained recovery records. The files have been preserved for recovery.");
            if (files.Any(file => RevisionFromPath(guid, file) == 0)) throw new ArgumentException("An unrecognized recovery filename has been preserved for inspection.");
            return files;
        }
        static long RevisionFromPath(string guid, string file)
        {
            string prefix = Path.GetFileName(PathFor(guid)) + ".pending.", name = Path.GetFileName(file);
            string suffix = name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : "";
            if (suffix.Length != 20 || !suffix.All(c => c >= '0' && c <= '9')) return 0;
            if (!long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out long revision) || revision <= 0)
                throw new ArgumentException("Recovery filename has an unsupported revision.");
            return revision;
        }
        static void Cleanup(string guid, RecoveryRecord latest, bool discard)
        {
            string path = PathFor(guid), keep = RecordPath(guid, latest.Revision);
            try
            {
                if (discard && File.Exists(path)) File.Delete(path);
                foreach (string file in RecordFiles(guid).Where(file => RevisionFromPath(guid, file) < latest.Revision)
                    .OrderBy(file => RevisionFromPath(guid, file)).ToArray()) File.Delete(file);
                if (File.Exists(path + ".stage")) File.Delete(path + ".stage");
                if (File.Exists(path + ".publish")) File.Delete(path + ".publish");
                if (File.Exists(path + ".tmp"))
                {
                    bool acknowledged = HashFile(path + ".tmp") == latest.LegacyHash ||
                        (!discard && DraftJson(ParseDraft(ReadText(path + ".tmp"), guid)) == DraftJson(latest.Draft));
                    if (!acknowledged) throw new IOException("The temporary draft changed before cleanup; it has been preserved.");
                    File.Delete(path + ".tmp");
                }
                if (File.Exists(keep)) File.Delete(keep);
                cleanupWarnings.Remove(guid);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is JsonException)
            {
                cleanupWarnings[guid] = discard ? "Your discard is saved. Some older recovery files could not be removed." :
                    "Recovery is saved. Some older recovery files could not be removed.";
            }
        }
        static void ReclaimOlder(string guid, RecoveryRecord latest)
        {
            try
            {
                foreach (string file in RecordFiles(guid).Where(file => RevisionFromPath(guid, file) < latest.Revision)
                    .OrderBy(file => RevisionFromPath(guid, file)).ToArray()) File.Delete(file);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                cleanupWarnings[guid] = "Newest recovery is saved. Some older recovery files could not be removed.";
            }
        }

        static string GuidFor(WordboundCourseAsset asset)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (guid.Length != 32) throw new ArgumentException("Save the course asset in the Project window before editing its form.");
            return guid;
        }

        static string PathFor(string guid) => Path.Combine("Library", "Wordbound", "InspectorDrafts", guid + ".json");

        sealed class PointConverter : JsonConverter<CoursePoint>
        {
            public override void WriteJson(JsonWriter writer, CoursePoint value, JsonSerializer serializer) =>
                serializer.Serialize(writer, new[] { value.X, value.Y, value.Z });
            public override CoursePoint ReadJson(JsonReader reader, Type type, CoursePoint existingValue,
                bool hasExistingValue, JsonSerializer serializer)
            {
                var values = JArray.Load(reader);
                if (values.Count != 3) throw new JsonSerializationException("A draft point needs three integer coordinates.");
                var points = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    if (values[i].Type != JTokenType.Integer) throw new JsonSerializationException("Draft coordinates must be integers.");
                    points[i] = checked((int)values[i].Value<long>());
                }
                return new CoursePoint(points[0], points[1], points[2]);
            }
        }

        sealed class BoxConverter : JsonConverter<CourseBox>
        {
            public override void WriteJson(JsonWriter writer, CourseBox value, JsonSerializer serializer) =>
                serializer.Serialize(writer, new[] { value.Min, value.Max });
            public override CourseBox ReadJson(JsonReader reader, Type type, CourseBox existingValue,
                bool hasExistingValue, JsonSerializer serializer)
            {
                var points = serializer.Deserialize<CoursePoint[]>(reader);
                if (points == null || points.Length != 2) throw new JsonSerializationException("A draft volume needs two points.");
                return new CourseBox(points[0], points[1]);
            }
        }
    }
}
