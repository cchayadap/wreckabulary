using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Editor
{
    internal sealed class WordboundSoloPreviewStage : IDisposable
    {
        internal readonly IWordboundPuzzleContent Content;
        internal readonly PreviewRenderUtility Preview;
        internal readonly Vector3 Center;
        internal readonly WordboundCourseWorld World;
        readonly GameObject root;
        readonly Scene scene;
        readonly List<WordboundVisualInstance> visuals = new List<WordboundVisualInstance>();
        bool disposed;
        internal WordboundSoloPreviewStage(WordboundSoloPreviewSettings settings, string map)
        {
            settings.Validate();
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            if (!data || !data.items || !data.rules || !launch || !launch.VisualProfile) throw new InvalidOperationException("Installed Solo inputs are missing.");
            Content = WordboundLauncher.CreateSoloContent(data.items.text, data.rules.text, settings.Mode, settings.Seed, map);
            var pose = settings.Pose(map);
            Center = Metres(Content.Course.Geometry.Single(m => m.Id == pose.Module).Position);
            try
            {
                Preview = new PreviewRenderUtility(); scene = Preview.camera.gameObject.scene;
                if (!EditorSceneManager.IsPreviewScene(scene)) throw new InvalidOperationException("Expected an owned preview scene.");
                root = EditorUtility.CreateGameObjectWithHideFlags("Installed Solo thumbnail " + map, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(root, scene);
                World = WordboundCourseWorld.Build(Content.Course, Content.Geometry, root.transform);
                var resolver = new WordboundVisualResolver(Content.Items, profile: launch.VisualProfile);
                foreach (var placement in Content.Course.Placements)
                {
                    var definition = Content.Geometry.Assets[placement.AssetId];
                    var anchor = new GameObject("Preview prop " + placement.Id).transform;
                    anchor.SetParent(root.transform, false); anchor.localPosition = Metres(placement.Position);
                    anchor.localRotation = Quaternion.Euler(0, placement.Yaw, 0);
                    visuals.Add(resolver.Create(placement.AssetId, new Vector3(definition.WidthMm, definition.HeightMm, definition.DepthMm) / 1000f, anchor));
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                Preview.camera.aspect = 16f / 9; Preview.camera.nearClipPlane = .08f; Preview.camera.farClipPlane = 180;
                Preview.camera.clearFlags = CameraClearFlags.SolidColor; Preview.camera.backgroundColor = new Color(.14f, .19f, .22f);
                Preview.camera.allowHDR = false; Preview.camera.allowMSAA = true;
                Preview.lights[0].intensity = 1.2f; Preview.lights[0].transform.rotation = Quaternion.Euler(35, 25, 0);
                Preview.lights[1].intensity = .7f; Preview.lights[1].transform.rotation = Quaternion.Euler(340, 220, 0);
                foreach (var light in Preview.lights) { light.color = Color.white; light.shadows = LightShadows.None; }
                Preview.ambientColor = Color.gray;
                SetPose(pose.Position, pose.Target, pose.FieldOfView);
            }
            catch { Dispose(); throw; }
        }
        internal void SetPose(Vector3 position, Vector3 target, float fov)
        {
            Preview.camera.fieldOfView = fov;
            Preview.camera.transform.SetPositionAndRotation(Center + position, Quaternion.LookRotation(target - position, Vector3.up));
        }
        internal void RequireCameraOutsideSurface()
        {
            var position = Preview.camera.transform.position;
            if (World.Surfaces.Cast<BoxCollider>().Any(box => {
                var p = box.transform.InverseTransformPoint(position) - box.center; var half = box.size * .5f;
                return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
            })) throw new InvalidOperationException("The preview camera is inside a canonical wall, floor or ceiling. Move it before capture.");
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            var errors = new List<string>();
            void Clean(Action action) { try { action(); } catch (Exception e) { errors.Add(e.ToString()); } }
            Clean(() => { if (root) TactileMaterials.Release(root); });
            foreach (var visual in visuals) Clean(visual.Dispose);
            visuals.Clear();
            Clean(() => { if (World) World.Dispose(); });
            Clean(() => { if (root) Object.DestroyImmediate(root); });
            Clean(() => Preview?.Cleanup());
            Clean(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); });
            if (errors.Count != 0) throw new InvalidOperationException("Solo preview cleanup failed: " + string.Join("\n", errors));
        }
        static Vector3 Metres(CoursePoint p) => new Vector3(p.X, p.Y, p.Z) / 1000f;
    }
}
