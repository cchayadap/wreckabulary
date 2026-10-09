using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Cosmetic imported avatar, wardrobe and canonical animation playback.</summary>
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(90)]
    public sealed class PlayerAppearance : MonoBehaviour
    {
        const string AvatarKey = "Avatar/Avatar";
        [Tooltip("An authored avatar instance beneath Visual. Runtime initialization preserves this hierarchy and its transforms.")]
        [SerializeField] GameObject authoredAvatar;
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        readonly List<Renderer> oldRenderers = new List<Renderer>();
        PlayerController controller;
        GameObject model;
        SkinnedMeshRenderer[] meshes;
        Transform gripL, gripR;
        Quaternion gripCorrectionL, gripCorrectionR;
        PlayableGraph graph;
        AnimationLayerMixerPlayable layers;
        MotionTrack locomotion, upperBody;
        AvatarMask upperBodyMask;
        SkinnedMeshRenderer face;
        int blinkShape = -1, browShape = -1;
        float poseWeight, expression, blinkAt;
        string actionClip;
        bool fullBodyAction;
        float actionUntil;
        float actionStarted, actionDuration, blockReactionUntil;
        bool itemUsePose, eating, suppressReleasePose;
        bool wasHolding;
        bool initialized;
        Outfit outfit;
        Transform[] postureBones;
        Quaternion[] postureBase, postureWritten;
        Vector3 lastVelocity;
        float lean, bank;
        ItemContactPose itemContact;

        public const float WalkStride = .2483f, RunStride = .5176f;
        const float WalkStretch = 1.7f, RunStretch = 2f, MinCycles = .6f, MaxWalkCycles = 2.4f, MaxRunCycles = 3.6f;
        public static float CyclesPerSecond(bool run, float speed) =>
            Mathf.Clamp(speed / (run ? RunStride * RunStretch : WalkStride * WalkStretch),
                MinCycles, run ? MaxRunCycles : MaxWalkCycles);
        public bool IsAnimationReady => initialized && graph.IsValid() && graph.IsPlaying() && locomotion != null && locomotion.Playable.IsValid();
        public AnimationClip CurrentAnimationClip => CurrentPlayable.IsValid() ? CurrentPlayable.GetAnimationClip() : null;
        public AnimationClipPlayable CurrentPlayable => Time.time < actionUntil
            ? (fullBodyAction ? locomotion?.Playable : upperBody?.Playable) ?? default
            : poseWeight > .5f && upperBody != null ? upperBody.Playable : locomotion?.Playable ?? default;


        public Outfit CurrentOutfit => outfit?.Clone();
        public GameObject AuthoredAvatar => authoredAvatar;
        public GameObject AvatarModel => model;
        public Transform RightGrip => gripR;
        public string LocomotionClip => locomotion?.Current;
        public string UpperBodyClip => upperBody?.Current;
        public float UpperBodyWeight => poseWeight;
        public bool IsUsingItemPose => itemUsePose && Time.time < actionUntil;

        /// <summary>The unsaved presentation look; shared rules defaults and saved player choices remain independent.</summary>
        public static Outfit DefaultPresentationOutfit()
        {
            var catalogue = GameConfig.Current.Wardrobe;
            var look = catalogue.Default.Clone();
            look.Pieces["Top"] = "Hoodie";
            look.Pieces["Headwear"] = "Hood";
            look.Pieces["Back"] = "Satchel";
            look.Colours["Gloves"] = "charcoal";
            look.Colours["Bottoms"] = "charcoal";
            return catalogue.Sanitize(look);
        }

        public static Outfit PresentationOutfit(int seat, Color colour)
        {
            var catalogue = GameConfig.Current.Wardrobe;
            string saved = PlayerPrefs.GetString("wv.outfit." + seat, "");
            var initial = string.IsNullOrEmpty(saved) ? DefaultPresentationOutfit() : Outfit.Deserialize(saved);
            if (string.IsNullOrEmpty(saved) && catalogue.Palettes.TryGetValue("Top", out var palette))
            {
                float nearest = float.PositiveInfinity;
                foreach (var option in palette)
                {
                    float distance = (new Vector3(option.R, option.G, option.B) - new Vector3(colour.r, colour.g, colour.b)).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    initial.Colours["Top"] = option.Id;
                }
            }
            return catalogue.Sanitize(initial);
        }

        /// <summary>Reuse saved artist content; create a visual only for legacy prefabs after all required clips validate.</summary>
        public bool Initialize(PlayerController player)
        {
            controller = player;
            if (initialized) return IsAnimationReady;
            var library = ModelLibrary.Load();
            if (!controller || !controller.visual || !library || !library.Find(AvatarKey)) return false;
            if (!LoadRequiredClips(library)) return false;
            oldRenderers.Clear();
            GameObject createdRoot = null;
            model = authoredAvatar;
            if (!model)
            {
                foreach (var renderer in controller.visual.GetComponentsInChildren<Renderer>(true)) oldRenderers.Add(renderer);
                createdRoot = new GameObject("ImportedAvatar");
                createdRoot.transform.SetParent(controller.visual, false);
                model = ModelVisual.Spawn(AvatarKey, createdRoot.transform);
            }
            var animator = model ? model.GetComponentInChildren<Animator>(true) : null;
            if (!animator)
            {
                if (createdRoot) { createdRoot.SetActive(false); Destroy(createdRoot); }
                model = null;
                clips.Clear();
                return false;
            }
            meshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            gripL = ModelVisual.FindNamed(model, "grip_L");
            gripR = ModelVisual.FindNamed(model, "grip_R");
            if (gripL && controller.handL) gripCorrectionL = Quaternion.Inverse(gripL.rotation) * controller.handL.rotation;
            if (gripR && controller.handR) gripCorrectionR = Quaternion.Inverse(gripR.rotation) * controller.handR.rotation;
            foreach (var mesh in meshes)
            {
                if (!mesh.sharedMesh || mesh.name != "SK_Head") continue;
                face = mesh;
                for (int i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
                {
                    string shape = mesh.sharedMesh.GetBlendShapeName(i);
                    if (shape.EndsWith("Blink", System.StringComparison.OrdinalIgnoreCase)) blinkShape = i;
                    if (shape.EndsWith("BrowRelax", System.StringComparison.OrdinalIgnoreCase)) browShape = i;
                }
            }
            blinkAt = Time.time + 2.2f + controller.Index * .31f;
            itemContact = new ItemContactPose(model, gripR, face, gripCorrectionR);
            animator.applyRootMotion = false;
            // Gameplay gear follows these animated sockets even when the avatar is outside the camera frustum.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Wreckabulary Avatar " + controller.Index);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            locomotion = new MotionTrack(graph, clips);
            upperBody = new MotionTrack(graph, clips);
            layers = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(locomotion.Mixer, 0, layers, 0);
            graph.Connect(upperBody.Mixer, 0, layers, 1);
            layers.SetInputWeight(0, 1f);
            layers.SetInputWeight(1, 0f);
            upperBodyMask = CreateUpperBodyMask(animator.transform);
            layers.SetLayerMaskFromAvatarMask(1, upperBodyMask);
            AnimationPlayableOutput.Create(graph, "Avatar", animator).SetSourcePlayable(layers);
            locomotion.Set("Idle", true);
            graph.Play();
            graph.Evaluate(0f);
            foreach (var renderer in oldRenderers) if (renderer) renderer.enabled = false;
            initialized = true;
            ApplyOutfit(PresentationOutfit(controller.Index, controller.Color), false);
            controller.Jumped += OnJumped;
            controller.Dodged += OnDodged;
            if (controller.Health) controller.Health.Damaged += OnDamaged;
            if (controller.Summoner) controller.Summoner.Summoned += OnSummoned;
            var bones = new List<Transform>();
            foreach (string bone in new[] { "spine", "chest", "head" })
            {
                var found = ModelVisual.FindNamed(model, bone);
                if (found) bones.Add(found);
            }
            postureBones = bones.ToArray();
            postureBase = new Quaternion[postureBones.Length];
            postureWritten = new Quaternion[postureBones.Length];
            return true;
        }

        bool LoadRequiredClips(ModelLibrary library)
        {
            clips.Clear();
            foreach (string name in new[] { "Idle", "Walk_InPlace", "Run_InPlace", "Jump_Preview",
                "Hold_OneHand", "Carry_TwoHand", "Block_Plate", "Swing_OneHand", "Thrust_OneHand",
                "Throw_OneHand", "Hit_Reaction", "Celebrate", "Drink_Consumable", "Pickup", "Place" })
            {
                var clip = library.FindClip(AvatarKey, name);
                if (!clip) { clips.Clear(); return false; }
                clips.Add(name, clip);
            }
            foreach (string name in new[] { "Inspect_OneHand", "Present_Item" })
            {
                var clip = library.FindClip(AvatarKey, name);
                if (clip) clips.Add(name, clip);
            }
            return true;
        }

        public void ApplyOutfit(Outfit next) => ApplyOutfit(next, true);

        void ApplyOutfit(Outfit next, bool save)
        {
            outfit = Dress(meshes, next);
            if (save && controller)
            {
                PlayerPrefs.SetString("wv.outfit." + controller.Index, outfit.Serialize());
                PlayerPrefs.Save();
            }
        }

        public static Outfit Dress(SkinnedMeshRenderer[] meshes, Outfit next)
        {
            var catalogue = GameConfig.Current.Wardrobe;
            var outfit = catalogue.Sanitize(next).Clone();
            if (meshes != null)
                foreach (var renderer in meshes)
                {
                    WardrobePiece piece = null;
                    foreach (var candidate in catalogue.Pieces)
                        if (candidate.Mesh == renderer.name) { piece = candidate; break; }
                    bool visible = piece == null || outfit.PieceIn(piece.Slot) == piece.Id;
                    renderer.enabled = visible;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var block = new MaterialPropertyBlock();
                        if (visible && piece != null)
                        {
                            var colour = catalogue.ColourFor(outfit, piece.Slot);
                            string materialName = materials[i] ? materials[i].name : "";
                            bool rib = MatchesTintMaterial(materialName, "fabric_rib") && piece.TintMaterial == "fabric_main";
                            if (colour != null && (MatchesTintMaterial(materialName, piece.TintMaterial) || rib))
                            {
                                var tint = new Color(colour.R, colour.G, colour.B, 1f);
                                if (rib) tint *= .75f;
                                tint.a = 1f;
                                block.SetColor("_BaseColor", tint);
                                block.SetColor("_Color", tint);
                            }
                        }
                        renderer.SetPropertyBlock(block, i);
                    }
                }
            return outfit;
        }

        public string SkinFor(string word) => outfit?.SkinFor(word) ?? Skin.Standard;

        static bool MatchesTintMaterial(string materialName, string canonicalName) =>
            !string.IsNullOrEmpty(canonicalName) && (materialName == canonicalName ||
                materialName.StartsWith(canonicalName + "__Default_", System.StringComparison.Ordinal));

        public void SetItemSkin(string word, string skin)
        {
            if (outfit == null || !GameConfig.Current.Items.TryGet(word, out var definition)) return;
            outfit.ItemSkins[word] = Appearance.Pick(definition, skin);
            ApplyOutfit(outfit);
        }

        /// <summary>Action visuals never change damage windows, movement or item rules.</summary>
        public void Play(string clipName, float duration = .5f)
        {
            if (!clips.ContainsKey(clipName)) return;
            itemUsePose = false;
            actionClip = clipName;
            fullBodyAction = clipName is "Idle" or "Walk_InPlace" or "Jump_Preview" or "Run_InPlace" or "Hit_Reaction" or "Celebrate";
            actionStarted = Time.time;
            actionDuration = Mathf.Max(.05f, duration);
            actionUntil = actionStarted + actionDuration;
            (fullBodyAction ? locomotion : upperBody)?.Set(clipName, true);
            poseWeight = fullBodyAction ? 0f : 1f;
            if (layers.IsValid()) layers.SetInputWeight(1, poseWeight);
        }

        /// <summary>Fit the authored hand-to-mouth motion to the actual use channel; food adds a small bite nod.</summary>
        public void PlayItemUse(ItemDefinition item, float duration)
        {
            Play("Drink_Consumable", duration);
            itemUsePose = true;
            eating = item?.Id is "APPLE" or "CAKE";
            suppressReleasePose = true;
        }

        void OnJumped(PlayerController _) => Play("Jump_Preview", .45f);
        void OnDodged(PlayerController _) => Play("Run_InPlace", .2f);

        void OnDamaged(PlayerHealth _, HitInfo __, HitResult result)
        {
            if (result.Blocked) blockReactionUntil = Time.time + .22f;
            if (result.Landed && !result.Blocked && result.Damage > 0f && !result.BecameDowned && !result.BecameEliminated)
                Play("Hit_Reaction", Mathf.Clamp(result.HitStun, .25f, .45f));
        }

        void OnSummoned(string word)
        {
            if (GameConfig.Current.Items.TryGet(word, out var definition) && definition.Consumable) return;
            if (clips.ContainsKey("Present_Item")) Play("Present_Item", .6f);
        }

        void Update()
        {
            if (!initialized || !controller) return;
            bool holding = controller.Combat && controller.Combat.IsHolding;
            bool blocking = controller.Combat && controller.Combat.IsBlocking;
            if (itemUsePose && (!holding || !controller.Combat.Weapon || !controller.Combat.Weapon.IsUsing))
            {
                itemUsePose = false;
                actionUntil = 0f;
            }
            if (!controller.IsKnockedOut && holding && !wasHolding && !itemUsePose) Play("Pickup", .28f);
            if (!controller.IsKnockedOut && !holding && wasHolding)
            {
                if (!suppressReleasePose) Play("Throw_OneHand", .35f);
                suppressReleasePose = false;
            }
            if (holding && !itemUsePose) suppressReleasePose = false;
            wasHolding = holding;
            var velocity = controller.Body ? controller.Body.linearVelocity : Vector3.zero;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool acting = Time.time < actionUntil;
            string movement = controller.IsKnockedOut ? "Hit_Reaction"
                : acting && fullBodyAction ? actionClip
                : !controller.Grounded ? "Jump_Preview"
                : speed > 2.5f ? "Run_InPlace" : speed > .2f ? "Walk_InPlace" : "Idle";
            locomotion.Set(movement, false, controller.IsKnockedOut ? .25f : .15f);
            var moving = locomotion.Playable;
            if (movement is "Walk_InPlace" or "Run_InPlace")
                moving.SetSpeed(CyclesPerSecond(movement == "Run_InPlace", speed) * moving.GetAnimationClip().length);
            else if (controller.IsKnockedOut)
            {
                float end = moving.GetAnimationClip().length * .95f;
                if (moving.GetTime() >= end) { moving.SetTime(end); moving.SetSpeed(0); }
            }
            else moving.SetSpeed(1);
            bool crafting = controller.Summoner && controller.Summoner.IsCrafting && clips.ContainsKey("Inspect_OneHand");

            string pose = controller.IsKnockedOut ? null
                : acting && !fullBodyAction ? actionClip
                : acting && fullBodyAction ? null
                : blocking ? "Block_Plate"
                : crafting && controller.Grounded ? "Inspect_OneHand"
                : holding ? (controller.Combat.Weapon && controller.Combat.Weapon.Definition?.IsTwoHanded != true
                    ? "Hold_OneHand" : "Carry_TwoHand") : null;
            if (pose != null) upperBody?.Set(pose);
            if (upperBody != null && upperBody.Playable.IsValid())
                upperBody.Playable.SetSpeed(acting && !fullBodyAction
                    ? upperBody.Playable.GetAnimationClip().length / actionDuration : 1f);
            float dt = Time.deltaTime;
            poseWeight = Mathf.MoveTowards(poseWeight, pose == null ? 0f : 1f, dt * 10f);
            if (layers.IsValid()) layers.SetInputWeight(1, poseWeight);
        }

        void LateUpdate()
        {
            if (!initialized || !controller) return;
            Posture();
            float useProgress = actionDuration > 0f ? Mathf.Clamp01((Time.time - actionStarted) / actionDuration) : 0f;
            Quaternion itemRotation = Quaternion.identity;
            bool contact = itemContact != null && itemContact.Apply(controller, itemUsePose, eating, useProgress, out itemRotation);
            // Animated mitten transforms retain the existing combat sockets and miniature item grip offsets.
            if (gripL && controller.handL) controller.handL.SetPositionAndRotation(gripL.position, gripL.rotation * gripCorrectionL);
            if (gripR && controller.handR) controller.handR.SetPositionAndRotation(gripR.position, gripR.rotation * gripCorrectionR);
            if (contact && controller.Combat && controller.Combat.Weapon)
                controller.Combat.Weapon.SetPoseRotation(Quaternion.Inverse(controller.handR.rotation) * itemRotation);
            if (!face) return;
            float now = Time.time;
            if (now > blinkAt + .18f) blinkAt = now + 2.8f + controller.Index * .23f;
            float blink = now < blinkAt ? 0f : Mathf.Sin(Mathf.Clamp01((now - blinkAt) / .18f) * Mathf.PI) * 100f;
            if (blinkShape >= 0) face.SetBlendShapeWeight(blinkShape, controller.IsKnockedOut ? 78f : blink);
            bool exerting = controller.IsDodging || controller.IsStaggered ||
                (controller.Combat && (controller.Combat.IsChanneling || controller.Combat.IsBlocking));
            expression = Mathf.MoveTowards(expression, exerting ? 0f : 55f, Time.deltaTime * 180f);
            if (browShape >= 0) face.SetBlendShapeWeight(browShape, expression);
        }

        void Posture()
        {
            if (postureBones == null || postureBones.Length == 0) return;
            for (int i = 0; i < postureBones.Length; i++)
            {
                var bone = postureBones[i];
                if (bone.localRotation == postureWritten[i]) bone.localRotation = postureBase[i];
                postureBase[i] = bone.localRotation;
            }
            float dt = Time.deltaTime;
            var velocity = controller.Body ? controller.Body.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            var accel = dt > 0f ? (velocity - lastVelocity) / dt : Vector3.zero;
            lastVelocity = velocity;
            var forward = controller.visual ? World.Flat(controller.visual.forward).normalized : Vector3.forward;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            var side = Vector3.Cross(Vector3.up, forward);
            bool live = !controller.IsDowned && !controller.IsEliminated;
            float targetLean = live ? Mathf.Clamp(Vector3.Dot(accel, forward) * .012f + velocity.magnitude * .02f, -.12f, .2f) : 0f;
            if (live && controller.BoostLeft > 0f && velocity.sqrMagnitude > .5f) targetLean += .06f;
            if (live && controller.Combat && controller.Combat.IsBlocking) targetLean += .05f;
            if (live && Time.time < blockReactionUntil)
                targetLean -= Mathf.Sin((1f - (blockReactionUntil - Time.time) / .22f) * Mathf.PI) * .15f;
            float targetBank = live ? Mathf.Clamp(-Vector3.Dot(accel, side) * .01f, -.12f, .12f) : 0f;
            float k = 1f - Mathf.Exp(-8f * dt);
            lean = Mathf.Lerp(lean, targetLean, k);
            bank = Mathf.Lerp(bank, targetBank, k);
            for (int i = 0; i < postureBones.Length; i++)
            {
                var bone = postureBones[i];
                float share = bone.name == "head" ? -.35f : bone.name == "chest" ? .4f : .6f;
                var tilt = Quaternion.AngleAxis(lean * share * Mathf.Rad2Deg, side)
                    * Quaternion.AngleAxis(bank * share * Mathf.Rad2Deg, forward);
                bone.rotation = tilt * bone.rotation;
                if (itemUsePose && eating && bone.name == "head")
                {
                    float progress = Mathf.Clamp01((Time.time - actionStarted) / actionDuration);
                    float bite = Mathf.Sin(progress * Mathf.PI) * Mathf.Sin(progress * Mathf.PI * 4f) * 5f;
                    bone.localRotation *= Quaternion.Euler(bite, 0f, 0f);
                }
                postureWritten[i] = bone.localRotation;
            }
        }

        static AvatarMask CreateUpperBodyMask(Transform animatorRoot)
        {
            var bones = animatorRoot.GetComponentsInChildren<Transform>(true);
            var mask = new AvatarMask { name = "Wreckabulary upper body", transformCount = bones.Length };
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                string path = "";
                bool upper = false;
                for (var at = bone; at && at != animatorRoot; at = at.parent)
                {
                    path = path.Length == 0 ? at.name : at.name + "/" + path;
                    if (at.name == "spine") upper = true;
                }
                mask.SetTransformPath(i, path);
                mask.SetTransformActive(i, upper);
            }
            return mask;
        }

        /// <summary>Each masked layer owns two clips and blends during graph evaluation, including manual evaluation.</summary>
        sealed class MotionTrack
        {
            readonly PlayableGraph graph;
            readonly Dictionary<string, AnimationClip> clips;
            readonly AnimationClipPlayable[] playing = new AnimationClipPlayable[2];
            readonly ScriptPlayable<Crossfade> fader;
            int active;
            public AnimationMixerPlayable Mixer { get; }
            public string Current { get; private set; }
            public AnimationClipPlayable Playable => playing[active];

            public MotionTrack(PlayableGraph graph, Dictionary<string, AnimationClip> clips)
            {
                this.graph = graph;
                this.clips = clips;
                Mixer = AnimationMixerPlayable.Create(graph, 2);
                fader = ScriptPlayable<Crossfade>.Create(graph);
                fader.GetBehaviour().Mixer = Mixer;
                ScriptPlayableOutput.Create(graph, "Layer crossfade").SetSourcePlayable(fader);
            }

            public void Set(string name, bool restart = false, float fade = .15f)
            {
                if ((!restart && Current == name) || !clips.TryGetValue(name, out var clip)) return;
                int next = 1 - active;
                if (playing[next].IsValid())
                {
                    Mixer.DisconnectInput(next);
                    graph.DestroyPlayable(playing[next]);
                }
                playing[next] = AnimationClipPlayable.Create(graph, clip);
                playing[next].SetApplyFootIK(false);
                playing[next].SetTime(0);
                playing[next].SetSpeed(1);
                graph.Connect(playing[next], 0, Mixer, next);
                active = next;
                Current = name;
                var crossfade = fader.GetBehaviour();
                crossfade.Active = active;
                crossfade.Duration = fade;
                crossfade.Apply();
            }
        }

        sealed class Crossfade : PlayableBehaviour
        {
            public AnimationMixerPlayable Mixer;
            public int Active;
            public float Duration;
            public override void PrepareFrame(Playable playable, FrameData info) => Apply();
            public void Apply()
            {
                if (!Mixer.IsValid()) return;
                var current = Mixer.GetInput(Active);
                var previous = Mixer.GetInput(1 - Active);
                float weight = previous.IsValid() && current.IsValid() && Duration > 0f
                    ? Mathf.Clamp01((float)current.GetTime() / Duration) : 1f;
                Mixer.SetInputWeight(Active, weight);
                Mixer.SetInputWeight(1 - Active, previous.IsValid() ? 1f - weight : 0f);
            }
        }

        void OnDestroy()
        {
            if (controller)
            {
                controller.Jumped -= OnJumped;
                controller.Dodged -= OnDodged;
                if (controller.Health) controller.Health.Damaged -= OnDamaged;
                if (controller.Summoner) controller.Summoner.Summoned -= OnSummoned;
            }
            if (graph.IsValid()) graph.Destroy();
            if (upperBodyMask) Destroy(upperBodyMask);
        }
    }
}
