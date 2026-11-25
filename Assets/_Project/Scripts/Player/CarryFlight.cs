using UnityEngine;

namespace Wreckabulary
{
    public sealed class CarryFlight : MonoBehaviour
    {
        const float SettledSpeed = 1.5f;
        const float MinFlightSeconds = .15f;

        Rigidbody body;
        CollisionDetectionMode before;
        float startedAt, until;
        bool restored;

        public CollisionDetectionMode Before => before;
        public bool InFlight => enabled && !restored;

        public static void Attach(Rigidbody body, float seconds)
        {
            if (!body || body.isKinematic) return;
            if (!body.TryGetComponent(out CarryFlight flight))
            {
                flight = body.gameObject.AddComponent<CarryFlight>();
                flight.body = body;
                flight.restored = true;
            }
            if (flight.restored) flight.before = body.collisionDetectionMode;
            flight.restored = false;
            flight.enabled = true;
            flight.startedAt = Time.time;
            flight.until = Time.time + seconds;
            if (body.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public static void End(Rigidbody body)
        {
            if (body && body.TryGetComponent(out CarryFlight flight)) flight.Finish();
        }

        void FixedUpdate()
        {
            if (restored) return;
            if (!body || body.isKinematic || Time.time >= until) { Finish(); return; }
            if (Time.time - startedAt >= MinFlightSeconds && body.linearVelocity.sqrMagnitude < SettledSpeed * SettledSpeed) Finish();
        }

        public void Finish()
        {
            Restore();
            enabled = false;
        }

        void Restore()
        {
            if (restored) return;
            restored = true;
            if (body && body.collisionDetectionMode != before) body.collisionDetectionMode = before;
        }

        void OnDestroy() => Restore();
    }
}
