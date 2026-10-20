// Game Studio Kit (MIT). Programmatic Unity Recorder sessions (ARCHITECTURE §3.11, Recorder 5.1 API). Compiled only
// when com.unity.recorder is installed (asmdef versionDefines → GSK_RECORDER). Recorder facts this code respects
// (research_qa_harness §1.8): it works only in the Editor in Play Mode, never under -batchmode; with URP it needs a
// Tagged Camera (not ActiveCamera); run one movie recorder at a time. MP4 is encoded by the Recorder: no ffmpeg.
#if GSK_RECORDER
using System;
using System.IO;
using GameStudioKit.Common;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameStudioKit.Capture
{
    public static class KitRecorder
    {
        static RecorderController _controller;
        static RecorderControllerSettings _settings;
        static RecorderSettings _recorder;

        public static bool IsRecording => _controller != null && _controller.IsRecording();

        /// <summary>
        /// Starts an MP4 recording of the camera tagged <paramref name="cameraTag"/> to
        /// <paramref name="outputPathWithoutExtension"/>.mp4. Returns the output path.
        /// </summary>
        public static string StartMovie(string outputPathWithoutExtension, string cameraTag = "MainCamera", int width = 1920, int height = 1080,
            float fps = 30f, bool captureAudio = true, bool captureUi = false)
        {
            EnsureCanRecord();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "GSK Movie";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movie.ImageInputSettings = new CameraInputSettings
            {
                Source = ImageSource.TaggedCamera,
                CameraTag = cameraTag,
                OutputWidth = width,
                OutputHeight = height,
                CaptureUI = captureUi,
            };
            movie.CaptureAudio = captureAudio;
            movie.OutputFile = Prepare(outputPathWithoutExtension);
            Start(movie, fps);
            return movie.OutputFile + ".mp4";
        }

        /// <summary>Starts a PNG sequence (frame_&lt;Frame&gt;.png) of the tagged camera into <paramref name="outputDir"/>.</summary>
        public static string StartImageSequence(string outputDir, string cameraTag = "MainCamera", int width = 1920, int height = 1080, float fps = 30f)
        {
            EnsureCanRecord();
            var images = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            images.name = "GSK Images";
            images.Enabled = true;
            images.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            images.imageInputSettings = new CameraInputSettings
            {
                Source = ImageSource.TaggedCamera,
                CameraTag = cameraTag,
                OutputWidth = width,
                OutputHeight = height,
            };
            string dir = Path.GetFullPath(outputDir);
            Directory.CreateDirectory(dir);
            images.OutputFile = KitIo.ToPosix(Path.Combine(dir, "frame_<Frame>"));
            Start(images, fps);
            return dir;
        }

        /// <summary>Stops the current recording and releases its settings.</summary>
        public static void Stop()
        {
            if (_controller != null && _controller.IsRecording()) _controller.StopRecording();
            _controller = null;
            if (_recorder != null) Object.DestroyImmediate(_recorder);
            if (_settings != null) Object.DestroyImmediate(_settings);
            _recorder = null;
            _settings = null;
        }

        static void EnsureCanRecord()
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Unity Recorder does not record under -batchmode; launch the Editor without it");
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Unity Recorder records only in Play Mode");
            if (IsRecording)
                throw new InvalidOperationException("a kit recording is already running; call KitRecorder.Stop first");
        }

        static string Prepare(string outputPathWithoutExtension)
        {
            string full = Path.GetFullPath(outputPathWithoutExtension);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return KitIo.ToPosix(full);
        }

        static void Start(RecorderSettings recorder, float fps)
        {
            Stop();
            _settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            _settings.AddRecorderSettings(recorder);
            _settings.SetRecordModeToManual();
            _settings.FrameRate = fps;
            _recorder = recorder;
            _controller = new RecorderController(_settings);
            _controller.PrepareRecording();
            if (!_controller.StartRecording())
            {
                Stop();
                throw new InvalidOperationException("the Recorder refused to start (check the camera tag and the Recorder window for errors)");
            }
            KitLog.Info("KitRecorder: recording " + recorder.OutputFile);
        }
    }
}
#endif
