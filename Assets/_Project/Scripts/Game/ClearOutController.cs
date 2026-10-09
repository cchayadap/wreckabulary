using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class ClearOutController : MonoBehaviour
    {
        readonly Dictionary<string, Renderer> indicators = new();
        readonly Dictionary<string, RoomPhase> phases = new();
        readonly Dictionary<string, ClearOutSwirl> swirls = new();
        float began, nextDamage;
        public HouseLayout Layout { get; private set; }
        public ClearOutSchedule Schedule { get; private set; }
        public bool Running { get; set; }
        public float Elapsed => Running ? Mathf.Max(0f, Time.time - began) : 0f;
        public string Message { get; private set; } = "";
        public event System.Action ClosureStarted;

        public void Configure(HouseLayout layout, string mode)
        {
            Layout = layout;
            layout.ClearOutOrders.TryGetValue(mode, out var order);
            Schedule = new ClearOutSchedule(GameConfig.Current.RulesFor(mode), order ?? new List<string>());
            ResetSchedule();
        }

        public void ResetSchedule()
        {
            Running = false;
            Message = "";
            phases.Clear();
            swirls.Clear();
            foreach (var indicator in indicators.Values) if (indicator) Destroy(indicator.gameObject);
            indicators.Clear();
        }

        public void Begin()
        {
            began = Time.time;
            nextDamage = began;
            Running = true;
        }

        void Update()
        {
            if (!Running || Layout == null) return;
            float now = Elapsed;
            Message = "";
            foreach (var closure in Schedule.Closures)
            {
                var phase = Schedule.PhaseOf(closure.Room, now);
                if (phase == RoomPhase.Safe) continue;
                if (!indicators.TryGetValue(closure.Room, out var indicator) || !indicator)
                {
                    var room = Layout.Room(closure.Room);
                    var go = new GameObject();
                    go.name = $"Clear-out: {closure.Room}";
                    go.transform.SetParent(transform, false);
                    go.transform.position = new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY + .035f, (room.MinZ + room.MaxZ) * .5f);
                    float x = (room.MaxX - room.MinX) * .5f - .2f, z = (room.MaxZ - room.MinZ) * .5f - .2f;
                    var border = SummonEffects.Line(go.transform, new Color(1f, .72f, .23f), new[]
                    {
                        new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z),
                        new Vector3(x, 0f, z), new Vector3(x, 0f, -z)
                    }, true);
                    border.startWidth = border.endWidth = .13f;
                    indicator = border;
                    indicators[closure.Room] = indicator;
                }
                var color = phase == RoomPhase.Warning ? new Color(.91f, .65f, .19f) : phase == RoomPhase.Filling
                    ? new Color(.87f, .32f, .15f) : new Color(.36f, .22f, .22f);
                if (!phases.TryGetValue(closure.Room, out var old) || old != phase)
                {
                    phases[closure.Room] = phase;
                    indicator.sharedMaterial = GameAssets.I.Tinted(color);
                    if (phase == RoomPhase.Filling)
                    {
                        CameraRig.Shake(.2f);
                        swirls[closure.Room] = ClearOutSwirl.Create(indicator.transform, this);
                        ClosureStarted?.Invoke();
                    }
                    else if (swirls.TryGetValue(closure.Room, out var swirl) && swirl)
                    {
                        Destroy(swirl.gameObject);
                        swirls.Remove(closure.Room);
                    }
                }
                if (phase == RoomPhase.Warning)
                    Message = $"MOVERS: {Layout.WithStorey(closure.Room)} in {Mathf.CeilToInt((float)closure.FillAt - now)}s — follow a doorway out";
                else if (phase == RoomPhase.Filling && Message.Length == 0)
                    Message = $"LEAVE {Layout.WithStorey(closure.Room).ToUpperInvariant()} — movers are packing it";
            }
            if (Time.time < nextDamage) return;
            nextDamage = Time.time + .5f;
            foreach (var p in World.Players)
            {
                if (!p || !p.Health.IsAlive) continue;
                var at = p.transform.position;
                string room = Layout.RoomAt(at.x, at.y, at.z);
                if (room == null) continue;
                float damage = Schedule.DamagePerSecond(room, now) * .5f;
                if (damage > 0f) p.Health.ApplyDamage(HitInfo.Hazard(damage));
            }
        }
    }

    /// <summary>A brief visual wind-up during the existing Filling phase; it has no physics or damage.</summary>
    public sealed class ClearOutSwirl : MonoBehaviour
    {
        ClearOutController owner;

        public static ClearOutSwirl Create(Transform parent, ClearOutController owner)
        {
            var root = new GameObject("Mover whirlwind");
            root.transform.SetParent(parent, false);
            var swirl = root.AddComponent<ClearOutSwirl>();
            swirl.owner = owner;
            for (int strand = 0; strand < 3; strand++)
            {
                var ribbon = new GameObject("Wind ribbon").transform;
                ribbon.SetParent(root.transform, false);
                var points = new Vector3[100];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = (float)i / (points.Length - 1);
                    float radius = Mathf.Lerp(.13f, 1.1f, t);
                    float angle = t * Mathf.PI * 8f + strand * Mathf.PI * 2f / 3f;
                    points[i] = new Vector3(Mathf.Cos(angle) * radius, t * 2.6f, Mathf.Sin(angle) * radius);
                }
                var line = SummonEffects.Line(ribbon, strand == 1 ? new Color(.94f, .83f, .67f) : new Color(.74f, .82f, .82f), points);
                line.startWidth = .04f;
                line.endWidth = .16f;
            }
            return swirl;
        }

        void Update()
        {
            if (owner && owner.Running) transform.Rotate(0f, -Time.deltaTime * 185f, 0f, Space.Self);
        }
    }
}
