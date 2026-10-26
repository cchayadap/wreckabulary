from pathlib import Path
import hashlib, json, difflib

root = Path(__file__).resolve().parents[3]
draft = Path(__file__).resolve().parent
source = 'Assets/_Project/Scripts/WordboundQa/WordboundMeleeCapture.cs'
test = 'Assets/_Project/Tests/Editor/WordboundMeleeOrientationTests.cs'
original = (root / source).read_text(encoding='utf-8-sig')
candidate = original.replace('bool disposed, subscribed, reported, readbackStarted;', 'bool disposed, subscribed, reported, readbackStarted;\n        string readbackOrientation = "unverified";\n        readonly Dictionary<string, object> orientationFacts = new Dictionary<string, object>();')
candidate = candidate.replace('Capture("baseline");', 'Capture("baseline");\n                if (error == "") CalibrateReadback();')
candidate = candidate.replace('                client.Gameplay.FrameChanged += Observe; subscribed = true;', '''                yield return new WaitForEndOfFrame();
                orientationFacts["attack_submit_unity_frame"] = Time.frameCount;
                orientationFacts["natural_frames_before_attack"] = Time.frameCount - samples[0].Frame;
                orientationFacts["attack_submit_realtime_seconds"] = Time.realtimeSinceStartupAsDouble;
                if (Time.frameCount <= samples[0].Frame) Fail("BAT calibration must drain through a later natural frame before Attack submission.");
            }
            if (error == "")
            {
                client.Gameplay.FrameChanged += Observe; subscribed = true;''')
candidate = candidate.replace('        void SaveFrame(int index)\n', '''        void CalibrateReadback()
        {
            Texture2D reference = null, buffered = null;
            var previous = RenderTexture.active;
            double began = Time.realtimeSinceStartupAsDouble;
            try
            {
                if (Command != null || samples.Count != 1 || samples[0].Phase != "baseline" || samples[0].Frame != Time.frameCount)
                    throw new QaFailException("BAT orientation calibration requires the same baseline frame before Attack submission.");
                int frame = Time.frameCount;
                reference = ScreenCapture.CaptureScreenshotAsTexture();
                if (!reference || reference.width != width || reference.height != height)
                    throw new QaFailException("The upright native baseline reference has unexpected dimensions.");
                reference.hideFlags = HideFlags.DontSave;
                RenderTexture.active = buffers[0];
                buffered = new Texture2D(width, height, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.DontSave };
                buffered.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); buffered.Apply(false, false);
                string selected = MatchOrientation(buffered.GetPixels32(), reference.GetPixels32(), width, height, out var errors);
                orientationFacts["reference_method"] = "ScreenCapture.CaptureScreenshotAsTexture at the same WaitForEndOfFrame, before Attack";
                orientationFacts["unity_frame"] = frame;
                orientationFacts["same_frame"] = frame == Time.frameCount && frame == samples[0].Frame;
                orientationFacts["before_attack"] = Command == null;
                orientationFacts["identity_rgb_mean_delta"] = errors[0]; orientationFacts["identity_rgb_max_delta"] = errors[1];
                orientationFacts["vertical_rgb_mean_delta"] = errors[2]; orientationFacts["vertical_rgb_max_delta"] = errors[3];
                orientationFacts["required_rgb_mean_delta_at_most"] = 1;
                orientationFacts["required_rgb_max_delta_at_most"] = 2;
                orientationFacts["compared_rgb_channels"] = (long)width * height * 3;
                orientationFacts["selected"] = selected;
                orientationFacts["maximum_temporary_texture_bytes"] = (long)width * height * 4 * 2;
                string name = "wordbound_original_bat_baseline_reference";
                string path = context.PathInRun(name + ".png");
                byte[] bytes = reference.EncodeToPNG();
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) stream.Write(bytes, 0, bytes.Length);
                using var hash = SHA256.Create();
                orientationFacts["reference_path"] = Path.GetFileName(path);
                orientationFacts["reference_sha256"] = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                context.AddCapture(name, path, "png", "screen");
                if (frame != Time.frameCount || Command != null || string.IsNullOrEmpty(selected))
                    throw new QaFailException("BAT readback orientation was ambiguous or did not match the same-frame upright reference within RGB tolerance; Attack was not submitted.");
                readbackOrientation = selected;
            }
            catch (Exception failure) { Fail(failure.Message); }
            finally
            {
                RenderTexture.active = previous;
                if (buffered) Object.Destroy(buffered);
                if (reference) Object.Destroy(reference);
                orientationFacts["calibration_seconds_before_attack"] = Time.realtimeSinceStartupAsDouble - began;
            }
        }

        static string MatchOrientation(Color32[] buffered, Color32[] reference, int width, int height, out double[] errors)
        {
            if (width < 1 || height < 2 || buffered == null || reference == null ||
                (long)width * height != buffered.Length || reference.Length != buffered.Length)
                throw new ArgumentException("Orientation requires two equal, complete pixel arrays with at least two rows.");
            long identitySum = 0, verticalSum = 0;
            int identityMax = 0, verticalMax = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    AccumulateRgb(buffered[index], reference[index], ref identitySum, ref identityMax);
                    AccumulateRgb(buffered[(height - 1 - y) * width + x], reference[index], ref verticalSum, ref verticalMax);
                }
            double channels = (long)width * height * 3;
            errors = new[] { identitySum / channels, identityMax, verticalSum / channels, verticalMax };
            bool identity = errors[0] <= 1 && identityMax <= 2;
            bool vertical = errors[2] <= 1 && verticalMax <= 2;
            return identity == vertical ? "" : identity ? "identity" : "vertical_rows";
        }

        static void AccumulateRgb(Color32 a, Color32 b, ref long sum, ref int maximum)
        {
            int r = Math.Abs(a.r - b.r), g = Math.Abs(a.g - b.g), blue = Math.Abs(a.b - b.b);
            sum += r + g + blue;
            maximum = Math.Max(maximum, Math.Max(r, Math.Max(g, blue)));
        }

        static void OrientRows(Color32[] pixels, int width, int height, string orientation)
        {
            if (pixels == null || width < 1 || height < 2 || (long)width * height != pixels.Length ||
                (orientation != "identity" && orientation != "vertical_rows"))
                throw new ArgumentException("A complete pixel array and measured orientation are required.");
            if (orientation == "identity") return;
            for (int y = 0; y < height / 2; y++)
                for (int x = 0; x < width; x++)
                {
                    int a = y * width + x, b = (height - 1 - y) * width + x;
                    Color32 value = pixels[a]; pixels[a] = pixels[b]; pixels[b] = value;
                }
        }

        void SaveFrame(int index)
''')
candidate = candidate.replace('var pixels = texture.GetPixels32(); var first = pixels[0];', 'var pixels = texture.GetPixels32();\n                OrientRows(pixels, width, height, readbackOrientation);\n                texture.SetPixels32(pixels); texture.Apply(false, false);\n                var first = pixels[0];')
candidate = candidate.replace('sample.Facts["width"] = width;', 'sample.Facts["readback_orientation"] = readbackOrientation;\n                sample.Facts["width"] = width;')
candidate = candidate.replace('Bounded GPU copies of presented frames at WaitForEndOfFrame; synchronous readback and PNG encoding only after capture stops.', 'Same-frame upright baseline calibration before Attack; bounded GPU copies at WaitForEndOfFrame during Attack; attack-frame readback, measured row orientation and PNG encoding only after capture stops.')
candidate = candidate.replace('["maximum_gpu_bytes"] =', '["readback_orientation"] = readbackOrientation, ["orientation_calibration"] = orientationFacts,\n                ["maximum_gpu_bytes"] =')
assert candidate != original and candidate.count('void CalibrateReadback()') == 1

tests = '''using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundMeleeOrientationTests
    {
        static readonly Color32[] Reference = {
            new Color32(10, 20, 30, 255), new Color32(40, 50, 60, 255),
            new Color32(70, 80, 90, 255), new Color32(100, 110, 120, 255),
            new Color32(130, 140, 150, 255), new Color32(160, 170, 180, 255) };

        static object Invoke(string name, params object[] args)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Wreckabulary.Qa.WordboundMeleeCapture"))
                .SingleOrDefault(t => t != null);
            Assert.IsNotNull(type, "The editor must load the existing Wordbound QA assembly.");
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return method.Invoke(null, args);
        }

        static string Select(Color32[] pixels, Color32[] reference) =>
            (string)Invoke("MatchOrientation", pixels, reference, 2, 3, null);

        [Test]
        public void UprightReferenceSelectsIdentityAndPreservesPixels()
        {
            var pixels = Reference.ToArray();
            Assert.AreEqual("identity", Select(pixels, Reference));
            Invoke("OrientRows", pixels, 2, 3, "identity");
            CollectionAssert.AreEqual(Reference, pixels);
        }

        [Test]
        public void VerticalRowsAreMeasuredAndRestoredIncludingOddMiddleRow()
        {
            var pixels = new[] { Reference[4], Reference[5], Reference[2], Reference[3], Reference[0], Reference[1] };
            Assert.AreEqual("vertical_rows", Select(pixels, Reference));
            Invoke("OrientRows", pixels, 2, 3, "vertical_rows");
            CollectionAssert.AreEqual(Reference, pixels);
        }

        [Test]
        public void SymmetricImageCannotChooseAnOrientation()
        {
            var pixels = Enumerable.Repeat(new Color32(80, 90, 100, 255), 6).ToArray();
            Assert.AreEqual("", Select(pixels, pixels));
        }

        [Test]
        public void ColorTransferMismatchCannotBeHiddenByAFlip()
        {
            var pixels = Reference.Select(p => new Color32((byte)(p.r + 10), p.g, p.b, p.a)).ToArray();
            Assert.AreEqual("", Select(pixels, Reference));
        }

        [Test]
        public void UnmeasuredOrientationAndIncompleteArraysFailClosed()
        {
            var failure = Assert.Throws<TargetInvocationException>(() => Invoke("OrientRows", Reference.ToArray(), 2, 3, "unverified"));
            Assert.IsInstanceOf<ArgumentException>(failure.InnerException);
            failure = Assert.Throws<TargetInvocationException>(() => Select(new Color32[4], Reference));
            Assert.IsInstanceOf<ArgumentException>(failure.InnerException);
        }
    }
}
'''

files = [(source, original, candidate), (test, '', tests)]
patch = ''
manifest = []
for rel, before, after in files:
    before_path, after_path = draft / 'baseline' / rel, draft / 'candidate' / rel
    after_path.parent.mkdir(parents=True, exist_ok=True)
    after_path.write_text(after, encoding='utf-8', newline='\n')
    if before:
        before_path.parent.mkdir(parents=True, exist_ok=True)
        before_path.write_text(before, encoding='utf-8', newline='\n')
    patch += f'diff --git a/{rel} b/{rel}\n'
    if not before:
        patch += 'new file mode 100644\n'
    patch += ''.join(difflib.unified_diff(before.splitlines(True), after.splitlines(True), fromfile=f'a/{rel}' if before else '/dev/null', tofile=f'b/{rel}'))
    manifest.append({'path': rel, 'baseline_sha256_lf': hashlib.sha256(before.encode()).hexdigest() if before else None, 'candidate_sha256': hashlib.sha256(after.encode()).hexdigest()})
(draft / 'motion-orientation.patch').write_text(patch, encoding='utf-8', newline='\n')
(draft / 'manifest.json').write_text(json.dumps({'scope':'Draft only; no live edits or native execution', 'patch_sha256':hashlib.sha256(patch.encode()).hexdigest(), 'files':manifest},indent=2)+'\n', encoding='utf-8')
print(json.dumps({'patch_sha256':hashlib.sha256(patch.encode()).hexdigest(),'files':manifest},indent=2))
