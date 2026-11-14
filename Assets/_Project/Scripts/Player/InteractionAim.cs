using UnityEngine;

namespace Wreckabulary
{
    public static class InteractionAim
    {
        static Camera fallback;
        static readonly RaycastHit[] hits = new RaycastHit[64];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => fallback = null;

        public static bool TryRay(PlayerController player, out Ray ray)
        {
            ray = default;
            if (!player || (!player.ShooterView && !player.Commands.aimAtPointer)) return false;
            var camera = CameraRig.Instance ? CameraRig.Instance.ViewCamera : fallback;
            if (!camera) camera = fallback = Camera.main;
            if (!camera) return false;
            ray = player.ShooterView ? camera.ViewportPointToRay(new Vector3(.5f, .5f))
                : camera.ScreenPointToRay(player.Commands.pointer);
            return true;
        }

        public static bool Trace(PlayerController player, Ray ray, out RaycastHit nearest, float distance = 50f)
        {
            int count = Physics.RaycastNonAlloc(ray, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var results = hits;
            if (count == hits.Length) { results = Physics.RaycastAll(ray, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore); count = results.Length; }
            nearest = default;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = results[i];
                if (Ignored(player, hit.collider) || hit.distance >= closest) continue;
                closest = hit.distance; nearest = hit;
            }
            return nearest.collider;
        }

        public static bool Ignored(PlayerController player, Collider collider)
        {
            if (!collider || collider.transform.IsChildOf(player.transform)) return true;
            var body = collider.attachedRigidbody;
            return body && (body == player.Body || body == player.Combat.Held || body.GetComponent<LetterTile>());
        }

        public static Vector3 LaunchVelocity(Vector3 delta, float speed, bool gravity)
        {
            if (delta.sqrMagnitude < .0001f) return Vector3.forward * speed;
            var flat = World.Flat(delta);
            float range = flat.magnitude;
            float g = -Physics.gravity.y;
            if (!gravity || g <= 0f || range < .01f) return delta.normalized * speed;
            float squared = speed * speed;
            float discriminant = squared * squared - g * (g * range * range + 2f * delta.y * squared);
            if (discriminant < 0f) return delta.normalized * speed;
            float slope = (squared - Mathf.Sqrt(discriminant)) / (g * range);
            float horizontal = speed / Mathf.Sqrt(1f + slope * slope);
            return flat / range * horizontal + Vector3.up * (horizontal * slope);
        }
    }
}
