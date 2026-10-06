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
            root.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(new Color(0.4f, 0.75f, 0.9f));
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
