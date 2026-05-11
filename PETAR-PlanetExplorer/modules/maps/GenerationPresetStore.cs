using System;
using System.IO;
using System.Text.Json;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed class GenerationPresetStore
    {
        private readonly string _presetFilePath;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public GenerationPresetStore(string presetFilePath)
        {
            _presetFilePath = presetFilePath ?? throw new ArgumentNullException(nameof(presetFilePath));
        }

        public bool TryLoad(out WorldGenerationSettings settings)
        {
            return TryLoad(out settings, out _);
        }

        public bool TryLoad(out WorldGenerationSettings settings, out int terrainEditSlot)
        {
            settings = WorldGenerationSettings.Default;
            terrainEditSlot = 0;
            if (!File.Exists(_presetFilePath))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(_presetFilePath);
                var preset = JsonSerializer.Deserialize<PresetData>(json, _jsonOptions);
                if (preset == null)
                {
                    return false;
                }

                settings = new WorldGenerationSettings(
                    preset.Seed,
                    PlanetTheme.All[Math.Clamp(preset.ThemeIndex, 0, PlanetTheme.All.Length - 1)],
                    preset.MountainIntensity,
                    preset.PlateauIntensity,
                    preset.VolcanoIntensity,
                    preset.CraterIntensity,
                    preset.GorgeIntensity,
                    preset.MaxCubeColumn,
                    preset.TownDensity,
                    preset.TrafficCount,
                    preset.TreeCount);
                terrainEditSlot = Math.Max(0, preset.TerrainEditSlot);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Save(WorldGenerationSettings settings)
        {
            Save(settings, 0);
        }

        public void Save(WorldGenerationSettings settings, int terrainEditSlot)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var directory = Path.GetDirectoryName(_presetFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var preset = new PresetData
            {
                Seed = settings.Seed,
                ThemeIndex = Array.IndexOf(PlanetTheme.All, settings.Theme),
                MountainIntensity = settings.MountainIntensity,
                PlateauIntensity = settings.PlateauIntensity,
                VolcanoIntensity = settings.VolcanoIntensity,
                CraterIntensity = settings.CraterIntensity,
                GorgeIntensity = settings.GorgeIntensity,
                MaxCubeColumn = settings.MaxCubeColumn,
                TownDensity = settings.TownDensity,
                TrafficCount = settings.TrafficCount,
                TreeCount = settings.TreeCount,
                TerrainEditSlot = Math.Max(0, terrainEditSlot)
            };
            var json = JsonSerializer.Serialize(preset, _jsonOptions);
            File.WriteAllText(_presetFilePath, json);
        }

        private sealed class PresetData
        {
            public int Seed { get; set; }

            public int ThemeIndex { get; set; }

            public float MountainIntensity { get; set; }

            public float PlateauIntensity { get; set; }

            public float VolcanoIntensity { get; set; }

            public float CraterIntensity { get; set; }

            public float GorgeIntensity { get; set; }

            public int MaxCubeColumn { get; set; }

            public int TownDensity { get; set; } = WorldGenerationSettings.DefaultTownDensity;

            public int TrafficCount { get; set; } = WorldGenerationSettings.DefaultTrafficCount;

            public int TreeCount { get; set; }

            public int TerrainEditSlot { get; set; }
        }
    }
}
