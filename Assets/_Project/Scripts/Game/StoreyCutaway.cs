using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Calculates visibility without changing authored renderers, meshes, transforms or colliders.</summary>
    public sealed class StoreyCutaway : MonoBehaviour
    {
        public static StoreyCutaway Instance { get; private set; }
        const float Interval = .2f, Hysteresis = .3f, Margin = .6f, BodyHeight = 1.8f, WallHeight = 1.1f;
        HouseLayout layout;
        List<float> floors;
        readonly List<(RoomBox room, int storey)> rooms = new();
        readonly List<(Renderer renderer, int storey)> fixedParts = new();
        readonly HashSet<Renderer> fixedSet = new();
        readonly Dictionary<Renderer, CutawaySurface> surfaces = new();
        readonly HashSet<Renderer> flightParts = new();
        readonly List<Renderer> ceilings = new();
        readonly List<Renderer> dynamicParts = new();
        readonly List<PlayerController> locals = new(4);
        readonly VisibilityPlan defaultView = new();
        float next, nextDynamic;

        public sealed class VisibilityPlan
        {
            public readonly HashSet<Renderer> Hidden = new();
            public readonly HashSet<Renderer> Shadows = new();
            public readonly HashSet<string> LiftedRooms = new();
            internal readonly List<(int storey, Rect area)> liftedAreas = new();
            internal readonly Dictionary<PlayerController, int> storeyOf = new();
            internal readonly List<PlayerController> removed = new();
            public int TopStorey { get; internal set; }
            public float FocusY { get; internal set; }
            public bool WouldHide(Renderer renderer) => renderer && (Hidden.Contains(renderer) || Shadows.Contains(renderer));
            public void Clear()
            {
                Hidden.Clear(); Shadows.Clear(); LiftedRooms.Clear(); liftedAreas.Clear();
                TopStorey = 0; FocusY = 0f;
            }
            public void Reset() { Clear(); storeyOf.Clear(); removed.Clear(); }
        }

        public int TopStorey => defaultView.TopStorey;
        public float FocusY => defaultView.FocusY;
        public IReadOnlyCollection<string> LiftedRooms => defaultView.LiftedRooms;

        public void Configure(HouseLayout house, Transform geometry, bool makeDefault = true)
        {
            layout = house;
            floors = house.StoreyFloors();
            rooms.Clear(); fixedParts.Clear(); fixedSet.Clear(); surfaces.Clear(); flightParts.Clear(); ceilings.Clear();
            defaultView.Reset(); nextDynamic = 0f;
            foreach (var room in house.Rooms) rooms.Add((room, house.StoreyOf(room)));
            if (makeDefault) Instance = this;
            if (geometry)
            {
                foreach (var surface in geometry.GetComponentsInChildren<CutawaySurface>(true))
                    foreach (var renderer in surface.Renderers)
                    {
                        if (!renderer) continue;
                        surfaces[renderer] = surface;
                        if (surface.Kind is CutawayKind.Ceiling or CutawayKind.Roof) ceilings.Add(renderer);
                    }
                for (int s = 0; s < floors.Count; s++)
                {
                    var group = geometry.Find(RoomBuilder.StoreyName(s));
                    if (!group) continue;
                    foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                    {
                        fixedParts.Add((renderer, s));
                        fixedSet.Add(renderer);
                        if (renderer.name is "Step" or "Stair rail") flightParts.Add(renderer);
                    }
                }
                foreach (var pair in surfaces)
                    if (fixedSet.Add(pair.Key)) fixedParts.Add((pair.Key, pair.Value.OwnerStorey));
            }
            Refresh();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
        void OnDestroy() { if (Instance == this) Instance = null; }
        void LateUpdate()
        {
            if (layout == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + Interval;
            Refresh();
        }

        public void CollectParticipants(List<PlayerController> destination)
        {
            destination.Clear();
            foreach (var player in World.Players)
                if (BelongsToScene(player) && !player.IsEliminated && player.Binding is not BotBinding) destination.Add(player);
            if (destination.Count == 0)
                foreach (var player in World.Players)
                    if (BelongsToScene(player) && !player.IsEliminated) destination.Add(player);
        }

        bool BelongsToScene(PlayerController player) => player && player.isActiveAndEnabled && player.gameObject.scene == gameObject.scene;

        public void Refresh()
        {
            if (layout == null) return;
            CollectParticipants(locals);
            var rig = CameraRig.Instance;
            bool overhead = !rig || !rig.isActiveAndEnabled || !rig.IsThirdPerson;
            Evaluate(rig ? rig.ViewCamera : null, locals, overhead, defaultView);
        }

        public void Evaluate(Camera camera, IReadOnlyList<PlayerController> participants, bool overhead, VisibilityPlan plan)
        {
            plan.Clear();
            if (layout == null || floors == null || floors.Count == 0) return;
            plan.removed.Clear();
            foreach (var player in plan.storeyOf.Keys)
                if (!player || !Contains(participants, player)) plan.removed.Add(player);
            foreach (var gone in plan.removed) plan.storeyOf.Remove(gone);
            int top = 0, low = int.MaxValue;
            foreach (var player in participants)
            {
                if (!player) continue;
                int storey = StoreyAt(player.transform.position.y);
                if (plan.storeyOf.TryGetValue(player, out int was) && storey < was && StoreyAt(player.transform.position.y + Hysteresis) >= was) storey = was;
                plan.storeyOf[player] = storey;
                top = Mathf.Max(top, storey); low = Mathf.Min(low, storey);
            }
            if (low == int.MaxValue) low = 0;
            plan.TopStorey = top;
            plan.FocusY = (floors[low] + floors[top]) * .5f;
            var projection = new Vector2(0f, -22f / 30f);
            if (camera && -camera.transform.forward.y > .1f)
            {
                var ray = -camera.transform.forward;
                projection = new Vector2(ray.x, ray.z) / ray.y;
            }
            if (overhead) foreach (var player in participants)
            {
                if (!player) continue;
                var at = player.transform.position;
                for (int above = plan.storeyOf[player] + 1; above <= top; above++)
                {
                    var view = ProjectedBody(at, floors[above] - at.y, projection);
                    foreach (var entry in rooms)
                    {
                        if (entry.storey != above) continue;
                        var room = entry.room;
                        var area = Rect.MinMaxRect(room.MinX + .05f, room.MinZ + .05f, room.MaxX - .05f, room.MaxZ - .05f);
                        if (area.Overlaps(view) && plan.LiftedRooms.Add(room.Name)) plan.liftedAreas.Add((above, area));
                    }
                }
            }
            foreach (var part in fixedParts)
            {
                var renderer = part.renderer;
                if (!renderer) continue;
                if (surfaces.TryGetValue(renderer, out var surface))
                {
                    if (surface.OwnerStorey > top) plan.Hidden.Add(renderer);
                    else if (overhead || Obstructs(camera, renderer.bounds, participants)) plan.Shadows.Add(renderer);
                    continue;
                }
                var bounds = renderer.bounds;
                bool hidden = part.storey > top || Lifted(plan, part.storey, Area(bounds));
                if (!hidden && overhead && flightParts.Contains(renderer))
                    foreach (var player in participants)
                    {
                        if (!player) continue;
                        var at = player.transform.position;
                        float over = bounds.max.y - at.y;
                        if (plan.storeyOf[player] == part.storey && over >= WallHeight && Area(bounds).Overlaps(ProjectedBody(at, over, projection)))
                        { hidden = true; break; }
                    }
                if (hidden) plan.Hidden.Add(renderer);
            }
            RefreshDynamicParts();
            foreach (var renderer in dynamicParts)
            {
                if (!renderer) continue;
                var bounds = renderer.bounds;
                int storey = StoreyAt(bounds.min.y);
                if (storey <= top && !Lifted(plan, storey, Area(bounds))) continue;
                bool mine = false;
                foreach (var player in participants)
                    if (player && renderer.transform.IsChildOf(player.transform)) { mine = true; break; }
                if (!mine) plan.Hidden.Add(renderer);
            }
        }

        void RefreshDynamicParts()
        {
            if (Time.unscaledTime < nextDynamic) return;
            nextDynamic = Time.unscaledTime + Interval;
            dynamicParts.Clear();
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (fixedSet.Contains(renderer) || renderer.gameObject.scene != gameObject.scene) continue;
                var canvas = renderer.GetComponentInParent<Canvas>();
                if (canvas && canvas.renderMode != RenderMode.WorldSpace) continue;
                dynamicParts.Add(renderer);
            }
        }

        internal static bool Obstructs(Camera camera, Bounds bounds, IReadOnlyList<PlayerController> participants)
        {
            if (!camera) return false;
            foreach (var player in participants)
            {
                if (!player) continue;
                var delta = player.transform.position + Vector3.up * .9f - camera.transform.position;
                if (bounds.IntersectRay(new Ray(camera.transform.position, delta.normalized), out float distance) && distance < delta.magnitude) return true;
            }
            return false;
        }

        /// <summary>Ceilings constrain the camera without introducing new gameplay colliders.</summary>
        public float ProbeCeilings(Vector3 from, Vector3 direction, float reach, float radius)
        {
            float available = reach;
            foreach (var renderer in ceilings)
                available = ProbeCeiling(renderer, from, direction, available, radius);
            return available;
        }

        internal static float ProbeCeiling(Renderer renderer, Vector3 from, Vector3 direction, float reach, float radius)
        {
            if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return reach;
            var bounds = renderer.bounds;
            bounds.Expand(radius * 2f);
            if (bounds.Contains(from)) return reach;
            return bounds.IntersectRay(new Ray(from, direction), out float distance) && distance >= 0f
                ? Mathf.Min(reach, Mathf.Max(0f, distance - ShoulderView.Skin)) : reach;
        }

        static Rect Area(Bounds bounds) => Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
        static Rect ProjectedBody(Vector3 at, float rise, Vector2 projection)
        {
            var feet = new Vector2(at.x, at.z) + projection * rise;
            var head = new Vector2(at.x, at.z) + projection * Mathf.Max(0f, rise - BodyHeight);
            return Rect.MinMaxRect(Mathf.Min(feet.x, head.x) - Margin, Mathf.Min(feet.y, head.y) - Margin,
                Mathf.Max(feet.x, head.x) + Margin, Mathf.Max(feet.y, head.y) + Margin);
        }
        static bool Contains(IReadOnlyList<PlayerController> players, PlayerController target)
        { foreach (var player in players) if (player == target) return true; return false; }
        static bool Lifted(VisibilityPlan plan, int storey, Rect area)
        { foreach (var entry in plan.liftedAreas) if (entry.storey == storey && entry.area.Overlaps(area)) return true; return false; }
        int StoreyAt(float y)
        {
            int storey = 0;
            if (floors != null) for (int i = 1; i < floors.Count; i++) if (floors[i] <= y + HouseLayout.StandingSlack) storey = i;
            return storey;
        }
        public int StoreyOfPlayer(PlayerController player) => player && defaultView.storeyOf.TryGetValue(player, out int storey)
            ? storey : player ? StoreyAt(player.transform.position.y) : 0;
        public bool Draws(Renderer renderer) => !defaultView.WouldHide(renderer);
    }
}
