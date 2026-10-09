using UnityEngine;

namespace Wreckabulary.Art
{
    /// <summary>Two persistent wear stages without changing furniture collision or its letter payout.</summary>
    public sealed class FurnitureDamageView : MonoBehaviour
    {
        int stage;
        Bounds bounds;
        bool cloth;
        bool flatTop;

        public static void Show(Smashable furniture, float fraction)
        {
            if (fraction > .7f) return;
            if (!furniture.TryGetComponent(out FurnitureDamageView view))
            {
                view = furniture.gameObject.AddComponent<FurnitureDamageView>();
                view.bounds = ModelVisual.BoundsIn(furniture.transform, furniture.gameObject);
                view.cloth = furniture.Word is "SOFA" or "BED" or "RUG" or "MAT";
                view.flatTop = furniture.Word is "TABLE" or "DESK" or "RUG" or "MAT";
                if (!view.cloth && !view.flatTop)
                {
                    var visual = furniture.transform.Find("ImportedVisual");
                    if (visual && visual.childCount > 0)
                        visual.GetChild(0).localRotation *= Quaternion.Euler(0f, 0f, 7f);
                }
            }
            view.ShowStage(fraction > .35f ? 1 : 2);
        }

        void ShowStage(int next)
        {
            if (next <= stage || bounds.size.sqrMagnitude < .01f) return;
            if (!cloth && !flatTop) { stage = next; return; }
            for (int i = stage; i < next; i++)
            {
                float offset = i == 0 ? -.16f : .21f;
                var centre = bounds.center;
                float width = Mathf.Min(bounds.size.x * .27f, .52f);
                float depth = Mathf.Min(bounds.size.z * .32f, .45f);
                var anchor = flatTop
                    ? new Vector3(centre.x + bounds.size.x * offset, bounds.max.y + .009f, centre.z)
                    : new Vector3(centre.x + bounds.size.x * offset, bounds.min.y + bounds.size.y * .42f, bounds.max.z + .009f);
                Vector3 Surface(float x, float vertical) => flatTop ? new Vector3(x, 0f, vertical) : new Vector3(x, vertical, 0f);
                var points = new[]
                {
                    anchor + Surface(-width, -depth), anchor + Surface(-width * .25f, -depth * .3f),
                    anchor + Surface(-width * .5f, depth * .05f), anchor + Surface(width * .1f, depth * .3f),
                    anchor + Surface(width * .7f, depth)
                };
                var line = new GameObject(cloth ? "Fabric tear" : "Wood fracture").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.useWorldSpace = false;
                line.positionCount = points.Length;
                line.SetPositions(points);
                line.startWidth = line.endWidth = cloth ? .035f : .018f;
                line.numCapVertices = 2;
                line.sharedMaterial = GameAssets.I.Tinted(cloth ? new Color(.93f, .86f, .67f) : new Color(.19f, .105f, .055f));
                if (cloth)
                    for (int tuft = 0; tuft < 3; tuft++)
                    {
                        var stuffing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        stuffing.name = "Exposed stuffing";
                        stuffing.transform.SetParent(transform, false);
                        stuffing.transform.localPosition = anchor + new Vector3(tuft * .055f - .05f, .025f, 0f);
                        stuffing.transform.localScale = new Vector3(.10f, .075f, .09f);
                        var collider = stuffing.GetComponent<Collider>();
                        collider.enabled = false;
                        Destroy(collider);
                        stuffing.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(new Color(1f, .94f, .80f));
                    }
            }
            stage = next;
        }
    }
}
