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
        const float Distance = 4.2f, EyeHeight = .95f, LookHeight = .5f, FieldOfView = 30f;

        public enum Focus { Centre, Left }

        public static readonly Color SkyTop = LobbyKit.Hex(0xf4e2bd);
        static readonly Color SkyMiddle = LobbyKit.Hex(0xf8eccf), SkyLow = LobbyKit.Hex(0xfff4de);
        public const float SideAt = .3f;
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
        }

        readonly List<Guest> guests = new();
        int partySize = 1;
        bool partyVisible = true;
        public int PresentedPartySize => partyVisible ? partySize : 1;

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
        readonly List<(Renderer renderer, bool hidden)> mapRenderers = new();
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
        Camera cam;
        CameraRig rig;
        bool rigWasEnabled, cameraTaken;
        Vector3 savedPosition; Quaternion savedRotation; bool savedOrtho; float savedFov, savedNear;
        CameraClearFlags savedClear; Color savedBackground;
        Vector3 spot, toCamera;
        float yaw, targetYaw, side, targetSide, panel;
        float imageAspect = -1f;
        bool artwork = true, themeEffects = true;

        public string Map { get; private set; }
        public Transform Avatar => avatarRoot;
        public float Yaw => Mathf.Repeat(targetYaw, 360f);
        public Vector3 Spot => spot;
        public Camera Camera => cam;
        public bool ArtworkShown => artwork;
        public bool ThemeEffectsEnabled => themeEffects;
        public Transform ItemPreview => itemPreview;
        public bool IsPresenting => cameraTaken && isActiveAndEnabled;
        public bool IsEmoting => graph.IsValid() && emotePlayable.IsValid();

        void OnEnable() => LobbyThemes.Changed += ApplyTheme;
        void OnDisable() { LobbyThemes.Changed -= ApplyTheme; Release(); }

        /// <summary>The map remains built for selection and inspection; artwork hides only its rendered content.</summary>
        public void ShowArtwork(bool show)
        {
            artwork = show;
            foreach (var entry in mapRenderers)
                if (entry.renderer) entry.renderer.forceRenderingOff = show || entry.hidden;
            if (backdrop) { backdrop.texture = show && LobbyThemes.Current.Background ? LobbyThemes.Current.Background : skyTexture; imageAspect = -1f; }
            if (contactShadow) contactShadow.enabled = show;
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

        public void Show(string mapId, Outfit outfit)
        {
            TakeCamera();
            SetMap(mapId);
            Dress(outfit);
        }

        public void SetMap(string mapId)
        {
            if (Map == mapId && set) return;
            var layout = GameConfig.Current.HouseFor(mapId);
            if (set) { set.gameObject.SetActive(false); TactileMaterials.Release(set.gameObject); Destroy(set.gameObject); }
            set = new GameObject("Lobby backdrop · " + layout.Name).transform;
            set.SetParent(transform, false);
            var geometry = RoomBuilder.CreateGeometry(layout, mapId, set);
            var room = OpenRoom(layout);
            int storey = layout.StoreyOf(room);
            foreach (Transform group in geometry)
                for (int above = storey + 1; above < layout.StoreyFloors().Count; above++)
                    if (group.name == RoomBuilder.StoreyName(above)) group.gameObject.SetActive(false);
            foreach (var child in geometry.GetComponentsInChildren<Transform>(true))
            {
                var p = set.InverseTransformPoint(child.position);
                if (child.name == "Rug" && Mathf.Abs(p.y - room.FloorY) < 1f && p.x > room.MinX && p.x < room.MaxX && p.z > room.MinZ && p.z < room.MaxZ)
                { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }
            float depth = room.MaxZ - room.MinZ;
            float distance = Mathf.Min(Distance, depth * .62f);
            var local = new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY, Mathf.Min(room.MaxZ - 1.6f, room.MinZ + .45f + distance + depth * .12f));
            toCamera = new Vector3(0f, 0f, -distance);
            Furnish(layout, storey, local, local + toCamera);
            mapRenderers.Clear();
            foreach (var renderer in set.GetComponentsInChildren<Renderer>(true)) mapRenderers.Add((renderer, renderer.forceRenderingOff));
            set.position = Origin;
            spot = Origin + local;
            Map = mapId;
            if (avatarRoot) avatarRoot.position = spot;
            ShowArtwork(artwork);
            Place(true);
        }

        static RoomBox OpenRoom(HouseLayout layout) =>
            (layout.LobbyRoom != null ? layout.Room(layout.LobbyRoom) : null)
            ?? layout.Rooms.Where(r => layout.StoreyOf(r) == 0)
                .OrderByDescending(r => Mathf.Min(r.MaxX - r.MinX, r.MaxZ - r.MinZ)).ThenBy(r => r.Name, System.StringComparer.Ordinal).First();

        void Furnish(HouseLayout layout, int storey, Vector3 stand, Vector3 eye)
        {
            var library = ModelLibrary.Load();
            if (!library) return;
            var decor = new GameObject("Furniture").transform;
            decor.SetParent(set, false);
            foreach (var f in layout.Furniture)
            {
                if (layout.StoreyOf(layout.Room(f.Room)) > storey) continue;
                var at = new Vector3(f.X, layout.Room(f.Room).FloorY + f.Y, f.Z);
                float clear = f.Word == "RUG" ? 2.2f : 1.1f;
                if (DistanceToSegment(new Vector2(at.x, at.z), new Vector2(eye.x, eye.z), new Vector2(stand.x, stand.z)) < clear) continue;
                string key = f.Word == "RUG" ? "Environment/Round_Rug" : "Items/" + f.Word.ToUpperInvariant();
                if (!library.Find(key)) continue;
                var root = new GameObject(f.Word).transform;
                root.SetParent(decor, false);
                root.SetPositionAndRotation(at, Quaternion.Euler(0f, f.Yaw, 0f));
                ModelVisual.Spawn(key, root);
            }
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
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
            PlaceParty();
        }

        public void SetPartySize(int count)
        {
            partySize = Mathf.Clamp(count, 1, LobbyMenu.PartyMax);
            while (guests.Count < partySize - 1)
            {
                int seat = guests.Count + 1;
                var root = new GameObject("Lobby guest " + (seat + 1)).transform;
                root.SetParent(transform, false);
                var model = ModelVisual.Spawn(AvatarKey, root);
                if (!model) { Destroy(root.gameObject); break; }
                var guest = new Guest { root = root };
                PlayerAppearance.Dress(model.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                    PlayerAppearance.PresentationOutfit(seat, GameAssets.I.PlayerColor(seat)));
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
            PlaceParty();
        }

        void PlaceParty()
        {
            if (!avatarRoot) return;
            int count = PresentedPartySize;
            float scale = count == 4 ? .82f : count == 3 ? .90f : 1f;
            var right = Vector3.Cross(Vector3.up, -toCamera.normalized);
            var rotation = Quaternion.LookRotation(toCamera.sqrMagnitude > 0 ? toCamera.normalized : Vector3.back) * Quaternion.Euler(0f, yaw, 0f);
            avatarRoot.SetPositionAndRotation(spot + right * PartyOffset(0, count), rotation);
            avatarRoot.localScale = Vector3.one * scale;
            for (int i = 0; i < guests.Count; i++)
            {
                var guest = guests[i];
                bool show = IsPresenting && partyVisible && i < partySize - 1;
                if (guest.root.gameObject.activeSelf != show) guest.root.gameObject.SetActive(show);
                if (guest.graph.IsValid() && guest.graph.IsPlaying() != show)
                {
                    if (show) guest.graph.Play(); else guest.graph.Stop();
                }
                guest.shadow.enabled = show && artwork;
                if (!show) continue;
                guest.root.SetPositionAndRotation(spot + right * PartyOffset(i + 1, count), rotation);
                guest.root.localScale = Vector3.one * scale;
            }
        }

        static float PartyOffset(int seat, int count) => count switch
        {
            2 => seat == 0 ? -.48f : .48f,
            3 => seat == 0 ? 0f : seat == 1 ? -.86f : .86f,
            4 => seat == 0 ? -.41f : seat == 1 ? -1.23f : seat == 2 ? .41f : 1.23f,
            _ => 0f,
        };

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
                var output = AnimationPlayableOutput.Create(graph, "Lobby avatar", animator);
                output.SetSourcePlayable(animationMixer);
                graph.Play();
            }
            Place(true);
            return true;
        }

        /// <summary>Plays a cosmetic Generic clip once, using unscaled lobby time and no root motion.</summary>
        public bool PlayEmote(AnimationClip clip)
        {
            if (!IsPresenting || !graph.IsValid() || !clip || clip.legacy || clip.isHumanMotion ||
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

        public void Frame(Focus focus, float panel = 0f)
        {
            targetSide = focus == Focus.Left ? 1f : 0f;
            if (focus == Focus.Left) this.panel = panel;
        }

        public static float StandAt(float panel, float aspect) =>
            panel > 0f && aspect > 0f ? Mathf.Clamp(.5f - panel / (2f * aspect), .15f, .45f) : SideAt;

        public void TakeCamera()
        {
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
            cam.fieldOfView = FieldOfView;
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
                float source = backdrop.texture ? (float)backdrop.texture.width / backdrop.texture.height : cam.aspect;
                if (!artwork) backdrop.uvRect = new Rect(0f, 0f, 1f, 1f);
                else if (source > cam.aspect)
                {
                    float width = cam.aspect / source;
                    backdrop.uvRect = new Rect((1f - width) * .5f, 0f, width, 1f);
                }
                else
                {
                    float height = source / cam.aspect;
                    backdrop.uvRect = new Rect(0f, (1f - height) * .35f, 1f, height);
                }
            }
            if (!artwork) return;
            PlaceShadow(contactShadow, avatarRoot ? avatarRoot : null);
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
            var eye = spot + toCamera + Vector3.up * EyeHeight + shift;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(spot + Vector3.up * LookHeight + shift - eye));
            FitItemPreview();
            PlaceArtwork();
        }

        void FitItemPreview()
        {
            if (!itemPreview || targetSide <= 0f || panel <= 0f) return;
            float rightLimit = 1f - panel / cam.aspect - .015f;
            float widthAtUnitDepth = 2f * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * cam.aspect;
            float moveLeft = 0f;
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                var world = itemPreview.TransformPoint(itemPreviewBounds.center + Vector3.Scale(itemPreviewBounds.extents, sign));
                var projected = cam.WorldToViewportPoint(world);
                if (projected.z > 0f)
                    moveLeft = Mathf.Max(moveLeft, (projected.x - rightLimit) * widthAtUnitDepth * projected.z);
            }
            itemPreview.position -= cam.transform.right * moveLeft;
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
            if (skyTexture) Destroy(skyTexture);
            if (softTexture) Destroy(softTexture);
        }
    }
}
