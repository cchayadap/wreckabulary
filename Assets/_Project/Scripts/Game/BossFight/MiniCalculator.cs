using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.BossFight
{
    /// <summary>
    /// A tiny calculator that waddles after the player. Touching never removes the player's HP:
    /// it only knocks back, stuns very briefly and shakes one letter loose. Each mini can hit a
    /// given player once per cooldown, recoils after contact, and refuses to hit an already-staggered
    /// player, so repeated contact can never chain-stun the player. Minis now have their own health
    /// bar and take several melee hits; breaking one yields a single random A-Z letter, never CALC.
    /// </summary>
    // @trace AMD-008, AMD-014
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MiniCalculator : MonoBehaviour, IDamageable
    {
        GameRules rules;
        Transform visual, hpFill, hpBack;
        float bobOffset;
        float nextTouch;
        float health, maxHealth;
        bool broken;
        readonly Dictionary<int, float> lastHitAt = new();

        public bool Broken => broken;
        public Vector3 Position => transform.position;
        /// <summary>Mini's own health, shown on its bar. Capped damage per swing guarantees several hits.</summary>
        // @trace AMD-014
        public float Health => health;
        public float MaxHealth => maxHealth;

        public void Configure(GameRules gameRules, Vector3 at)
        {
            rules = gameRules;
            transform.position = at;
            maxHealth = rules != null && rules.MiniHealth > 0f ? rules.MiniHealth : 42f;
            health = maxHealth;
            bobOffset = Random.value * Mathf.PI * 2f;
            nextTouch = Time.time + .6f;

            // PlayerCombat.Strike ignores colliders without a Rigidbody - same lesson as the boss.
            // @trace AMD-001, AMD-008
            if (!TryGetComponent(out Rigidbody body))
            {
                body = gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.mass = 4f;
            }
            body.isKinematic = true;
            body.useGravity = false;
            if (!TryGetComponent(out SphereCollider hitbox)) hitbox = gameObject.AddComponent<SphereCollider>();
            hitbox.center = new Vector3(0f, .45f, 0f);
            hitbox.radius = .46f;

            BuildVisual();
        }

        void BuildVisual()
        {
            if (visual) Destroy(visual.gameObject);
            var root = new GameObject("Mini calculator visual");
            root.transform.SetParent(transform, false);
            visual = root.transform;

            Piece(root.transform, "Shell", PrimitiveType.Cube, new Vector3(0f, .45f, 0f),
                new Vector3(.62f, .72f, .34f), 0xf5a623);
            Piece(root.transform, "Screen", PrimitiveType.Cube, new Vector3(0f, .53f, -.19f),
                new Vector3(.44f, .26f, .04f), 0x2c3e50);
            Piece(root.transform, "Eye L", PrimitiveType.Sphere, new Vector3(-.13f, .55f, -.23f),
                Vector3.one * .07f, 0x4ecdc4);
            Piece(root.transform, "Eye R", PrimitiveType.Sphere, new Vector3(.13f, .55f, -.23f),
                Vector3.one * .07f, 0x4ecdc4);
            Piece(root.transform, "Keys", PrimitiveType.Cube, new Vector3(0f, .24f, -.2f),
                new Vector3(.38f, .12f, .04f), 0xffd166);
            hpBack = Bar(root.transform, "HP back", new Vector3(0f, 1.02f, -.05f),
                new Vector3(.72f, .09f, .05f), new Color(.08f, .09f, .12f));
            hpFill = Bar(root.transform, "HP fill", new Vector3(0f, 1.02f, -.1f),
                new Vector3(.68f, .06f, .05f), new Color(.28f, .92f, .55f));
            RefreshHp();
        }

        static Transform Bar(Transform parent, string name, Vector3 at, Vector3 scale, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(c);
            return go.transform;
        }

        void RefreshHp()
        {
            if (!hpFill || !hpBack) return;
            float ratio = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
            bool hurt = health > 0f && ratio < 1f;
            hpBack.gameObject.SetActive(hurt);
            hpFill.gameObject.SetActive(hurt);
            hpFill.localScale = new Vector3(.68f * ratio, .06f, .05f);
            hpFill.localPosition = new Vector3(-.34f * (1f - ratio), 1.02f, -.1f);
        }

        static void Piece(Transform parent, string name, PrimitiveType type, Vector3 at, Vector3 scale, int hex)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider) Destroy(collider);
            ColorUtility.TryParseHtmlString("#" + hex.ToString("X6"), out Color c);
            go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(c);
        }

        /// <summary>Called by the director so the mode controls when minis are active.</summary>
        public void Tick(PlayerController target, float delta)
        {
            if (broken || rules == null || delta <= 0f || !target || target.IsKnockedOut) return;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance > .01f)
            {
                Vector3 flat = to / distance;
                transform.position += flat * (rules.MiniSpeed * delta);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat),
                    Mathf.Clamp01(8f * delta));
            }
            if (visual)
            {
                visual.localPosition = Vector3.up * (.04f + Mathf.Sin(Time.time * 7f + bobOffset) * .05f);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 5f + bobOffset) * 5f);
            }

            if (distance <= .95f && Time.time >= nextTouch) Touch(target);
        }

        void Touch(PlayerController target)
        {
            // Three independent guards against stun-lock:
            // 1) never touch an already-staggered or dodging player,
            // 2) one hit per player per cooldown on this mini,
            // 3) the mini recoils and cannot touch anyone during the cooldown.
            // @trace AMD-008
            if (target.IsStaggered || target.IsDodging || target.Health.IsInvulnerable)
            {
                nextTouch = Time.time + .25f;
                return;
            }
            float now = Time.time;
            if (lastHitAt.TryGetValue(target.Index, out float at) && now - at < rules.MiniHitCooldownSeconds)
                return;

            lastHitAt[target.Index] = now;
            Vector3 away = target.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < .001f) away = Vector3.forward;
            away.Normalize();

            // No HP damage. Knock() only applies velocity + a brief stagger.
            target.Knock(away * rules.MiniKnockback + Vector3.up * .7f, rules.MiniStunSeconds);
            if (!target.Summoner.IsSpelling && !target.Summoner.IsCrafting)
                target.Inventory.DropRandom(1, target.transform.position, away);
            Popup.Show("BONK!", target.transform.position + Vector3.up * 1.7f,
                new Color(1f, .68f, .22f), 1f);
            GameFeedback.Play(GameCue.Block);
            GameFeedback.Burst("Impact_Star", target.transform.position + Vector3.up * 1.1f, .45f);

            transform.position -= away * 1.5f; // recoil so it cannot immediately touch again
            nextTouch = now + rules.MiniHitCooldownSeconds;
        }

        /// <summary>Minis have real HP; damage per swing is capped so no single hit destroys one.</summary>
        // @trace AMD-014
        public bool ApplyDamage(in HitInfo hit)
        {
            if (broken || hit.Damage <= 0f) return false;
            float capped = Mathf.Min(hit.Damage, maxHealth * .5f);
            health = Mathf.Max(0f, health - capped);
            GameFeedback.Play(GameCue.Hit);
            GameFeedback.Burst("Impact_Star", transform.position + Vector3.up * .5f, .35f);
            if (health <= 0f) { Break(); return true; }
            RefreshHp();
            Popup.Show("MINI " + Mathf.CeilToInt(health), transform.position + Vector3.up * 1.25f,
                new Color(1f, .68f, .22f), .7f);
            return true;
        }

        /// <summary>Breaking a mini drops ONE random A-Z letter; it must never hand out CALC.</summary>
        // @trace AMD-014
        void Break()
        {
            broken = true;
            var at = transform.position + Vector3.up * .5f;
            char letter = (char)('A' + Random.Range(0, 26));
            var pool = TilePool.Instance;
            if (pool) pool.Burst(letter.ToString(), at, 4f);
            GameFeedback.Play(GameCue.Break);
            GameFeedback.Burst("Wood_Splinter", at, .5f, new Color(1f, .7f, .25f), .4f);
            Popup.Show("+" + letter, at + Vector3.up * .6f, new Color(1f, .78f, .35f), 1.1f);
            Destroy(gameObject);
        }
    }
}
