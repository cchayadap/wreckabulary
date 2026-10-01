using UnityEngine;

namespace Wreckabulary.Art
{
    /// <summary>Shared imported-model placement helpers; never reset the FBX root's rotation.</summary>
    public static class ModelVisual
    {
        public static GameObject Spawn(string key, Transform parent, string skin = MaterialLibrary.StandardSkin)
        {
            var library = ModelLibrary.Load();
            if (!library || !library.Find(key)) return null;
            var copy = library.Spawn(key, parent);
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            var materials = MaterialLibrary.Load();
            if (materials) materials.ApplySkin(copy, skin);
            return copy;
        }

        /// <summary>Uses shared mesh bounds, including the authored import-root transform.</summary>
        public static Bounds BoundsIn(Transform space, GameObject model)
        {
            var result = new Bounds();
            bool hasPoint = false;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                Encapsulate(ref result, ref hasPoint, filter.sharedMesh.bounds,
                    space.worldToLocalMatrix * filter.transform.localToWorldMatrix);
            }
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.sharedMesh) continue;
                Encapsulate(ref result, ref hasPoint, renderer.sharedMesh.bounds,
                    space.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
            }
            return result;
        }

        static void Encapsulate(ref Bounds result, ref bool hasPoint, Bounds meshBounds, Matrix4x4 matrix)
        {
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                var point = matrix.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents, sign));
                if (hasPoint) result.Encapsulate(point);
                else { result = new Bounds(point, Vector3.zero); hasPoint = true; }
            }
        }

        public static Transform FindNamed(GameObject root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        public static Vector3 GripIn(Transform space, GameObject model)
        {
            var grip = FindNamed(model, "Grip_R");
            return grip ? space.InverseTransformPoint(grip.position) : BoundsIn(space, model).center;
        }
    }
}
