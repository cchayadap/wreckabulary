using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(90)]
    public sealed class PlayerAppearance : MonoBehaviour
    {
        const string AvatarKey = "Avatar/Avatar";

        public const float WalkStride = .2483f, RunStride = .5176f;
        const float WalkStretch = 1.7f, RunStretch = 2f, MinCycles = .6f, MaxWalkCycles = 2.4f, MaxRunCycles = 3.6f;

        public static float CyclesPerSecond(bool run, float speed) =>
            Mathf.Clamp(speed / (run ? RunStride * RunStretch : WalkStride * WalkStretch),
                MinCycles, run ? MaxRunCycles : MaxWalkCycles);

        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        readonly List<Renderer> oldRenderers = new List<Renderer>();
        PlayerController controller;
        GameObject model;
        SkinnedMeshRenderer[] meshes;
        Transform gripL, gripR;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable currentPlayable, previousPlayable;
        ScriptPlayable<Crossfade> fader;
        string currentClip;
        float actionUntil;
        bool wasHolding;
        bool initialized;
        Outfit outfit;
        Transform[] postureBones;
        Quaternion[] postureBase, postureWritten;
        Vector3 lastVelocity;
        float lean, bank;

        sealed class Crossfade : PlayableBehaviour
        {
            public AnimationMixerPlayable Mixer;
            public float Duration = .15f;

            public override void PrepareFrame(Playable playable, FrameData info) => Apply();

            public void Apply()
            {
                if (!Mixer.IsValid()) return;
                var current = Mixer.GetInput(0);
                var previous = Mixer.GetInput(1);
                float w = previous.IsValid() && current.IsValid() && Duration > 0f
                    ? Mathf.Clamp01((float)current.GetTime() / Duration) : 1f;
                Mixer.SetInputWeight(0, w);
                Mixer.SetInputWeight(1, previous.IsValid() ? 1f - w : 0f);
            }
        }

        public Outfit CurrentOutfit => outfit?.Clone();
        public GameObject AvatarModel => model;
        public Transform RightGrip => gripR;
        public bool IsAnimationReady => initialized && graph.IsValid() && graph.IsPlaying() && currentPlayable.IsValid();
        public AnimationClip CurrentAnimationClip => currentPlayable.IsValid() ? currentPlayable.GetAnimationClip() : null;

        public bool Initialize(PlayerController player)
        {
            controller = player;
            if (initialized) return IsAnimationReady;
            var library = ModelLibrary.Load();
            if (!controller || !controller.visual || !library || !library.Find(AvatarKey)) return false;
            if (!LoadRequiredClips(library)) return false;
            oldRenderers.Clear();
            foreach (var renderer in controller.visual.GetComponentsInChildren<Renderer>(true))
                oldRenderers.Add(renderer);
            var root = new GameObject("ImportedAvatar");
            root.transform.SetParent(controller.visual, false);
            model = ModelVisual.Spawn(AvatarKey, root.transform);
            if (!model) { clips.Clear(); Destroy(root); return false; }
            meshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            gripL = ModelVisual.FindNamed(model, "grip_L");
            gripR = ModelVisual.FindNamed(model, "grip_R");
            var animator = model.GetComponentInChildren<Animator>(true);
            if (!animator)
            {
                root.SetActive(false);
                Destroy(root);
                model = null;
                meshes = null;
                gripL = gripR = null;
                clips.Clear();
                return false;
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            graph = PlayableGraph.Create("Wreckabulary Avatar " + controller.Index);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            fader = ScriptPlayable<Crossfade>.Create(graph);
            fader.GetBehaviour().Mixer = mixer;
            ScriptPlayableOutput.Create(graph, "Crossfade").SetSourcePlayable(fader);
            var output = AnimationPlayableOutput.Create(graph, "Avatar", animator);
            output.SetSourcePlayable(mixer);
            SetClip("Idle");
            graph.Play();
            graph.Evaluate(0f);
            foreach (var renderer in oldRenderers) if (renderer) renderer.enabled = false;
            initialized = true;
            var catalogue = GameConfig.Current.Wardrobe;
            string saved = PlayerPrefs.GetString("wv.outfit." + controller.Index, "");
            var initial = string.IsNullOrEmpty(saved) ? catalogue.Default.Clone() : Outfit.Deserialize(saved);
            if (string.IsNullOrEmpty(saved) && catalogue.Palettes.TryGetValue("Top", out var palette))
            {
                float nearest = float.PositiveInfinity;
                foreach (var colour in palette)
                {
                    var rgb = new Vector3(colour.R, colour.G, colour.B);
                    float distance = (rgb - new Vector3(controller.Color.r, controller.Color.g, controller.Color.b)).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    initial.Colours["Top"] = colour.Id;
                }
            }
            ApplyOutfit(catalogue.Sanitize(initial), false);
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
                            bool rib = materialName == "fabric_rib" && piece.TintMaterial == "fabric_main";
                            if (colour != null && (materialName == piece.TintMaterial || rib))
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

        public void SetItemSkin(string word, string skin)
        {
            if (outfit == null || !GameConfig.Current.Items.TryGet(word, out var definition)) return;
            outfit.ItemSkins[word] = Appearance.Pick(definition, skin);
            ApplyOutfit(outfit);
        }

        public void Play(string clipName, float duration = .5f)
        {
            actionUntil = Time.time + Mathf.Max(.05f, duration);
            SetClip(clipName, true);
        }

        void OnJumped(PlayerController _) => Play("Jump_Preview", .45f);
        void OnDodged(PlayerController _) => Play("Run_InPlace", .2f);

        void OnDamaged(PlayerHealth _, HitInfo __, HitResult result)
        {
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
            if (holding && !wasHolding) Play("Pickup", .28f);
            if (!holding && wasHolding) Play("Throw_OneHand", .35f);
            wasHolding = holding;
            var velocity = controller.Body ? controller.Body.linearVelocity : Vector3.zero;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool down = controller.IsEliminated || controller.IsDowned;
            if (Time.time >= actionUntil || down)
            {
                bool crafting = controller.Summoner && controller.Summoner.IsCrafting && clips.ContainsKey("Inspect_OneHand");
                string clip = down ? "Hit_Reaction"
                    : blocking ? "Block_Plate"
                    : crafting && controller.Grounded ? "Inspect_OneHand"
                    : holding ? (controller.Combat.Weapon && controller.Combat.Weapon.Definition?.IsTwoHanded != true
                        ? "Hold_OneHand" : "Carry_TwoHand")
                    : !controller.Grounded ? "Jump_Preview"
                    : speed > 2.5f ? "Run_InPlace" : speed > .2f ? "Walk_InPlace" : "Idle";
                SetClip(clip, false, down ? .25f : .15f);
            }
            if (!currentPlayable.IsValid()) return;
            if (currentClip == "Walk_InPlace" || currentClip == "Run_InPlace")
                currentPlayable.SetSpeed(CyclesPerSecond(currentClip == "Run_InPlace", speed)
                    * currentPlayable.GetAnimationClip().length);
            else if (down && currentClip == "Hit_Reaction")
            {
                float end = currentPlayable.GetAnimationClip().length * .95f;
                if (currentPlayable.GetTime() >= end) { currentPlayable.SetTime(end); currentPlayable.SetSpeed(0); }
            }
        }

        void LateUpdate()
        {
            if (!initialized || !controller) return;
            Posture();
            if (gripL && controller.handL) controller.handL.position = gripL.position;
            if (gripR && controller.handR) controller.handR.position = gripR.position;
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
                postureWritten[i] = bone.localRotation;
            }
        }

        void SetClip(string name, bool restart = false, float fade = .15f)
        {
            if (!graph.IsValid() || (!restart && currentClip == name) || !clips.TryGetValue(name, out var clip)) return;
            if (previousPlayable.IsValid())
            {
                mixer.DisconnectInput(1);
                graph.DestroyPlayable(previousPlayable);
            }
            if (currentPlayable.IsValid())
            {
                mixer.DisconnectInput(0);
                previousPlayable = currentPlayable;
                graph.Connect(previousPlayable, 0, mixer, 1);
            }
            currentPlayable = AnimationClipPlayable.Create(graph, clip);
            currentPlayable.SetApplyFootIK(false);
            currentPlayable.SetTime(0);
            currentPlayable.SetSpeed(1);
            graph.Connect(currentPlayable, 0, mixer, 0);
            currentClip = name;
            if (fader.IsValid())
            {
                var behaviour = fader.GetBehaviour();
                behaviour.Duration = fade;
                behaviour.Apply();
            }
            else mixer.SetInputWeight(0, 1);
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
        }
    }
}
