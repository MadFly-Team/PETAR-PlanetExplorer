namespace VoxelPrototype.Core.Meshing;

public sealed class VoxelMeshData
{
    public List<VoxelVertex> Vertices { get; } = new();

    public List<int> Indices { get; } = new();

    public int EmittedFaceCount { get; set; }

    public void Clear()
    {
        Vertices.Clear();
        Indices.Clear();
        EmittedFaceCount = 0;
    }
}
