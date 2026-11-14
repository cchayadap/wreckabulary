// Game Studio Kit (MIT). PNG-sequence capture that works in built players (Unity Recorder is Editor-only):
// each frame the presented screen is copied into a RenderTexture, read back with AsyncGPUReadback, and written as
// frame_000001.png. Time.captureDeltaTime = 1/fps makes game time advance exactly one frame per capture, so slow
// encoding never drops frames. A capture.json manifest records what was written. No ffmpeg needed.
using System;
using System.Collections;
using System.IO;
using GameStudioKit.Common;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace GameStudioKit.Capture
{
    [AddComponentMenu("Game Studio Kit/In-Player Capture")]
    public sealed class InPlayerCapture : MonoBehaviour
    {
        public const int MaxPendingReadbacks = 8;

        public bool IsCapturing { get; private set; }
        public int FramesWritten => _written;
        public int FramesRequested => _requested;
        public string OutDir => _outDir;

        string _outDir;
        int _fps;
        int _maxFrames;
        int _requested;
        int _written;
        int _failed;
        int _pending;
        float _previousCaptureDelta;
        string _startedUtc;
        RenderTexture _rt;
        Coroutine _loop;

        /// <summary>Starts capturing the screen into <paramref name="outDir"/> at <paramref name="fps"/> game frames per
        /// second, for at most <paramref name="maxFrames"/> frames (0 = until End).</summary>
        public void Begin(string outDir, int fps = 30, int maxFrames = 0)
        {
            if (IsCapturing) End();
            if (Application.isBatchMode)
            {
                KitLog.Warn("InPlayerCapture: -batchmode players present no frames; nothing captured");
                return;
            }
            _outDir = Path.GetFullPath(outDir);
            Directory.CreateDirectory(_outDir);
            _fps = Mathf.Clamp(fps, 1, 240);
            _maxFrames = Mathf.Max(0, maxFrames);
            _requested = _written = _failed = _pending = 0;
            _startedUtc = KitIo.UtcNowIso();
            _previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / _fps;
            IsCapturing = true;
            _loop = StartCoroutine(Loop());
        }

        /// <summary>Stops capturing, waits for pending readbacks and writes capture.json.</summary>
        public void End()
        {
            if (!IsCapturing) return;
            IsCapturing = false;
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            AsyncGPUReadback.WaitAllRequests();
            Time.captureDeltaTime = _previousCaptureDelta;
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
            WriteManifest();
        }

        void OnDisable() => End();

        IEnumerator Loop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (IsCapturing)
            {
                yield return endOfFrame;
                if (!IsCapturing) yield break;
                CaptureFrame();
                if (_maxFrames > 0 && _requested >= _maxFrames)
                {
                    End();
                    yield break;
                }
            }
        }

        void CaptureFrame()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0) return;
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) { _rt.Release(); Destroy(_rt); }
                _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                _rt.Create();
            }
            if (_pending >= MaxPendingReadbacks) AsyncGPUReadback.WaitAllRequests();
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_rt);
            int index = ++_requested;
            bool flip = SystemInfo.graphicsUVStartsAtTop;
            _pending++;
            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, req => OnReadback(req, index, w, h, flip));
        }

        void OnReadback(AsyncGPUReadbackRequest req, int index, int w, int h, bool flip)
        {
            _pending--;
            if (req.hasError)
            {
                _failed++;
                return;
            }
            try
            {
                NativeArray<byte> data = req.GetData<byte>();
                byte[] pixels = data.ToArray();
                if (flip) FlipRows(pixels, w * 4, h);
                byte[] png = ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h);
                File.WriteAllBytes(Path.Combine(_outDir, "frame_" + index.ToString("D6") + ".png"), png);
                _written++;
            }
            catch (Exception e)
            {
                _failed++;
                KitLog.Warn("InPlayerCapture: frame " + index + " failed: " + e.Message);
            }
        }

        /// <summary>Flips an RGBA image vertically in place.</summary>
        public static void FlipRows(byte[] pixels, int rowBytes, int rows)
        {
            var tmp = new byte[rowBytes];
            for (int top = 0, bottom = rows - 1; top < bottom; top++, bottom--)
            {
                Buffer.BlockCopy(pixels, top * rowBytes, tmp, 0, rowBytes);
                Buffer.BlockCopy(pixels, bottom * rowBytes, pixels, top * rowBytes, rowBytes);
                Buffer.BlockCopy(tmp, 0, pixels, bottom * rowBytes, rowBytes);
            }
        }

        void WriteManifest()
        {
            if (string.IsNullOrEmpty(_outDir)) return;
            try
            {
                var w = new KitJsonWriter();
                w.BeginObject()
                    .Prop("schema", 1)
                    .Prop("fps", _fps)
                    .Prop("width", Screen.width)
                    .Prop("height", Screen.height)
                    .Prop("frames_requested", _requested)
                    .Prop("frames_written", _written)
                    .Prop("frames_failed", _failed)
                    .Prop("started_utc", _startedUtc)
                    .Prop("ended_utc", KitIo.UtcNowIso())
                    .EndObject();
                KitIo.WriteAllTextAtomic(Path.Combine(_outDir, "capture.json"), w + "\n");
            }
            catch (Exception e)
            {
                KitLog.Warn("InPlayerCapture: cannot write capture.json: " + e.Message);
            }
        }
    }
}
