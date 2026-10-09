using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class SlipZone : MonoBehaviour
    {
        public static GameObject Create(Vector3 position, ItemDefinition item, PlayerController owner)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = item.Id + " slippery patch";
            root.transform.SetParent(World.Transient, false);
            root.transform.position = position + Vector3.up * 0.02f;
            root.transform.localScale = new Vector3(item.Deploy.Radius * 2f, 0.025f, item.Deploy.Radius * 2f);
            var collider = root.GetComponent<Collider>();
            Object.Destroy(collider);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var area = new GameObject("Slip area");
            area.transform.SetParent(root.transform, false);
            area.transform.localScale = new Vector3(1f / root.transform.localScale.x, 1f / root.transform.localScale.y, 1f / root.transform.localScale.z);
            var box = area.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.up * 0.6f;
            box.size = new Vector3(item.Deploy.Radius * 2f, 1.5f, item.Deploy.Radius * 2f);
            var zone = area.AddComponent<SlipZone>();
            zone.radius = item.Deploy.Radius;
            root.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(new Color(.36f, .74f, .80f));
            var rim = SummonEffects.Ring(area.transform, "Soap area edge", new Color(.76f, .97f, 1f), item.Deploy.Radius, Quaternion.Euler(90f, 0f, 0f));
            rim.transform.localPosition = Vector3.up * .035f;
            var pink = SummonEffects.Ring(area.transform, "Soap pink sheen", new Color(1f, .66f, .85f), item.Deploy.Radius * .94f, Quaternion.Euler(90f, 0f, 0f));
            pink.transform.localPosition = Vector3.up * .04f;
            var gold = SummonEffects.Ring(area.transform, "Soap gold sheen", new Color(1f, .9f, .55f), item.Deploy.Radius * .88f, Quaternion.Euler(90f, 0f, 0f));
            gold.transform.localPosition = Vector3.up * .04f;
            for (int i = 0; i < 3; i++)
            {
                var bubble = SummonEffects.Ring(area.transform, "Soap bubble", new Color(.8f, 1f, 1f), .1f + i * .035f, Quaternion.Euler(90f, 0f, 0f));
                bubble.transform.localPosition = new Vector3((i - 1) * item.Deploy.Radius * .42f, .04f, (i % 2 == 0 ? .2f : -.25f) * item.Deploy.Radius);
                bubble.startWidth = bubble.endWidth = .02f;
            }
            GameFeedback.Burst("Soap_Puddle", position + Vector3.up * .15f, .65f, GameFeedback.SkillColor("SOAP"), .35f);
            var life = SummonedThing.Attach(root, item.Id, null, item.Deploy.LifetimeSeconds);
            life.ReturnsLetters = false;
            DeployedGear.Register(root, owner);
            return root;
        }

        float radius;
        void OnTriggerStay(Collider other)
        {
            var body = other.attachedRigidbody;
            if (!body || !body.TryGetComponent(out PlayerController player) || player.IsKnockedOut) return;
            if (World.Flat(player.transform.position - transform.position).sqrMagnitude <= radius * radius) player.MakeSlippery(0.15f);
        }
    }
}
