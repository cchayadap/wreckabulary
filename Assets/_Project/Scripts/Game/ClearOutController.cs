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
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"Clear-out: {closure.Room}";
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(transform, false);
                    go.transform.position = new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY + .012f, (room.MinZ + room.MaxZ) * .5f);
                    go.transform.localScale = new Vector3(room.MaxX - room.MinX - .3f, .01f, room.MaxZ - room.MinZ - .3f);
                    indicator = go.GetComponent<Renderer>();
                    indicators[closure.Room] = indicator;
                }
                var color = phase == RoomPhase.Warning ? new Color(.91f, .65f, .19f) : phase == RoomPhase.Filling
                    ? new Color(.87f, .32f, .15f) : new Color(.36f, .22f, .22f);
                indicator.sharedMaterial = GameAssets.I.Tinted(color);
                if (!phases.TryGetValue(closure.Room, out var old) || old != phase)
                {
                    phases[closure.Room] = phase;
                    if (phase == RoomPhase.Filling) { CameraRig.Shake(.2f); ClosureStarted?.Invoke(); }
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
}
