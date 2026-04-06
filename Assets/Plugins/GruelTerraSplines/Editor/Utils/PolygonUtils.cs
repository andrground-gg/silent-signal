using System.Collections.Generic;
using UnityEngine;

namespace GruelTerraSplines
{
    public static class PolygonUtils
    {
        public static bool PointInPolygonXZ(Vector2 p, List<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var pi = poly[i];
                var pj = poly[j];
                bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                                 (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + 1e-6f) + pi.x);
                if (intersect) inside = !inside;
            }

            return inside;
        }

        public static float SignedDistanceToPolygon(Vector2 p, List<Vector2> poly)
        {
            bool inside = PointInPolygonXZ(p, poly);
            float minDist = float.MaxValue;

            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var p1 = poly[i];
                var p2 = poly[j];
                float dist = DistanceToLineSegment(p, p1, p2);
                if (dist < minDist) minDist = dist;
            }

            return inside ? -minDist : minDist;
        }

        public static float DistanceToLineSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            Vector2 ap = p - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 1e-6f) return ap.magnitude;

            float t = Vector2.Dot(ap, ab) / abLenSq;
            t = Mathf.Clamp01(t);
            Vector2 closest = a + t * ab;
            return Vector2.Distance(p, closest);
        }

        public static float NearestSquaredDistanceToPoints(Vector2 p, List<Vector2> points)
        {
            float best = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float d = (points[i] - p).sqrMagnitude;
                if (d < best) best = d;
            }

            return best;
        }

        public static Vector2 CalculatePolygonCenter(List<Vector2> poly)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < poly.Count; i++)
            {
                sum += poly[i];
            }

            return sum / poly.Count;
        }

        public static float CalculateMaxDistanceFromCenter(List<Vector2> poly, Vector2 center)
        {
            float maxDist = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                float dist = Vector2.Distance(poly[i], center);
                if (dist > maxDist) maxDist = dist;
            }

            return maxDist;
        }
    }
}