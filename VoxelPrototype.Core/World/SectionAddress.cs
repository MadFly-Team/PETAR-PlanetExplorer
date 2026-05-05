namespace VoxelPrototype.Core.World;

public readonly record struct SectionAddress(int X, int Y, int Z)
{
    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
}
