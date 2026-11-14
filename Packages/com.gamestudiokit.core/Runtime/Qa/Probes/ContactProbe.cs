// Game Studio Kit (MIT). Ground contact of a whole body pose (death holds, sitting, landing, props):
//  - death_pose_ground_penetration_cm (or penetrationMetric): how far the lowest skinned vertex sinks below the ground;
//  - pose_float_cm: how far the lowest vertex hovers above it.
// Skinned meshes are baked each sample (BakeMesh) so the real deformed surface is measured, not the bounds. Sample every
// sampleEveryNFrames frames to bound the cost; MeasureNow() measures the current pose once.
// The ground is the highest up-facing surface at or below the hips (a table, bed or low ceiling above the body never
// counts, nor does a dropped weapon with a Rigidbody); on a terrain it is Terrain.SampleHeight, which also measures a
// body sunk metres deep. A window that measured nothing is a FAIL (NaN), never a 0 cm PASS.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameStudioKit.Qa.Probes
{
    [AddComponentMenu("Game Studio Kit/Probes/Contact Probe")]
    public sealed class ContactProbe : EmbodimentProbe
    {
        public string penetrationMetric = "death_pose_ground_penetration_cm";
        public string floatMetric = "pose_float_cm";
        [Min(1)] public int sampleEveryNFrames = 5;
        [Tooltip("Renderers to measure; empty = every renderer under the animator (or this object).")]
        public List<Renderer> renderers = new List<Renderer>();

        /// <summary>Samples in the current window that found both the body and its ground.</summary>
        public int MeasuredSamples { get; private set; }

        float _maxPenetration;
        float _maxFloat;
        int _frame;
        int _attempts;
        Mesh _baked;
        readonly List<Vector3> _verts = new List<Vector3>(4096);
        readonly List<SurfaceHit> _surfaces = new List<SurfaceHit>(16);
        readonly RaycastHit[] _groundHits = new RaycastHit[32];

        protected override void OnBegin()
        {
            _maxPenetration = 0f;
            _maxFloat = 0f;
            _frame = 0;
            _attempts = 0;
            MeasuredSamples = 0;
            if (renderers.Count == 0)
            {
                var root = animator != null ? animator.transform : transform;
                renderers.AddRange(root.GetComponentsInChildren<Renderer>());
            }
        }

        protected override void Sample(float dt)
        {
            if (_frame++ % Mathf.Max(1, sampleEveryNFrames) != 0) return;
            Measure();
        }

        void Measure()
        {
            _attempts++;
            if (!MeasurePose(out float penetration, out float hover)) return;
            MeasuredSamples++;
            _maxPenetration = Mathf.Max(_maxPenetration, penetration);
            _maxFloat = Mathf.Max(_maxFloat, hover);
        }

        /// <summary>Measures the current pose once (no window needed; an open window is restarted).</summary>
        public List<ProbeResult> MeasureNow(ProbeConfig config)
        {
            BeginWindow();
            Measure();
            return EndWindow(config);
        }

        bool MeasurePose(out float penetration, out float hover)
        {
            penetration = 0f;
            hover = 0f;
            if (!LowestPoint(out Vector3 lowest, out float topY)) return false;
            var self = Bones?.Root != null ? Bones.Root : transform;
            float reference = Bones?.Hips != null ? Bones.Hips.position.y : Mathf.Max(lowest.y, topY - 0.5f * (topY - lowest.y));
            if (!GroundUnder(lowest, topY, reference, self, out float ground)) return false;
            penetration = Mathf.Max(0f, ground - lowest.y);
            hover = Mathf.Max(0f, lowest.y - ground);
            return true;
        }

        /// <summary>
        /// Ground at the lowest vertex: the highest up-facing hit at or below <paramref name="referenceY"/> (hips), cast
        /// from above the whole body; triggers, the body's own colliders and non-kinematic Rigidbodies are ignored. When
        /// no such hit exists but the point lies over a terrain (a body sunk below it), the terrain height is used.
        /// </summary>
        bool GroundUnder(Vector3 lowest, float topY, float referenceY, Transform self, out float ground)
        {
            ground = float.NaN;
            float originY = Mathf.Max(topY, referenceY) + 1f;
            var origin = new Vector3(lowest.x, originY, lowest.z);
            float distance = originY - lowest.y + 4f;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, distance, groundMask, QueryTriggerInteraction.Ignore);
            _surfaces.Clear();
            for (int i = 0; i < n; i++)
            {
                var h = _groundHits[i];
                var col = h.collider;
                if (col == null) continue;
                if (self != null && col.transform.IsChildOf(self)) continue;
                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                float y = h.point.y;
                if (col is TerrainCollider tc && TerrainHeight(tc.GetComponent<Terrain>(), lowest, out float th)) y = th;
                _surfaces.Add(new SurfaceHit(y, h.normal.y, col.bounds.min.y, col is TerrainCollider, col is TerrainCollider));
            }
            if (ProbeMath.TryContactGround(_surfaces, referenceY, out ground)) return true;

            // Deep sink: the ray started inside or under the ground and hit nothing; a terrain still knows its height.
            foreach (var t in Terrain.activeTerrains)
            {
                if (TerrainHeight(t, lowest, out float th) && th > lowest.y)
                {
                    ground = th;
                    return true;
                }
            }
            return false;
        }

        static bool TerrainHeight(Terrain t, Vector3 at, out float height)
        {
            height = float.NaN;
            if (t == null || t.terrainData == null) return false;
            var tp = t.transform.position;
            var size = t.terrainData.size;
            if (at.x < tp.x || at.x > tp.x + size.x || at.z < tp.z || at.z > tp.z + size.z) return false;
            height = t.SampleHeight(at) + tp.y;
            return true;
        }

        bool LowestPoint(out Vector3 lowest, out float topY)
        {
            lowest = new Vector3(0f, float.PositiveInfinity, 0f);
            topY = float.NegativeInfinity;
            bool found = false;
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                topY = Mathf.Max(topY, r.bounds.max.y);
                if (r is SkinnedMeshRenderer smr)
                {
                    try
                    {
                        if (_baked == null) _baked = new Mesh { name = "GSK ContactProbe bake" };
                        smr.BakeMesh(_baked, true);
                        _baked.GetVertices(_verts);
                        var t = smr.transform;
                        foreach (var v in _verts)
                        {
                            Vector3 w = t.position + t.rotation * v;
                            if (w.y < lowest.y) { lowest = w; found = true; }
                        }
                        continue;
                    }
                    catch (Exception)
                    {
                        // Fall through to the bounds when the mesh cannot be baked.
                    }
                }
                var bounds = r.bounds;
                if (bounds.min.y < lowest.y)
                {
                    lowest = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    found = true;
                }
            }
            return found;
        }

        protected override void OnEnd(ProbeConfig config, List<ProbeResult> results)
        {
            if (MeasuredSamples == 0)
            {
                Notes.Add("ContactProbe measured nothing in " + _attempts + " attempt(s): no enabled renderer, or no ground under the body");
                results.Add(Eval(config, penetrationMetric, double.NaN));
                return;
            }
            results.Add(Eval(config, penetrationMetric, _maxPenetration * 100f));
            results.Add(Eval(config, floatMetric, _maxFloat * 100f));
        }

        void OnDestroy()
        {
            if (_baked != null) Destroy(_baked);
        }
    }
}
