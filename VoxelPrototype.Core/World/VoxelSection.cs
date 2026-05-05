namespace VoxelPrototype.Core.World;

public sealed class VoxelSection
{
    private readonly VoxelBlock[] _blocks;
    private readonly int _sliceStride;

    public VoxelSection(SectionAddress address, int sizeX, int sizeY, int sizeZ)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeZ);

        Address = address;
        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        _sliceStride = sizeX * sizeY;
        _blocks = new VoxelBlock[sizeX * sizeY * sizeZ];
    }

    public SectionAddress Address { get; }

    public int SizeX { get; }

    public int SizeY { get; }

    public int SizeZ { get; }

    public int SolidVoxelCount { get; private set; }

    public VoxelBlock GetBlock(int x, int y, int z)
    {
        return _blocks[GetIndex(x, y, z)];
    }

    public VoxelBlock GetBlockUnchecked(int x, int y, int z)
    {
        return _blocks[x + (y * SizeX) + (z * _sliceStride)];
    }

    public void SetBlock(int x, int y, int z, VoxelBlock block)
    {
        var index = GetIndex(x, y, z);
        var previous = _blocks[index];
        if (previous.IsSolid)
        {
            SolidVoxelCount--;
        }

        _blocks[index] = block;
        if (block.IsSolid)
        {
            SolidVoxelCount++;
        }
    }

    public ReadOnlySpan<VoxelBlock> AsSpan()
    {
        return _blocks;
    }

    public void Clear()
    {
        Array.Clear(_blocks);
        SolidVoxelCount = 0;
    }

    private int GetIndex(int x, int y, int z)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfNegative(z);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, SizeX);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, SizeY);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(z, SizeZ);
        return x + (y * SizeX) + (z * _sliceStride);
    }
}
