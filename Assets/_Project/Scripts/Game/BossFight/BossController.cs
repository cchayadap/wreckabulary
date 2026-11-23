using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.BossFight
{
    /// <summary>
    /// The Calculator. Three shield layers gate all damage; once they are spelled away the
    /// existing melee/thrown pipeline can hurt it through IDamageable.
    /// </summary>
    // @trace MECH-003, MECH-009, NUM-001, NUM-002, ART-002, ART-001, AUD-001
    public sealed class BossController : MonoBehaviour, IDamageable
    {
        GameRules rules;
        BossNameCatalogue names;
        float health;
        float nextShieldCue;
        float staggerUntil;
        float leapAt, leapDuration, landUntil;
        bool revealed, leaping;
        Vector3 leapFrom, leapTo, home;
        const float LeapHeight = 2.4f;
        const float LandSquashSeconds = .22f;
        readonly List<LineRenderer> shieldRings = new();
        Transform visual;

        public event Action<int> LayerCracked;
        public event Action ShieldBroken;
        public event Action<float> Damaged;

        public int LayersRemaining { get; private set; }
        public float Health => health;
        public float MaxHealth => rules != null ? rules.BossMaxHealth : 240f;
        public bool Vulnerable => LayersRemaining <= 0;
        public Vector3 Centre => transform.position + Vector3.up;
        /// <summary>True while the boss is airborne towards a jump-smash landing spot.</summary>
        // @trace AMD-012
        public bool IsLeaping => leaping;
        public Vector3 LeapTarget => leapTo;

        public void Configure(GameRules gameRules, BossNameCatalogue catalogue, Vector3 at)
        {
            rules = gameRules;
            names = catalogue;
            home = at;
            transform.position = at;
            BuildVisual();
            ResetBoss();
        }

        void BuildVisual()
        {
            // PlayerCombat.Strike only considers colliders with a Rigidbody. Without this the
            // boss looks hittable but every swing is skipped at `rb == null`.
            // @trace MECH-009, AMD-001
            if (!GetComponent<Rigidbody>())
            {
                var body = gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.mass = 100f;
            }
            if (!GetComponent<Collider>())
            {
                var hitbox = gameObject.AddComponent<BoxCollider>();
                hitbox.center = new Vector3(0f, .95f, 0f);
                hitbox.size = new Vector3(1.9f, 2.1f, 1.5f);
            }

            if (visual) Destroy(visual.gameObject);
            var root = new GameObject("Calculator visual");
            root.transform.SetParent(transform, false);
            visual = root.transform;

            Piece("Body", PrimitiveType.Cube, new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 1.8f, 0.9f), 0xf5a623);
            Piece("Screen", PrimitiveType.Cube, new Vector3(0f, 1.15f, -0.47f), new Vector3(1.15f, .7f, .06f), 0x2c3e50);
            Piece("Eye L", PrimitiveType.Sphere, new Vector3(-.3f, 1.25f, -.55f), Vector3.one * .16f, 0x4ecdc4);
            Piece("Eye R", PrimitiveType.Sphere, new Vector3(.3f, 1.25f, -.55f), Vector3.one * .16f, 0x4ecdc4);
            Piece("Display", PrimitiveType.Cube, new Vector3(0f, .72f, -.53f), new Vector3(.95f, .22f, .05f), 0xffd166);
            Piece("Foot L", PrimitiveType.Cube, new Vector3(-.5f, .06f, -.15f), new Vector3(.4f, .16f, .6f), 0x2c3e50);
            Piece("Foot R", PrimitiveType.Cube, new Vector3(.5f, .06f, -.15f), new Vector3(.4f, .16f, .6f), 0x2c3e50);

            for (int i = 0; i < 3; i++)
            {
                float radius = 1.5f - i * .22f;
                var ring = SummonEffects.Ring(transform, "Shield " + i,
                    i == 0 ? new Color(.98f, .65f, .14f) : new Color(.31f, .8f, .77f), radius, Quaternion.identity);
                ring.transform.localPosition = Vector3.up * .85f;
                shieldRings.Add(ring);
            }
        }

        void Piece(string name, PrimitiveType type, Vector3 at, Vector3 scale, int hex)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(visual, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex.ToString("X6"), out c);
            go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(c);
            if (type != PrimitiveType.Cube) Destroy(go.GetComponent<Collider>());
        }

        public void ResetBoss()
        {
            CancelLeap();
            transform.position = home;
            LayersRemaining = names != null ? names.LayerCount : 3;
            health = MaxHealth;
            nextShieldCue = 0f;
            staggerUntil = 0f;
            revealed = true;
            if (visual)
            {
                visual.gameObject.SetActive(true);
                visual.localRotation = Quaternion.identity;
            }
            RefreshRings();
        }

        /// <summary>Preparation hides the boss model AND its name/shield rings until the countdown ends.</summary>
        // @trace AMD-006
        public void SetVisible(bool visible)
        {
            revealed = visible;
            if (visual) visual.gameObject.SetActive(visible);
            var hitbox = GetComponent<Collider>();
            if (hitbox) hitbox.enabled = visible;
            RefreshRings();
        }

        void RefreshRings()
        {
            for (int i = 0; i < shieldRings.Count; i++)
                if (shieldRings[i]) shieldRings[i].gameObject.SetActive(revealed && i < LayersRemaining);
        }

        /// <summary>Real leap: an arced hop towards the telegraphed landing spot, then a squash on impact.</summary>
        // @trace AMD-012
        public void LeapTo(Vector3 landing, float seconds)
        {
            if (seconds <= 0f) { transform.position = landing; landUntil = Time.time + LandSquashSeconds; return; }
            leapFrom = transform.position;
            leapTo = landing;
            leapAt = Time.time;
            leapDuration = seconds;
            landUntil = 0f;
            leaping = true;
        }

        public void CancelLeap()
        {
            if (leaping) transform.position = leapTo; // never freeze the boss mid-air
            leaping = false;
            landUntil = 0f;
            if (visual) visual.localScale = Vector3.one;
        }

        void Update()
        {
            if (leaping)
            {
                float t = Mathf.Clamp01((Time.time - leapAt) / Mathf.Max(.01f, leapDuration));
                float arc = Mathf.Sin(t * Mathf.PI);
                var p = Vector3.Lerp(leapFrom, leapTo, t);
                p.y += arc * LeapHeight;
                transform.position = p;
                if (visual)
                {
                    visual.localRotation = Quaternion.Euler(-arc * 14f, 0f, 0f);
                    visual.localScale = new Vector3(1f - arc * .06f, 1f + arc * .18f, 1f - arc * .06f);
                }
                if (t >= 1f)
                {
                    transform.position = leapTo;
                    leaping = false;
                    landUntil = Time.time + LandSquashSeconds;
                    GameFeedback.Burst("Impact_Star", Centre + Vector3.up * .1f, .9f);
                    CameraRig.Shake(.18f);
                }
                return;
            }

            if (!visual) return;
            if (landUntil > Time.time)
            {
                float settle = Mathf.Clamp01((landUntil - Time.time) / LandSquashSeconds);
                visual.localScale = new Vector3(1f + (1f - settle) * .22f,
                    1f - (1f - settle) * .28f, 1f + (1f - settle) * .22f);
            }
            else if (visual.localScale != Vector3.one) visual.localScale = Vector3.one;

            if (staggerUntil > Time.time)
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 26f) * 8f);
            else if (visual.localRotation != Quaternion.identity)
                visual.localRotation = Quaternion.identity;
        }

        /// <summary>Called by the director when the player spells the current shield word.</summary>
        // @trace MECH-003, AMD-009
        public bool CrackLayer()
        {
            if (Vulnerable || LayersRemaining <= 0) return false;
            LayersRemaining--;
            RefreshRings();
            staggerUntil = Time.time + (Vulnerable ? 2.2f : .9f);
            GameFeedback.Play(GameCue.Break);
            GameFeedback.Burst("Wood_Splinter", Centre, .6f, new Color(1f, .82f, .4f), .5f);
            GameFeedback.Burst("Craft_Ring", Centre, Vulnerable ? 1.6f : .9f,
                new Color(1f, .74f, .25f), .6f);
            if (Vulnerable)
            {
                GameFeedback.Play(GameCue.Blast);
                Popup.Show("SHIELD BROKEN!", Centre + Vector3.up * 1.1f,
                    new Color(1f, .72f, .2f), 2f);
            }
            else Popup.Show("LAYER " + LayersRemaining + " DOWN!", Centre + Vector3.up * .8f,
                new Color(1f, .82f, .4f), 1.1f);
            LayerCracked?.Invoke(LayersRemaining);
            if (Vulnerable) ShieldBroken?.Invoke();
            return true;
        }

        /// <summary>Existing melee and thrown hits arrive here; shielded hits are refused.</summary>
        public bool ApplyDamage(in HitInfo hit)
        {
            if (!Vulnerable)
            {
                if (Time.time >= nextShieldCue)
                {
                    nextShieldCue = Time.time + 1.2f;
                    Popup.Show("SHIELDED!", Centre + Vector3.up * .5f, new Color(1f, .78f, .35f), 1.1f);
                    GameFeedback.Play(GameCue.Protect);
                }
                return false;
            }
            if (hit.Damage <= 0f) return false;
            float final = Mathf.Max(1f, hit.Damage * rules.BossVulnerability);
            health = Mathf.Max(0f, health - final);
            GameFeedback.Play(GameCue.Hit);
            GameFeedback.Burst("Impact_Star", Centre, .35f);
            Damaged?.Invoke(final);
            if (health <= 0f) visual.gameObject.SetActive(false);
            return true;
        }
    }
}
