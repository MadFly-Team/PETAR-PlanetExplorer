using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Materials;

public static class VoxelPrototypeMaterialLibrary
{
    public static VoxelMaterialTable CreateDefault()
    {
        var table = new VoxelMaterialTable();
        table.Register(new VoxelMaterial(VoxelMaterialIds.Air, "Air", false, false, false, 0x00000000));
        table.Register(new VoxelMaterial(VoxelMaterialIds.Grass, "Grass", true, true, false, 0xFF63B54B));
        table.Register(new VoxelMaterial(VoxelMaterialIds.Dirt, "Dirt", true, true, false, 0xFF7A5230));
        table.Register(new VoxelMaterial(VoxelMaterialIds.Stone, "Stone", true, true, false, 0xFF7B7F86));
        table.Register(new VoxelMaterial(VoxelMaterialIds.Sand, "Sand", true, true, false, 0xFFD4C08A));
        table.Register(new VoxelMaterial(VoxelMaterialIds.Basalt, "Basalt", true, true, false, 0xFF45484E));
        return table;
    }

    public static VoxelBlock CreateBlock(VoxelMaterialTable materials, byte materialId)
    {
        if (!materials.TryGet(materialId, out var material))
        {
            throw new InvalidOperationException($"Material id {materialId} is not registered.");
        }

        var flags = VoxelBlockFlags.None;
        if (material.IsSolid)
        {
            flags |= VoxelBlockFlags.Solid;
        }

        if (material.IsOpaque)
        {
            flags |= VoxelBlockFlags.Opaque;
        }

        if (material.IsEmissive)
        {
            flags |= VoxelBlockFlags.Emissive;
        }

        return new VoxelBlock(materialId, flags);
    }
}
