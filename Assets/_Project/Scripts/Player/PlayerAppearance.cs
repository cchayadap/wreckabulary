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
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        readonly List<Renderer> oldRenderers = new List<Renderer>();
        PlayerController controller;
        GameObject model;
        SkinnedMeshRenderer[] meshes;
        Transform gripL, gripR;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable currentPlayable;
        string currentClip;
        float actionUntil;
        bool wasHolding;
        bool initialized;
        Outfit outfit;

        public Outfit CurrentOutfit => outfit?.Clone();
        public GameObject AvatarModel => model;
        public Transform RightGrip => gripR;

        /// <summary>Called by PlayerController.Setup, including existing serialized prefabs.</summary>
        public bool Initialize(PlayerController player)
        {
            controller = player;
            if (initialized) return true;
            var library = ModelLibrary.Load();
            if (!controller || !controller.visual || !library || !library.Find(AvatarKey)) return false;
            foreach (var renderer in controller.visual.GetComponentsInChildren<Renderer>(true))
                oldRenderers.Add(renderer);
            var root = new GameObject("ImportedAvatar");
            root.transform.SetParent(controller.visual, false);
            model = ModelVisual.Spawn(AvatarKey, root.transform);
            if (!model) { Destroy(root); return false; }
            meshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            gripL = ModelVisual.FindNamed(model, "grip_L");
            gripR = ModelVisual.FindNamed(model, "grip_R");
            foreach (var renderer in oldRenderers) if (renderer) renderer.enabled = false;

            foreach (string name in new[] { "Idle", "Walk_InPlace", "Run_InPlace", "Jump_Preview",
                "Hold_OneHand", "Carry_TwoHand", "Block_Plate", "Swing_OneHand", "Thrust_OneHand",
                "Throw_OneHand", "Hit_Reaction", "Celebrate", "Drink_Consumable", "Pickup", "Place" })
            {
                var clip = library.FindClip(AvatarKey, name);
                if (clip) clips[name] = clip;
            }
            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator && clips.Count > 0)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                graph = PlayableGraph.Create("Wreckabulary Avatar " + controller.Index);
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                mixer = AnimationMixerPlayable.Create(graph, 1);
                var output = AnimationPlayableOutput.Create(graph, "Avatar", animator);
                output.SetSourcePlayable(mixer);
                graph.Play();
            }
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
            SetClip("Idle");
            return true;
        }

        public void ApplyOutfit(Outfit next) => ApplyOutfit(next, true);

        void ApplyOutfit(Outfit next, bool save)
        {
            var catalogue = GameConfig.Current.Wardrobe;
            outfit = catalogue.Sanitize(next).Clone();
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
            if (save && controller)
            {
                PlayerPrefs.SetString("wv.outfit." + controller.Index, outfit.Serialize());
                PlayerPrefs.Save();
            }
        }

        public string SkinFor(string word) => outfit?.SkinFor(word) ?? Skin.Standard;

        public void SetItemSkin(string word, string skin)
        {
            if (outfit == null || !GameConfig.Current.Items.TryGet(word, out var definition)) return;
            outfit.ItemSkins[word] = Appearance.Pick(definition, skin);
            ApplyOutfit(outfit);
        }

        /// <summary>Action visuals never change damage windows, movement or item rules.</summary>
        public void Play(string clipName, float duration = .5f)
        {
            actionUntil = Time.time + Mathf.Max(.05f, duration);
            SetClip(clipName, true);
        }

        void OnJumped(PlayerController _) => Play("Jump_Preview", .45f);
        void OnDodged(PlayerController _) => Play("Run_InPlace", .2f);

        void LateUpdate()
        {
            if (!initialized || !controller) return;
            bool holding = controller.Combat && controller.Combat.IsHolding;
            bool blocking = controller.Combat && controller.Combat.IsBlocking;
            if (holding && !wasHolding) Play("Pickup", .28f);
            if (!holding && wasHolding) Play("Throw_OneHand", .35f);
            wasHolding = holding;
            var velocity = controller.Body ? controller.Body.linearVelocity : Vector3.zero;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            if (Time.time >= actionUntil)
            {
                string clip = controller.IsEliminated || controller.IsDowned ? "Hit_Reaction"
                    : blocking ? "Block_Plate"
                    : holding ? (controller.Combat.Weapon && controller.Combat.Weapon.Definition?.IsTwoHanded != true
                        ? "Hold_OneHand" : "Carry_TwoHand")
                    : !controller.Grounded ? "Jump_Preview"
                    : speed > 2.5f ? "Run_InPlace" : speed > .2f ? "Walk_InPlace" : "Idle";
                SetClip(clip);
            }
            if (currentPlayable.IsValid() && (currentClip == "Walk_InPlace" || currentClip == "Run_InPlace"))
                currentPlayable.SetSpeed(Mathf.Clamp(speed / (currentClip == "Run_InPlace" ? 5f : 2f), .4f, 1.6f));
            // Combat keeps its original proxy/socket contracts. Place those proxies
            // at the animated mittens after legacy wobble, without changing physics.
            if (gripL && controller.handL) controller.handL.position = gripL.position;
            if (gripR && controller.handR) controller.handR.position = gripR.position;
        }

        void SetClip(string name, bool restart = false)
        {
            if (!graph.IsValid() || (!restart && currentClip == name) || !clips.TryGetValue(name, out var clip)) return;
            if (currentPlayable.IsValid())
            {
                mixer.DisconnectInput(0);
                graph.DestroyPlayable(currentPlayable);
            }
            currentPlayable = AnimationClipPlayable.Create(graph, clip);
            currentPlayable.SetApplyFootIK(false);
            currentPlayable.SetTime(0);
            currentPlayable.SetSpeed(1);
            graph.Connect(currentPlayable, 0, mixer, 0);
            mixer.SetInputWeight(0, 1);
            currentClip = name;
        }

        void OnDestroy()
        {
            if (controller)
            {
                controller.Jumped -= OnJumped;
                controller.Dodged -= OnDodged;
            }
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
