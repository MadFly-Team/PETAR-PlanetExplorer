namespace VoxelPrototype.Core.World;

[Flags]
public enum VoxelBlockFlags : byte
{
    None = 0,
    Solid = 1 << 0,
    Opaque = 1 << 1,
    Emissive = 1 << 2
}
