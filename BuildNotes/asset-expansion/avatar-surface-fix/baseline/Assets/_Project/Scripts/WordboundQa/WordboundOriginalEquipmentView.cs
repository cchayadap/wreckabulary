#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GameStudioKit.Common;
using GameStudioKit.Qa;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

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
        WordboundGameplayController observed;
        Collider roomFloor;
        readonly List<object> firstSamples = new List<object>();
        readonly Queue<object> sampleTail = new Queue<object>();
        long firstTick = -1, lastTick = -1, observations, missedTicks, largestTickGap;
        long settleTick = -1, settleLastTick = -1;
        long settleUnobservedTicks, settleLargestGap;
        long observedEpoch = -1, settleEpoch = -1;
        int epochChanges, settleFrames, settleLastFrame = -1;
        float maxPlayerY = float.NegativeInfinity, maxKeepsakeY = float.NegativeInfinity;
        string observationError = "";
        bool initialComplete;
        WordboundMeleeCapture meleeCapture;

        internal WordboundMeleeCapture BeginMelee(ItemDefinition item)
        {
            if (meleeCapture != null) throw new QaFailException("Only one bounded melee capture may run.");
            meleeCapture = new WordboundMeleeCapture(context, client, this, item);
            return meleeCapture;
        }

        internal void EndMelee()
        {
            meleeCapture?.Dispose();
            meleeCapture = null;
        }

        internal Camera MeleeCamera => camera;

        internal object SampleMelee(out Transform avatar, out Transform hand, out Transform held, out Transform grip)
        {
            if (!Ready("BAT") || lastCameraFrame != Time.frameCount)
                throw new QaFailException("The actual BAT camera did not render this frame with ready original equipment.");
            avatar = AvatarRoot();
            hand = ModelVisual.FindNamed(Artwork(avatar, "player").gameObject, "grip_R");
            held = Artwork(avatar, "BAT");
            grip = held ? ModelVisual.FindNamed(held.gameObject, "Grip_R") : null;
            if (!hand || !held || !grip) throw new QaFailException("The live original BAT and right-hand grip markers are required.");
            return new[] { Observe("player", Artwork(avatar, "player")), Observe("BAT", held) };
        }

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
            AttachObservation(source);
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
            roomFloor = client.Gameplay.CourseWorld.Surfaces.Single(surface => {
                var node = surface.GetComponent<WordboundCourseNode>();
                return node && node.Kind == WordboundCourseNodeKind.Floor && node.ModuleId == "Room";
            });
            RecordPhysics("first_ready");
        }

        void AttachObservation(WordboundLocalMatchClient source)
        {
            if (!source || !source.Gameplay || observed) return;
            observed = source.Gameplay;
            observed.FrameChanged += ObserveFrame;
            if (observed.Frame != null) ObserveFrame(observed.Frame);
        }

        void ObserveFrame(MatchSnapshot frame)
        {
            if (initialComplete || observationError != "") return;
            try
            {
                if (observedEpoch != frame.Epoch) { if (observedEpoch >= 0) epochChanges++; observedEpoch = frame.Epoch; lastTick = -1; }
                if (frame.Progress.Tick <= lastTick) return;
                var player = Body(frame, "player:0"); var keepsake = Body(frame, "keepsake:0");
                if (player == null || keepsake == null) return;
                if (firstTick < 0) firstTick = frame.Progress.Tick;
                if (lastTick >= 0) { long gap = frame.Progress.Tick - lastTick; missedTicks += Math.Max(0, gap - 1); largestTickGap = Math.Max(largestTickGap, gap); }
                lastTick = frame.Progress.Tick; observations++;
                maxPlayerY = Mathf.Max(maxPlayerY, player.Rigidbody.position.y);
                maxKeepsakeY = Mathf.Max(maxKeepsakeY, keepsake.Rigidbody.position.y);
                if (firstSamples.Count < 16 || observations % 15 == 0)
                {
                    var sample = PhysicsSample(frame, player, keepsake, firstSamples.Count < 16);
                    if (firstSamples.Count < 16) firstSamples.Add(sample);
                    else { if (sampleTail.Count == 32) sampleTail.Dequeue(); sampleTail.Enqueue(sample); }
                }
            }
            catch (Exception error) { observationError = error.GetType().Name; }
        }

        WordboundNetworkBody Body(MatchSnapshot frame, string id) =>
            frame.Bodies.Any(state => state.Id == id) && observed.Bodies.TryGetValue(id, out var body) && body.Rigidbody ? body : null;

        object PhysicsSample(MatchSnapshot frame, WordboundNetworkBody player, WordboundNetworkBody keepsake, bool detailed)
        {
            var sample = new Dictionary<string, object> { ["epoch"] = frame.Epoch, ["tick"] = frame.Progress.Tick,
                ["unity_frame"] = Time.frameCount, ["realtime_seconds"] = Time.realtimeSinceStartupAsDouble,
                ["player"] = BodyFacts(frame, player, "player:0", detailed),
                ["keepsake"] = BodyFacts(frame, keepsake, "keepsake:0", detailed) };
            var capsule = player.GetComponent<CapsuleCollider>(); var box = keepsake.GetComponent<BoxCollider>();
            if (capsule && box)
            {
                sample["player_keepsake_penetrating"] = Physics.ComputePenetration(capsule, player.Rigidbody.position, player.Rigidbody.rotation,
                    box, keepsake.Rigidbody.position, keepsake.Rigidbody.rotation, out _, out float depth);
                sample["penetration_depth"] = depth;
            }
            return sample;
        }

        object BodyFacts(MatchSnapshot frame, WordboundNetworkBody body, string id, bool detailed)
        {
            var state = frame.Bodies.Single(value => value.Id == id);
            var visual = body.GetComponent<WordboundGameplayBody>().VisualRoot;
            var facts = new Dictionary<string, object> { ["id"] = id, ["canonical_position"] = V(Vector(state.Position)),
                ["canonical_velocity"] = V(Vector(state.Velocity)), ["canonical_active"] = state.Active, ["canonical_kinematic"] = state.Kinematic,
                ["native_position"] = V(body.Rigidbody.position), ["native_velocity"] = V(body.Rigidbody.linearVelocity),
                ["native_kinematic"] = body.Rigidbody.isKinematic, ["native_active"] = body.gameObject.activeInHierarchy,
                ["transform_position"] = V(body.transform.position), ["visual_parent_world"] = visual ? V(visual.position) : null,
                ["visual_parent_local"] = visual ? V(visual.localPosition) : null };
            if (detailed) facts["owned_colliders"] = body.OwnedColliders.Select(collider => new Dictionary<string, object> {
                ["type"] = collider.GetType().Name, ["enabled"] = collider.enabled, ["active"] = collider.gameObject.activeInHierarchy,
                ["trigger"] = collider.isTrigger, ["attached_to_this_body"] = collider.attachedRigidbody == body.Rigidbody,
                ["center_world"] = V(collider.bounds.center), ["size_world"] = V(collider.bounds.size),
                ["contact_offset"] = collider.contactOffset }).ToArray();
            return facts;
        }

        static Vector3 Vector(WorldVector value) => new Vector3(value.X, value.Y, value.Z);

        public bool Settled
        {
            get
            {
                if (observationError != "") throw new QaFailException("Equipment physics observation failed: " + observationError);
                return observed && observed.Frame != null && Body(observed.Frame, "player:0") && Body(observed.Frame, "keepsake:0") &&
                    observed.Frame.Epoch == settleEpoch && observed.Frame.Progress.Tick >= settleLastTick &&
                    !observed.IsSimulationPaused && Time.frameCount - settleLastFrame <= 1 &&
                    settleFrames >= 30 && settleLastTick - settleTick >= 30;
            }
        }

        void LateUpdate()
        {
            if (!captured || restored || initialComplete) return;
            if (observationError != "" || !observed || observed.Frame == null) { settleFrames = 0; settleTick = -1; return; }
            try
            {
                var frame = observed.Frame; long tick = frame.Progress.Tick;
                var player = Body(frame, "player:0"); var keepsake = Body(frame, "keepsake:0");
                if (player == null || keepsake == null) { settleFrames = 0; settleTick = -1; return; }
                if (settleEpoch == frame.Epoch && tick == settleLastTick || settleLastFrame == Time.frameCount) return;
                bool ordered = settleEpoch == frame.Epoch && tick > settleLastTick;
                if (ordered) { long gap = tick - settleLastTick; settleUnobservedTicks += Math.Max(0, gap - 1); settleLargestGap = Math.Max(settleLargestGap, gap); }
                settleEpoch = frame.Epoch; settleLastTick = tick; settleLastFrame = Time.frameCount;
                var capsule = player ? player.GetComponent<CapsuleCollider>() : null;
                var box = keepsake ? keepsake.GetComponent<BoxCollider>() : null;
                bool valid = !observed.IsSimulationPaused && Grounded(frame, player, capsule, "player:0") &&
                    Grounded(frame, keepsake, box, "keepsake:0") &&
                    !Physics.ComputePenetration(capsule, player.Rigidbody.position, player.Rigidbody.rotation,
                        box, keepsake.Rigidbody.position, keepsake.Rigidbody.rotation, out _, out _);
                if (!valid) { settleFrames = 0; settleTick = -1; }
                else if (!ordered || settleFrames == 0) { settleFrames = 1; settleTick = tick; }
                else settleFrames++;
            }
            catch (Exception error) { observationError = error.GetType().Name; }
        }

        bool Grounded(MatchSnapshot frame, WordboundNetworkBody body, Collider collider, string id)
        {
            if (!body || !collider || !roomFloor || !roomFloor.enabled || roomFloor.isTrigger || !roomFloor.gameObject.activeInHierarchy ||
                !body.gameObject.activeInHierarchy || !collider.gameObject.activeInHierarchy || !body.OwnedColliders.Contains(collider) ||
                !collider.enabled || collider.isTrigger || collider.attachedRigidbody != body.Rigidbody ||
                body.Rigidbody.isKinematic || body.Rigidbody.linearVelocity.sqrMagnitude > .05f * .05f) return false;
            var state = frame.Bodies.Single(value => value.Id == id);
            var visual = body.GetComponent<WordboundGameplayBody>().VisualRoot;
            if (!state.Active || state.Kinematic || !visual ||
                Vector3.Distance(Vector(state.Position), body.Rigidbody.position) > .025f ||
                Vector3.Distance(visual.position, body.Rigidbody.position) > .05f ||
                Mathf.Abs(collider.bounds.min.y - roomFloor.bounds.max.y) > .025f) return false;
            var floorPoint = new Vector3(collider.bounds.center.x, roomFloor.bounds.max.y + .1f, collider.bounds.center.z);
            return roomFloor.Raycast(new Ray(floorPoint, Vector3.down), out var hit, .2f) && hit.collider == roomFloor;
        }

        public void RecordPhysics(string boundary, string target = "")
        {
            if (!observed || observed.Frame == null) return;
            var frame = observed.Frame; var player = Body(frame, "player:0"); var keepsake = Body(frame, "keepsake:0");
            if (player == null || keepsake == null) throw new QaFailException("Actual player/keepsake physics facts are unavailable.");
            var facts = new Dictionary<string, object> { ["boundary"] = boundary, ["sample"] = PhysicsSample(frame, player, keepsake, true),
                ["observation_error"] = observationError, ["first_tick"] = firstTick, ["last_tick"] = lastTick,
                ["canonical_callbacks"] = observations, ["unobserved_ticks"] = missedTicks, ["largest_tick_gap"] = largestTickGap,
                ["observed_epoch_changes"] = epochChanges,
                ["maximum_observed_player_native_y"] = maxPlayerY, ["maximum_observed_keepsake_native_y"] = maxKeepsakeY,
                ["settle_frames"] = settleFrames, ["settle_first_tick"] = settleTick, ["settle_last_tick"] = settleLastTick,
                ["settle_observation_unobserved_ticks"] = settleUnobservedTicks, ["settle_observation_largest_tick_gap"] = settleLargestGap,
                ["floor_name"] = roomFloor ? roomFloor.name : "", ["floor_top_y"] = roomFloor ? roomFloor.bounds.max.y : (object)null,
                ["scope"] = "Same-callback canonical/native/visual observations; visual interpolation may lag. Settling uses distinct LateUpdate frames, not continuous contact proof. No pose or physics writes." };
            if (boundary == "first_ready" || boundary == "before_wordbound_original_world" || boundary == "return_entry" || boundary == "cleanup")
            { facts["first_samples"] = firstSamples.ToArray(); facts["periodic_tail"] = sampleTail.ToArray(); }
            var capsule = player.GetComponent<CapsuleCollider>(); var box = keepsake.GetComponent<BoxCollider>();
            if (capsule && box)
            {
                facts["player_keepsake_penetrating"] = Physics.ComputePenetration(capsule, player.Rigidbody.position, player.Rigidbody.rotation,
                    box, keepsake.Rigidbody.position, keepsake.Rigidbody.rotation, out var direction, out float depth);
                facts["penetration_direction"] = V(direction); facts["penetration_depth"] = depth;
                facts["player_grounded_predicate"] = Grounded(frame, player, capsule, "player:0");
                facts["keepsake_grounded_predicate"] = Grounded(frame, keepsake, box, "keepsake:0");
            }
            var destination = frame.Bodies.SingleOrDefault(value => value.Id == target);
            if (destination != null)
            {
                var origin = frame.Bodies.Single(value => value.Id == "player:0");
                facts["target"] = target; facts["target_position"] = V(Vector(destination.Position));
                facts["canonical_distance"] = Vector3.Distance(Vector(origin.Position), Vector(destination.Position));
                facts["native_player_to_canonical_target_distance"] = Vector3.Distance(player.Rigidbody.position, Vector(destination.Position));
                facts["host_line_of_sight"] = observed.PhysicsWorld.HasLineOfSight(WordboundLocalMatchClient.LocalActor, origin.Position, destination.Position);
            }
            File.WriteAllText(context.PathInRun("equipment-physics-" + boundary + ".json"), KitJson.Serialize(facts));
            context.Note("equipment_physics_" + boundary, facts);
        }

        public void RecordBeforeCleanup()
        {
            try { RecordPhysics("return_entry"); }
            catch (Exception error) { context?.Note("equipment_physics_return_entry_error", error.GetType().Name); }
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
            RecordPhysics("before_" + name);
            if (held == null) initialComplete = true;
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
            if (meleeCapture != null && (!QaRunner.IsRunning || context?.CurrentStepName != "equipment_bat_motion")) EndMelee();
            if (expectClient && !client && launcher) client = launcher.GetComponentInChildren<WordboundLocalMatchClient>();
            if (expectClient && client) AttachObservation(client);
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
            EndMelee();
            if (restored) return;
            restored = true;
            if (observed) observed.FrameChanged -= ObserveFrame;
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
            finishing = true;
            try
            {
                if (observed && observed.Bodies.ContainsKey("player:0") && observed.Bodies.ContainsKey("keepsake:0")) RecordPhysics("cleanup");
            }
            catch (Exception error) { context?.Note("equipment_physics_cleanup_error", error.GetType().Name); }
            RestoreView();
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
