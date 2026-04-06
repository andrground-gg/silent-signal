using UnityEngine;
using UnityEngine.Splines;

namespace GruelTerraSplines
{
    /// <summary>
    /// Cache key that includes both terrain and spline container to ensure terrain-specific caching
    /// </summary>
    public struct TerrainSplineCacheKey
    {
        public Terrain terrain;
        public SplineContainer container;

        public TerrainSplineCacheKey(Terrain terrain, SplineContainer container)
        {
            this.terrain = terrain;
            this.container = container;
        }

        public override bool Equals(object obj)
        {
            if (obj is TerrainSplineCacheKey other)
            {
                return terrain == other.terrain && container == other.container;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (terrain != null ? terrain.GetInstanceID() : 0);
            hash = hash * 31 + (container != null ? container.GetInstanceID() : 0);
            return hash;
        }

        public static bool operator ==(TerrainSplineCacheKey left, TerrainSplineCacheKey right)
        {
            return left.terrain == right.terrain && left.container == right.container;
        }

        public static bool operator !=(TerrainSplineCacheKey left, TerrainSplineCacheKey right)
        {
            return !(left == right);
        }
    }
}
