using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed class TerrainEditStore
    {
        private const int MaxRecordsPerSeed = 256;
        private readonly string _storeFilePath;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public TerrainEditStore(string storeFilePath)
        {
            _storeFilePath = storeFilePath ?? throw new ArgumentNullException(nameof(storeFilePath));
        }

        public IReadOnlyList<TerrainEditRecord> Load(int seed, int slot)
        {
            if (!File.Exists(_storeFilePath))
            {
                return Array.Empty<TerrainEditRecord>();
            }

            try
            {
                var json = File.ReadAllText(_storeFilePath);
                var data = JsonSerializer.Deserialize<StoreData>(json, _jsonOptions);
                if (data?.Records == null)
                {
                    return Array.Empty<TerrainEditRecord>();
                }

                return data.Records
                    .Where(record => record.Seed == seed && record.Slot == slot)
                    .Select(record => new TerrainEditRecord(record.Seed, record.Slot, new Vector2(record.CenterX, record.CenterY), record.CenterWorldY, record.Radius, record.ProtectedColumnHeight))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<TerrainEditRecord>();
            }
        }

        public void Save(int seed, int slot, IReadOnlyList<TerrainEditRecord> records)
        {
            ArgumentNullException.ThrowIfNull(records);
            var directory = Path.GetDirectoryName(_storeFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            StoreData data;
            if (File.Exists(_storeFilePath))
            {
                try
                {
                    data = JsonSerializer.Deserialize<StoreData>(File.ReadAllText(_storeFilePath), _jsonOptions) ?? new StoreData();
                }
                catch
                {
                    data = new StoreData();
                }
            }
            else
            {
                data = new StoreData();
            }

            data.Records.RemoveAll(record => record.Seed == seed && record.Slot == slot);
            var compactedRecords = records
                .Where(record => record.Seed == seed && record.Slot == slot)
                .TakeLast(MaxRecordsPerSeed)
                .ToArray();
            foreach (var record in compactedRecords)
            {
                data.Records.Add(new TerrainEditRecordData
                {
                    Seed = record.Seed,
                    Slot = record.Slot,
                    CenterX = record.Center.X,
                    CenterY = record.Center.Y,
                    CenterWorldY = record.CenterWorldY,
                    Radius = record.Radius,
                    ProtectedColumnHeight = record.ProtectedColumnHeight
                });
            }

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(_storeFilePath, json);
        }

        public void Clear(int seed, int slot)
        {
            if (!File.Exists(_storeFilePath))
            {
                return;
            }

            try
            {
                var data = JsonSerializer.Deserialize<StoreData>(File.ReadAllText(_storeFilePath), _jsonOptions) ?? new StoreData();
                data.Records.RemoveAll(record => record.Seed == seed && record.Slot == slot);
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                File.WriteAllText(_storeFilePath, json);
            }
            catch
            {
            }
        }

        private sealed class StoreData
        {
            public List<TerrainEditRecordData> Records { get; set; } = new();
        }

        private sealed class TerrainEditRecordData
        {
            public int Seed { get; set; }

            public int Slot { get; set; }

            public float CenterX { get; set; }

            public float CenterY { get; set; }

            public float CenterWorldY { get; set; }

            public float Radius { get; set; }

            public int ProtectedColumnHeight { get; set; }
        }
    }

    public readonly record struct TerrainEditRecord(int Seed, int Slot, Vector2 Center, float CenterWorldY, float Radius, int ProtectedColumnHeight);
}
