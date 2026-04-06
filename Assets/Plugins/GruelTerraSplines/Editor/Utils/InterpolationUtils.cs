using System.Collections.Generic;
using UnityEngine;

namespace GruelTerraSplines
{
    public static class InterpolationUtils
    {
        public static float InterpolateIDW(Vector2 p, List<Vector2> samples, List<float> values, int k)
        {
            int count = Mathf.Min(k, samples.Count);
            // simple linear scan for nearest k
            List<(float d2, int idx)> tmp = new List<(float, int)>(samples.Count);
            for (int i = 0; i < samples.Count; i++) tmp.Add(((samples[i] - p).sqrMagnitude, i));
            tmp.Sort((a, b) => a.d2.CompareTo(b.d2));
            float sumW = 0f;
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                float d2 = Mathf.Max(tmp[i].d2, 1e-6f);
                float w = 1f / d2;
                sumW += w;
                sum += w * values[tmp[i].idx];
            }

            if (sumW <= 1e-6f) return values[tmp[0].idx];
            return sum / sumW;
        }

        public static float InterpolateSmooth(Vector2 p, List<Vector2> samples, List<float> values)
        {
            // Smooth interpolation between border heights using distance weighting
            // This creates a natural convex surface that respects spline heights
            float sumW = 0f;
            float sum = 0f;

            for (int i = 0; i < samples.Count; i++)
            {
                float dist = Vector2.Distance(p, samples[i]);
                // Use inverse distance squared for smooth, natural interpolation
                // This creates a convex surface that naturally rises from border points
                float w = 1f / (dist * dist + 0.1f); // +0.1 to avoid division by zero
                sumW += w;
                sum += w * values[i];
            }

            return sumW > 1e-6f ? sum / sumW : values[0];
        }
    }
}