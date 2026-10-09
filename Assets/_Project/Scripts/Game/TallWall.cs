using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary
{
    public sealed class TallWall : MonoBehaviour
    {
        public const float Low = 1.1f, Exterior = 2.7f, Interior = 2.4f;

        public float FloorY, Height;
        public bool Outside;
        public Transform Trim;
        public Vector2 Tile;

        public float VisualHeight(bool tall) => tall ? Mathf.Min(Outside ? Exterior : Interior, Height) : Low;

        public void Apply(bool tall)
        {
            float h = VisualHeight(tall);
            var t = transform;
            var scale = t.localScale;
            scale.y = h;
            t.localScale = scale;
            var at = t.position;
            at.y = FloorY + h * .5f;
            t.position = at;
            var box = GetComponent<BoxCollider>();
            box.size = new Vector3(1f, Height / h, 1f);
            box.center = new Vector3(0f, (Height - h) * .5f / h, 0f);
            if (Tile != Vector2.zero && TryGetComponent<MeshFilter>(out var filter)) filter.sharedMesh = Surfaces.Box(scale, Tile, t.localPosition);
            if (Trim)
            {
                var top = Trim.position;
                top.y = FloorY + h + .04f;
                Trim.position = top;
            }
        }
    }
}
