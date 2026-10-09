using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wreckabulary
{
    /// <summary>Visibility is temporary to one render, including nested secondary camera requests.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class CameraCutaway : MonoBehaviour
    {
        static readonly Dictionary<Camera, CameraCutaway> cameras = new();
        static readonly List<RenderScope> scopes = new();
        static readonly Stack<RenderScope> pool = new();
        static readonly Stack<VisibilityScope> pipelineScopes = new();
        static readonly Dictionary<Renderer, CameraCutawayRenderer> prepared = new();
        static readonly List<CameraCutawayRenderer> releasing = new();
        static long nextToken;
        static bool subscribed;
        Camera lens;
        UnityEngine.Object owner;
        readonly List<PlayerController> participants = new(4);
        IReadOnlyList<PlayerController> suppliedParticipants;
        IReadOnlyCollection<Renderer> occluders;
        public StoreyCutaway World { get; private set; }
        public PlayerController FollowedPlayer { get; private set; }
        public bool IsOverview => !FollowedPlayer;
        public StoreyCutaway.VisibilityPlan Visibility { get; } = new();
        public static int PreparedRendererCount => prepared.Count;

        void Awake() => lens = GetComponent<Camera>();

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!lens) lens = GetComponent<Camera>();
            cameras[lens] = this;
            if (subscribed) return;
            RenderPipelineManager.beginCameraRendering += BeginRender;
            RenderPipelineManager.endCameraRendering += EndRender;
            subscribed = true;
        }

        void OnDisable()
        {
            RestoreAll();
            if (lens) cameras.Remove(lens);
            owner = null;
            Visibility.Clear();
            if (cameras.Count == 0) { Unsubscribe(); ReleaseRenderers(); }
        }

        public void SetView(UnityEngine.Object owner, StoreyCutaway world, PlayerController followed,
            IReadOnlyList<PlayerController> overviewParticipants = null)
        {
            if (World != world) Visibility.Reset();
            this.owner = owner;
            World = world;
            FollowedPlayer = followed;
            suppliedParticipants = overviewParticipants;
        }

        public void ClearView(UnityEngine.Object source)
        {
            if (owner != source) return;
            owner = null;
            occluders = null;
            Visibility.Clear();
        }

        public void SetOccluders(IReadOnlyCollection<Renderer> renderers) => occluders = renderers;

        public void RefreshVisibility()
        {
            Visibility.Clear();
            if (!owner || !isActiveAndEnabled) return;
            participants.Clear();
            if (FollowedPlayer) participants.Add(FollowedPlayer);
            else if (suppliedParticipants != null)
            {
                foreach (var player in suppliedParticipants)
                    if (player && player.isActiveAndEnabled && !player.IsEliminated) participants.Add(player);
            }
            else if (World) World.CollectParticipants(participants);
            else foreach (var player in Wreckabulary.World.Players)
                if (player && player.isActiveAndEnabled && player.gameObject.scene == gameObject.scene &&
                    !player.IsEliminated && player.Binding is not BotBinding) participants.Add(player);
            if (World) World.Evaluate(lens, participants, IsOverview, Visibility);
            else EvaluateStandalone();
            if (occluders != null)
                foreach (var renderer in occluders)
                    if (renderer && !Visibility.Hidden.Contains(renderer)) Visibility.Shadows.Add(renderer);
            foreach (var renderer in Visibility.Hidden) PrepareRenderer(renderer);
            foreach (var renderer in Visibility.Shadows) PrepareRenderer(renderer);
        }

        static void PrepareRenderer(Renderer renderer)
        {
            if (!Application.isPlaying || renderer is not MeshRenderer || prepared.ContainsKey(renderer)) return;
            if (renderer.TryGetComponent<CameraCutawayRenderer>(out _)) return;
            var callbacks = renderer.gameObject.AddComponent<CameraCutawayRenderer>();
            callbacks.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
            prepared.Add(renderer, callbacks);
        }

        static void ReleaseRenderers()
        {
            releasing.Clear();
            releasing.AddRange(prepared.Values);
            prepared.Clear();
            foreach (var component in releasing)
                if (component)
                {
                    if (Application.isPlaying) Destroy(component);
                    else DestroyImmediate(component);
                }
            releasing.Clear();
        }

        internal static void UnregisterRenderer(Renderer renderer, CameraCutawayRenderer component)
        {
            if (!ReferenceEquals(renderer, null) && prepared.TryGetValue(renderer, out var current) && ReferenceEquals(current, component))
                prepared.Remove(renderer);
        }

        public bool WouldHide(Renderer renderer) => owner && Visibility.WouldHide(renderer);
        public bool IsVisible(Renderer renderer) => renderer && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy && !WouldHide(renderer);

        void EvaluateStandalone()
        {
            foreach (var surface in CutawaySurface.Active)
            {
                if (!surface || surface.gameObject.scene != gameObject.scene) continue;
                foreach (var renderer in surface.Renderers)
                    if (renderer && (IsOverview || StoreyCutaway.Obstructs(lens, renderer.bounds, participants)))
                        Visibility.Shadows.Add(renderer);
            }
        }

        public float ProbeCeilings(Vector3 from, Vector3 direction, float reach, float radius)
        {
            if (World) return World.ProbeCeilings(from, direction, reach, radius);
            float available = reach;
            foreach (var surface in CutawaySurface.Active)
            {
                if (!surface || surface.gameObject.scene != gameObject.scene || surface.Kind == CutawayKind.UpperWall) continue;
                foreach (var renderer in surface.Renderers)
                    available = StoreyCutaway.ProbeCeiling(renderer, from, direction, available, radius);
            }
            return available;
        }

        /// <summary>Used by pipeline callbacks and deterministic render-scope regression tests.</summary>
        public static VisibilityScope BeginCameraVisibility(Camera camera)
        {
            if (scopes.Count > 0) scopes[^1].Restore();
            var scope = pool.Count > 0 ? pool.Pop() : new RenderScope();
            scope.token = ++nextToken;
            scopes.Add(scope);
            if (camera && camera.cameraType == CameraType.Game && cameras.TryGetValue(camera, out var view) && view && view.owner)
            {
                view.RefreshVisibility();
                foreach (var renderer in view.Visibility.Hidden) scope.Add(renderer, true);
                foreach (var renderer in view.Visibility.Shadows)
                    if (!view.Visibility.Hidden.Contains(renderer)) scope.Add(renderer, false);
            }
            scope.Apply();
            return new VisibilityScope(scope.token);
        }

        static void BeginRender(ScriptableRenderContext _, Camera camera) => pipelineScopes.Push(BeginCameraVisibility(camera));
        static void EndRender(ScriptableRenderContext _, Camera camera)
        {
            if (pipelineScopes.Count > 0) pipelineScopes.Pop().Dispose();
        }

        static void RestoreAll()
        {
            if (scopes.Count > 0) scopes[^1].Restore();
            foreach (var scope in scopes) { scope.states.Clear(); scope.active = false; }
            scopes.Clear();
            pipelineScopes.Clear();
        }

        public readonly struct VisibilityScope : IDisposable
        {
            readonly long token;
            internal VisibilityScope(long token) => this.token = token;
            public void Dispose()
            {
                for (int i = scopes.Count - 1; i >= 0; i--)
                    if (scopes[i].token == token) { scopes[i].Dispose(); return; }
            }
        }

        static void Unsubscribe()
        {
            if (!subscribed) return;
            RenderPipelineManager.beginCameraRendering -= BeginRender;
            RenderPipelineManager.endCameraRendering -= EndRender;
            subscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            RestoreAll();
            Unsubscribe();
            cameras.Clear();
            pool.Clear();
            ReleaseRenderers();
        }

        readonly struct RendererState
        {
            readonly Renderer renderer;
            readonly bool forceOff, hide;
            readonly ShadowCastingMode shadows;
            public RendererState(Renderer renderer, bool hide)
            {
                this.renderer = renderer;
                this.hide = hide;
                forceOff = renderer.forceRenderingOff;
                shadows = renderer.shadowCastingMode;
            }
            public void Apply()
            {
                if (!renderer) return;
                if (hide || shadows == ShadowCastingMode.Off) renderer.forceRenderingOff = true;
                else renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            public void Restore()
            {
                if (!renderer) return;
                renderer.forceRenderingOff = forceOff;
                renderer.shadowCastingMode = shadows;
            }
        }

        sealed class RenderScope : IDisposable
        {
            public readonly List<RendererState> states = new(128);
            public long token;
            public bool active;
            public void Add(Renderer renderer, bool hide)
            {
                if (renderer) states.Add(new RendererState(renderer, hide));
            }
            public void Apply() { active = true; foreach (var state in states) state.Apply(); }
            public void Restore() { foreach (var state in states) state.Restore(); }
            public void Dispose()
            {
                if (!active) return;
                if (scopes.Count == 0 || scopes[^1] != this) { RestoreAll(); return; }
                Restore();
                scopes.RemoveAt(scopes.Count - 1);
                active = false;
                states.Clear();
                pool.Push(this);
                if (scopes.Count > 0) scopes[^1].Apply();
            }
        }
    }
}
