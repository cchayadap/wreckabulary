using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class PrototypeGear
    {
        public static HeldWeapon CreateBelt(FlexibleWeaponProfile profile)
        {
            if (!profile || !profile.visual) return null;
            var belt = profile.Definition();
            var item = belt.ToItem();
            var root = new GameObject(item.Id);
            root.transform.SetParent(World.Transient, false);
            var visual = Object.Instantiate(profile.visual, root.transform, false);
            visual.name = "Visual";
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(0.1f, item.Size[0]), Mathf.Max(0.1f, item.Size[1]), Mathf.Max(0.1f, item.Size[2]));
            box.center = Vector3.up * box.size.y * 0.5f;
            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var gear = root.AddComponent<HeldWeapon>();
            gear.Configure(item);
            root.AddComponent<BeltWeapon>().Configure(profile, belt);
            root.AddComponent<BeltFlexDriver>().Configure(profile, belt, visual.transform);
            var materials = MaterialLibrary.Load();
            if (materials) materials.ApplySkin(root, Skin.Standard);
            return gear;
        }
    }
}
