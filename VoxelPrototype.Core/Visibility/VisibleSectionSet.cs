namespace VoxelPrototype.Core.Visibility;

public sealed class VisibleSectionSet
{
    public List<VisibleSection> Sections { get; } = new();

    public VisibleSectionSet(int capacity)
    {
        Sections = new List<VisibleSection>(capacity);
    }

    public VisibleSectionSet()
    {
    }

    public void Clear()
    {
        Sections.Clear();
    }
}
