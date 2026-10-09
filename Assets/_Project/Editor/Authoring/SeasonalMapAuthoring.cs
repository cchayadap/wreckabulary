using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class SeasonalMapAuthoring
    {
        public const string WorldPath = "Assets/_Project/Resources/Worlds/PinwheelHouse.prefab";
        public const string CollectionId = "winter-house-party", RoomId = "LivingRoom";
        const float RouteRadius = .4f;

        [MenuItem("Wreckabulary/Authoring/Add Pinwheel Winter Corner", priority = 24)]
        public static void ApplyPinwheelWinter()
        {
            Guard();
            var root = PrefabUtility.LoadPrefabContents(WorldPath);
            Mesh[] temporary = Array.Empty<Mesh>();
            try
            {
                var world = root.GetComponent<AuthoredHouse>();
                if (!ApplyMissing(world)) { Debug.Log("PINWHEEL_WINTER_PRESERVED: existing artist decoration retained."); return; }
                var addition = Find(world);
                temporary = addition.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                    .Where(mesh => mesh && !EditorUtility.IsPersistent(mesh)).Distinct().ToArray();
                WorldAuthoring.PersistAssets(addition.gameObject, "pinwheel_winter_room_v1");
                if (!PrefabUtility.SaveAsPrefabAsset(root, WorldPath)) throw new IOException("Could not save the Pinwheel winter corner.");
                AssetDatabase.SaveAssets();
                Debug.Log("PINWHEEL_WINTER_READY: one editable room corner; gameplay geometry and furniture retained.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                foreach (var mesh in temporary) if (mesh) Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>Builds only missing decor on the supplied world; callers choose whether to persist or discard it.</summary>
        public static bool ApplyMissing(AuthoredHouse world)
        {
            Guard();
            if (!world || !world.IsCurrent || world.MapId != "pinwheel" || !world.GeometryRoot)
                throw new ArgumentException("A current authored Pinwheel world is required.", nameof(world));
            if (Find(world)) return false;
            var layout = GameConfig.Current.HouseFor(world.MapId);
            var room = layout.Room(RoomId);
            int storey = layout.StoreyOf(room);
            var parent = world.GeometryRoot.Find(RoomBuilder.StoreyName(storey));
            if (!parent) throw new InvalidOperationException("Pinwheel's authored ground storey is missing.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(WinterPropsBuilder.PrefabPath);
            if (!source) throw new InvalidOperationException("Build the verified Winter props before adding the room corner.");

            var root = new GameObject("Winter reading corner");
            root.transform.SetParent(parent, false);
            Mesh garlandMesh = null;
            try
            {
                var marker = root.AddComponent<SeasonalRoomDressing>();
                marker.Configure(CollectionId, RoomId, storey);
                var space = world.GeometryRoot;
                float floor = room.FloorY;
                var floorDecor = Group(root.transform, "Floor decorations");
                SavedMesh(source, "Winter tree", "Winter tree", floorDecor, space, new Vector3(-4.85f, floor, 3f), 1f);
                SavedMesh(source, "GiftTall", "Tall wrapped gift", floorDecor, space, new Vector3(-4.55f, floor, 2.25f), .6f, 12f);
                SavedMesh(source, "GiftWide", "Wide wrapped gift", floorDecor, space, new Vector3(-4.85f, floor, 2.38f), .65f, -10f);
                SavedMesh(source, "GiftSmall", "Small wrapped gift", floorDecor, space, new Vector3(-4.62f, floor, 1.88f), .65f, 5f);
                ValidateFloorPlacement(world, layout, room, floorDecor);

                var wallDecor = Group(root.transform, "North wall decorations");
                var wreath = SavedMesh(source, "Mounted wreath", "Wall wreath", wallDecor, space, new Vector3(-4.95f, floor + 1.86f, 3.75f), .9f);
                MountNorthWall(world, room, wreath);
                garlandMesh = Garland();
                var brass = AssetDatabase.LoadAssetAtPath<Material>(WinterPropsBuilder.ArtRoot + "/Materials/WinterBrass.mat");
                if (!brass) throw new InvalidOperationException("The saved WinterBrass material is missing.");
                var garland = Group(wallDecor, "Brass winter garland");
                garland.SetPositionAndRotation(space.TransformPoint(new Vector3(-5.05f, floor + 2.76f, 3.79f)), space.rotation);
                garland.gameObject.AddComponent<MeshFilter>().sharedMesh = garlandMesh;
                garland.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
                MountNorthWall(world, room, garland);
                ValidateWallPlacement(world, wallDecor);
                wallDecor.gameObject.AddComponent<CutawaySurface>().Configure(CutawayKind.UpperWall, storey, RoomId,
                    wallDecor.GetComponentsInChildren<Renderer>(true));
                return true;
            }
            catch
            {
                Object.DestroyImmediate(root);
                if (garlandMesh) Object.DestroyImmediate(garlandMesh);
                throw;
            }
        }

        public static SeasonalRoomDressing Find(AuthoredHouse world) => world ? world.GetComponentsInChildren<SeasonalRoomDressing>(true)
            .FirstOrDefault(marker => marker.CollectionId == CollectionId && marker.RoomId == RoomId) : null;

        static Transform SavedMesh(GameObject source, string sourceName, string name, Transform parent, Transform space,
            Vector3 at, float scale, float yaw = 0f)
        {
            var original = ModelVisual.FindNamed(source, sourceName);
            var filter = original ? original.GetComponent<MeshFilter>() : null;
            var renderer = original ? original.GetComponent<MeshRenderer>() : null;
            if (!filter || !filter.sharedMesh || !renderer || renderer.sharedMaterials.Any(material => !material))
                throw new InvalidOperationException("Verified saved Winter mesh is missing: " + sourceName);
            var result = Group(parent, name);
            result.SetPositionAndRotation(space.TransformPoint(at), space.rotation * Quaternion.Euler(0f, yaw, 0f));
            result.localScale = Vector3.one * scale;
            result.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var visual = result.gameObject.AddComponent<MeshRenderer>();
            visual.sharedMaterials = renderer.sharedMaterials;
            visual.shadowCastingMode = ShadowCastingMode.On;
            var bounds = ModelVisual.BoundsIn(space, result.gameObject);
            result.position += space.TransformVector(new Vector3(at.x - bounds.center.x, at.y - bounds.min.y, at.z - bounds.center.z));
            return result;
        }

        static void ValidateFloorPlacement(AuthoredHouse world, HouseLayout layout, RoomBox room, Transform decor)
        {
            var doors = layout.Doors.Where(door => door.A == RoomId || door.B == RoomId).ToArray();
            var furniture = world.FurnitureRoot ? world.FurnitureRoot.GetComponentsInChildren<MeshRenderer>(true) : Array.Empty<MeshRenderer>();
            var walls = world.GeometryRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("Wall", StringComparison.Ordinal) && renderer.GetComponent<BoxCollider>()).ToArray();
            foreach (Transform part in decor)
            {
                var bounds = ModelVisual.BoundsIn(world.GeometryRoot, part.gameObject);
                if (bounds.min.x < room.MinX + .2f || bounds.max.x > room.MaxX - .2f ||
                    bounds.min.z < room.MinZ + .2f || bounds.max.z > room.MaxZ - .2f || Mathf.Abs(bounds.min.y - room.FloorY) > .005f)
                    throw new InvalidOperationException(part.name + " does not fit on the authored room floor.");
                foreach (var renderer in furniture)
                {
                    var occupied = ModelVisual.BoundsIn(world.GeometryRoot, renderer.gameObject);
                    occupied.Expand(.16f);
                    if (occupied.Intersects(bounds)) throw new InvalidOperationException(part.name + " overlaps authored furniture: " + renderer.name);
                }
                foreach (var wall in walls)
                {
                    var occupied = ModelVisual.BoundsIn(world.GeometryRoot, wall.gameObject);
                    occupied.Expand(.06f);
                    if (occupied.Intersects(bounds)) throw new InvalidOperationException(part.name + " overlaps an authored wall: " + wall.name);
                }
                foreach (var spawn in layout.Spawns.Where(spawn => spawn.Room == RoomId))
                    if (Distance(bounds, spawn.X, spawn.Z) < 1.2f) throw new InvalidOperationException(part.name + " encroaches on a spawn.");
                foreach (var door in doors)
                    if (Distance(bounds, door.X, door.Z) < door.Width * .5f + .4f)
                        throw new InvalidOperationException(part.name + " encroaches on a doorway.");
                for (int i = 0; i < doors.Length; i++)
                    for (int j = i + 1; j < doors.Length; j++)
                        if (Crosses(bounds, new Vector2(doors[i].X, doors[i].Z), new Vector2(doors[j].X, doors[j].Z), RouteRadius))
                            throw new InvalidOperationException(part.name + " encroaches on a direct walking route.");
            }
        }

        static void MountNorthWall(AuthoredHouse world, RoomBox room, Transform attachment)
        {
            var space = world.GeometryRoot;
            var bounds = ModelVisual.BoundsIn(space, attachment.gameObject);
            float face = float.PositiveInfinity;
            foreach (var renderer in space.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "Upper wall infill" &&
                    !(renderer.name.StartsWith("Wall", StringComparison.Ordinal) && renderer.GetComponent<BoxCollider>())) continue;
                var wall = ModelVisual.BoundsIn(space, renderer.gameObject);
                if (Mathf.Abs(wall.center.z - room.MaxZ) > .3f || wall.size.z > .5f ||
                    wall.min.x > bounds.min.x + .02f || wall.max.x < bounds.max.x - .02f ||
                    wall.max.y < bounds.min.y || wall.min.y > bounds.max.y) continue;
                face = Mathf.Min(face, wall.min.z);
            }
            if (float.IsPositiveInfinity(face)) throw new InvalidOperationException("No saved north-wall face supports " + attachment.name + ".");
            attachment.position += space.TransformVector(Vector3.forward * (face + .004f - bounds.max.z));
        }

        static void ValidateWallPlacement(AuthoredHouse world, Transform decor)
        {
            var fixtures = world.GeometryRoot.GetComponentsInChildren<Transform>(true)
                .Where(part => part.name is "Window vignette" or "Framed townhouse print" or "Wall sconce").ToArray();
            foreach (var renderer in decor.GetComponentsInChildren<Renderer>(true))
            {
                var bounds = ModelVisual.BoundsIn(world.GeometryRoot, renderer.gameObject);
                foreach (var fixture in fixtures)
                {
                    var occupied = ModelVisual.BoundsIn(world.GeometryRoot, fixture.gameObject);
                    occupied.Expand(.08f);
                    if (bounds.Intersects(occupied)) throw new InvalidOperationException(renderer.name + " overlaps saved wall detail: " + fixture.name);
                }
            }
        }

        static float Distance(Bounds bounds, float x, float z)
        {
            float dx = Mathf.Max(bounds.min.x - x, 0f, x - bounds.max.x);
            float dz = Mathf.Max(bounds.min.z - z, 0f, z - bounds.max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static bool Crosses(Bounds bounds, Vector2 a, Vector2 b, float margin)
        {
            float enter = 0f, leave = 1f;
            var lo = new Vector2(bounds.min.x, bounds.min.z);
            var hi = new Vector2(bounds.max.x, bounds.max.z);
            var delta = b - a;
            bool intersects = true;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(delta[axis]) < .00001f) { if (a[axis] < lo[axis] || a[axis] > hi[axis]) intersects = false; continue; }
                float x = (lo[axis] - a[axis]) / delta[axis], y = (hi[axis] - a[axis]) / delta[axis];
                enter = Mathf.Max(enter, Mathf.Min(x, y)); leave = Mathf.Min(leave, Mathf.Max(x, y));
                if (enter > leave) intersects = false;
            }
            if (intersects || Distance(bounds, a.x, a.y) < margin || Distance(bounds, b.x, b.y) < margin) return true;
            foreach (var corner in new[] { lo, hi, new Vector2(lo.x, hi.y), new Vector2(hi.x, lo.y) })
            {
                float t = delta.sqrMagnitude > .00001f ? Mathf.Clamp01(Vector2.Dot(corner - a, delta) / delta.sqrMagnitude) : 0f;
                if (Vector2.Distance(corner, a + delta * t) < margin) return true;
            }
            return false;
        }

        static Mesh Garland()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var boxFaces = new[] { 0,2,1, 1,2,3, 4,5,6, 5,7,6, 0,1,4, 1,5,4, 2,6,3, 3,6,7, 0,4,2, 2,4,6, 1,3,5, 3,7,5 };
            Vector3 At(float t) => new Vector3(Mathf.Lerp(-.72f, .72f, t), -.14f * Mathf.Sin(t * Mathf.PI), 0f);
            for (int n = 0; n < 18; n++)
            {
                Vector3 a = At(n / 18f), b = At((n + 1f) / 18f), centre = (a + b) * .5f;
                var rotation = Quaternion.FromToRotation(Vector3.right, b - a);
                int first = vertices.Count;
                for (int i = 0; i < 8; i++) vertices.Add(centre + rotation * new Vector3((i % 2 == 0 ? -1f : 1f) * (b - a).magnitude * .51f,
                    (i / 2 % 2 == 0 ? -1f : 1f) * .012f, (i / 4 == 0 ? -1f : 1f) * .014f));
                foreach (int index in boxFaces) triangles.Add(first + index);
            }
            var beadFaces = new[] { 0,2,4, 0,4,3, 0,3,5, 0,5,2, 1,4,2, 1,3,4, 1,5,3, 1,2,5 };
            for (int n = 0; n < 9; n++)
            {
                Vector3 at = At((n + .5f) / 9f) + Vector3.down * .045f;
                int first = vertices.Count;
                foreach (var offset in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                    vertices.Add(at + offset * .038f);
                foreach (int index in beadFaces) triangles.Add(first + index);
            }
            var mesh = new Mesh { name = "Winter brass garland" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static Transform Group(Transform parent, string name)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent, false);
            return result;
        }

        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Add saved seasonal dressing outside Play mode.");
        }
    }
}
