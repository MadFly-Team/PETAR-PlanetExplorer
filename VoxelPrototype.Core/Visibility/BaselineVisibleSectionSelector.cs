using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Visibility;

public sealed class BaselineVisibleSectionSelector : IVisibleSectionSelector
{
    private IReadOnlyList<SectionBounds>? _cachedCandidates;
    private Dictionary<SectionAddress, int>? _candidateIndexByAddress;
    private int[]? _dirtyStamps;
    private int _dirtyStamp;

    public VisibleSectionSet BuildVisibleSet(VisibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var maxDistanceSquared = context.MaxDistance * context.MaxDistance;
        var visibleSet = new VisibleSectionSet(context.Candidates.Count);
        var visibleSections = visibleSet.Sections;
        var dirtySections = context.DirtySections;

        if (context.Candidates is List<SectionBounds> candidateList)
        {
            var candidates = CollectionsMarshal.AsSpan(candidateList);
            if (dirtySections is HashSet<SectionAddress> dirtySet)
            {
                var dirtyStamps = GetDirtyStampSpan(candidateList, dirtySet);
                BuildVisibleSections(candidates, context.CameraPosition, context.MaxDistance, maxDistanceSquared, dirtyStamps, _dirtyStamp, visibleSections);
            }
            else
            {
                BuildVisibleSections(candidates, context.CameraPosition, context.MaxDistance, maxDistanceSquared, dirtySections, visibleSections);
            }
        }
        else
        {
            BuildVisibleSections(context.Candidates, context.CameraPosition, context.MaxDistance, maxDistanceSquared, dirtySections, visibleSections);
        }

        SortVisibleSectionsByDistance(visibleSections);
        return visibleSet;
    }

    private static void BuildVisibleSections(Span<SectionBounds> candidates, Vector3 cameraPosition, float maxDistance, float maxDistanceSquared, IReadOnlySet<SectionAddress> dirtySections, List<VisibleSection> visibleSections)
    {
        var cameraX = cameraPosition.X;
        var cameraY = cameraPosition.Y;
        var cameraZ = cameraPosition.Z;
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            var deltaX = (((candidate.Min.X + candidate.Max.X) * 0.5f) - cameraX);
            if (deltaX > maxDistance || deltaX < -maxDistance)
            {
                continue;
            }

            var deltaY = (((candidate.Min.Y + candidate.Max.Y) * 0.5f) - cameraY);
            if (deltaY > maxDistance || deltaY < -maxDistance)
            {
                continue;
            }

            var deltaZ = (((candidate.Min.Z + candidate.Max.Z) * 0.5f) - cameraZ);
            if (deltaZ > maxDistance || deltaZ < -maxDistance)
            {
                continue;
            }

            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);
            if (distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            visibleSections.Add(new VisibleSection(candidate.Address, distanceSquared, dirtySections.Contains(candidate.Address)));
        }
    }

    private static void BuildVisibleSections(Span<SectionBounds> candidates, Vector3 cameraPosition, float maxDistance, float maxDistanceSquared, Span<int> dirtyStamps, int dirtyStamp, List<VisibleSection> visibleSections)
    {
        var cameraX = cameraPosition.X;
        var cameraY = cameraPosition.Y;
        var cameraZ = cameraPosition.Z;
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            var deltaX = (((candidate.Min.X + candidate.Max.X) * 0.5f) - cameraX);
            if (deltaX > maxDistance || deltaX < -maxDistance)
            {
                continue;
            }

            var deltaY = (((candidate.Min.Y + candidate.Max.Y) * 0.5f) - cameraY);
            if (deltaY > maxDistance || deltaY < -maxDistance)
            {
                continue;
            }

            var deltaZ = (((candidate.Min.Z + candidate.Max.Z) * 0.5f) - cameraZ);
            if (deltaZ > maxDistance || deltaZ < -maxDistance)
            {
                continue;
            }

            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);
            if (distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            visibleSections.Add(new VisibleSection(candidate.Address, distanceSquared, dirtyStamps[index] == dirtyStamp));
        }
    }

    private static void BuildVisibleSections(Span<SectionBounds> candidates, Vector3 cameraPosition, float maxDistance, float maxDistanceSquared, HashSet<SectionAddress> dirtySections, List<VisibleSection> visibleSections)
    {
        var cameraX = cameraPosition.X;
        var cameraY = cameraPosition.Y;
        var cameraZ = cameraPosition.Z;
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            var deltaX = (((candidate.Min.X + candidate.Max.X) * 0.5f) - cameraX);
            if (deltaX > maxDistance || deltaX < -maxDistance)
            {
                continue;
            }

            var deltaY = (((candidate.Min.Y + candidate.Max.Y) * 0.5f) - cameraY);
            if (deltaY > maxDistance || deltaY < -maxDistance)
            {
                continue;
            }

            var deltaZ = (((candidate.Min.Z + candidate.Max.Z) * 0.5f) - cameraZ);
            if (deltaZ > maxDistance || deltaZ < -maxDistance)
            {
                continue;
            }

            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);
            if (distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            visibleSections.Add(new VisibleSection(candidate.Address, distanceSquared, dirtySections.Contains(candidate.Address)));
        }
    }

    private static void BuildVisibleSections(IReadOnlyList<SectionBounds> candidates, Vector3 cameraPosition, float maxDistance, float maxDistanceSquared, IReadOnlySet<SectionAddress> dirtySections, List<VisibleSection> visibleSections)
    {
        var cameraX = cameraPosition.X;
        var cameraY = cameraPosition.Y;
        var cameraZ = cameraPosition.Z;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var deltaX = (((candidate.Min.X + candidate.Max.X) * 0.5f) - cameraX);
            if (deltaX > maxDistance || deltaX < -maxDistance)
            {
                continue;
            }

            var deltaY = (((candidate.Min.Y + candidate.Max.Y) * 0.5f) - cameraY);
            if (deltaY > maxDistance || deltaY < -maxDistance)
            {
                continue;
            }

            var deltaZ = (((candidate.Min.Z + candidate.Max.Z) * 0.5f) - cameraZ);
            if (deltaZ > maxDistance || deltaZ < -maxDistance)
            {
                continue;
            }

            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);
            if (distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            visibleSections.Add(new VisibleSection(candidate.Address, distanceSquared, dirtySections.Contains(candidate.Address)));
        }
    }

    private static void SortVisibleSectionsByDistance(List<VisibleSection> sections)
    {
        if (sections.Count < 2)
        {
            return;
        }

        QuickSort(CollectionsMarshal.AsSpan(sections));
    }

    private static void QuickSort(Span<VisibleSection> sections)
    {
        const int InsertionSortThreshold = 32;
        ref var firstSection = ref MemoryMarshal.GetReference(sections);
        Span<int> stack = stackalloc int[128];
        var stackCount = 0;
        stack[stackCount++] = 0;
        stack[stackCount++] = sections.Length - 1;

        while (stackCount > 0)
        {
            var right = stack[--stackCount];
            var left = stack[--stackCount];

            while ((right - left) > InsertionSortThreshold)
            {
                var i = left;
                var j = right;
                var pivot = Unsafe.Add(ref firstSection, left + ((right - left) >> 1)).DistanceSquared;
                do
                {
                    while (Unsafe.Add(ref firstSection, i).DistanceSquared < pivot)
                    {
                        i++;
                    }

                    while (Unsafe.Add(ref firstSection, j).DistanceSquared > pivot)
                    {
                        j--;
                    }

                    if (i <= j)
                    {
                        if (i != j)
                        {
                            Swap(ref Unsafe.Add(ref firstSection, i), ref Unsafe.Add(ref firstSection, j));
                        }

                        i++;
                        j--;
                    }
                }
                while (i <= j);

                if ((j - left) > (right - i))
                {
                    if (left < j)
                    {
                        stack[stackCount++] = left;
                        stack[stackCount++] = j;
                    }

                    left = i;
                }
                else
                {
                    if (i < right)
                    {
                        stack[stackCount++] = i;
                        stack[stackCount++] = right;
                    }

                    right = j;
                }
            }

            if (left < right)
            {
                InsertionSort(sections, left, right);
            }
        }
    }

    private static void InsertionSort(Span<VisibleSection> sections, int left, int right)
    {
        ref var firstSection = ref MemoryMarshal.GetReference(sections);
        for (var index = left + 1; index <= right; index++)
        {
            var current = Unsafe.Add(ref firstSection, index);
            var currentDistance = current.DistanceSquared;
            var previousIndex = index - 1;
            while (previousIndex >= left && Unsafe.Add(ref firstSection, previousIndex).DistanceSquared > currentDistance)
            {
                Unsafe.Add(ref firstSection, previousIndex + 1) = Unsafe.Add(ref firstSection, previousIndex);
                previousIndex--;
            }

            Unsafe.Add(ref firstSection, previousIndex + 1) = current;
        }
    }

    private static void Swap(ref VisibleSection left, ref VisibleSection right)
    {
        var temp = left;
        left = right;
        right = temp;
    }

    private Span<int> GetDirtyStampSpan(List<SectionBounds> candidates, HashSet<SectionAddress> dirtySections)
    {
        EnsureCandidateIndexCache(candidates);
        EnsureDirtyStampCapacity(candidates.Count);
        AdvanceDirtyStamp();

        var candidateIndexByAddress = _candidateIndexByAddress!;
        var dirtyStamps = _dirtyStamps!;
        foreach (var address in dirtySections)
        {
            if (candidateIndexByAddress.TryGetValue(address, out var index))
            {
                dirtyStamps[index] = _dirtyStamp;
            }
        }

        return dirtyStamps.AsSpan(0, candidates.Count);
    }

    private void EnsureCandidateIndexCache(List<SectionBounds> candidates)
    {
        if (ReferenceEquals(_cachedCandidates, candidates) && _candidateIndexByAddress is not null && _candidateIndexByAddress.Count == candidates.Count)
        {
            return;
        }

        var candidateIndexByAddress = new Dictionary<SectionAddress, int>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            candidateIndexByAddress[candidates[index].Address] = index;
        }

        _cachedCandidates = candidates;
        _candidateIndexByAddress = candidateIndexByAddress;
    }

    private void EnsureDirtyStampCapacity(int count)
    {
        if (_dirtyStamps is null || _dirtyStamps.Length < count)
        {
            _dirtyStamps = new int[count];
        }
    }

    private void AdvanceDirtyStamp()
    {
        if (_dirtyStamp == int.MaxValue)
        {
            Array.Clear(_dirtyStamps!);
            _dirtyStamp = 1;
            return;
        }

        _dirtyStamp++;
        if (_dirtyStamp == 0)
        {
            _dirtyStamp = 1;
        }
    }
}
