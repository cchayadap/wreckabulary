using UnityEngine;
using Wreckabulary.Rules;
using System.Linq;

namespace Wreckabulary
{
    /// <summary>Mostly fixed couch-game camera that drifts a little towards the action and shakes on big hits.</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        [SerializeField] float follow = 0.12f;

        Vector3 basePosition, smooth;
        float shake;
        bool framesLayout;
        Vector3 layoutCentre;
        float wholeHouseSize;
        float layoutWidth, layoutDepth;
        Camera lens;

        public void FrameLayout(HouseLayout layout)
        {
            var camera = lens = GetComponent<Camera>();
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            var centre = new Vector3((minX + maxX) * .5f, 0f, (minZ + maxZ) * .5f);
            layoutWidth = maxX - minX;
            layoutDepth = maxZ - minZ;
            camera.orthographic = true;
            float aspect = Mathf.Max(.5f, camera.aspect);
            camera.orthographicSize = Mathf.Max((maxX - minX) / aspect, (maxZ - minZ) * .85f) * .55f + 1.5f;
            layoutCentre = centre;
            wholeHouseSize = camera.orthographicSize;
            basePosition = smooth = centre + new Vector3(0f, 30f, -22f);
            transform.position = basePosition;
            transform.LookAt(centre);
            framesLayout = true;
        }

        void Awake()
        {
            Instance = this;
            basePosition = smooth = transform.position;
        }

        public static void Shake(float amount)
        {
            if (Instance) Instance.shake = Mathf.Max(Instance.shake, amount);
        }

        void LateUpdate()
        {
            var centre = Vector3.zero;
            int n = 0;
            int localCount = 0;
            PlayerController localPlayer = null;
            foreach (var p in World.Players)
            {
                if (!p || p.IsEliminated) continue;
                centre += p.transform.position;
                n++;
                if (p.Binding is not BotBinding) { localCount++; localPlayer = p; }
            }
            var target = basePosition;
            if (framesLayout)
            {
                // One local player needs readable action, even in the large house. Couch players share the full view.
                bool followLocal = localCount == 1;
                var focus = followLocal ? localPlayer.transform.position : layoutCentre;
                focus.y = 0f;
                target = focus + new Vector3(0f, 30f, -22f);
                if (lens)
                {
                    wholeHouseSize = Mathf.Max(layoutWidth / Mathf.Max(.5f, lens.aspect), layoutDepth * .85f) * .55f + 1.5f;
                    lens.orthographicSize = Mathf.Lerp(lens.orthographicSize,
                        followLocal ? Mathf.Min(wholeHouseSize, 8.5f) : wholeHouseSize,
                        1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
                }
            }
            else if (n > 0)
            {
                centre /= n;
                target += new Vector3(centre.x * follow, 0f, centre.z * follow * 0.6f);
            }
            smooth = Vector3.Lerp(smooth, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            transform.position = smooth + Random.insideUnitSphere * shake;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
        }
    }
}
