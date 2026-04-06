using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GruelTerraSplines
{

    public static partial class AnimationCurveExtensions
    {
        public static int GetAnimationCurveHash(this AnimationCurve curve)
        {
            if (curve == null) return 0;

            int hash = 0;
            var keys = curve.keys;
            // Add curve length to hash to catch empty curves
            hash ^= keys.Length.GetHashCode();

            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                hash ^= key.time.GetHashCode();
                hash ^= key.value.GetHashCode();
                hash ^= key.inTangent.GetHashCode();
                hash ^= key.outTangent.GetHashCode();
            }

            return hash;
        }

        public static AnimationCurve CloneCurve(this AnimationCurve curve)
        {
            if (curve == null) return null;

            var clone = new AnimationCurve(curve.keys)
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode
            };

            return clone;
        }
    }

}
