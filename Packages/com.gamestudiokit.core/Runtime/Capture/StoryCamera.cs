// Game Studio Kit (MIT). Deterministic story-camera captures for gate evidence (research_qa_harness §1.7):
// fixed pose, fixed resolution, Time.captureDeltaTime from cameras.json, warm-up frames, then a render into a
// RenderTexture encoded as PNG. Camera renders never include Screen Space Overlay canvases; use CaptureScreen for UI.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameStudioKit.Capture
{
    public static class StoryCamera
    {
        /// <summary>
        /// Captures one camera definition to <paramref name="outPath"/>. A temporary camera copies the settings of
        /// <paramref name="source"/> (default Camera.main), is placed at the defined pose, and renders once after the
        /// warm-up frames. <paramref name="onError"/> receives a message on failure; nothing is thrown.
        /// </summary>
        public static IEnumerator Capture(StoryCameraDef def, string outPath, StoryCameraSet set, Action<string> onError, Camera source = null)
        {
            if (def == null)
            {
                onError?.Invoke("no camera definition");
                yield break;
            }
            if (!string.IsNullOrEmpty(def.Scene) && !IsSceneLoaded(def.Scene))
            {
                onError?.Invoke("scene '" + def.Scene + "' is not loaded");
                yield break;
            }

            float previousCapture = Time.captureDeltaTime;
            if (set != null && set.CaptureDeltaTime > 0) Time.captureDeltaTime = (float)set.CaptureDeltaTime;
            Camera cam = null;
            string error = null;
            try
            {
                cam = CreateCamera(def, source != null ? source : Camera.main);
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            if (error == null)
            {
                int warmup = set?.WarmupFrames ?? 30;
                for (int i = 0; i < warmup; i++) yield return null;
                try
                {
                    RenderToPng(cam, def.Width, def.Height, outPath);
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
            }

            if (cam != null) Object.Destroy(cam.gameObject);
            Time.captureDeltaTime = previousCapture;
            if (error != null) onError?.Invoke(error);
        }

        /// <summary>Captures every camera of <paramref name="set"/> whose scene is loaded (or that names no scene).</summary>
        public static IEnumerator CaptureAll(StoryCameraSet set, string outDir, List<string> written, Action<string> onError = null)
        {
            if (set == null) yield break;
            foreach (var def in set.Cameras)
            {
                if (!string.IsNullOrEmpty(def.Scene) && !IsSceneLoaded(def.Scene)) continue;
                string path = Path.Combine(outDir, KitIo.SafeFileName(def.Name) + ".png");
                string err = null;
                yield return Capture(def, path, set, e => err = e);
                if (err == null) written?.Add(path);
                else onError?.Invoke(def.Name + ": " + err);
            }
        }

        /// <summary>
        /// Captures the presented frame, overlay UI included, at the end of the frame. Batchmode players present no
        /// frame, so this reports an error there instead of waiting forever.
        /// </summary>
        public static IEnumerator CaptureScreen(string outPath, Action<string> onError)
        {
            if (Application.isBatchMode)
            {
                onError?.Invoke("screen capture is unavailable in -batchmode");
                yield break;
            }
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                WritePng(tex, outPath);
            }
            catch (Exception e)
            {
                onError?.Invoke(e.Message);
            }
            finally
            {
                if (tex != null) Object.Destroy(tex);
            }
        }

        public static Camera CreateCamera(StoryCameraDef def, Camera source)
        {
            var go = new GameObject("GSK StoryCamera " + def.Name);
            var cam = go.AddComponent<Camera>();
            if (source != null) cam.CopyFrom(source);
            cam.enabled = false;
            cam.transform.SetPositionAndRotation(ToVector3(def.Position), Quaternion.Euler(ToVector3(def.RotationEuler)));
            cam.fieldOfView = (float)def.Fov;
            cam.aspect = def.Width / (float)def.Height;
            return cam;
        }

        /// <summary>Renders <paramref name="cam"/> once into an sRGB RenderTexture and writes a PNG.</summary>
        public static void RenderToPng(Camera cam, int width, int height, string outPath)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previousTarget = cam.targetTexture;
            var previousActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false);
                WritePng(tex, outPath);
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                if (tex != null) Object.Destroy(tex);
            }
        }

        public static void WritePng(Texture2D tex, string outPath)
        {
            byte[] png = tex.EncodeToPNG();
            string dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(outPath, png);
        }

        public static Vector3 ToVector3(double[] v) =>
            v == null || v.Length < 3 ? Vector3.zero : new Vector3((float)v[0], (float)v[1], (float)v[2]);

        static bool IsSceneLoaded(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && (s.name == name || s.path == name)) return true;
            }
            return false;
        }
    }
}
