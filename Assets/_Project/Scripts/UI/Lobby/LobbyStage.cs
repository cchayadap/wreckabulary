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

        public static readonly Color SkyTop = LobbyKit.Hex(0x2b1736);
        static readonly Color SkyMiddle = LobbyKit.Hex(0x6b3f6e), SkyLow = LobbyKit.Hex(0xf2b57a);
        public const float SideAt = .3f;
        public const int LookPriority = 10;
        static readonly Vector3 KeyFrom = new Vector3(-1.8f, 2.6f, -2.4f), RimFrom = new Vector3(1.6f, 2.2f, 1.8f);

        Transform set, avatarRoot;
        Canvas sky;
        Texture2D skyTexture;
        Volume look;
        VolumeProfile lookProfile;
        Light key, rim;
        GameObject avatar;
        SkinnedMeshRenderer[] meshes;
        PlayableGraph graph;
        Camera cam;
        CameraRig rig;
        bool rigWasEnabled, cameraTaken;
        Vector3 savedPosition; Quaternion savedRotation; bool savedOrtho; float savedFov, savedNear;
        CameraClearFlags savedClear; Color savedBackground;
        Vector3 spot, toCamera;
        float yaw, targetYaw, side, targetSide, panel;

        public string Map { get; private set; }
        public Transform Avatar => avatarRoot;
        public float Yaw => Mathf.Repeat(targetYaw, 360f);
        public Vector3 Spot => spot;
        public Camera Camera => cam;

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
            set.position = Origin;
            spot = Origin + local;
            Map = mapId;
            if (avatarRoot) avatarRoot.position = spot;
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
            if (!avatar && !SpawnAvatar()) return;
            PlayerAppearance.Dress(meshes, outfit);
        }

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
                graph = PlayableGraph.Create("Lobby avatar");
                graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
                var output = AnimationPlayableOutput.Create(graph, "Lobby avatar", animator);
                output.SetSourcePlayable(AnimationClipPlayable.Create(graph, idle));
                graph.Play();
            }
            Place(true);
            return true;
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
            cam.backgroundColor = SkyTop;
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
            if (rim) rim.enabled = on;
        }

        void BuildShowroom()
        {
            var canvasObject = new GameObject("Lobby sky", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            sky = canvasObject.AddComponent<Canvas>();
            sky.renderMode = RenderMode.ScreenSpaceCamera;
            sky.sortingOrder = -100;
            var gradient = LobbyKit.Rect(canvasObject.transform, "Gradient").Fill().gameObject.AddComponent<RawImage>();
            gradient.texture = skyTexture = SkyTexture();
            gradient.raycastTarget = false;

            var volumeObject = new GameObject("Lobby look");
            volumeObject.transform.SetParent(transform, false);
            look = volumeObject.AddComponent<Volume>();
            look.isGlobal = true;
            look.priority = LookPriority;
            lookProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            lookProfile.name = "Lobby look";
            var vignette = lookProfile.Add<Vignette>(true);
            vignette.color.Override(LobbyKit.ScrimNavy);
            vignette.intensity.Override(.32f);
            vignette.smoothness.Override(.45f);
            var focus = lookProfile.Add<DepthOfField>(true);
            focus.mode.Override(DepthOfFieldMode.Gaussian);
            focus.gaussianStart.Override(5.5f);
            focus.gaussianEnd.Override(14f);
            var grade = lookProfile.Add<ColorAdjustments>(true);
            grade.contrast.Override(8f);
            grade.saturation.Override(12f);
            var bloom = lookProfile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(.25f);
            look.sharedProfile = lookProfile;

            key = ShowroomLight("Key light", LobbyKit.Hex(0xffd9a0), 5f, 46f);
            rim = ShowroomLight("Rim light", LobbyKit.Hex(0x6fd4ff), 4f, 30f);
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
            if (!cameraTaken) return;
            if (cam)
            {
                cam.transform.SetPositionAndRotation(savedPosition, savedRotation);
                cam.orthographic = savedOrtho; cam.fieldOfView = savedFov; cam.nearClipPlane = savedNear;
                cam.clearFlags = savedClear; cam.backgroundColor = savedBackground;
            }
            if (rig) rig.enabled = rigWasEnabled;
            cameraTaken = false;
            Showroom(false);
        }

        void LateUpdate() => Place(false);

        void Place(bool snap)
        {
            float k = snap ? 1f : 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            side = Mathf.Lerp(side, targetSide, snap ? 1f : 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
            if (avatarRoot) avatarRoot.rotation = Quaternion.LookRotation(toCamera.sqrMagnitude > 0 ? toCamera.normalized : Vector3.back) * Quaternion.Euler(0f, yaw, 0f);
            Aim(key, KeyFrom);
            Aim(rim, RimFrom);
            if (!cameraTaken || !cam) return;
            var right = Vector3.Cross(Vector3.up, -toCamera.normalized);
            float halfWidth = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * cam.aspect;
            var shift = right * side * ((1f - 2f * StandAt(panel, cam.aspect)) * toCamera.magnitude * halfWidth);
            var eye = spot + toCamera + Vector3.up * EyeHeight + shift;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(spot + Vector3.up * LookHeight + shift - eye));
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
            if (graph.IsValid()) graph.Destroy();
            Release();
            TactileMaterials.Release(gameObject);
            if (lookProfile) Destroy(lookProfile);
            if (skyTexture) Destroy(skyTexture);
        }
    }
}
