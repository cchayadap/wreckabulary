using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundOriginalArtworkCapture
    {
        const int Width = 1280, Height = 720, PoseSteps = 30;
        const float Tolerance = .002f;
        const string CoursePath = "Assets/_Project/Data/Wordbound/Authoring/FlagshipCourse.asset";
        const string ProfilePath = "Assets/_Project/Data/Wordbound/Authoring/Presentation.asset";
        const string ScriptPath = "Assets/_Project/Scripts/Online/Authoring/Editor/WordboundOriginalArtworkCapture.cs";
        static readonly string[] Words = { "SOFA", "FAN", "HAMMER", "BAT" };
        static readonly Vector3[] Positions = {
            new Vector3(-2.9f, 0, 0), new Vector3(1, 0, 0),
            new Vector3(1.8f, 0, .6f), new Vector3(2.35f, 0, .6f)
        };

        [MenuItem("Tools/Wordbound/Capture original artwork baseline")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Capture outside Play mode.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A graphics device is required; omit -nographics.");
            Require(GraphicsSettings.currentRenderPipeline, "Use the project's active render pipeline.");
            var course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(CoursePath);
            var profile = AssetDatabase.LoadAssetAtPath<WordboundVisualProfile>(ProfilePath);
            Require(course && profile && course.VisualProfile == profile, "The flagship must use the actual assigned Presentation profile.");
            profile.Validate();
            Require(profile.BaseArtwork == WordboundBaseArtwork.OriginalWreckabulary &&
                !profile.Overrides.Any(o => Words.Contains(o.VisualKey)), "The four items must use original artwork without overrides.");
            var items = course.CreateContent(course.Resolve()).Items;
            var resolver = new WordboundVisualResolver(items, profile: profile);
            Require(!resolver.UseWordboundCollection, "The original artwork resolver must be active.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = FreshOutput(project);
            var visuals = new List<WordboundVisualInstance>();
            PreviewRenderUtility preview = null;
            WordboundAvatarMotion motion = null;
            GameObject stage = null;
            Material groundMaterial = null;
            bool asynchronousShaders = EditorSettings.asyncShaderCompilation;
            var previousActive = RenderTexture.active;
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                preview = new PreviewRenderUtility();
                stage = new GameObject("Original artwork capture") { hideFlags = HideFlags.HideAndDontSave };
                preview.AddSingleGO(stage);
                groundMaterial = Ground(stage.transform);
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, 25, 0);
                preview.lights[1].intensity = .7f;
                preview.ambientColor = Color.gray;
                var camera = preview.camera;
                camera.fieldOfView = 40;
                camera.aspect = (float)Width / Height;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.16f, .17f, .18f, 1);
                var facts = new List<object>();
                for (int i = 0; i < Words.Length; i++)
                {
                    var item = items.Get(Words[i]);
                    var size = Size(item);
                    var visual = resolver.Create(item.Id, size, Anchor(stage.transform, item.Id, Positions[i]));
                    visuals.Add(visual);
                    AssertOriginal(visual, item.Model);
                    var bounds = ModelVisual.BoundsIn(stage.transform, visual.Model);
                    Require(Vector3.Distance(bounds.size, size) <= Tolerance && Math.Abs(bounds.min.y) <= Tolerance,
                        item.Id + " must preserve canonical dimensions and grounding.");
                    facts.Add(VisualFacts(item.Id, item.Model, visual, size, Skin.Standard, stage.transform));
                }
                var avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f),
                    Anchor(stage.transform, "Avatar", new Vector3(-.1f, 0, .7f)));
                visuals.Add(avatar);
                AssertOriginal(avatar, WordboundVisualResolver.AvatarKey);
                avatar.Dress("");
                var skinned = avatar.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var skin in skinned) skin.forceMatrixRecalculationPerRender = true;
                motion = new WordboundAvatarMotion(avatar);
                for (int i = 0; i < PoseSteps; i++) motion.Step(1f / 60, true, false, false);
                facts.Add(VisualFacts("player", WordboundVisualResolver.AvatarKey, avatar,
                    new Vector3(.6f, 1.8f, .6f), avatar.Outfit.Serialize(), stage.transform));
                Pose(camera, new Vector3(2.8f, 2, 4.8f), new Vector3(.05f, .75f, .2f));
                foreach (var visual in visuals) RequireFramed(camera, VisibleBounds(visual), visual.Root.name);
                var idleBefore = MittenFacts(avatar, "idle-before-world-render");
                var worldImage = Capture(preview, output, "original-four-world.png");
                var idleAfter = MittenFacts(avatar, "idle-after-world-render");
                foreach (var visual in visuals.Take(Words.Length)) visual.Show(false);
                var bat = items.Get("BAT");
                string heldSkin = resolver.SkinFor("BAT", avatar.Outfit);
                var held = resolver.Create("BAT", Size(bat), stage.transform, heldSkin);
                visuals.Add(held);
                AssertOriginal(held, bat.Model);
                for (int i = 0; i < PoseSteps; i++)
                {
                    motion.Step(1f / 60, true, true, false);
                    held.PlaceAtHand(stage.transform, avatar.RightHand, true);
                }
                var grip = held.Root.transform.TransformPoint(held.Grip);
                float gap = Vector3.Distance(grip, avatar.RightHand.position);
                Require(gap <= Tolerance && Vector3.Distance(held.Root.transform.localScale,
                    Vector3.one * bat.HeldScale) < .000001f, "The held BAT must use its actual miniature scale and right-hand grip.");
                Pose(camera, new Vector3(.85f, 1.35f, 2.8f), new Vector3(-.1f, .95f, .7f));
                RequireFramed(camera, VisibleBounds(held), "Held BAT");
                RequirePoint(camera, avatar.RightHand.position, "Right-hand socket");
                RequirePoint(camera, avatar.Root.transform.position + Vector3.up, "Avatar torso");
                var heldBefore = MittenFacts(avatar, "held-before-render");
                var heldImage = Capture(preview, output, "original-bat-held.png");
                var heldAfter = MittenFacts(avatar, "held-after-render");
                var receipt = new {
                    schema = 1, status = "CAPTURED_NOT_VISUALLY_APPROVED", utc = DateTime.UtcNow.ToString("O"),
                    sourceCommit = ReadHead(project), sourceFileSha256 = Hash(File.ReadAllBytes(Path.Combine(project, ScriptPath))),
                    unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(), colorSpace = QualitySettings.activeColorSpace.ToString(),
                    pipeline = AssetFact(GraphicsSettings.currentRenderPipeline), pipelineType = GraphicsSettings.currentRenderPipeline.GetType().FullName,
                    course = AssetFact(course), profile = AssetFact(profile), artwork = profile.BaseArtwork.ToString(),
                    scope = "Isolated Editor PreviewRenderUtility; neutral preview lighting, not Windows gameplay or owner acceptance.",
                    geometryEvidence = "Original shared Mesh identity and transformed mesh/renderer bounds; no new vertex scan or physics coverage.",
                    cameraSettings = new { fov = camera.fieldOfView, near = camera.nearClipPlane, far = camera.farClipPlane,
                        hdr = camera.allowHDR, msaa = camera.allowMSAA, cameraType = camera.cameraType.ToString() },
                    lights = preview.lights.Select(l => new { l.intensity, color = ColorFact(l.color),
                        rotation = V(l.transform.eulerAngles), type = l.type.ToString() }).ToArray(), ambient = ColorFact(preview.ambientColor),
                    animation = new { idleSteps = PoseSteps, additionalHeldSteps = PoseSteps, stepSeconds = 1f / 60,
                        idleClip = "Idle", heldClip = "Hold_OneHand", inputOrEquipSimulation = false },
                    previewSkinning = new { forceMatrixRecalculationPerRender = true, renderers = skinned.Length,
                        observations = new[] { idleBefore, idleAfter, heldBefore, heldAfter },
                        scope = "CPU-baked right-hand-weighted mitten vertices and socket facts; not a GPU pixel/contact or production fix claim." },
                    originals = facts, held = VisualFacts("BAT held", bat.Model, held, Size(bat), heldSkin, stage.transform),
                    hand = new { socket = V(avatar.RightHand.position), itemGrip = V(grip), gapMetres = gap, expectedScale = bat.HeldScale },
                    images = new[] { worldImage, heldImage }
                };
                WriteNew(Path.Combine(output, "receipt.json"), System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(receipt, Formatting.Indented)));
                Debug.Log("WORDBOUND_ORIGINAL_ART_CAPTURE_COMPLETE: " + output);
            }
            catch (Exception error)
            {
                WriteNew(Path.Combine(output, "failure.json"), System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                    status = "FAILED", exception = error.GetType().Name, error.Message, utc = DateTime.UtcNow.ToString("O")
                }, Formatting.Indented)));
                throw;
            }
            finally
            {
                try { motion?.Dispose(); }
                finally
                {
                    try
                    {
                        if (stage) TactileMaterials.Release(stage);
                        foreach (var visual in visuals) visual.Dispose();
                    }
                    finally
                    {
                        try { preview?.Cleanup(); }
                        finally
                        {
                            if (stage) Object.DestroyImmediate(stage);
                            if (groundMaterial) Object.DestroyImmediate(groundMaterial);
                            RenderTexture.active = previousActive;
                            EditorSettings.asyncShaderCompilation = asynchronousShaders;
                        }
                    }
                }
            }
        }

        static object Capture(PreviewRenderUtility preview, string output, string filename)
        {
            Texture2D pixels = null;
            bool opened = false;
            try
            {
                preview.BeginStaticPreview(new Rect(0, 0, Width, Height));
                opened = true;
                preview.Render(true);
                preview.Render(true);
                pixels = preview.EndStaticPreview();
                opened = false;
                Require(pixels && pixels.width == Width && pixels.height == Height, "The native capture must have the requested dimensions.");
                var colors = pixels.GetPixels32();
                int nonblack = 0, opaque = 0, minimum = 765, maximum = 0;
                var distinct = new HashSet<int>();
                foreach (var color in colors)
                {
                    int sum = color.r + color.g + color.b;
                    if (sum > 6) nonblack++;
                    if (color.a > 0) opaque++;
                    minimum = Math.Min(minimum, sum); maximum = Math.Max(maximum, sum);
                    if (distinct.Count < 4096) distinct.Add(color.r << 16 | color.g << 8 | color.b);
                }
                Require(nonblack > colors.Length / 20 && opaque > colors.Length / 20 && distinct.Count > 64 && maximum - minimum > 40,
                    "The single completed render is blank or insufficiently varied; preserve failure instead of retrying.");
                byte[] png = pixels.EncodeToPNG();
                WriteNew(Path.Combine(output, filename), png);
                return new { filename, width = Width, height = Height, sha256 = Hash(png), bytes = png.Length,
                    nonblack, opaque, distinctRgbCapped4096 = distinct.Count, minimumRgbSum = minimum, maximumRgbSum = maximum,
                    cameraPosition = V(preview.camera.transform.position), cameraRotation = V(preview.camera.transform.eulerAngles),
                    nativeRenderCalls = 2, windowsGameplay = false };
            }
            finally
            {
                if (opened) { var abandoned = preview.EndStaticPreview(); if (abandoned) Object.DestroyImmediate(abandoned); }
                if (pixels) Object.DestroyImmediate(pixels);
            }
        }

        static object MittenFacts(WordboundVisualInstance avatar, string phase)
        {
            var mitten = avatar.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(r => r.name == "SK_Mittens" && r.enabled && !r.forceRenderingOff);
            var hand = ModelVisual.FindNamed(avatar.Model, "hand_R");
            int bone = Array.IndexOf(mitten.bones, hand);
            Require(hand && bone >= 0 && mitten.sharedMesh, "The actual visible mitten must bind the right-hand bone.");
            var weights = new List<BoneWeight>();
            mitten.sharedMesh.GetBoneWeights(weights);
            var baked = new Mesh { name = "Owned mitten pose observation", hideFlags = HideFlags.HideAndDontSave };
            try
            {
                mitten.BakeMesh(baked, false);
                var vertices = new List<Vector3>();
                baked.GetVertices(vertices);
                Require(vertices.Count > 0 && vertices.Count == weights.Count, "Baked mitten vertices must retain their original weight indices.");
                var bounds = new Bounds();
                Vector3 sum = Vector3.zero, closest = Vector3.zero;
                float distance = float.PositiveInfinity, minWeight = 1, maxWeight = 0;
                int count = 0;
                for (int i = 0; i < vertices.Count; i++)
                {
                    var weight = weights[i];
                    float right = (weight.boneIndex0 == bone ? weight.weight0 : 0) +
                        (weight.boneIndex1 == bone ? weight.weight1 : 0) +
                        (weight.boneIndex2 == bone ? weight.weight2 : 0) +
                        (weight.boneIndex3 == bone ? weight.weight3 : 0);
                    Require(float.IsFinite(right) && right >= 0 && right <= 1.001f, "Original right-hand bone weights must be finite and normalized.");
                    if (right <= .5f) continue;
                    var point = mitten.transform.TransformPoint(vertices[i]);
                    Require(float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z), "Baked mitten geometry must be finite.");
                    if (count == 0) bounds = new Bounds(point, Vector3.zero); else bounds.Encapsulate(point);
                    count++; sum += point; minWeight = Mathf.Min(minWeight, right); maxWeight = Mathf.Max(maxWeight, right);
                    float gap = Vector3.Distance(point, avatar.RightHand.position);
                    if (gap < distance) { distance = gap; closest = point; }
                }
                Require(count > 0, "The original mitten must contain right-hand-weighted vertices.");
                return new { phase, unityFrame = Time.frameCount, editorTime = EditorApplication.timeSinceStartup,
                    mesh = AssetFact(mitten.sharedMesh), vertexCount = vertices.Count, rightWeightedVertices = count,
                    selection = "Original bone weight for hand_R exceeds 0.5; no left-mitten or nearest-side inference.",
                    minWeight, maxWeight, forceMatrixRecalculationPerRender = mitten.forceMatrixRecalculationPerRender,
                    bakeUseScale = false, rendererLossyScale = V(mitten.transform.lossyScale),
                    handPosition = V(hand.position), handRotation = V(hand.eulerAngles), socketPosition = V(avatar.RightHand.position),
                    socketInHand = V(hand.InverseTransformPoint(avatar.RightHand.position)),
                    rightBounds = BoundsFact(bounds), rightCentroid = V(sum / count), closestSocketVertex = V(closest),
                    closestSocketVertexDistanceMetres = distance, socketToRightBoundsMetres = Mathf.Sqrt(bounds.SqrDistance(avatar.RightHand.position)) };
            }
            finally { Object.DestroyImmediate(baked); }
        }

        static Transform Anchor(Transform stage, string name, Vector3 position)
        {
            var anchor = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }.transform;
            anchor.SetParent(stage, false); anchor.localPosition = position;
            return anchor;
        }
        static Material Ground(Transform stage)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader && shader.isSupported, "The neutral preview ground requires the existing URP Lit shader.");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Owned neutral preview ground"; ground.hideFlags = HideFlags.HideAndDontSave;
            ground.transform.SetParent(stage, false); ground.transform.localPosition = new Vector3(0, -.025f, 0);
            ground.transform.localScale = new Vector3(12, .05f, 12);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            var material = new Material(shader) { name = "Owned neutral preview ground", hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_BaseColor", new Color(.32f, .34f, .35f, 1)); material.SetFloat("_Smoothness", .15f);
            ground.GetComponent<Renderer>().sharedMaterial = material;
            return material;
        }
        static void AssertOriginal(WordboundVisualInstance visual, string modelKey)
        {
            var original = ModelLibrary.Load().Find(modelKey);
            Require(original, "Original ModelLibrary entry is missing: " + modelKey);
            Require(AssetDatabase.GetAssetPath(original) == "Assets/_Project/Art/Imported/" + modelKey + ".fbx",
                "The ModelLibrary entry must be the actual original imported FBX: " + modelKey);
            var expected = Meshes(original).GroupBy(m => m).ToDictionary(g => g.Key, g => g.Count());
            var actual = Meshes(visual.Model).GroupBy(m => m).ToDictionary(g => g.Key, g => g.Count());
            Require(expected.Count > 0 && actual.Count == expected.Count && expected.All(p => actual.TryGetValue(p.Key, out int count) && count == p.Value),
                "Original shared Mesh identity/count differs: " + modelKey);
        }
        static IEnumerable<Mesh> Meshes(GameObject model) =>
            model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)).Where(m => m);
        static object VisualFacts(string id, string modelKey, WordboundVisualInstance visual, Vector3 canonicalSize, string appearance, Transform stage) => new {
            id, modelKey, appearance, canonicalSize = V(canonicalSize), heldScale = visual.HeldScale,
            worldBounds = BoundsFact(VisibleBounds(visual)), transformedMeshBounds = BoundsFact(ModelVisual.BoundsIn(stage, visual.Model)),
            rootPosition = V(visual.Root.transform.position), rootScale = V(visual.Root.transform.localScale),
            gripLocal = V(visual.Grip), meshes = Meshes(visual.Model).Select(AssetFact).ToArray(),
            materials = visual.Model.GetComponentsInChildren<Renderer>(true).Select(r => new {
                renderer = r.name, enabled = r.enabled, forceOff = r.forceRenderingOff,
                slots = r.sharedMaterials.Select(m => new { asset = AssetFact(m), shader = m.shader.name,
                    textures = m.GetTexturePropertyNames().Select(p => new { property = p, texture = AssetFact(m.GetTexture(p)) }).ToArray() }).ToArray()
            }).ToArray()
        };
        static object AssetFact(Object asset)
        {
            if (!asset) return null;
            string path = AssetDatabase.GetAssetPath(asset);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
            return new { name = asset.name, path, guid, localId,
                dependencyHash = path.Length == 0 ? "" : AssetDatabase.GetAssetDependencyHash(path).ToString() };
        }
        static Bounds VisibleBounds(WordboundVisualInstance visual)
        {
            var visible = visual.Model.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy).ToArray();
            Require(visible.Length > 0, "A captured model must have visible renderers.");
            var result = visible[0].bounds;
            foreach (var renderer in visible.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }
        static void Pose(Camera camera, Vector3 position, Vector3 target) =>
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
        static void RequireFramed(Camera camera, Bounds bounds, string label)
        {
            for (int i = 0; i < 8; i++) RequirePoint(camera, bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)), label);
        }
        static void RequirePoint(Camera camera, Vector3 point, string label)
        {
            var viewport = camera.WorldToViewportPoint(point);
            Require(float.IsFinite(viewport.x) && float.IsFinite(viewport.y) && float.IsFinite(viewport.z) &&
                viewport.z > camera.nearClipPlane && viewport.z < camera.farClipPlane &&
                viewport.x >= .02f && viewport.x <= .98f && viewport.y >= .02f && viewport.y <= .98f,
                label + " is outside the fixed capture framing; revise the capture, not the model dimensions.");
        }
        static string FreshOutput(string project)
        {
            string name = Environment.GetEnvironmentVariable("WORDBOUND_ART_CAPTURE_NAME");
            if (string.IsNullOrEmpty(name)) name = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Require(Regex.IsMatch(name, "^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$"), "Capture name must be a simple fresh folder label.");
            string parent = Path.Combine(project, "BuildNotes", "equipment-polish", "native-originals");
            for (var directory = new DirectoryInfo(parent); directory != null; directory = directory.Parent)
                if (directory.Exists) Require((directory.Attributes & FileAttributes.ReparsePoint) == 0, "Capture output cannot traverse a reparse point.");
            string output = Path.Combine(parent, name);
            Require(!Directory.Exists(output) && !File.Exists(output), "Capture output already exists; choose a new label.");
            Directory.CreateDirectory(output);
            return output;
        }
        static string ReadHead(string project)
        {
            string git = Path.Combine(project, ".git");
            if (File.Exists(git))
            {
                string pointer = File.ReadAllText(git).Trim();
                Require(pointer.StartsWith("gitdir: ", StringComparison.Ordinal), "Cannot resolve worktree Git metadata.");
                git = Path.GetFullPath(Path.Combine(project, pointer.Substring(8)));
            }
            string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
            if (head.StartsWith("ref: ", StringComparison.Ordinal))
            {
                string reference = head.Substring(5), common = git;
                if (File.Exists(Path.Combine(git, "commondir"))) common = Path.GetFullPath(Path.Combine(git, File.ReadAllText(Path.Combine(git, "commondir")).Trim()));
                string loose = Path.Combine(common, reference.Replace('/', Path.DirectorySeparatorChar));
                head = File.Exists(loose) ? File.ReadAllText(loose).Trim() :
                    File.ReadLines(Path.Combine(common, "packed-refs")).Single(l => l.EndsWith(" " + reference, StringComparison.Ordinal)).Split(' ')[0];
            }
            Require(Regex.IsMatch(head, "^[a-fA-F0-9]{40}$"), "Cannot identify capture source commit.");
            return head;
        }
        static void WriteNew(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(bytes, 0, bytes.Length);
        }
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        static Vector3 Size(ItemDefinition item) => new Vector3(item.Size[0], item.Size[1], item.Size[2]);
        static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
        static float[] ColorFact(Color c) => new[] { c.r, c.g, c.b, c.a };
        static object BoundsFact(Bounds b) => new { center = V(b.center), size = V(b.size), min = V(b.min), max = V(b.max) };
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
