using System;
using UnityEngine;

namespace Wreckabulary.Online
{
    [CreateAssetMenu(menuName = "Wreckabulary/Wordbound/Camera profile")]
    public sealed class WordboundCameraProfile : ScriptableObject
    {
        [Range(35, 90)] public float FieldOfView = 55;
        [Range(.02f, .3f)] public float NearClip = .1f;
        [Range(100, 3000)] public float FarClip = 1800;
        [Range(1, 8)] public float Distance = 3.3f;
        [Range(.2f, 1)] public float MinDistance = .35f;
        [Range(.5f, 2)] public float PivotHeight = 1;
        [Range(-.5f, .5f)] public float Lift = .1f;
        [Range(.05f, .4f)] public float ProbeRadius = .2f;
        [Range(.01f, .15f)] public float Skin = .05f;
        [Range(1, 20)] public float OutEase = 6;
        [Tooltip("Pitch values are in radians, matching the existing camera.")]
        [Range(-1.3f, 1.3f)] public float DefaultPitch = .16f;
        [Range(-1.3f, 0)] public float MinPitch = -.45f;
        [Range(0, 1.3f)] public float MaxPitch = .95f;
        [Range(.0002f, .01f)] public float MouseSensitivity = .0024f;
        [Range(.2f, 6)] public float StickYawSpeed = 2.6f;
        [Range(.2f, 6)] public float StickPitchSpeed = 1.8f;

        public void Validate()
        {
            Check(FieldOfView, 35, 90, nameof(FieldOfView)); Check(NearClip, .02f, .3f, nameof(NearClip));
            Check(FarClip, 100, 3000, nameof(FarClip)); Check(Distance, 1, 8, nameof(Distance));
            Check(MinDistance, .2f, 1, nameof(MinDistance)); Check(PivotHeight, .5f, 2, nameof(PivotHeight));
            Check(Lift, -.5f, .5f, nameof(Lift)); Check(ProbeRadius, .05f, .4f, nameof(ProbeRadius));
            Check(Skin, .01f, .15f, nameof(Skin)); Check(OutEase, 1, 20, nameof(OutEase));
            Check(MinPitch, -1.3f, 0, nameof(MinPitch)); Check(MaxPitch, 0, 1.3f, nameof(MaxPitch));
            Check(DefaultPitch, MinPitch, MaxPitch, nameof(DefaultPitch));
            Check(MouseSensitivity, .0002f, .01f, nameof(MouseSensitivity));
            Check(StickYawSpeed, .2f, 6, nameof(StickYawSpeed)); Check(StickPitchSpeed, .2f, 6, nameof(StickPitchSpeed));
            if (MinDistance >= Distance || NearClip >= MinDistance || ProbeRadius + Skin > MinDistance)
                throw new ArgumentException("Camera clearance must fit inside the minimum follow distance.");
        }
        static void Check(float value, float minimum, float maximum, string field)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentException(field + " is outside its supported camera range.");
        }
    }
}
