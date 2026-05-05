using VoxelPrototype.Core.Materials;
using VoxelPrototype.Core.World;

namespace VoxelPrototype.Core.Generation;

public sealed class BaselineSectionGenerator : IVoxelSectionGenerator
{
    private const float SurfaceNoiseFrequency = 0.0035f;
    private const float RidgeNoiseFrequency = 0.0095f;
    private const float CaveNoiseFrequency = 0.041f;
    private const float ArchNoiseFrequency = 0.018f;
    private const float SurfaceWarpFrequency = 0.024f;
    private const float SignedHashScale = 1f / 1073741824f;

    public void GenerateSection(SectionGenerationContext context, VoxelSection targetSection)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(targetSection);

        var options = context.Options;
        options.Validate();
        ValidateSectionDimensions(context, targetSection);

        var sectionOriginX = context.Address.X * targetSection.SizeX;
        var sectionOriginY = context.Address.Y * targetSection.SizeY;
        var sectionOriginZ = context.Address.Z * targetSection.SizeZ;
        var seaLevel = options.WorldHeightInSections * targetSection.SizeZ * 0.3f;
        var surfaceHeightBase = seaLevel + (options.WorldHeightInSections * options.SectionSizeZ * 0.08f);
        var air = VoxelBlock.Air;
        var materials = context.Materials;
        var grass = VoxelPrototypeMaterialLibrary.CreateBlock(materials, VoxelMaterialIds.Grass);
        var dirt = VoxelPrototypeMaterialLibrary.CreateBlock(materials, VoxelMaterialIds.Dirt);
        var stone = VoxelPrototypeMaterialLibrary.CreateBlock(materials, VoxelMaterialIds.Stone);
        var sand = VoxelPrototypeMaterialLibrary.CreateBlock(materials, VoxelMaterialIds.Sand);
        var basalt = VoxelPrototypeMaterialLibrary.CreateBlock(materials, VoxelMaterialIds.Basalt);
        var sizeX = targetSection.SizeX;
        var sizeY = targetSection.SizeY;
        var sizeZ = targetSection.SizeZ;
        var columnCount = sizeX * sizeY;
        Span<float> surfaceHeights = columnCount <= 1024 ? stackalloc float[columnCount] : new float[columnCount];
        Span<float> archBandCenters = columnCount <= 1024 ? stackalloc float[columnCount] : new float[columnCount];
        Span<float> overhangNoiseXs = columnCount <= 1024 ? stackalloc float[columnCount] : new float[columnCount];
        Span<float> overhangNoiseYs = columnCount <= 1024 ? stackalloc float[columnCount] : new float[columnCount];
        Span<float> caveNoiseXs = columnCount <= 1024 ? stackalloc float[columnCount] : new float[columnCount];
        Span<float> caveNoiseYs = columnCount <= 1024 ? stackalloc float[columnCount] : new float[columnCount];
        Span<float> archNoiseXs = columnCount <= 1024 ? stackalloc float[columnCount] : new float[columnCount];
        Span<float> archNoiseYs = columnCount <= 1024 ? stackalloc float[columnCount] : new float[columnCount];

        targetSection.Clear();

        var columnIndex = 0;
        for (var localY = 0; localY < sizeY; localY++)
        {
            var worldY = sectionOriginY + localY;
            for (var localX = 0; localX < sizeX; localX++)
            {
                var worldX = sectionOriginX + localX;
                var surfaceHeight = ComputeSurfaceHeight(worldX, worldY, context.Seed, surfaceHeightBase);
                surfaceHeights[columnIndex] = surfaceHeight;
                var overhangNoiseX = worldX * 0.014f;
                var overhangNoiseY = worldY * 0.014f;
                var caveNoiseX = worldX * CaveNoiseFrequency;
                var caveNoiseY = worldY * CaveNoiseFrequency;
                var archNoiseX = worldX * ArchNoiseFrequency;
                var archNoiseY = worldY * ArchNoiseFrequency;
                overhangNoiseXs[columnIndex] = overhangNoiseX;
                overhangNoiseYs[columnIndex] = overhangNoiseY;
                caveNoiseXs[columnIndex] = caveNoiseX;
                caveNoiseYs[columnIndex] = caveNoiseY;
                archNoiseXs[columnIndex] = archNoiseX;
                archNoiseYs[columnIndex] = archNoiseY;
                archBandCenters[columnIndex] = surfaceHeight + 10f + (SignedValueNoise3D(archNoiseX, archNoiseY, 0f, context.Seed + 71) * 10f);
                columnIndex++;
            }
        }

        for (var localZ = 0; localZ < sizeZ; localZ++)
        {
            var worldZ = sectionOriginZ + localZ;
            var overhangNoiseZ = worldZ * 0.018f;
            var caveNoiseZ = worldZ * CaveNoiseFrequency;
            var archNoiseZ = worldZ * (ArchNoiseFrequency * 0.7f);
            var shorelineFill = worldZ < seaLevel ? (seaLevel - worldZ) * 0.18f : 0f;
            for (var localY = 0; localY < sizeY; localY++)
            {
                var rowOffset = localY * sizeX;
                for (var localX = 0; localX < sizeX; localX++)
                {
                    var currentColumnIndex = rowOffset + localX;
                    var surfaceHeight = surfaceHeights[currentColumnIndex];
                    var archBandCenter = archBandCenters[currentColumnIndex];
                    var density = ComputeDensity(
                        overhangNoiseXs[currentColumnIndex],
                        overhangNoiseYs[currentColumnIndex],
                        overhangNoiseZ,
                        caveNoiseXs[currentColumnIndex],
                        caveNoiseYs[currentColumnIndex],
                        caveNoiseZ,
                        archNoiseXs[currentColumnIndex],
                        archNoiseYs[currentColumnIndex],
                        archNoiseZ,
                        worldZ,
                        surfaceHeight,
                        archBandCenter,
                        context.Seed,
                        shorelineFill);
                    if (density <= 0f)
                    {
                        targetSection.SetBlock(localX, localY, localZ, air);
                        continue;
                    }

                    targetSection.SetBlock(localX, localY, localZ, ResolveBlock(worldZ, surfaceHeight, seaLevel, grass, dirt, stone, sand, basalt));
                }
            }
        }
    }

    private static void ValidateSectionDimensions(SectionGenerationContext context, VoxelSection targetSection)
    {
        if (targetSection.SizeX != context.Options.SectionSizeX ||
            targetSection.SizeY != context.Options.SectionSizeY ||
            targetSection.SizeZ != context.Options.SectionSizeZ)
        {
            throw new ArgumentException("Target section dimensions must match world options.", nameof(targetSection));
        }
    }

    private static float ComputeSurfaceHeight(int worldX, int worldY, int seed, float surfaceHeightBase)
    {
        var broadNoise = SignedFbm2D(worldX * SurfaceNoiseFrequency, worldY * SurfaceNoiseFrequency, seed + 11, 4, 2f, 0.5f);
        var ridgeNoise = MathF.Abs(SignedFbm2D(worldX * RidgeNoiseFrequency, worldY * RidgeNoiseFrequency, seed + 23, 3, 2f, 0.55f));
        var warp = SignedValueNoise3D(worldX * SurfaceWarpFrequency, worldY * SurfaceWarpFrequency, 0f, seed + 37) * 6f;
        return surfaceHeightBase + (broadNoise * 22f) + (ridgeNoise * 18f) + warp;
    }

    private static float ComputeDensity(float overhangNoiseX, float overhangNoiseY, float overhangNoiseZ, float caveNoiseX, float caveNoiseY, float caveNoiseZ, float archNoiseX, float archNoiseY, float archNoiseZ, int worldZ, float surfaceHeight, float archBandCenter, int seed, float shorelineFill)
    {
        var surfaceDelta = surfaceHeight - worldZ;
        var baseDensity = surfaceDelta;
        var overhangBias = SignedValueNoise3D(overhangNoiseX, overhangNoiseY, overhangNoiseZ, seed + 41) * 8f;

        var archDistance = MathF.Abs(worldZ - archBandCenter);
        var archDensity = 0f;
        if (archDistance < 7f)
        {
            var archNoise = SignedValueNoise3D(archNoiseX, archNoiseY, archNoiseZ, seed + 83);
            if (archNoise > 0.18f)
            {
                archDensity = (archNoise - 0.18f) * (1f - (archDistance / 7f)) * 24f;
            }
        }

        var densityBeforeCaves = baseDensity + overhangBias + archDensity + shorelineFill;
        var caveNoise = SignedFbm3D(caveNoiseX, caveNoiseY, caveNoiseZ, seed + 59, 2, 2f, 0.5f);
        var caveThreshold = Lerp(0.52f, 0.66f, Saturate(surfaceDelta / 28f));
        var caveCarve = caveNoise > caveThreshold ? (caveNoise - caveThreshold) * 48f : 0f;
        return densityBeforeCaves - caveCarve;
    }

    private static VoxelBlock ResolveBlock(int worldZ, float surfaceHeight, float seaLevel, VoxelBlock grass, VoxelBlock dirt, VoxelBlock stone, VoxelBlock sand, VoxelBlock basalt)
    {
        if (worldZ < seaLevel - 18f)
        {
            return basalt;
        }

        if (worldZ >= surfaceHeight - 1.25f)
        {
            return worldZ <= seaLevel + 2f ? sand : grass;
        }

        if (worldZ >= surfaceHeight - 4.5f)
        {
            return dirt;
        }

        return stone;
    }

    private static float SignedFbm2D(float x, float y, int seed, int octaves, float lacunarity, float persistence)
    {
        var amplitude = 1f;
        var frequency = 1f;
        var sum = 0f;
        var maxAmplitude = 0f;

        for (var octave = 0; octave < octaves; octave++)
        {
            sum += SignedValueNoise3D(x * frequency, y * frequency, octave * 17.23f, seed + octave * 101) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return maxAmplitude > 0f ? sum / maxAmplitude : 0f;
    }

    private static float SignedFbm3D(float x, float y, float z, int seed, int octaves, float lacunarity, float persistence)
    {
        var amplitude = 1f;
        var frequency = 1f;
        var sum = 0f;
        var maxAmplitude = 0f;

        for (var octave = 0; octave < octaves; octave++)
        {
            sum += SignedValueNoise3D(x * frequency, y * frequency, z * frequency, seed + octave * 131) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return maxAmplitude > 0f ? sum / maxAmplitude : 0f;
    }

    private static float SignedValueNoise3D(float x, float y, float z, int seed)
    {
        var x0 = FastFloor(x);
        var y0 = FastFloor(y);
        var z0 = FastFloor(z);
        var x1 = x0 + 1;
        var y1 = y0 + 1;
        var z1 = z0 + 1;
        var tx = x - x0;
        var ty = y - y0;
        var tz = z - z0;

        var c000 = HashToSignedUnitFloat(x0, y0, z0, seed);
        var c100 = HashToSignedUnitFloat(x1, y0, z0, seed);
        var c010 = HashToSignedUnitFloat(x0, y1, z0, seed);
        var c110 = HashToSignedUnitFloat(x1, y1, z0, seed);
        var c001 = HashToSignedUnitFloat(x0, y0, z1, seed);
        var c101 = HashToSignedUnitFloat(x1, y0, z1, seed);
        var c011 = HashToSignedUnitFloat(x0, y1, z1, seed);
        var c111 = HashToSignedUnitFloat(x1, y1, z1, seed);

        var sx = tx * tx * (3f - (2f * tx));
        var sy = ty * ty * (3f - (2f * ty));
        var sz = tz * tz * (3f - (2f * tz));

        var x00 = c000 + ((c100 - c000) * sx);
        var x10 = c010 + ((c110 - c010) * sx);
        var x01 = c001 + ((c101 - c001) * sx);
        var x11 = c011 + ((c111 - c011) * sx);
        var y0Blend = x00 + ((x10 - x00) * sy);
        var y1Blend = x01 + ((x11 - x01) * sy);
        return y0Blend + ((y1Blend - y0Blend) * sz);
    }

    private static float HashToSignedUnitFloat(int x, int y, int z, int seed)
    {
        unchecked
        {
            var hash = (uint)seed;
            hash ^= (uint)x * 0x9E3779B9u;
            hash ^= (uint)y * 0x85EBCA6Bu;
            hash ^= (uint)z * 0xC2B2AE35u;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            return (hash & 0x7fffffffu) * SignedHashScale - 1f;
        }
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3f - (2f * value));
    }

    private static int FastFloor(float value)
    {
        var integer = (int)value;
        return value < integer ? integer - 1 : integer;
    }

    private static float Saturate(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }

    private static float Lerp(float start, float end, float amount)
    {
        return start + ((end - start) * amount);
    }
}
