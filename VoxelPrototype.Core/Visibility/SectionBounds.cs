using System.Numerics;
using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Visibility;

public readonly record struct SectionBounds(SectionAddress Address, Vector3 Min, Vector3 Max);
