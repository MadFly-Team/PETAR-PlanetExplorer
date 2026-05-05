using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Visibility;

public readonly record struct VisibleSection(SectionAddress Address, float DistanceSquared, bool RequiresMeshBuild);
