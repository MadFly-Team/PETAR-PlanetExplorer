using System.Numerics;
using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Meshing;

public sealed class CoarseSectionMesher
{
    private const int LodFactor = 2;

    public VoxelMeshData BuildMesh(VoxelSectionNeighborhood neighborhood)
    {
        ArgumentNullException.ThrowIfNull(neighborhood);
        ArgumentNullException.ThrowIfNull(neighborhood.Center);

        var center = neighborhood.Center;
        var coarseSizeX = center.SizeX / LodFactor;
        var coarseSizeY = center.SizeY / LodFactor;
        var coarseSizeZ = center.SizeZ / LodFactor;
        var maxX = coarseSizeX - 1;
        var maxY = coarseSizeY - 1;
        var maxZ = coarseSizeZ - 1;
        var coarseBlocks = new CoarseVoxel[coarseSizeX * coarseSizeY * coarseSizeZ];
        var anySolid = false;

        for (var coarseZ = 0; coarseZ < coarseSizeZ; coarseZ++)
        {
            for (var coarseY = 0; coarseY < coarseSizeY; coarseY++)
            {
                for (var coarseX = 0; coarseX < coarseSizeX; coarseX++)
                {
                    var coarseBlock = ResolveCoarseVoxel(center, coarseX, coarseY, coarseZ);
                    coarseBlocks[GetIndex(coarseX, coarseY, coarseZ, coarseSizeX, coarseSizeY)] = coarseBlock;
                    anySolid |= coarseBlock.IsSolid;
                }
            }
        }

        var mesh = new VoxelMeshData();
        if (!anySolid)
        {
            return mesh;
        }

        for (var coarseZ = 0; coarseZ < coarseSizeZ; coarseZ++)
        {
            for (var coarseY = 0; coarseY < coarseSizeY; coarseY++)
            {
                for (var coarseX = 0; coarseX < coarseSizeX; coarseX++)
                {
                    var coarseBlock = coarseBlocks[GetIndex(coarseX, coarseY, coarseZ, coarseSizeX, coarseSizeY)];
                    if (!coarseBlock.IsSolid)
                    {
                        continue;
                    }

                    if (coarseX > 0 && coarseX < maxX && coarseY > 0 && coarseY < maxY && coarseZ > 0 && coarseZ < maxZ)
                    {
                        if (!coarseBlocks[GetIndex(coarseX - 1, coarseY, coarseZ, coarseSizeX, coarseSizeY)].IsOpaque)
                        {
                            AppendNegativeXFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                        }

                        if (!coarseBlocks[GetIndex(coarseX + 1, coarseY, coarseZ, coarseSizeX, coarseSizeY)].IsOpaque)
                        {
                            AppendPositiveXFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                        }

                        if (!coarseBlocks[GetIndex(coarseX, coarseY - 1, coarseZ, coarseSizeX, coarseSizeY)].IsOpaque)
                        {
                            AppendNegativeYFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                        }

                        if (!coarseBlocks[GetIndex(coarseX, coarseY + 1, coarseZ, coarseSizeX, coarseSizeY)].IsOpaque)
                        {
                            AppendPositiveYFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                        }

                        if (!coarseBlocks[GetIndex(coarseX, coarseY, coarseZ - 1, coarseSizeX, coarseSizeY)].IsOpaque)
                        {
                            AppendNegativeZFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                        }

                        if (!coarseBlocks[GetIndex(coarseX, coarseY, coarseZ + 1, coarseSizeX, coarseSizeY)].IsOpaque)
                        {
                            AppendPositiveZFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                        }

                        continue;
                    }

                    if (!IsNeighborOpaque(coarseBlocks, coarseSizeX, coarseSizeY, coarseSizeZ, coarseX - 1, coarseY, coarseZ))
                    {
                        AppendNegativeXFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                    }

                    if (!IsNeighborOpaque(coarseBlocks, coarseSizeX, coarseSizeY, coarseSizeZ, coarseX + 1, coarseY, coarseZ))
                    {
                        AppendPositiveXFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                    }

                    if (!IsNeighborOpaque(coarseBlocks, coarseSizeX, coarseSizeY, coarseSizeZ, coarseX, coarseY - 1, coarseZ))
                    {
                        AppendNegativeYFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                    }

                    if (!IsNeighborOpaque(coarseBlocks, coarseSizeX, coarseSizeY, coarseSizeZ, coarseX, coarseY + 1, coarseZ))
                    {
                        AppendPositiveYFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                    }

                    if (!IsNeighborOpaque(coarseBlocks, coarseSizeX, coarseSizeY, coarseSizeZ, coarseX, coarseY, coarseZ - 1))
                    {
                        AppendNegativeZFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                    }

                    if (!IsNeighborOpaque(coarseBlocks, coarseSizeX, coarseSizeY, coarseSizeZ, coarseX, coarseY, coarseZ + 1))
                    {
                        AppendPositiveZFace(mesh, coarseX, coarseY, coarseZ, coarseBlock.MaterialId);
                    }
                }
            }
        }

        return mesh;
    }

    private static CoarseVoxel ResolveCoarseVoxel(VoxelSection section, int coarseX, int coarseY, int coarseZ)
    {
        var startX = coarseX * LodFactor;
        var startY = coarseY * LodFactor;
        var startZ = coarseZ * LodFactor;
        for (var z = startZ; z < startZ + LodFactor; z++)
        {
            for (var y = startY; y < startY + LodFactor; y++)
            {
                for (var x = startX; x < startX + LodFactor; x++)
                {
                    var block = section.GetBlockUnchecked(x, y, z);
                    if (block.IsSolid)
                    {
                        return new CoarseVoxel(true, block.IsOpaque, block.MaterialId);
                    }
                }
            }
        }

        return default;
    }

    private static bool IsNeighborOpaque(CoarseVoxel[] coarseBlocks, int sizeX, int sizeY, int sizeZ, int x, int y, int z)
    {
        if (x < 0 || x >= sizeX || y < 0 || y >= sizeY || z < 0 || z >= sizeZ)
        {
            return false;
        }

        return coarseBlocks[GetIndex(x, y, z, sizeX, sizeY)].IsOpaque;
    }

    private static int GetIndex(int x, int y, int z, int sizeX, int sizeY)
    {
        return x + (y * sizeX) + (z * sizeX * sizeY);
    }

    private static void AppendNegativeXFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x * LodFactor, y * LodFactor, z * LodFactor),
            new Vector3(x * LodFactor, y * LodFactor, (z + 1) * LodFactor),
            new Vector3(x * LodFactor, (y + 1) * LodFactor, (z + 1) * LodFactor),
            new Vector3(x * LodFactor, (y + 1) * LodFactor, z * LodFactor),
            new Vector3(-1f, 0f, 0f),
            materialId);
    }

    private static void AppendPositiveXFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3((x + 1) * LodFactor, y * LodFactor, z * LodFactor),
            new Vector3((x + 1) * LodFactor, (y + 1) * LodFactor, z * LodFactor),
            new Vector3((x + 1) * LodFactor, (y + 1) * LodFactor, (z + 1) * LodFactor),
            new Vector3((x + 1) * LodFactor, y * LodFactor, (z + 1) * LodFactor),
            new Vector3(1f, 0f, 0f),
            materialId);
    }

    private static void AppendNegativeYFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x * LodFactor, y * LodFactor, z * LodFactor),
            new Vector3((x + 1) * LodFactor, y * LodFactor, z * LodFactor),
            new Vector3((x + 1) * LodFactor, y * LodFactor, (z + 1) * LodFactor),
            new Vector3(x * LodFactor, y * LodFactor, (z + 1) * LodFactor),
            new Vector3(0f, -1f, 0f),
            materialId);
    }

    private static void AppendPositiveYFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x * LodFactor, (y + 1) * LodFactor, z * LodFactor),
            new Vector3(x * LodFactor, (y + 1) * LodFactor, (z + 1) * LodFactor),
            new Vector3((x + 1) * LodFactor, (y + 1) * LodFactor, (z + 1) * LodFactor),
            new Vector3((x + 1) * LodFactor, (y + 1) * LodFactor, z * LodFactor),
            new Vector3(0f, 1f, 0f),
            materialId);
    }

    private static void AppendNegativeZFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x * LodFactor, y * LodFactor, z * LodFactor),
            new Vector3(x * LodFactor, (y + 1) * LodFactor, z * LodFactor),
            new Vector3((x + 1) * LodFactor, (y + 1) * LodFactor, z * LodFactor),
            new Vector3((x + 1) * LodFactor, y * LodFactor, z * LodFactor),
            new Vector3(0f, 0f, -1f),
            materialId);
    }

    private static void AppendPositiveZFace(VoxelMeshData mesh, int x, int y, int z, byte materialId)
    {
        AppendQuad(
            mesh,
            new Vector3(x * LodFactor, y * LodFactor, (z + 1) * LodFactor),
            new Vector3((x + 1) * LodFactor, y * LodFactor, (z + 1) * LodFactor),
            new Vector3((x + 1) * LodFactor, (y + 1) * LodFactor, (z + 1) * LodFactor),
            new Vector3(x * LodFactor, (y + 1) * LodFactor, (z + 1) * LodFactor),
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

    private readonly record struct CoarseVoxel(bool IsSolid, bool IsOpaque, byte MaterialId);
}