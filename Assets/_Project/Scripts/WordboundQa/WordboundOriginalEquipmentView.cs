#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GameStudioKit.Qa;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Qa
{
    public sealed class WordboundOriginalEquipmentView : MonoBehaviour
    {
        QaContext context;
        WordboundLauncher launcher;
        WordboundLocalMatchClient client;
        WordboundPlayerView view;
        WordboundMatchHud hud;
        WordboundGameplayPresentation presentation;
        Camera camera;
        Vector3 previousPosition;
        Quaternion previousRotation;
        float previousFov;
        double cameraSetAt;
        bool viewEnabled, hudEnabled, captured, restored, expectClient, subscribed, finishing;
        int cameraFrames, lastCameraFrame = -1;
        string shot = "world";
        Task fallbackShutdown;
        GUIStyle label;

        public void Begin(QaContext owner, WordboundLauncher source)
        {
            context = owner; launcher = source; hideFlags = HideFlags.DontSave;
        }

        public void ExpectClient()
        {
            if (!launcher || launcher.GetComponentInChildren<WordboundLocalMatchClient>())
                throw new QaFailException("Equipment QA must own a newly requested local client.");
            expectClient = true;
        }

        public void Prepare(WordboundLocalMatchClient source)
        {
            client = source; view = client.GetComponent<WordboundPlayerView>(); hud = client.GetComponent<WordboundMatchHud>();
            presentation = client.GetComponent<WordboundGameplayPresentation>();
            if (!view || !view.Camera || !hud || !presentation || presentation.ArtworkResolver == null ||
                presentation.ArtworkResolver.UseWordboundCollection || !presentation.ArtworkResolver.Profile ||
                presentation.ArtworkResolver.Profile.BaseArtwork != WordboundBaseArtwork.OriginalWreckabulary)
                throw new QaFailException("The actual client must use its assigned Original Wreckabulary presentation and camera.");
            if (presentation.ArtworkResolver.Profile.Overrides.Any(entry => entry != null &&
                (WordboundOriginalEquipmentQa.Props.Contains(entry.VisualKey) || entry.VisualKey == "player")))
                throw new QaFailException("Original equipment inspection does not accept replacement model overrides.");
            camera = view.Camera; previousPosition = camera.transform.position; previousRotation = camera.transform.rotation;
            previousFov = camera.fieldOfView; viewEnabled = view.enabled; hudEnabled = hud.enabled; captured = true;
            view.enabled = false; hud.enabled = false;
            RenderPipelineManager.endCameraRendering += PipelineCamera;
            Camera.onPostRender += BuiltinCamera; subscribed = true;
        }

        public void FrameWorld()
        {
            shot = "world";
            var world = client.Gameplay.CourseWorld;
            SetCamera(world.ToWorld(new CoursePoint(7500, 4300, -6500)), world.ToWorld(new CoursePoint(500, 900, 1400)), 48);
        }

        public void FrameHeld(string recipe)
        {
            shot = recipe;
            var origin = AvatarRoot().position;
            SetCamera(origin + new Vector3(-2.5f, 1.65f, 3.4f), origin + Vector3.up * 1.05f, 40);
        }

        void SetCamera(Vector3 position, Vector3 target, float fov)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            camera.fieldOfView = fov; cameraFrames = 0; lastCameraFrame = -1; cameraSetAt = Time.realtimeSinceStartupAsDouble;
        }

        Transform AvatarRoot()
        {
            if (!view || !view.Player) throw new QaFailException("The real local avatar is unavailable.");
            return view.Player.GetComponent<WordboundGameplayBody>().VisualRoot;
        }

        Transform Artwork(Transform parent, string key)
        {
            var root = parent ? parent.Find("Wordbound artwork " + key) : null;
            if (!root || !root.gameObject.activeInHierarchy) return null;
            return root;
        }

        Transform MapArtwork(string recipe)
        {
            var frame = client.Gameplay.Frame;
            var item = frame.Economy.Items.Single(value => value.Word == recipe && value.Origin == ItemOrigin.Map);
            if (!client.Gameplay.Bodies.TryGetValue("item:" + item.Id, out var body)) return null;
            return Artwork(body.GetComponent<WordboundGameplayBody>().VisualRoot, recipe);
        }

        public bool Ready(string held)
        {
            if (restored || !camera || !camera.isActiveAndEnabled || camera.targetTexture || camera.targetDisplay != 0 ||
                cameraFrames < 2 || Time.frameCount - lastCameraFrame > 1 || Time.realtimeSinceStartupAsDouble - cameraSetAt < .5 ||
                !presentation || presentation.Fault != "") return false;
            if (!Artwork(AvatarRoot(), "player")) return false;
            if (held == null) return WordboundOriginalEquipmentQa.Props.All(recipe => MapArtwork(recipe));
            var frame = client.Gameplay.Frame;
            string id = WordboundCombatPolicy.EquippedItemId(frame, WordboundLocalMatchClient.LocalActor);
            var item = frame.Economy.Items.SingleOrDefault(value => "item:" + value.Id == id);
            return item != null && item.Word == held && item.Origin == ItemOrigin.Crafted && item.State == ItemState.Held &&
                item.HolderId == 0 && item.CrafterId == 0 && !frame.Bodies.Single(body => body.Id == id).Active &&
                Artwork(AvatarRoot(), held);
        }

        public void Record(QaContext owner, string name, string held)
        {
            var facts = new List<object> { Observe("player", Artwork(AvatarRoot(), "player")) };
            if (held == null) foreach (string recipe in WordboundOriginalEquipmentQa.Props) facts.Add(Observe(recipe, MapArtwork(recipe)));
            else facts.Add(Observe(held, Artwork(AvatarRoot(), held)));
            var hand = ModelVisual.FindNamed(Artwork(AvatarRoot(), "player").gameObject, "grip_R");
            owner.Note(name + "_facts", new Dictionary<string, object> {
                ["scope"] = "Actual local presentation with QA inspection camera; not ordinary camera, multiplayer, GPU timing or art approval.",
                ["canonical_tick"] = client.Gameplay.Frame.Progress.Tick, ["mode"] = "Freestyle", ["local_actors"] = client.Gameplay.Frame.Actors.Count,
                ["artwork_source"] = "original_model_library", ["profile"] = presentation.ArtworkResolver.Profile.name,
                ["camera_position"] = V(camera.transform.position), ["camera_rotation"] = V(camera.transform.eulerAngles), ["camera_fov"] = camera.fieldOfView,
                ["camera_callbacks"] = cameraFrames, ["last_camera_frame"] = lastCameraFrame,
                ["right_hand_socket_world"] = hand ? V(hand.position) : null,
                ["held_recipe"] = held ?? "", ["artwork"] = facts });
        }

        object Observe(string key, Transform root)
        {
            if (!root) throw new QaFailException("Missing active actual artwork: " + key);
            string modelKey = presentation.ArtworkResolver.ModelKey(key);
            var source = ModelLibrary.Load()?.Find(modelKey);
            if (!source || !Meshes(source).SequenceEqual(Meshes(root.gameObject)))
                throw new QaFailException("Actual " + key + " meshes do not match the original ModelLibrary source.");
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled &&
                !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) throw new QaFailException("Actual " + key + " has no enabled visible renderer.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            for (int corner = 0; corner < 8; corner++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                var screen = camera.WorldToViewportPoint(point);
                if (screen.z <= camera.nearClipPlane || screen.x < .025f || screen.x > .975f || screen.y < .025f || screen.y > .92f)
                    throw new QaFailException("Actual " + key + " bounds do not fit the inspection frame with margin.");
            }
            var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).ToArray();
            if (materials.Any(material => !material || !material.shader || !material.shader.isSupported ||
                material.shader.name.Contains("InternalErrorShader"))) throw new QaFailException("Invalid actual artwork material/shader: " + key);
            var grip = ModelVisual.FindNamed(root.gameObject, "Grip_R");
            return new Dictionary<string, object> { ["visual_key"] = key, ["original_model"] = modelKey,
                ["original_mesh_identity"] = true, ["mesh_names"] = Meshes(source).Select(mesh => mesh.name).ToArray(),
                ["world_bounds_center"] = V(bounds.center), ["world_bounds_size"] = V(bounds.size),
                ["visual_root_local_scale"] = V(root.localScale), ["visual_root_world"] = V(root.position),
                ["authored_grip_marker_world"] = grip ? V(grip.position) : null,
                ["material_names"] = materials.Select(material => material.name).ToArray(),
                ["shader_names"] = materials.Select(material => material.shader.name).Distinct().ToArray(),
                ["bounds_inside_frame"] = true, ["occlusion_or_hand_contact_visually_approved"] = false };
        }

        static Mesh[] Meshes(GameObject model) => model.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh)
            .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => renderer.sharedMesh)).ToArray();
        static float[] V(Vector3 value) => new[] { value.x, value.y, value.z };

        public static void VerifyPixels(QaContext context, string name, string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false) || texture.width != Screen.width || texture.height != Screen.height)
                    throw new QaFailException("The actual equipment PNG is missing, invalid or has different screen dimensions.");
                var pixels = texture.GetPixels32(); var first = pixels[0];
                if (!pixels.Any(pixel => pixel.a != 0 && (pixel.r != 0 || pixel.g != 0 || pixel.b != 0)) ||
                    !pixels.Any(pixel => pixel.r != first.r || pixel.g != first.g || pixel.b != first.b))
                    throw new QaFailException("The first equipment PNG is blank; evidence is retained without retry.");
                using var hash = SHA256.Create();
                context.Note(name + "_sha256", BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant());
                context.Note(name + "_dimensions", texture.width + "x" + texture.height);
            }
            finally { Destroy(texture); }
        }

        void PipelineCamera(ScriptableRenderContext _, Camera actual) { if (GraphicsSettings.currentRenderPipeline != null) Rendered(actual); }
        void BuiltinCamera(Camera actual) { if (GraphicsSettings.currentRenderPipeline == null) Rendered(actual); }
        void Rendered(Camera actual) { if (actual == camera && lastCameraFrame != Time.frameCount) { cameraFrames++; lastCameraFrame = Time.frameCount; } }

        void Update()
        {
            if (expectClient && !client && launcher) client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
            if (!finishing && (!QaRunner.IsRunning || context == null ||
                !(context.CurrentStepName ?? "").StartsWith("equipment_", StringComparison.Ordinal) || !launcher))
                Finish();
        }

        void OnGUI()
        {
            if (!captured || restored) return;
            label ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(12, 12, Mathf.Min(780, Screen.width - 24), 32),
                "QA INSPECTION CAMERA | ORIGINAL WRECKABULARY | " + (shot == "world" ? "4 map props + actual player" : "actually crafted / equipped " + shot), label);
        }

        public void RestoreView()
        {
            if (restored) return;
            restored = true;
            if (subscribed)
            {
                RenderPipelineManager.endCameraRendering -= PipelineCamera;
                Camera.onPostRender -= BuiltinCamera; subscribed = false;
            }
            if (!captured) return;
            if (camera) { camera.transform.SetPositionAndRotation(previousPosition, previousRotation); camera.fieldOfView = previousFov; }
            if (view) view.enabled = viewEnabled;
            if (hud) hud.enabled = hudEnabled;
        }

        public void Finish()
        {
            if (finishing) return;
            finishing = true; RestoreView();
            if (expectClient && !client && launcher) client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
            if (client && !client.ShutdownConfirmed) fallbackShutdown = Shutdown(client);
            Destroy(this);
        }

        static async Task Shutdown(WordboundLocalMatchClient owned)
        {
            try { await owned.ReturnToWorkshop(); }
            catch (Exception error) { Debug.LogError("Equipment QA fallback shutdown failed: " + error.GetType().Name); }
        }

        void OnDisable() => Finish();
        void OnDestroy() => Finish();
    }
}
#endif
