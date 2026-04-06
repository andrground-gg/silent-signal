using System.Collections.Generic;
using UnityEngine;

namespace GruelTerraSplines
{
    public static class TerrainCoordinates
    {
        public static int WorldToHeightmapX(Terrain terrain, float worldX)
        {
            var td = terrain.terrainData;
            float rel = (worldX - terrain.transform.position.x) / td.size.x;
            return Mathf.Clamp(Mathf.RoundToInt(rel * (td.heightmapResolution - 1)), 0, td.heightmapResolution - 1);
        }

        public static int WorldToHeightmapZ(Terrain terrain, float worldZ)
        {
            var td = terrain.terrainData;
            float rel = (worldZ - terrain.transform.position.z) / td.size.z;
            return Mathf.Clamp(Mathf.RoundToInt(rel * (td.heightmapResolution - 1)), 0, td.heightmapResolution - 1);
        }

        public static float WorldYToNormalizedHeight(Terrain terrain, float worldY)
        {
            var td = terrain.terrainData;
            return Mathf.InverseLerp(terrain.transform.position.y, terrain.transform.position.y + td.size.y, worldY);
        }

        public static Vector2Int WorldToHeightmap(Terrain terrain, Vector3 worldPos)
        {
            return new Vector2Int(
                WorldToHeightmapX(terrain, worldPos.x),
                WorldToHeightmapZ(terrain, worldPos.z)
            );
        }

        public static Vector3 HeightmapToWorld(Terrain terrain, int hx, int hz)
        {
            var td = terrain.terrainData;
            float x = terrain.transform.position.x + ((float)hx / (td.heightmapResolution - 1)) * td.size.x;
            float z = terrain.transform.position.z + ((float)hz / (td.heightmapResolution - 1)) * td.size.z;
            float y = 0f; // height not needed here
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Convert world X coordinate to heightmap X coordinate without clamping (for bounds calculation)
        /// </summary>
        public static float WorldToHeightmapXUnclamped(Terrain terrain, float worldX)
        {
            var td = terrain.terrainData;
            float rel = (worldX - terrain.transform.position.x) / td.size.x;
            return rel * (td.heightmapResolution - 1);
        }

        /// <summary>
        /// Convert world Z coordinate to heightmap Z coordinate without clamping (for bounds calculation)
        /// </summary>
        public static float WorldToHeightmapZUnclamped(Terrain terrain, float worldZ)
        {
            var td = terrain.terrainData;
            float rel = (worldZ - terrain.transform.position.z) / td.size.z;
            return rel * (td.heightmapResolution - 1);
        }

        public static float MetersToHeightmapPixels(Terrain terrain, float meters)
        {
            var td = terrain.terrainData;
            float pxX = td.heightmapResolution / td.size.x;
            float pxZ = td.heightmapResolution / td.size.z;
            return meters * 0.5f * (pxX + pxZ);
        }

        public static float WorldMetersPerPixel(Terrain terrain)
        {
            var td = terrain.terrainData;
            return (td.size.x / td.heightmapResolution + td.size.z / td.heightmapResolution) * 0.5f;
        }

        /// <summary>
        /// Get the world-space bounds of a terrain
        /// </summary>
        public static Bounds GetTerrainBounds(Terrain terrain)
        {
            var td = terrain.terrainData;
            var pos = terrain.transform.position;
            return new Bounds(
                pos + td.size * 0.5f,
                td.size
            );
        }

        /// <summary>
        /// Get all terrains that overlap with a world position and radius
        /// </summary>
        public static List<Terrain> GetOverlappingTerrains(Vector3 worldPos, float radius, List<Terrain> terrains)
        {
            var result = new List<Terrain>();
            var bounds = new Bounds(worldPos, Vector3.one * radius * 2f);
            
            foreach (var terrain in terrains)
            {
                if (terrain == null) continue;
                var terrainBounds = GetTerrainBounds(terrain);
                if (bounds.Intersects(terrainBounds))
                {
                    result.Add(terrain);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Get all terrains that overlap with world-space bounds
        /// </summary>
        public static List<Terrain> GetOverlappingTerrains(Bounds worldBounds, List<Terrain> terrains)
        {
            var result = new List<Terrain>();
            
            foreach (var terrain in terrains)
            {
                if (terrain == null) continue;
                var terrainBounds = GetTerrainBounds(terrain);
                if (worldBounds.Intersects(terrainBounds))
                {
                    result.Add(terrain);
                }
            }
            
            return result;
        }
    }
}