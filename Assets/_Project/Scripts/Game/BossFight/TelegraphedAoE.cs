using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.BossFight
{
    /// <summary>
    /// Red stomp circles: a growing warning ring, a short active window, then a fade.
    /// A hit costs health AND letters, so dodging protects the player's spelling progress.
    /// </summary>
    // @trace MECH-006, NUM-003, ART-004
    public sealed class TelegraphedAoE : MonoBehaviour
    {
        sealed class Circle
        {
            public GameObject Root;
            public LineRenderer Ring;
            public Vector3 Centre;
            public float Radius;
            public float Warning;
            public float Active;
            public float Fade;
            public bool IsActive;
            public float NextBeep;
            public readonly HashSet<int> HitPlayers = new();
        }

        GameRules rules;
        float nextSpawn;
        readonly List<Circle> circles = new();
        readonly Collider[] overlaps = new Collider[16];

        public void Configure(GameRules gameRules)
        {
            rules = gameRules;
            nextSpawn = rules.AoeCooldownFor(0);
        }

        public void Tick(bool running, int layer, Vector3 target, float delta)
        {
            if (!running) { CancelAll(); return; }
            if (delta <= 0f) return;

            nextSpawn -= delta;
            if (nextSpawn <= 0f && circles.Count < rules.MaxAoe)
            {
                Spawn(target, layer);
                nextSpawn = rules.AoeCooldownFor(layer);
            }

            for (int i = circles.Count - 1; i >= 0; i--)
            {
                Circle c = circles[i];
                if (!c.IsActive)
                {
                    c.Warning -= delta;
                    float t = 1f - Mathf.Clamp01(c.Warning / Mathf.Max(0.01f, rules.AoeWarnSeconds));
                    DrawRing(c, Mathf.Lerp(.35f, c.Radius, t), .05f + .035f * Mathf.Sin(t * Mathf.PI * 12f));
                    if (Time.time >= c.NextBeep)
                    {
                        c.NextBeep = Time.time + Mathf.Lerp(.4f, .14f, t);
                        GameFeedback.Burst("Bomb_Warning", c.Centre + Vector3.up * .3f, .32f,
                            new Color(1f, .28f, .34f), .16f);
                    }
                    if (c.Warning <= 0f)
                    {
                        c.IsActive = true;
                        c.Active = rules.AoeActiveSeconds;
                        DrawRing(c, c.Radius, .12f);
                        Strike(c, layer);
                    }
                }
                else if (c.Active > 0f)
                {
                    c.Active -= delta;
                    if (c.Active <= 0f) c.Fade = .25f;
                }
                else
                {
                    c.Fade -= delta;
                    DrawRing(c, c.Radius, .12f * Mathf.Clamp01(c.Fade / .25f));
                    if (c.Fade <= 0f) { Destroy(c.Root); circles.RemoveAt(i); }
                }
            }
        }

        void Spawn(Vector3 target, int layer)
        {
            float radius = rules.AoeRadiusFor(layer);
            var centre = new Vector3(
                target.x + Random.Range(-1.1f, 1.1f),
                target.y,
                target.z + Random.Range(-1.1f, 1.1f));
            var root = new GameObject("Boss AoE warning");
            root.transform.SetParent(transform, false);
            var ring = root.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 48;
            ring.numCornerVertices = 2;
            ring.numCapVertices = 2;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sharedMaterial = GameAssets.I.Tinted(new Color(1f, .28f, .34f));
            var c = new Circle
            {
                Root = root, Ring = ring, Centre = centre, Radius = radius,
                Warning = rules.AoeWarnSeconds, NextBeep = Time.time + .1f,
            };
            DrawRing(c, .35f, .05f);
            circles.Add(c);
        }

        void DrawRing(Circle c, float radius, float width)
        {
            c.Ring.startWidth = c.Ring.endWidth = width;
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                c.Ring.SetPosition(i, c.Centre + new Vector3(Mathf.Cos(a) * radius, .06f, Mathf.Sin(a) * radius));
            }
        }

        void Strike(Circle c, int layer)
        {
            int mask = World.PlayerLayer >= 0 ? 1 << World.PlayerLayer : ~0;
            int n = Physics.OverlapSphereNonAlloc(c.Centre, c.Radius, overlaps, mask, QueryTriggerInteraction.Ignore);
            float damage = rules.AoeDamageFor(layer);
            for (int i = 0; i < n; i++)
            {
                var p = overlaps[i].GetComponentInParent<PlayerController>();
                if (!p || p.IsKnockedOut || !c.HitPlayers.Add(p.Index)) continue;
                Vector3 away = (p.transform.position - c.Centre);
                away.y = 0f;
                if (away.sqrMagnitude < .001f) away = Vector3.forward;
                p.Health.ApplyDamage(Hits.Hazard(away.normalized, damage, 2f));
                p.Inventory.DropRandom(rules.LettersLostOnHit, p.transform.position, away.normalized);
                GameFeedback.Play(GameCue.Blast);
            }
            CameraRig.Shake(.06f);
        }

        public void CancelAll()
        {
            for (int i = 0; i < circles.Count; i++)
                if (circles[i].Root) Destroy(circles[i].Root);
            circles.Clear();
            nextSpawn = rules != null ? rules.AoeCooldownFor(0) : 4.5f;
        }
    }
}
