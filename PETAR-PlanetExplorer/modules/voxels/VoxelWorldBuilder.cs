using System;
using System.Threading;
using System.Threading.Tasks;
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
        private const float CaveNoiseXYScale = 0.075f;
        private const float CaveNoiseZScale = 0.11f;
        private const float CaveSeedXOffsetScale = 0.00825f;
        private const float CaveSeedYOffsetScale = -0.00525f;
        private const float CaveSeedZOffsetScale = 0.0055f;
        private const int CaveNoiseSeedSalt = unchecked((int)0x51f15e23);

        public static VoxelWorld CreateFromHeightMap(ProceduralWorldMap worldMap)
        {
            return CreateFromHeightMap(worldMap, null);
        }

        public static VoxelWorld CreateFromHeightMap(ProceduralWorldMap worldMap, Action<float, string> progressCallback)
        {
            var voxelWorld = new VoxelWorld();
            if (worldMap == null)
            {
                return voxelWorld;
            }

            SyncRegionFromHeightMap(voxelWorld, worldMap, 0, worldMap.Width - 1, 0, worldMap.Height - 1, progressCallback, 0f, 1f, "Calculating voxel chunks");
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
            SyncRegionFromHeightMap(voxelWorld, worldMap, minWorldX, maxWorldX, minWorldY, maxWorldY, null, 0f, 1f, "Calculating voxel chunks");
        }

        public static void SyncRegionFromHeightMap(VoxelWorld voxelWorld, ProceduralWorldMap worldMap, int minWorldX, int maxWorldX, int minWorldY, int maxWorldY, Action<float, string> progressCallback, float progressStart, float progressRange, string progressStatus)
        {
            if (voxelWorld == null || worldMap == null)
            {
                return;
            }

            var startX = Math.Max(0, Math.Min(minWorldX, maxWorldX));
            var endX = Math.Min(worldMap.Width - 1, Math.Max(minWorldX, maxWorldX));
            var startY = Math.Max(0, Math.Min(minWorldY, maxWorldY));
            var endY = Math.Min(worldMap.Height - 1, Math.Max(minWorldY, maxWorldY));
            var totalRows = Math.Max(1, endY - startY + 1);
            var startChunkY = FloorDiv(startY, VoxelConstants.ChunkSize);
            var endChunkY = FloorDiv(endY, VoxelConstants.ChunkSize);
            var partialWorlds = new VoxelWorld[(endChunkY - startChunkY) + 1];
            long rowsCompleted = 0;

            Parallel.For(
                startChunkY,
                endChunkY + 1,
                chunkY =>
                {
                    var localWorld = new VoxelWorld();
                    var bandStartY = Math.Max(startY, chunkY * VoxelConstants.ChunkSize);
                    var bandEndY = Math.Min(endY, ((chunkY + 1) * VoxelConstants.ChunkSize) - 1);
                    for (var worldY = bandStartY; worldY <= bandEndY; worldY++)
                    {
                        for (var worldX = startX; worldX <= endX; worldX++)
                        {
                            SyncColumn(localWorld, worldMap, worldX, worldY);
                        }

                        ReportProgress(progressCallback, progressStart, progressRange, Interlocked.Increment(ref rowsCompleted), totalRows, progressStatus);
                    }

                    partialWorlds[chunkY - startChunkY] = localWorld;
                });

            foreach (var partialWorld in partialWorlds)
            {
                if (partialWorld != null)
                {
                    voxelWorld.AdoptChunksFrom(partialWorld);
                }
            }
        }

        private static void SyncColumn(VoxelWorld voxelWorld, ProceduralWorldMap worldMap, int worldX, int worldY)
        {
            var surfaceHeight = MathHelper.Max(worldMap.SampleVoxelHeight(worldX, worldY), ProceduralWorldMap.SeaLevel);
            var topVoxel = GetSurfaceTopVoxel(surfaceHeight);
            var topMaterial = ResolveTopMaterial(worldMap, topVoxel);
            var topBlock = new VoxelBlock(topMaterial);
            var soilBlock = new VoxelBlock(VoxelMaterial.Soil);
            var rockBlock = new VoxelBlock(VoxelMaterial.Rock);
            var airBlock = new VoxelBlock(VoxelMaterial.Air);
            var seed = worldMap.Seed;
            var caveBandMinZ = topVoxel * CaveBandStart;
            var caveBandMaxZ = topVoxel * CaveBandEnd;
            var caveSampleX = (worldX * CaveNoiseXYScale) + (seed * CaveSeedXOffsetScale);
            var caveSampleY = (worldY * CaveNoiseXYScale) + (seed * CaveSeedYOffsetScale);
            var caveSampleZOffset = seed * CaveSeedZOffsetScale;
            var caveSeed = seed ^ CaveNoiseSeedSalt;
            var soilStartZ = Math.Max(0, topVoxel - 2);
            var caveStartZ = topVoxel >= 10 ? Math.Max(5, (int)MathF.Ceiling(caveBandMinZ)) : 1;
            var caveEndZ = topVoxel >= 10 ? Math.Min(soilStartZ - 1, (int)MathF.Floor(caveBandMaxZ)) : 0;
            var chunkX = FloorDiv(worldX, VoxelConstants.ChunkSize);
            var chunkY = FloorDiv(worldY, VoxelConstants.ChunkSize);
            var localX = Mod(worldX, VoxelConstants.ChunkSize);
            var localY = Mod(worldY, VoxelConstants.ChunkSize);
            var currentChunk = voxelWorld.GetOrCreateChunkForBulkUpdate(new VoxelChunkKey(chunkX, chunkY, 0));
            var localZ = 0;
            var worldZ = 0;
            for (; worldZ < soilStartZ; worldZ++)
            {
                if (localZ == VoxelConstants.ChunkSize)
                {
                    currentChunk.MarkDirty();
                    currentChunk = voxelWorld.GetOrCreateChunkForBulkUpdate(new VoxelChunkKey(chunkX, chunkY, worldZ / VoxelConstants.ChunkSize));
                    localZ = 0;
                }

                var block = worldZ >= caveStartZ && worldZ <= caveEndZ && ValueNoise3D(caveSampleX, caveSampleY, (worldZ * CaveNoiseZScale) + caveSampleZOffset, caveSeed) > CaveThreshold
                    ? airBlock
                    : rockBlock;
                currentChunk.SetBlockUnchecked(localX, localY, localZ, block);
                localZ++;
            }

            for (; worldZ < topVoxel; worldZ++)
            {
                if (localZ == VoxelConstants.ChunkSize)
                {
                    currentChunk.MarkDirty();
                    currentChunk = voxelWorld.GetOrCreateChunkForBulkUpdate(new VoxelChunkKey(chunkX, chunkY, worldZ / VoxelConstants.ChunkSize));
                    localZ = 0;
                }

                currentChunk.SetBlockUnchecked(localX, localY, localZ, soilBlock);
                localZ++;
            }

            if (worldZ < VoxelConstants.WorldHeight)
            {
                if (localZ == VoxelConstants.ChunkSize)
                {
                    currentChunk.MarkDirty();
                    currentChunk = voxelWorld.GetOrCreateChunkForBulkUpdate(new VoxelChunkKey(chunkX, chunkY, worldZ / VoxelConstants.ChunkSize));
                    localZ = 0;
                }

                currentChunk.SetBlockUnchecked(localX, localY, localZ, topBlock);
                localZ++;
                worldZ++;
            }

            for (; worldZ < VoxelConstants.WorldHeight; worldZ++)
            {
                if (localZ == VoxelConstants.ChunkSize)
                {
                    currentChunk.MarkDirty();
                    currentChunk = voxelWorld.GetOrCreateChunkForBulkUpdate(new VoxelChunkKey(chunkX, chunkY, worldZ / VoxelConstants.ChunkSize));
                    localZ = 0;
                }

                currentChunk.SetBlockUnchecked(localX, localY, localZ, airBlock);
                localZ++;
            }

            currentChunk.MarkDirty();
        }

        private static VoxelMaterial ResolveMaterial(int worldZ, int topVoxel, VoxelMaterial topMaterial, float caveBandMinZ, float caveBandMaxZ, float caveSampleX, float caveSampleY, float caveSampleZOffset, int caveSeed)
        {
            if (worldZ > topVoxel)
            {
                return VoxelMaterial.Air;
            }

            if (ShouldCarveCave(worldZ, topVoxel, caveBandMinZ, caveBandMaxZ, caveSampleX, caveSampleY, caveSampleZOffset, caveSeed))
            {
                return VoxelMaterial.Air;
            }

            if (worldZ == topVoxel)
            {
                return topMaterial;
            }

            if (worldZ >= topVoxel - 2)
            {
                return VoxelMaterial.Soil;
            }

            return VoxelMaterial.Rock;
        }

        private static VoxelMaterial ResolveTopMaterial(ProceduralWorldMap worldMap, int topVoxel)
        {
            var normalizedHeight = topVoxel / (float)(LandscapeHeightLimit - 1);
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

        private static bool ShouldCarveCave(int worldZ, int topVoxel, float caveBandMinZ, float caveBandMaxZ, float caveSampleX, float caveSampleY, float caveSampleZOffset, int caveSeed)
        {
            if (topVoxel < 10 || worldZ >= topVoxel - 2 || worldZ <= 4)
            {
                return false;
            }

            if (worldZ < caveBandMinZ || worldZ > caveBandMaxZ)
            {
                return false;
            }

            var caveNoise = ValueNoise3D(caveSampleX, caveSampleY, (worldZ * CaveNoiseZScale) + caveSampleZOffset, caveSeed);
            return caveNoise > CaveThreshold;
        }

        private static float ValueNoise3D(float x, float y, float z, int seed)
        {
            var x0 = (int)MathF.Floor(x);
            var y0 = (int)MathF.Floor(y);
            var z0 = (int)MathF.Floor(z);
            var x1 = x0 + 1;
            var y1 = y0 + 1;
            var z1 = z0 + 1;
            var tx = x - x0;
            var ty = y - y0;
            var tz = z - z0;
            var sx = SmoothStep(tx);
            var sy = SmoothStep(ty);
            var sz = SmoothStep(tz);

            var n000 = HashToUnitFloat(x0, y0, z0, seed);
            var n100 = HashToUnitFloat(x1, y0, z0, seed);
            var n010 = HashToUnitFloat(x0, y1, z0, seed);
            var n110 = HashToUnitFloat(x1, y1, z0, seed);
            var n001 = HashToUnitFloat(x0, y0, z1, seed);
            var n101 = HashToUnitFloat(x1, y0, z1, seed);
            var n011 = HashToUnitFloat(x0, y1, z1, seed);
            var n111 = HashToUnitFloat(x1, y1, z1, seed);

            var ix00 = n000 + ((n100 - n000) * sx);
            var ix10 = n010 + ((n110 - n010) * sx);
            var ix01 = n001 + ((n101 - n001) * sx);
            var ix11 = n011 + ((n111 - n011) * sx);
            var iy0 = ix00 + ((ix10 - ix00) * sy);
            var iy1 = ix01 + ((ix11 - ix01) * sy);
            return iy0 + ((iy1 - iy0) * sz);
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

        private static int FloorDiv(int value, int divisor)
        {
            var quotient = value / divisor;
            var remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int Mod(int value, int modulus)
        {
            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static void ReportProgress(Action<float, string> progressCallback, float progressStart, float progressRange, long completed, long total, string status)
        {
            if (progressCallback == null)
            {
                return;
            }

            var progress = progressStart + (progressRange * (completed / (float)Math.Max(1L, total)));
            progressCallback(MathHelper.Clamp(progress, 0f, 1f), status);
        }
    }
}
