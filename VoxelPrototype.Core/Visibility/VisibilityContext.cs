using System.Numerics;

namespace VoxelPrototype.Core.Visibility;

public sealed record VisibilityContext(
    Vector3 CameraPosition,
    IReadOnlyList<SectionBounds> Candidates,
    float MaxDistance,
    IReadOnlySet<World.SectionAddress> DirtySections);
