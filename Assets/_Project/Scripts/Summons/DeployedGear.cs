using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Reusable placed gear: it keeps its identity and can be picked up again.</summary>
    public sealed class DeployedGear : MonoBehaviour
    {
        static readonly List<DeployedGear> Active = new();
        public PlayerController Owner { get; private set; }
        ItemDefinition definition;
        bool registered;
        readonly Dictionary<int, float> lastLaunch = new();

        public static int CountFor(PlayerController owner)
        {
            int n = 0;
            foreach (var gear in Active) if (gear && gear.registered && gear.Owner == owner) n++;
            return n;
        }

        public static void Attach(HeldWeapon gear, PlayerController owner)
        {
            var component = Register(gear.gameObject, owner);
            component.definition = gear.Definition;
            var stats = component.definition.Deploy;
            if (stats.Effect == DeployEffect.Cover) return;
            var sensor = new GameObject("Tool area");
            sensor.transform.SetParent(gear.transform, false);
            var box = sensor.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.up * 0.45f;
            box.size = new Vector3(stats.FootprintX + 0.6f, 1.4f, stats.FootprintZ + 0.6f);
            sensor.AddComponent<GearTrigger>().Tool = component;
        }

        public static DeployedGear Register(GameObject root, PlayerController owner)
        {
            var component = root.AddComponent<DeployedGear>();
            component.Owner = owner;
            component.registered = true;
            Active.Add(component);
            return component;
        }

        public void Deactivate()
        {
            registered = false;
            Active.Remove(this);
            foreach (var trigger in GetComponentsInChildren<GearTrigger>()) Destroy(trigger.gameObject);
            Destroy(this);
        }

        void OnDestroy() => Active.Remove(this);

        public void Affect(Collider collider)
        {
            if (!registered || definition == null || !collider.attachedRigidbody || !collider.attachedRigidbody.TryGetComponent(out PlayerController player) || !player.CanAct) return;
            var stats = definition.Deploy;
            if (stats.Effect == DeployEffect.JumpPad)
            {
                if (player.Body.linearVelocity.y > 1f || (lastLaunch.TryGetValue(player.Index, out float t) && Time.time - t < 1f)) return;
                lastLaunch[player.Index] = Time.time;
                player.Launch(World.Flat(player.Body.linearVelocity) + Vector3.up * stats.Strength);
            }
            else if (stats.Effect == DeployEffect.SpeedStrip)
            {
                var motion = new Vector3(player.Commands.move.x, 0f, player.Commands.move.y);
                if (Vector3.Dot(motion, transform.forward) > 0.1f) player.Boost(stats.Strength, 1.5f);
            }
        }
    }
}
