using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTools
{
    public static class HouseItemsV2Regrip
    {
        const string GripName = "Grip_R";
        const float Settled = .001f;

        [MenuItem("Wreckabulary/Art/Refit House Items (batch 2) To The Manifest")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Refit House items outside Play Mode.");
            var entries = Json.Parse(File.ReadAllText(HouseItemsV2Setup.SourceRoot + "/manifest.json"), "manifest.json")["entries"].Items.ToArray();
            var refitted = new List<string>();
            foreach (var entry in entries)
            {
                string word = entry["word"].String();
                AssetDatabase.ImportAsset(HouseItemsV2Setup.ModelPath(word), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                if (Refit(word, entry["targetLongest"].Float(), entry["grip"].Floats(3))) refitted.Add(word);
            }
            HouseItemsV2Setup.CreateOrVerify();
            Debug.Log("HOUSE_ITEMS_V2_REFIT " + (refitted.Count == 0 ? "none" : string.Join(", ", refitted)));
        }

        static bool Refit(string word, float longest, float[] grip)
        {
            string path = HouseItemsV2Setup.PrefabPath(word);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) return false;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var socket = root.transform.Find(GripName);
                var model = root.transform.Cast<Transform>().SingleOrDefault(child => child != socket);
                if (!socket || !model) throw new InvalidOperationException("Expected one model and one " + GripName + " under " + path);
                var target = new Vector3(grip[0], grip[1], grip[2]);
                var bounds = ModelVisual.BoundsIn(root.transform, model.gameObject);
                float raw = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (raw <= 0f) throw new InvalidOperationException("House item has no visible mesh: " + path);
                bool sized = Mathf.Abs(raw - longest) <= Settled;
                bool seated = Mathf.Abs(bounds.center.x) <= Settled && Mathf.Abs(bounds.min.y) <= Settled && Mathf.Abs(bounds.center.z) <= Settled;
                bool gripped = Vector3.Distance(socket.localPosition, target) <= Settled;
                if (sized && seated && gripped) return false;
                model.localScale *= longest / raw;
                bounds = ModelVisual.BoundsIn(root.transform, model.gameObject);
                model.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                socket.localPosition = target;
                if (!PrefabUtility.SaveAsPrefabAsset(root, path)) throw new IOException("Could not save House item prefab: " + path);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
