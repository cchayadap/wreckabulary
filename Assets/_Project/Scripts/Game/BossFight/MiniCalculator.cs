using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;
using Random = UnityEngine.Random;

namespace Wreckabulary.BossFight
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MiniCalculator : MonoBehaviour, IDamageable
    {
        const float HitRadius = .46f, BodyLift = .45f, TouchRange = .95f, SpawnGrace = .6f, BusyRetry = .25f, Recoil = 1.5f, Skin = .05f;
        const float TurnRate = 8f, TilePop = 4f, BarWidth = .68f, BarY = 1.02f;

        BossFightTuning tuning;
        Transform visual, hpFill, hpBack;
        float bobOffset, nextTouch, health, maxHealth;
        bool broken;
        readonly Dictionary<int, float> lastHitAt = new();
        readonly RaycastHit[] blockers = new RaycastHit[8];

        public bool Broken => broken;
        public Vector3 Position => transform.position;
        public float Health => health;
        public float MaxHealth => maxHealth;
        public Func<Vector3, bool> Inside { get; set; }

        public void Configure(BossFightTuning bossTuning, Vector3 at)
        {
            tuning = bossTuning ?? new BossFightTuning();
            transform.position = at;
            maxHealth = tuning.MiniHealth > 0f ? tuning.MiniHealth : 42f;
            health = maxHealth;
            bobOffset = Random.value * Mathf.PI * 2f;
            nextTouch = Time.time + SpawnGrace;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.mass = 4f;
            if (!TryGetComponent(out SphereCollider hitbox)) hitbox = gameObject.AddComponent<SphereCollider>();
            hitbox.center = new Vector3(0f, BodyLift, 0f);
            hitbox.radius = HitRadius;
            BuildVisual();
        }

        void BuildVisual()
        {
            if (visual) Destroy(visual.gameObject);
            var root = new GameObject("Mini calculator visual");
            root.transform.SetParent(transform, false);
            visual = root.transform;
            Piece(visual, "Shell", PrimitiveType.Cube, new Vector3(0f, .45f, 0f), new Vector3(.62f, .72f, .34f), 0xf5a623);
            Piece(visual, "Screen", PrimitiveType.Cube, new Vector3(0f, .53f, -.19f), new Vector3(.44f, .26f, .04f), 0x2c3e50);
            Piece(visual, "Eye L", PrimitiveType.Sphere, new Vector3(-.13f, .55f, -.23f), Vector3.one * .07f, 0x4ecdc4);
            Piece(visual, "Eye R", PrimitiveType.Sphere, new Vector3(.13f, .55f, -.23f), Vector3.one * .07f, 0x4ecdc4);
            Piece(visual, "Keys", PrimitiveType.Cube, new Vector3(0f, .24f, -.2f), new Vector3(.38f, .12f, .04f), 0xffd166);
            hpBack = Bar(visual, "HP back", new Vector3(0f, BarY, -.05f), new Vector3(.72f, .09f, .05f), new Color(.08f, .09f, .12f));
            hpFill = Bar(visual, "HP fill", new Vector3(0f, BarY, -.1f), new Vector3(BarWidth, .06f, .05f), new Color(.28f, .92f, .55f));
            RefreshHp();
        }

        static Transform Bar(Transform parent, string label, Vector3 at, Vector3 scale, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            Strip(go);
            go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(c);
            return go.transform;
        }

        static void Piece(Transform parent, string label, PrimitiveType type, Vector3 at, Vector3 scale, int hex)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            Strip(go);
            var colour = new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
            go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(colour);
        }

        static void Strip(GameObject go)
        {
            var shape = go.GetComponent<Collider>();
            if (!shape) return;
            shape.enabled = false;
            Destroy(shape);
        }

        void RefreshHp()
        {
            if (!hpFill || !hpBack) return;
            float ratio = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
            bool hurt = health > 0f && ratio < 1f;
            hpBack.gameObject.SetActive(hurt);
            hpFill.gameObject.SetActive(hurt);
            hpFill.localScale = new Vector3(BarWidth * ratio, .06f, .05f);
            hpFill.localPosition = new Vector3(-BarWidth * .5f * (1f - ratio), BarY, -.1f);
        }

        public void Tick(PlayerController target, float delta)
        {
            if (broken || delta <= 0f || !target || target.IsKnockedOut) return;
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance > .01f)
            {
                Vector3 flat = to / distance;
                Walk(flat * Mathf.Min(distance, tuning.MiniSpeed * delta));
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Mathf.Clamp01(TurnRate * delta));
            }
            if (visual)
            {
                visual.localPosition = Vector3.up * (.04f + Mathf.Sin(Time.time * 7f + bobOffset) * .05f);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 5f + bobOffset) * 5f);
            }
            if (distance <= TouchRange && Time.time >= nextTouch) Touch(target);
        }

        void Walk(Vector3 step)
        {
            float length = step.magnitude;
            if (length < .0001f) return;
            var dir = step / length;
            if (Blocked(dir, length, out var normal))
            {
                normal.y = 0f;
                var slide = Vector3.ProjectOnPlane(step, normal.sqrMagnitude > .0001f ? normal.normalized : -dir);
                length = slide.magnitude;
                if (length < .0001f || Blocked(slide / length, length, out _)) return;
                step = slide;
            }
            var next = transform.position + step;
            if (Inside != null && !Inside(next)) return;
            transform.position = next;
        }

        bool Blocked(Vector3 dir, float length, out Vector3 normal)
        {
            normal = Vector3.zero;
            var from = transform.position + Vector3.up * BodyLift;
            int mask = World.GroundMask & Physics.DefaultRaycastLayers;
            int count = Physics.SphereCastNonAlloc(from, HitRadius * .8f, dir, blockers, length + Skin, mask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var shape = blockers[i].collider;
                if (!shape || shape.transform.IsChildOf(transform) || shape.GetComponentInParent<LetterTile>()) continue;
                if (shape.GetComponentInParent<MiniCalculator>() || shape.GetComponentInParent<BossController>()) continue;
                if (blockers[i].distance >= nearest) continue;
                nearest = blockers[i].distance;
                normal = blockers[i].normal;
            }
            return nearest < float.MaxValue;
        }

        void Touch(PlayerController target)
        {
            if (target.IsStaggered || target.IsDodging || target.Health.IsInvulnerable)
            {
                nextTouch = Time.time + BusyRetry;
                return;
            }
            float now = Time.time;
            if (lastHitAt.TryGetValue(target.Index, out float at) && now - at < tuning.MiniHitCooldownSeconds) return;
            lastHitAt[target.Index] = now;
            Vector3 away = target.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < .001f) away = Vector3.forward;
            away.Normalize();
            target.Knock(away * tuning.MiniKnockback + Vector3.up * .7f, tuning.MiniStunSeconds);
            if (!target.Summoner.IsSpelling && !target.Summoner.IsCrafting)
                target.Inventory.DropRandom(1, target.transform.position, away);
            Popup.Show("BONK!", target.transform.position + Vector3.up * 1.7f, new Color(1f, .68f, .22f), 1f);
            GameFeedback.Play(GameCue.Block);
            GameFeedback.Burst("Impact_Star", target.transform.position + Vector3.up * 1.1f, .45f);
            Walk(-away * Recoil);
            nextTouch = now + tuning.MiniHitCooldownSeconds;
        }

        public bool ApplyDamage(in HitInfo hit)
        {
            if (broken || hit.Damage <= 0f) return false;
            float capped = Mathf.Min(hit.Damage, maxHealth * .5f);
            health = Mathf.Max(0f, health - capped);
            GameFeedback.Play(GameCue.Hit);
            GameFeedback.Burst("Impact_Star", transform.position + Vector3.up * .5f, .35f);
            if (health <= 0f) { Break(); return true; }
            RefreshHp();
            Popup.Show("MINI " + Mathf.CeilToInt(health), transform.position + Vector3.up * 1.25f, new Color(1f, .68f, .22f), .7f);
            return true;
        }

        void Break()
        {
            broken = true;
            var at = transform.position + Vector3.up * .5f;
            char letter = (char)('A' + Random.Range(0, 26));
            var pool = TilePool.Instance;
            if (pool) pool.Burst(letter.ToString(), at, TilePop);
            GameFeedback.Play(GameCue.Break);
            GameFeedback.Burst("Wood_Splinter", at, .5f, new Color(1f, .7f, .25f), .4f);
            Popup.Show("+" + letter, at + Vector3.up * .6f, new Color(1f, .78f, .35f), 1.1f);
            Destroy(gameObject);
        }
    }
}
