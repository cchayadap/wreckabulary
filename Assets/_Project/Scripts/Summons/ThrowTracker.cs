using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public class ThrowTracker : MonoBehaviour
    {
        PlayerController thrower;
        float until;
        float knockback;

        const float DamagePerMomentum = 0.6f, MinDamage = 5f, MaxDamage = 20f;

        public static void Attach(GameObject go, PlayerController thrower, float seconds, float knockback = 4f)
        {
            if (!go.TryGetComponent(out ThrowTracker t)) t = go.AddComponent<ThrowTracker>();
            t.thrower = thrower;
            t.until = Time.time + seconds;
            t.knockback = knockback;
        }

        void Update()
        {
            if (Time.time > until) Destroy(this);
        }

        void OnCollisionEnter(Collision c)
        {
            var rb = c.rigidbody;
            if (!rb || c.relativeVelocity.magnitude < 4f) return;
            if (!rb.TryGetComponent(out PlayerHealth victim)) return;
            if (thrower && victim.gameObject == thrower.gameObject) return;
            if (victim.gameObject == gameObject) return;

            float mass = TryGetComponent(out Rigidbody self) ? self.mass : 1f;
            float damage = Mathf.Clamp(DamagePerMomentum * mass * c.relativeVelocity.magnitude, MinDamage, MaxDamage);
            victim.ApplyDamage(Hits.Of(thrower, rb.position - transform.position, HitSource.Thrown, damage, knockback, 0.3f));
            Destroy(this);
        }
    }
}
