using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.WordboundRuntime
{
    [Serializable]
    public sealed class WordboundModuleDressingEntry
    {
        public string ModuleId = "";
        [Tooltip("Saved architectural wall-fixture prefab. Static mesh renderers only; no usable items, physics, scripts or lights.")]
        public GameObject Prefab;
        public Vector3 LocalPosition;
        public Vector3 LocalEulerAngles;
        public Vector3 LocalScale = Vector3.one;
    }

    public static class WordboundModuleDressing
    {
        public const string RootName = "Authored wall fixtures";
        public const float MinimumHeight = 2.45f;
        public const float MaximumWallDepth = .4f;

        public static void Validate(IReadOnlyList<WordboundModuleDressingEntry> entries)
        {
            if (entries == null) return;
            if (entries.Count > 32) throw new ArgumentException("At most 32 dressed modules are supported.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int rendererCount = 0;
            foreach (var entry in entries)
            {
                if (entry == null || !WordboundIdentity.IsId(entry.ModuleId) || !ids.Add(entry.ModuleId) ||
                    !Finite(entry.LocalPosition) || !Finite(entry.LocalEulerAngles) || !Positive(entry.LocalScale))
                    throw new ArgumentException("Wall fixtures require distinct module IDs and finite transforms with positive scale.");
                ValidatePrefab(entry.Prefab);
                rendererCount += entry.Prefab.GetComponentsInChildren<MeshRenderer>(true).Length;
                if (rendererCount > 128) throw new ArgumentException("A course supports at most 128 authored wall-fixture renderers.");
            }
        }

        static void ValidatePrefab(GameObject prefab)
        {
            if (!prefab || !prefab.activeSelf) throw new ArgumentException("Assign an active static wall-fixture prefab.");
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            if (transforms.Length > 128) throw new ArgumentException("A wall-fixture group supports at most 128 transforms.");
            foreach (var t in transforms)
            {
                var rotation = t.localRotation;
                if (!t.gameObject.activeSelf || !Finite(t.localPosition) || !Positive(t.localScale) ||
                    !Good(rotation.x) || !Good(rotation.y) || !Good(rotation.z) || !Good(rotation.w))
                    throw new ArgumentException("Wall-fixture transforms must be active, finite and positively scaled.");
            }
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                if (!component || !(component is Transform || component is MeshFilter || component is MeshRenderer))
                    throw new ArgumentException("Wall fixtures accept only Transform, MeshFilter and MeshRenderer; no lights, physics, scripts or animation.");
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0 || renderers.Length > 32)
                throw new ArgumentException("A wall-fixture group needs 1–32 static mesh renderers.");
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!renderer.enabled || !filter || !filter.sharedMesh || filter.sharedMesh.vertexCount == 0 ||
                    filter.sharedMesh.subMeshCount == 0 || renderer.sharedMaterials.Length != filter.sharedMesh.subMeshCount ||
                    renderer.sharedMaterials.Any(m => !m) || !Finite(filter.sharedMesh.bounds.center) || !Positive(filter.sharedMesh.bounds.size))
                    throw new ArgumentException("Wall fixtures need enabled meshes with finite nonzero bounds and one assigned material per submesh.");
            }
            if (prefab.GetComponentsInChildren<MeshFilter>(true).Any(f => !f.GetComponent<MeshRenderer>()))
                throw new ArgumentException("Each wall-fixture mesh requires a renderer.");
        }

        public static GameObject Apply(IReadOnlyList<WordboundModuleDressingEntry> entries, string moduleId,
            CourseModuleDefinition definition, Transform module, int? renderLayer = null)
        {
            Validate(entries);
            var entry = entries?.SingleOrDefault(e => e.ModuleId == moduleId);
            if (entry == null) return null;
            if (definition == null || !module) throw new ArgumentException("Wall fixtures require the canonical module and its dimensions.");
            if (renderLayer.HasValue && (renderLayer.Value < 0 || renderLayer.Value > 31)) throw new ArgumentOutOfRangeException(nameof(renderLayer));
            var root = new GameObject(RootName);
            root.SetActive(false);
            root.transform.SetParent(module, false);
            root.transform.localPosition = entry.LocalPosition;
            root.transform.localRotation = Quaternion.Euler(entry.LocalEulerAngles);
            root.transform.localScale = entry.LocalScale;
            try
            {
                var instance = Object.Instantiate(entry.Prefab, root.transform, false);
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                    ValidatePlacement(BoundsIn(module, filter), definition);
                if (renderLayer.HasValue)
                    foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = renderLayer.Value;
                root.SetActive(true);
                return root;
            }
            catch
            {
                root.SetActive(false);
                if (Application.isPlaying) Object.Destroy(root); else Object.DestroyImmediate(root);
                throw;
            }
        }

        static Bounds BoundsIn(Transform module, MeshFilter filter)
        {
            var bounds = filter.sharedMesh.bounds;
            var matrix = module.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.min), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z)));
            return result;
        }

        static void ValidatePlacement(Bounds bounds, CourseModuleDefinition room)
        {
            var min = bounds.min; var max = bounds.max;
            float halfX = room.WidthMm / 2000f, halfZ = room.DepthMm / 2000f;
            bool wallBand = min.x >= halfX - MaximumWallDepth || max.x <= -halfX + MaximumWallDepth ||
                min.z >= halfZ - MaximumWallDepth || max.z <= -halfZ + MaximumWallDepth;
            if (!Finite(min) || !Finite(max) || min.y < MinimumHeight || max.y > room.HeightMm / 1000f - .05f ||
                min.x < -halfX || max.x > halfX || min.z < -halfZ || max.z > halfZ || !wallBand)
                throw new ArgumentException("Wall fixtures must remain inside a shallow room-edge band, above 2.45 m and below the ceiling. Edit the saved prefab or its local pose.");
        }

        static bool Finite(Vector3 value) => Good(value.x) && Good(value.y) && Good(value.z);
        static bool Positive(Vector3 value) => Finite(value) && value.x > 0 && value.y > 0 && value.z > 0;
        static bool Good(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
