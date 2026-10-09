using System;
using UnityEngine;

namespace Wreckabulary
{
    public sealed class ShoulderView
    {
        public const float FieldOfView = 55f, NearClip = .1f;
        public const float Distance = 3.3f, MinDistance = .35f;
        public const float PivotHeight = 1f, Lift = .10f;
        public const float LookAhead = 12f;
        public const float ProbeRadius = .2f, Skin = .05f;
        public const float HeightEase = 12f, OutEase = 6f;
        public const float DefaultPitch = .16f, MinPitch = -.45f, MaxPitch = .95f;
        public const float MouseSensitivity = .0024f;
        public const float ClimbHeight = 1.2f, OpenAhead = 8f;
        public const float AimDistance = 1.5f, AimFieldOfView = 50f, AimShoulder = .45f, AimEase = 12f;

        static readonly RaycastHit[] hits = new RaycastHit[16];
        static readonly float[] turns = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
        float eyeY, distance = Distance, aim, climb;
        bool placed;

        public StoreyCutaway Environment { get; set; }
        public CameraCutaway Cutaway { get; set; }
        public float CurrentDistance => distance;
        public float Aim => aim;

        public static Vector3 Forward(float yaw, float pitch) =>
            new(Mathf.Sin(yaw) * Mathf.Cos(pitch), -Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));

        public static Vector2 CameraRelative(Vector2 move, float yaw)
        {
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            return new Vector2(c * move.x + s * move.y, -s * move.x + c * move.y);
        }

        public void Snap() => placed = false;

        public float Climb => climb;

        public static float OpenYaw(Vector3 feet, float yaw)
        {
            var origin = feet + Vector3.up * (PivotHeight + Lift);
            float best = yaw, score = float.NegativeInfinity;
            foreach (float turn in turns)
            {
                float y = yaw + turn * Mathf.Deg2Rad;
                var ahead = new Vector3(Mathf.Sin(y), 0f, Mathf.Cos(y));
                float behind = Probe(origin, -ahead, Distance);
                float s = Mathf.Min(behind, Distance - .01f) * 10f + Probe(origin, ahead, OpenAhead) - Mathf.Abs(turn) * .002f;
                if (s > score + .01f) { score = s; best = y; }
            }
            return best;
        }

        public static float Openness(Vector3 feet)
        {
            var origin = feet + Vector3.up * (PivotHeight + Lift);
            float sum = 0f;
            for (int i = 0; i < 8; i++)
            {
                float y = i * Mathf.PI * .25f;
                sum += Probe(origin, new Vector3(Mathf.Sin(y), 0f, Mathf.Cos(y)), Distance);
            }
            return sum;
        }

        public static Vector3 RoomySpot(Vector3 feet, Func<Vector3, bool> allowed, float radius = 1.5f, float step = .5f)
        {
            var best = feet;
            float score = Openness(feet);
            for (float dx = -radius; dx <= radius + .01f; dx += step)
                for (float dz = -radius; dz <= radius + .01f; dz += step)
                {
                    var at = feet + new Vector3(dx, 0f, dz);
                    float moved = new Vector2(dx, dz).magnitude;
                    if (moved < .01f || moved > radius + .01f || (allowed != null && !allowed(at))) continue;
                    var chest = feet + Vector3.up;
                    if (Physics.Linecast(chest, at + Vector3.up, World.GroundMask, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.CheckCapsule(at + Vector3.up * .45f, at + Vector3.up * 1.3f, .35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!Physics.Raycast(at + Vector3.up * .5f, Vector3.down, .8f, World.GroundMask, QueryTriggerInteraction.Ignore)) continue;
                    float s = Openness(at) - moved * .3f;
                    if (s > score + .01f) { score = s; best = at; }
                }
            return best;
        }

        static float Probe(Vector3 from, Vector3 direction, float reach)
        {
            float want = reach;
            int n = Physics.SphereCastNonAlloc(from, ProbeRadius, direction, hits, reach, World.GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!hits[i].rigidbody && !IsLegacyCutaway(hits[i].collider)) want = Mathf.Min(want, hits[i].distance - Skin);
            return Mathf.Max(0f, want);
        }

        // Older authored houses retain low visible walls and full-height gameplay colliders.
        public static bool IsLegacyCutaway(Collider collider) => collider &&
            !collider.GetComponent<TallWall>() && collider.TryGetComponent<Renderer>(out var renderer) &&
            renderer.bounds.size.y <= 1.35f && collider.bounds.size.y > renderer.bounds.size.y * 2f;

        float ProbeEnvironment(Vector3 from, Vector3 direction, float reach)
        {
            float available = Probe(from, direction, reach);
            if (Cutaway) return Cutaway.ProbeCeilings(from, direction, available, ProbeRadius);
            return Environment ? Environment.ProbeCeilings(from, direction, available, ProbeRadius) : available;
        }

        public void Place(Camera camera, Vector3 feet, float yaw, float pitch, float dt, bool aiming = false)
        {
            if (!placed)
            {
                eyeY = feet.y;
                distance = Distance;
                aim = aiming ? 1f : 0f;
                climb = 0f;
                placed = true;
            }
            eyeY = Mathf.Lerp(eyeY, feet.y, 1f - Mathf.Exp(-HeightEase * dt));
            aim = Mathf.Lerp(aim, aiming ? 1f : 0f, 1f - Mathf.Exp(-AimEase * dt));
            var forward = Forward(yaw, pitch);
            var pivot = new Vector3(feet.x, eyeY + PivotHeight, feet.z);
            var shoulder = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw)) * (AimShoulder * aim);
            var origin = pivot + Vector3.up * Lift + shoulder;

            float reach = Mathf.Lerp(Distance, AimDistance, aim);
            float minimumRise = Mathf.Max(0f, -forward.y) * MinDistance;
            float clearance = Mathf.Max(0f, ProbeEnvironment(origin, Vector3.up, ClimbHeight + minimumRise) - minimumRise);
            float wantClimb = ProbeEnvironment(origin, -forward, reach) < reach * .75f ? Mathf.Min(ClimbHeight, clearance) : 0f;
            climb = Mathf.Min(clearance, Mathf.Lerp(climb, wantClimb, 1f - Mathf.Exp(-OutEase * dt)));
            origin += Vector3.up * climb;
            float want = Mathf.Max(MinDistance, ProbeEnvironment(origin, -forward, reach));
            distance = want < distance ? want : Mathf.Lerp(distance, want, 1f - Mathf.Exp(-OutEase * dt));

            var position = origin - forward * distance;
            position.y = Mathf.Max(position.y, eyeY + MinDistance);
            var t = camera.transform;
            t.SetPositionAndRotation(position, Quaternion.LookRotation(pivot + forward * LookAhead - position));
            camera.orthographic = false;
            camera.fieldOfView = Mathf.Lerp(FieldOfView, AimFieldOfView, aim);
            camera.nearClipPlane = NearClip;
        }
    }
}
