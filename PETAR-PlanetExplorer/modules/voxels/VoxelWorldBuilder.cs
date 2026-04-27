using System;
using Microsoft.Xna.Framework;
using PETAR_PlanetExplorer.Modules.Maps;

namespace PETAR_PlanetExplorer.Modules.Voxels
{
    public static class VoxelWorldBuilder
    {
        public const int LandscapeHeightLimit = 48;

        private const float CaveBandStart = 0.18f;
        private const float CaveBandEnd = 0.82f;
        private const float CaveThreshold = 0.76f;

        public static VoxelWorld CreateFromHeightMap(ProceduralWorldMap worldMap)
        {
            var voxelWorld = new VoxelWorld();
            if (worldMap == null)
            {
                return voxelWorld;
            }

            SyncRegionFromHeightMap(voxelWorld, worldMap, 0, worldMap.Width - 1, 0, worldMap.Height - 1);
            return voxelWorld;
        }

        public static VoxelWorld CreateRegionFromHeightMap(ProceduralWorldMap worldMap, int minWorldX, int maxWorldX, int minWorldY, int maxWorldY)
        {
            var voxelWorld = new VoxelWorld();
            if (worldMap == null)
            {
                return voxelWorld;
            }

            SyncRegionFromHeightMap(voxelWorld, worldMap, minWorldX, maxWorldX, minWorldY, maxWorldY);
            return voxelWorld;
        }

        public static void SyncRegionFromHeightMap(VoxelWorld voxelWorld, ProceduralWorldMap worldMap, int minWorldX, int maxWorldX, int minWorldY, int maxWorldY)
        {
            if (voxelWorld == null || worldMap == null)
            {
                return;
            }

            var startX = Math.Max(0, Math.Min(minWorldX, maxWorldX));
            var endX = Math.Min(worldMap.Width - 1, Math.Max(minWorldX, maxWorldX));
            var startY = Math.Max(0, Math.Min(minWorldY, maxWorldY));
            var endY = Math.Min(worldMap.Height - 1, Math.Max(minWorldY, maxWorldY));

            for (var worldY = 0; worldY < worldMap.Height; worldY++)
            {
                if (worldY < startY || worldY > endY)
                {
                    continue;
                }

                for (var worldX = 0; worldX < worldMap.Width; worldX++)
                {
                    if (worldX < startX || worldX > endX)
                    {
                        continue;
                    }

                    SyncColumn(voxelWorld, worldMap, worldX, worldY);
                }
            }
        }

        private static void SyncColumn(VoxelWorld voxelWorld, ProceduralWorldMap worldMap, int worldX, int worldY)
        {
            var surfaceHeight = MathHelper.Max(worldMap.SampleVoxelHeight(worldX, worldY), ProceduralWorldMap.SeaLevel);
            var topVoxel = GetSurfaceTopVoxel(surfaceHeight);
            for (var worldZ = 0; worldZ < VoxelConstants.WorldHeight; worldZ++)
            {
                var material = ResolveMaterial(worldMap, worldX, worldY, worldZ, topVoxel);
                voxelWorld.SetBlock(worldX, worldY, worldZ, new VoxelBlock(material));
            }
        }

        private static VoxelMaterial ResolveMaterial(ProceduralWorldMap worldMap, int worldX, int worldY, int worldZ, int topVoxel)
        {
            if (worldZ > topVoxel)
            {
                return VoxelMaterial.Air;
            }

            if (ShouldCarveCave(worldMap, worldX, worldY, worldZ, topVoxel))
            {
                return VoxelMaterial.Air;
            }

            if (worldZ == topVoxel)
            {
                var normalizedHeight = topVoxel / (float)Math.Max(1, LandscapeHeightLimit - 1);
                if (normalizedHeight > 0.82f)
                {
                    return VoxelMaterial.Snow;
                }

                if (normalizedHeight <= ProceduralWorldMap.SeaLevel + 0.02f)
                {
                    return VoxelMaterial.Sand;
                }

                return worldMap.Theme.HasTrees ? VoxelMaterial.Grass : VoxelMaterial.Rock;
            }

            if (worldZ >= topVoxel - 2)
            {
                return VoxelMaterial.Soil;
            }

            return VoxelMaterial.Rock;
        }

        private static bool ShouldCarveCave(ProceduralWorldMap worldMap, int worldX, int worldY, int worldZ, int topVoxel)
        {
            if (topVoxel < 10 || worldZ >= topVoxel - 2 || worldZ <= 4)
            {
                return false;
            }

            var heightRatio = worldZ / (float)Math.Max(1, topVoxel);
            if (heightRatio < CaveBandStart || heightRatio > CaveBandEnd)
            {
                return false;
            }

            var caveNoise = ValueNoise3D(
                (worldX + (worldMap.Seed * 0.11f)) * 0.075f,
                (worldY - (worldMap.Seed * 0.07f)) * 0.075f,
                (worldZ + (worldMap.Seed * 0.05f)) * 0.11f,
                worldMap.Seed ^ unchecked((int)0x51f15e23));
            return caveNoise > CaveThreshold;
        }

        private static float ValueNoise3D(float x, float y, float z, int seed)
        {
            var x0 = (int)MathF.Floor(x);
            var y0 = (int)MathF.Floor(y);
            var z0 = (int)MathF.Floor(z);
            var tx = x - x0;
            var ty = y - y0;
            var tz = z - z0;

            var n000 = HashToUnitFloat(x0, y0, z0, seed);
            var n100 = HashToUnitFloat(x0 + 1, y0, z0, seed);
            var n010 = HashToUnitFloat(x0, y0 + 1, z0, seed);
            var n110 = HashToUnitFloat(x0 + 1, y0 + 1, z0, seed);
            var n001 = HashToUnitFloat(x0, y0, z0 + 1, seed);
            var n101 = HashToUnitFloat(x0 + 1, y0, z0 + 1, seed);
            var n011 = HashToUnitFloat(x0, y0 + 1, z0 + 1, seed);
            var n111 = HashToUnitFloat(x0 + 1, y0 + 1, z0 + 1, seed);

            var ix00 = MathHelper.Lerp(n000, n100, SmoothStep(tx));
            var ix10 = MathHelper.Lerp(n010, n110, SmoothStep(tx));
            var ix01 = MathHelper.Lerp(n001, n101, SmoothStep(tx));
            var ix11 = MathHelper.Lerp(n011, n111, SmoothStep(tx));
            var iy0 = MathHelper.Lerp(ix00, ix10, SmoothStep(ty));
            var iy1 = MathHelper.Lerp(ix01, ix11, SmoothStep(ty));
            return MathHelper.Lerp(iy0, iy1, SmoothStep(tz));
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3f - (2f * value));
        }

        private static float HashToUnitFloat(int x, int y, int z, int seed)
        {
            var hash = seed;
            hash ^= x * 374761393;
            hash = (hash << 13) ^ hash;
            hash ^= y * 668265263;
            hash = (hash << 11) ^ hash;
            hash ^= z * 2147483647;
            hash = (hash << 7) ^ hash;
            return ((hash & 0x7fffffff) / (float)int.MaxValue);
        }

        public static int GetSurfaceTopVoxel(float surfaceHeight)
        {
            return Math.Clamp((int)MathF.Round(surfaceHeight * (LandscapeHeightLimit - 1)), 1, LandscapeHeightLimit - 1);
        }

        public static float GetRenderVerticalOrigin()
        {
            return ProceduralWorldMap.SeaLevel * (LandscapeHeightLimit - 1f);
        }
    }
}
