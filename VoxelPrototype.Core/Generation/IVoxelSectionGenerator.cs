using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Generation;

public interface IVoxelSectionGenerator
{
    void GenerateSection(SectionGenerationContext context, VoxelSection targetSection);
}
