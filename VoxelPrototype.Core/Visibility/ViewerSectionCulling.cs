using System.Numerics;

namespace VoxelPrototype.Core.Visibility;

public static class ViewerSectionCulling
{
    public static bool ShouldDrawSection(SectionBounds bounds, Vector3 cameraPosition, Vector3 worldMin, Vector3 worldMax, float cullPadding)
    {
        var sectionCenter = (bounds.Min + bounds.Max) * 0.5f;
        var boundsSize = worldMax - worldMin;
        var maxVisibleDistance = MathF.Max(boundsSize.Length(), 1f) + cullPadding;
        var maxVisibleDistanceSquared = maxVisibleDistance * maxVisibleDistance;
        return Vector3.DistanceSquared(cameraPosition, sectionCenter) <= maxVisibleDistanceSquared;
    }
}