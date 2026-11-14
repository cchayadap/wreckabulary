using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class EscapeDirector : MonoBehaviour
    {
        public enum State { Waiting, Countdown, Playing, Complete, Failed }

        sealed class LockedDoor
        {
            public Doorway Door;
            public string Word;
            public bool Open;
        }

        sealed class DoorVisual
        {
            public Doorway Door;
            public Transform Hinge;
            public Collider Leaf;
            public GameObject Padlock;
            public Renderer Render;
            public Quaternion Closed, Opened;
            public bool Barred, Open;
            public float Swing = 1f;
        }

        const float UnlockRadius = 5.5f;
        const float BarredWordScale = 4.2f;

        const bool DoorDiagnostics = true;

        PlayerJoinManager joins;
        RoomBuilder room;
        DeliverySpawner deliveries;
        GameHud hud;
        ClearOutController clearOut;
        Vector3 extraction;
        string spawnRoom;
        int seed;
        float began, nextCheck, finishedAt, nextLetterCheck, nextRoomCheck, nextGuideCheck, nextLookCheck;
        readonly List<LockedDoor> doors = new();
        readonly List<DoorVisual> visuals = new();
        readonly HashSet<int> hooked = new();
        readonly List<GameObject> spawned = new();

        DoorVisual exitDoor;
        GameObject exitCircle, exitLabel;
        string exitRoom;
        Vector3 exitCentre, exitOutward;
        float exitYaw, exitWidth = 1.8f;
        bool exitGated;

        sealed class KeyProp
        {
            public GameObject Root, Beacon;
            public string Room;
        }
        readonly List<KeyProp> keys = new();
        int keysNeeded, keysFound;
        string guideRoom;
        LockedDoor guideDoor;
        bool exitOpen;

        public State Current { get; private set; }
        public Vector3 ExtractionPoint => extraction;
        public int Seed => seed;
        public int DoorsOpen => doors.Count(d => d.Open);
        public int DoorCount => doors.Count;
        public int KeysFound => keysFound;
        public int KeysNeeded => keysNeeded;
        public bool AllKeysFound => keysFound >= keysNeeded;
        public bool ExitOpen => exitOpen;
        public string KeyRoom => NextKeyRoom;
        public string GuideRoom => guideRoom;
        public bool ExitRevealed => !exitGated || AllKeysFound;
        public float TimeLeft => Mathf.Max(0f, Match.Rules.RoundTimeLimitSeconds - (Time.time - began));

        public void Configure(PlayerJoinManager players, RoomBuilder builder, DeliverySpawner boxes, GameHud display)
        {
            joins = players; room = builder; deliveries = boxes; hud = display;
        }

        void Start()
        {
            deliveries.Running = false;
            clearOut = gameObject.AddComponent<ClearOutController>();
            clearOut.Configure(room.Layout, "Escape");
            clearOut.ShowIndicators = false;
            clearOut.Harms = false;
            exitRoom = room.Layout.ExtractionRoom;
            ConfigureExit();
            joins.Joined += OnJoined;
            foreach (var p in joins.Players) HookSummoner(p);
            joins.RespawnKnockedOut = false;
            CreateExitMarker();
            BuildNotice();
            Debug.Log($"[Escape] build v11 (hinged doors, key exit, guide) needsKey={Match.Rules.EscapeNeedsKey}");
            Debug.Log($"[Escape] start house={room.Layout.Name} spawn={spawnRoom} exit={room.Layout.ExtractionRoom} layoutDoors={room.Layout.Doors.Count} humans={joins.HumanCount}");
            Current = State.Waiting;
            if (joins.HumanCount > 0) Restart();
        }

        string PickStartRoom()
        {
            var layout = room.Layout;
            var options = new List<string>();
            foreach (var spawn in layout.Spawns)
                if (!string.IsNullOrEmpty(spawn.Room) && layout.Room(spawn.Room) != null && !options.Contains(spawn.Room))
                    options.Add(spawn.Room);
            if (options.Count > 0) return options[Random.Range(0, options.Count)];

            RoomBox best = null;
            float bestDistance = -1f;
            foreach (var box in layout.Rooms)
            {
                float distance = Vector2.Distance(Centre(box), new Vector2(layout.ExtractionX, layout.ExtractionZ));
                if (distance > bestDistance) { bestDistance = distance; best = box; }
            }
            return best != null ? best.Name : layout.ExtractionRoom;
        }

        string PickExitRoom(string spawn)
        {
            var layout = room.Layout;
            var candidates = new List<(string name, int doors)>();
            foreach (var box in layout.Rooms)
            {
                if (box.Name == spawn || box.Outdoor || !HasOutsideWall(box)) continue;
                int doors = string.IsNullOrEmpty(spawn) ? 2 : Escape.PathDoors(layout, spawn, box.Name).Count;
                candidates.Add((box.Name, doors));
            }
            if (candidates.Count == 0) return layout.ExtractionRoom;

            int farthest = 0;
            foreach (var entry in candidates) farthest = Mathf.Max(farthest, entry.doors);
            int floor = farthest >= 2 ? Mathf.Max(2, farthest - 1) : 1;
            var far = candidates.FindAll(c => c.doors >= floor);
            if (far.Count == 0) far = candidates;
            return far[Random.Range(0, far.Count)].name;
        }

        (Vector3 centre, float yaw, Vector3 outward, float length)[] Walls(RoomBox box)
        {
            float y = box.FloorY;
            return new (Vector3 centre, float yaw, Vector3 outward, float length)[]
            {
                (new Vector3((box.MinX + box.MaxX) * .5f, y, box.MinZ), 0f, Vector3.back, box.MaxX - box.MinX),
                (new Vector3((box.MinX + box.MaxX) * .5f, y, box.MaxZ), 0f, Vector3.forward, box.MaxX - box.MinX),
                (new Vector3(box.MinX, y, (box.MinZ + box.MaxZ) * .5f), 90f, Vector3.left, box.MaxZ - box.MinZ),
                (new Vector3(box.MaxX, y, (box.MinZ + box.MaxZ) * .5f), 90f, Vector3.right, box.MaxZ - box.MinZ),
            };
        }

        bool IsOutside(Vector3 point)
        {
            string at = room.Layout.RoomAt(point.x, point.y, point.z);
            return string.IsNullOrEmpty(at) || room.Layout.IsOutdoor(at);
        }

        bool HasOutsideWall(RoomBox box)
        {
            foreach (var wall in Walls(box))
                if (IsOutside(wall.centre + wall.outward * .6f + Vector3.up * .5f)) return true;
            return false;
        }

        static Vector2 Centre(RoomBox box) => new Vector2((box.MinX + box.MaxX) * .5f, (box.MinZ + box.MaxZ) * .5f);

        Vector3 StartSpot(int index)
        {
            var box = room.Layout.Room(spawnRoom);
            if (box == null) return extraction + Vector3.up;
            var centre = Centre(box);
            float radius = Mathf.Min(2.4f, Mathf.Min(box.MaxX - box.MinX, box.MaxZ - box.MinZ) * .3f);
            float angle = index * 1.7f;
            var spot = new Vector2(centre.x + Mathf.Cos(angle) * radius, centre.y + Mathf.Sin(angle) * radius);
            spot.x = Mathf.Clamp(spot.x, box.MinX + .6f, box.MaxX - .6f);
            spot.y = Mathf.Clamp(spot.y, box.MinZ + .6f, box.MaxZ - .6f);
            return new Vector3(spot.x, box.FloorY + .2f, spot.y);
        }

        void HookSummoner(PlayerController p)
        {
            if (p == null || !p.Summoner || !hooked.Add(p.Index)) return;
            p.Summoner.Summoned += word => OnSummoned(p, word);
        }

        void OnSummoned(PlayerController p, string word)
        {
            if (Current != State.Playing || p == null || string.IsNullOrEmpty(word)) return;

            int closest = -1;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i].Open) continue;
                float distance = Vector3.Distance(p.transform.position, DoorCentre(doors[i].Door));
                if (distance < closestDistance) { closestDistance = distance; closest = i; }
            }

            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (door.Open || !string.Equals(door.Word, word, System.StringComparison.OrdinalIgnoreCase)) continue;
                float distance = Vector3.Distance(p.transform.position, DoorCentre(door.Door));
                if (distance <= UnlockRadius || (i == closest && distance <= 8f))
                {
                    Debug.Log($"[Escape] door '{door.Word}' opened by spelling it {distance:0.0}m away");
                    OpenDoor(door, i);
                    return;
                }
            }
            Debug.Log($"[Escape] spelled '{word}' but no matching barred door was close enough");
        }

        void OnJoined(PlayerController p)
        {
            HookSummoner(p);
            joins.AssignTeams();
            p.Health.ResetForRound();
            p.Frozen = Current != State.Playing;
        }

        public void Restart()
        {
            if (Time.timeScale <= 0f || joins.HumanCount == 0) return;
            room.ResetRoom();
            clearOut.ResetSchedule();
            hud.HideResult();
            hud.HideCountdown();
            MatchTally.BeginMatch();
            ClearRunObjects();
            seed = Random.Range(1, 999999);
            spawnRoom = PickStartRoom();
            exitRoom = Match.Rules.EscapeRandomExit && Match.Rules.EscapeNeedsKey ? PickExitRoom(spawnRoom) : room.Layout.ExtractionRoom;
            ConfigureExit();
            MoveExitMarker();
            ShuffleFurniture(rng: new System.Random(seed));
            SetUpDoors();
            BuildAllDoors();
            BuildKeys();
            guideRoom = NextKeyRoom;
            SyncExitMarker();
            joins.AssignTeams();
            foreach (var p in joins.Players) { joins.Place(p); p.Respawn(StartSpot(p.Index)); p.Frozen = true; }
            joins.AllowJoining = true;
            joins.RespawnKnockedOut = false;
            Current = State.Countdown;
            began = Time.time;
        }

        void ClearRunObjects()
        {
            foreach (var go in spawned) if (go) Destroy(go);
            spawned.Clear();
            doors.Clear();
            visuals.Clear();
            exitDoor = null;
            keys.Clear();
            guideRoom = null;
            guideDoor = null;
            keysNeeded = keysFound = 0;
            exitOpen = false;
            var pool = TilePool.Instance;
            if (pool) pool.ReleaseAll();
        }

        void SetUpDoors()
        {
            var layout = room.Layout;
            var rng = new System.Random(seed);
            var used = new List<string>();
            var reachable = new HashSet<string>();
            var pending = new List<Doorway>(layout.Doors);
            AddRoom(reachable, spawnRoom);

            var pools = new Dictionary<int, List<string>>();
            foreach (var item in GameConfig.Current.Items.Enabled)
            {
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;
                int size = item.Id.Length;
                if (size < 3 || size > 5) continue;
                if (!pools.TryGetValue(size, out var list)) pools[size] = list = new List<string>();
                if (!list.Contains(item.Id)) list.Add(item.Id);
            }

            var houseWords = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (room.Originals != null)
                foreach (var prop in room.Originals)
                    if (prop && !string.IsNullOrEmpty(prop.Word)) houseWords.Add(prop.Word);

            int index = 0;
            while (pending.Count > 0)
            {
                Doorway pick = null;
                foreach (var d in pending)
                {
                    bool a = reachable.Contains(d.A), b = reachable.Contains(d.B);
                    if (a != b) { pick = d; break; }
                }
                if (pick == null)
                    foreach (var d in pending)
                        if (reachable.Contains(d.A) && reachable.Contains(d.B)) { pick = d; break; }
                if (pick == null) pick = pending[0];

                pending.Remove(pick);

                int length = Escape.DoorLengths[index % Escape.DoorLengths.Length];
                string word = PickWord(pools, length, rng, used, houseWords);
                used.Add(word);

                var door = new LockedDoor { Door = pick, Word = word };
                doors.Add(door);

                StockDoor(word, pick, reachable, rng);
                AddRoom(reachable, pick.A);
                AddRoom(reachable, pick.B);
                index++;
            }

            Debug.Log($"[Escape] every doorway is barred: {doors.Count} doors | layout doors={layout.Doors.Count}");
            foreach (var d in doors)
                Debug.Log($"[Escape] door '{d.Word}' between {d.Door.A}/{d.Door.B} at {DoorCentre(d.Door)}");
        }

        static string PickWord(Dictionary<int, List<string>> pools, int length, System.Random rng, List<string> used, HashSet<string> avoid)
        {
            if (pools.TryGetValue(length, out var pool) && pool.Count > 0)
            {
                var spare = new List<string>();
                foreach (var word in pool)
                    if (!used.Contains(word) && (avoid == null || !avoid.Contains(word))) spare.Add(word);
                if (spare.Count == 0)
                    foreach (var word in pool) if (!used.Contains(word)) spare.Add(word);
                if (spare.Count == 0) spare = pool;
                return spare[rng.Next(spare.Count)];
            }
            return Escape.PickWord(length, rng, used);
        }

        void BuildAllDoors()
        {
            foreach (var doorway in room.Layout.Doors)
            {
                LockedDoor locked = null;
                foreach (var door in doors)
                    if (ReferenceEquals(door.Door, doorway) || (door.Door.A == doorway.A && door.Door.B == doorway.B)) { locked = door; break; }
                BuildDoor(doorway, locked);
            }
        }

        void BuildDoor(Doorway d, LockedDoor locked)
        {
            var box = room.Layout.Room(d.A) ?? room.Layout.Room(d.B);
            float y = box != null ? box.FloorY : 0f;
            bool barred = locked != null;

            float yaw = DoorYaw(d);
            float width = Mathf.Max(1.2f, d.Width);
            MeasureDoorway(d, y + 1.15f, ref yaw, ref width);
            width = Mathf.Max(width, d.Width);

            var rot = Quaternion.Euler(0f, yaw, 0f);
            var centre = new Vector3(d.X, y, d.Z);

            var visual = BuildLeaf(centre, rot, width, barred, new Color(.85f, .2f, .18f));
            visual.Door = d;
            visuals.Add(visual);

            Debug.Log($"[Escape] door {d.A}/{d.B} yaw={yaw:0} width={width:0.00} centre={centre} leaf={visual.Render.transform.position}"
                + $" visible={visual.Render.gameObject.activeInHierarchy && visual.Render.enabled} mat={(visual.Render.sharedMaterial ? visual.Render.sharedMaterial.name : "none")}"
                + $" colour={visual.Render.sharedMaterial.color} worldSize={visual.Render.bounds.size}"
                + $" layer={visual.Render.gameObject.layer} parent={visual.Render.transform.parent.name} parentActive={transform.gameObject.activeInHierarchy} barred={barred}");
        }

        DoorVisual BuildLeaf(Vector3 centre, Quaternion rot, float width, bool barred, Color lockColour)
        {
            const float DoorHeight = 2.6f;
            const float LeafThickness = .80f;
            float half = width * .5f;

            Piece("Door post", centre + rot * new Vector3(-half - .13f, 1.25f, 0f), rot, new Vector3(.26f, 2.5f, .5f), new Color(.24f, .15f, .1f));
            Piece("Door post", centre + rot * new Vector3(half + .13f, 1.25f, 0f), rot, new Vector3(.26f, 2.5f, .5f), new Color(.24f, .15f, .1f));
            Piece("Door lintel", centre + Vector3.up * 2.55f, rot, new Vector3(width + .8f, .26f, .5f), new Color(.24f, .15f, .1f));

            var hinge = new GameObject(barred ? "Barred door hinge" : "Door hinge").transform;
            hinge.SetParent(transform, false);
            hinge.SetPositionAndRotation(centre + rot * new Vector3(-half, 0f, 0f), rot);
            spawned.Add(hinge.gameObject);

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = barred ? "Barred door" : "Door";
            leaf.transform.SetParent(hinge, false);
            leaf.transform.localPosition = new Vector3(half + .05f, DoorHeight * .5f, 0f);
            leaf.transform.localRotation = Quaternion.identity;
            leaf.transform.localScale = new Vector3(width + .1f, DoorHeight, LeafThickness);
            var leafRenderer = leaf.GetComponent<Renderer>();
            leafRenderer.sharedMaterial = DoorMaterial();
            var leafCollider = leaf.GetComponent<Collider>();
            spawned.Add(leaf);

            GameObject padlock = null;
            if (barred)
            {
                padlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                padlock.name = "Padlock";
                padlock.transform.SetParent(transform, false);
                padlock.transform.position = centre + Vector3.up * 1.15f + rot * Vector3.right * (width * .3f);
                padlock.transform.rotation = rot;
                padlock.transform.localScale = new Vector3(.2f, .3f, 1.2f);
                var padlockCollider = padlock.GetComponent<Collider>();
                if (padlockCollider) Destroy(padlockCollider);
                padlock.GetComponent<Renderer>().sharedMaterial = Solid(lockColour);
                spawned.Add(padlock);
            }

            return new DoorVisual
            {
                Hinge = hinge,
                Leaf = leafCollider,
                Padlock = padlock,
                Render = leafRenderer,
                Closed = rot,
                Opened = rot * Quaternion.Euler(0f, OpenAngle(centre, rot, half), 0f),
                Barred = barred,
                Open = false,
                Swing = 1f,
            };
        }

        static readonly Color DoorBrown = new(.48f, .29f, .18f);
        static Material doorMaterial;

        static Material DoorMaterial() => doorMaterial ? doorMaterial : doorMaterial = Solid(DoorBrown);

        static Material Paint(Material material, Color colour)
        {
            if (!material) return material;
            colour.a = 1f;
            material.color = colour;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            return material;
        }

        float OpenAngle(Vector3 centre, Quaternion rot, float half)
        {
            float reach = half + .8f;
            bool minusZ = Inside(centre + rot * new Vector3(0f, 0f, -reach));
            bool plusZ = Inside(centre + rot * new Vector3(0f, 0f, reach));
            if (plusZ == minusZ) return 90f;
            return plusZ ? -90f : 90f;
        }

        bool Inside(Vector3 point) => !string.IsNullOrEmpty(room.Layout.RoomAt(point.x, point.y, point.z));

        void AddRoom(HashSet<string> reachable, string name)
        {
            if (string.IsNullOrEmpty(name) || room.Layout.Room(name) == null) return;
            reachable.Add(name);
        }

        List<string> PickKeyRooms(int count)
        {
            var result = new List<string>();
            if (!exitGated) return result;
            var layout = room.Layout;
            int wanted = Mathf.Max(1, Match.Rules.EscapeKeyAfterRooms);
            var others = new List<(string name, int doors)>();
            foreach (var box in layout.Rooms)
            {
                if (box.Name == spawnRoom || box.Name == exitRoom || box.Outdoor) continue;
                int doors = string.IsNullOrEmpty(spawnRoom) ? wanted : Escape.PathDoors(layout, spawnRoom, box.Name).Count;
                if (doors >= wanted) others.Add((box.Name, doors));
            }
            if (others.Count == 0)
                foreach (var box in layout.Rooms)
                    if (box.Name != spawnRoom && box.Name != exitRoom && !box.Outdoor) others.Add((box.Name, 1));
            if (others.Count == 0) return result;

            others.Sort((a, b) => b.doors.CompareTo(a.doors));
            int band = Mathf.Clamp(Mathf.Max(count, Mathf.CeilToInt(others.Count * .5f)), 1, others.Count);
            var pool = new List<string>();
            for (int i = 0; i < band; i++) pool.Add(others[i].name);

            while (result.Count < count && pool.Count > 0)
            {
                int pick = Random.Range(0, pool.Count);
                result.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            for (int i = 0; result.Count < count && i < others.Count; i++)
                if (!result.Contains(others[i].name)) result.Add(others[i].name);
            return result;
        }

        string NextKeyRoom
        {
            get
            {
                foreach (var key in keys)
                    if (key != null && key.Root) return key.Room;
                return keys.Count > 0 ? keys[0].Room : exitRoom;
            }
        }

        void ConfigureExit()
        {
            var layout = room.Layout;
            var box = layout.Room(exitRoom);
            float y = box != null ? box.FloorY : 0f;
            extraction = new Vector3(layout.ExtractionX, y, layout.ExtractionZ);
            exitGated = false;
            if (!Match.Rules.EscapeNeedsKey || box == null) return;

            float longest = 0f;
            foreach (var wall in Walls(box))
            {
                if (!IsOutside(wall.centre + wall.outward * .6f + Vector3.up * .5f)) continue;
                if (wall.length <= longest) continue;
                longest = wall.length;
                exitCentre = wall.centre;
                exitYaw = wall.yaw;
                exitOutward = wall.outward;
                exitWidth = Mathf.Clamp(wall.length - 1f, 1.1f, 2.4f);
                exitGated = true;
            }

            extraction = new Vector3((box.MinX + box.MaxX) * .5f, y, (box.MinZ + box.MaxZ) * .5f);

            Debug.Log($"[Escape] exit room={exitRoom} gated={exitGated} centre={exitCentre} yaw={exitYaw:0} width={exitWidth:0.00} outward={exitOutward} stand={extraction}");
        }

        void BuildKeys()
        {
            keys.Clear();
            keysNeeded = keysFound = 0;
            if (!exitGated) return;

            var rooms = PickKeyRooms(Mathf.Max(1, Match.Rules.EscapeKeys));
            foreach (string roomName in rooms)
            {
                var key = BuildKeyIn(roomName);
                if (key == null) continue;
                keys.Add(key);
                keysNeeded++;
            }
            Debug.Log($"[Escape] {keysNeeded} key(s) hidden in {string.Join(", ", rooms)}");
        }

        KeyProp BuildKeyIn(string roomName)
        {
            var box = room.Layout.Room(roomName);
            if (box == null) return null;

            var gold = Solid(new Color(1f, .82f, .28f));
            var floor = new Vector3((box.MinX + box.MaxX) * .5f, box.FloorY, (box.MinZ + box.MaxZ) * .5f);

            var root = new GameObject("Van key").transform;
            root.SetParent(transform, false);
            root.position = floor + Vector3.up * .78f;

            const int RingSteps = 14;
            for (int i = 0; i < RingSteps; i++)
            {
                float angle = i / (float)RingSteps * Mathf.PI * 2f;
                KeyPiece(root, "Key bow",
                    new Vector3(Mathf.Cos(angle) * .12f, .21f + Mathf.Sin(angle) * .12f, 0f),
                    Vector3.one * .055f, gold, Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg));
            }
            KeyPiece(root, "Key shaft", new Vector3(0f, -.02f, 0f), new Vector3(.06f, .32f, .06f), gold, Quaternion.identity);
            KeyPiece(root, "Key bit", new Vector3(.09f, -.13f, 0f), new Vector3(.12f, .05f, .06f), gold, Quaternion.identity);
            KeyPiece(root, "Key bit", new Vector3(.09f, -.03f, 0f), new Vector3(.12f, .05f, .06f), gold, Quaternion.identity);

            var beacon = new GameObject("Key beacon").transform;
            beacon.SetParent(transform, false);
            beacon.position = floor + Vector3.up * .04f;
            var points = new Vector3[25];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i / (float)(points.Length - 1) * Mathf.PI * 2f;
                points[i] = new Vector3(Mathf.Cos(angle) * .6f, 0f, Mathf.Sin(angle) * .6f);
            }
            var ring = SummonEffects.Line(beacon, new Color(1f, .82f, .28f), points, true);
            if (ring) ring.startWidth = ring.endWidth = .07f;
            spawned.Add(beacon.gameObject);

            var key = root.gameObject;
            spawned.Add(key);
            return new KeyProp { Root = key, Beacon = beacon.gameObject, Room = roomName };
        }

        void KeyPiece(Transform root, string name, Vector3 at, Vector3 scale, Material material, Quaternion rotation)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = at;
            piece.transform.localRotation = rotation;
            piece.transform.localScale = scale;
            var collider = piece.GetComponent<Collider>();
            if (collider) Destroy(collider);
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        void BuildExitDoor()
        {
            if (!exitGated) return;
            exitDoor = BuildLeaf(exitCentre, Quaternion.Euler(0f, exitYaw, 0f), exitWidth, true, new Color(1f, .84f, .32f));
        }

        void UnlockExit()
        {
            if (exitOpen) return;
            exitOpen = true;
            if (exitDoor != null)
            {
                exitDoor.Open = true;
                exitDoor.Swing = 0f;
                if (exitDoor.Leaf) exitDoor.Leaf.enabled = false;
                if (exitDoor.Padlock) Destroy(exitDoor.Padlock);
            }

            var daylight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            daylight.name = "Daylight";
            daylight.transform.SetParent(transform, false);
            daylight.transform.SetPositionAndRotation(exitCentre - exitOutward * .42f + Vector3.up * 1.15f, Quaternion.Euler(0f, exitYaw, 0f));
            daylight.transform.localScale = new Vector3(exitWidth, 2.3f, .06f);
            var collider = daylight.GetComponent<Collider>();
            if (collider) Destroy(collider);
            daylight.GetComponent<Renderer>().sharedMaterial = Solid(new Color(.93f, .96f, .86f));
            spawned.Add(daylight);

            hud.Toast("The key turns \u2014 the way out is open. Everybody out!");
            Debug.Log("[Escape] exit door opened with the key");
        }

        void TickExitDoor()
        {
            if (exitDoor == null || !exitDoor.Open || exitDoor.Swing >= 1f || !exitDoor.Hinge) return;
            exitDoor.Swing = Mathf.Min(1f, exitDoor.Swing + Time.deltaTime / .8f);
            exitDoor.Hinge.rotation = Quaternion.Slerp(exitDoor.Closed, exitDoor.Opened, Mathf.SmoothStep(0f, 1f, exitDoor.Swing));
        }

        void TickKey()
        {
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                var key = keys[i];
                if (key == null || !key.Root) { keys.RemoveAt(i); continue; }
                key.Root.transform.Rotate(0f, 55f * Time.deltaTime, 0f, Space.World);

                foreach (var p in joins.Players)
                {
                    if (p == null || p.IsEliminated) continue;
                    var delta = p.transform.position - key.Root.transform.position;
                    if (World.Flat(delta).sqrMagnitude > 1.5f * 1.5f || Mathf.Abs(delta.y) > 2f) continue;

                    Destroy(key.Root);
                    if (key.Beacon) Destroy(key.Beacon);
                    keys.RemoveAt(i);
                    keysFound++;
                    Debug.Log($"[Escape] key {keysFound}/{keysNeeded} collected from {key.Room}");

                    if (AllKeysFound)
                    {
                        BuildExitDoor();
                        UnlockExit();
                        SyncExitMarker();
                        hud.Toast("Last key found \u2014 the way out is on the map");
                    }
                    else hud.Toast($"Key {keysFound}/{keysNeeded} \u2014 keep looking");
                    return;
                }
            }
        }

        void LogDoorVisibility()
        {
            var who = GuidePlayer();
            var lens = Camera.main;
            if (who == null || lens == null) return;
            var planes = GeometryUtility.CalculateFrustumPlanes(lens);
            int logged = 0;
            foreach (var visual in visuals)
            {
                if (visual.Render == null || visual.Door == null || logged >= 2) continue;
                float distance = Vector3.Distance(who.transform.position, DoorCentre(visual.Door));
                if (distance > 9f) continue;
                logged++;
                Debug.Log($"[Escape] look {visual.Door.A}/{visual.Door.B} d={distance:0.0} drawn={visual.Render.isVisible}"
                    + $" inView={GeometryUtility.TestPlanesAABB(planes, visual.Render.bounds)} enabled={visual.Render.enabled}"
                    + $" open={visual.Open} layer={visual.Render.gameObject.layer} size={visual.Render.bounds.size}"
                    + $" pos={visual.Render.transform.position}");
            }
        }

        string GuideTargetRoom => exitGated && !AllKeysFound ? NextKeyRoom : exitRoom;

        PlayerController GuidePlayer()
        {
            var target = room.Layout.Room(GuideTargetRoom);
            var centre = target != null
                ? new Vector3((target.MinX + target.MaxX) * .5f, target.FloorY, (target.MinZ + target.MaxZ) * .5f)
                : extraction;
            PlayerController best = null;
            float bestDistance = float.MaxValue;
            foreach (var p in joins.Players)
            {
                if (p == null || p.IsEliminated) continue;
                float distance = (p.transform.position - centre).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = p; }
            }
            return best;
        }

        LockedDoor LockedBetween(Doorway door)
        {
            foreach (var locked in doors)
                if (!locked.Open && locked.Door.A == door.A && locked.Door.B == door.B) return locked;
            return null;
        }

        void UpdateGuide()
        {
            guideDoor = null;
            if (!exitGated) { guideRoom = null; return; }
            string target = GuideTargetRoom;
            guideRoom = target;

            var who = GuidePlayer();
            if (who == null) return;
            string here = room.Layout.RoomAt(who.transform.position.x, who.transform.position.y, who.transform.position.z);
            if (string.IsNullOrEmpty(here) || room.Layout.Room(here) == null) return;

            foreach (var door in Escape.PathDoors(room.Layout, here, target))
            {
                var locked = LockedBetween(door);
                if (locked == null) continue;
                guideDoor = locked;
                guideRoom = string.Equals(door.A, here, System.StringComparison.Ordinal) ? door.B : door.A;
                return;
            }
        }

        static string RoomName(Rules.HouseLayout layout, string room) =>
            string.IsNullOrEmpty(room) ? "the exit" : layout.WithStorey(room);

        string GuideText()
        {
            if (!exitGated) return "";
            if (exitOpen) return "EXIT OPEN \u00b7 everybody out!";
            string near = RoomName(room.Layout, guideRoom);
            string lead = keysNeeded > 1 ? $"KEYS {keysFound}/{keysNeeded}" : "VAN KEY";
            if (guideDoor != null)
            {
                string word = Spaced(guideDoor.Word);
                return AllKeysFound
                    ? $"EXIT \u00b7 spell {word} to get through, then on to {near}"
                    : $"{lead} \u00b7 spell {word}, then on to {near}";
            }
            return AllKeysFound
                ? $"EXIT \u00b7 get everybody to {near}"
                : keysNeeded > 1 ? $"{lead} \u00b7 the next one is in {near}" : $"{lead} \u00b7 it is hidden in {near}";
        }

        void MeasureDoorway(Doorway d, float height, ref float yaw, ref float width)
        {
            var centre = new Vector3(d.X, height, d.Z);
            float freeX = Mathf.Min(Free(centre, Vector3.right), Free(centre, Vector3.left));
            float freeZ = Mathf.Min(Free(centre, Vector3.forward), Free(centre, Vector3.back));
            bool passageAlongX = freeX >= freeZ;
            yaw = passageAlongX ? 90f : 0f;

            var wallAxis = passageAlongX ? Vector3.forward : Vector3.right;
            float jambA = Free(centre, wallAxis, 4f);
            float jambB = Free(centre, -wallAxis, 4f);
            float measured = jambA + jambB;
            if (measured > d.Width * .8f && measured < d.Width * 1.3f) width = measured;

            Debug.Log($"[Escape] scan {d.A}/{d.B} at {centre}: freeX={freeX:0.00} freeZ={freeZ:0.00} → yaw={yaw:0} jambs={jambA:0.00}/{jambB:0.00} width={width:0.00} (data {d.Width:0.00})");
        }

        float WallFace(Doorway d, Vector3 through, float y)
        {
            var start = new Vector3(d.X, y + 1.15f, d.Z) + through * 3f;
            var hits = Physics.RaycastAll(start, -through, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null) continue;
                if (collider.gameObject.layer == World.PlayerLayer || collider.gameObject.layer == World.TileLayer) continue;
                if (collider.GetComponentInParent<PlayerController>()) continue;
                if (hit.distance < best) best = hit.distance;
            }
            if (best == float.MaxValue) return .35f;
            return Mathf.Clamp(3f - best, 0f, .9f);
        }

        static float Free(Vector3 from, Vector3 direction, float max = 3f)
        {
            float best = max;
            var hits = Physics.RaycastAll(from, direction, max, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null) continue;
                if (collider.gameObject.layer == World.PlayerLayer || collider.gameObject.layer == World.TileLayer) continue;
                if (collider.GetComponentInParent<PlayerController>()) continue;
                if (hit.distance < best) best = hit.distance;
            }
            return best;
        }

        Vector3 DoorCentre(Doorway d)
        {
            var box = room.Layout.Room(d.A) ?? room.Layout.Room(d.B);
            float y = box != null ? box.FloorY : 0f;
            return new Vector3(d.X, y, d.Z);
        }

        float DoorYaw(Doorway d)
        {
            var a = room.Layout.Room(d.A);
            var b = room.Layout.Room(d.B);
            if (a == null || b == null) return 0f;
            var from = Centre(a);
            var to = Centre(b);
            return Mathf.Abs(to.x - from.x) >= Mathf.Abs(to.y - from.y) ? 90f : 0f;
        }


        void Piece(string name, Vector3 position, Quaternion rotation, Vector3 scale, Color colour)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(transform, false);
            piece.transform.position = position;
            piece.transform.rotation = rotation;
            piece.transform.localScale = scale;
            var collider = piece.GetComponent<Collider>();
            if (collider) Destroy(collider);
            piece.GetComponent<Renderer>().sharedMaterial = Paint(GameAssets.I.Tinted(colour), colour);
            spawned.Add(piece);
        }

        const float AutoOpenRadius = .55f;

        void TickDoors()
        {
            foreach (var door in visuals)
            {
                if (!door.Open && !door.Barred && door.Hinge && PlayerNear(door.Door, AutoOpenRadius))
                {
                    door.Open = true;
                    door.Swing = 0f;
                    if (door.Leaf) door.Leaf.enabled = false;
                    if (door.Padlock) Destroy(door.Padlock);
                    Debug.Log($"[Escape] free door swung open {door.Door.A}/{door.Door.B}");
                }
                if (!door.Open || door.Swing >= 1f || !door.Hinge) continue;
                door.Swing = Mathf.Min(1f, door.Swing + Time.deltaTime / .8f);
                door.Hinge.rotation = Quaternion.Slerp(door.Closed, door.Opened, Mathf.SmoothStep(0f, 1f, door.Swing));
            }
        }

        bool PlayerNear(Doorway d, float radius) => PlayerNear(DoorCentre(d), radius);

        bool PlayerNear(Vector3 spot, float radius)
        {
            foreach (var p in joins.Players)
            {
                if (p == null || p.IsEliminated) continue;
                if ((p.transform.position - spot).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }

        DoorVisual VisualFor(Doorway d)
        {
            foreach (var visual in visuals)
                if (visual.Door.A == d.A && visual.Door.B == d.B) return visual;
            return null;
        }

        static bool HoldingWord(PlayerController p, string word)
        {
            if (p == null || p.Combat == null) return false;
            var weapon = p.Combat.Weapon;
            if (weapon && string.Equals(weapon.word, word, System.StringComparison.OrdinalIgnoreCase)) return true;
            var held = p.Combat.Held;
            if (!held) return false;
            var smash = held.GetComponentInParent<Smashable>();
            if (smash && string.Equals(smash.Word, word, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void StockDoor(string word, Doorway door, HashSet<string> reachable, System.Random rng)
        {
            var layout = room.Layout;
            var open = layout.Rooms.Where(r => reachable != null && reachable.Contains(r.Name)).ToList();
            if (open.Count == 0) open = layout.Rooms.ToList();
            if (open.Count == 0) return;

            string near = reachable != null && reachable.Contains(door.A) ? door.A
                        : reachable != null && reachable.Contains(door.B) ? door.B
                        : open[rng.Next(open.Count)].Name;
            var box = layout.Room(near) ?? open[rng.Next(open.Count)];
            var placed = new List<Vector2>();

            var needed = new Dictionary<char, int>();
            foreach (char c in word) needed[c] = needed.TryGetValue(c, out var n) ? n + 1 : 1;

            int guard = 0;
            while (needed.Values.Any(v => v > 0) && guard++ < 16)
            {
                string pick = PickPropFor(needed, word, rng);
                if (pick == null) break;
                var prop = PlaceProp(pick, box, placed, rng);
                if (prop == null) break;
                spawned.Add(prop.gameObject);
                foreach (char c in pick) if (needed.TryGetValue(c, out var k) && k > 0) needed[c] = k - 1;
                Debug.Log($"[Escape] stocked '{pick}' for door '{word}' in {box.Name}");
            }

            foreach (var pair in needed.Where(p => p.Value > 0).ToList())
                for (int i = 0; i < pair.Value; i++)
                {
                    var prop = PlaceProp(new string(pair.Key, 1), box, placed, rng);
                    if (prop) spawned.Add(prop.gameObject);
                }
        }

        Smashable PlaceProp(string word, RoomBox box, List<Vector2> placed, System.Random rng)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var spot = ClearSpot(box, rng);
                var here = new Vector2(spot.x, spot.z);
                bool clash = false;
                foreach (var other in placed)
                    if ((here - other).sqrMagnitude < 1.1f * 1.1f) { clash = true; break; }
                if (clash) continue;
                var prop = SpawnProp(word, new Vector3(spot.x, box.FloorY + .12f, spot.z), (float)rng.NextDouble() * 360f);
                if (prop == null) continue;
                placed.Add(here);
                return prop;
            }
            return SpawnProp(word, new Vector3(Centre(box).x, box.FloorY + .12f, Centre(box).y), 0f);
        }

        Smashable SpawnProp(string word, Vector3 position, float yaw)
        {
            word = word.ToUpperInvariant();
            var item = GameConfig.Current.Items.TryGet(word, out var found) ? found : null;
            float health = (Match.Rules.FurnitureToughnessBase + Match.Rules.FurnitureToughnessPerLetter * word.Length)
                * Smashable.HealthPerBreakPower;

            if (item == null || string.IsNullOrEmpty(item.Model)) return FurnitureCatalog.Spawn(word, position, yaw, transform);

            var root = new GameObject(word + " \u00b7 escape").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var visual = Art.ModelVisual.Spawn(item.Model, root);
            if (visual == null)
            {
                Destroy(root.gameObject);
                return FurnitureCatalog.Spawn(word, position, yaw, transform);
            }

            var bounds = Art.ModelVisual.BoundsIn(root, visual);
            visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            float sx = item.Size != null && item.Size.Length > 0 ? Mathf.Max(.4f, item.Size[0]) : .6f;
            float sy = item.Size != null && item.Size.Length > 1 ? Mathf.Max(.1f, item.Size[1]) : .6f;
            float sz = item.Size != null && item.Size.Length > 2 ? Mathf.Max(.4f, item.Size[2]) : .6f;
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(sx, sy, sz);
            box.center = new Vector3(0f, sy * .5f, 0f);
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.mass = 4f;
            var smash = root.gameObject.AddComponent<Smashable>();
            smash.Init(word, health);
            return smash;
        }

        static string PickPropFor(Dictionary<char, int> needed, string word, System.Random rng)
        {
            int best = 0;
            var ties = new List<string>();
            foreach (var item in GameConfig.Current.Items.All)
            {
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;
                if (string.IsNullOrEmpty(item.Model)) continue;
                if (string.Equals(item.Id, word, System.StringComparison.OrdinalIgnoreCase)) continue;
                var left = new Dictionary<char, int>(needed);
                int score = 0;
                foreach (char c in item.Id)
                    if (left.TryGetValue(c, out var k) && k > 0) { left[c] = k - 1; score++; }
                if (score == 0) continue;
                if (score > best) { best = score; ties.Clear(); ties.Add(item.Id); }
                else if (score == best) ties.Add(item.Id);
            }
            return ties.Count > 0 ? ties[rng.Next(ties.Count)] : null;
        }

        Vector3 ClearSpot(RoomBox box, System.Random rng)
        {
            var layout = room.Layout;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float x = Mathf.Lerp(box.MinX + 1.2f, box.MaxX - 1.2f, (float)rng.NextDouble());
                float z = Mathf.Lerp(box.MinZ + 1.2f, box.MaxZ - 1.2f, (float)rng.NextDouble());
                if (layout.RoomAt(x, box.FloorY, z) != box.Name) continue;
                bool nearDoor = false;
                foreach (var d in layout.Doors)
                    if ((new Vector2(x, z) - new Vector2(d.X, d.Z)).sqrMagnitude < 1.6f * 1.6f) { nearDoor = true; break; }
                if (nearDoor) continue;
                return new Vector3(x, box.FloorY + .9f, z);
            }
            var centre = Centre(box);
            return new Vector3(centre.x, box.FloorY + .9f, centre.y);
        }

        void EnsureDoorLetters()
        {
            foreach (var door in doors)
            {
                if (door.Open) continue;
                var need = new Dictionary<char, int>();
                foreach (char c in door.Word) need[c] = need.TryGetValue(c, out var n) ? n + 1 : 1;

                var pool = TilePool.Instance;
                if (pool != null)
                    foreach (var tile in pool.Active)
                        if (tile && need.TryGetValue(tile.Letter, out var k) && k > 0) need[tile.Letter] = k - 1;
                foreach (var p in joins.Players)
                {
                    if (!p || !p.Inventory) continue;
                    foreach (char c in p.Inventory.Letters)
                        if (need.TryGetValue(c, out var k) && k > 0) need[c] = k - 1;
                }
                foreach (var go in spawned)
                {
                    var prop = go ? go.GetComponent<Smashable>() : null;
                    if (prop == null || prop.IsBroken || string.IsNullOrEmpty(prop.Word)) continue;
                    foreach (char c in prop.Word)
                        if (need.TryGetValue(c, out var k) && k > 0) need[c] = k - 1;
                }

                foreach (var pair in need.Where(p => p.Value > 0).ToList())
                    for (int i = 0; i < pair.Value; i++) DropPropNear(pair.Key);
            }
        }

        void DropPropNear(char letter)
        {
            var player = joins.Players.FirstOrDefault(p => p && !p.IsEliminated);
            Vector3 at = player ? player.transform.position + player.transform.forward * 1.8f : extraction + Vector3.up;
            var prop = SpawnProp(new string(letter, 1), at, 0f);
            if (prop) spawned.Add(prop.gameObject);
        }

        void ShuffleFurniture(System.Random rng)
        {
            var layout = room.Layout;
            if (layout == null || room.Originals == null) return;

            var doorSpots = new List<Vector2>();
            foreach (var d in layout.Doors) doorSpots.Add(new Vector2(d.X, d.Z));
            var spawnSpots = new List<Vector2>();
            foreach (var s in layout.Spawns) spawnSpots.Add(new Vector2(s.X, s.Z));
            var stairSpots = new List<Vector2>();
            foreach (var s in layout.Stairs) { stairSpots.Add(new Vector2(s.FromX, s.FromZ)); stairSpots.Add(new Vector2(s.ToX, s.ToZ)); }
            var placed = new List<Vector2>();

            foreach (var prop in room.Originals)
            {
                if (!prop) continue;
                var at = prop.transform.position;
                string roomName = layout.RoomAt(at.x, at.y, at.z);
                var box = string.IsNullOrEmpty(roomName) ? null : layout.Room(roomName);
                if (box == null) continue;

                var localDoors = new List<Vector2>();
                foreach (var d in layout.Doors)
                    if (string.Equals(d.A, roomName, System.StringComparison.Ordinal) ||
                        string.Equals(d.B, roomName, System.StringComparison.Ordinal))
                        localDoors.Add(new Vector2(d.X, d.Z));

                Vector3 best = at;
                bool found = false;
                for (int attempt = 0; attempt < 24 && !found; attempt++)
                {
                    float x = Mathf.Lerp(box.MinX + .9f, box.MaxX - .9f, (float)rng.NextDouble());
                    float z = Mathf.Lerp(box.MinZ + .9f, box.MaxZ - .9f, (float)rng.NextDouble());
                    if (!string.Equals(layout.RoomAt(x, at.y, z), roomName, System.StringComparison.Ordinal)) continue;

                    var candidate = new Vector2(x, z);
                    if (TooClose(candidate, doorSpots, 1.5f)) continue;
                    if (TooClose(candidate, spawnSpots, 1.2f)) continue;
                    if (TooClose(candidate, stairSpots, 1.4f)) continue;
                    if (BlocksWalkLine(candidate, localDoors, 1.0f)) continue;
                    if (TooClose(candidate, placed, .9f)) continue;

                    best = new Vector3(x, at.y, z);
                    found = true;
                }

                if (!found) { placed.Add(new Vector2(at.x, at.z)); continue; }
                var body = prop.GetComponent<Rigidbody>();
                if (body) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                prop.transform.position = best;
                prop.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                placed.Add(new Vector2(best.x, best.z));
            }
        }

        static bool TooClose(Vector2 point, List<Vector2> spots, float radius)
        {
            foreach (var spot in spots)
                if ((point - spot).sqrMagnitude < radius * radius) return true;
            return false;
        }

        static bool BlocksWalkLine(Vector2 point, List<Vector2> doorways, float clearance)
        {
            for (int i = 0; i < doorways.Count; i++)
                for (int j = i + 1; j < doorways.Count; j++)
                {
                    var a = doorways[i];
                    var b = doorways[j];
                    var line = b - a;
                    float length = line.magnitude;
                    if (length < .01f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(point - a, line) / (length * length));
                    if ((point - (a + line * t)).sqrMagnitude < clearance * clearance) return true;
                }
            return false;
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            switch (Current)
            {
                case State.Waiting:
                    hud.SetInstruction(ControlHints.Join("join"), "");
                    if (joins.HumanCount > 0) Restart();
                    break;

                case State.Countdown:
                    hud.SetInstruction("", "");
                    hud.ShowCountdown(Mathf.Max(1, Mathf.CeilToInt(3f - (Time.time - began))),
                        $"THE ESCAPE \u00b7 {room.Layout.Name.ToUpperInvariant()}");
                    if (Time.time - began >= 3f)
                    {
                        Current = State.Playing; began = Time.time;
                        foreach (var p in joins.Players) p.Frozen = false;
                        clearOut.Begin();
                        hud.ShowGo();

                    }
                    break;

                case State.Playing:
                    hud.SetTimer(RoundManager.FormatTime(TimeLeft));
                    if (Time.time >= nextGuideCheck) { nextGuideCheck = Time.time + .4f; UpdateGuide(); }
                    hud.SetInstruction(GuideText(), clearOut.Message);
                    UpdateNotice();
                    MakeRoomForLetters();
                    CheckDoors();
                    TickKey();
                    if (DoorDiagnostics && Time.time >= nextLookCheck) { nextLookCheck = Time.time + 2f; LogDoorVisibility(); }
                    if (Time.time >= nextLetterCheck)
                    {
                        nextLetterCheck = Time.time + 4f;
                        EnsureDoorLetters();
                    }
                    CheckObjectives();
                    break;

                case State.Complete:
                case State.Failed:
                    if (hud.ResultShown && Time.time - finishedAt > 1.6f && joins.AnyStartPressed()) hud.ConfirmResult();
                    break;
            }
            TickDoors();
            TickExitDoor();
            hud.SetChecklist(ChecklistText());
            hud.SetScoreboard(joins.Players, _ => 0, 0, false);
        }

        const float HintRadius = 5f;

        string DoorHint()
        {
            var player = joins.Players.FirstOrDefault(p => p && !p.IsEliminated);
            if (player == null) return "";
            LockedDoor near = null;
            float best = float.MaxValue;
            foreach (var door in doors)
            {
                if (door.Open) continue;
                float distance = Vector3.Distance(player.transform.position, DoorCentre(door.Door));
                if (distance < best) { best = distance; near = door; }
            }
            if (near == null) return "";
            if (best > HintRadius) return "";
            var missing = MissingLetters(player.Inventory.Letters, near.Word);
            return missing.Count == 0
                ? $"LOCKED DOOR \u00b7 spell {Spaced(near.Word)} here"
                : $"LOCKED DOOR \u00b7 needs {Spaced(near.Word)} (missing {string.Join(" ", missing.Select(c => c.ToString()))})";
        }

        static string Spaced(string word) => string.Join(" ", word.Select(c => c.ToString()));

        static List<char> MissingLetters(IReadOnlyList<char> held, string word)
        {
            var spare = new List<char>(held);
            var missing = new List<char>();
            foreach (char c in word)
            {
                int at = spare.IndexOf(c);
                if (at >= 0) spare.RemoveAt(at); else missing.Add(c);
            }
            return missing;
        }

        string ChecklistText()
        {
            int left = 0;
            foreach (var door in doors) if (!door.Open) left++;
            string key = !exitGated ? ""
                : exitOpen ? $"\nKeys: {keysFound}/{keysNeeded} \u2014 the way out is open"
                : AllKeysFound ? $"\nKeys: {keysFound}/{keysNeeded} \u2014 head for the exit"
                : $"\nKeys: {keysFound}/{keysNeeded} \u2014 look in {RoomName(room.Layout, NextKeyRoom)}";
            return "<b>THE ESCAPE</b>\n"
                 + "Goal: get the whole crew to the EXIT.\n"
                 + (left > 0 ? $"Barred doors still shut: {left}" : "No barred doors left \u2014 head for the exit.")
                 + key;
        }


        CanvasGroup notice;
        TextMeshProUGUI noticeHead, noticeBody;

        void BuildNotice()
        {
            var root = new GameObject("Escape notice", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            notice = root.GetComponent<CanvasGroup>();
            notice.alpha = 0f;
            notice.blocksRaycasts = false;
            notice.interactable = false;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(56f, 150f);
            rect.sizeDelta = new Vector2(640f, 200f);
            panel.GetComponent<Image>().color = new Color(.07f, .06f, .1f, .93f);

            noticeHead = NoticeText(panel.transform, "Head", new Vector2(0f, 64f), new Vector2(620f, 46f), 32f, new Color(1f, .84f, .42f));
            noticeBody = NoticeText(panel.transform, "Body", new Vector2(0f, -16f), new Vector2(620f, 120f), 27f, new Color(.98f, .95f, .9f));
        }

        static TextMeshProUGUI NoticeText(Transform parent, string name, Vector2 at, Vector2 size, float fontSize, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            go.SetParent(parent, false);
            go.anchorMin = go.anchorMax = new Vector2(.5f, .5f);
            go.pivot = new Vector2(.5f, .5f);
            go.anchoredPosition = at;
            go.sizeDelta = size;
            var text = go.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = GameAssets.I.font;
            text.fontSize = fontSize;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        void UpdateNotice()
        {
            if (notice == null) return;
            var player = joins.Players.FirstOrDefault(p => p && !p.IsEliminated);
            LockedDoor near = null;
            float best = 6.5f;
            if (player != null)
                foreach (var door in doors)
                {
                    if (door.Open) continue;
                    float distance = Vector3.Distance(player.transform.position, DoorCentre(door.Door));
                    if (distance < best) { best = distance; near = door; }
                }

            float target = 0f;
            if (Current != State.Playing) near = null;
            if (near != null && player != null)
            {
                target = Mathf.Clamp01(1f - (best - 1.6f) / 4.9f);
                var missing = MissingLetters(player.Inventory.Letters, near.Word);
                noticeHead.text = "BARRED DOOR";
                noticeBody.text = $"SPELL   <b>{Spaced(near.Word)}</b>\n"
                    + (missing.Count == 0
                        ? "You have every letter \u2014 spell it right here."
                        : $"Missing   <b>{string.Join(" ", missing.Select(c => c.ToString()))}</b>");
            }
            notice.alpha = Mathf.MoveTowards(notice.alpha, target, Time.deltaTime * 2.5f);
        }

        static Material Solid(Color colour)
        {
            colour.a = 1f;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");
            var material = shader ? new Material(shader) : null;
            if (material == null) return GameAssets.I.Tinted(colour);
            material.color = colour;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            return material;
        }

        void MakeRoomForLetters()
        {
            if (Time.time < nextRoomCheck) return;
            nextRoomCheck = Time.time + .5f;

            foreach (var p in joins.Players)
            {
                if (!p || !p.Inventory || !p.Inventory.IsFull) continue;

                var wanted = new HashSet<char>();
                foreach (var door in doors)
                {
                    if (door.Open) continue;
                    if (Vector3.Distance(p.transform.position, DoorCentre(door.Door)) > 14f) continue;
                    foreach (char c in door.Word) wanted.Add(c);
                }
                if (wanted.Count == 0) continue;

                bool nearby = false;
                var pool = TilePool.Instance;
                if (pool != null)
                    foreach (var tile in pool.Active)
                    {
                        if (!tile || !wanted.Contains(tile.Letter)) continue;
                        if ((tile.transform.position - p.transform.position).sqrMagnitude < 2.2f * 2.2f) { nearby = true; break; }
                    }
                if (!nearby) continue;

                foreach (char c in p.Inventory.Letters.ToArray())
                {
                    if (wanted.Contains(c)) continue;
                    if (!p.Inventory.DropLetter(c, p.transform.position, p.transform.forward)) continue;
                    Debug.Log($"[Escape] bag full: dropped '{c}' to pick up a needed letter");
                    break;
                }
            }
        }

        void CheckDoors()
        {
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (door.Open) continue;
                var centre = DoorCentre(door.Door);
                foreach (var p in joins.Players)
                {
                    if (p == null || p.IsEliminated || !p.Inventory) continue;
                    if (Vector3.Distance(p.transform.position, centre) > UnlockRadius) continue;
                    if (!HoldingWord(p, door.Word)) continue;
                    OpenDoor(door, i);
                    break;
                }
            }
        }

        void OpenDoor(LockedDoor door, int index)
        {
            if (door.Open) return;
            door.Open = true;
            var visual = VisualFor(door.Door);
            if (visual == null) return;
            visual.Open = true;
            visual.Swing = 0f;
            if (visual.Leaf) visual.Leaf.enabled = false;
            if (visual.Padlock) Destroy(visual.Padlock);
        }

        void CheckObjectives()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + .2f;

            var combatants = joins.Players.Select(p => new Combatant(p.Index, p.Team, p.Health.State)).ToArray();
            foreach (int id in WinCheck.Unrevivable(combatants)) World.PlayerById(id)?.Health.Eliminate();

            var survivors = joins.Players.Where(p => !p.IsEliminated).ToArray();
            if (survivors.Length == 0 || TimeLeft <= 0f) Finish(false);
            else if ((!exitGated || exitOpen) && survivors.All(p => p.Health.IsAlive && AtExit(p.transform.position))) Finish(true);
        }

        public bool AtExit(Vector3 p) =>
            World.Flat(p - extraction).sqrMagnitude <= 2.4f * 2.4f && Mathf.Abs(p.y - extraction.y) < 1.5f;

        void Finish(bool won)
        {
            bool wiped = joins.Players.All(p => p.IsEliminated);
            Current = won ? State.Complete : State.Failed;
            finishedAt = Time.time;
            clearOut.Running = false;
            foreach (var p in joins.Players) p.Frozen = true;
            World.FreezeTransient();
            joins.AllowJoining = false;
            hud.SetTimer("");
            string heading = won
                ? $"Everybody out with {Mathf.CeilToInt(TimeLeft)}s to spare!"
                : wiped ? "The crew went down inside." : "The Movers finished packing.";
            hud.ShowResult(HudResult.Of(1, true, won, heading, MatchTally.Finish(won)), Restart);
        }

        void CreateExitMarker()
        {
            var circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            circle.name = "Escape exit circle";
            circle.transform.SetParent(transform, false);
            circle.transform.position = extraction + Vector3.up * .01f;
            circle.transform.localScale = new Vector3(4.8f, .01f, 4.8f);
            Destroy(circle.GetComponent<Collider>());
            circle.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(new Color(.34f, .63f, .59f));
            exitCircle = circle;

            var text = new GameObject("Exit label").AddComponent<TextMeshPro>();
            text.transform.SetParent(transform, false);
            text.transform.position = extraction + Vector3.up * .035f;
            text.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            text.font = GameAssets.I.font;
            text.fontSize = 6f;
            text.text = "EXIT";
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(4f, 2f);
            exitLabel = text.gameObject;

            SyncExitMarker();
        }

        void MoveExitMarker()
        {
            if (exitCircle) exitCircle.transform.position = extraction + Vector3.up * .01f;
            if (exitLabel) exitLabel.transform.position = extraction + Vector3.up * .035f;
        }

        void SyncExitMarker()
        {
            bool show = !exitGated || AllKeysFound;
            if (exitCircle) exitCircle.SetActive(show);
            if (exitLabel) exitLabel.SetActive(show);
        }
    }
}
