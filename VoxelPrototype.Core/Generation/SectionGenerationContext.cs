using VoxelPrototype.Core.Materials;
using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Generation;

public sealed record SectionGenerationContext(
    int Seed,
    VoxelPrototypeWorldOptions Options,
    VoxelMaterialTable Materials,
    SectionAddress Address);
