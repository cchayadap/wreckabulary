using System;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Anything built from letters: furniture and summoned objects alike.
    /// When it breaks, it bursts into the tiles that spell its word.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Smashable : MonoBehaviour, IDamageable
    {
        /// <summary>
        /// Furniture health one point of break power takes off: a punch (1) still does the old
        /// punch's 12. Stands in until furniture toughness comes from the letter economy.
        /// </summary>
        public const float HealthPerBreakPower = 12f;

        [SerializeField] string word = "TABLE";
        [SerializeField] float health = 30f;
        [Tooltip("Impacts only hurt when something was thrown (by a player, or falling in the collapse). Below this impulse they do nothing.")]
        [SerializeField] float impactThreshold = 6f;
        [SerializeField] float impactDamageScale = 2.5f;
        [Tooltip("Seconds after spawning when impacts do no damage, so dropped boxes survive landing.")]
        [SerializeField] float spawnGrace = 1.5f;

        public string Word => word;
        public float Health => health;
        public bool IsBroken { get; private set; }
        /// <summary>Placed Moving Day furniture can't be wrecked.</summary>
        public bool Invulnerable { get; set; }
        public event Action<Smashable> Broken;

        float graceUntil;

        void OnEnable() => graceUntil = Time.time + spawnGrace;

        public void Init(string newWord, float newHealth = -1f)
        {
            word = newWord.ToUpperInvariant();
            if (newHealth > 0f) health = newHealth;
        }

        /// <summary>A hit from a player, a weapon or a blast. Furniture feels break power, not damage.</summary>
        public bool ApplyDamage(in HitInfo hit)
        {
            if (IsBroken || Invulnerable || hit.BreakPower <= 0f) return false;
            TakeHit(hit.BreakPower * HealthPerBreakPower);
            return true;
        }

        public void TakeHit(float damage)
        {
            if (IsBroken || Invulnerable) return;
            health -= damage;
            if (health <= 0f) Break();
        }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time < graceUntil) return;
            var other = c.rigidbody;
            if (other && other.GetComponent<LetterTile>()) return;
            // Only throws do damage, so furniture doesn't wreck itself by toppling or boxes landing on each other.
            bool thrown = GetComponent<ThrowTracker>() || (other && other.GetComponent<ThrowTracker>());
            if (!thrown) return;
            float impulse = c.impulse.magnitude;
            if (impulse > impactThreshold) TakeHit((impulse - impactThreshold) * impactDamageScale);
        }

        public void Break()
        {
            if (IsBroken) return;
            IsBroken = true;

            var pool = TilePool.Instance;
            if (pool)
            {
                var centre = TryGetComponent(out Rigidbody rb) ? rb.worldCenterOfMass : transform.position;
                if (TryGetComponent(out LetterBuilt built) && built.Blocks.Count == word.Length)
                    pool.BurstFrom(built.Blocks, word, centre);
                else
                    pool.Burst(word, centre + Vector3.up * 0.3f);
            }
            CameraRig.Shake(0.08f);
            GameFeedback.Play(GameCue.Break);
            GameFeedback.Burst("Wood_Splinter", transform.position + Vector3.up * 0.4f, 0.65f);
            Broken?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
