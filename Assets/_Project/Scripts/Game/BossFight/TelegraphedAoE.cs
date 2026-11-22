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
            public GameObject Disc;
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

        static Material warningFill, activeFill;

        /// <summary>Translucent floor disc: the thin outline alone was too easy to miss on a busy floor.</summary>
        // @trace MECH-006, ART-004, AMD-002
        static Material Fill(bool striking)
        {
            if (striking && activeFill) return activeFill;
            if (!striking && warningFill) return warningFill;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            Color color = striking ? new Color(1f, .08f, .16f, .55f) : new Color(1f, .28f, .22f, .3f);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_SrcBlend", 5f);   // SrcAlpha
            mat.SetFloat("_DstBlend", 10f);  // OneMinusSrcAlpha
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (striking) activeFill = mat; else warningFill = mat;
            return mat;
        }

        /// <summary>Last telegraph centre; exposed so playtests can verify circles land outside the feet.</summary>
        // @trace MECH-006
        public Vector3 LastSpawnCentre { get; private set; }

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
                    float radius = Mathf.Lerp(.35f, c.Radius, t);
                    DrawRing(c, radius, .13f + .07f * Mathf.Sin(t * Mathf.PI * 10f));
                    if (c.Disc) c.Disc.transform.localScale = new Vector3(radius * 2f, .012f, radius * 2f);
                    if (Time.time >= c.NextBeep)
                    {
                        c.NextBeep = Time.time + Mathf.Lerp(.5f, .18f, t);
                        GameFeedback.Burst("Bomb_Warning", c.Centre + Vector3.up * .3f, .6f,
                            new Color(1f, .35f, .28f), .22f);
                    }
                    if (c.Warning <= 0f)
                    {
                        c.IsActive = true;
                        c.Active = rules.AoeActiveSeconds;
                        DrawRing(c, c.Radius, .24f);
                        if (c.Disc)
                        {
                            c.Disc.transform.localScale = new Vector3(c.Radius * 2.05f, .012f, c.Radius * 2.05f);
                            c.Disc.GetComponent<Renderer>().sharedMaterial = Fill(true);
                        }
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
                    DrawRing(c, c.Radius, .24f * Mathf.Clamp01(c.Fade / .25f));
                    if (c.Fade <= 0f) { Destroy(c.Root); circles.RemoveAt(i); }
                }
            }
        }

        void Spawn(Vector3 target, int layer)
        {
            float radius = rules.AoeRadiusFor(layer);
            // Land just outside the player's feet. Standing still and spelling must stay safe;
            // walking into a circle is the choice the player makes.
            // @trace MECH-006
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < .0001f) dir = Vector2.up;
            dir.Normalize();
            float distance = radius + .55f;
            var centre = new Vector3(target.x + dir.x * distance, target.y, target.z + dir.y * distance);
            LastSpawnCentre = centre;

            var root = new GameObject("Boss AoE warning");
            root.transform.SetParent(transform, false);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Danger fill";
            disc.transform.SetParent(root.transform, false);
            disc.transform.position = new Vector3(centre.x, centre.y + .04f, centre.z);
            disc.transform.localScale = new Vector3(.7f, .012f, .7f);
            var discCollider = disc.GetComponent<Collider>();
            if (discCollider) Destroy(discCollider);
            disc.GetComponent<Renderer>().sharedMaterial = Fill(false);

            var ring = root.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 48;
            ring.numCornerVertices = 2;
            ring.numCapVertices = 2;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sharedMaterial = GameAssets.I.Tinted(new Color(1f, .35f, .25f));
            var c = new Circle
            {
                Root = root, Disc = disc, Ring = ring, Centre = centre, Radius = radius,
                Warning = rules.AoeWarnSeconds, NextBeep = Time.time + .1f,
            };
            DrawRing(c, .35f, .13f);
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
                p.Health.ApplyDamage(Hits.Hazard(away.normalized, damage, 1f));
                // A hit while actively spelling costs health, not the word the player is building.
                // @trace MECH-006
                if (!p.Summoner.IsSpelling && !p.Summoner.IsCrafting)
                    p.Inventory.DropRandom(rules.LettersLostOnHit, p.transform.position, away.normalized);
                GameFeedback.Burst("Impact_Star", c.Centre + Vector3.up * .5f, .8f, new Color(1f, .32f, .28f), .3f);
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
