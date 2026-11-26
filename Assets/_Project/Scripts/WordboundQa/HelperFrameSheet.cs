#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.IO;
using GameStudioKit.Qa;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public static class HelperFrameSheet
    {
        public const float Seconds = 4f, Zoom = .3f;
        public const int FramesPerSecond = 8, Columns = 8, CellWidth = 320, CellHeight = 180;

        public static int Frames => Mathf.RoundToInt(Seconds * FramesPerSecond);

        public static IEnumerator Record(QaContext context, string name, Action<string> failed, Action<int, float> recorded = null)
        {
            string path = context.PathInRun(name + ".png");
            if (File.Exists(path)) { failed?.Invoke("Capture never overwrites existing evidence: " + name); yield break; }
            if (Application.isBatchMode) { failed?.Invoke("A frame sheet needs a screen, not -batchmode: " + name); yield break; }
            int count = Frames, rows = (count + Columns - 1) / Columns, taken = 0;
            var sheet = new Texture2D(Columns * CellWidth, rows * CellHeight, TextureFormat.RGB24, false);
            var read = new Texture2D(CellWidth, CellHeight, TextureFormat.RGB24, false);
            var cell = RenderTexture.GetTemporary(CellWidth, CellHeight, 0);
            float first = Time.time, next = first, last = first;
            var corner = Vector2.zero;
            bool close = false;
            try
            {
                while (taken < count)
                {
                    while (Time.time < next) yield return null;
                    next += 1f / FramesPerSecond;
                    yield return new WaitForEndOfFrame();
                    var screen = ScreenCapture.CaptureScreenshotAsTexture();
                    var helpers = HelperAgent.All;
                    var camera = Camera.main;
                    if (camera && helpers.Count > 0 && helpers[helpers.Count - 1])
                    {
                        var seen = camera.WorldToViewportPoint(helpers[helpers.Count - 1].transform.position);
                        if (seen.z > 0f)
                        {
                            corner = new Vector2(Mathf.Clamp(seen.x - Zoom * .5f, 0f, 1f - Zoom), Mathf.Clamp(seen.y - Zoom * .5f, 0f, 1f - Zoom));
                            close = true;
                        }
                    }
                    if (close) Graphics.Blit(screen, cell, new Vector2(Zoom, Zoom), corner);
                    else Graphics.Blit(screen, cell);
                    Object.Destroy(screen);
                    var previous = RenderTexture.active;
                    RenderTexture.active = cell;
                    read.ReadPixels(new Rect(0, 0, CellWidth, CellHeight), 0, 0);
                    read.Apply();
                    RenderTexture.active = previous;
                    int column = taken % Columns, row = rows - 1 - taken / Columns;
                    sheet.SetPixels(column * CellWidth, row * CellHeight, CellWidth, CellHeight, read.GetPixels());
                    last = Time.time;
                    taken++;
                }
                sheet.Apply();
                File.WriteAllBytes(path, sheet.EncodeToPNG());
            }
            finally
            {
                RenderTexture.ReleaseTemporary(cell);
                Object.Destroy(read);
                Object.Destroy(sheet);
            }
            context.AddCapture(name, path, "png", "frame_sheet");
            recorded?.Invoke(taken, last - first);
        }
    }
}
#endif
