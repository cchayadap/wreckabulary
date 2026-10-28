using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Workshop
{
    public sealed class WordboundWorkshopViewport : IDisposable
    {
        const int PreviewLayer = 31;
        readonly GameObject stage;
        readonly Camera lens;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly Dictionary<string, Material> styledFloors = new Dictionary<string, Material>(StringComparer.Ordinal);
        readonly List<Light> previewLights = new List<Light>();
        readonly Dictionary<string, Transform> modules = new Dictionary<string, Transform>(StringComparer.Ordinal);
        readonly Dictionary<string, CourseModule> authored = new Dictionary<string, CourseModule>(StringComparer.Ordinal);
        readonly Dictionary<string, Transform> fixedVisuals = new Dictionary<string, Transform>(StringComparer.Ordinal);
        readonly Dictionary<string, CourseZone> fixedZones = new Dictionary<string, CourseZone>(StringComparer.Ordinal);
        readonly Material floor, selected, ghost, path, prop;
        Transform content;
        RenderTexture texture;
        WorkshopSnapshot snapshot;
        CourseCatalogue catalogue;
        Vector3 target;
        float yaw = 35, pitch = 48, distance = 70;
        int width = 800, height = 600;
        public Texture Texture => texture;
        public Vector3 Target => target;
        public float Distance => distance;
        public Func<string, Transform, GameObject> SpawnAssetVisual { get; set; }
        public Func<WordboundRoomStyleProfile> RoomStyleProvider { get; set; }
        public Func<IReadOnlyList<WordboundModuleDressingEntry>> ModuleDressingProvider { get; set; }
        public bool Active { set { if (stage) stage.SetActive(value); } }

        public WordboundWorkshopViewport(Transform owner)
        {
            stage = new GameObject("Wordbound workshop preview") { hideFlags = HideFlags.DontSave, layer = PreviewLayer };
            stage.transform.SetParent(owner, false);
            stage.transform.position = new Vector3(0, -20000, 0);
            var camera = new GameObject("Workshop camera") { hideFlags = HideFlags.DontSave, layer = PreviewLayer };
            camera.transform.SetParent(stage.transform, false);
            lens = camera.AddComponent<Camera>();
            lens.cullingMask = 1 << PreviewLayer; lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = new Color(.12f, .19f, .21f); lens.nearClipPlane = .1f; lens.farClipPlane = 20000;
            lens.fieldOfView = 45; lens.allowHDR = false; lens.allowMSAA = false; lens.depth = -50;
            floor = Material(new Color(.79f, .72f, .58f), Surfaces.Kind.Planks);
            selected = Material(new Color(.42f, .82f, .72f), Surfaces.Kind.Planks);
            ghost = Material(new Color(.59f, .69f, .73f, .20f), Surfaces.Kind.Plaster);
            path = Material(new Color(.9f, .78f, .49f), Surfaces.Kind.Wood);
            prop = Material(new Color(.7f, .85f, .8f), Surfaces.Kind.Plaster);
            PreviewLight("Workshop key", new Vector3(45, -35, 0), 1.25f);
            PreviewLight("Workshop fill", new Vector3(35, 145, 0), .55f);
            Resize(width, height);
            RenderPipelineManager.beginCameraRendering += BeginPreviewCamera;
            RenderPipelineManager.endCameraRendering += EndPreviewCamera;
        }

        void PreviewLight(string name, Vector3 rotation, float intensity)
        {
            var root = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = PreviewLayer };
            root.transform.SetParent(stage.transform, false); root.transform.localRotation = Quaternion.Euler(rotation);
            var light = root.AddComponent<Light>();
            light.type = LightType.Directional; light.color = Color.white; light.intensity = intensity;
            light.cullingMask = 1 << PreviewLayer; light.shadows = LightShadows.None;
            light.enabled = false; previewLights.Add(light);
        }

        void BeginPreviewCamera(ScriptableRenderContext context, Camera camera)
        {
            foreach (var light in previewLights) if (light) light.enabled = camera == lens;
        }
        void EndPreviewCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != lens) return;
            foreach (var light in previewLights) if (light) light.enabled = false;
        }

        Material Material(Color color, Surfaces.Kind surface)
        {
            var source = Surfaces.Get(surface, Color.white);
            var result = color.a < 1
                ? WordboundRenderMaterials.Create(true, "Workshop " + surface)
                : new Material(source) { name = "Workshop " + surface, hideFlags = HideFlags.DontSave };
            result.mainTexture = source.mainTexture;
            result.color = color;
            if (result.HasProperty("_BaseMap")) result.SetTexture("_BaseMap", source.mainTexture);
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            if (color.a < 1)
            {
                result.SetFloat("_Surface", 1); result.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                result.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); result.SetFloat("_ZWrite", 0);
                result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); result.renderQueue = (int)RenderQueue.Transparent;
            }
            ownedMaterials.Add(result); return result;
        }

        public void Resize(int desiredWidth, int desiredHeight)
        {
            desiredWidth = Mathf.Clamp(desiredWidth, 64, 1920); desiredHeight = Mathf.Clamp(desiredHeight, 64, 1080);
            if (texture && width == desiredWidth && height == desiredHeight) return;
            width = desiredWidth; height = desiredHeight;
            if (texture) { lens.targetTexture = null; texture.Release(); Destroy(texture); }
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = "Wordbound workshop view", hideFlags = HideFlags.DontSave };
            texture.Create(); lens.targetTexture = texture; lens.aspect = width / (float)height;
            UpdateCamera();
        }

        public void Sync(WorkshopSnapshot state, CourseCatalogue approved, ResolvedCourse resolved = null)
        {
            var style = RoomStyleProvider?.Invoke();
            if (style) style.Validate();
            var dressing = ModuleDressingProvider?.Invoke();
            WordboundModuleDressing.Validate(dressing);
            foreach (var material in styledFloors.Values) { ownedMaterials.Remove(material); Destroy(material); }
            styledFloors.Clear();
            bool changedFloor = snapshot != null && snapshot.ActiveLayer != state.ActiveLayer;
            snapshot = state; catalogue = approved;
            if (content) { content.gameObject.SetActive(false); Destroy(content.gameObject); }
            content = new GameObject("Draft geometry").transform; content.SetParent(stage.transform, false);
            modules.Clear(); authored.Clear(); fixedVisuals.Clear(); fixedZones.Clear();
            var document = state.Course();
            foreach (var zone in document.Zones.Where(zone => zone.Placement == "Fixed")) fixedZones[zone.Id] = zone;
            var layers = document.Layers.OrderBy(layer => layer.HeightMm).Select(layer => layer.Id).ToList();
            foreach (var module in document.Modules)
            {
                if (!approved.Modules.TryGetValue(module.DefinitionId, out var definition) || state.HiddenLayers.Contains(module.LayerId)) continue;
                bool adjacent = module.LayerId != state.ActiveLayer;
                if (adjacent && Math.Abs(layers.IndexOf(module.LayerId) - layers.IndexOf(state.ActiveLayer)) > 1) continue;
                var root = new GameObject(module.Id).transform; root.SetParent(content, false);
                root.localPosition = Point(module.Position); root.localRotation = Quaternion.Euler(0, module.Yaw, 0);
                modules.Add(module.Id, root); authored.Add(module.Id, module);
                var material = adjacent ? ghost : state.Selection.Contains(module.Id) ? selected : FloorFor(module.LayerId, style);
                float x = definition.WidthMm / 1000f, z = definition.DepthMm / 1000f;
                Box(root, "Floor", new Vector3(0, -.1f, 0), new Vector3(x, .2f, z), material);
                Box(root, "Edge north", new Vector3(0, .12f, z * .5f), new Vector3(x, .24f, .12f), material);
                Box(root, "Edge south", new Vector3(0, .12f, -z * .5f), new Vector3(x, .24f, .12f), material);
                Box(root, "Edge east", new Vector3(x * .5f, .12f, 0), new Vector3(.12f, .24f, z), material);
                Box(root, "Edge west", new Vector3(-x * .5f, .12f, 0), new Vector3(.12f, .24f, z), material);
            }
            foreach (var connection in document.Connections)
            {
                if (!authored.TryGetValue(connection.FromModuleId, out var from) || !authored.TryGetValue(connection.ToModuleId, out var to)) continue;
                Vector3 a = Point(connection.From), b = Point(connection.To), delta = b - a;
                if (delta.magnitude < .2f) continue;
                var bridge = Box(content, connection.Id, (a + b) * .5f - Vector3.up * .1f,
                    new Vector3(connection.WidthMm / 1000f, .2f, delta.magnitude), from.LayerId == state.ActiveLayer || to.LayerId == state.ActiveLayer ? path : ghost);
                bridge.localRotation = Quaternion.LookRotation(delta.normalized);
            }
            if (resolved != null)
            {
                foreach (var placement in resolved.Placements.Where(item => item.TeamIndex <= 0))
                    Asset(placement.AssetId, placement.ModuleId, placement.Position, placement.Yaw, placement.Id, placement.ZoneId);
            }
            else
                foreach (var zone in document.Zones.Where(item => item.Placement == "Fixed" && item.Pool.Count == 1))
                    Asset(zone.Pool[0].AssetId, zone.ModuleId, zone.FixedPosition, zone.FixedYaw, zone.Id);
            if (changedFloor) Frame(); else UpdateCamera();
        }

        Material FloorFor(string layerId, WordboundRoomStyleProfile style)
        {
            if (!style || !style.Enabled) return floor;
            var palette = style.ForLayer(layerId);
            if (palette.FloorMaterial) return palette.FloorMaterial;
            if (!styledFloors.TryGetValue(layerId, out var material))
            {
                material = Material(palette.Floor, Surfaces.Kind.Planks);
                styledFloors.Add(layerId, material);
            }
            return material;
        }

        void Asset(string assetId, string moduleId, CoursePoint position, int angle, string name, string sourceZone = null)
        {
            if (!modules.TryGetValue(moduleId, out var parent) || !catalogue.Assets.TryGetValue(assetId, out var definition)) return;
            if (authored[moduleId].LayerId != snapshot.ActiveLayer) return;
            string fixedId = fixedZones.ContainsKey(sourceZone ?? name) ? sourceZone ?? name : null;
            var anchor = new GameObject(fixedId ?? name).transform; anchor.SetParent(parent, false);
            anchor.position = stage.transform.TransformPoint(Point(position));
            anchor.rotation = stage.transform.rotation * Quaternion.Euler(0, angle, 0);
            var fit = new GameObject("Artwork fit").transform; fit.SetParent(anchor, false);
            var visual = SpawnAssetVisual?.Invoke(assetId, fit);
            if (visual && visual.transform != fit && !visual.transform.IsChildOf(fit))
            {
                Destroy(anchor.gameObject);
                throw new InvalidOperationException("A workshop visual factory must create its instance below the supplied parent.");
            }
            var size = new Vector3(definition.WidthMm, definition.HeightMm, definition.DepthMm) / 1000f;
            if (!visual || !FitArtwork(fit, visual, size))
            {
                fit.gameObject.SetActive(false); Destroy(fit.gameObject);
                Box(anchor, assetId, Vector3.up * size.y * .5f, size, prop);
            }
            foreach (var child in anchor.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
            foreach (var collider in anchor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var collider in anchor.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var body in anchor.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; body.detectCollisions = false; }
            foreach (var body in anchor.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            if (fixedId != null) fixedVisuals[fixedId] = anchor;
        }

        static bool FitArtwork(Transform fit, GameObject visual, Vector3 size)
        {
            var bounds = ModelVisual.BoundsIn(fit, visual);
            if (!Positive(bounds.size.x) || !Positive(bounds.size.y) || !Positive(bounds.size.z) ||
                !Finite(bounds.center.x) || !Finite(bounds.center.y) || !Finite(bounds.center.z)) return false;
            var scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            fit.localScale = scale;
            fit.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
            return true;
        }
        static bool Positive(float value) => value > .000001f && !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var root = new GameObject(name) { layer = PreviewLayer, hideFlags = HideFlags.DontSave };
            root.transform.SetParent(parent, false); root.transform.localPosition = position; root.transform.localScale = size;
            root.AddComponent<MeshFilter>().sharedMesh = Surfaces.Box(size, Surfaces.Tile(Surfaces.Kind.Planks));
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            bool transparent = material.HasProperty("_Surface") && material.GetFloat("_Surface") > .5f;
            renderer.shadowCastingMode = transparent ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = !transparent;
            return root.transform;
        }

        public void Frame(IEnumerable<CourseModule> selection = null)
        {
            var all = (selection ?? authored.Values.Where(module => module.LayerId == snapshot.ActiveLayer))
                .Where(module => authored.ContainsKey(module.Id)).ToArray();
            if (all.Length == 0 && selection == null) all = authored.Values.ToArray();
            if (all.Length == 0) { target = Vector3.zero; distance = 70; UpdateCamera(); return; }
            Bounds bounds = Bounds(catalogue.Bounds(all[0]));
            foreach (var module in all.Skip(1)) bounds.Encapsulate(Bounds(catalogue.Bounds(module)));
            target = bounds.center;
            float fit = 0, slope = Mathf.Tan(lens.fieldOfView * Mathf.Deg2Rad * .5f);
            var inverse = Quaternion.Inverse(lens.transform.localRotation);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = inverse * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        fit = Mathf.Max(fit, Mathf.Max(Mathf.Abs(corner.x) / (slope * lens.aspect), Mathf.Abs(corner.y) / slope) - corner.z);
                    }
            distance = Mathf.Clamp(fit * 1.15f, 5, 9000);
            UpdateCamera();
        }
        public void Focus(CoursePoint point) { target = Point(point); UpdateCamera(); }
        public void Orbit(Vector2 delta) { yaw += delta.x * .35f; pitch = Mathf.Clamp(pitch + delta.y * .35f, 12, 88); UpdateCamera(); }
        public void Pan(Vector2 delta)
        {
            var right = lens.transform.right; right.y = 0; right.Normalize();
            var forward = Vector3.Cross(right, Vector3.up);
            target += (-right * delta.x + forward * delta.y) * distance / Mathf.Max(200, height);
            target.x = Mathf.Clamp(target.x, -CourseLimits.HorizontalMm / 1000f, CourseLimits.HorizontalMm / 1000f);
            target.z = Mathf.Clamp(target.z, -CourseLimits.HorizontalMm / 1000f, CourseLimits.HorizontalMm / 1000f);
            UpdateCamera();
        }
        public void Zoom(float delta) { distance = Mathf.Clamp(distance * Mathf.Exp(delta * .08f), 3, 9000); UpdateCamera(); }
        public void Top() { pitch = 88; yaw = 0; UpdateCamera(); }
        void UpdateCamera()
        {
            if (!lens) return;
            lens.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
            lens.transform.localPosition = target - lens.transform.localRotation * Vector3.forward * distance;
        }
        public Vector2 Project(CoursePoint point, Rect viewport)
        {
            return TryProject(point, viewport, out var screen) ? screen : new Vector2(-100000, -100000);
        }
        Vector3 CameraPoint(CoursePoint point) => Quaternion.Inverse(lens.transform.localRotation) * (Point(point) - lens.transform.localPosition);
        Vector2 ProjectCamera(Vector3 cameraPoint, Rect viewport)
        {
            float halfHeight = Mathf.Tan(lens.fieldOfView * Mathf.Deg2Rad * .5f);
            return new Vector2((cameraPoint.x / (cameraPoint.z * halfHeight * lens.aspect) + 1) * viewport.width * .5f,
                (1 - cameraPoint.y / (cameraPoint.z * halfHeight)) * viewport.height * .5f);
        }
        public bool TryProject(CoursePoint point, Rect viewport, out Vector2 screen)
        {
            var cameraPoint = CameraPoint(point);
            screen = default;
            if (cameraPoint.z < lens.nearClipPlane) return false;
            screen = ProjectCamera(cameraPoint, viewport);
            return WordboundWorkshopOverlay.Finite(screen);
        }
        public bool ProjectSegment(CoursePoint from, CoursePoint to, Rect viewport, out Vector2 a, out Vector2 b)
        {
            a = b = default;
            var first = CameraPoint(from); var last = CameraPoint(to);
            float near = lens.nearClipPlane;
            if (first.z < near && last.z < near) return false;
            if (first.z < near) first = Vector3.LerpUnclamped(first, last, (near - first.z) / (last.z - first.z));
            else if (last.z < near) last = Vector3.LerpUnclamped(last, first, (near - last.z) / (first.z - last.z));
            a = ProjectCamera(first, viewport); b = ProjectCamera(last, viewport);
            return WordboundWorkshopOverlay.ClipLine(new Rect(0, 0, viewport.width, viewport.height), ref a, ref b);
        }
        Ray Ray(Vector2 local, Rect viewport)
        {
            float halfHeight = Mathf.Tan(lens.fieldOfView * Mathf.Deg2Rad * .5f);
            var direction = new Vector3((local.x / viewport.width * 2 - 1) * halfHeight * lens.aspect,
                (1 - local.y / viewport.height * 2) * halfHeight, 1);
            return new Ray(lens.transform.localPosition, lens.transform.localRotation * direction.normalized);
        }
        public bool Surface(Vector2 local, Rect viewport, int floorMm, out CoursePoint point)
        {
            var ray = Ray(local, viewport);
            var plane = new Plane(Vector3.up, new Vector3(0, floorMm / 1000f, 0));
            if (!plane.Raycast(ray, out float enter) || enter > 20000) { point = default; return false; }
            var hit = ray.GetPoint(enter);
            point = new CoursePoint(checked((int)Math.Round(hit.x * 1000)), floorMm, checked((int)Math.Round(hit.z * 1000)));
            return Math.Abs((long)point.X) <= CourseLimits.HorizontalMm && Math.Abs((long)point.Z) <= CourseLimits.HorizontalMm;
        }
        public string Pick(Vector2 local, Rect viewport)
        {
            string nearest = null; float best = float.MaxValue; var ray = Ray(local, viewport);
            foreach (var module in authored.Values.Where(module => module.LayerId == snapshot.ActiveLayer && !snapshot.LockedLayers.Contains(module.LayerId)))
            {
                var bounds = Bounds(catalogue.Bounds(module));
                bounds.center = new Vector3(bounds.center.x, Point(module.Position).y - .1f, bounds.center.z);
                bounds.size = new Vector3(bounds.size.x, .25f, bounds.size.z);
                if (bounds.IntersectRay(ray, out float hit) && hit < best) { best = hit; nearest = module.Id; }
            }
            return nearest;
        }
        public string PickFixed(Vector2 local, Rect viewport)
        {
            var ray = Ray(local, viewport); float best = float.MaxValue; string nearest = null;
            foreach (var zone in fixedZones.Values)
            {
                if (!authored.TryGetValue(zone.ModuleId, out var module) || module.LayerId != snapshot.ActiveLayer ||
                    snapshot.LockedLayers.Contains(module.LayerId) || zone.Pool.Count != 1 ||
                    !catalogue.Assets.TryGetValue(zone.Pool[0].AssetId, out var asset)) continue;
                var size = new Vector3(asset.WidthMm, asset.HeightMm, asset.DepthMm) / 1000f;
                if (zone.FixedYaw % 180 != 0) size = new Vector3(size.z, size.y, size.x);
                var bounds = new Bounds(Point(zone.FixedPosition) + Vector3.up * size.y * .5f, size);
                if (bounds.IntersectRay(ray, out float hit) && hit < best) { nearest = zone.Id; best = hit; }
            }
            return nearest;
        }
        public void PlacementPreview(WorkshopPlacementDrag drag)
        {
            foreach (var pair in fixedVisuals)
            {
                var zone = fixedZones[pair.Key];
                bool active = drag != null && pair.Key == drag.ZoneId;
                pair.Value.position = stage.transform.TransformPoint(Point(active ? drag.Position : zone.FixedPosition));
                pair.Value.rotation = stage.transform.rotation * Quaternion.Euler(0, active ? drag.Yaw : zone.FixedYaw, 0);
            }
        }
        public void TransformPreview(WorkshopTransformDrag drag, CoursePoint pivot)
        {
            foreach (var pair in modules)
            {
                var module = authored[pair.Key]; var root = pair.Value;
                root.localPosition = Point(module.Position); root.localRotation = Quaternion.Euler(0, module.Yaw, 0);
                if (drag == null || !drag.Selection.Contains(pair.Key)) continue;
                var rotation = Quaternion.Euler(0, drag.QuarterTurns * 90, 0);
                root.localPosition = Point(pivot) + rotation * (root.localPosition - Point(pivot)) + Point(drag.Delta);
                root.localRotation = rotation * root.localRotation;
            }
        }
        public static Vector3 Point(CoursePoint point) => new Vector3(point.X, point.Y, point.Z) / 1000f;
        static Bounds Bounds(CourseBox box) => new Bounds((Point(box.Min) + Point(box.Max)) * .5f, Point(box.Max) - Point(box.Min));
        static void Destroy(UnityEngine.Object target)
        {
            if (!target) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target); else UnityEngine.Object.DestroyImmediate(target);
        }
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= BeginPreviewCamera;
            RenderPipelineManager.endCameraRendering -= EndPreviewCamera;
            foreach (var light in previewLights) if (light) light.enabled = false;
            previewLights.Clear();
            if (lens) lens.targetTexture = null;
            if (texture) { texture.Release(); Destroy(texture); texture = null; }
            Destroy(stage);
            foreach (var material in ownedMaterials) Destroy(material);
            ownedMaterials.Clear();
            styledFloors.Clear();
        }
    }
}
