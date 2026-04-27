using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Voxels
{
    public sealed class VoxelWorld
    {
        private readonly Dictionary<VoxelChunkKey, VoxelChunk> _chunks = new();

        public IReadOnlyDictionary<VoxelChunkKey, VoxelChunk> Chunks => _chunks;

        public VoxelChunk GetOrCreateChunk(VoxelChunkKey key)
        {
            if (_chunks.TryGetValue(key, out var chunk))
            {
                return chunk;
            }

            chunk = new VoxelChunk(key);
            _chunks[key] = chunk;
            return chunk;
        }

        public bool TryGetChunk(VoxelChunkKey key, out VoxelChunk chunk)
        {
            return _chunks.TryGetValue(key, out chunk);
        }

        public VoxelBlock GetBlock(int x, int y, int z)
        {
            if (z < 0 || z >= VoxelConstants.WorldHeight)
            {
                return default;
            }

            var chunkKey = new VoxelChunkKey(
                FloorDiv(x, VoxelConstants.ChunkSize),
                FloorDiv(y, VoxelConstants.ChunkSize),
                FloorDiv(z, VoxelConstants.ChunkSize));
            if (!_chunks.TryGetValue(chunkKey, out var chunk))
            {
                return default;
            }

            return chunk.GetBlock(
                Mod(x, VoxelConstants.ChunkSize),
                Mod(y, VoxelConstants.ChunkSize),
                Mod(z, VoxelConstants.ChunkSize));
        }

        public bool IsSolid(int x, int y, int z)
        {
            return GetBlock(x, y, z).IsSolid;
        }

        public bool SetBlock(int x, int y, int z, VoxelBlock block)
        {
            if (z < 0 || z >= VoxelConstants.WorldHeight)
            {
                return false;
            }

            var chunkKey = new VoxelChunkKey(
                FloorDiv(x, VoxelConstants.ChunkSize),
                FloorDiv(y, VoxelConstants.ChunkSize),
                FloorDiv(z, VoxelConstants.ChunkSize));
            var chunk = GetOrCreateChunk(chunkKey);
            var localX = Mod(x, VoxelConstants.ChunkSize);
            var localY = Mod(y, VoxelConstants.ChunkSize);
            var localZ = Mod(z, VoxelConstants.ChunkSize);
            var previousBlock = chunk.GetBlock(localX, localY, localZ);
            if (previousBlock.Equals(block))
            {
                return false;
            }

            chunk.SetBlock(localX, localY, localZ, block);
            MarkNeighborChunksDirty(chunkKey, localX, localY, localZ);
            return true;
        }

        public bool TryGetTopSolidVoxel(int x, int y, out int topZ)
        {
            for (var z = VoxelConstants.WorldHeight - 1; z >= 0; z--)
            {
                if (IsSolid(x, y, z))
                {
                    topZ = z;
                    return true;
                }
            }

            topZ = -1;
            return false;
        }

        public bool TryFindFirstSolidOnSegment(Vector3 start, Vector3 end, float stepSize, out VoxelCoord hitVoxel)
        {
            stepSize = Math.Max(0.05f, stepSize);
            var delta = end - start;
            var distance = delta.Length();
            if (distance <= 0.0001f)
            {
                var startVoxel = new VoxelCoord((int)MathF.Floor(start.X), (int)MathF.Floor(start.Y), (int)MathF.Floor(start.Z));
                if (IsSolid(startVoxel.X, startVoxel.Y, startVoxel.Z))
                {
                    hitVoxel = startVoxel;
                    return true;
                }

                hitVoxel = default;
                return false;
            }

            var steps = Math.Max(1, (int)MathF.Ceiling(distance / stepSize));
            for (var step = 0; step <= steps; step++)
            {
                var sample = Vector3.Lerp(start, end, step / (float)steps);
                var voxel = new VoxelCoord((int)MathF.Floor(sample.X), (int)MathF.Floor(sample.Y), (int)MathF.Floor(sample.Z));
                if (IsSolid(voxel.X, voxel.Y, voxel.Z))
                {
                    hitVoxel = voxel;
                    return true;
                }
            }

            hitVoxel = default;
            return false;
        }

        public bool HasAnySolidInBox(Vector3 min, Vector3 max)
        {
            var startX = (int)MathF.Floor(Math.Min(min.X, max.X));
            var endX = (int)MathF.Floor(Math.Max(min.X, max.X));
            var startY = (int)MathF.Floor(Math.Min(min.Y, max.Y));
            var endY = (int)MathF.Floor(Math.Max(min.Y, max.Y));
            var startZ = (int)MathF.Floor(Math.Min(min.Z, max.Z));
            var endZ = (int)MathF.Floor(Math.Max(min.Z, max.Z));

            for (var z = startZ; z <= endZ; z++)
            {
                for (var y = startY; y <= endY; y++)
                {
                    for (var x = startX; x <= endX; x++)
                    {
                        if (IsSolid(x, y, z))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool TryGetTopSolidBelow(int x, int y, int maxZ, out int topZ)
        {
            var searchStart = Math.Min(Math.Max(maxZ, 0), VoxelConstants.WorldHeight - 1);
            for (var z = searchStart; z >= 0; z--)
            {
                if (IsSolid(x, y, z))
                {
                    topZ = z;
                    return true;
                }
            }

            topZ = -1;
            return false;
        }

        public void MergeFrom(VoxelWorld source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var chunkEntry in source.Chunks)
            {
                var sourceChunk = chunkEntry.Value;
                var destinationChunk = GetOrCreateChunk(chunkEntry.Key);
                for (var z = 0; z < VoxelConstants.ChunkSize; z++)
                {
                    for (var y = 0; y < VoxelConstants.ChunkSize; y++)
                    {
                        for (var x = 0; x < VoxelConstants.ChunkSize; x++)
                        {
                            destinationChunk.SetBlock(x, y, z, sourceChunk.GetBlock(x, y, z));
                        }
                    }
                }
            }
        }

        private void MarkNeighborChunksDirty(VoxelChunkKey chunkKey, int localX, int localY, int localZ)
        {
            MarkChunkDirty(chunkKey.X - (localX == 0 ? 1 : 0), chunkKey.Y, chunkKey.Z);
            MarkChunkDirty(chunkKey.X + (localX == VoxelConstants.ChunkSize - 1 ? 1 : 0), chunkKey.Y, chunkKey.Z);
            MarkChunkDirty(chunkKey.X, chunkKey.Y - (localY == 0 ? 1 : 0), chunkKey.Z);
            MarkChunkDirty(chunkKey.X, chunkKey.Y + (localY == VoxelConstants.ChunkSize - 1 ? 1 : 0), chunkKey.Z);
            MarkChunkDirty(chunkKey.X, chunkKey.Y, chunkKey.Z - (localZ == 0 ? 1 : 0));
            MarkChunkDirty(chunkKey.X, chunkKey.Y, chunkKey.Z + (localZ == VoxelConstants.ChunkSize - 1 ? 1 : 0));
        }

        private void MarkChunkDirty(int chunkX, int chunkY, int chunkZ)
        {
            var key = new VoxelChunkKey(chunkX, chunkY, chunkZ);
            if (_chunks.TryGetValue(key, out var chunk))
            {
                chunk.MarkDirty();
            }
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
    }
}
