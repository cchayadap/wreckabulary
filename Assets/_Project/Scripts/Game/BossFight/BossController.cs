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

        public void Configure(GameRules gameRules, BossNameCatalogue catalogue, Vector3 at)
        {
            rules = gameRules;
            names = catalogue;
            transform.position = at;
            BuildVisual();
            ResetBoss();
        }

        void BuildVisual()
        {
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
            LayersRemaining = names != null ? names.LayerCount : 3;
            health = MaxHealth;
            for (int i = 0; i < shieldRings.Count; i++)
                if (shieldRings[i]) shieldRings[i].gameObject.SetActive(i < LayersRemaining);
            if (visual) visual.gameObject.SetActive(true);
        }

        /// <summary>Called by the director when the player spells the current shield word.</summary>
        public bool CrackLayer()
        {
            if (Vulnerable || LayersRemaining <= 0) return false;
            LayersRemaining--;
            if (shieldRings.Count > LayersRemaining && shieldRings[LayersRemaining])
                shieldRings[LayersRemaining].gameObject.SetActive(false);
            GameFeedback.Play(GameCue.Break);
            GameFeedback.Burst("Wood_Splinter", Centre, .6f, new Color(1f, .82f, .4f), .5f);
            LayerCracked?.Invoke(LayersRemaining);
            if (Vulnerable) ShieldBroken?.Invoke();
            return true;
        }

        /// <summary>Existing melee and thrown hits arrive here; shielded hits are refused.</summary>
        public bool ApplyDamage(in HitInfo hit)
        {
            if (!Vulnerable || hit.Damage <= 0f) return false;
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
