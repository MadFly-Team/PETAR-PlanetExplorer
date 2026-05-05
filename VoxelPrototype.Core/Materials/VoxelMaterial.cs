namespace VoxelPrototype.Core.Materials;

public readonly record struct VoxelMaterial(
    byte Id,
    string Name,
    bool IsSolid,
    bool IsOpaque,
    bool IsEmissive,
    uint PackedColor);
