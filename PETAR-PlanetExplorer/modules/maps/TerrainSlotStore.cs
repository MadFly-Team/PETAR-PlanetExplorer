using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed class TerrainSlotStore
    {
        private readonly string _storeFilePath;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public TerrainSlotStore(string storeFilePath)
        {
            _storeFilePath = storeFilePath ?? throw new ArgumentNullException(nameof(storeFilePath));
        }

        public string[] Load(int slotCount)
        {
            slotCount = Math.Max(1, slotCount);
            var fallback = CreateDefaultNames(slotCount);
            if (!File.Exists(_storeFilePath))
            {
                return fallback;
            }

            try
            {
                var json = File.ReadAllText(_storeFilePath);
                var data = JsonSerializer.Deserialize<StoreData>(json, _jsonOptions);
                if (data?.SlotNames == null || data.SlotNames.Length == 0)
                {
                    return fallback;
                }

                var names = CreateDefaultNames(slotCount);
                for (var index = 0; index < Math.Min(slotCount, data.SlotNames.Length); index++)
                {
                    if (!string.IsNullOrWhiteSpace(data.SlotNames[index]))
                    {
                        names[index] = data.SlotNames[index].Trim();
                    }
                }

                return names;
            }
            catch
            {
                return fallback;
            }
        }

        public void Save(string[] slotNames, int slotCount)
        {
            ArgumentNullException.ThrowIfNull(slotNames);
            slotCount = Math.Max(1, slotCount);
            var directory = Path.GetDirectoryName(_storeFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var normalizedNames = CreateDefaultNames(slotCount);
            for (var index = 0; index < Math.Min(slotCount, slotNames.Length); index++)
            {
                if (!string.IsNullOrWhiteSpace(slotNames[index]))
                {
                    normalizedNames[index] = slotNames[index].Trim();
                }
            }

            var data = new StoreData { SlotNames = normalizedNames };
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(_storeFilePath, json);
        }

        private static string[] CreateDefaultNames(int slotCount)
        {
            return Enumerable.Range(1, slotCount)
                .Select(index => $"Slot {index}")
                .ToArray();
        }

        private sealed class StoreData
        {
            public string[] SlotNames { get; set; } = Array.Empty<string>();
        }
    }
}
