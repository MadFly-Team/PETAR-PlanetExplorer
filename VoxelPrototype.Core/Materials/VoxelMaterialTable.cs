namespace VoxelPrototype.Core.Materials;

public sealed class VoxelMaterialTable
{
    private readonly Dictionary<byte, VoxelMaterial> _materials = new();

    public void Register(VoxelMaterial material)
    {
        _materials[material.Id] = material;
    }

    public bool TryGet(byte id, out VoxelMaterial material)
    {
        return _materials.TryGetValue(id, out material);
    }

    public IReadOnlyCollection<VoxelMaterial> GetAll()
    {
        return _materials.Values;
    }
}
