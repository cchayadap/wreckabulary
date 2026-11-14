// Game Studio Kit (MIT). Damped springs for procedural secondary motion (body bob, lean, tails, ears, squash).
// Implicit integration (LocoMath.SpringStep), so a long frame never makes a spring explode.
using System;
using UnityEngine;

namespace GameStudioKit.Locomotion
{
    [Serializable]
    public struct FloatSpring
    {
        [Tooltip("Oscillation frequency in Hz.")]
        public float frequency;
        [Tooltip("1 = critically damped, < 1 overshoots, > 1 is sluggish.")]
        public float damping;
        public float value;
        public float velocity;

        public FloatSpring(float frequency, float damping, float value = 0f)
        {
            this.frequency = frequency;
            this.damping = damping;
            this.value = value;
            velocity = 0f;
        }

        public float Step(float target, float dt)
        {
            LocoMath.SpringStep(ref value, ref velocity, target, frequency, damping, dt);
            return value;
        }

        public void Reset(float v)
        {
            value = v;
            velocity = 0f;
        }
    }

    [Serializable]
    public struct Vector3Spring
    {
        public float frequency;
        public float damping;
        public Vector3 value;
        public Vector3 velocity;

        public Vector3Spring(float frequency, float damping, Vector3 value = default)
        {
            this.frequency = frequency;
            this.damping = damping;
            this.value = value;
            velocity = Vector3.zero;
        }

        public Vector3 Step(Vector3 target, float dt)
        {
            float x = value.x, vx = velocity.x;
            float y = value.y, vy = velocity.y;
            float z = value.z, vz = velocity.z;
            LocoMath.SpringStep(ref x, ref vx, target.x, frequency, damping, dt);
            LocoMath.SpringStep(ref y, ref vy, target.y, frequency, damping, dt);
            LocoMath.SpringStep(ref z, ref vz, target.z, frequency, damping, dt);
            value = new Vector3(x, y, z);
            velocity = new Vector3(vx, vy, vz);
            return value;
        }

        public void Reset(Vector3 v)
        {
            value = v;
            velocity = Vector3.zero;
        }
    }
}
