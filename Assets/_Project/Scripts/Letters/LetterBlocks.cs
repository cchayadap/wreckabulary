using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    public static class LetterBlocks
    {
        static readonly Vector3[] Faces =
        {
            Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down
        };

        public static GameObject Create(string text, Vector3 size, Material material, Transform parent,
                                        bool collider, bool skipBottom = false)
        {
            var root = new GameObject($"Block_{text}");
            root.transform.SetParent(parent, false);
            root.layer = parent ? parent.gameObject.layer : 0;

            var mesh = new GameObject("Mesh");
            mesh.layer = root.layer;
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale = size;
            mesh.AddComponent<MeshFilter>().sharedMesh = GameAssets.I.blockMesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;

            if (collider) root.AddComponent<BoxCollider>().size = size;

            AddLabels(root.transform, text, size, skipBottom);
            return root;
        }

        public static TextMeshPro[] AddLabels(Transform block, string text, Vector3 size, bool skipBottom)
        {
            var labels = new List<TextMeshPro>();
            foreach (var n in Faces)
            {
                if (skipBottom && n == Vector3.down) continue;

                var go = new GameObject("Label");
                go.layer = block.gameObject.layer;
                go.transform.SetParent(block, false);
                go.transform.localPosition = Vector3.Scale(n, size * 0.5f) + n * 0.003f;
                var up = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
                go.transform.localRotation = Quaternion.LookRotation(-n, up);

                var face = FaceSize(n, size);
                var t = go.AddComponent<TextMeshPro>();
                t.font = GameAssets.I.font;
                t.text = text;
                t.color = GameAssets.I.ink;
                t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.rectTransform.sizeDelta = face * 0.86f;
                t.enableAutoSizing = true;
                t.fontSizeMin = 0.05f;
                t.fontSizeMax = 40f;
                labels.Add(t);
            }
            return labels.ToArray();
        }

        static Vector2 FaceSize(Vector3 n, Vector3 size)
        {
            if (Mathf.Abs(n.x) > 0.5f) return new Vector2(size.z, size.y);
            if (Mathf.Abs(n.y) > 0.5f) return new Vector2(size.x, size.z);
            return new Vector2(size.x, size.y);
        }
    }
}
