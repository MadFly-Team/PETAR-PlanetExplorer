namespace VoxelPrototype.Core;

public sealed record VoxelPrototypeWorldOptions
{
    public static VoxelPrototypeWorldOptions Default { get; } = new();

    public int SectionSizeX { get; init; } = 16;

    public int SectionSizeY { get; init; } = 16;

    public int SectionSizeZ { get; init; } = 16;

    public int RenderRadiusInSections { get; init; } = 12;

    public int WorldHeightInSections { get; init; } = 16;

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(SectionSizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(SectionSizeY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(SectionSizeZ);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RenderRadiusInSections);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(WorldHeightInSections);
    }
}
