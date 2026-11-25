using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.BossFight
{
    public sealed class BossController : MonoBehaviour, IDamageable
    {
        const float LeapHeight = 2.4f, LandSquashSeconds = .22f, PathRadius = .6f, PathLift = 1f;
        const float ShieldCueSeconds = 1.2f, CrackStagger = .9f, BreakStagger = 2.2f;
        static readonly Vector3 HitboxCentre = new(0f, .95f, 0f), HitboxSize = new(1.9f, 2.1f, 1.5f);

        BossFightTuning tuning;
        BossNameCatalogue names;
        float health, nextShieldCue, staggerUntil, leapAt, leapDuration, landUntil;
        bool revealed, leaping, popping;
        Vector3 leapFrom, leapTo, home;
        readonly List<LineRenderer> shieldRings = new();
        readonly RaycastHit[] pathHits = new RaycastHit[16];
        Transform visual;
        BoxCollider hitbox;

        public event Action<int> LayerCracked;
        public event Action ShieldBroken;
        public event Action<float> Damaged;

        public int LayersRemaining { get; private set; }
        public float Health => health;
        public float MaxHealth => tuning != null ? tuning.BossMaxHealth : 240f;
        public bool Vulnerable => LayersRemaining <= 0;
        public Vector3 Centre => transform.position + Vector3.up;
        public Vector3 Home => home;
        public bool IsLeaping => leaping;
        public bool LeapsOverWalls => leaping && !popping;
        public Vector3 LeapTarget => leapTo;

        public void Configure(BossFightTuning bossTuning, BossNameCatalogue catalogue, Vector3 at)
        {
            tuning = bossTuning ?? new BossFightTuning();
            names = catalogue ?? new BossNameCatalogue();
            home = at;
            transform.position = at;
            BuildVisual();
            ResetBoss();
        }

        public void MoveHome(Vector3 at)
        {
            home = at;
            if (!leaping) transform.position = at;
        }

        void BuildVisual()
        {
            if (!TryGetComponent(out Rigidbody body)) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.mass = 100f;
            if (!TryGetComponent(out hitbox)) hitbox = gameObject.AddComponent<BoxCollider>();
            hitbox.center = HitboxCentre;
            hitbox.size = HitboxSize;

            if (visual) Destroy(visual.gameObject);
            var root = new GameObject("Calculator visual");
            root.transform.SetParent(transform, false);
            visual = root.transform;

            Piece("Body", PrimitiveType.Cube, new Vector3(0f, .9f, 0f), new Vector3(1.6f, 1.8f, .9f), 0xf5a623);
            Piece("Screen", PrimitiveType.Cube, new Vector3(0f, 1.15f, -.47f), new Vector3(1.15f, .7f, .06f), 0x2c3e50);
            Piece("Eye L", PrimitiveType.Sphere, new Vector3(-.3f, 1.25f, -.55f), Vector3.one * .16f, 0x4ecdc4);
            Piece("Eye R", PrimitiveType.Sphere, new Vector3(.3f, 1.25f, -.55f), Vector3.one * .16f, 0x4ecdc4);
            Piece("Display", PrimitiveType.Cube, new Vector3(0f, .72f, -.53f), new Vector3(.95f, .22f, .05f), 0xffd166);
            Piece("Foot L", PrimitiveType.Cube, new Vector3(-.5f, .06f, -.15f), new Vector3(.4f, .16f, .6f), 0x2c3e50);
            Piece("Foot R", PrimitiveType.Cube, new Vector3(.5f, .06f, -.15f), new Vector3(.4f, .16f, .6f), 0x2c3e50);

            shieldRings.Clear();
            for (int i = 0; i < names.LayerCount; i++)
            {
                float radius = 1.5f - i * .22f;
                var ring = SummonEffects.Ring(transform, "Shield " + i,
                    i == 0 ? new Color(.98f, .65f, .14f) : new Color(.31f, .8f, .77f), radius, Quaternion.identity);
                ring.transform.localPosition = Vector3.up * .85f;
                shieldRings.Add(ring);
            }
        }

        void Piece(string label, PrimitiveType type, Vector3 at, Vector3 scale, int hex)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = label;
            go.transform.SetParent(visual, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            var colour = new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
            go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(colour);
            var shape = go.GetComponent<Collider>();
            if (shape)
            {
                shape.enabled = false;
                Destroy(shape);
            }
        }

        public void ResetBoss()
        {
            CancelLeap();
            transform.position = home;
            LayersRemaining = names != null ? names.LayerCount : BossFightTuning.Words.Count;
            health = MaxHealth;
            nextShieldCue = 0f;
            staggerUntil = 0f;
            revealed = true;
            if (visual)
            {
                visual.gameObject.SetActive(true);
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }
            if (hitbox) hitbox.enabled = true;
            RefreshRings();
        }

        public void SetVisible(bool visible)
        {
            revealed = visible;
            if (visual) visual.gameObject.SetActive(visible && health > 0f);
            if (hitbox) hitbox.enabled = visible && health > 0f;
            RefreshRings();
        }

        void RefreshRings()
        {
            for (int i = 0; i < shieldRings.Count; i++)
                if (shieldRings[i]) shieldRings[i].gameObject.SetActive(revealed && i < LayersRemaining);
        }

        public bool PathClear(Vector3 landing)
        {
            var from = transform.position + Vector3.up * PathLift;
            var to = landing + Vector3.up * PathLift;
            var delta = to - from;
            float distance = delta.magnitude;
            if (distance < .01f) return true;
            int mask = World.GroundMask & Physics.DefaultRaycastLayers;
            int count = Physics.SphereCastNonAlloc(from, PathRadius, delta / distance, pathHits, distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var shape = pathHits[i].collider;
                if (!shape || shape.transform.IsChildOf(transform)) continue;
                var body = shape.attachedRigidbody;
                if (body && !body.isKinematic) continue;
                if (body && body.GetComponentInParent<MiniCalculator>()) continue;
                return false;
            }
            return true;
        }

        public void LeapTo(Vector3 landing, float seconds)
        {
            if (seconds <= 0f) { transform.position = landing; landUntil = Time.time + LandSquashSeconds; return; }
            leapFrom = transform.position;
            leapTo = landing;
            leapAt = Time.time;
            leapDuration = seconds;
            landUntil = 0f;
            popping = !PathClear(landing);
            leaping = true;
        }

        public void CancelLeap()
        {
            if (leaping) transform.position = leapTo;
            leaping = false;
            popping = false;
            landUntil = 0f;
            if (visual) visual.localScale = Vector3.one;
        }

        void Update()
        {
            if (leaping)
            {
                Leap();
                return;
            }
            if (!visual) return;
            if (landUntil > Time.time)
            {
                float settle = Mathf.Clamp01((landUntil - Time.time) / LandSquashSeconds);
                visual.localScale = new Vector3(1f + (1f - settle) * .22f, 1f - (1f - settle) * .28f, 1f + (1f - settle) * .22f);
            }
            else if (visual.localScale != Vector3.one) visual.localScale = Vector3.one;

            if (staggerUntil > Time.time) visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 26f) * 8f);
            else if (visual.localRotation != Quaternion.identity) visual.localRotation = Quaternion.identity;
        }

        void Leap()
        {
            float t = Mathf.Clamp01((Time.time - leapAt) / Mathf.Max(.01f, leapDuration));
            if (popping) Pop(t);
            else Arc(t);
            if (t < 1f) return;
            transform.position = leapTo;
            leaping = false;
            popping = false;
            if (visual) visual.localScale = Vector3.one;
            landUntil = Time.time + LandSquashSeconds;
            GameFeedback.Burst("Impact_Star", Centre + Vector3.up * .1f, .9f);
            CameraRig.Shake(.18f);
        }

        void Arc(float t)
        {
            float arc = Mathf.Sin(t * Mathf.PI);
            var p = Vector3.Lerp(leapFrom, leapTo, t);
            p.y += arc * LeapHeight;
            transform.position = p;
            if (!visual) return;
            visual.localRotation = Quaternion.Euler(-arc * 14f, 0f, 0f);
            visual.localScale = new Vector3(1f - arc * .06f, 1f + arc * .18f, 1f - arc * .06f);
        }

        void Pop(float t)
        {
            bool second = t >= .5f;
            var at = second ? leapTo : leapFrom;
            if (transform.position != at)
            {
                transform.position = at;
                GameFeedback.Burst("Craft_Ring", Centre, .8f, new Color(1f, .74f, .25f), .4f);
            }
            float size = second ? (t - .5f) * 2f : 1f - t * 2f;
            if (visual) visual.localScale = Vector3.one * Mathf.Max(.02f, size);
        }

        public bool CrackLayer()
        {
            if (Vulnerable) return false;
            LayersRemaining--;
            RefreshRings();
            staggerUntil = Time.time + (Vulnerable ? BreakStagger : CrackStagger);
            GameFeedback.Play(GameCue.Break);
            GameFeedback.Burst("Wood_Splinter", Centre, .6f, new Color(1f, .82f, .4f), .5f);
            GameFeedback.Burst("Craft_Ring", Centre, Vulnerable ? 1.6f : .9f, new Color(1f, .74f, .25f), .6f);
            if (Vulnerable) GameFeedback.Play(GameCue.Blast);
            LayerCracked?.Invoke(LayersRemaining);
            if (Vulnerable) ShieldBroken?.Invoke();
            return true;
        }

        public bool ApplyDamage(in HitInfo hit)
        {
            if (health <= 0f) return false;
            if (!Vulnerable)
            {
                if (Time.time >= nextShieldCue)
                {
                    nextShieldCue = Time.time + ShieldCueSeconds;
                    Popup.Show("SHIELDED!", Centre + Vector3.up * .5f, new Color(1f, .78f, .35f), 1.1f);
                    GameFeedback.Play(GameCue.Protect);
                }
                return false;
            }
            if (hit.Damage <= 0f) return false;
            float final = Mathf.Max(1f, hit.Damage * tuning.BossVulnerability);
            health = Mathf.Max(0f, health - final);
            GameFeedback.Play(GameCue.Hit);
            GameFeedback.Burst("Impact_Star", Centre, .35f);
            Damaged?.Invoke(final);
            if (health <= 0f)
            {
                if (visual) visual.gameObject.SetActive(false);
                if (hitbox) hitbox.enabled = false;
            }
            return true;
        }
    }
}
