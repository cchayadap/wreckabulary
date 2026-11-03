using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Audio
{
    [DisallowMultipleComponent]
    public sealed class SurfaceAudioTag : MonoBehaviour
    {
        public SurfaceMaterial material = SurfaceMaterial.Wood;

        public static SurfaceMaterial Resolve(Component hit)
        {
            if (!hit) return SurfaceMaterial.Wood;
            var tag = hit.GetComponentInParent<SurfaceAudioTag>();
            if (tag) return tag.material;
            if (hit.GetComponentInParent<PlayerHealth>()) return SurfaceMaterial.Body;
            var body = hit.GetComponentInParent<Rigidbody>();
            return SurfaceMaterials.ForWord(body ? body.name : hit.name);
        }
    }
}
