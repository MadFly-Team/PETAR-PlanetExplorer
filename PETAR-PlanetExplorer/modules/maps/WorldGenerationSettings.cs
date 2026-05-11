using System;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed class WorldGenerationSettings
    {
        public const int DefaultSeed = 74291;
        public const int MinimumMaxCubeColumn = 32;
        public const int MaximumMaxCubeColumn = 255;
        public const int DefaultMaxCubeColumn = 96;
        public const int MinimumTownDensity = 0;
        public const int MaximumTownDensity = 100;
        public const int DefaultTownDensity = 40;
        public const int MinimumTrafficCount = 0;
        public const int MaximumTrafficCount = 10000;
        public const int DefaultTrafficCount = 4000;
        public const float MinimumFeatureIntensity = 0f;
        public const float MaximumFeatureIntensity = 1f;
        public const int DefaultThemeIndex = 0;

        public WorldGenerationSettings(
            int seed,
            PlanetTheme theme,
            float mountainIntensity,
            float plateauIntensity,
            float volcanoIntensity,
            float craterIntensity,
            float gorgeIntensity,
            int maxCubeColumn,
            int townDensity,
            int trafficCount,
            int treeCount)
        {
            Seed = Math.Max(1, seed);
            Theme = theme ?? PlanetTheme.Earth;
            MountainIntensity = ClampIntensity(mountainIntensity);
            PlateauIntensity = ClampIntensity(plateauIntensity);
            VolcanoIntensity = ClampIntensity(volcanoIntensity);
            CraterIntensity = ClampIntensity(craterIntensity);
            GorgeIntensity = ClampIntensity(gorgeIntensity);
            MaxCubeColumn = Math.Clamp(maxCubeColumn, MinimumMaxCubeColumn, MaximumMaxCubeColumn);
            TownDensity = Math.Clamp(townDensity, MinimumTownDensity, MaximumTownDensity);
            TrafficCount = Math.Clamp(trafficCount, MinimumTrafficCount, MaximumTrafficCount);
            TreeCount = Math.Max(0, treeCount);
        }

        public int Seed { get; }

        public PlanetTheme Theme { get; }

        public float MountainIntensity { get; }

        public float PlateauIntensity { get; }

        public float VolcanoIntensity { get; }

        public float CraterIntensity { get; }

        public float GorgeIntensity { get; }

        public int MaxCubeColumn { get; }

        public int TownDensity { get; }

        public int TrafficCount { get; }

        public int TreeCount { get; }

        public static WorldGenerationSettings Default { get; } = new WorldGenerationSettings(
            seed: DefaultSeed,
            PlanetTheme.GetByIndex(DefaultThemeIndex),
            mountainIntensity: 0.55f,
            plateauIntensity: 0.3f,
            volcanoIntensity: 0.2f,
            craterIntensity: 0.35f,
            gorgeIntensity: 0.25f,
            maxCubeColumn: DefaultMaxCubeColumn,
            townDensity: DefaultTownDensity,
            trafficCount: DefaultTrafficCount,
            treeCount: ProceduralWorldMap.DefaultTreeCount);

        public WorldGenerationSettings WithSeed(int seed)
        {
            return new WorldGenerationSettings(seed, Theme, MountainIntensity, PlateauIntensity, VolcanoIntensity, CraterIntensity, GorgeIntensity, MaxCubeColumn, TownDensity, TrafficCount, TreeCount);
        }

        public WorldGenerationSettings WithTheme(PlanetTheme theme)
        {
            return new WorldGenerationSettings(Seed, theme, MountainIntensity, PlateauIntensity, VolcanoIntensity, CraterIntensity, GorgeIntensity, MaxCubeColumn, TownDensity, TrafficCount, TreeCount);
        }

        public WorldGenerationSettings WithMountainIntensity(float intensity)
        {
            return new WorldGenerationSettings(Seed, Theme, intensity, PlateauIntensity, VolcanoIntensity, CraterIntensity, GorgeIntensity, MaxCubeColumn, TownDensity, TrafficCount, TreeCount);
        }

        public WorldGenerationSettings WithPlateauIntensity(float intensity)
        {
            return new WorldGenerationSettings(Seed, Theme, MountainIntensity, intensity, VolcanoIntensity, CraterIntensity, GorgeIntensity, MaxCubeColumn, TownDensity, TrafficCount, TreeCount);
        }

        public WorldGenerationSettings WithVolcanoIntensity(float intensity)
        {
            return new WorldGenerationSettings(Seed, Theme, MountainIntensity, PlateauIntensity, intensity, CraterIntensity, GorgeIntensity, MaxCubeColumn, TownDensity, TrafficCount, TreeCount);
        }

        public WorldGenerationSettings WithCraterIntensity(float intensity)
        {
            return new WorldGenerationSettings(Seed, Theme, MountainIntensity, PlateauIntensity, VolcanoIntensity, intensity, GorgeIntensity, MaxCubeColumn, TownDensity, TrafficCount, TreeCount);
        }

        public WorldGenerationSettings WithGorgeIntensity(float intensity)
        {
            return new WorldGenerationSettings(Seed, Theme, MountainIntensity, PlateauIntensity, VolcanoIntensity, CraterIntensity, intensity, MaxCubeColumn, TownDensity, TrafficCount, TreeCount);
        }

        public WorldGenerationSettings WithMaxCubeColumn(int maxCubeColumn)
        {
            return new WorldGenerationSettings(Seed, Theme, MountainIntensity, PlateauIntensity, VolcanoIntensity, CraterIntensity, GorgeIntensity, maxCubeColumn, TownDensity, TrafficCount, TreeCount);
        }

        public WorldGenerationSettings WithTownDensity(int townDensity)
        {
            return new WorldGenerationSettings(Seed, Theme, MountainIntensity, PlateauIntensity, VolcanoIntensity, CraterIntensity, GorgeIntensity, MaxCubeColumn, townDensity, TrafficCount, TreeCount);
        }

        public WorldGenerationSettings WithTrafficCount(int trafficCount)
        {
            return new WorldGenerationSettings(Seed, Theme, MountainIntensity, PlateauIntensity, VolcanoIntensity, CraterIntensity, GorgeIntensity, MaxCubeColumn, TownDensity, trafficCount, TreeCount);
        }

        public WorldGenerationSettings WithTreeCount(int treeCount)
        {
            return new WorldGenerationSettings(Seed, Theme, MountainIntensity, PlateauIntensity, VolcanoIntensity, CraterIntensity, GorgeIntensity, MaxCubeColumn, TownDensity, TrafficCount, treeCount);
        }

        private static float ClampIntensity(float intensity)
        {
            return Math.Clamp(intensity, MinimumFeatureIntensity, MaximumFeatureIntensity);
        }
    }
}
