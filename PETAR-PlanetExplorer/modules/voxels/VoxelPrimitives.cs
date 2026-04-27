using System;

namespace PETAR_PlanetExplorer.Modules.Voxels
{
    public enum VoxelMaterial : byte
    {
        Air = 0,
        Rock = 1,
        Soil = 2,
        Grass = 3,
        Sand = 4,
        Snow = 5,
        Water = 6
    }

    public readonly record struct VoxelCoord(int X, int Y, int Z);

    public readonly record struct VoxelChunkKey(int X, int Y, int Z);

    public readonly record struct VoxelBlock(VoxelMaterial Material)
    {
        public bool IsSolid => Material != VoxelMaterial.Air;
    }

    public static class VoxelConstants
    {
        public const int ChunkSize = 32;
        public const int WorldHeight = 192;
    }
}
