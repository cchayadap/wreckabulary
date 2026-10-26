#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GameStudioKit.Common;
using GameStudioKit.Qa;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    internal sealed class WordboundMeleeCapture : IDisposable
    {
        const string Actor = WordboundLocalMatchClient.LocalActor;
        const int MaximumFrames = 8;
        readonly QaContext context;
        readonly WordboundLocalMatchClient client;
        readonly WordboundOriginalEquipmentView view;
        readonly RenderTexture[] buffers = new RenderTexture[MaximumFrames];
        readonly List<Sample> samples = new List<Sample>();
        readonly List<object> canonical = new List<object>();
        readonly int width, height, windup, active, cycle;
        readonly long epoch;
        readonly string weapon, reportPath;
        readonly Matrix4x4 cameraMatrix;
        readonly float cameraFov, timeScale, captureDelta;
        long start = -1, end = -1, lastTick = -1, canonicalCount, missedTicks;
        int lastFrame = -1;
        string error = "";
        bool disposed, subscribed, reported, readbackStarted;
        string readbackOrientation = "unverified";
        readonly Dictionary<string, object> orientationFacts = new Dictionary<string, object>();
        CommandReceipt receipt;
        public PlayerCommand Command { get; private set; }
        public bool Passed { get; private set; }

        sealed class Sample
        {
            public string Phase, Sha;
            public long Tick;
            public int Frame;
            public Vector3 HandLocal;
            public Quaternion HandRotation;
            public Matrix4x4 HeldRelativeToAvatar;
            public Dictionary<string, object> Facts;
        }

        public WordboundMeleeCapture(QaContext owner, WordboundLocalMatchClient source,
            WordboundOriginalEquipmentView observation, ItemDefinition item)
        {
            context = owner; client = source; view = observation;
            reportPath = context.PathInRun("equipment-bat-motion.json");
            if (File.Exists(reportPath)) throw new QaFailException("BAT motion evidence already exists; capture never overwrites a previous attempt.");
            if (item?.Melee == null || item.Id != "BAT" || !view.Ready("BAT"))
                throw new QaFailException("The original equipped BAT and its actual catalogue timing are required.");
            width = Screen.width; height = Screen.height;
            if (width < 320 || height < 180 || (long)width * height * 4 * MaximumFrames > 128L * 1024 * 1024)
                throw new QaFailException("BAT motion buffers require a normal window within the 128 MiB eight-frame GPU budget.");
            windup = WordboundCombatPolicy.WindupTicks(item.Melee);
            active = WordboundCombatPolicy.ActiveTicks(item.Melee);
            cycle = WordboundCombatPolicy.CycleTicks(item.Melee);
            if (windup < 1 || cycle <= windup + active || cycle > 3 * ModeDefinition.TickRate)
                throw new QaFailException("BAT motion requires three observable phases and an actual cycle of at most three seconds.");
            var frame = Frame;
            epoch = frame.Epoch; weapon = WordboundCombatPolicy.EquippedItemId(frame, Actor);
            var actor = frame.Actors.Single(a => a.PlayerId == Actor);
            if (weapon == "" || actor.Attack != null || actor.NextAttackTick > frame.Progress.Tick)
                throw new QaFailException("BAT motion must begin from a ready, idle actual weapon.");
            cameraMatrix = view.MeleeCamera.transform.localToWorldMatrix;
            cameraFov = view.MeleeCamera.fieldOfView;
            timeScale = Time.timeScale; captureDelta = Time.captureDeltaTime;
        }

        MatchSnapshot Frame
        {
            get
            {
                if (!client || !client.Gameplay || client.Gameplay.Fault != "" || client.Gameplay.IsSimulationPaused || client.Gameplay.Frame == null)
                    throw new QaFailException("The actual BAT simulation is unavailable, paused or faulted.");
                return client.Gameplay.Frame;
            }
        }

        public IEnumerator Run()
        {
            try
            {
                for (int i = 0; i < buffers.Length; i++)
                {
                    buffers[i] = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = "QA live BAT frame " + i, antiAliasing = 1, useMipMap = false, hideFlags = HideFlags.DontSave };
                    if (!buffers[i].Create()) throw new QaFailException("Unable to allocate the bounded BAT screen-copy buffers.");
                }
            }
            catch (Exception failure) { Fail(failure.Message); }
            if (error == "")
            {
                yield return new WaitForEndOfFrame();
                Capture("baseline");
                if (error == "") CalibrateReadback();
            }
            if (error == "")
            {
                yield return new WaitForEndOfFrame();
                orientationFacts["attack_submit_unity_frame"] = Time.frameCount;
                orientationFacts["natural_frames_before_attack"] = Time.frameCount - samples[0].Frame;
                orientationFacts["attack_submit_realtime_seconds"] = Time.realtimeSinceStartupAsDouble;
                if (Time.frameCount <= samples[0].Frame) Fail("BAT calibration must drain through a later natural frame before Attack submission.");
            }
            if (error == "")
            {
                client.Gameplay.FrameChanged += Observe; subscribed = true;
                try { Command = client.Gameplay.Submit(WordboundAction.Attack); Observe(Frame); }
                catch (Exception failure) { Fail(failure.Message); }
            }
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (error == "" && !samples.Any(s => s.Phase == "complete") && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return new WaitForEndOfFrame();
                try
                {
                    var frame = Frame;
                    Observe(frame);
                    if (error != "" || start < 0) continue;
                    long age = frame.Progress.Tick - start;
                    string phase = age < windup ? "windup" : age < windup + active ? "active" : age < cycle ? "recovery" : "complete";
                    int limit = phase == "complete" ? 1 : 2;
                    if (samples.Count(s => s.Phase == phase) < limit &&
                        !samples.Any(s => s.Phase == phase && s.Tick == frame.Progress.Tick)) Capture(phase);
                }
                catch (Exception failure) { Fail(failure.Message); }
            }
            if (error == "" && !samples.Any(s => s.Phase == "complete")) Fail("The accepted BAT attack did not finish within 15 seconds.");
            Unsubscribe();
            if (error == "") ValidateSequence();
            readbackStarted = true;
            WriteReport("IN PROGRESS");
            for (int i = 0; i < samples.Count; i++)
            {
                SaveFrame(i);
                yield return null;
            }
            if (error == "")
            {
                string baselineHash = samples.Single(s => s.Phase == "baseline").Sha;
                int distinctAttackImages = samples.Where(s => s.Phase == "windup" || s.Phase == "active" || s.Phase == "recovery")
                    .Select(s => s.Sha).Where(hash => !string.IsNullOrEmpty(hash) && hash != baselineHash).Distinct().Count();
                if (samples.Any(s => string.IsNullOrEmpty(s.Sha)) || distinctAttackImages < 2)
                    Fail("The buffered attack requires at least two distinct attack images different from baseline; returning to the baseline pose is allowed.");
                try
                {
                    var frame = Frame;
                    if (!view.Ready("BAT") || frame.Epoch != epoch || WordboundCombatPolicy.EquippedItemId(frame, Actor) != weapon ||
                        frame.Actors.Single(a => a.PlayerId == Actor).Attack != null)
                        Fail("The same BAT did not remain held after capture readback.");
                }
                catch (Exception failure) { Fail(failure.Message); }
            }
            Passed = error == "";
            WriteReport(Passed ? "PASS" : "FAIL");
            ReleaseBuffers();
            if (!Passed) throw new QaFailException("Actual BAT motion capture failed: " + error);
        }

        void Observe(MatchSnapshot frame)
        {
            if (error != "" || disposed || Command == null) return;
            try
            {
                if (frame.Epoch != epoch || Command.Epoch != epoch || frame.Progress.Tick < lastTick)
                    throw new QaFailException("BAT motion changed epoch or regressed canonical time.");
                if (WordboundCombatPolicy.EquippedItemId(frame, Actor) != weapon)
                    throw new QaFailException("The equipped BAT entity changed during its attack.");
                var actor = frame.Actors.Single(a => a.PlayerId == Actor);
                if (actor.Health <= 0 || actor.StunnedUntilTick > frame.Progress.Tick || actor.Defense != null)
                    throw new QaFailException("The actual BAT attack was interrupted by player state.");
                var found = frame.Commands.Receipts.SingleOrDefault(r => r.ActorId == Actor && r.Sequence == Command.Sequence);
                if (found != null)
                {
                    if (found.PayloadHash != Command.PayloadHash || found.Effect.Code != "Ok" || found.Effect.EntityId != weapon)
                        throw new QaFailException("The real BAT Attack lacks the exact accepted payload/weapon receipt.");
                    receipt = found;
                }
                if (actor.Attack != null && start < 0 && receipt != null)
                {
                    start = actor.Attack.StartedTick; end = actor.NextAttackTick;
                    if (start != receipt.Tick || actor.Attack.WeaponEntityId != weapon || end != start + cycle)
                        throw new QaFailException("Canonical AttackStarted/Weapon/cooldown do not match the actual accepted command.");
                }
                if (start >= 0)
                {
                    if (actor.NextAttackTick != end || (actor.Attack != null &&
                        (actor.Attack.StartedTick != start || actor.Attack.WeaponEntityId != weapon)))
                        throw new QaFailException("A different attack or weapon replaced the observed BAT attack.");
                    bool shouldAttack = frame.Progress.Tick < start + windup + active;
                    if ((actor.Attack != null) != shouldAttack)
                        throw new QaFailException("BAT canonical attack state ended early or persisted beyond its active window.");
                }
                if (frame.Progress.Tick == lastTick) return;
                if (lastTick >= 0) missedTicks += Math.Max(0, frame.Progress.Tick - lastTick - 1);
                lastTick = frame.Progress.Tick; canonicalCount++;
                if (canonical.Count < 256) canonical.Add(new Dictionary<string, object> {
                    ["epoch"] = frame.Epoch, ["tick"] = frame.Progress.Tick, ["unity_frame"] = Time.frameCount,
                    ["attack_started_tick"] = actor.Attack == null ? (object)null : actor.Attack.StartedTick,
                    ["attack_weapon"] = actor.Attack?.WeaponEntityId, ["next_attack_tick"] = actor.NextAttackTick,
                    ["equipped_entity"] = weapon, ["receipt_observed"] = receipt != null });
            }
            catch (Exception failure) { Fail(failure.Message); }
        }

        void Capture(string phase)
        {
            try
            {
                if (samples.Count >= MaximumFrames || lastFrame == Time.frameCount || Screen.width != width || Screen.height != height ||
                    Time.timeScale != timeScale || Time.captureDeltaTime != captureDelta || !view.MeleeCamera ||
                    !Near(view.MeleeCamera.transform.localToWorldMatrix, cameraMatrix) || view.MeleeCamera.fieldOfView != cameraFov)
                    throw new QaFailException("BAT capture changed camera/time/window, duplicated a frame or exceeded its buffer budget.");
                var artwork = view.SampleMelee(out var avatar, out var hand, out var held, out var grip);
                float separation = Vector3.Distance(hand.position, grip.position);
                if (separation > .005f) throw new QaFailException("The actual BAT grip separated from the animated right hand by more than 5 mm.");
                var frame = Frame;
                var handLocal = avatar.InverseTransformPoint(hand.position);
                var handRotation = Quaternion.Inverse(avatar.rotation) * hand.rotation;
                var heldRelative = avatar.worldToLocalMatrix * held.localToWorldMatrix;
                var sample = new Sample { Phase = phase, Tick = frame.Progress.Tick, Frame = Time.frameCount,
                    HandLocal = handLocal, HandRotation = handRotation, HeldRelativeToAvatar = heldRelative,
                    Facts = new Dictionary<string, object> {
                        ["phase"] = phase, ["epoch"] = frame.Epoch, ["canonical_tick"] = frame.Progress.Tick,
                        ["age_ticks"] = start < 0 ? (object)null : frame.Progress.Tick - start,
                        ["unity_frame"] = Time.frameCount, ["realtime_seconds"] = Time.realtimeSinceStartupAsDouble,
                        ["held_entity"] = weapon, ["hand_grip_world"] = V(hand.position), ["held_grip_world"] = V(grip.position),
                        ["grip_separation_metres"] = separation, ["hand_world_matrix"] = M(hand.localToWorldMatrix),
                        ["held_world_matrix"] = M(held.localToWorldMatrix), ["held_grip_world_matrix"] = M(grip.localToWorldMatrix),
                        ["avatar_world_matrix"] = M(avatar.localToWorldMatrix), ["held_relative_to_avatar_matrix"] = M(heldRelative),
                        ["hand_local_to_avatar"] = V(handLocal), ["camera_world_matrix"] = M(cameraMatrix),
                        ["camera_fov"] = cameraFov, ["actual_original_artwork"] = artwork } };
                ScreenCapture.CaptureScreenshotIntoRenderTexture(buffers[samples.Count]);
                samples.Add(sample); lastFrame = Time.frameCount;
            }
            catch (Exception failure) { Fail(failure.Message); }
        }

        void ValidateSequence()
        {
            if (receipt == null || start < 0 || end != start + cycle ||
                new[] { "baseline", "windup", "active", "recovery", "complete" }.Any(p => !samples.Any(s => s.Phase == p)))
            { Fail("The actual accepted BAT sequence missed a required rendered phase; no synthetic or repeated attack substitutes are used."); return; }
            var baseline = samples[0];
            var moving = samples.Where(s => s.Phase == "windup" || s.Phase == "active" || s.Phase == "recovery").ToArray();
            if (!moving.Any(s => Quaternion.Angle(s.HandRotation, baseline.HandRotation) > 5 ||
                Vector3.Distance(s.HandLocal, baseline.HandLocal) > .01f) ||
                !moving.Any(s => !Near(s.HeldRelativeToAvatar, baseline.HeldRelativeToAvatar, .005f)))
                Fail("Canonical attack occurred, but the rendered hand and held BAT did not show measurable motion relative to the avatar.");
        }

        void CalibrateReadback()
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
        {
            Texture2D texture = null;
            var previous = RenderTexture.active;
            try
            {
                var sample = samples[index];
                string name = "wordbound_original_bat_motion_" + index.ToString("00") + "_" + sample.Phase;
                string path = context.PathInRun(name + ".png");
                if (File.Exists(path)) throw new QaFailException("BAT sequence PNG already exists; evidence cannot be overwritten.");
                RenderTexture.active = buffers[index];
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); texture.Apply(false, false);
                var pixels = texture.GetPixels32();
                OrientRows(pixels, width, height, readbackOrientation);
                texture.SetPixels32(pixels); texture.Apply(false, false);
                var first = pixels[0];
                if (!pixels.Any(p => p.a != 0 && (p.r != 0 || p.g != 0 || p.b != 0)) ||
                    !pixels.Any(p => p.r != first.r || p.g != first.g || p.b != first.b))
                    throw new QaFailException("Buffered BAT frame is blank.");
                byte[] bytes = texture.EncodeToPNG();
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) stream.Write(bytes, 0, bytes.Length);
                using var hash = SHA256.Create();
                sample.Sha = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                sample.Facts["path"] = Path.GetFileName(path); sample.Facts["sha256"] = sample.Sha;
                sample.Facts["readback_orientation"] = readbackOrientation;
                sample.Facts["width"] = width; sample.Facts["height"] = height; sample.Facts["png_bytes"] = bytes.Length;
                context.AddCapture(name, path, "png", "screen");
            }
            catch (Exception failure) { Fail(failure.Message); }
            finally { RenderTexture.active = previous; if (texture) Object.Destroy(texture); }
            WriteReport("IN PROGRESS");
        }

        void Fail(string reason) { if (error == "") error = reason; }

        void WriteReport(string status)
        {
            var facts = new Dictionary<string, object> {
                ["status"] = status, ["failure"] = error, ["scope"] = "Actual local Windows QA camera. Canonical phase labels; interpolated visual pose may lead within a tick. No multiplayer, human input, art approval or performance claim.",
                ["capture_method"] = "Same-frame upright baseline calibration before Attack; bounded GPU copies at WaitForEndOfFrame during Attack; attack-frame readback, measured row orientation and PNG encoding only after capture stops.",
                ["epoch"] = epoch, ["weapon_entity"] = weapon, ["attack_started_tick"] = start, ["attack_finished_tick"] = end,
                ["windup_ticks"] = windup, ["active_ticks"] = active, ["cycle_ticks"] = cycle,
                ["command_sequence"] = Command == null ? (object)null : Command.Sequence, ["command_payload_hash"] = Command?.PayloadHash,
                ["receipt"] = receipt == null ? null : new Dictionary<string, object> {
                    ["sequence"] = receipt.Sequence, ["tick"] = receipt.Tick, ["payload_hash"] = receipt.PayloadHash,
                    ["code"] = receipt.Effect.Code, ["entity"] = receipt.Effect.EntityId },
                ["canonical_callbacks"] = canonicalCount, ["unobserved_canonical_ticks"] = missedTicks, ["canonical_samples"] = canonical,
                ["readback_orientation"] = readbackOrientation, ["orientation_calibration"] = orientationFacts,
                ["maximum_gpu_bytes"] = (long)width * height * 4 * MaximumFrames, ["readback_started"] = readbackStarted,
                ["time_scale"] = timeScale, ["capture_delta_time"] = captureDelta,
                ["samples"] = samples.Select(s => s.Facts).ToArray() };
            File.WriteAllText(reportPath, KitJson.Serialize(facts));
            context.Note("equipment_bat_motion", facts); reported = true;
        }

        void Unsubscribe()
        {
            if (subscribed && client && client.Gameplay) client.Gameplay.FrameChanged -= Observe;
            subscribed = false;
        }

        void ReleaseBuffers()
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                if (!buffers[i]) continue;
                buffers[i].Release(); Object.Destroy(buffers[i]); buffers[i] = null;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Unsubscribe();
            try
            {
                if (!Passed) { Fail("BAT motion capture was interrupted before successful completion."); WriteReport("FAIL"); }
                else if (!reported) WriteReport("PASS");
            }
            catch (Exception failure) { context.Note("equipment_bat_motion_cleanup_error", failure.GetType().Name); }
            finally { ReleaseBuffers(); }
        }

        static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
        static float[] M(Matrix4x4 m) => Enumerable.Range(0, 16).Select(i => m[i / 4, i % 4]).ToArray();
        static bool Near(Matrix4x4 a, Matrix4x4 b, float tolerance = .00001f) =>
            Enumerable.Range(0, 16).All(i => Mathf.Abs(a[i] - b[i]) <= tolerance);
    }
}
#endif
