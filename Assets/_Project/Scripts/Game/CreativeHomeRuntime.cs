using System;
using System.Linq;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class CreativeHomeRuntime : MonoBehaviour
    {
        Transform geometry, decor, ghost;
        HouseLayout house;
        string map;
        LineRenderer selection;
        public HouseLayout House => house;
        public void SetTallWalls(bool tall) => RoomBuilder.SetTallWalls(geometry, tall);

        public bool Show(HomeLayout layout, out string error)
        {
            error = null;
            var validation = new HomeDesigner(GameConfig.Current.Houses, GameConfig.Current.Items).Validate(layout);
            if (!validation.Ok) { error = string.Join("\n", validation.Errors); return false; }
            Transform staged = null, stagedGeometry = null;
            try
            {
                var targetHouse = GameConfig.Current.HouseFor(layout.Map);
                staged = new GameObject("Home decor").transform;
                staged.SetParent(transform, false); staged.gameObject.SetActive(false);
                foreach (var prop in layout.Props) SpawnProp(prop, targetHouse, staged, true);
                if (map != layout.Map || !geometry)
                {
                    stagedGeometry = RoomBuilder.CreateGeometry(targetHouse, layout.Map, transform, false);
                    stagedGeometry.gameObject.SetActive(false);
                }
                if (decor) Dispose(decor);
                if (stagedGeometry)
                {
                    if (geometry) Dispose(geometry);
                    geometry = stagedGeometry; geometry.gameObject.SetActive(true);
                }
                decor = staged; decor.gameObject.SetActive(true); map = layout.Map; house = targetHouse;
                ClearGhost(); Highlight(null); return true;
            }
            catch (Exception ex)
            {
                if (staged) Dispose(staged);
                if (stagedGeometry) Dispose(stagedGeometry);
                error = "Could not show home: " + ex.Message; return false;
            }
        }

        static Transform SpawnProp(HomeProp prop, HouseLayout house, Transform parent, bool solid)
        {
            var item = GameConfig.Current.Items.Get(prop.Word);
            var room = house.Rooms.First(r => r.Contains((float)prop.X, (float)prop.Z));
            var root = new GameObject(prop.Word + " · " + prop.Id).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(new Vector3((float)prop.X, room.FloorY, (float)prop.Z), Quaternion.Euler(0f, prop.Yaw, 0f));
            var visual = ModelVisual.Spawn(item.Model, root, prop.Skin);
            if (!visual) throw new InvalidOperationException("Imported model is unavailable for " + prop.Word + ".");
            var bounds = ModelVisual.BoundsIn(root, visual);
            visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (solid)
            {
                var box = root.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(Mathf.Max(.4f, item.Size[0]), Mathf.Max(.1f, item.Size[1]), Mathf.Max(.4f, item.Size[2]));
                box.center = new Vector3(0f, box.size.y * .5f, 0f);
                root.gameObject.AddComponent<HomePropView>().Id = prop.Id;
            }
            return root;
        }

        public void Preview(HomeProp prop, bool valid)
        {
            ClearGhost();
            if (prop == null || house == null || !house.Rooms.Any(r => r.Contains((float)prop.X, (float)prop.Z))) return;
            try
            {
                ghost = new GameObject("Placement preview").transform; ghost.SetParent(transform, false);
                SpawnProp(prop, house, ghost, false);
                Outline(ghost, prop, valid ? new Color(.2f, .85f, .52f) : new Color(1f, .26f, .2f));
            }
            catch (Exception) { ClearGhost(); }
        }
        public void ClearGhost() { if (ghost) Dispose(ghost); ghost = null; }
        static void Dispose(Transform root)
        { root.gameObject.SetActive(false); TactileMaterials.Release(root.gameObject); Destroy(root.gameObject); }
        void OnDestroy()
        {
            TactileMaterials.Release(gameObject);
        }
        public void Highlight(HomeProp prop)
        {
            if (selection) { selection.gameObject.SetActive(false); Destroy(selection.gameObject); }
            selection = prop == null ? null : Outline(transform, prop, new Color(1f, .78f, .26f));
        }
        LineRenderer Outline(Transform parent, HomeProp prop, Color colour)
        {
            var item = GameConfig.Current.Items.Get(prop.Word);
            float x = Mathf.Max(.4f, item.Size[0]) * .5f, z = Mathf.Max(.4f, item.Size[2]) * .5f;
            if (prop.Yaw % 180 != 0) { float swap = x; x = z; z = swap; }
            var room = house.Rooms.FirstOrDefault(r => r.Contains((float)prop.X, (float)prop.Z));
            float y = (room?.FloorY ?? 0f) + .035f;
            var line = new GameObject("Footprint").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false); line.useWorldSpace = true; line.loop = true; line.positionCount = 4;
            line.startWidth = line.endWidth = .045f; line.sharedMaterial = GameAssets.I.Tinted(colour);
            line.SetPositions(new[] { new Vector3((float)prop.X-x,y,(float)prop.Z-z), new Vector3((float)prop.X+x,y,(float)prop.Z-z),
                new Vector3((float)prop.X+x,y,(float)prop.Z+z), new Vector3((float)prop.X-x,y,(float)prop.Z+z) });
            return line;
        }

        public void Frame(Camera camera)
        {
            if (!camera || house == null) return;
            float minX = house.Rooms.Min(r => r.MinX), maxX = house.Rooms.Max(r => r.MaxX);
            float minZ = house.Rooms.Min(r => r.MinZ), maxZ = house.Rooms.Max(r => r.MaxZ);
            var centre = new Vector3((minX + maxX) * .5f, 0f, (minZ + maxZ) * .5f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max((maxX-minX) / Mathf.Max(.1f,camera.aspect), (maxZ-minZ)*.85f)*.55f+1.5f;
            camera.transform.position = centre + new Vector3(0f,30f,-22f); camera.transform.LookAt(centre);
        }
    }

    public sealed class HomePropView : MonoBehaviour { public string Id; }
}
