namespace VoxelPrototype.Core.Meshing;

public interface IVoxelSectionMesher
{
    VoxelMeshData BuildMesh(VoxelSectionNeighborhood neighborhood);
}
