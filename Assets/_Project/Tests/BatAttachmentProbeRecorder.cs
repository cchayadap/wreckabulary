using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wreckabulary.Art;

namespace Wreckabulary.Tests
{
    [DefaultExecutionOrder(110)]
    public sealed class BatAttachmentProbeRecorder : MonoBehaviour
    {
        Camera camera;
        PlayerController player;
        PlayerAppearance appearance;
        Quaternion nativeGripCorrection;
        RenderTexture target;
        StreamWriter writer;
        float started;
        bool finished;
        bool observeSoleContact;
        SkinnedMeshRenderer boots;
        Collider[] floors;
        Mesh bakedBoots;
        readonly List<Vector3> bootVertices = new List<Vector3>();
        readonly List<int> leftSoleIndices = new List<int>();
        readonly List<int> rightSoleIndices = new List<int>();
        readonly Dictionary<Renderer,Vector3> idleRendererScales = new Dictionary<Renderer,Vector3>();
        Vector3 idleBootsLossyScale;
        int idleBootVertexCount;
        float idleLeftSoleY, idleRightSoleY;
        public string Phase = "setup", Failure;
        public float MaximumInterpolatedGap, MaximumHierarchyGap, MaximumHierarchyAngle;
        public float MaximumPreRenderHierarchyGap, MaximumPreRenderHierarchyAngle;
        public readonly Dictionary<string,int> PhaseSamples = new Dictionary<string,int>();
        public readonly Dictionary<string,int> ClipSamples = new Dictionary<string,int>();
        public int HierarchySamples;
        public int PickupSoleSamples, FirstHoldSoleSamples;
        public float MinimumLeftSoleGap = float.PositiveInfinity, MaximumLeftSoleGap = float.NegativeInfinity;
        public float MinimumRightSoleGap = float.PositiveInfinity, MaximumRightSoleGap = float.NegativeInfinity;
        public bool SoleCalibrationReady => observeSoleContact && leftSoleIndices.Count >= 3 && rightSoleIndices.Count >= 3;
        public int LeftSoleVertexCount => leftSoleIndices.Count;
        public int RightSoleVertexCount => rightSoleIndices.Count;
        public int IdleRendererCount => idleRendererScales.Count;
        public float MinimumRendererIdleRatio = float.PositiveInfinity, MaximumRendererIdleRatio = float.NegativeInfinity;
        public float MaximumRendererIdleDeviation;

        struct SoleSample
        {
            public float MinGap, MaxGap, LowestX, LowestY, LowestZ, FloorY;
            public int FloorHits;
            public string Floor;
        }

        public void Initialize(Camera productionCamera, PlayerController p, PlayerAppearance a, string directory, bool captureSoleContact = false)
        {
            camera = productionCamera; player = p; appearance = a; nativeGripCorrection = Quaternion.Inverse(a.RightGrip.rotation) * p.handR.rotation;
            observeSoleContact = captureSoleContact;
            if (observeSoleContact) CalibrateIdleSoles();
            started = Time.realtimeSinceStartup;
            target = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 24, RenderTextureFormat.ARGB32); target.Create();
            if (!RenderPipeline.SupportsRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }))
                throw new InvalidOperationException("Actual camera requires supported render request.");
            writer = new StreamWriter(Path.Combine(directory, "pose-probe.csv"));
            writer.WriteLine("frame,gameTime,wallTime,phase,clip,speed,interpolation,kinematic,anchorNativeGap,anchorProxyGap,authoredOrientationDegrees,proxyLocalOrientationDegrees,rigidbodyTransformPositionGap,rigidbodyTransformRotationDegrees,localX,localY,localZ,scaleX,scaleY,scaleZ,nativeX,nativeY,nativeZ,handX,handY,handZ,anchorX,anchorY,anchorZ,preRenderAnchorNativeGap,renderRequestAnchorMovement,renderRequestRootRotationDegrees,nativeQx,nativeQy,nativeQz,nativeQw,handQx,handQy,handQz,handQw,itemQx,itemQy,itemQz,itemQw,playableTime,clipDuration,clipFrameRate,grounded,leftSoleCount,leftSoleFloorHits,leftSoleMinGap,leftSoleMaxGap,leftSoleLowestX,leftSoleLowestY,leftSoleLowestZ,leftSoleFloorY,leftSoleFloor,rightSoleCount,rightSoleFloorHits,rightSoleMinGap,rightSoleMaxGap,rightSoleLowestX,rightSoleLowestY,rightSoleLowestZ,rightSoleFloorY,rightSoleFloor,idleLeftSoleY,idleRightSoleY,bootsLossyScaleX,bootsLossyScaleY,bootsLossyScaleZ,bootsRelativeIdleScaleX,bootsRelativeIdleScaleY,bootsRelativeIdleScaleZ,idleRendererCount,rendererIdleRatioMin,rendererIdleRatioMax,rendererIdleMaxDeviation");
        }

        void LateUpdate()
        {
            if (finished) return;
            try
            {
                if (Time.realtimeSinceStartup-started > 60) throw new InvalidOperationException("Probe wall bound.");
                var bat = player.Combat.Weapon; if (!bat) return;
                var grip = bat.Definition.Grip;
                var gripLocal = new Vector3(grip[0],grip[1],grip[2]);
                var beforeAnchor = bat.transform.TransformPoint(gripLocal);
                float beforeGap = Vector3.Distance(beforeAnchor, appearance.RightGrip.position);
                var beforeItemRotation = bat.transform.rotation;
                float beforeAngle = Quaternion.Angle(beforeItemRotation, appearance.RightGrip.rotation * nativeGripCorrection * bat.HoldRotation);
                var beforePosition = camera.transform.position; var beforeProjection = camera.projectionMatrix;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                if (camera.transform.position != beforePosition || camera.projectionMatrix != beforeProjection)
                    throw new InvalidOperationException("Probe changed camera state.");
                var rb = bat.GetComponent<Rigidbody>();
                var clipAsset = appearance.CurrentAnimationClip;
                var playable = appearance.CurrentPlayable;
                if (!clipAsset || !playable.IsValid()) throw new InvalidOperationException("Evaluated native clip is unavailable.");
                var anchor = bat.transform.TransformPoint(gripLocal);
                var native = appearance.RightGrip.position; var hand = player.handR.position;
                float gap = Vector3.Distance(anchor,native);
                float angle = Quaternion.Angle(bat.transform.rotation, appearance.RightGrip.rotation * nativeGripCorrection * bat.HoldRotation);
                if (Phase.StartsWith("interpolated-")) MaximumInterpolatedGap = Mathf.Max(MaximumInterpolatedGap,gap);
                if (Phase.StartsWith("hierarchy-owned-"))
                {
                    MaximumHierarchyGap = Mathf.Max(MaximumHierarchyGap,gap); MaximumHierarchyAngle = Mathf.Max(MaximumHierarchyAngle,angle);
                    MaximumPreRenderHierarchyGap = Mathf.Max(MaximumPreRenderHierarchyGap,beforeGap);
                    MaximumPreRenderHierarchyAngle = Mathf.Max(MaximumPreRenderHierarchyAngle,beforeAngle); HierarchySamples++;
                    PhaseSamples.TryGetValue(Phase,out int phaseCount); PhaseSamples[Phase] = phaseCount+1;
                    string clip = clipAsset.name; ClipSamples.TryGetValue(clip,out int clipCount); ClipSamples[clip] = clipCount+1;
                }
                var leftSole = EmptySole(); var rightSole = EmptySole();
                var bootsScale = new Vector3(float.NaN, float.NaN, float.NaN);
                var relativeIdleScale = bootsScale;
                float minimumRendererRatio = float.NaN, maximumRendererRatio = float.NaN, maximumRendererDeviation = float.NaN;
                if (observeSoleContact)
                {
                    bool contactPhase = Phase == "hierarchy-owned-pickup-and-first-hold" && (clipAsset.name == "Pickup" || clipAsset.name == "Hold_OneHand");
                    MeasureRendererScales(contactPhase, out minimumRendererRatio, out maximumRendererRatio, out maximumRendererDeviation);
                    boots.BakeMesh(bakedBoots, false); bakedBoots.GetVertices(bootVertices);
                    if (bootVertices.Count != idleBootVertexCount) throw new InvalidOperationException("Boot topology changed after Idle sole calibration.");
                    leftSole = MeasureSole(leftSoleIndices); rightSole = MeasureSole(rightSoleIndices);
                    bootsScale = boots.transform.lossyScale;
                    relativeIdleScale = new Vector3(bootsScale.x / idleBootsLossyScale.x, bootsScale.y / idleBootsLossyScale.y, bootsScale.z / idleBootsLossyScale.z);
                    if (contactPhase)
                    {
                        if (leftSole.FloorHits != leftSoleIndices.Count || rightSole.FloorHits != rightSoleIndices.Count)
                            throw new InvalidOperationException("Both retained sole bands require an actual floor ray hit for every vertex.");
                        MinimumLeftSoleGap = Mathf.Min(MinimumLeftSoleGap, leftSole.MinGap); MaximumLeftSoleGap = Mathf.Max(MaximumLeftSoleGap, leftSole.MaxGap);
                        MinimumRightSoleGap = Mathf.Min(MinimumRightSoleGap, rightSole.MinGap); MaximumRightSoleGap = Mathf.Max(MaximumRightSoleGap, rightSole.MaxGap);
                        MinimumRendererIdleRatio = Mathf.Min(MinimumRendererIdleRatio, minimumRendererRatio);
                        MaximumRendererIdleRatio = Mathf.Max(MaximumRendererIdleRatio, maximumRendererRatio);
                        MaximumRendererIdleDeviation = Mathf.Max(MaximumRendererIdleDeviation, maximumRendererDeviation);
                        if (clipAsset.name == "Pickup") PickupSoleSamples++; else FirstHoldSoleSamples++;
                    }
                }
                var local = bat.transform.localPosition; var scale = bat.transform.localScale; var v = player.Body.linearVelocity;
                writer.WriteLine(string.Join(",", new[] {Time.frameCount.ToString(),F(Time.time),F(Time.realtimeSinceStartup-started),Phase,clipAsset.name,
                    F(new Vector2(v.x,v.z).magnitude),rb.interpolation.ToString(),rb.isKinematic.ToString(),F(gap),F(Vector3.Distance(anchor,hand)),F(angle),
                    F(Quaternion.Angle(player.handR.rotation, appearance.RightGrip.rotation * nativeGripCorrection)),F(Vector3.Distance(rb.position,bat.transform.position)),F(Quaternion.Angle(rb.rotation,bat.transform.rotation)),
                    F(local.x),F(local.y),F(local.z),F(scale.x),F(scale.y),F(scale.z),F(native.x),F(native.y),F(native.z),F(hand.x),F(hand.y),F(hand.z),F(anchor.x),F(anchor.y),F(anchor.z),
                    F(beforeGap),F(Vector3.Distance(beforeAnchor,anchor)),F(Quaternion.Angle(beforeItemRotation,bat.transform.rotation)),Q(appearance.RightGrip.rotation),Q(player.handR.rotation),Q(bat.transform.rotation),
                    playable.GetTime().ToString("0.000000",CultureInfo.InvariantCulture),F(clipAsset.length),F(clipAsset.frameRate),player.Grounded.ToString(),
                    LeftSoleVertexCount.ToString(),leftSole.FloorHits.ToString(),F(leftSole.MinGap),F(leftSole.MaxGap),F(leftSole.LowestX),F(leftSole.LowestY),F(leftSole.LowestZ),F(leftSole.FloorY),Csv(leftSole.Floor),
                    RightSoleVertexCount.ToString(),rightSole.FloorHits.ToString(),F(rightSole.MinGap),F(rightSole.MaxGap),F(rightSole.LowestX),F(rightSole.LowestY),F(rightSole.LowestZ),F(rightSole.FloorY),Csv(rightSole.Floor),
                    F(observeSoleContact ? idleLeftSoleY : float.NaN),F(observeSoleContact ? idleRightSoleY : float.NaN),
                    F(bootsScale.x),F(bootsScale.y),F(bootsScale.z),F(relativeIdleScale.x),F(relativeIdleScale.y),F(relativeIdleScale.z),
                    IdleRendererCount.ToString(),F(minimumRendererRatio),F(maximumRendererRatio),F(maximumRendererDeviation)}));
            }
            catch(Exception exception) { Failure = exception.ToString(); Finish(); }
        }
        void CalibrateIdleSoles()
        {
            if (!appearance.CurrentAnimationClip || appearance.CurrentAnimationClip.name != "Idle")
                throw new InvalidOperationException("Fixed sole bands must be calibrated from natural Idle before pickup.");
            foreach (var renderer in appearance.AvatarModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.enabled && renderer.name == "SK_Boots")
                {
                    if (boots) throw new InvalidOperationException("Expected one enabled SK_Boots renderer.");
                    boots = renderer;
                }
            var leftFoot = ModelVisual.FindNamed(appearance.AvatarModel, "foot_L");
            var rightFoot = ModelVisual.FindNamed(appearance.AvatarModel, "foot_R");
            if (!boots || !leftFoot || !rightFoot) throw new InvalidOperationException("Native boots and both foot bones are required.");
            foreach (var renderer in appearance.AvatarModel.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled)
                {
                    var idleScale = renderer.transform.lossyScale;
                    if (!FiniteScale(idleScale) || Mathf.Abs(idleScale.x) < .000001f || Mathf.Abs(idleScale.y) < .000001f || Mathf.Abs(idleScale.z) < .000001f)
                        throw new InvalidOperationException("Idle renderer must have a finite, nonzero signed scale: " + renderer.name);
                    idleRendererScales.Add(renderer, idleScale);
                }
            var foundFloors = new List<Collider>();
            foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (collider.name.EndsWith(" floor", StringComparison.Ordinal)) foundFloors.Add(collider);
            floors = foundFloors.ToArray();
            if (floors.Length == 0) throw new InvalidOperationException("Actual room floor colliders are required.");
            idleBootsLossyScale = boots.transform.lossyScale;
            if (Mathf.Abs(idleBootsLossyScale.x) < .000001f || Mathf.Abs(idleBootsLossyScale.y) < .000001f || Mathf.Abs(idleBootsLossyScale.z) < .000001f)
                throw new InvalidOperationException("Idle boots must have a nonzero renderer scale.");
            bakedBoots = new Mesh(); boots.BakeMesh(bakedBoots, false); bakedBoots.GetVertices(bootVertices);
            idleBootVertexCount = bootVertices.Count;
            var leftCandidates = new List<int>(); var rightCandidates = new List<int>();
            var leftLocal = boots.transform.InverseTransformPoint(leftFoot.position);
            var rightLocal = boots.transform.InverseTransformPoint(rightFoot.position);
            idleLeftSoleY = idleRightSoleY = float.PositiveInfinity;
            for (int i = 0; i < bootVertices.Count; i++)
            {
                var vertex = bootVertices[i]; var world = boots.transform.TransformPoint(vertex);
                if ((vertex - leftLocal).sqrMagnitude <= (vertex - rightLocal).sqrMagnitude)
                { leftCandidates.Add(i); idleLeftSoleY = Mathf.Min(idleLeftSoleY, world.y); }
                else { rightCandidates.Add(i); idleRightSoleY = Mathf.Min(idleRightSoleY, world.y); }
            }
            foreach (int index in leftCandidates)
                if (boots.transform.TransformPoint(bootVertices[index]).y <= idleLeftSoleY + .002f) leftSoleIndices.Add(index);
            foreach (int index in rightCandidates)
                if (boots.transform.TransformPoint(bootVertices[index]).y <= idleRightSoleY + .002f) rightSoleIndices.Add(index);
            if (!SoleCalibrationReady) throw new InvalidOperationException("Idle must supply at least three fixed sole vertices for each native foot.");
        }
        SoleSample MeasureSole(List<int> indices)
        {
            var sample = EmptySole(); sample.MinGap = float.PositiveInfinity; sample.MaxGap = float.NegativeInfinity;
            sample.LowestY = float.PositiveInfinity;
            foreach (int index in indices)
            {
                var world = boots.transform.TransformPoint(bootVertices[index]);
                var ray = new Ray(new Vector3(world.x, player.transform.position.y + 1f, world.z), Vector3.down);
                bool found = false; var hit = new RaycastHit();
                foreach (var floor in floors)
                    if (floor && floor.enabled && floor.Raycast(ray, out var next, 4f) && (!found || next.distance < hit.distance))
                    { hit = next; found = true; }
                if (!found) continue;
                float gap = world.y - hit.point.y;
                if (float.IsNaN(gap) || float.IsInfinity(gap)) throw new InvalidOperationException("Retained sole gap must be finite.");
                sample.FloorHits++; sample.MinGap = Mathf.Min(sample.MinGap, gap); sample.MaxGap = Mathf.Max(sample.MaxGap, gap);
                if (world.y < sample.LowestY)
                {
                    sample.LowestX = world.x; sample.LowestY = world.y; sample.LowestZ = world.z;
                    sample.FloorY = hit.point.y; sample.Floor = hit.collider.name;
                }
            }
            return sample.FloorHits == 0 ? EmptySole() : sample;
        }
        void MeasureRendererScales(bool contactPhase, out float minimum, out float maximum, out float deviation)
        {
            minimum = float.PositiveInfinity; maximum = float.NegativeInfinity; deviation = 0f;
            if (contactPhase)
                foreach (var renderer in appearance.AvatarModel.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled && !idleRendererScales.ContainsKey(renderer))
                        throw new InvalidOperationException("Native renderer enabled after Idle calibration: " + renderer.name);
            foreach (var pair in idleRendererScales)
            {
                if (!pair.Key || !pair.Key.enabled) throw new InvalidOperationException("Calibrated native renderer changed during the transition.");
                var current = pair.Key.transform.lossyScale; var idle = pair.Value;
                if (!FiniteScale(current)) throw new InvalidOperationException("Native renderer scale became nonfinite: " + pair.Key.name);
                var ratio = new Vector3(current.x / idle.x, current.y / idle.y, current.z / idle.z);
                if (!FiniteScale(ratio)) throw new InvalidOperationException("Native renderer Idle scale ratio became nonfinite: " + pair.Key.name);
                if (contactPhase && (ratio.x < .5f || ratio.x > 1.5f || ratio.y < .5f || ratio.y > 1.5f || ratio.z < .5f || ratio.z > 1.5f))
                    throw new InvalidOperationException("Native renderer signed Idle scale ratio changed sign or left [.5,1.5]: " + pair.Key.name + " " + ratio);
                minimum = Mathf.Min(minimum, Mathf.Min(ratio.x, Mathf.Min(ratio.y, ratio.z)));
                maximum = Mathf.Max(maximum, Mathf.Max(ratio.x, Mathf.Max(ratio.y, ratio.z)));
                deviation = Mathf.Max(deviation, Mathf.Max(Mathf.Abs(ratio.x - 1f), Mathf.Max(Mathf.Abs(ratio.y - 1f), Mathf.Abs(ratio.z - 1f))));
            }
        }
        static bool FiniteScale(Vector3 scale) => !(float.IsNaN(scale.x) || float.IsInfinity(scale.x) || float.IsNaN(scale.y)
            || float.IsInfinity(scale.y) || float.IsNaN(scale.z) || float.IsInfinity(scale.z));
        static SoleSample EmptySole() => new SoleSample { MinGap = float.NaN, MaxGap = float.NaN, LowestX = float.NaN,
            LowestY = float.NaN, LowestZ = float.NaN, FloorY = float.NaN, Floor = "NONE" };
        static string F(float value) => value.ToString("0.000000",CultureInfo.InvariantCulture);
        static string Q(Quaternion value) => F(value.x)+","+F(value.y)+","+F(value.z)+","+F(value.w);
        static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        public void Finish()
        {
            if(finished)return; finished=true;
            try { writer?.Dispose(); } finally { if(target){target.Release();Destroy(target);} if(bakedBoots)Destroy(bakedBoots); }
        }
        void OnDestroy() => Finish();
    }
}
