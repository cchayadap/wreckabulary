using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class DeployedGear : MonoBehaviour
    {
        static readonly List<DeployedGear> Active = new();
        public PlayerController Owner { get; private set; }
        ItemDefinition definition;
        bool registered;
        Transform marker;
        readonly Transform[] chevrons = new Transform[2];
        DeployedPowerField field;
        float expires;
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
            component.expires = stats.LifetimeSeconds > 0f ? Time.time + stats.LifetimeSeconds : float.PositiveInfinity;
            if (stats.Effect == DeployEffect.Cover) return;
            if (stats.Effect is DeployEffect.WindField or DeployEffect.SlowField)
            {
                component.field = gear.gameObject.AddComponent<DeployedPowerField>();
                component.field.Configure(component.definition);
                return;
            }
            var sensor = new GameObject("Tool area");
            sensor.transform.SetParent(gear.transform, false);
            var box = sensor.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.up * 0.45f;
            box.size = new Vector3(stats.FootprintX + 0.6f, 1.4f, stats.FootprintZ + 0.6f);
            sensor.AddComponent<GearTrigger>().Tool = component;
            component.BuildMarker();
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
            if (field) { field.enabled = false; Destroy(field); }
            if (marker) Destroy(marker.gameObject);
            Destroy(this);
        }

        void OnDestroy() => Active.Remove(this);

        void Update()
        {
            if (!registered || definition == null) return;
            if (Time.time >= expires)
            {
                registered = false;
                Active.Remove(this);
                if (TryGetComponent<HeldWeapon>(out var gear)) gear.Break();
                return;
            }
            if (definition.Deploy.Effect != DeployEffect.SpeedStrip || !marker) return;
            float run = definition.Deploy.FootprintZ * .7f;
            for (int i = 0; i < chevrons.Length; i++)
                if (chevrons[i]) chevrons[i].localPosition = Vector3.forward * ((Mathf.Repeat(Time.time * 1.2f + i * .5f, 1f) - .5f) * run);
        }

        void BuildMarker()
        {
            marker = new GameObject("Ability marker").transform;
            marker.SetParent(transform, false);
            marker.localPosition = Vector3.up * (definition.Size[1] + .035f);
            var color = GameFeedback.SkillColor(definition.Id);
            var stats = definition.Deploy;
            float x = stats.FootprintX * .4f, z = stats.FootprintZ * .4f;
            SummonEffects.Line(marker, color, new[] { new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z), new Vector3(x, 0f, z), new Vector3(x, 0f, -z) }, true);
            for (int i = 0; i < 2; i++)
            {
                var arrow = new GameObject("Direction chevron").transform;
                arrow.SetParent(marker, false);
                chevrons[i] = arrow;
                float center = stats.Effect == DeployEffect.SpeedStrip ? 0f : (i - .5f) * z;
                SummonEffects.Line(arrow, color, new[] { new Vector3(-x * .45f, .01f, center - .12f), new Vector3(0f, .01f, center + .15f), new Vector3(x * .45f, .01f, center - .12f) });
            }
        }

        public void Affect(Collider collider)
        {
            if (!registered || definition == null || !collider.attachedRigidbody || !collider.attachedRigidbody.TryGetComponent(out PlayerController player) || !player.CanAct) return;
            var stats = definition.Deploy;
            if (stats.Effect == DeployEffect.JumpPad)
            {
                if (player.Body.linearVelocity.y > 1f || (lastLaunch.TryGetValue(player.Index, out float t) && Time.time - t < 1f)) return;
                lastLaunch[player.Index] = Time.time;
                player.Launch(World.Flat(player.Body.linearVelocity) + Vector3.up * stats.Strength);
                GameFeedback.Play(GameCue.Jump);
                GameFeedback.Burst("Jump_Arrow", player.transform.position + Vector3.up * .3f, .8f, GameFeedback.SkillColor("BED"), .55f);
            }
            else if (stats.Effect == DeployEffect.SpeedStrip)
            {
                var motion = new Vector3(player.Commands.move.x, 0f, player.Commands.move.y);
                if (Vector3.Dot(motion, transform.forward) > 0.1f)
                {
                    player.Boost(stats.Strength, 1.5f);
                    if (!lastLaunch.TryGetValue(player.Index, out float t) || Time.time - t > .7f)
                    {
                        lastLaunch[player.Index] = Time.time;
                        GameFeedback.Play(GameCue.Boost);
                        GameFeedback.Burst("Speed_Trail", player.transform.position + Vector3.up * .25f, .7f, GameFeedback.SkillColor("MAT"), .4f);
                    }
                }
            }
        }
    }
}
