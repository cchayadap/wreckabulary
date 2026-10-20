// Game Studio Kit (MIT). Engine-free decisions shared by the world audits and the contact probe, kept out of the
// physics code so the .NET harness can test them: which ray hit buries an object (sunk), which hit is the ground under
// a lying body (contact), and whether a hierarchy path lies under an intended prefix (bulk-tool diff).
using System;
using System.Collections.Generic;

namespace GameStudioKit.Qa.Probes
{
    /// <summary>One downward ray hit, reduced to what the decisions need.</summary>
    public struct SurfaceHit
    {
        /// <summary>World height of the hit point.</summary>
        public float PointY;

        /// <summary>Y component of the hit normal (1 = flat ground facing up).</summary>
        public float NormalY;

        /// <summary>Lowest point of the hit collider's world bounds.</summary>
        public float ColliderMinY;

        /// <summary>The collider is ground: a TerrainCollider or on a ground layer.</summary>
        public bool IsGround;

        /// <summary>The collider is a TerrainCollider (heightfield).</summary>
        public bool IsTerrain;

        public SurfaceHit(float pointY, float normalY, float colliderMinY, bool isGround, bool isTerrain = false)
        {
            PointY = pointY;
            NormalY = normalY;
            ColliderMinY = colliderMinY;
            IsGround = isGround;
            IsTerrain = isTerrain;
        }
    }

    public static class ProbeMath
    {
        /// <summary>Normals steeper than this (y below it) are walls or undersides, never ground.</summary>
        public const float GroundNormalMinY = 0.7f;

        /// <summary>
        /// The ground surface that buries an object whose top is at <paramref name="topY"/>: the lowest hit that is ground
        /// (terrain or a ground layer), faces up, lies more than <paramref name="sunkDepthM"/> above the top, and whose
        /// collider reaches below the top (it encloses the object, unlike a roof, a table or a bridge above it). A terrain
        /// is solid all the way down, so its collider bounds are not checked.
        /// </summary>
        public static bool TryBuryingSurface(IList<SurfaceHit> hits, float topY, float sunkDepthM, out float surfaceY)
        {
            surfaceY = float.NaN;
            if (hits == null) return false;
            bool found = false;
            foreach (var h in hits)
            {
                if (!h.IsGround || h.NormalY <= GroundNormalMinY) continue;
                if (h.PointY <= topY + sunkDepthM) continue;
                if (!h.IsTerrain && h.ColliderMinY >= topY) continue;
                if (!found || h.PointY < surfaceY)
                {
                    surfaceY = h.PointY;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// The ground under a body: the highest hit that faces up and lies at or below <paramref name="referenceY"/> (the
        /// hips) plus <paramref name="toleranceM"/>. A table, bed or ceiling above the body is never its ground.
        /// </summary>
        public static bool TryContactGround(IList<SurfaceHit> hits, float referenceY, out float groundY, float toleranceM = 0.05f)
        {
            groundY = float.NaN;
            if (hits == null) return false;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.NormalY <= GroundNormalMinY) continue;
                if (h.PointY > referenceY + toleranceM) continue;
                if (!found || h.PointY > groundY)
                {
                    groundY = h.PointY;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// True when <paramref name="path"/> is <paramref name="prefix"/> itself, below it ("prefix/…"), or a same-named
        /// sibling of it ("prefix#1", the snapshot's disambiguation). "Root/Group" does not cover "Root/Group2".
        /// </summary>
        public static bool PathUnder(string path, string prefix)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(prefix)) return false;
            string p = prefix.TrimEnd('/');
            if (p.Length == 0) return false;
            if (string.Equals(path, p, StringComparison.Ordinal)) return true;
            if (path.Length <= p.Length || !path.StartsWith(p, StringComparison.Ordinal)) return false;
            char next = path[p.Length];
            return next == '/' || next == '#';
        }

        /// <summary>True when <paramref name="path"/> is under any of <paramref name="prefixes"/>.</summary>
        public static bool PathUnderAny(string path, IList<string> prefixes)
        {
            if (prefixes == null) return false;
            foreach (var p in prefixes) if (PathUnder(path, p)) return true;
            return false;
        }
    }
}
