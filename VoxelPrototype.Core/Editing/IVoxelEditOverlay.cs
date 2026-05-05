using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Editing;

public interface IVoxelEditOverlay
{
    void Apply(VoxelSection section);
}
