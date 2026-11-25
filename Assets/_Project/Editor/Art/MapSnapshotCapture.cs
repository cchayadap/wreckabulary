using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public enum MapHeroKind { Room, Dollhouse, Course, Arena }

    public sealed class MapHeroShot
    {
        public readonly string Id, Subject;
        public readonly IReadOnlyList<string> Modules;
        public readonly MapHeroKind Kind;
        public readonly Rect Area;
        public readonly int Storey;
        public readonly float Yaw, Pitch, FieldOfView, Fill, Eye, Back;
        public readonly bool Locked;

        MapHeroShot(string id, string subject, MapHeroKind kind, Rect area, int storey, string[] modules, float yaw, float pitch, float fieldOfView, float fill, float eye, float back, bool locked)
        {
            Locked = locked;
            Id = id; Subject = subject; Kind = kind; Area = area; Storey = storey; Modules = modules ?? Array.Empty<string>();
            Yaw = yaw; Pitch = pitch; FieldOfView = fieldOfView; Fill = fill; Eye = eye; Back = back;
        }

        public bool House => Kind == MapHeroKind.Room || Kind == MapHeroKind.Dollhouse;

        public static MapHeroShot Room(string id, string subject, float minX, float minZ, float maxX, float maxZ, int storey, float yaw, float pitch, float fieldOfView, float fill,
            bool locked = false) =>
            new MapHeroShot(id, subject, MapHeroKind.Room, Rect.MinMaxRect(minX, minZ, maxX, maxZ), storey, null, yaw, pitch, fieldOfView, fill, 0f, 0f, locked);

        public static MapHeroShot Dollhouse(string id, string subject, float minX, float minZ, float maxX, float maxZ, float yaw, float pitch, float fieldOfView, float fill,
            bool locked = false) =>
            new MapHeroShot(id, subject, MapHeroKind.Dollhouse, Rect.MinMaxRect(minX, minZ, maxX, maxZ), -1, null, yaw, pitch, fieldOfView, fill, 0f, 0f, locked);

        public static MapHeroShot Course(string id, string subject, string[] modules, float turn, float pitch, float fieldOfView, float eye, float back, bool locked = false) =>
            new MapHeroShot(id, subject, MapHeroKind.Course, Rect.zero, -1, modules, turn, pitch, fieldOfView, 1f, eye, back, locked);

        public static MapHeroShot Arena(string id, string subject, string[] modules, float turn, float pitch, float fieldOfView, float eye, float back, bool locked = false) =>
            new MapHeroShot(id, subject, MapHeroKind.Arena, Rect.zero, -1, modules, turn, pitch, fieldOfView, 1f, eye, back, locked);

        public string Pose => Kind + (House ? " yaw " : " turn from downtown ") + Yaw.ToString("F0") + " pitch " + Pitch.ToString("F0") + " fov " + FieldOfView.ToString("F0") +
            (House ? " fill " + Fill.ToString("F2") + " area " + Area.xMin + "," + Area.yMin + " to " + Area.xMax + "," + Area.yMax + (Storey >= 0 ? " storey " + Storey : "")
                : " eye " + Eye.ToString("F1") + " back " + Back.ToString("F2") + " modules " + string.Join("/", Modules)) + (Locked ? " locked" : " searched");
    }

    public static class MapSnapshotCapture
    {
        public const string Folder = "Assets/_Project/Resources/MapSnapshots";
        public const string SheetPath = "BuildNotes/w23a/map-heroes-sheet.png";
        public const int Width = 1280, Height = 720;
        const int Supersample = 2, MaximumShadowDistance = 400;
        const float MinimumNear = .3f, NearSlack = 4f, FarSlack = 400f, FurnitureHeight = 1.4f, StoreyHeight = 3f, RoofRoom = 2.5f;
        const float SunPitch = 46f, SunTurn = 24f, SunIntensity = .62f, FillPitch = 28f, FillTurn = 75f, FillIntensity = .2f, ShadowStrength = .6f;
        const float WallProbe = .45f, WallReach = .45f, WallSlack = .2f, LowSlack = .05f, YardMargin = 1.5f, Oversize = 8f;
        const float LevelStep = .5f, SectionThin = 1f, SectionBand = .9f, AutoYaw = 35f, AutoPitch = 38f, AutoFieldOfView = 30f;
        const float CeilingRoom = 4f, EyeFar = 2000f, EyeNear = .1f, EdgeMargin = .8f, FlatLimit = .35f;
        const int FlatReach = 14, FlatBits = 4, LeastProps = 12, ProbeWidth = 320, ProbeHeight = 180, LeastPieces = 3;
        const float FurnishedMargin = 1.2f, HandMargin = .08f, PropHeight = 2.5f, PropSpan = 3f, PropRows = 40f, PropWeight = .6f, ShareWeight = .8f, ShareScale = 3f, Tie = .0001f;
        const float NearProps = 4f, FarProps = 7f, CourseTurn = 30f;
        static readonly float[] AutoYaws = { 35f, 155f, 275f };
        const float MinCover = .004f, SolidCover = .95f, WhitePoint = .98f, TargetWhite = .86f, MinGain = .8f, MaxGain = 1.1f, Contrast = 1.1f, Saturation = 1.12f, EmptyShare = .02f;
        const int SheetColumns = 3, CellWidth = 640, CellHeight = 360, SheetGap = 8, SmallColumns = 9, SmallWidth = 236, SmallHeight = 133, LabelScale = 2, LabelRoom = 16;
        const string OnlineEditor = ", Wreckabulary.Online.Authoring.Editor", OnlineClient = ", Wreckabulary.Online.Client";
        const string StageType = "Wreckabulary.Online.Editor.WordboundSoloPreviewStage" + OnlineEditor;
        const string SettingsType = "Wreckabulary.Online.Editor.WordboundSoloPreviewSettings" + OnlineEditor;
        const string PoseType = "Wreckabulary.Online.Editor.WordboundSoloPreviewPose" + OnlineEditor;
        const string ContentType = "Wreckabulary.Online.WordboundClientContent" + OnlineClient;
        const string LaunchType = "Wreckabulary.Online.WordboundLaunchAssets" + OnlineClient;
        const string ArenaMode = "FreeForAll", ArenaFormat = "Solo", ArenaSeed = "0123456789abcdef", ArenaModule = "Start", ArenaLabel = "Toybox Arena";
        const int StageInputs = 6;
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Color SunColour = LobbyKit.Hex(0xfff3dc), FillColour = LobbyKit.Hex(0xfff3e6), Ambient = new Color(.38f, .36f, .34f);
        static readonly Color SkyHigh = LobbyStage.SkyTop, SkyMid = LobbyKit.Hex(0xf8eccf), SkyLow = LobbyKit.Hex(0xfff4de), PaperCentre = LobbyKit.Hex(0xf8eccf);

        public static readonly IReadOnlyList<MapHeroShot> Heroes = new[]
        {
            MapHeroShot.Room("pinwheel", "Playroom at the hub, pinwheel arms running out", -10f, -10f, 10f, 10f, 0, 40f, 42f, 30f, 1f, locked: true),
            MapHeroShot.Room("courtyard", "Open-air fountain square with the house wings behind", -14f, -13f, 14f, 12f, 0, 15f, 34f, 30f, 1.05f, locked: true),
            MapHeroShot.Room("flat", "Roof garden washing lines, used only if it beats the searched rooms", 5f, -5f, 20f, 13f, 1, 0f, 26f, 30f, 1.1f),
            MapHeroShot.Room("terrace", "Attic crate pile, used only if it beats the searched rooms", 3f, 6f, 16f, 14f, 2, 0f, 32f, 30f, 1f),
            MapHeroShot.Dollhouse("walkup", "Four storeys cut open like a dollhouse, fire escapes in front", -18f, -12f, 12f, 12f, 270f, 10f, 28f, 1f, locked: true),
            MapHeroShot.Course(WordboundSoloEntry.ParcelServiceMap, "Loading ramp and tables, industrial skyline above the wall", new[] { "Hall" }, 0f, -3f, 58f, 2f, .8f, locked: true),
            MapHeroShot.Course(WordboundSoloEntry.LostAddressMap, "Course tables in front, old brick town behind", new[] { "Rooftop_PottingSort", "Attic_LostLabels", "Kitchen_CutlerySort", "" }, 0f, -1f, 44f, 1.6f, .9f),
            MapHeroShot.Course(WordboundSoloEntry.ParcelHallMap, "Parcel gate and its furniture, civic glass towers behind", new[] { "Packing_ParcelGate" }, 0f, 4f, 56f, 2.4f, .85f, locked: true),
            MapHeroShot.Arena(LobbyMapSnapshots.ToyboxArena, "Arena floor at eye height with the skyline above the walls", new[] { ArenaModule }, 0f, -2f, 58f, 2f, .85f, locked: true),
        };

        sealed class Shot { public string id, path, source, note, guard; public int bytes; public Color32[] pixels; public byte[] previous; }

        readonly struct Guard
        {
            public readonly float Flat, Share;
            public readonly int Props;
            public Guard(float flat, int props, float share) { Flat = flat; Props = props; Share = share; }
            public bool Passed => Flat <= FlatLimit && Props >= LeastProps;
            public float Value => 1f - Flat + PropWeight * Mathf.Min(Props, PropRows) / PropRows + ShareWeight * Mathf.Min(1f, Share * ShareScale);
            public string Line => "flat " + Mathf.RoundToInt(Flat * 100f) + " props " + Props + " cover " + Mathf.RoundToInt(Share * 100f) +
                (Flat > FlatLimit ? " FLAT FRAME" : "") + (Props < LeastProps ? " FEW PROPS" : "");
        }

        sealed class Candidate
        {
            public string Name;
            public MapHeroShot Shot;
            public string Module;
            public Vector3 Eye;
            public View View;
            public Guard Score;
        }

        sealed class Partition { public Bounds Box; public float Top; public int Storey; public bool AlongX, Lowered; }

        readonly struct View
        {
            public readonly float Yaw, Pitch;
            public View(float yaw, float pitch) { Yaw = yaw; Pitch = pitch; }
            public Vector3 Forward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
            public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);
            public float SunYaw => Yaw + SunTurn;
            public float FillYaw => Yaw - FillTurn;
        }

        public static MapHeroShot HeroFor(string id) => Heroes.FirstOrDefault(hero => hero.Id == id);

        [MenuItem("Wreckabulary/Art/Capture map hero snapshots")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture map snapshots in Edit Mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Map snapshots need a graphics device; do not run with -nographics.");
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline) throw new InvalidOperationException("Map snapshots expect the game's URP pipeline asset to be active.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            if (!data) throw new InvalidOperationException("Resources/GameData is missing; run Wreckabulary > Data > Set Up Game Data.");
            var houses = data.Parse().Houses;
            var plan = Plan(houses.Keys);
            Log("start: " + plan.Count + " hero shots -> " + Folder + " (" + Width + "x" + Height + ")");

            string scenes = SceneState(), global = Global();
            var selection = Selection.objects; var selected = Selection.activeObject;
            var target = RenderTexture.active; bool srgb = GL.sRGBWrite, async = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            float shadowDistance = pipeline.shadowDistance;
            int tactile = TactileMaterials.RegisteredScopeCount;
            var shots = new List<Shot>(); var skipped = new List<string>(); var errors = new List<Exception>();
            Exception failure = null;
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                EnsureFolder();
                foreach (var hero in plan)
                {
                    if (hero.House)
                    {
                        if (!houses.TryGetValue(hero.Id, out var layout)) { skipped.Add(hero.Id + ": no such house in GameData"); continue; }
                        Attempt(hero, () => House(hero, layout, pipeline), shots, skipped);
                    }
                    else if (hero.Kind == MapHeroKind.Course) Attempt(hero, () => Course(hero, pipeline), shots, skipped);
                    else Attempt(hero, () => Arena(hero, pipeline), shots, skipped);
                }
            }
            catch (Exception error) { failure = error; }
            finally
            {
                Clean(() => pipeline.shadowDistance = shadowDistance, errors);
                Clean(() => EditorSettings.asyncShaderCompilation = async, errors);
                Clean(() => RenderTexture.active = target, errors); Clean(() => GL.sRGBWrite = srgb, errors); Clean(() => SetSsao(ssao), errors);
                Clean(() => { Selection.objects = selection; Selection.activeObject = selected; }, errors);
            }
            if (failure == null) Clean(() => Import(shots), errors);
            string sheet = WriteSheet(shots);
            bool scenesKept = SceneState() == scenes, globalsKept = Global() == global, tactileKept = TactileMaterials.RegisteredScopeCount == tactile;
            foreach (var shot in shots) Log("wrote " + shot.path + " (" + shot.bytes + " bytes, " + shot.source + (shot.note == null ? "" : ", " + shot.note) + ")");
            foreach (string reason in skipped) Debug.LogWarning("[Wreckabulary] Map snapshot skipped " + reason);
            string summary = "[Wreckabulary] Map snapshots " + (failure == null && errors.Count == 0 && scenesKept && globalsKept && tactileKept ? "DONE" : "FAIL") +
                ": written=" + shots.Count + " skipped=" + skipped.Count + " scenesPreserved=" + scenesKept + " globalsPreserved=" + globalsKept +
                " tactileReleased=" + tactileKept + " folder=" + Folder + " sheet=" + sheet;
            if (failure != null) throw new InvalidOperationException(summary, failure);
            if (errors.Count > 0) throw new AggregateException(summary, errors);
            if (!scenesKept || !globalsKept || !tactileKept) throw new InvalidOperationException(summary + "\nscenes before: " + scenes + "\nscenes after: " + SceneState() + "\nglobals before: " + global + "\nglobals after: " + Global());
            if (skipped.Count > 0) Debug.LogWarning(summary); else Debug.Log(summary);
        }

        static List<MapHeroShot> Plan(IEnumerable<string> houses)
        {
            var plan = Heroes.ToList();
            foreach (string id in houses.OrderBy(id => id, StringComparer.Ordinal))
                if (plan.All(hero => hero.Id != id)) plan.Add(MapHeroShot.Room(id, "automatic view of the spawn storey", 0f, 0f, 0f, 0f, -1, AutoYaw, AutoPitch, AutoFieldOfView, 1f));
            return plan;
        }

        static void Attempt(MapHeroShot hero, Func<(Color32[] pixels, string source, string note, Guard guard)> render, List<Shot> shots, List<string> skipped)
        {
            try
            {
                var (pixels, source, note, guard) = render();
                byte[] png = Encode(pixels);
                string path = Folder + "/" + hero.Id + ".png", file = Full(path);
                byte[] previous = File.Exists(file) ? File.ReadAllBytes(file) : null;
                File.WriteAllBytes(file, png);
                shots.Add(new Shot { id = hero.Id, path = path, source = source, note = hero.Pose + "; " + guard.Line + "; " + note, guard = guard.Line, bytes = png.Length, pixels = pixels, previous = previous });
            }
            catch (Exception error) when (error is not (IOException or UnauthorizedAccessException))
            {
                var cause = error is TargetInvocationException { InnerException: { } inner } ? inner : error;
                skipped.Add(hero.Id + ": " + cause.GetType().Name + ": " + cause.Message);
            }
        }

        static (Color32[] pixels, string source, string note, Guard guard) House(MapHeroShot hero, HouseLayout layout, UniversalRenderPipelineAsset pipeline)
        {
            PreviewRenderUtility preview = null; GameObject anchor = null; Scene scene = default;
            try
            {
                preview = new PreviewRenderUtility(); scene = preview.camera.gameObject.scene;
                if (!EditorSceneManager.IsPreviewScene(scene)) throw new InvalidOperationException("Expected an owned preview scene.");
                anchor = EditorUtility.CreateGameObjectWithHideFlags("Map snapshot " + hero.Id, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(anchor, scene);
                var (geometry, source) = Build(hero.Id, layout, anchor.transform);
                if (anchor.scene != scene || anchor.GetComponentsInChildren<Transform>(true).Any(part => part.gameObject.scene != scene))
                    throw new InvalidOperationException("The house did not stay inside the owned preview scene.");
                foreach (var part in anchor.GetComponentsInChildren<Transform>(true)) part.gameObject.hideFlags = HideFlags.HideAndDontSave;
                var floors = layout.StoreyFloors();
                if (layout.Rooms.Count == 0) throw new InvalidOperationException("The house has no rooms to frame.");
                var yard = Footprint(layout.Rooms);
                yard.Expand(new Vector3(YardMargin, 0f, YardMargin) * 2f);
                var initial = anchor.GetComponentsInChildren<Renderer>(true).ToDictionary(renderer => renderer, renderer => renderer.forceRenderingOff);
                var candidates = new List<Candidate> { new Candidate { Name = "hand pose", Shot = hero } };
                if (!hero.Locked) candidates.AddRange(RoomCandidates(hero.Id, layout));
                foreach (var candidate in candidates)
                {
                    Restore(initial);
                    var (focus, view, _) = Prepare(candidate.Shot, layout, anchor.transform, geometry, floors, yard);
                    var (position, nearest, farthest) = Frame(focus, view, candidate.Shot.FieldOfView, candidate.Shot.Fill);
                    candidate.Score = Probe(preview, position, view, candidate.Shot.FieldOfView, Mathf.Max(MinimumNear, nearest - NearSlack), farthest + FarSlack, pipeline, null);
                }
                var chosen = Choose(candidates, HandMargin);
                Restore(initial);
                var (shotFocus, shotView, note) = Prepare(chosen.Shot, layout, anchor.transform, geometry, floors, yard);
                var (pixels, framing, guard) = Shoot(preview, shotFocus, shotView, chosen.Shot.FieldOfView, chosen.Shot.Fill, pipeline);
                return (pixels, source, "subject " + chosen.Shot.Subject + ", chose " + chosen.Name + " of " + candidates.Count + " (" + Scores(candidates) + "), " + note + ", " + framing, guard);
            }
            finally
            {
                var errors = new List<Exception>();
                Clean(() => { if (anchor) Object.DestroyImmediate(anchor); }, errors);
                Clean(() => preview?.Cleanup(), errors);
                Clean(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); }, errors);
                if (errors.Count > 0) throw new AggregateException("Map snapshot cleanup failed: " + hero.Id, errors);
            }
        }

        static (Bounds focus, View view, string note) Prepare(MapHeroShot shot, HouseLayout layout, Transform anchor, Transform geometry, List<float> floors, Bounds yard)
        {
            var view = new View(shot.Yaw, shot.Pitch);
            if (shot.Kind == MapHeroKind.Dollhouse)
            {
                var whole = DollhouseFocus(shot, layout, floors);
                int cut = Section(anchor, geometry, layout, view.Forward);
                HideOutlying(anchor, yard);
                return (whole, view, "all " + floors.Count + " storeys, " + cut + " facade parts removed");
            }
            int storey = shot.Storey >= 0 ? Mathf.Min(shot.Storey, floors.Count - 1) : FocusStorey(layout);
            var focus = RoomFocus(shot, layout, floors, storey);
            var walls = SetWalls(geometry, layout, floors, storey, view.Forward, focus);
            CutAway(anchor, geometry, floors, storey, walls, yard);
            return (focus, view, (floors.Count > 1 ? layout.StoreyLabel(storey).ToLowerInvariant() + " shown" : "single floor") + ", " +
                walls.Count(wall => wall.Lowered) + " of " + walls.Count + " walls low");
        }

        static IEnumerable<Candidate> RoomCandidates(string id, HouseLayout layout)
        {
            foreach (var room in layout.Rooms)
            {
                var pieces = layout.Furniture.Where(piece => piece.Room == room.Name).ToList();
                if (pieces.Count < LeastPieces) continue;
                float minX = Mathf.Max(room.MinX, pieces.Min(piece => piece.X) - FurnishedMargin), maxX = Mathf.Min(room.MaxX, pieces.Max(piece => piece.X) + FurnishedMargin);
                float minZ = Mathf.Max(room.MinZ, pieces.Min(piece => piece.Z) - FurnishedMargin), maxZ = Mathf.Min(room.MaxZ, pieces.Max(piece => piece.Z) + FurnishedMargin);
                if (maxX - minX < FurnishedMargin || maxZ - minZ < FurnishedMargin) continue;
                int storey = layout.StoreyOf(room);
                foreach (float yaw in AutoYaws)
                    yield return new Candidate { Name = room.Name + " yaw " + yaw.ToString("F0"),
                        Shot = MapHeroShot.Room(id, "furnished " + Spaced(room.Name), minX, minZ, maxX, maxZ, storey, yaw, AutoPitch, AutoFieldOfView, 1f) };
            }
        }

        static string Spaced(string name) =>
            string.Concat(name.Select((letter, index) => index > 0 && (char.IsUpper(letter) || char.IsDigit(letter) && !char.IsDigit(name[index - 1])) ? " " + char.ToLowerInvariant(letter) : letter.ToString()));

        static Candidate Choose(List<Candidate> candidates, float handMargin)
        {
            Candidate best = null;
            foreach (var candidate in candidates)
                if (best == null || candidate.Score.Value > best.Score.Value + Tie ||
                    Mathf.Abs(candidate.Score.Value - best.Score.Value) <= Tie && string.CompareOrdinal(candidate.Name, best.Name) < 0) best = candidate;
            var hand = candidates[0];
            return hand.Score.Value >= best.Score.Value - handMargin ? hand : best;
        }

        static string Scores(List<Candidate> candidates) =>
            string.Join("; ", candidates.OrderByDescending(candidate => candidate.Score.Value).ThenBy(candidate => candidate.Name, StringComparer.Ordinal).Take(4)
                .Select(candidate => candidate.Name + " " + candidate.Score.Value.ToString("F2") + " [" + candidate.Score.Line + "]"));

        static void Restore(Dictionary<Renderer, bool> initial)
        {
            foreach (var pair in initial) if (pair.Key && pair.Key.forceRenderingOff != pair.Value) pair.Key.forceRenderingOff = pair.Value;
        }

        static (Transform geometry, string source) Build(string id, HouseLayout layout, Transform anchor)
        {
            string path = AuthoredHouse.ResourcePath(id);
            var prefab = string.IsNullOrEmpty(path) ? null : Resources.Load<GameObject>(path);
            var authored = prefab ? prefab.GetComponent<AuthoredHouse>() : null;
            if (authored && authored.IsCurrent && authored.Matches(id))
            {
                var copy = Object.Instantiate(prefab, anchor, false).GetComponent<AuthoredHouse>();
                return (copy.GeometryRoot, "furnished prefab Resources/" + path);
            }
            var geometry = RoomBuilder.CreateGeometry(layout, id, anchor);
            RoomBuilder.SetTallWalls(geometry, true);
            var furniture = EditorUtility.CreateGameObjectWithHideFlags("Original furniture", HideFlags.HideAndDontSave).transform;
            furniture.SetParent(anchor, false);
            foreach (var piece in layout.Furniture)
            {
                var room = layout.Room(piece.Room);
                FurnitureCatalog.Spawn(piece.Word, new Vector3(piece.X, (room?.FloorY ?? 0f) + piece.Y, piece.Z), piece.Yaw, furniture);
            }
            return (geometry, "built from house_" + id + ".json (no current authored prefab)");
        }

        static int FocusStorey(HouseLayout layout) => layout.Spawns.Select(spawn => layout.Room(spawn.Room)).Where(room => room != null)
            .GroupBy(layout.StoreyOf).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).Select(group => group.Key).DefaultIfEmpty(0).First();

        static Bounds RoomFocus(MapHeroShot hero, HouseLayout layout, List<float> floors, int storey)
        {
            float floor = storey < floors.Count ? floors[storey] : 0f;
            var area = hero.Area;
            if (area.width <= 0f || area.height <= 0f)
            {
                var rooms = layout.Rooms.Where(room => layout.StoreyOf(room) == storey).ToList();
                var footprint = Footprint(rooms.Count > 0 ? rooms : layout.Rooms.ToList());
                area = Rect.MinMaxRect(footprint.min.x, footprint.min.z, footprint.max.x, footprint.max.z);
            }
            var box = new Bounds();
            box.SetMinMax(new Vector3(area.xMin, floor, area.yMin), new Vector3(area.xMax, floor + FurnitureHeight, area.yMax));
            return box;
        }

        static Bounds DollhouseFocus(MapHeroShot hero, HouseLayout layout, List<float> floors)
        {
            float top = (floors.Count > 0 ? floors[floors.Count - 1] : 0f) + (layout.Rooms.Any(room => room.Outdoor && layout.StoreyOf(room) == floors.Count - 1) ? RoofRoom : StoreyHeight);
            var box = new Bounds();
            box.SetMinMax(new Vector3(hero.Area.xMin, 0f, hero.Area.yMin), new Vector3(hero.Area.xMax, top, hero.Area.yMax));
            return box;
        }

        static Bounds Footprint(IReadOnlyList<RoomBox> rooms)
        {
            var box = new Bounds();
            box.SetMinMax(new Vector3(rooms.Min(r => r.MinX), rooms.Min(r => r.FloorY), rooms.Min(r => r.MinZ)),
                new Vector3(rooms.Max(r => r.MaxX), rooms.Max(r => r.FloorY), rooms.Max(r => r.MaxZ)));
            return box;
        }

        static List<Partition> SetWalls(Transform geometry, HouseLayout layout, List<float> floors, int focus, Vector3 forward, Bounds area)
        {
            var walls = new List<Partition>();
            if (!geometry) return walls;
            foreach (var wall in geometry.GetComponentsInChildren<TallWall>(true))
            {
                int storey = StoreyAt(floors, wall.FloorY);
                if (storey > focus || !wall.TryGetComponent<Renderer>(out var renderer) || !wall.TryGetComponent<BoxCollider>(out _)) continue;
                var box = renderer.bounds;
                bool alongX = box.size.x >= box.size.z;
                bool inside = box.center.x >= area.min.x && box.center.x <= area.max.x && box.center.z >= area.min.z && box.center.z <= area.max.z;
                bool tall;
                if (inside && storey == focus) tall = Vector3.Dot(box.center - area.center, forward) > 0f;
                else
                {
                    var nearSide = NearSide(box, alongX, forward);
                    tall = layout.Rooms.Any(room => !room.Outdoor && layout.StoreyOf(room) == storey && room.Contains(nearSide.x, nearSide.z));
                }
                wall.Apply(tall);
                float rise = wall.VisualHeight(tall);
                walls.Add(new Partition { Box = box, Top = wall.FloorY + rise, Storey = storey, AlongX = alongX, Lowered = rise <= TallWall.Low });
            }
            return walls;
        }

        static Vector3 NearSide(Bounds box, bool alongX, Vector3 forward)
        {
            var away = alongX ? Vector3.forward : Vector3.right;
            if (Vector3.Dot(away, forward) < 0f) away = -away;
            return box.center - away * ((alongX ? box.extents.z : box.extents.x) + WallProbe);
        }

        static void CutAway(Transform root, Transform geometry, List<float> floors, int focus, List<Partition> walls, Bounds yard)
        {
            var settled = new HashSet<Renderer>();
            foreach (var surface in root.GetComponentsInChildren<CutawaySurface>(true))
                foreach (var renderer in surface.Renderers)
                {
                    if (!renderer || !settled.Add(renderer)) continue;
                    bool onWall = surface.Kind == CutawayKind.UpperWall;
                    bool open = surface.OwnerStorey > focus || !onWall && surface.OwnerStorey == focus;
                    if (open || onWall && AboveWall(renderer.bounds, surface.OwnerStorey, walls, floors)) renderer.forceRenderingOff = true;
                }
            var fixedParts = new HashSet<Renderer>();
            for (int storey = 0; geometry && storey < floors.Count; storey++)
            {
                var group = geometry.Find(RoomBuilder.StoreyName(storey));
                if (!group) continue;
                foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                {
                    fixedParts.Add(renderer);
                    if (storey > focus && settled.Add(renderer)) renderer.forceRenderingOff = true;
                }
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var box = renderer.bounds;
                bool above = !fixedParts.Contains(renderer) && !settled.Contains(renderer) && StoreyAt(floors, box.min.y) > focus;
                if (above || Outlying(box, yard)) renderer.forceRenderingOff = true;
            }
        }

        static int Section(Transform root, Transform geometry, HouseLayout layout, Vector3 forward)
        {
            if (geometry)
                foreach (var wall in geometry.GetComponentsInChildren<TallWall>(true)) wall.Apply(true);
            var indoor = layout.Rooms.Where(room => !room.Outdoor).ToList();
            if (indoor.Count == 0) indoor = layout.Rooms.ToList();
            var toward = -new Vector3(forward.x, 0f, forward.z).normalized;
            float face = indoor.Max(room => Mathf.Max(
                Mathf.Max(Vector3.Dot(new Vector3(room.MinX, 0f, room.MinZ), toward), Vector3.Dot(new Vector3(room.MaxX, 0f, room.MinZ), toward)),
                Mathf.Max(Vector3.Dot(new Vector3(room.MinX, 0f, room.MaxZ), toward), Vector3.Dot(new Vector3(room.MaxX, 0f, room.MaxZ), toward))));
            int cut = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var box = renderer.bounds;
                float thickness = Mathf.Abs(toward.x) * box.size.x + Mathf.Abs(toward.z) * box.size.z;
                float at = Vector3.Dot(new Vector3(box.center.x, 0f, box.center.z), toward);
                if (thickness > SectionThin || Mathf.Abs(at - face) > SectionBand || renderer.forceRenderingOff) continue;
                renderer.forceRenderingOff = true;
                cut++;
            }
            return cut;
        }

        static void HideOutlying(Transform root, Bounds yard)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (Outlying(renderer.bounds, yard)) renderer.forceRenderingOff = true;
        }

        static bool AboveWall(Bounds detail, int storey, List<Partition> walls, List<float> floors)
        {
            float top = float.MaxValue; bool lowered = true;
            foreach (var wall in walls)
            {
                if (wall.Storey != storey) continue;
                float from = wall.AlongX ? wall.Box.min.x : wall.Box.min.z, to = wall.AlongX ? wall.Box.max.x : wall.Box.max.z;
                float low = wall.AlongX ? detail.min.x : detail.min.z, high = wall.AlongX ? detail.max.x : detail.max.z;
                float gap = wall.AlongX ? Mathf.Abs(detail.center.z - wall.Box.center.z) : Mathf.Abs(detail.center.x - wall.Box.center.x);
                float reach = (wall.AlongX ? wall.Box.extents.z : wall.Box.extents.x) + WallReach;
                if (low > to || high < from || gap > reach || wall.Top >= top) continue;
                top = wall.Top; lowered = wall.Lowered;
            }
            if (top == float.MaxValue) top = (storey >= 0 && storey < floors.Count ? floors[storey] : 0f) + TallWall.Low;
            return detail.max.y > top + (lowered ? LowSlack : WallSlack);
        }

        static bool Outlying(Bounds box, Bounds yard) =>
            box.size.x > yard.size.x + Oversize || box.size.z > yard.size.z + Oversize ||
            box.center.x < yard.min.x || box.center.x > yard.max.x || box.center.z < yard.min.z || box.center.z > yard.max.z;

        static int StoreyAt(List<float> floors, float y)
        {
            int storey = 0;
            for (int i = 1; i < floors.Count; i++) if (floors[i] <= y + HouseLayout.StandingSlack) storey = i;
            return storey;
        }

        static (Color32[] pixels, string framing, Guard guard) Shoot(PreviewRenderUtility preview, Bounds focus, View view, float fieldOfView, float fill, UniversalRenderPipelineAsset pipeline)
        {
            var (position, nearest, farthest) = Frame(focus, view, fieldOfView, fill);
            return ShootAt(preview, position, view, fieldOfView, Mathf.Max(MinimumNear, nearest - NearSlack), farthest + FarSlack, pipeline);
        }

        static void Aim(PreviewRenderUtility preview, Vector3 position, View view, float fieldOfView, float near, float far, UniversalRenderPipelineAsset pipeline)
        {
            pipeline.shadowDistance = Mathf.Min(MaximumShadowDistance, Mathf.Max(pipeline.shadowDistance, Mathf.Min(far, MaximumShadowDistance)));
            var camera = preview.camera;
            camera.orthographic = false; camera.fieldOfView = fieldOfView; camera.aspect = Width / (float)Height;
            camera.nearClipPlane = near; camera.farClipPlane = far; camera.cullingMask = ~0;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.allowHDR = false; camera.allowMSAA = true;
            camera.transform.SetPositionAndRotation(position, view.Rotation);
            Illuminate(preview, view);
        }

        static (Color32[] pixels, string framing, Guard guard) ShootAt(PreviewRenderUtility preview, Vector3 position, View view, float fieldOfView, float near, float far,
            UniversalRenderPipelineAsset pipeline, HashSet<Renderer> scenery = null)
        {
            Aim(preview, position, view, fieldOfView, near, far, pipeline);
            bool composed = Compose(Render(preview, Color.white), Render(preview, Color.black), out var pixels, out float cover, out float gain);
            if (!composed) pixels = Render(preview, PaperCentre);
            RequireVaried(pixels);
            var guard = Measure(preview.camera, pixels, scenery);
            return (pixels, "camera at " + position.x.ToString("F1") + "," + position.y.ToString("F1") + "," + position.z.ToString("F1") + ", clip " +
                near.ToString("F1") + " to " + far.ToString("F0") + " m, " +
                (composed ? "scene covers " + Mathf.RoundToInt(cover * 100f) + "% over the sky, gain " + gain.ToString("F2") : "NEARLY EMPTY (" + Mathf.RoundToInt(cover * 100f) + "% covered)"), guard);
        }

        static Guard Probe(PreviewRenderUtility preview, Vector3 position, View view, float fieldOfView, float near, float far, UniversalRenderPipelineAsset pipeline, HashSet<Renderer> scenery)
        {
            Aim(preview, position, view, fieldOfView, near, far, pipeline);
            Texture2D image = null; bool begun = false; var active = RenderTexture.active;
            try
            {
                preview.camera.backgroundColor = PaperCentre;
                preview.BeginStaticPreview(new Rect(0, 0, ProbeWidth, ProbeHeight)); begun = true;
                SetSsao(false); preview.Render(true, false);
                image = preview.EndStaticPreview(); begun = false;
                if (!image) throw new InvalidOperationException("The probe render returned no image.");
                return Measure(preview.camera, image.GetPixels32(), scenery);
            }
            finally
            {
                if (begun) { var pending = preview.EndStaticPreview(); if (pending) Object.DestroyImmediate(pending); }
                if (image) Object.DestroyImmediate(image);
                RenderTexture.active = active;
            }
        }

        static Guard Measure(Camera camera, Color32[] pixels, HashSet<Renderer> scenery)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            int props = 0; float share = 0f;
            foreach (var root in camera.gameObject.scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (renderer is not (MeshRenderer or SkinnedMeshRenderer) || !renderer.enabled || renderer.forceRenderingOff ||
                        renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly || scenery != null && scenery.Contains(renderer)) continue;
                    var box = renderer.bounds;
                    if (box.size.y > PropHeight || box.size.x > PropSpan || box.size.z > PropSpan || !GeometryUtility.TestPlanesAABB(planes, box)) continue;
                    props++;
                    share += ScreenShare(camera, box);
                }
            return new Guard(Flatness(pixels), props, Mathf.Min(1f, share));
        }

        static float ScreenShare(Camera camera, Bounds box)
        {
            float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
            foreach (var corner in Corners(box))
            {
                var at = camera.WorldToViewportPoint(corner);
                if (at.z <= 0f) return 0f;
                minX = Mathf.Min(minX, at.x); maxX = Mathf.Max(maxX, at.x); minY = Mathf.Min(minY, at.y); maxY = Mathf.Max(maxY, at.y);
            }
            return Mathf.Max(0f, Mathf.Min(1f, maxX) - Mathf.Max(0f, minX)) * Mathf.Max(0f, Mathf.Min(1f, maxY) - Mathf.Max(0f, minY));
        }

        static float Flatness(Color32[] pixels)
        {
            int shift = 8 - FlatBits, levels = 1 << FlatBits;
            var counts = new int[levels * levels * levels];
            foreach (var c in pixels) counts[(c.r >> shift) * levels * levels + (c.g >> shift) * levels + (c.b >> shift)]++;
            int modal = 0;
            for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[modal]) modal = i;
            long r = 0, g = 0, b = 0; int members = 0;
            foreach (var c in pixels)
            {
                if ((c.r >> shift) * levels * levels + (c.g >> shift) * levels + (c.b >> shift) != modal) continue;
                r += c.r; g += c.g; b += c.b; members++;
            }
            if (members == 0) return 0f;
            int mr = (int)(r / members), mg = (int)(g / members), mb = (int)(b / members), near = 0;
            foreach (var c in pixels)
                if (Mathf.Abs(c.r - mr) <= FlatReach && Mathf.Abs(c.g - mg) <= FlatReach && Mathf.Abs(c.b - mb) <= FlatReach) near++;
            return near / (float)pixels.Length;
        }

        static (Vector3 position, float nearest, float farthest) Frame(Bounds focus, View view, float fieldOfView, float fill)
        {
            var points = Corners(focus).ToList();
            var rotation = view.Rotation;
            var (position, nearest, farthest) = Fit(points, rotation, fieldOfView, Width / (float)Height);
            float scale = 1f / Mathf.Max(.25f, fill);
            var moved = focus.center + (position - focus.center) * scale;
            float shift = Vector3.Dot(position - moved, rotation * Vector3.forward);
            return (moved, nearest + shift, farthest + shift);
        }

        static (Vector3 position, float nearest, float farthest) Fit(List<Vector3> points, Quaternion rotation, float fieldOfView, float aspect)
        {
            var right = rotation * Vector3.right; var up = rotation * Vector3.up; var forward = rotation * Vector3.forward;
            float tan = Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad), across = tan * aspect, above = tan, below = tan;
            float leftLimit = float.MaxValue, rightLimit = float.MinValue, lowLimit = float.MaxValue, highLimit = float.MinValue;
            float front = float.MaxValue, back = float.MinValue;
            foreach (var point in points)
            {
                float x = Vector3.Dot(point, right), y = Vector3.Dot(point, up), z = Vector3.Dot(point, forward);
                leftLimit = Mathf.Min(leftLimit, x + across * z); rightLimit = Mathf.Max(rightLimit, x - across * z);
                lowLimit = Mathf.Min(lowLimit, y + below * z); highLimit = Mathf.Max(highLimit, y - above * z);
                front = Mathf.Min(front, z); back = Mathf.Max(back, z);
            }
            float depth = Mathf.Min((leftLimit - rightLimit) / (2f * across), (lowLimit - highLimit) / (above + below));
            float side = (leftLimit + rightLimit) * .5f, height = (highLimit + above * depth + lowLimit - below * depth) * .5f;
            var position = right * side + up * height + forward * depth;
            float nearest = front - depth, farthest = back - depth;
            if (!float.IsFinite(depth) || !float.IsFinite(side) || !float.IsFinite(height) || farthest <= 0f)
                throw new InvalidOperationException("The camera fit is degenerate for " + points.Count + " points.");
            return (position, nearest, farthest);
        }

        static void Illuminate(PreviewRenderUtility preview, View view)
        {
            var lights = preview.lights;
            var sun = lights[0];
            sun.type = LightType.Directional; sun.cullingMask = ~0;
            sun.color = SunColour; sun.intensity = SunIntensity;
            sun.transform.rotation = Quaternion.Euler(SunPitch, view.SunYaw, 0f);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = ShadowStrength;
            for (int i = 1; i < lights.Length; i++) { lights[i].intensity = 0f; lights[i].shadows = LightShadows.None; }
            if (lights.Length > 1)
            {
                var fill = lights[1];
                fill.type = LightType.Directional; fill.cullingMask = ~0;
                fill.color = FillColour; fill.intensity = FillIntensity;
                fill.transform.rotation = Quaternion.Euler(FillPitch, view.FillYaw, 0f);
            }
            preview.ambientColor = Ambient;
        }

        static (Color32[] pixels, string source, string note, Guard guard) Course(MapHeroShot hero, UniversalRenderPipelineAsset pipeline)
        {
            var stageType = Type.GetType(StageType); var settingsType = Type.GetType(SettingsType);
            if (stageType == null || settingsType == null) throw new InvalidOperationException("The Solo preview stage is not compiled in this editor; the card keeps its play-layout preview.");
            string path = settingsType.GetField("Path", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
            var saved = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, settingsType);
            if (!saved) throw new InvalidOperationException("No saved Solo preview settings at " + (path ?? "(unknown path)") + ".");
            var create = stageType.GetConstructor(Members, null, new[] { settingsType, typeof(string) }, null);
            if (create == null) throw new MissingMemberException("WordboundSoloPreviewStage changed; review MapSnapshotCapture.Course.");
            Object settings = null; IDisposable stage = null;
            try
            {
                settings = Object.Instantiate(saved);
                settings.hideFlags = HideFlags.HideAndDontSave;
                string anchor = Anchor(settingsType, settings, hero.Id);
                stage = (IDisposable)create.Invoke(new object[] { settings, hero.Id });
                var (pixels, note, guard) = Diorama(stageType, stage, hero, pipeline);
                return (pixels, "installed Solo content", note + anchor, guard);
            }
            finally
            {
                stage?.Dispose();
                if (settings) Object.DestroyImmediate(settings);
            }
        }

        static (Color32[] pixels, string source, string note, Guard guard) Arena(MapHeroShot hero, UniversalRenderPipelineAsset pipeline)
        {
            var stageType = Type.GetType(StageType); var poseType = Type.GetType(PoseType);
            var contentType = Type.GetType(ContentType); var launchType = Type.GetType(LaunchType);
            if (stageType == null || poseType == null || contentType == null || launchType == null)
                throw new InvalidOperationException("The online course content is not compiled in this editor; the panel keeps its play-layout preview.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            string path = launchType.GetField("ResourcePath", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
            var launch = string.IsNullOrEmpty(path) ? null : Resources.Load(path, launchType);
            if (!data || !data.items || !data.rules || !launch) throw new InvalidOperationException("The installed arena inputs are missing.");
            var create = stageType.GetConstructors(Members).FirstOrDefault(candidate => candidate.GetParameters().Length == StageInputs);
            var profile = launchType.GetField("VisualProfile", BindingFlags.Public | BindingFlags.Instance);
            var module = poseType.GetField("Module", BindingFlags.Public | BindingFlags.Instance);
            var course = contentType.GetProperty("Course"); var geometry = contentType.GetProperty("Geometry"); var items = contentType.GetProperty("Items");
            if (create == null || profile == null || module == null || course == null || geometry == null || items == null)
                throw new MissingMemberException("The online course content changed; review MapSnapshotCapture.Arena.");
            object content = Activator.CreateInstance(contentType, data.items.text, data.rules.text, ArenaMode, ArenaFormat, ArenaSeed);
            object pose = Activator.CreateInstance(poseType);
            module.SetValue(pose, ArenaModule);
            IDisposable stage = null;
            try
            {
                stage = (IDisposable)create.Invoke(new[] { course.GetValue(content), geometry.GetValue(content), items.GetValue(content),
                    profile.GetValue(launch), pose, ArenaLabel });
                var (pixels, note, guard) = Diorama(stageType, stage, hero, pipeline);
                return (pixels, "installed online arena", note, guard);
            }
            finally { stage?.Dispose(); }
        }

        static (Color32[] pixels, string note, Guard guard) Diorama(Type stageType, object stage, MapHeroShot hero, UniversalRenderPipelineAsset pipeline)
        {
            var previewField = stageType.GetField("Preview", Members);
            var worldField = stageType.GetField("World", Members);
            if (previewField == null || worldField == null) throw new MissingMemberException("WordboundSoloPreviewStage changed; review MapSnapshotCapture.Diorama.");
            var preview = previewField.GetValue(stage) as PreviewRenderUtility ?? throw new InvalidOperationException("The Solo preview stage has no preview renderer.");
            var world = worldField.GetValue(stage) as WordboundCourseWorld ?? throw new InvalidOperationException("The Solo preview stage has no course world.");
            var owned = new List<Mesh>();
            GameObject backdrop = null;
            try
            {
                string skyline = "no skyline";
                float heading = 0f;
                try
                {
                    var kit = CourseBackdropKit.Load();
                    if (kit && WordboundCourseBackdrop.Supported)
                    {
                        backdrop = WordboundCourseBackdrop.Build(world, kit, false, owned, out var layout);
                        foreach (var part in backdrop.GetComponentsInChildren<Transform>(true)) part.gameObject.hideFlags = HideFlags.HideAndDontSave;
                        if (layout.Skyline.Headings.Count > 0) heading = layout.Skyline.Headings[0];
                        skyline = "town and skyline built (" + backdrop.GetComponentsInChildren<Renderer>(true).Length + " renderers), downtown heading " + heading.ToString("F0");
                    }
                }
                catch (Exception error) { skyline = "skyline skipped (" + error.Message + ")"; }
                var scenery = backdrop ? new HashSet<Renderer>(backdrop.GetComponentsInChildren<Renderer>(true)) : null;
                var candidates = new List<Candidate>();
                var missing = new List<string>();
                foreach (var (module, turn) in Poses(hero).Take(hero.Locked ? 1 : int.MaxValue))
                {
                    var changed = new List<Renderer>();
                    try
                    {
                        var (floors, level, found) = ModuleFloors(world, module);
                        if (!found) { if (!missing.Contains(module)) missing.Add(module); continue; }
                        var view = EyeView(world.transform, heading + hero.Yaw + turn, hero.Pitch);
                        Stage(world, level, view, changed);
                        var props = PropCentre(world, floors, level, scenery);
                        var points = new List<(string name, Vector3 eye)> { ("back edge", EyePoint(floors, level, view.Forward, hero.Eye, hero.Back)) };
                        if (!hero.Locked && props is Vector3 centre)
                        {
                            points.Add(("near props", StandAt(floors, level, centre - view.Forward * NearProps, hero.Eye)));
                            points.Add(("before props", StandAt(floors, level, centre - view.Forward * FarProps, hero.Eye)));
                        }
                        foreach (var (name, eye) in points)
                        {
                            var candidate = new Candidate { Name = (module.Length == 0 ? "spawn level" : module) + " turn " + turn.ToString("F0") + " " + name,
                                Module = module, Eye = eye, View = view };
                            candidate.Score = Probe(preview, eye, view, hero.FieldOfView, EyeNear, EyeFar, pipeline, scenery);
                            candidates.Add(candidate);
                        }
                    }
                    finally { foreach (var renderer in changed) if (renderer) renderer.forceRenderingOff = false; }
                }
                if (candidates.Count == 0) throw new InvalidOperationException("No hero pose found a floor: " + string.Join(" / ", missing));
                var chosen = Choose(candidates, 0f);
                var shotChanged = new List<Renderer>();
                try
                {
                    var (_, chosenLevel, _) = ModuleFloors(world, chosen.Module);
                    int hidden = Stage(world, chosenLevel, chosen.View, shotChanged);
                    var (pixels, framing, guard) = ShootAt(preview, chosen.Eye, chosen.View, hero.FieldOfView, EyeNear, EyeFar, pipeline, scenery);
                    return (pixels, "subject " + hero.Subject + ", chose " + chosen.Name + " of " + candidates.Count + " (" + Scores(candidates) + ")" +
                        (missing.Count > 0 ? ", not found: " + string.Join("/", missing) : "") + ", level " + chosenLevel.ToString("F1") + " m, eye " + hero.Eye.ToString("F1") +
                        " m, " + hidden + " near walls hidden, " + skyline + ", " + framing, guard);
                }
                finally { foreach (var renderer in shotChanged) if (renderer) renderer.forceRenderingOff = false; }
            }
            finally
            {
                if (backdrop) Object.DestroyImmediate(backdrop);
                foreach (var mesh in owned) if (mesh) Object.DestroyImmediate(mesh);
            }
        }

        static IEnumerable<(string module, float turn)> Poses(MapHeroShot hero)
        {
            foreach (string module in hero.Modules.DefaultIfEmpty(""))
                foreach (float turn in new[] { 0f, CourseTurn, -CourseTurn })
                    yield return (module ?? "", turn);
        }

        static int Stage(WordboundCourseWorld world, float level, View view, List<Renderer> changed)
        {
            CutAbove(world.transform.root, level + CeilingRoom, changed);
            var standing = Floors(world).Where(floor => floor.max.y <= level + LevelStep).ToList();
            return HideNearWalls(world, standing, view.Forward, changed);
        }

        static Vector3? PropCentre(WordboundCourseWorld world, List<Bounds> floors, float level, HashSet<Renderer> scenery)
        {
            var sum = Vector3.zero; int count = 0;
            foreach (var renderer in world.transform.root.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer.forceRenderingOff || scenery != null && scenery.Contains(renderer)) continue;
                var box = renderer.bounds;
                if (box.size.y > PropHeight || box.size.x > PropSpan || box.size.z > PropSpan || box.min.y < level - LevelStep || box.min.y > level + PropHeight) continue;
                if (!floors.Any(floor => box.center.x >= floor.min.x && box.center.x <= floor.max.x && box.center.z >= floor.min.z && box.center.z <= floor.max.z)) continue;
                sum += box.center; count++;
            }
            return count == 0 ? (Vector3?)null : sum / count;
        }

        static Vector3 StandAt(List<Bounds> floors, float level, Vector3 wanted, float eye)
        {
            var best = wanted; float closest = float.MaxValue;
            foreach (var floor in floors)
            {
                float marginX = Mathf.Min(EdgeMargin, floor.extents.x), marginZ = Mathf.Min(EdgeMargin, floor.extents.z);
                var inside = new Vector3(Mathf.Clamp(wanted.x, floor.min.x + marginX, floor.max.x - marginX), 0f, Mathf.Clamp(wanted.z, floor.min.z + marginZ, floor.max.z - marginZ));
                float distance = (new Vector3(wanted.x, 0f, wanted.z) - inside).sqrMagnitude;
                if (distance < closest) { closest = distance; best = inside; }
            }
            return new Vector3(best.x, level + eye, best.z);
        }

        static View EyeView(Transform course, float heading, float pitch)
        {
            var direction = course.TransformDirection(Quaternion.Euler(0f, heading, 0f) * Vector3.forward);
            return new View(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, pitch);
        }

        static Vector3 EyePoint(List<Bounds> floors, float level, Vector3 forward, float eye, float back)
        {
            var union = floors[0];
            foreach (var floor in floors) union.Encapsulate(floor);
            float half = Mathf.Abs(forward.x) * union.extents.x + Mathf.Abs(forward.z) * union.extents.z;
            return StandAt(floors, level, union.center - forward * half * Mathf.Clamp01(back), eye);
        }

        static List<Bounds> Floors(WordboundCourseWorld world) => world.GetComponentsInChildren<WordboundCourseNode>(true)
            .Where(node => node.Kind == WordboundCourseNodeKind.Floor).Select(node => node.GetComponent<Renderer>()).Where(renderer => renderer)
            .Select(renderer => renderer.bounds).ToList();

        static (List<Bounds> floors, float level, bool found) ModuleFloors(WordboundCourseWorld world, string module)
        {
            var nodes = world.GetComponentsInChildren<WordboundCourseNode>(true);
            var all = Floors(world);
            if (all.Count == 0) throw new InvalidOperationException("The course has no floors to frame.");
            List<Bounds> floors;
            if (module.Length > 0)
            {
                floors = nodes.Where(node => node.Kind == WordboundCourseNodeKind.Floor && node.ModuleId == module)
                    .Select(node => node.GetComponent<Renderer>()).Where(renderer => renderer).Select(renderer => renderer.bounds).ToList();
                if (floors.Count == 0) return (floors, 0f, false);
                float top = floors.Max(bounds => bounds.max.y);
                return (floors.Where(bounds => bounds.max.y >= top - LevelStep).ToList(), top, true);
            }
            var spawn = nodes.FirstOrDefault(node => node.Kind == WordboundCourseNodeKind.Spawn);
            var levels = all.GroupBy(bounds => Mathf.RoundToInt(bounds.max.y / LevelStep)).ToList();
            var chosen = spawn ? levels.Where(group => group.Key * LevelStep <= spawn.transform.position.y + LevelStep).OrderByDescending(group => group.Key).FirstOrDefault() : null;
            chosen ??= levels.OrderByDescending(group => group.Sum(bounds => bounds.size.x * bounds.size.z)).First();
            floors = chosen.ToList();
            return (floors, floors.Max(bounds => bounds.max.y), true);
        }

        static string Anchor(Type settingsType, Object settings, string id)
        {
            if (id != WordboundSoloEntry.ParcelServiceMap) return "";
            var depot = settingsType.GetMethod("DepotPose", BindingFlags.Public | BindingFlags.Static);
            var field = settingsType.GetField("ParcelService", BindingFlags.Public | BindingFlags.Instance);
            if (depot == null || field == null) throw new MissingMemberException("WordboundSoloPreviewSettings changed; review MapSnapshotCapture.Anchor.");
            field.SetValue(settings, depot.Invoke(null, null));
            return ", anchored on the default Depot module";
        }

        static void CutAbove(Transform root, float height, List<Renderer> changed)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (!renderer.forceRenderingOff && renderer.bounds.min.y > height) { renderer.forceRenderingOff = true; changed.Add(renderer); }
        }

        static int HideNearWalls(WordboundCourseWorld world, List<Bounds> floors, Vector3 forward, List<Renderer> changed)
        {
            int hidden = 0;
            foreach (var node in world.GetComponentsInChildren<WordboundCourseNode>(true))
            {
                if (node.Kind != WordboundCourseNodeKind.Wall || !node.TryGetComponent<Renderer>(out var renderer) || renderer.forceRenderingOff) continue;
                var box = renderer.bounds;
                var nearSide = NearSide(box, box.size.x >= box.size.z, forward);
                if (floors.Any(floor => nearSide.x >= floor.min.x && nearSide.x <= floor.max.x && nearSide.z >= floor.min.z && nearSide.z <= floor.max.z)) continue;
                renderer.forceRenderingOff = true;
                changed.Add(renderer);
                hidden++;
            }
            return hidden;
        }

        static Color32[] Render(PreviewRenderUtility preview, Color clear)
        {
            Texture2D image = null; bool begun = false; var active = RenderTexture.active;
            try
            {
                preview.camera.backgroundColor = clear;
                preview.BeginStaticPreview(new Rect(0, 0, Width * Supersample, Height * Supersample)); begun = true;
                SetSsao(false); preview.Render(true, false); preview.Render(true, false);
                image = preview.EndStaticPreview(); begun = false;
                if (!image || image.width < Width || image.height < Height || Mathf.Abs(image.width / (float)image.height - Width / (float)Height) > .02f)
                    throw new InvalidOperationException("The native capture has an unexpected size: " + (image ? image.width + "x" + image.height : "none"));
                return Downsample(image.GetPixels32(), image.width, image.height);
            }
            finally
            {
                if (begun) { var pending = preview.EndStaticPreview(); if (pending) Object.DestroyImmediate(pending); }
                if (image) Object.DestroyImmediate(image);
                RenderTexture.active = active;
            }
        }

        static Color32[] Downsample(Color32[] source, int width, int height)
        {
            var result = new Color32[Width * Height];
            float stepX = width / (float)Width, stepY = height / (float)Height;
            for (int y = 0; y < Height; y++)
            {
                int y0 = Mathf.FloorToInt(y * stepY), y1 = Mathf.Max(y0 + 1, Mathf.Min(height, Mathf.FloorToInt((y + 1) * stepY)));
                for (int x = 0; x < Width; x++)
                {
                    int x0 = Mathf.FloorToInt(x * stepX), x1 = Mathf.Max(x0 + 1, Mathf.Min(width, Mathf.FloorToInt((x + 1) * stepX)));
                    int r = 0, g = 0, b = 0, count = 0;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++)
                        {
                            var c = source[sy * width + sx];
                            r += c.r; g += c.g; b += c.b; count++;
                        }
                    result[y * Width + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
                }
            }
            return result;
        }

        static bool Compose(Color32[] onWhite, Color32[] onBlack, out Color32[] result, out float share, out float gain)
        {
            int count = Width * Height, solid = 0;
            result = null; gain = 1f;
            var cover = new float[count]; var colour = new Vector3[count]; var histogram = new int[256];
            for (int i = 0; i < count; i++)
            {
                var white = onWhite[i]; var black = onBlack[i];
                float covered = 1f - Mathf.Clamp01((white.r - black.r + white.g - black.g + white.b - black.b) / (3f * 255f));
                cover[i] = covered;
                if (covered <= MinCover) continue;
                var seen = Vector3.Min(new Vector3(black.r, black.g, black.b) / (255f * covered), Vector3.one);
                colour[i] = seen;
                if (covered < SolidCover) continue;
                histogram[Mathf.Clamp(Mathf.RoundToInt(Luma(seen) * 255f), 0, 255)]++;
                solid++;
            }
            share = solid / (float)count;
            if (share < EmptyShare) return false;
            gain = Gain(histogram, solid);
            result = new Color32[count];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    var mixed = Vector3.Lerp(Sky(y), Grade(colour[i] * gain), cover[i]);
                    result[i] = new Color(Mathf.Clamp01(mixed.x), Mathf.Clamp01(mixed.y), Mathf.Clamp01(mixed.z), 1f);
                }
            return true;
        }

        static Vector3 Sky(int y)
        {
            float t = (y + .5f) / Height;
            var c = t < .5f ? Color.Lerp(SkyLow, SkyMid, t * 2f) : Color.Lerp(SkyMid, SkyHigh, (t - .5f) * 2f);
            return new Vector3(c.r, c.g, c.b);
        }

        static float Luma(Vector3 v) => v.x * .2126f + v.y * .7152f + v.z * .0722f;

        static float Gain(int[] histogram, int total)
        {
            int wanted = Mathf.CeilToInt(total * WhitePoint), seen = 0, level = histogram.Length - 1;
            for (int i = 0; i < histogram.Length; i++)
            {
                seen += histogram[i];
                if (seen < wanted) continue;
                level = i;
                break;
            }
            return Mathf.Clamp(TargetWhite * (histogram.Length - 1) / Mathf.Max(1, level), MinGain, MaxGain);
        }

        static Vector3 Grade(Vector3 v)
        {
            float luma = Luma(v);
            v = Vector3.LerpUnclamped(new Vector3(luma, luma, luma), v, Saturation);
            v = (v - Vector3.one * .5f) * Contrast + Vector3.one * .5f;
            return new Vector3(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y), Mathf.Clamp01(v.z));
        }

        static void RequireVaried(Color32[] pixels)
        {
            var distinct = new HashSet<int>(); int lit = 0;
            foreach (var c in pixels)
            {
                if (c.r + c.g + c.b > 24) lit++;
                if (distinct.Count < 4096) distinct.Add(c.r << 16 | c.g << 8 | c.b);
            }
            if (lit < pixels.Length / 4 || distinct.Count < 256) throw new InvalidOperationException("The render is blank or nearly uniform (" + distinct.Count + " colours).");
        }

        static byte[] Encode(Color32[] pixels)
        {
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(Width, Height, TextureFormat.RGB24, false, false) { hideFlags = HideFlags.HideAndDontSave };
                texture.SetPixels32(pixels); texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally { if (texture) Object.DestroyImmediate(texture); }
        }

        static string WriteSheet(List<Shot> shots)
        {
            if (shots.Count == 0) return "none";
            try
            {
                int rows = (shots.Count + SheetColumns - 1) / SheetColumns, smallRows = (shots.Count + SmallColumns - 1) / SmallColumns;
                int width = Mathf.Max(SheetColumns * CellWidth + (SheetColumns + 1) * SheetGap, SmallColumns * SmallWidth + (SmallColumns + 1) * SheetGap);
                int height = SheetGap + rows * (LabelRoom + CellHeight + SheetGap) + 2 * smallRows * (LabelRoom + SmallHeight + SheetGap);
                var sheet = new CaptureSheet(width, height, PaperCentre);
                Color32 ink = LobbyParcel.Ink;
                int top = SheetGap;
                for (int i = 0; i < shots.Count; i++)
                {
                    int left = SheetGap + i % SheetColumns * (CellWidth + SheetGap), at = top + i / SheetColumns * (LabelRoom + CellHeight + SheetGap);
                    sheet.Label(shots[i].id + "  " + shots[i].guard, left, at + 2, LabelScale, ink);
                    sheet.Paste(CaptureSheet.Resize(shots[i].pixels, Width, Height, CellWidth, CellHeight), CellWidth, CellHeight, left, at + LabelRoom);
                }
                top += rows * (LabelRoom + CellHeight + SheetGap);
                Strip(sheet, shots, top, "NEW AT CARD SIZE", ink, shot => shot.pixels == null ? null : CaptureSheet.Resize(shot.pixels, Width, Height, SmallWidth, SmallHeight));
                top += smallRows * (LabelRoom + SmallHeight + SheetGap);
                Strip(sheet, shots, top, "BEFORE", ink, Before);
                string file = Full(SheetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                sheet.Save(file);
                return SheetPath;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Wreckabulary] Map snapshots: the contact sheet was not written. " + error);
                return "not written";
            }
        }

        static void Strip(CaptureSheet sheet, List<Shot> shots, int top, string caption, Color32 ink, Func<Shot, Color32[]> picture)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                int left = SheetGap + i % SmallColumns * (SmallWidth + SheetGap), at = top + i / SmallColumns * (LabelRoom + SmallHeight + SheetGap);
                if (i % SmallColumns == 0) sheet.Label(caption, left, at + 2, LabelScale, ink);
                var small = picture(shots[i]);
                if (small != null) sheet.Paste(small, SmallWidth, SmallHeight, left, at + LabelRoom);
            }
        }

        static Color32[] Before(Shot shot)
        {
            if (shot.previous == null) return null;
            var old = CaptureSheet.Decode(shot.previous, out int width, out int height);
            return CaptureSheet.Resize(old, width, height, SmallWidth, SmallHeight);
        }

        static void Import(List<Shot> shots)
        {
            foreach (var shot in shots)
            {
                AssetDatabase.ImportAsset(shot.path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(shot.path) as TextureImporter ?? throw new InvalidOperationException("No texture importer for " + shot.path);
                if (MapSnapshotImporter.Configure(importer)) importer.SaveAndReimport();
                if (!AssetDatabase.LoadAssetAtPath<Sprite>(shot.path)) throw new InvalidOperationException("The snapshot did not import as a Sprite: " + shot.path);
            }
        }

        static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            int split = Folder.LastIndexOf('/');
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Folder.Substring(0, split), Folder.Substring(split + 1))))
                throw new IOException("Could not create " + Folder);
        }

        static IEnumerable<Vector3> Corners(Bounds bounds)
        { for (int i = 0; i < 8; i++) yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)); }
        static string Global() => string.Join("|", new object[] {
            RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
            RenderSettings.ambientIntensity, RenderSettings.reflectionIntensity, RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogMode,
            RenderSettings.skybox ? RenderSettings.skybox.GetEntityId().ToString() : "", RenderSettings.sun ? RenderSettings.sun.GetEntityId().ToString() : "",
            AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline), GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp ? urp.shadowDistance : -1f,
            RenderTexture.active ? RenderTexture.active.GetEntityId().ToString() : "", GL.sRGBWrite, EditorSettings.asyncShaderCompilation,
            Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION"), QualitySettings.GetQualityLevel(), QualitySettings.activeColorSpace,
            string.Join(",", Selection.objects.Select(o => o ? o.GetEntityId().ToString() : "")) });
        static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isLoaded + ":" + s.isDirty + ":" + (s == SceneManager.GetActiveScene()) + ":" +
                (s.isLoaded ? string.Join(",", s.GetRootGameObjects().Select(o => o.GetEntityId().ToString()).OrderBy(id => id, StringComparer.Ordinal)) : "unloaded"); }));
        static void SetSsao(bool enabled) { if (enabled) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); }
        static void Clean(Action action, List<Exception> errors) { try { action(); } catch (Exception error) { errors.Add(error); } }
        static void Log(string line) => Debug.Log("[Wreckabulary] Map snapshots " + line);
        static string Full(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }

    public sealed class MapSnapshotImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(MapSnapshotCapture.Folder + "/", StringComparison.Ordinal)) Configure((TextureImporter)assetImporter);
        }

        public static bool Configure(TextureImporter importer)
        {
            string before = Describe(importer);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            return before != Describe(importer);
        }

        static string Describe(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return string.Join("|", new object[] { importer.textureType, importer.spriteImportMode, settings.spriteMeshType, settings.spriteGenerateFallbackPhysicsShape,
                importer.sRGBTexture, importer.alphaSource, importer.alphaIsTransparency, importer.mipmapEnabled, importer.isReadable, importer.wrapMode,
                importer.filterMode, importer.maxTextureSize, importer.textureCompression });
        }
    }
}
