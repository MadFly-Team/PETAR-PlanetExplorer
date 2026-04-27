using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace PETAR_PlanetExplorer.Modules.Voxels
{
    public sealed class VoxelChunk
    {
        private readonly VoxelBlock[] _blocks;
        private readonly Dictionary<int, VertexPositionColor[]> _cachedVerticesByLod = new();

        public VoxelChunk(VoxelChunkKey key)
        {
            Key = key;
            _blocks = new VoxelBlock[VoxelConstants.ChunkSize * VoxelConstants.ChunkSize * VoxelConstants.ChunkSize];
            IsDirty = true;
        }

        public VoxelChunkKey Key { get; }

        public bool IsDirty { get; private set; }

        public VoxelBlock GetBlock(int x, int y, int z)
        {
            return _blocks[GetIndex(x, y, z)];
        }

        public void SetBlock(int x, int y, int z, VoxelBlock block)
        {
            if (_blocks[GetIndex(x, y, z)].Equals(block))
            {
                return;
            }

            _blocks[GetIndex(x, y, z)] = block;
            MarkDirty();
        }

        public void MarkDirty()
        {
            _cachedVerticesByLod.Clear();
            IsDirty = true;
        }

        public bool TryGetCachedMesh(int lodStep, out VertexPositionColor[] vertices)
        {
            return _cachedVerticesByLod.TryGetValue(Math.Max(1, lodStep), out vertices);
        }

        public void UpdateCachedMesh(int lodStep, VertexPositionColor[] vertices)
        {
            _cachedVerticesByLod[Math.Max(1, lodStep)] = vertices;
            IsDirty = false;
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        private static int GetIndex(int x, int y, int z)
        {
            return (z * VoxelConstants.ChunkSize * VoxelConstants.ChunkSize) + (y * VoxelConstants.ChunkSize) + x;
        }
    }
}
