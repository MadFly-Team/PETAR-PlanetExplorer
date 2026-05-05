namespace VoxelPrototype.Core.World;

public readonly record struct VoxelBlock(byte MaterialId, VoxelBlockFlags Flags)
{
    public bool IsSolid => (Flags & VoxelBlockFlags.Solid) != 0;

    public bool IsOpaque => (Flags & VoxelBlockFlags.Opaque) != 0;

    public bool IsEmissive => (Flags & VoxelBlockFlags.Emissive) != 0;

    public static VoxelBlock Air => new(0, VoxelBlockFlags.None);
}
