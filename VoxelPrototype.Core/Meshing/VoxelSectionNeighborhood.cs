using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Meshing;

public sealed record VoxelSectionNeighborhood(
    VoxelSection Center,
    VoxelSection? NegativeX,
    VoxelSection? PositiveX,
    VoxelSection? NegativeY,
    VoxelSection? PositiveY,
    VoxelSection? NegativeZ,
    VoxelSection? PositiveZ);
