// Game Studio Kit (MIT). Held prop (weapon, staff, tool) probe during locomotion. At BeginWindow it records the
// authored hold (the prop's rotation in hand space); then per frame:
//  - grip_offset_cm: distance between the prop's grip socket and the hand socket (worst frame; × k);
//  - prop_angular_velocity_deg_s: rotation speed of the prop relative to the hand (95th percentile, so one glitch
//    frame does not dominate; WoO's owner-found "staff rotating like crazy");
//  - prop_up_axis_error_deg: worst angle between the prop's up axis and its authored up axis, in hand space;
//  - prop_over_face_pct: share of frames where the prop's bounds enter a sphere in front of the face.
using System.Collections.Generic;
using UnityEngine;

namespace GameStudioKit.Qa.Probes
{
    [AddComponentMenu("Game Studio Kit/Probes/Grip Probe")]
    public sealed class GripProbe : EmbodimentProbe
    {
        [Tooltip("The held prop's root transform.")]
        public Transform prop;
        [Tooltip("Grip point on the prop; default the prop root.")]
        public Transform gripSocket;
        [Tooltip("Hand socket or hand bone; default the right hand bone.")]
        public Transform handSocket;
        [Tooltip("Face sphere radius in metres at the 1.8 m reference (× k).")]
        public float faceRadius = 0.12f;
        [Tooltip("Face sphere centre distance in front of the head, metres at 1.8 m (× k).")]
        public float faceForward = 0.08f;

        Quaternion _authoredLocal;
        Vector3 _authoredUpHand;
        Quaternion _lastRel;
        bool _hasLast;
        float _maxOffset;
        float _maxUpError;
        int _faceFrames;
        int _frames;
        readonly List<float> _angularSpeeds = new List<float>(2048);
        Renderer[] _propRenderers;
        float _k = 1f;
        ProbeConfig _config;

        /// <summary>Optional: the config used for k (defaults to the kit defaults when EndWindow gets none).</summary>
        public void UseConfig(ProbeConfig config) => _config = config;

        protected override void OnBegin()
        {
            if (handSocket == null) handSocket = Bones?.RightHand;
            if (gripSocket == null) gripSocket = prop;
            _hasLast = false;
            _maxOffset = 0f;
            _maxUpError = 0f;
            _faceFrames = 0;
            _frames = 0;
            _angularSpeeds.Clear();
            _propRenderers = prop != null ? prop.GetComponentsInChildren<Renderer>() : new Renderer[0];
            _k = (float)(_config ?? ProbeConfig.Defaults()).K(bodyId);
            if (prop != null && handSocket != null)
            {
                _authoredLocal = Quaternion.Inverse(handSocket.rotation) * prop.rotation;
                _authoredUpHand = handSocket.InverseTransformDirection(prop.up);
            }
        }

        protected override void Sample(float dt)
        {
            if (prop == null || handSocket == null) return;
            _frames++;
            _maxOffset = Mathf.Max(_maxOffset, Vector3.Distance(gripSocket.position, handSocket.position));

            Quaternion rel = Quaternion.Inverse(handSocket.rotation) * prop.rotation;
            if (_hasLast && dt > 0f) _angularSpeeds.Add(Quaternion.Angle(_lastRel, rel) / dt);
            _lastRel = rel;
            _hasLast = true;

            Vector3 upHand = handSocket.InverseTransformDirection(prop.up);
            _maxUpError = Mathf.Max(_maxUpError, Vector3.Angle(upHand, _authoredUpHand));

            if (Bones?.Head != null && Bones.Root != null)
            {
                Vector3 centre = Bones.Head.position + Bones.Root.forward * (faceForward * _k);
                float radius = faceRadius * _k;
                foreach (var r in _propRenderers)
                {
                    if (r == null || !r.enabled) continue;
                    if (r.bounds.SqrDistance(centre) <= radius * radius)
                    {
                        _faceFrames++;
                        break;
                    }
                }
            }
        }

        protected override void OnEnd(ProbeConfig config, List<ProbeResult> results)
        {
            if (prop == null || handSocket == null || _frames == 0)
            {
                results.Add(Eval(config, "grip_offset_cm", double.NaN));
                return;
            }
            results.Add(Eval(config, "grip_offset_cm", _maxOffset * 100f));
            if (_angularSpeeds.Count > 0) results.Add(Eval(config, "prop_angular_velocity_deg_s", FrameStats.PercentileOf(_angularSpeeds, 95)));
            results.Add(Eval(config, "prop_up_axis_error_deg", _maxUpError));
            if (Bones?.Head != null) results.Add(Eval(config, "prop_over_face_pct", 100.0 * _faceFrames / _frames));
        }

        /// <summary>The prop's rotation in hand space at BeginWindow (the authored hold).</summary>
        public Quaternion AuthoredHold => _authoredLocal;
    }
}
