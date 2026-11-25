using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class LobbyStage : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(0f, 0f, -400f);
        const string AvatarKey = "Avatar/Avatar";
        [Tooltip("Optional saved framing override. Otherwise UI/LobbyCamera is loaded. Read once per stage; restart Play after editing.")]
        public LobbyCameraProfile CameraProfile;
        bool cameraProfileLoaded;
        float cameraDistance = LobbyCameraProfile.DefaultDistance, cameraEyeHeight = LobbyCameraProfile.DefaultEyeHeight;
        float cameraLookHeight = LobbyCameraProfile.DefaultLookHeight, cameraFieldOfView = LobbyCameraProfile.DefaultFieldOfView;

        public enum Focus { Centre, Left }

        public static readonly Color SkyTop = LobbyKit.Hex(0xf4e2bd);
        static readonly Color SkyMiddle = LobbyKit.Hex(0xf8eccf), SkyLow = LobbyKit.Hex(0xfff4de);
        public const float SideAt = .3f;
        const float PreviewMargin = .015f, PreviewShare = .96f, SmallestPreview = .5f;
        public const int LookPriority = 10;
        static readonly Vector3 KeyFrom = new Vector3(-1.6f, 2.3f, -2.3f), FillFrom = new Vector3(1.6f, 1.4f, -1.8f), RimFrom = new Vector3(.5f, 2.2f, 1.4f);

        sealed class ThemeMote
        {
            public RawImage image;
            public float seed;
            public Color colour;
        }

        sealed class Guest
        {
            public Transform root;
            public PlayableGraph graph;
            public RawImage shadow;
            public SkinnedMeshRenderer[] meshes;
            public bool friend;
        }

        readonly List<Guest> guests = new();
        int partySize = 1, partyFriends;
        bool partyVisible = true;
        int Standing => Mathf.Min(LobbyMenu.PartyMax, partySize + partyFriends);
        public int PresentedPartySize => partyVisible ? Standing : 1;
        public int PartyFriends => Standing - partySize;
        public bool PartyShown => IsPresenting && charactersVisible && partyVisible && avatarRoot;

        Transform set, avatarRoot;
        Transform itemPreview;
        Renderer itemStand;
        Bounds itemPreviewBounds;
        string previewWord, previewSkin;
        Canvas sky;
        Texture2D skyTexture, softTexture;
        RawImage backdrop, contactShadow;
        RectTransform effectLayer;
        readonly ThemeMote[] motes = new ThemeMote[14];
        LobbyRoom room;
        LobbyWreck wreck;
        Volume look;
        VolumeProfile lookProfile;
        Light key, fill, rim;
        GameObject avatar;
        SkinnedMeshRenderer[] meshes;
        PlayableGraph graph;
        AnimationMixerPlayable animationMixer;
        AnimationClipPlayable emotePlayable;
        const float EmoteFade = .14f;
        float emoteDuration;
        const string RightGripBone = "grip_R", UpperBodyBone = "spine";
        const string HoldOneHandClip = "Hold_OneHand", CarryTwoHandClip = "Carry_TwoHand";
        const int BodyLayer = 0, HoldLayer = 1;
        const float HoldBlendPerSecond = 8f;
        Transform rightGrip, handAnchor, heldItem;
        Quaternion gripCorrection = Quaternion.identity;
        string heldWord, heldSkin;
        AnimationPlayableOutput avatarOutput;
        AnimationLayerMixerPlayable holdLayers;
        AnimationClipPlayable holdPlayable;
        AnimationClip holdClip;
        AvatarMask holdMask;
        float holdWeight;
        Camera cam;
        CameraRig rig;
        bool rigWasEnabled, cameraTaken;
        Vector3 savedPosition; Quaternion savedRotation; bool savedOrtho; float savedFov, savedNear;
        CameraClearFlags savedClear; Color savedBackground;
        Vector3 spot, toCamera;
        float yaw, targetYaw, side, targetSide, panel;
        float imageAspect = -1f;
        bool artwork = true, themeEffects = true, charactersVisible = true;

        public string Map { get; private set; }
        public Transform Avatar => avatarRoot;
        public float Yaw => Mathf.Repeat(targetYaw, 360f);
        public Vector3 Spot => spot;
        public Camera Camera => cam;
        public bool ArtworkShown => artwork;
        public bool CharactersVisible => charactersVisible;
        public bool ThemeEffectsEnabled => themeEffects;
        public Transform ItemPreview => itemPreview;
        public Transform HeldItem => heldItem;
        public bool IsHoldingItem => heldItem;
        public LobbyRoom Room => room;
        public LobbyWreck Wreck => wreck;
        public float VisibleRight => cam && panel > 0f && targetSide > .5f ? Mathf.Clamp01(1f - panel / cam.aspect) : 1f;
        public bool IsPresenting => cameraTaken && isActiveAndEnabled;
        public bool IsEmoting => graph.IsValid() && emotePlayable.IsValid();

        void OnEnable() => LobbyThemes.Changed += ApplyTheme;
        void OnDisable() { LobbyThemes.Changed -= ApplyTheme; Release(); }

        public void ShowArtwork(bool show)
        {
            artwork = show;
            if (backdrop) { backdrop.texture = skyTexture; imageAspect = -1f; }
            if (contactShadow) contactShadow.enabled = charactersVisible;
            if (effectLayer) effectLayer.gameObject.SetActive(show && themeEffects);
            if (lookProfile && lookProfile.TryGet<DepthOfField>(out var depth))
                depth.mode.Override(show ? DepthOfFieldMode.Off : DepthOfFieldMode.Gaussian);
            Place(true);
        }

        public void SetThemeEffects(bool enabled)
        {
            themeEffects = enabled;
            if (effectLayer) effectLayer.gameObject.SetActive(artwork && enabled);
        }

        public bool PreviewItem(string word, string skin)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            word = word.ToUpperInvariant();
            if (!GameConfig.Current.Items.TryGet(word, out var item) || !item.HasSkin(skin)) return false;
            var library = ModelLibrary.Load();
            if (!library || !library.Find("Items/" + word)) return false;
            if (itemPreview && previewWord == word && previewSkin == skin) return true;
            ClearItemPreview();
            ClearHeldItem();
            itemPreview = new GameObject("Lobby item preview").transform;
            itemPreview.SetParent(transform, false);
            var pivot = new GameObject("Preview model").transform;
            pivot.SetParent(itemPreview, false);
            var model = ModelVisual.Spawn("Items/" + word, pivot, skin);
            if (!model) { ClearItemPreview(); return false; }
            var bounds = ModelVisual.BoundsIn(pivot, model);
            float scale = .7f / Mathf.Max(.01f, bounds.size.x, bounds.size.y, bounds.size.z);
            pivot.localScale = Vector3.one * scale;
            pivot.localPosition = new Vector3(-bounds.center.x * scale, .12f - bounds.min.y * scale, -bounds.center.z * scale);
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stand.name = "Item display stand";
            stand.transform.SetParent(itemPreview, false);
            stand.transform.localPosition = Vector3.up * .055f;
            stand.transform.localScale = new Vector3(.7f, .055f, .7f);
            stand.GetComponent<Collider>().enabled = false;
            Destroy(stand.GetComponent<Collider>());
            itemStand = stand.GetComponent<Renderer>();
            itemStand.sharedMaterial = GameAssets.I.Tinted(LobbyThemes.Current.Surface);
            itemPreviewBounds = ModelVisual.BoundsIn(itemPreview, itemPreview.gameObject);
            previewWord = word; previewSkin = skin;
            Place(true);
            return true;
        }

        public void ClearItemPreview()
        {
            if (itemPreview)
            {
                itemPreview.gameObject.SetActive(false);
                TactileMaterials.Release(itemPreview.gameObject);
                Destroy(itemPreview.gameObject);
            }
            itemPreview = null; itemStand = null; previewWord = previewSkin = null;
        }

        public bool HoldItem(string word, string skin)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            word = word.ToUpperInvariant();
            if (!GameConfig.Current.Items.TryGet(word, out var item) || !item.HasSkin(skin) || string.IsNullOrEmpty(item.Model)) return false;
            var library = ModelLibrary.Load();
            var source = library ? library.Find(item.Model) : null;
            if (!source || !avatar || !rightGrip) return false;
            if (heldItem && heldWord == word && heldSkin == skin) return true;
            ClearHeldItem();
            ClearItemPreview();
            if (!handAnchor)
            {
                handAnchor = new GameObject("Lobby hand anchor").transform;
                handAnchor.SetParent(avatarRoot, false);
            }
            heldItem = new GameObject("Lobby held item").transform;
            heldItem.SetParent(handAnchor, false);
            var model = ModelVisual.Spawn(item.Model, heldItem, skin);
            if (!model) { ClearHeldItem(); return false; }
            SeatHeldModel(item, model.transform, source.transform);
            var turn = HeldItemPose.Turn(item);
            heldItem.localScale = Vector3.one * item.HeldScale;
            heldItem.SetLocalPositionAndRotation(-(turn * HeldItemPose.Grip(item)) * item.HeldScale, turn);
            heldWord = word; heldSkin = skin;
            PlayHoldPose(library.FindClip(AvatarKey, item.IsTwoHanded ? CarryTwoHandClip : HoldOneHandClip));
            FollowHand();
            return true;
        }

        public void ClearHeldItem()
        {
            if (heldItem)
            {
                heldItem.gameObject.SetActive(false);
                TactileMaterials.Release(heldItem.gameObject);
                Destroy(heldItem.gameObject);
            }
            heldItem = null; heldWord = heldSkin = null;
        }

        static void SeatHeldModel(ItemDefinition item, Transform model, Transform source)
        {
            var hold = ItemHolds.For(item.Id);
            if (hold == null || hold.ModelYaw == 0f && hold.ModelShift == null) return;
            var turn = Quaternion.Euler(0f, hold.ModelYaw, 0f);
            var shift = hold.ModelShift == null ? Vector3.zero : new Vector3(hold.ModelShift[0], hold.ModelShift[1], hold.ModelShift[2]);
            model.SetLocalPositionAndRotation(turn * source.localPosition + shift, turn * source.localRotation);
        }

        void FollowHand()
        {
            if (!heldItem || !rightGrip) return;
            handAnchor.SetPositionAndRotation(rightGrip.position, rightGrip.rotation * gripCorrection);
        }

        void PlayHoldPose(AnimationClip clip)
        {
            if (!graph.IsValid() || !clip) return;
            if (!holdLayers.IsValid()) BuildHoldLayer();
            if (holdPlayable.IsValid() && holdClip == clip) return;
            if (holdPlayable.IsValid())
            {
                graph.Disconnect(holdLayers, HoldLayer);
                graph.DestroyPlayable(holdPlayable);
            }
            holdPlayable = AnimationClipPlayable.Create(graph, clip);
            holdPlayable.SetApplyFootIK(false);
            holdClip = clip;
            graph.Connect(holdPlayable, 0, holdLayers, HoldLayer);
            holdLayers.SetInputWeight(HoldLayer, holdWeight);
        }

        void BuildHoldLayer()
        {
            holdLayers = AnimationLayerMixerPlayable.Create(graph, 2);
            avatarOutput.SetSourcePlayable(holdLayers);
            graph.Connect(animationMixer, 0, holdLayers, BodyLayer);
            holdLayers.SetInputWeight(BodyLayer, 1f);
            holdMask = UpperBodyMask(avatar.GetComponentInChildren<Animator>(true).transform);
            holdLayers.SetLayerMaskFromAvatarMask(HoldLayer, holdMask);
        }

        static AvatarMask UpperBodyMask(Transform animatorRoot)
        {
            var bones = animatorRoot.GetComponentsInChildren<Transform>(true);
            var mask = new AvatarMask { name = "Lobby upper body", transformCount = bones.Length };
            for (int i = 0; i < bones.Length; i++)
            {
                string path = "";
                bool upper = false;
                for (var at = bones[i]; at && at != animatorRoot; at = at.parent)
                {
                    path = path.Length == 0 ? at.name : at.name + "/" + path;
                    if (at.name == UpperBodyBone) upper = true;
                }
                mask.SetTransformPath(i, path);
                mask.SetTransformActive(i, upper);
            }
            return mask;
        }

        void StepHoldPose()
        {
            if (!holdLayers.IsValid()) return;
            float target = heldItem && holdPlayable.IsValid() && !IsEmoting ? 1f : 0f;
            if (holdWeight == target) return;
            holdWeight = Mathf.MoveTowards(holdWeight, target, HoldBlendPerSecond * Time.unscaledDeltaTime);
            holdLayers.SetInputWeight(HoldLayer, holdWeight);
        }

        void DropHoldPose()
        {
            holdWeight = 0f;
            if (holdLayers.IsValid()) holdLayers.SetInputWeight(HoldLayer, 0f);
        }

        public void Show(string mapId, Outfit outfit)
        {
            TakeCamera();
            SetMap(mapId);
            Dress(outfit);
        }

        public void SetMap(string mapId)
        {
            LoadCameraProfile();
            if (string.IsNullOrEmpty(mapId) || !GameConfig.Current.Houses.ContainsKey(mapId))
                foreach (var key in GameConfig.Current.Houses.Keys) { mapId = key; break; }
            Map = mapId;
            if (set) return;
            set = new GameObject("Lobby backdrop · " + LobbyRoom.RoomName).transform;
            set.SetParent(transform, false);
            room = LobbyRoom.Build(set);
            room.Dress(LobbyThemes.Current);
            wreck = set.gameObject.AddComponent<LobbyWreck>();
            wreck.Init(room, this);
            toCamera = new Vector3(0f, 0f, -cameraDistance);
            set.position = Origin;
            spot = Origin;
            if (avatarRoot) avatarRoot.position = spot;
            ShowArtwork(artwork);
            Place(true);
        }

        public void Dress(Outfit outfit)
        {
            StopEmote();
            if (!avatar && !SpawnAvatar()) return;
            PlayerAppearance.Dress(meshes, outfit);
        }

        public void SetPartyVisible(bool visible)
        {
            partyVisible = visible;
            if (visible) ClearHeldItem();
            PlaceParty();
        }

        public void SetCharactersVisible(bool visible)
        {
            charactersVisible = visible;
            if (!visible) { StopEmote(); ClearHeldItem(); }
            if (avatarRoot) avatarRoot.gameObject.SetActive(visible);
            if (graph.IsValid())
            {
                if (visible) graph.Play(); else graph.Stop();
            }
            if (contactShadow) contactShadow.enabled = visible;
            PlaceParty();
        }

        public void SetPartySize(int count)
        {
            partySize = Mathf.Clamp(count, 1, LobbyMenu.PartyMax);
            SeatGuests();
        }

        public void SetPartyFriends(int count)
        {
            count = Mathf.Clamp(count, 0, LobbyMenu.PartyMax - 1);
            if (count == partyFriends) return;
            partyFriends = count;
            SeatGuests();
        }

        public Vector3 PartyPoint(float metres) => spot + PartyRight * metres;

        public bool PartyFoot(int place, out Vector3 foot)
        {
            var root = place == 0 ? avatarRoot : place > 0 && place <= guests.Count ? guests[place - 1].root : null;
            bool standing = PartyShown && root && root.gameObject.activeSelf && place < PresentedPartySize;
            foot = standing ? root.position : spot;
            return standing;
        }

        Vector3 PartyRight => Vector3.Cross(Vector3.up, -toCamera.normalized);

        static void DressGuest(Guest guest, int seat, bool friend)
        {
            var colour = GameAssets.I.PlayerColor(seat);
            guest.friend = friend;
            PlayerAppearance.Dress(guest.meshes, friend
                ? PlayerAppearance.SeatDefault(GameConfig.Current.Wardrobe, seat, colour)
                : PlayerAppearance.PresentationOutfit(seat, colour));
        }

        void SeatGuests()
        {
            while (guests.Count < Standing - 1)
            {
                int seat = guests.Count + 1;
                var root = new GameObject("Lobby guest " + (seat + 1)).transform;
                root.SetParent(transform, false);
                var model = ModelVisual.Spawn(AvatarKey, root);
                if (!model) { Destroy(root.gameObject); break; }
                var guest = new Guest { root = root, meshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true) };
                DressGuest(guest, seat, seat >= partySize);
                var animator = model.GetComponentInChildren<Animator>(true);
                var idle = ModelLibrary.Load().FindClip(AvatarKey, "Idle");
                if (animator && idle)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    guest.graph = PlayableGraph.Create(root.name);
                    guest.graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
                    var clip = AnimationClipPlayable.Create(guest.graph, idle);
                    clip.SetTime(seat * .27d);
                    AnimationPlayableOutput.Create(guest.graph, root.name, animator).SetSourcePlayable(clip);
                }
                guest.shadow = LobbyKit.Rect(sky.transform, root.name + " contact shadow").gameObject.AddComponent<RawImage>();
                guest.shadow.texture = softTexture;
                guest.shadow.color = contactShadow.color;
                guest.shadow.raycastTarget = false;
                guest.shadow.transform.SetSiblingIndex(contactShadow.transform.GetSiblingIndex() + 1);
                guests.Add(guest);
            }
            for (int i = 0; i < guests.Count && i < Standing - 1; i++)
            {
                bool friend = i + 1 >= partySize;
                if (guests[i].friend != friend) DressGuest(guests[i], i + 1, friend);
            }
            PlaceParty();
        }

        void PlaceParty()
        {
            if (!avatarRoot) return;
            int count = PresentedPartySize;
            float scale = PartyPlates.Scale(count);
            var right = PartyRight;
            var rotation = Quaternion.LookRotation(toCamera.sqrMagnitude > 0 ? toCamera.normalized : Vector3.back) * Quaternion.Euler(0f, yaw, 0f);
            avatarRoot.SetPositionAndRotation(spot + right * PartyOffset(0, count), rotation);
            avatarRoot.localScale = Vector3.one * scale;
            for (int i = 0; i < guests.Count; i++)
            {
                var guest = guests[i];
                bool show = IsPresenting && charactersVisible && partyVisible && i < Standing - 1;
                if (guest.root.gameObject.activeSelf != show) guest.root.gameObject.SetActive(show);
                if (guest.graph.IsValid() && guest.graph.IsPlaying() != show)
                {
                    if (show) guest.graph.Play(); else guest.graph.Stop();
                }
                guest.shadow.enabled = show;
                if (!show) continue;
                guest.root.SetPositionAndRotation(spot + right * PartyOffset(i + 1, count), rotation);
                guest.root.localScale = Vector3.one * scale;
            }
        }

        static float PartyOffset(int seat, int count) => PartyPlates.Offset(seat, count);

        bool SpawnAvatar()
        {
            var library = ModelLibrary.Load();
            if (!library || !library.Find(AvatarKey)) return false;
            avatarRoot = new GameObject("Lobby avatar").transform;
            avatarRoot.SetParent(transform, false);
            avatarRoot.position = spot;
            avatar = ModelVisual.Spawn(AvatarKey, avatarRoot);
            if (!avatar) return false;
            meshes = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            rightGrip = ModelVisual.FindNamed(avatar, RightGripBone);
            if (rightGrip) gripCorrection = Quaternion.Inverse(rightGrip.rotation) * avatarRoot.rotation;
            var animator = avatar.GetComponentInChildren<Animator>(true);
            var idle = library.FindClip(AvatarKey, "Idle");
            if (animator && idle)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Lobby avatar");
                graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
                animationMixer = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(AnimationClipPlayable.Create(graph, idle), 0, animationMixer, 0);
                animationMixer.SetInputWeight(0, 1f);
                avatarOutput = AnimationPlayableOutput.Create(graph, "Lobby avatar", animator);
                avatarOutput.SetSourcePlayable(animationMixer);
                if (charactersVisible) graph.Play();
            }
            avatarRoot.gameObject.SetActive(charactersVisible);
            Place(true);
            return true;
        }

        public bool PlayEmote(AnimationClip clip)
        {
            if (!IsPresenting || !charactersVisible || !graph.IsValid() || !clip || clip.legacy || clip.isHumanMotion ||
                clip.length <= 0f || float.IsNaN(clip.length) || float.IsInfinity(clip.length)) return false;
            StopEmote();
            emotePlayable = AnimationClipPlayable.Create(graph, clip);
            emotePlayable.SetApplyFootIK(false);
            emotePlayable.SetTime(0d);
            emotePlayable.SetDuration(clip.length);
            graph.Connect(emotePlayable, 0, animationMixer, 1);
            animationMixer.SetInputWeight(1, 0f);
            emoteDuration = clip.length;
            return true;
        }

        public void StopEmote()
        {
            if (!graph.IsValid() || !emotePlayable.IsValid()) return;
            animationMixer.SetInputWeight(1, 0f);
            animationMixer.SetInputWeight(0, 1f);
            graph.Disconnect(animationMixer, 1);
            graph.DestroyPlayable(emotePlayable);
            emotePlayable = default;
            emoteDuration = 0f;
        }

        void Update()
        {
            StepEmote();
            StepHoldPose();
        }

        void StepEmote()
        {
            if (!IsEmoting) return;
            if (!IsPresenting) { StopEmote(); return; }
            float time = (float)emotePlayable.GetTime();
            if (time >= emoteDuration) { StopEmote(); return; }
            float fade = Mathf.Min(EmoteFade, emoteDuration * .5f);
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(time, emoteDuration - time) / fade));
            animationMixer.SetInputWeight(0, 1f - weight);
            animationMixer.SetInputWeight(1, weight);
        }

        public void Turn(float degrees) => targetYaw += degrees;

        void LoadCameraProfile()
        {
            if (cameraProfileLoaded) return;
            cameraProfileLoaded = true;
            var profile = CameraProfile ? CameraProfile : Resources.Load<LobbyCameraProfile>(LobbyCameraProfile.ResourcePath);
            if (!profile) return;
            if (!profile.TryValidate(out string reason))
            {
                Debug.LogWarning("Lobby camera profile is invalid; using the existing default framing. " + reason, this);
                return;
            }
            cameraDistance = profile.Distance;
            cameraEyeHeight = profile.EyeHeight;
            cameraLookHeight = profile.LookHeight;
            cameraFieldOfView = profile.FieldOfView;
        }

        public void Frame(Focus focus, float panel = 0f)
        {
            targetSide = focus == Focus.Left ? 1f : 0f;
            if (focus == Focus.Left) this.panel = panel;
        }

        public static float StandAt(float panel, float aspect) =>
            panel > 0f && aspect > 0f ? Mathf.Clamp(.5f - panel / (2f * aspect), .15f, .45f) : SideAt;

        public void TakeCamera()
        {
            LoadCameraProfile();
            if (cameraTaken) return;
            cam = Camera.main;
            if (!cam) return;
            rig = cam.GetComponent<CameraRig>();
            rigWasEnabled = rig && rig.enabled;
            if (rig) rig.enabled = false;
            var t = cam.transform;
            savedPosition = t.position; savedRotation = t.rotation; savedOrtho = cam.orthographic;
            savedFov = cam.fieldOfView; savedNear = cam.nearClipPlane; savedClear = cam.clearFlags; savedBackground = cam.backgroundColor;
            cam.orthographic = false;
            cam.fieldOfView = cameraFieldOfView;
            cam.nearClipPlane = .1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = LobbyThemes.Current.Surface;
            GraphicsOptions.ApplyTo(cam);
            cameraTaken = true;
            Showroom(true);
        }

        void Showroom(bool on)
        {
            if (on && !sky) BuildShowroom();
            if (sky)
            {
                sky.worldCamera = cam;
                if (cam) sky.planeDistance = Mathf.Min(cam.farClipPlane - 1f, 80f);
                sky.enabled = on;
            }
            if (look) look.enabled = on;
            if (key) key.enabled = on;
            if (fill) fill.enabled = on;
            if (rim) rim.enabled = on;
        }

        void BuildShowroom()
        {
            var canvasObject = new GameObject("Lobby sky", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            sky = canvasObject.AddComponent<Canvas>();
            sky.renderMode = RenderMode.ScreenSpaceCamera;
            sky.sortingOrder = -100;
            backdrop = LobbyKit.Rect(canvasObject.transform, "Theme artwork").Fill().gameObject.AddComponent<RawImage>();
            skyTexture = SkyTexture();
            backdrop.raycastTarget = false;
            softTexture = SoftTexture();
            contactShadow = LobbyKit.Rect(canvasObject.transform, "Avatar contact shadow").gameObject.AddComponent<RawImage>();
            contactShadow.texture = softTexture;
            contactShadow.color = new Color(.19f, .12f, .07f, .23f);
            contactShadow.raycastTarget = false;
            effectLayer = LobbyKit.Rect(canvasObject.transform, "Theme atmosphere").Fill();
            for (int i = 0; i < motes.Length; i++)
            {
                var image = LobbyKit.Rect(effectLayer, "Ambient mote " + i).gameObject.AddComponent<RawImage>();
                image.raycastTarget = false;
                motes[i] = new ThemeMote { image = image, seed = (i + 1f) * .618034f };
            }

            var volumeObject = new GameObject("Lobby look");
            volumeObject.transform.SetParent(transform, false);
            look = volumeObject.AddComponent<Volume>();
            look.isGlobal = true;
            look.priority = LookPriority;
            lookProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            lookProfile.name = "Lobby look";
            var vignette = lookProfile.Add<Vignette>(true);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0f);
            vignette.smoothness.Override(.45f);
            var focus = lookProfile.Add<DepthOfField>(true);
            focus.mode.Override(DepthOfFieldMode.Gaussian);
            focus.gaussianStart.Override(5.5f);
            focus.gaussianEnd.Override(14f);
            var grade = lookProfile.Add<ColorAdjustments>(true);
            grade.contrast.Override(2f);
            grade.saturation.Override(3f);
            grade.postExposure.Override(.1f);
            var bloom = lookProfile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(.12f);
            look.sharedProfile = lookProfile;

            key = ShowroomLight("Key light", LobbyKit.Hex(0xfff3dc), 7f, 65f);
            fill = ShowroomLight("Fill light", LobbyKit.Hex(0xfff3e6), 4f, 72f);
            rim = ShowroomLight("Rim light", LobbyKit.Hex(0xffefd0), 3f, 48f);
            ApplyTheme();
        }

        void ApplyTheme()
        {
            var theme = LobbyThemes.Current;
            if (cam && cameraTaken) cam.backgroundColor = theme.Surface;
            if (rim) rim.color = Color.Lerp(Color.white, theme.Secondary, .22f);
            if (itemStand) itemStand.sharedMaterial = GameAssets.I.Tinted(theme.Surface);
            if (room) room.Dress(theme);
            for (int i = 0; i < motes.Length; i++)
            {
                var mote = motes[i];
                if (mote == null) continue;
                bool confetti = theme.Id == "candy";
                mote.image.texture = confetti ? Texture2D.whiteTexture : softTexture;
                mote.colour = i % 3 == 0 ? theme.Secondary : Color.Lerp(theme.Accent, Color.white, .55f);
                mote.colour.a = confetti ? .38f : .2f;
                mote.image.color = mote.colour;
                float size = confetti ? 5f + i % 4 : 9f + i % 5;
                mote.image.rectTransform.sizeDelta = confetti ? new Vector2(size, size * .5f) : Vector2.one * size;
            }
            ShowArtwork(artwork);
        }

        static Texture2D SoftTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Lobby soft contact and dust", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f).magnitude;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        void PlaceArtwork()
        {
            if (!cam || !backdrop) return;
            if (imageAspect != cam.aspect)
            {
                imageAspect = cam.aspect;
                backdrop.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
            if (contactShadow.enabled) PlaceShadow(contactShadow, avatarRoot ? avatarRoot : null);
            foreach (var guest in guests)
                if (guest.shadow.enabled) PlaceShadow(guest.shadow, guest.root);
            if (!themeEffects) return;
            bool confetti = LobbyThemes.Current.Id == "candy";
            float time = Time.unscaledTime;
            foreach (var mote in motes)
            {
                float phase = mote.seed + time * (confetti ? -.027f : .012f);
                float x = Mathf.Repeat(mote.seed * 1.73f, 1f) + Mathf.Sin(time * .25f + mote.seed) * .018f;
                float y = Mathf.Repeat(phase, 1f);
                var rt = mote.image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(x, y);
                rt.anchoredPosition = Vector2.zero;
                if (confetti) rt.localRotation = Quaternion.Euler(0f, 0f, time * 15f + mote.seed * 90f);
                var colour = mote.colour;
                colour.a *= Mathf.Clamp01(Mathf.Min(y, 1f - y) * 8f);
                mote.image.color = colour;
            }
        }

        void PlaceShadow(RawImage image, Transform avatarTransform)
        {
            var foot = cam.WorldToViewportPoint(avatarTransform ? avatarTransform.position : spot);
            var shadow = image.rectTransform;
            shadow.anchorMin = shadow.anchorMax = new Vector2(foot.x, foot.y);
            shadow.anchoredPosition = Vector2.zero;
            float pixelsPerMetre = cam.pixelHeight / (2f * Mathf.Max(.1f, foot.z) * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad));
            shadow.sizeDelta = new Vector2(.95f, .17f) * pixelsPerMetre * (avatarTransform ? avatarTransform.localScale.x : 1f);
        }

        Light ShowroomLight(string name, Color colour, float intensity, float angle)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.type = LightType.Spot;
            light.color = colour;
            light.intensity = intensity;
            light.range = 8f;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * .6f;
            light.shadows = LightShadows.None;
            return light;
        }

        static Texture2D SkyTexture()
        {
            const int Texels = 128;
            var texture = new Texture2D(1, Texels, TextureFormat.RGBA32, false)
            { name = "Lobby sky", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[Texels];
            for (int i = 0; i < Texels; i++)
            {
                float down = 1f - (i + .5f) / Texels;
                Color colour = down < .55f
                    ? Color.Lerp(SkyTop, SkyMiddle, Mathf.SmoothStep(0f, 1f, down / .55f))
                    : Color.Lerp(SkyMiddle, SkyLow, Mathf.SmoothStep(0f, 1f, (down - .55f) / .45f));
                pixels[i] = colour;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public void Release()
        {
            StopEmote();
            ClearItemPreview();
            ClearHeldItem();
            DropHoldPose();
            if (!cameraTaken) return;
            if (cam)
            {
                cam.transform.SetPositionAndRotation(savedPosition, savedRotation);
                cam.orthographic = savedOrtho; cam.fieldOfView = savedFov; cam.nearClipPlane = savedNear;
                cam.clearFlags = savedClear; cam.backgroundColor = savedBackground;
            }
            if (rig) rig.enabled = rigWasEnabled;
            cameraTaken = false;
            PlaceParty();
            Showroom(false);
        }

        void LateUpdate() => Place(false);

        void Place(bool snap)
        {
            float k = snap ? 1f : 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            side = Mathf.Lerp(side, targetSide, snap ? 1f : 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
            PlaceParty();
            FollowHand();
            if (itemPreview)
            {
                var rightOfAvatar = Vector3.Cross(Vector3.up, -toCamera.normalized);
                itemPreview.SetPositionAndRotation(spot + rightOfAvatar * .8f + toCamera.normalized * .08f,
                    Quaternion.Euler(0f, 180f + yaw * .35f, 0f));
            }
            Aim(key, KeyFrom);
            Aim(fill, FillFrom);
            Aim(rim, RimFrom);
            if (!cameraTaken || !cam) return;
            var right = Vector3.Cross(Vector3.up, -toCamera.normalized);
            float halfWidth = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * cam.aspect;
            var shift = right * side * ((1f - 2f * StandAt(panel, cam.aspect)) * toCamera.magnitude * halfWidth);
            var eye = spot + toCamera + Vector3.up * cameraEyeHeight + shift;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(spot + Vector3.up * cameraLookHeight + shift - eye));
            FitItemPreview();
            PlaceArtwork();
        }

        void FitItemPreview()
        {
            if (!itemPreview) return;
            if (itemPreview.localScale != Vector3.one) itemPreview.localScale = Vector3.one;
            if (targetSide <= 0f || panel <= 0f) return;
            float rightLimit = 1f - panel / cam.aspect - PreviewMargin;
            float widthAtUnitDepth = 2f * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * cam.aspect;
            float room = (rightLimit - PreviewMargin) * PreviewShare, span = PreviewSpan();
            if (span > room && room > 0f) itemPreview.localScale = Vector3.one * Mathf.Max(SmallestPreview, room / span);
            float moveLeft = 0f, moveRight = 0f;
            for (int i = 0; i < 8; i++)
            {
                var projected = cam.WorldToViewportPoint(PreviewCorner(i));
                if (projected.z <= 0f) continue;
                moveLeft = Mathf.Max(moveLeft, (projected.x - rightLimit) * widthAtUnitDepth * projected.z);
                moveRight = Mathf.Max(moveRight, (PreviewMargin - projected.x) * widthAtUnitDepth * projected.z);
            }
            float shift = moveLeft > 0f && moveRight > 0f ? (moveLeft - moveRight) * .5f : moveLeft - moveRight;
            itemPreview.position -= cam.transform.right * shift;
        }

        Vector3 PreviewCorner(int i)
        {
            var sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
            return itemPreview.TransformPoint(itemPreviewBounds.center + Vector3.Scale(itemPreviewBounds.extents, sign));
        }

        float PreviewSpan()
        {
            float left = float.MaxValue, right = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var projected = cam.WorldToViewportPoint(PreviewCorner(i));
                if (projected.z <= 0f) continue;
                left = Mathf.Min(left, projected.x);
                right = Mathf.Max(right, projected.x);
            }
            return right > left ? right - left : 0f;
        }

        void Aim(Light light, Vector3 from)
        {
            if (!light) return;
            var forward = toCamera.sqrMagnitude > 0 ? -toCamera.normalized : Vector3.forward;
            var across = Vector3.Cross(Vector3.up, forward);
            var at = spot + across * from.x + Vector3.up * from.y + forward * from.z;
            light.transform.SetPositionAndRotation(at, Quaternion.LookRotation(spot + Vector3.up * .6f - at));
        }

        void OnDestroy()
        {
            Release();
            if (graph.IsValid()) graph.Destroy();
            foreach (var guest in guests) if (guest.graph.IsValid()) guest.graph.Destroy();
            TactileMaterials.Release(gameObject);
            if (lookProfile) Destroy(lookProfile);
            if (holdMask) Destroy(holdMask);
            if (skyTexture) Destroy(skyTexture);
            if (softTexture) Destroy(softTexture);
        }
    }
}
