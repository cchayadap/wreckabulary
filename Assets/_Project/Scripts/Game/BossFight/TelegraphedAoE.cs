using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;
using Random = UnityEngine.Random;

namespace Wreckabulary.BossFight
{
    public sealed class TelegraphedAoE : MonoBehaviour
    {
        const int RingPoints = 48, SpotTries = 8;
        const float StartRadius = .35f, FadeSeconds = .25f, RetrySeconds = .2f, LeapRetrySeconds = .25f;
        const float SmashNearMin = .7f, SmashNearMax = 1.15f, DiscHeight = .012f, DiscLift = .04f, RingLift = .06f;

        sealed class Circle
        {
            public GameObject Root, Disc;
            public LineRenderer Ring;
            public Vector3 Centre;
            public float Radius, Warning, WarnSeconds, Damage, Active, Fade, NextBeep;
            public bool IsActive, IsSmash;
            public readonly HashSet<int> HitPlayers = new();
        }

        static Material warningFill, activeFill;

        BossFightTuning tuning;
        BossController boss;
        float nextSpawn, nextSmash;
        readonly List<Circle> circles = new();
        readonly Collider[] overlaps = new Collider[16];

        public event Action<Vector3> SmashTelegraphed;

        public Func<Vector3> PickTarget { get; set; }
        public Func<Vector3, Vector3, bool> Reachable { get; set; }
        public Vector3 LastSpawnCentre { get; private set; }
        public int ActiveCircleCount => circles.Count;
        public float SmashIn => Mathf.Max(0f, nextSmash);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetMaterials()
        {
            warningFill = null;
            activeFill = null;
        }

        static Material Fill(bool striking)
        {
            if (striking && activeFill) return activeFill;
            if (!striking && warningFill) return warningFill;
            Color color = striking ? new Color(1f, .08f, .16f, .55f) : new Color(1f, .28f, .22f, .3f);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            if (!shader) return GameAssets.I.Tinted(color);
            var mat = new Material(shader) { name = striking ? "Boss strike fill" : "Boss warning fill", color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (striking) activeFill = mat; else warningFill = mat;
            return mat;
        }

        public void Configure(BossFightTuning bossTuning) => Configure(bossTuning, null);

        public void Configure(BossFightTuning bossTuning, BossController owner)
        {
            tuning = bossTuning ?? new BossFightTuning();
            boss = owner;
            nextSpawn = tuning.AoeCooldownFor(0);
            nextSmash = tuning.SmashEverySeconds;
        }

        Vector3 Target(Vector3 fallback) => PickTarget != null ? PickTarget() : fallback;

        public void TickSmash(bool running, Vector3 target, float delta)
        {
            if (!running) { nextSmash = tuning.SmashEverySeconds; return; }
            if (delta <= 0f) return;
            nextSmash -= delta;
            if (nextSmash > 0f) return;
            if (boss && boss.IsLeaping) { nextSmash = LeapRetrySeconds; return; }
            Spawn(Target(target), BossFightTuning.Phases - 1, true);
            nextSmash = tuning.SmashEverySeconds;
        }

        public void Tick(bool running, int phase, Vector3 target, float delta)
        {
            if (!running) { CancelAll(); return; }
            if (delta <= 0f) return;
            nextSpawn -= delta;
            if (nextSpawn <= 0f) Burst(phase, target);
            for (int i = circles.Count - 1; i >= 0; i--)
                if (Step(circles[i], delta)) { Destroy(circles[i].Root); circles.RemoveAt(i); }
        }

        void Burst(int phase, Vector3 target)
        {
            int room = Mathf.Max(0, tuning.MaxAoe - circles.Count);
            if (room == 0) { nextSpawn = RetrySeconds; return; }
            int low = Mathf.Max(1, tuning.AoeBurstMin);
            int high = Mathf.Max(low, tuning.AoeBurstMax);
            int burst = Mathf.Clamp(Random.Range(low, high + 1), 1, room);
            for (int i = 0; i < burst; i++) Spawn(Target(target), phase);
            nextSpawn = tuning.AoeCooldownFor(phase);
        }

        bool Step(Circle c, float delta)
        {
            if (!c.IsActive)
            {
                c.Warning -= delta;
                float t = 1f - Mathf.Clamp01(c.Warning / Mathf.Max(.01f, c.WarnSeconds));
                float radius = Mathf.Lerp(StartRadius, c.Radius, t);
                DrawRing(c, radius, .13f + .07f * Mathf.Sin(t * Mathf.PI * 10f));
                if (c.Disc) c.Disc.transform.localScale = new Vector3(radius * 2f, DiscHeight, radius * 2f);
                if (Time.time >= c.NextBeep)
                {
                    c.NextBeep = Time.time + Mathf.Lerp(.5f, .18f, t);
                    GameFeedback.Burst("Bomb_Warning", c.Centre + Vector3.up * .3f, .6f, new Color(1f, .35f, .28f), .22f);
                }
                if (c.Warning > 0f) return false;
                c.IsActive = true;
                c.Active = tuning.AoeActiveSeconds;
                DrawRing(c, c.Radius, .24f);
                if (c.Disc)
                {
                    c.Disc.transform.localScale = new Vector3(c.Radius * 2.05f, DiscHeight, c.Radius * 2.05f);
                    c.Disc.GetComponent<Renderer>().sharedMaterial = Fill(true);
                }
                Strike(c);
                return false;
            }
            if (c.Active > 0f)
            {
                c.Active -= delta;
                if (c.Active <= 0f) c.Fade = FadeSeconds;
                return false;
            }
            c.Fade -= delta;
            DrawRing(c, c.Radius, .24f * Mathf.Clamp01(c.Fade / FadeSeconds));
            return c.Fade <= 0f;
        }

        Vector3 Spot(Vector3 target, float distance, float groundY)
        {
            Vector3 first = Vector3.zero;
            for (int i = 0; i < SpotTries; i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                if (dir.sqrMagnitude < .0001f) dir = Vector2.up;
                dir.Normalize();
                var centre = new Vector3(target.x + dir.x * distance, groundY, target.z + dir.y * distance);
                if (i == 0) first = centre;
                if (Reachable == null || Reachable(target, centre)) return centre;
            }
            return first;
        }

        void Spawn(Vector3 target, int phase, bool smash = false)
        {
            float radius = smash ? tuning.SmashRadius : tuning.AoeRadiusFor(phase);
            float distance = smash ? Random.Range(SmashNearMin, SmashNearMax) : radius + BossFightTuning.FeetGap;
            float groundY = smash && boss ? boss.transform.position.y : target.y;
            var centre = Spot(target, distance, groundY);
            LastSpawnCentre = centre;
            if (smash && boss) boss.LeapTo(centre, tuning.SmashWarnSeconds);

            var root = new GameObject(smash ? "Boss jump smash" : "Boss AoE warning");
            root.transform.SetParent(transform, false);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Danger fill";
            disc.transform.SetParent(root.transform, false);
            disc.transform.position = new Vector3(centre.x, centre.y + DiscLift, centre.z);
            disc.transform.localScale = new Vector3(StartRadius * 2f, DiscHeight, StartRadius * 2f);
            var discCollider = disc.GetComponent<Collider>();
            if (discCollider) { discCollider.enabled = false; Destroy(discCollider); }
            disc.GetComponent<Renderer>().sharedMaterial = Fill(false);

            var ring = root.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = RingPoints;
            ring.numCornerVertices = 2;
            ring.numCapVertices = 2;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sharedMaterial = GameAssets.I.Tinted(smash ? new Color(1f, .55f, .12f) : new Color(1f, .35f, .25f));

            float warn = smash ? tuning.SmashWarnSeconds : tuning.AoeWarnSeconds;
            var c = new Circle
            {
                Root = root, Disc = disc, Ring = ring, Centre = centre, Radius = radius,
                Warning = warn, WarnSeconds = warn, IsSmash = smash,
                Damage = smash ? tuning.SmashDamage : tuning.AoeDamageFor(phase),
                NextBeep = Time.time + .1f,
            };
            DrawRing(c, StartRadius, smash ? .2f : .13f);
            if (smash)
            {
                GameFeedback.Burst("Jump_Arrow", centre + Vector3.up * .4f, .9f, new Color(1f, .55f, .12f), .4f);
                SmashTelegraphed?.Invoke(centre);
            }
            circles.Add(c);
        }

        void DrawRing(Circle c, float radius, float width)
        {
            c.Ring.startWidth = c.Ring.endWidth = width;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / RingPoints;
                c.Ring.SetPosition(i, c.Centre + new Vector3(Mathf.Cos(a) * radius, RingLift, Mathf.Sin(a) * radius));
            }
        }

        void Strike(Circle c)
        {
            int mask = World.PlayerLayer >= 0 ? 1 << World.PlayerLayer : ~0;
            int n = Physics.OverlapSphereNonAlloc(c.Centre, c.Radius, overlaps, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var p = overlaps[i].GetComponentInParent<PlayerController>();
                if (!p || p.IsKnockedOut || !c.HitPlayers.Add(p.Index)) continue;
                Vector3 away = p.transform.position - c.Centre;
                away.y = 0f;
                if (away.sqrMagnitude < .001f) away = Vector3.forward;
                p.Health.ApplyDamage(Hits.Hazard(away.normalized, c.Damage, 1f));
                if (!p.Summoner.IsSpelling && !p.Summoner.IsCrafting)
                    p.Inventory.DropRandom(tuning.LettersLostOnHit, p.transform.position, away.normalized);
                GameFeedback.Burst("Impact_Star", c.Centre + Vector3.up * .5f, .8f, new Color(1f, .32f, .28f), .3f);
                GameFeedback.Play(GameCue.Blast);
            }
            CameraRig.Shake(c.IsSmash ? .28f : .06f);
        }

        public void CancelAll()
        {
            for (int i = 0; i < circles.Count; i++)
                if (circles[i].Root) Destroy(circles[i].Root);
            circles.Clear();
            if (boss) boss.CancelLeap();
            if (tuning == null) return;
            nextSpawn = tuning.AoeCooldownFor(0);
            nextSmash = tuning.SmashEverySeconds;
        }
    }
}
