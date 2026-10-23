using System;
using System.Collections.Generic;
using System.IO;
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
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly Dictionary<string, string> unsavedRecovery = new Dictionary<string, string>();
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

        public static void Save(WordboundCourseAsset asset, string savedSourceHash, CourseDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            string guid = GuidFor(asset), path = PathFor(guid);
            string json = JsonConvert.SerializeObject(new PendingDraft {
                AssetGuid = guid, SavedSourceHash = savedSourceHash, Document = document
            }, Settings);
            if (Utf8.GetByteCount(json) > MaximumBytes) throw new ArgumentException("Pending form exceeds the 4 MiB recovery limit.");
            unsavedRecovery[guid] = json;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json, Utf8);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            unsavedRecovery.Remove(guid);
        }

        public static PendingDraft Load(WordboundCourseAsset asset)
        {
            string guid = GuidFor(asset), path = PathFor(guid);
            if (!unsavedRecovery.TryGetValue(guid, out string json))
            {
                if (!File.Exists(path)) return null;
                if (new FileInfo(path).Length > MaximumBytes) throw new ArgumentException("Pending form exceeds the 4 MiB recovery limit.");
                json = File.ReadAllText(path, Utf8);
            }
            var saved = JsonConvert.DeserializeObject<PendingDraft>(json, Settings);
            if (saved == null || saved.Format != "wordbound.inspector-draft-v1" || saved.AssetGuid != guid ||
                saved.SavedSourceHash == null || saved.SavedSourceHash.Length != 64 || saved.Document == null)
                throw new ArgumentException("Pending form recovery does not match this course asset.");
            return saved;
        }

        public static void Clear(WordboundCourseAsset asset)
        {
            string guid = GuidFor(asset), path = PathFor(guid);
            if (File.Exists(path)) File.Delete(path);
            unsavedRecovery.Remove(guid);
        }

        public static bool IsMemoryOnly(WordboundCourseAsset asset) => unsavedRecovery.ContainsKey(GuidFor(asset));

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
