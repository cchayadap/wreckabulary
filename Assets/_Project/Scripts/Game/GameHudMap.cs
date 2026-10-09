using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud
    {
        sealed class MapRoom
        {
            public RoomBox box;
            public Image fill;
            public TextMeshProUGUI label;
            public Color colour;
            public bool objective;
            public readonly List<Image> walls = new();
        }

        sealed class MapProp
        {
            public Image image;
            public Collider[] colliders;
            public Keepsake keepsake;
        }

        sealed class MapPlayer
        {
            public RectTransform root;
            public Image ring, dot, facing;
            public TextMeshProUGUI level;
            public PlayerController player;
            public int otherFloor = int.MinValue;
        }

        sealed class MapView
        {
            public RectTransform content, plan, propLayer, labelLayer, markerLayer;
            public readonly List<MapRoom> rooms = new();
            public readonly List<MapPlayer> dots = new();
            public readonly Dictionary<Smashable, MapProp> props = new();
            public readonly List<Smashable> stale = new();
            public readonly List<Smashable> transientProps = new();
            public readonly HashSet<Smashable> visitedProps = new();
            public TextMeshProUGUI badge;
            public Image extraction;
            public bool full;
            public HouseLayout layout;
            public List<float> floors;
            public MovingOutDirector movingOut;
            public MovingDayDirector movingDay;
            public int storey = -1;
            public float originX, originZ, extent, nextProps, nextTransient;
            public Vector2 At(float x, float z) => new((x - originX) / extent, (z - originZ) / extent);
            public int Storey(float y)
            {
                int result = 0;
                for (int i = 1; i < floors.Count; i++) if (floors[i] <= y + HouseLayout.StandingSlack) result = i;
                return result;
            }
        }

        Sprite mapPointer;

        MapView MakeMap(RectTransform frame, bool full, float border)
        {
            var edge = frame.GetComponent<Image>();
            float scale = edge.pixelsPerUnitMultiplier;
            edge.sprite = MakeShape(false, border * scale);
            var bg = Panel("Floor", frame, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f), MapBg);
            Stretch(bg); bg.offsetMin = Vector2.one * border; bg.offsetMax = -Vector2.one * border;
            bg.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / Mathf.Max(1f, 11f / scale - border);
            bg.gameObject.AddComponent<RectMask2D>();
            if (!mapPointer) mapPointer = MakeMapPointer();
            return new MapView { content = bg, full = full };
        }

        void UpdateMap(MapView map)
        {
            if (map == null) return;
            var layout = Layout;
            bool visible = layout != null && layout.Rooms.Count > 0;
            if (map.content.parent.gameObject.activeSelf != visible) map.content.parent.gameObject.SetActive(visible);
            if (!visible) return;
            if (map.layout != layout)
            {
                map.layout = layout;
                map.floors = layout.StoreyFloors();
                map.movingOut = FindAnyObjectByType<MovingOutDirector>();
                map.movingDay = FindAnyObjectByType<MovingDayDirector>();
                map.storey = -1;
            }
            var cutaway = StoreyCutaway.Instance;
            int FloorOf(PlayerController who) => cutaway ? cutaway.StoreyOfPlayer(who) : map.Storey(who.transform.position.y);
            int storey = LocalPlayer && map.floors.Count > 1 ? FloorOf(LocalPlayer) : 0;
            if (map.storey != storey) BuildMapPlan(map, storey);

            string here = LocalPlayer ? RoomOf(layout, LocalPlayer) : null;
            var schedule = clearOut && clearOut.Running ? clearOut.Schedule : null;
            foreach (var room in map.rooms)
            {
                var phase = schedule?.PhaseOf(room.box.Name, clearOut.Elapsed) ?? RoomPhase.Safe;
                Color safe = room.box.Name == here ? Color.Lerp(room.colour, Hex(0xa8e4c3), .42f) : room.colour;
                room.fill.color = phase is RoomPhase.Closed or RoomPhase.Filling ? RoomClosed
                    : phase == RoomPhase.Warning ? Color.Lerp(RoomWarn, safe, Mathf.PingPong(Time.unscaledTime * 1.4f, 1f)) : safe;
                foreach (var wall in room.walls) wall.color = room.box.Name == here ? Hex(0xf9ebbf) : Hex(0xc9b895);
            }
            if (Time.unscaledTime >= map.nextProps)
            {
                map.nextProps = Time.unscaledTime + .25f;
                UpdateMapProps(map);
                UpdateMapObjectives(map);
            }
            int n = 0;
            foreach (var player in World.Players)
            {
                if (!player || player.IsEliminated) continue;
                if (n == map.dots.Count) map.dots.Add(MakeMapPlayer(map));
                var marker = map.dots[n++];
                marker.root.gameObject.SetActive(true);
                if (marker.player != player) { marker.player = player; marker.root.name = "Player " + (player.Index + 1); }
                bool you = player == LocalPlayer;
                int level = FloorOf(player) - storey;
                var at = map.At(player.transform.position.x, player.transform.position.z);
                marker.root.anchorMin = marker.root.anchorMax = at;
                float size = map.full ? (you ? 18f : 12f) : (you ? 11f : 7f);
                marker.root.sizeDelta = Vector2.one * size;
                Color colour = player.IsDowned ? Coral : you ? Hex(0x99ffcf) : player.Color;
                colour.a = level == 0 ? 1f : .4f;
                marker.dot.color = marker.facing.color = colour;
                marker.ring.color = you ? Cream : Hex(0x163e3e, level == 0 ? 1f : .4f);
                marker.ring.rectTransform.sizeDelta = Vector2.one * (you ? 5f : 3f);
                marker.facing.rectTransform.anchoredPosition = new Vector2(0f, size * .5f + (map.full ? 7f : 5f));
                marker.root.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(player.Facing.x, player.Facing.z) * Mathf.Rad2Deg);
                marker.level.rectTransform.rotation = Quaternion.identity;
                if (marker.otherFloor != level)
                {
                    marker.otherFloor = level;
                    marker.level.text = level == 0 ? "" : level > 0 ? "+" + level : level.ToString();
                }
            }
            for (int i = n; i < map.dots.Count; i++) map.dots[i].root.gameObject.SetActive(false);
        }

        void BuildMapPlan(MapView map, int storey)
        {
            if (map.plan) { map.plan.gameObject.SetActive(false); Destroy(map.plan.gameObject); }
            map.rooms.Clear(); map.props.Clear(); map.stale.Clear(); map.dots.Clear();
            map.transientProps.Clear(); map.visitedProps.Clear();
            map.storey = storey; map.nextProps = map.nextTransient = 0f;
            float minX = float.PositiveInfinity, minZ = float.PositiveInfinity, maxX = float.NegativeInfinity, maxZ = float.NegativeInfinity;
            // Keep the same projection on every floor so stairs and player locations do not jump on a storey change.
            foreach (var room in map.layout.Rooms)
            {
                minX = Mathf.Min(minX, room.MinX); maxX = Mathf.Max(maxX, room.MaxX);
                minZ = Mathf.Min(minZ, room.MinZ); maxZ = Mathf.Max(maxZ, room.MaxZ);
            }
            map.extent = Mathf.Max(maxX - minX, maxZ - minZ) * 1.15f;
            map.originX = (minX + maxX - map.extent) * .5f;
            map.originZ = (minZ + maxZ - map.extent) * .5f;
            map.plan = MapLayer("House plan", map.content);
            var floors = MapLayer("Rooms", map.plan);
            map.propLayer = MapLayer("Furniture", map.plan);
            var walls = MapLayer("Walls and passages", map.plan);
            var stairs = MapLayer("Stairs", map.plan);
            map.labelLayer = MapLayer("Room names", map.plan);
            foreach (var box in map.layout.Rooms)
            {
                if (map.Storey(box.FloorY) != storey) continue;
                var room = new MapRoom { box = box, colour = MapRoomColour(box.Name) };
                room.fill = MapArea(box.Name, floors, map, box.MinX, box.MinZ, box.MaxX, box.MaxZ, room.colour);
                var label = Text(box.Name, map.labelLayer, map.At((box.MinX + box.MaxX) * .5f, box.MaxZ),
                    new Vector2(0f, map.full ? -13f : -8f), Vector2.zero, map.full ? 16f : 10f, Cream);
                label.rectTransform.anchorMin = map.At(box.MinX, box.MaxZ);
                label.rectTransform.anchorMax = map.At(box.MaxX, box.MaxZ);
                label.rectTransform.sizeDelta = new Vector2(-6f, map.full ? 22f : 14f);
                label.text = map.full ? Spaced(box.Name).ToUpperInvariant() : MapRoomName(box.Name);
                label.fontStyle = FontStyles.Bold; label.enableAutoSizing = true;
                label.fontSizeMin = map.full ? 10f : 8f; label.fontSizeMax = map.full ? 16f : 10f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                room.label = label;
                AddMapWall(map, room, walls, true, box.MinX, box.MinZ, box.MaxZ);
                AddMapWall(map, room, walls, true, box.MaxX, box.MinZ, box.MaxZ);
                AddMapWall(map, room, walls, false, box.MinZ, box.MinX, box.MaxX);
                AddMapWall(map, room, walls, false, box.MaxZ, box.MinX, box.MaxX);
                map.rooms.Add(room);
            }
            foreach (var flight in map.layout.Stairs)
            {
                var lower = map.layout.Room(flight.Lower); var upper = map.layout.Room(flight.Upper);
                if (map.Storey(lower.FloorY) != storey && map.Storey(upper.FloorY) != storey) continue;
                var area = MapArea("Stairs " + flight.Lower + " to " + flight.Upper, stairs, map,
                    flight.MinX, flight.MinZ, flight.MaxX, flight.MaxZ, Hex(0xd7bf86, .78f)).rectTransform;
                bool alongZ = Mathf.Abs(flight.ToX - flight.FromX) < .01f;
                for (int i = 1; i < 7; i++)
                {
                    var tread = CreateImage("Tread", area, Vector2.zero, Vector2.zero, Vector2.zero, Hex(0x27494a));
                    tread.rectTransform.anchorMin = alongZ ? new Vector2(.08f, i / 7f) : new Vector2(i / 7f, .08f);
                    tread.rectTransform.anchorMax = alongZ ? new Vector2(.92f, i / 7f) : new Vector2(i / 7f, .92f);
                    tread.rectTransform.sizeDelta = alongZ ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
                }
                var arrow = CreateImage("Stair direction", area, Vector2.one * .5f, Vector2.zero, Vector2.one * (map.full ? 15f : 10f), Cream);
                arrow.sprite = mapPointer;
                float direction = Mathf.Atan2(flight.ToX - flight.FromX, flight.ToZ - flight.FromZ) * Mathf.Rad2Deg;
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -direction + (map.Storey(lower.FloorY) == storey ? 0f : 180f));
            }
            map.markerLayer = MapLayer("Markers", map.plan);
            map.extraction = null;
            if ((map.movingOut || map.movingDay) && map.layout.ExtractionRoom != null)
            {
                map.extraction = Panel("Delivery point", map.markerLayer, Vector2.zero, Vector2.zero,
                    Vector2.one * (map.full ? 23f : 14f), Vector2.one * .5f, Hex(0xf6ce6d), true).GetComponent<Image>();
                var text = Text("Symbol", map.extraction.transform, Vector2.one * .5f, Vector2.zero,
                    Vector2.one * (map.full ? 30f : 20f), map.full ? 11f : 9f, Ink);
                text.text = map.movingOut ? "V" : "+"; text.fontStyle = FontStyles.Bold;
            }
            map.badge = Text("Storey", map.plan, new Vector2(0f, 1f), new Vector2(5f, -3f),
                new Vector2(190f, map.full ? 22f : 14f), map.full ? 14f : 9f, Cream, TextAlignmentOptions.TopLeft);
            map.badge.rectTransform.pivot = new Vector2(0f, 1f);
            map.badge.text = map.floors.Count > 1 ? map.layout.StoreyLabel(storey) : "";
            var north = Text("North", map.plan, Vector2.one, new Vector2(-5f, -3f), new Vector2(16f, 14f), map.full ? 11f : 9f, Cream);
            north.rectTransform.pivot = Vector2.one; north.text = "N";
        }

        RectTransform MapLayer(string name, Transform parent)
        {
            var layer = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(layer); return layer;
        }

        Image MapArea(string name, Transform parent, MapView map, float x0, float z0, float x1, float z1, Color colour)
        {
            var image = CreateImage(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, colour);
            image.rectTransform.anchorMin = map.At(x0, z0); image.rectTransform.anchorMax = map.At(x1, z1);
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            return image;
        }

        void AddMapWall(MapView map, MapRoom room, Transform parent, bool vertical, float fixedAt, float from, float to)
        {
            var openings = new List<Vector2>();
            foreach (var door in map.layout.Doors)
                if ((door.A == room.box.Name || door.B == room.box.Name) && Mathf.Abs((vertical ? door.X : door.Z) - fixedAt) < .01f)
                {
                    float centre = vertical ? door.Z : door.X;
                    openings.Add(new Vector2(Mathf.Max(from, centre - door.Width * .5f), Mathf.Min(to, centre + door.Width * .5f)));
                }
            openings.Sort((a, b) => a.x.CompareTo(b.x));
            float cursor = from;
            void Segment(float begin, float end)
            {
                if (end - begin < .01f) return;
                var wall = MapArea("Wall " + room.box.Name, parent, map,
                    vertical ? fixedAt : begin, vertical ? begin : fixedAt,
                    vertical ? fixedAt : end, vertical ? end : fixedAt, Hex(0xc9b895));
                wall.rectTransform.sizeDelta = vertical ? new Vector2(map.full ? 3f : 2f, 0f) : new Vector2(0f, map.full ? 3f : 2f);
                room.walls.Add(wall);
            }
            foreach (var opening in openings) { Segment(cursor, opening.x); cursor = Mathf.Max(cursor, opening.y); }
            Segment(cursor, to);
        }

        void UpdateMapProps(MapView map)
        {
            // Discover new furniture at a bounded rate; the cached colliders still track movement four times a second.
            if (Time.unscaledTime >= map.nextTransient)
            {
                map.nextTransient = Time.unscaledTime + 1f;
                World.Transient.GetComponentsInChildren(false, map.transientProps);
            }
            map.visitedProps.Clear();
            if (rooms) foreach (var prop in rooms.Originals) UpdateMapProp(map, prop);
            foreach (var prop in map.transientProps) UpdateMapProp(map, prop);
            map.stale.Clear();
            foreach (var entry in map.props)
                if (!map.visitedProps.Contains(entry.Key)) map.stale.Add(entry.Key);
            foreach (var gone in map.stale) { Destroy(map.props[gone].image.gameObject); map.props.Remove(gone); }
        }

        void UpdateMapProp(MapView map, Smashable prop)
        {
            if (!prop || prop.IsBroken || !prop.gameObject.activeInHierarchy || !map.visitedProps.Add(prop)) return;
            if (!map.props.TryGetValue(prop, out var view))
            {
                view = new MapProp { colliders = prop.GetComponentsInChildren<Collider>(),
                    image = CreateImage("Obstacle " + prop.Word + " " + prop.GetHashCode(), map.propLayer, Vector2.zero, Vector2.zero, Vector2.zero, Hex(0x284c49, .7f)) };
                map.props.Add(prop, view);
            }
            bool found = false; Bounds bounds = default;
            foreach (var collider in view.colliders)
            {
                if (!collider || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
                if (found) bounds.Encapsulate(collider.bounds); else { bounds = collider.bounds; found = true; }
            }
            bool show = found && map.Storey(bounds.min.y) == map.storey && bounds.size.x >= .4f && bounds.size.z >= .4f && bounds.size.y >= .2f;
            if (view.image.gameObject.activeSelf != show) view.image.gameObject.SetActive(show);
            if (!show) return;
            view.image.rectTransform.anchorMin = map.At(bounds.min.x, bounds.min.z);
            view.image.rectTransform.anchorMax = map.At(bounds.max.x, bounds.max.z);
            view.image.rectTransform.offsetMin = Vector2.one * .5f; view.image.rectTransform.offsetMax = -Vector2.one * .5f;
            if (map.movingOut && !view.keepsake) view.keepsake = prop.GetComponent<Keepsake>();
            view.image.color = view.keepsake && !view.keepsake.Packed ? Hex(0xf6ce6d) : Hex(0x284c49, .7f);
        }

        void UpdateMapObjectives(MapView map)
        {
            foreach (var room in map.rooms)
            {
                bool pending = false;
                if (map.movingDay)
                    foreach (var objective in map.movingDay.Remaining)
                        if (objective.room == room.box.Name) { pending = true; break; }
                if (room.objective == pending) continue;
                room.objective = pending;
                room.label.color = pending ? Hex(0xffd878) : Cream;
                room.label.text = (pending ? "+ " : "") + (map.full ? Spaced(room.box.Name).ToUpperInvariant() : MapRoomName(room.box.Name));
            }
            if (!map.extraction) return;
            var roomAt = map.layout.Room(map.layout.ExtractionRoom);
            var position = map.movingOut ? map.movingOut.ExtractionPoint : new Vector3(map.layout.ExtractionX, roomAt.FloorY, map.layout.ExtractionZ);
            map.extraction.gameObject.SetActive(map.Storey(position.y) == map.storey);
            map.extraction.rectTransform.anchorMin = map.extraction.rectTransform.anchorMax = map.At(position.x, position.z);
        }

        MapPlayer MakeMapPlayer(MapView map)
        {
            var root = Rect("Player", map.markerLayer, Vector2.zero, Vector2.zero, Vector2.one * 10f);
            var ring = Panel("Outline", root, Vector2.one * .5f, Vector2.zero, Vector2.zero, Vector2.one * .5f, Cream, true).GetComponent<Image>();
            Stretch(ring.rectTransform);
            var dot = Panel("Dot", root, Vector2.one * .5f, Vector2.zero, Vector2.zero, Vector2.one * .5f, Cream, true).GetComponent<Image>();
            Stretch(dot.rectTransform);
            var facing = CreateImage("Facing", root, Vector2.one * .5f, Vector2.zero, new Vector2(map.full ? 11f : 8f, map.full ? 13f : 9f), Cream);
            facing.sprite = mapPointer;
            var level = Text("Other floor", root, Vector2.one * .5f, new Vector2(10f, 0f), new Vector2(22f, 18f), map.full ? 12f : 9f, Cream);
            return new MapPlayer { root = root, ring = ring, dot = dot, facing = facing, level = level };
        }

        Sprite MakeMapPointer()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Map facing pointer", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float half = (size - 2f - y) * .45f;
                float alpha = Mathf.Clamp01(half - Mathf.Abs(x - (size - 1f) * .5f));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100f);
            ownedTextures.Add(texture); ownedSprites.Add(sprite); return sprite;
        }

        static string MapRoomName(string name) => name switch
        {
            "LivingRoom" => "LIVING", "Bedroom" => "BED", "Bathroom" => "BATH", "DiningRoom" => "DINING",
            "Kitchen" => "KITCHEN", "Playroom" => "PLAY", _ => Spaced(name).ToUpperInvariant()
        };

        static Color MapRoomColour(string name)
        {
            if (name.Contains("Garden") || name.Contains("Terrace") || name.Contains("Patio")) return Hex(0x638c6c);
            if (name.Contains("Kitchen") || name.Contains("Bath")) return Hex(0x678f96);
            if (name.Contains("Bed") || name.Contains("Play")) return Hex(0x8d829f);
            if (name.Contains("Hall") || name.Contains("Stair") || name.Contains("Landing")) return Hex(0x968b6b);
            return Hex(0xa38e71);
        }
    }
}
