using System.Numerics;

namespace VoxelPrototype.Core.Meshing;

public readonly record struct VoxelVertex(Vector3 Position, Vector3 Normal, byte MaterialId);
