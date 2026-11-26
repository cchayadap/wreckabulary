using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [RequireComponent(typeof(Rigidbody))]
    public class Smashable : MonoBehaviour, IDamageable
    {
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
        public float Remaining => IsBroken ? 0f : Mathf.Clamp01(health / Mathf.Max(1f, initialHealth));
        public bool Invulnerable { get; set; }
        public event Action<Smashable> Broken;
        public static event Action<Smashable> AnyBroken;
        public bool DropsWhole { get; set; }
        public string MustDrop { get; set; }
        public string Dropped { get; private set; } = "";
        public static System.Random DropRandom { get; set; } = new System.Random();
        static readonly List<Func<IEnumerable<string>>> dropGoals = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDrops()
        {
            dropGoals.Clear();
            DropRandom = new System.Random();
        }

        public static void AddDropGoals(Func<IEnumerable<string>> goals)
        {
            if (goals != null && !dropGoals.Contains(goals)) dropGoals.Add(goals);
        }

        public static void RemoveDropGoals(Func<IEnumerable<string>> goals) => dropGoals.Remove(goals);

        float graceUntil;
        float initialHealth;

        void OnEnable()
        {
            graceUntil = Time.time + spawnGrace;
            initialHealth = health;
        }

        void Start()
        {
            if (!TryGetComponent(out SmashableLabel _)) gameObject.AddComponent<SmashableLabel>();
        }

        public void Init(string newWord, float newHealth = -1f)
        {
            word = newWord.ToUpperInvariant();
            if (newHealth > 0f) health = newHealth;
            initialHealth = health;
        }

        public int LastAttacker { get; private set; } = -1;

        public bool ApplyDamage(in HitInfo hit)
        {
            if (IsBroken || Invulnerable || hit.BreakPower <= 0f) return false;
            if (hit.AttackerId >= 0) LastAttacker = hit.AttackerId;
            TakeHit(hit.BreakPower * HealthPerBreakPower);
            return true;
        }

        public void TakeHit(float damage)
        {
            if (IsBroken || Invulnerable) return;
            if (damage <= 0f) return;
            Popup.Damage(Mathf.Min(health, damage), transform.position + Vector3.up * 1.25f);
            health -= damage;
            if (health <= 0f) Break();
            else
            {
                Art.FurnitureDamageView.Show(this, health / Mathf.Max(1f, initialHealth));
                GameFeedback.Burst("Impact_Star", transform.position + Vector3.up * .55f, .35f);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time < graceUntil) return;
            var other = c.rigidbody;
            if (other && other.GetComponent<LetterTile>()) return;
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
            var drop = DropIndices();
            Dropped = LetterDrops.Letters(word, drop);
            if (pool)
            {
                var centre = TryGetComponent(out Rigidbody rb) ? rb.worldCenterOfMass : transform.position;
                if (TryGetComponent(out LetterBuilt built) && built.Blocks.Count == word.Length)
                    pool.BurstFrom(built.Blocks, word, drop, centre);
                else
                    pool.Burst(Dropped, centre + Vector3.up * 0.3f);
            }
            CameraRig.Shake(0.08f);
            if (!Audio.ImpactAudioBridge.VoicesBreak(this)) GameFeedback.Play(GameCue.Break);
            GameFeedback.Burst("Wood_Splinter", transform.position + Vector3.up * 0.4f, 0.65f);
            GameFeedback.Burst("Wood_Splinter", transform.position + new Vector3(.25f, .7f, .15f), .4f);
            if (word is "SOFA" or "BED" or "RUG" or "MAT")
                GameFeedback.Burst("Foam_Cloud", transform.position + Vector3.up * .55f, .7f);
            Broken?.Invoke(this);
            AnyBroken?.Invoke(this);
            Destroy(gameObject);
        }

        public int[] DropIndices()
        {
            var tuning = GameConfig.Current.Tuning?.Drops ?? LetterDropTuning.Default;
            if (DropsWhole || TryGetComponent(out SummonedThing _) || !tuning.Partial(Match.Mode)) return LetterDrops.All(word.Length);
            var needed = SeatNeeds();
            LetterDrops.Require(needed, MustDrop);
            return LetterDrops.Pick(word, tuning, DropRandom, needed);
        }

        static Dictionary<char, int> SeatNeeds()
        {
            var needed = new Dictionary<char, int>();
            var shared = SharedGoals();
            bool anyone = false;
            foreach (var p in World.Players)
            {
                if (!p) continue;
                anyone = true;
                var own = p.Summoner ? p.Summoner.Objectives : null;
                var goals = own != null ? new List<string>(own) : new List<string>();
                goals.AddRange(shared);
                LetterDrops.RequireFor(needed, goals, p.Inventory ? p.Inventory.Letters : null);
            }
            if (!anyone) LetterDrops.RequireFor(needed, shared, null);
            return needed;
        }

        static List<string> SharedGoals()
        {
            var words = new List<string>();
            foreach (var source in dropGoals.ToArray())
            {
                var goals = source();
                if (goals != null) words.AddRange(goals);
            }
            return words;
        }
    }
}
