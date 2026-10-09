using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>An arrow from BOW or a cannonball from CANNON.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        PlayerController owner;
        string word;
        float damage, knockback, breakPower, blastRadius;
        bool spent;

        public static Projectile Fire(string word, Vector3 from, Vector3 velocity, PlayerController owner,
                                      float damage, float knockback, float breakPower, float blastRadius)
        {
            bool heavy = blastRadius > 0f;
            var size = heavy ? Vector3.one * 0.45f : new Vector3(0.12f, 0.12f, 0.7f);
            var go = new GameObject($"{word} shot");
            go.transform.SetParent(World.Transient, false);
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(velocity));
            LetterBlocks.Create(heavy ? "O" : "", size, GameAssets.I.Tinted(heavy ? new Color(0.25f, 0.24f, 0.28f) : new Color(0.55f, 0.36f, 0.2f)),
                                go.transform, true);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = heavy ? 3f : 0.3f;
            rb.useGravity = heavy;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;

            if (owner)
                foreach (var mine in owner.GetComponents<Collider>())
                    Physics.IgnoreCollision(go.GetComponentInChildren<Collider>(), mine);

            var p = go.AddComponent<Projectile>();
            p.owner = owner;
            p.word = word;
            p.damage = damage;
            p.knockback = knockback;
            p.breakPower = breakPower;
            p.blastRadius = blastRadius;
            Destroy(go, 3f);
            return p;
        }

        void OnCollisionEnter(Collision c)
        {
            if (spent) return;
            var rb = c.rigidbody;
            if (rb && rb.GetComponent<LetterTile>()) return;
            spent = true;

            if (blastRadius > 0f)
            {
                Explode();
            }
            else if (rb && rb.TryGetComponent(out IDamageable target))
            {
                target.ApplyDamage(Hits.Of(owner, transform.forward, HitSource.Thrown, damage, knockback, 0.2f, breakPower, word));
            }
            Destroy(gameObject);
        }

        void Explode()
        {
            ExplodeAt(transform.position, owner, word, damage, damage, blastRadius, knockback, breakPower, false);
        }

        public static float BlastDamage(float centreDamage, float edgeDamage, float distance, float radius) =>
            Mathf.Lerp(centreDamage, edgeDamage, radius > 0f ? Mathf.Clamp01(distance / radius) : 0f);

        public static void ExplodeAt(Vector3 position, PlayerController owner, string word, float damage, float edgeDamage,
            float blastRadius, float knockback, float breakPower, bool hurtOwner)
        {
            CameraRig.Shake(0.25f);
            GameFeedback.Play(GameCue.Blast);
            GameFeedback.Burst("Impact_Star", position + Vector3.up * .65f, Mathf.Min(2.2f, blastRadius), GameFeedback.SkillColor("BOMB"), .5f);
            Popup.Show("BOOM", position + Vector3.up, Color.white, 4f);
            var seen = new HashSet<Rigidbody>();
            foreach (var col in Physics.OverlapSphere(position, blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = col.attachedRigidbody;
                if (!rb || !seen.Add(rb) || FloorBetween(position, rb.worldCenterOfMass)) continue;
                float distance = World.Flat(rb.position - position).magnitude;
                var hit = Hits.Of(owner, rb.position - position, HitSource.Explosion, BlastDamage(damage, edgeDamage, distance, blastRadius), knockback, 0.3f, breakPower, word);
                if (rb.TryGetComponent(out PlayerHealth victim))
                {
                    if (hurtOwner || !owner || victim.gameObject != owner.gameObject) victim.ApplyDamage(hit);
                }
                else
                {
                    if (rb.TryGetComponent(out IDamageable target)) target.ApplyDamage(hit);
                    if (rb && !rb.isKinematic) rb.AddExplosionForce(knockback * Hits.KnockbackSpeed, position, blastRadius, 0.5f, ForceMode.VelocityChange);
                }
            }
        }

        static bool FloorBetween(Vector3 from, Vector3 to)
        {
            var path = from - to;
            float length = path.magnitude, low = Mathf.Min(from.y, to.y) + .1f, high = Mathf.Max(from.y, to.y) - .1f;
            if (length < .01f || high <= low) return false;
            foreach (var hit in Physics.RaycastAll(to, path / length, length, World.GroundMask, QueryTriggerInteraction.Ignore))
                if (!hit.rigidbody && Mathf.Abs(hit.normal.y) > .7f && hit.point.y > low && hit.point.y < high) return true;
            return false;
        }
    }
}
