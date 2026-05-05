using System.Numerics;
using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Meshing;

public sealed class BaselineSectionMesher : IVoxelSectionMesher
{
    public VoxelMeshData BuildMesh(VoxelSectionNeighborhood neighborhood)
    {
        ArgumentNullException.ThrowIfNull(neighborhood);
        ArgumentNullException.ThrowIfNull(neighborhood.Center);

        var center = neighborhood.Center;
        var sizeX = center.SizeX;
        var sizeY = center.SizeY;
        var sizeZ = center.SizeZ;
        var maxX = sizeX - 1;
        var maxY = sizeY - 1;
        var maxZ = sizeZ - 1;
        var mesh = new VoxelMeshData();
        if (center.SolidVoxelCount == 0)
        {
            return mesh;
        }

        for (var z = 0; z < sizeZ; z++)
        {
            for (var y = 0; y < sizeY; y++)
            {
                for (var x = 0; x < sizeX; x++)
                {
                    var block = center.GetBlockUnchecked(x, y, z);
                    if (!block.IsSolid)
                    {
                        continue;
                    }

                    if (x > 0 && x < maxX && y > 0 && y < maxY && z > 0 && z < maxZ)
                    {
                        if (!center.GetBlockUnchecked(x - 1, y, z).IsOpaque)
                        {
                            AppendNegativeXFace(mesh, x, y, z, block.MaterialId);
                        }

                        if (!center.GetBlockUnchecked(x + 1, y, z).IsOpaque)
                        {
                            AppendPositiveXFace(mesh, x, y, z, block.MaterialId);
                        }

                        if (!center.GetBlockUnchecked(x, y - 1, z).IsOpaque)
                        {
                            AppendNegativeYFace(mesh, x, y, z, block.MaterialId);
                        }

                        if (!center.GetBlockUnchecked(x, y + 1, z).IsOpaque)
                        {
                            AppendPositiveYFace(mesh, x, y, z, block.MaterialId);
                        }

                        if (!center.GetBlockUnchecked(x, y, z - 1).IsOpaque)
                        {
                            AppendNegativeZFace(mesh, x, y, z, block.MaterialId);
                        }

                        if (!center.GetBlockUnchecked(x, y, z + 1).IsOpaque)
                        {
                            AppendPositiveZFace(mesh, x, y, z, block.MaterialId);
                        }

                        continue;
                    }

                    if (!(x > 0
                        ? center.GetBlockUnchecked(x - 1, y, z).IsOpaque
                        : IsNegativeXBoundaryOpaque(neighborhood, y, z)))
                    {
                        AppendNegativeXFace(mesh, x, y, z, block.MaterialId);
                    }

                    if (!(x < maxX
                        ? center.GetBlockUnchecked(x + 1, y, z).IsOpaque
                        : IsPositiveXBoundaryOpaque(neighborhood, y, z)))
                    {
                        AppendPositiveXFace(mesh, x, y, z, block.MaterialId);
                    }

                    if (!(y > 0
                        ? center.GetBlockUnchecked(x, y - 1, z).IsOpaque
                        : IsNegativeYBoundaryOpaque(neighborhood, x, z)))
                    {
                        AppendNegativeYFace(mesh, x, y, z, block.MaterialId);
                    }

                    if (!(y < maxY
                        ? center.GetBlockUnchecked(x, y + 1, z).IsOpaque
                        : IsPositiveYBoundaryOpaque(neighborhood, x, z)))
                    {
                        AppendPositiveYFace(mesh, x, y, z, block.MaterialId);
                    }

                    if (!(z > 0
                        ? center.GetBlockUnchecked(x, y, z - 1).IsOpaque
                        : IsNegativeZBoundaryOpaque(neighborhood, x, y)))
                    {
                        AppendNegativeZFace(mesh, x, y, z, block.MaterialId);
                    }

                    if (!(z < maxZ
                        ? center.GetBlockUnchecked(x, y, z + 1).IsOpaque
                        : IsPositiveZBoundaryOpaque(neighborhood, x, y)))
                    {
                        AppendPositiveZFace(mesh, x, y, z, block.MaterialId);
                    }
                }
            }
        }

        return mesh;
    }

    private static bool IsNegativeXBoundaryOpaque(VoxelSectionNeighborhood neighborhood, int y, int z)
    {
        var section = neighborhood.NegativeX;
        return section is not null && section.GetBlockUnchecked(section.SizeX - 1, y, z).IsOpaque;
    }

    private static bool IsPositiveXBoundaryOpaque(VoxelSectionNeighborhood neighborhood, int y, int z)
    {
        var section = neighborhood.PositiveX;
        return section is not null && section.GetBlockUnchecked(0, y, z).IsOpaque;
    }

    private static bool IsNegativeYBoundaryOpaque(VoxelSectionNeighborhood neighborhood, int x, int z)
    {
        var section = neighborhood.NegativeY;
        return section is not null && section.GetBlockUnchecked(x, section.SizeY - 1, z).IsOpaque;
    }

    private static bool IsPositiveYBoundaryOpaque(VoxelSectionNeighborhood neighborhood, int x, int z)
    {
        var section = neighborhood.PositiveY;
        return section is not null && section.GetBlockUnchecked(x, 0, z).IsOpaque;
    }

    private static bool IsNegativeZBoundaryOpaque(VoxelSectionNeighborhood neighborhood, int x, int y)
    {
        var section = neighborhood.NegativeZ;
        return section is not null && section.GetBlockUnchecked(x, y, section.SizeZ - 1).IsOpaque;
    }

    private static bool IsPositiveZBoundaryOpaque(VoxelSectionNeighborhood neighborhood, int x, int y)
    {
        var section = neighborhood.PositiveZ;
        return section is not null && section.GetBlockUnchecked(x, y, 0).IsOpaque;
    }

    private static void AppendNegativeXFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x, y, z),
            new Vector3(x, y, z + 1),
            new Vector3(x, y + 1, z + 1),
            new Vector3(x, y + 1, z),
            new Vector3(-1f, 0f, 0f),
            materialId);
    }

    private static void AppendPositiveXFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x + 1, y, z),
            new Vector3(x + 1, y + 1, z),
            new Vector3(x + 1, y + 1, z + 1),
            new Vector3(x + 1, y, z + 1),
            new Vector3(1f, 0f, 0f),
            materialId);
    }

    private static void AppendNegativeYFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x, y, z),
            new Vector3(x + 1, y, z),
            new Vector3(x + 1, y, z + 1),
            new Vector3(x, y, z + 1),
            new Vector3(0f, -1f, 0f),
            materialId);
    }

    private static void AppendPositiveYFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x, y + 1, z),
            new Vector3(x, y + 1, z + 1),
            new Vector3(x + 1, y + 1, z + 1),
            new Vector3(x + 1, y + 1, z),
            new Vector3(0f, 1f, 0f),
            materialId);
    }

    private static void AppendNegativeZFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x, y, z),
            new Vector3(x, y + 1, z),
            new Vector3(x + 1, y + 1, z),
            new Vector3(x + 1, y, z),
            new Vector3(0f, 0f, -1f),
            materialId);
    }

    private static void AppendPositiveZFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x, y, z + 1),
            new Vector3(x + 1, y, z + 1),
            new Vector3(x + 1, y + 1, z + 1),
            new Vector3(x, y + 1, z + 1),
            new Vector3(0f, 0f, 1f),
            materialId);
    }

    private static void AppendQuad(VoxelMeshData mesh, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal, byte materialId)
    {
        var startIndex = mesh.Vertices.Count;
        mesh.Vertices.Add(new VoxelVertex(v0, normal, materialId));
        mesh.Vertices.Add(new VoxelVertex(v1, normal, materialId));
        mesh.Vertices.Add(new VoxelVertex(v2, normal, materialId));
        mesh.Vertices.Add(new VoxelVertex(v3, normal, materialId));

        mesh.Indices.Add(startIndex);
        mesh.Indices.Add(startIndex + 1);
        mesh.Indices.Add(startIndex + 2);
        mesh.Indices.Add(startIndex);
        mesh.Indices.Add(startIndex + 2);
        mesh.Indices.Add(startIndex + 3);
        mesh.EmittedFaceCount++;
    }
}
