using System;
using UnityEngine;

namespace Wreckabulary
{
    public sealed class AuthoredHouse : MonoBehaviour
    {
        public const int CurrentContentVersion = 4;
        [SerializeField] string mapId = "pinwheel";
        [SerializeField] int contentVersion;
        [SerializeField] Transform geometryRoot;
        [SerializeField] Transform furnitureRoot;

        Transform furnitureSnapshot;
        public string MapId => mapId;
        public int ContentVersion => contentVersion;
        public bool IsCurrent => contentVersion >= CurrentContentVersion;
        public Transform GeometryRoot => geometryRoot;
        public Transform FurnitureRoot => furnitureRoot;

        public static string ResourcePath(string id) => id switch
        {
            "pinwheel" => "Worlds/PinwheelHouse",
            "courtyard" => "Worlds/GardenCourtyard",
            "flat" => "Worlds/CityFlat",
            "terrace" => "Worlds/TerraceHouse",
            "walkup" => "Worlds/WalkupApartments",
            _ => null
        };

        public bool Matches(string id) => string.Equals(mapId, id, StringComparison.Ordinal);

        public void Configure(string id, Transform geometry, Transform furniture)
        {
            mapId = id;
            contentVersion = CurrentContentVersion;
            geometryRoot = geometry;
            furnitureRoot = furniture;
        }

        public void PrepareForPlay()
        {
            if (furnitureSnapshot) return;
            if (!furnitureRoot)
            {
                furnitureRoot = new GameObject("Original furniture").transform;
                furnitureRoot.SetParent(transform, false);
            }
            var snapshotHolder = new GameObject("Round furniture snapshot");
            snapshotHolder.transform.SetParent(transform, false);
            snapshotHolder.SetActive(false);
            snapshotHolder.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            furnitureSnapshot = Instantiate(furnitureRoot, snapshotHolder.transform, false);
            furnitureSnapshot.name = "Initial authored furniture";
            furnitureSnapshot.gameObject.SetActive(false);
            KeepTactileMaterialsAlive(furnitureSnapshot);
        }

        public Transform ResetFurniture(bool furnish)
        {
            PrepareForPlay();
            if (furnitureRoot)
            {
                furnitureRoot.gameObject.SetActive(false);
                Destroy(furnitureRoot.gameObject);
            }
            if (furnish)
            {
                furnitureRoot = Instantiate(furnitureSnapshot, transform, false);
                furnitureRoot.name = "Original furniture";
                furnitureRoot.gameObject.hideFlags = HideFlags.None;
                KeepTactileMaterialsAlive(furnitureRoot);
                furnitureRoot.gameObject.SetActive(true);
            }
            else
            {
                furnitureRoot = new GameObject("Original furniture").transform;
                furnitureRoot.SetParent(transform, false);
            }
            return furnitureRoot;
        }

        static void KeepTactileMaterialsAlive(Transform root)
        {
            if (root.GetComponentInChildren<Art.TactileMaterials>(true)) Art.TactileMaterials.Apply(root.gameObject);
        }
    }
}
