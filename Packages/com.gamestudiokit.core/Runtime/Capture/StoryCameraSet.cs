// Game Studio Kit (MIT). Docs/progress/cameras.json (CONTRACTS §7.7) as plain data:
// {schema:1, seed, capture_delta_time, warmup_frames, cameras:[{name, scene, position:[x,y,z], rotation_euler:[x,y,z],
//  fov, resolution:[w,h], profile:"qa"|"marketing"}]}. Engine-free so it is unit-tested under plain .NET.
using System;
using System.Collections.Generic;
using GameStudioKit.Common;

namespace GameStudioKit.Capture
{
    public sealed class StoryCameraDef
    {
        public string Name;
        public string Scene;
        public double[] Position = new double[3];
        public double[] RotationEuler = new double[3];
        public double Fov = 60;
        public int Width = 1920;
        public int Height = 1080;

        /// <summary>"qa" (deterministic gate captures) or "marketing".</summary>
        public string Profile = "qa";
    }

    public sealed class StoryCameraSet
    {
        public int Seed;
        public double CaptureDeltaTime = 1.0 / 30.0;
        public int WarmupFrames = 30;
        public readonly List<StoryCameraDef> Cameras = new List<StoryCameraDef>();
        public readonly List<string> Warnings = new List<string>();

        public static StoryCameraSet Load(string path) => Parse(KitIo.ReadAllText(path));

        public static StoryCameraSet Parse(string json)
        {
            var root = KitJson.AsObject(KitJson.Parse(json));
            if (root == null) throw new FormatException("cameras.json: the root is not an object");
            var set = new StoryCameraSet
            {
                Seed = KitJson.GetInt(root, "seed", 0),
                CaptureDeltaTime = KitJson.GetDouble(root, "capture_delta_time", 1.0 / 30.0),
                WarmupFrames = Math.Max(0, KitJson.GetInt(root, "warmup_frames", 30)),
            };
            if (set.CaptureDeltaTime <= 0 || set.CaptureDeltaTime > 1)
            {
                set.Warnings.Add("capture_delta_time must be in (0, 1]; using 1/30");
                set.CaptureDeltaTime = 1.0 / 30.0;
            }
            var cams = KitJson.GetList(root, "cameras");
            if (cams == null) return set;
            foreach (var item in cams)
            {
                var o = KitJson.AsObject(item);
                if (o == null) continue;
                var def = new StoryCameraDef
                {
                    Name = KitJson.GetString(o, "name"),
                    Scene = KitJson.GetString(o, "scene"),
                    Fov = KitJson.GetDouble(o, "fov", 60),
                    Profile = KitJson.GetString(o, "profile", "qa"),
                };
                if (string.IsNullOrEmpty(def.Name)) { set.Warnings.Add("a camera has no name"); continue; }
                if (!ReadVec3(o, "position", def.Position)) set.Warnings.Add(def.Name + ": position must be [x,y,z]");
                if (!ReadVec3(o, "rotation_euler", def.RotationEuler)) set.Warnings.Add(def.Name + ": rotation_euler must be [x,y,z]");
                var res = KitJson.GetList(o, "resolution");
                if (res != null && res.Count == 2 && KitJson.IsNumber(res[0]) && KitJson.IsNumber(res[1]))
                {
                    def.Width = Math.Max(16, (int)KitJson.ToDouble(res[0]));
                    def.Height = Math.Max(16, (int)KitJson.ToDouble(res[1]));
                }
                if (def.Fov <= 1 || def.Fov >= 179) { set.Warnings.Add(def.Name + ": fov out of range; using 60"); def.Fov = 60; }
                set.Cameras.Add(def);
            }
            return set;
        }

        public StoryCameraDef Find(string name)
        {
            foreach (var c in Cameras) if (c.Name == name) return c;
            return null;
        }

        static bool ReadVec3(Dictionary<string, object> o, string key, double[] into)
        {
            var list = KitJson.GetList(o, key);
            if (list == null || list.Count != 3) return false;
            for (int i = 0; i < 3; i++)
            {
                if (!KitJson.IsNumber(list[i])) return false;
                into[i] = KitJson.ToDouble(list[i]);
            }
            return true;
        }
    }
}
