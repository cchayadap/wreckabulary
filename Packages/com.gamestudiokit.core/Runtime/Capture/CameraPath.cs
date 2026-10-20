// Game Studio Kit (MIT). Scripted camera path for perf_path runs, repeatable trailer shots and fly-throughs.
// Positions follow a uniform Catmull-Rom spline through the keys; rotations and field of view ease between keys.
// Time advances with Time.deltaTime, so a fixed Time.captureDeltaTime gives the same frames every run.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameStudioKit.Capture
{
    [Serializable]
    public struct CameraKey
    {
        [Tooltip("Seconds from the start of the path; keys must be in increasing time order.")]
        public float time;
        public Vector3 position;
        public Vector3 euler;
        [Tooltip("Field of view in degrees; 0 keeps the camera's current value.")]
        public float fov;
    }

    [AddComponentMenu("Game Studio Kit/Camera Path")]
    public sealed class CameraPath : MonoBehaviour
    {
        [Tooltip("Camera to drive; defaults to Camera.main when playback starts.")]
        public Camera target;
        public List<CameraKey> keys = new List<CameraKey>();

        [Tooltip("Build the keys from the child transforms, one every Seconds Per Child.")]
        public bool useChildrenAsKeys;
        public float secondsPerChild = 2f;
        public bool playOnStart;
        public bool loop;

        public bool IsPlaying { get; private set; }
        public float Time01 => Duration > 0 ? Mathf.Clamp01(_t / Duration) : 1f;
        public float Duration => keys.Count > 0 ? keys[keys.Count - 1].time : 0f;
        public event Action Finished;

        float _t;

        void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            if (useChildrenAsKeys) RebuildFromChildren();
            if (target == null) target = Camera.main;
            _t = 0f;
            IsPlaying = keys.Count > 0 && target != null;
            if (IsPlaying) Apply(0f);
        }

        public void Stop() => IsPlaying = false;

        void LateUpdate()
        {
            if (!IsPlaying) return;
            _t += Time.deltaTime;
            if (_t >= Duration)
            {
                if (loop && Duration > 0) _t %= Duration;
                else
                {
                    _t = Duration;
                    Apply(_t);
                    IsPlaying = false;
                    Finished?.Invoke();
                    return;
                }
            }
            Apply(_t);
        }

        void Apply(float t)
        {
            Evaluate(t, out var pos, out var rot, out float fov);
            target.transform.SetPositionAndRotation(pos, rot);
            if (fov > 0) target.fieldOfView = fov;
        }

        public void RebuildFromChildren()
        {
            keys.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                keys.Add(new CameraKey { time = i * Mathf.Max(0.01f, secondsPerChild), position = c.position, euler = c.eulerAngles, fov = 0f });
            }
        }

        /// <summary>Pose at time <paramref name="t"/> (clamped to the path).</summary>
        public void Evaluate(float t, out Vector3 position, out Quaternion rotation, out float fov)
        {
            if (keys.Count == 0)
            {
                position = transform.position;
                rotation = transform.rotation;
                fov = 0f;
                return;
            }
            if (keys.Count == 1 || t <= keys[0].time)
            {
                position = keys[0].position;
                rotation = Quaternion.Euler(keys[0].euler);
                fov = keys[0].fov;
                return;
            }
            int i = 0;
            while (i < keys.Count - 2 && t > keys[i + 1].time) i++;
            var k1 = keys[i];
            var k2 = keys[Mathf.Min(i + 1, keys.Count - 1)];
            var k0 = keys[Mathf.Max(i - 1, 0)];
            var k3 = keys[Mathf.Min(i + 2, keys.Count - 1)];
            float span = Mathf.Max(1e-5f, k2.time - k1.time);
            float u = Mathf.Clamp01((t - k1.time) / span);
            position = CatmullRom(k0.position, k1.position, k2.position, k3.position, u);
            float s = u * u * (3f - 2f * u);
            rotation = Quaternion.Slerp(Quaternion.Euler(k1.euler), Quaternion.Euler(k2.euler), s);
            fov = k1.fov > 0 && k2.fov > 0 ? Mathf.Lerp(k1.fov, k2.fov, s) : (k2.fov > 0 ? k2.fov : k1.fov);
        }

        /// <summary>Uniform Catmull-Rom point between p1 and p2 at u in [0, 1].</summary>
        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u;
            float u3 = u2 * u;
            return 0.5f * ((2f * p1) + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }
    }
}
