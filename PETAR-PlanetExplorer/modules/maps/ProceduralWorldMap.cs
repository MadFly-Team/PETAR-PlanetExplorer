using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed class ProceduralWorldMap
    {
        public const float SeaLevel = 0.46f;
        public const int DefaultTreeCount = 24000;
        private const float TreeMinHeight = 30f / 63f;
        private const float TreeMaxHeight = 35f / 63f;
        private const float SnowStartHeight = 58f / 63f;
        private const int TreeChunkSize = 16;

        private readonly float[] _heightData;
        private readonly bool[] _riverData;
        private readonly WorldGenerationSettings _settings;
        private Dictionary<int, TreeInstance[]> _treesByChunk;

        public ProceduralWorldMap(int width, int height, int seed, Action<float, string> progressCallback = null)
            : this(width, height, seed, WorldGenerationSettings.Default, progressCallback)
        {
        }

        public ProceduralWorldMap(int width, int height, int seed, int treeCount, Action<float, string> progressCallback = null)
            : this(width, height, seed, WorldGenerationSettings.Default.WithTreeCount(treeCount), progressCallback)
        {
        }

        public ProceduralWorldMap(int width, int height, int seed, PlanetTheme theme, Action<float, string> progressCallback = null)
            : this(width, height, seed, WorldGenerationSettings.Default.WithTheme(theme), progressCallback)
        {
        }

        public ProceduralWorldMap(int width, int height, int seed, PlanetTheme theme, int treeCount, Action<float, string> progressCallback = null)
            : this(width, height, seed, WorldGenerationSettings.Default.WithTheme(theme).WithTreeCount(treeCount), progressCallback)
        {
        }

        public ProceduralWorldMap(int width, int height, int seed, WorldGenerationSettings settings, Action<float, string> progressCallback = null)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
            Seed = seed;
            _settings = settings ?? WorldGenerationSettings.Default;
            Theme = _settings.Theme ?? PlanetTheme.Earth;
            TreeTargetCount = Theme.HasTrees ? Math.Max(0, _settings.TreeCount) : 0;
            _heightData = new float[width * height];
            _riverData = new bool[width * height];
            _treesByChunk = new Dictionary<int, TreeInstance[]>();

            Generate(progressCallback);
        }

        public int Width { get; }

        public int Height { get; }

        public int Seed { get; }

        public PlanetTheme Theme { get; }

        public WorldGenerationSettings Settings => _settings;

        public bool HasSurfaceWater => Theme.HasSurfaceWater;

        public bool HasTrees => Theme.HasTrees;

        public bool HasBirds => Theme.HasBirds;

        public int MaxCubeColumn => _settings.MaxCubeColumn;

        public int TreeTargetCount { get; }

        public int GeneratedTreeCount { get; private set; }

        public IReadOnlyList<TreeInstance> GetTreesInChunk(int chunkStartX, int chunkStartY)
        {
            var wrappedChunkX = WrapGridCoordinate(chunkStartX, Width);
            var wrappedChunkY = WrapGridCoordinate(chunkStartY, Height);
            var chunkKey = GetIndex(wrappedChunkX, wrappedChunkY);
            return _treesByChunk.TryGetValue(chunkKey, out var trees)
                ? trees
                : Array.Empty<TreeInstance>();
        }

        public Vector2 WrapPosition(Vector2 position)
        {
            return new Vector2(WrapCoordinate(position.X, Width), WrapCoordinate(position.Y, Height));
        }

        public Color[] CreateColorMap(Action<float, string> progressCallback = null)
        {
            var colors = new Color[_heightData.Length];
            long rowsCompleted = 0;

            Parallel.For(0, Height, y =>
            {
                var rowOffset = y * Width;
                for (var x = 0; x < Width; x++)
                {
                    var index = rowOffset + x;
                    colors[index] = GetTerrainColor(_heightData[index], _riverData[index], Theme);
                }

                ReportPhaseProgress(progressCallback, 0.92f, 0.04f, Interlocked.Increment(ref rowsCompleted), Height, "Coloring terrain");
            });

            return colors;
        }

        public Color[] CreateTerrainDataMap(Action<float, string> progressCallback = null)
        {
            var terrainData = new Color[_heightData.Length];
            long rowsCompleted = 0;

            Parallel.For(0, Height, y =>
            {
                var rowOffset = y * Width;
                for (var x = 0; x < Width; x++)
                {
                    var index = rowOffset + x;
                    var encodedHeight = (ushort)Math.Clamp((int)MathF.Round(_heightData[index] * ushort.MaxValue), 0, ushort.MaxValue);
                    terrainData[index] = new Color(
                        (byte)(encodedHeight >> 8),
                        (byte)(encodedHeight & byte.MaxValue),
                        (byte)0,
                        _riverData[index] ? byte.MaxValue : (byte)0);
                }

                ReportPhaseProgress(progressCallback, 0.96f, 0.04f, Interlocked.Increment(ref rowsCompleted), Height, "Encoding terrain data");
            });

            return terrainData;
        }

        public Color GetColorForHeight(float height, bool isRiver)
        {
            return GetTerrainColor(height, isRiver, Theme);
        }

        public bool SampleRiver(float x, float y)
        {
            return IsRiver((int)MathF.Round(x), (int)MathF.Round(y));
        }

        public float SampleHeight(float x, float y)
        {
            var wrappedX = WrapCoordinate(x, Width);
            var wrappedY = WrapCoordinate(y, Height);
            var x0 = (int)MathF.Floor(wrappedX);
            var y0 = (int)MathF.Floor(wrappedY);
            var x1 = (x0 + 1) % Width;
            var y1 = (y0 + 1) % Height;
            var tx = wrappedX - x0;
            var ty = wrappedY - y0;

            var top = MathHelper.Lerp(GetHeight(x0, y0), GetHeight(x1, y0), tx);
            var bottom = MathHelper.Lerp(GetHeight(x0, y1), GetHeight(x1, y1), tx);
            return MathHelper.Lerp(top, bottom, ty);
        }

        public float SampleVoxelHeight(float x, float y)
        {
            return GetHeight((int)MathF.Floor(WrapCoordinate(x, Width)), (int)MathF.Floor(WrapCoordinate(y, Height)));
        }

        public float SampleVoxelHeight(int x, int y)
        {
            return GetHeight(WrapGridCoordinate(x, Width), WrapGridCoordinate(y, Height));
        }

        public Color SampleSurfaceColor(float x, float y)
        {
            var river = IsRiver((int)MathF.Round(x), (int)MathF.Round(y));
            return GetTerrainColor(SampleHeight(x, y), river, Theme);
        }

        public int DestroyTerrainSphere(Vector2 center, float centerWorldY, float radius, int protectedColumnHeight)
        {
            if (radius <= 0f)
            {
                return 0;
            }

            var removedCells = 0;
            var protectedHeight = Math.Clamp(protectedColumnHeight, 0, MaxCubeColumn - 1);
            var blastRadiusSquared = radius * radius;
            var gridRadius = Math.Max(1, (int)MathF.Ceiling(radius));
            var minSurfaceHeight = protectedHeight / MathF.Max(1f, MaxCubeColumn - 1f);

            for (var offsetY = -gridRadius; offsetY <= gridRadius; offsetY++)
            {
                for (var offsetX = -gridRadius; offsetX <= gridRadius; offsetX++)
                {
                    var worldX = WrapGridCoordinate((int)MathF.Floor(center.X) + offsetX, Width);
                    var worldY = WrapGridCoordinate((int)MathF.Floor(center.Y) + offsetY, Height);
                    var surfaceHeight = _heightData[GetIndex(worldX, worldY)];
                    var columnHeight = Math.Clamp((int)MathF.Round(surfaceHeight * (MaxCubeColumn - 1)), 1, MaxCubeColumn - 1);
                    if (columnHeight <= protectedHeight)
                    {
                        continue;
                    }

                    var cellCenterWorldY = GetCubeBottom(columnHeight, MaxCubeColumn) + FaceOverlap;
                    var distanceSquared =
                        (offsetX * offsetX) +
                        (offsetY * offsetY) +
                        ((cellCenterWorldY - centerWorldY) * (cellCenterWorldY - centerWorldY));
                    if (distanceSquared > blastRadiusSquared)
                    {
                        continue;
                    }

                    var targetColumn = Math.Max(protectedHeight, columnHeight - (int)MathF.Ceiling((radius - MathF.Sqrt(distanceSquared)) * 0.9f));
                    if (targetColumn >= columnHeight)
                    {
                        continue;
                    }

                    _heightData[GetIndex(worldX, worldY)] = MathF.Max(minSurfaceHeight, targetColumn / (float)(MaxCubeColumn - 1));
                    _riverData[GetIndex(worldX, worldY)] = false;
                    removedCells++;
                }
            }

            return removedCells;
        }

        public bool HasVolcanoVent(int x, int y)
        {
            var centerHeight = SampleVoxelHeight(x, y);
            if (centerHeight < 0.72f)
            {
                return false;
            }

            var broadVolcanoNoise = RidgedNoise((x - (Seed * 0.09f)) * 0.016f, (y + (Seed * 0.14f)) * 0.016f, 4, 1.95f, 0.6f, Seed ^ unchecked((int)0x27d4eb2d));
            if (broadVolcanoNoise < 0.8f)
            {
                return false;
            }

            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    if (SampleVoxelHeight(x + offsetX, y + offsetY) > centerHeight)
                    {
                        return false;
                    }
                }
            }

            var spacingGate = ValueNoise((x + (Seed * 0.013f)) * 0.19f, (y - (Seed * 0.021f)) * 0.19f, Seed ^ unchecked((int)0x6d2b79f5));
            return spacingGate > 0.7f;
        }

        private static float GetCubeBottom(int level, int chunkHeight)
        {
            return (level - GetVerticalOrigin(chunkHeight)) * 2f;
        }

        private static float GetVerticalOrigin(int chunkHeight)
        {
            return SeaLevel * (chunkHeight - 1);
        }

        private const float FaceOverlap = 0.08f;

        private void Generate(Action<float, string> progressCallback)
        {
            GenerateBaseTerrain(progressCallback);
            NormalizeHeights(progressCallback);
            ShapeTerrain(progressCallback);
            if (Theme.HasSurfaceWater)
            {
                CarveRivers(progressCallback);
            }
            GenerateTrees(progressCallback);
            progressCallback?.Invoke(0.92f, "Terrain ready");
        }

        private void GenerateTrees(Action<float, string> progressCallback)
        {
            _treesByChunk.Clear();
            GeneratedTreeCount = 0;
            if (TreeTargetCount == 0)
            {
                return;
            }

            var random = new Random(Seed ^ unchecked((int)0x4f1bbcdc));
            var occupiedCells = new HashSet<int>();
            var chunkTreeLists = new Dictionary<int, List<TreeInstance>>();
            var maxAttempts = Math.Max(TreeTargetCount * 32, 512);
            for (var attempt = 0; attempt < maxAttempts && GeneratedTreeCount < TreeTargetCount; attempt++)
            {
                var x = random.Next(Width);
                var y = random.Next(Height);
                var index = GetIndex(x, y);
                if (!occupiedCells.Add(index))
                {
                    continue;
                }

                if (!CanPlaceTree(x, y, out var surfaceHeight))
                {
                    continue;
                }

                var tree = CreateTreeInstance(x, y, surfaceHeight, random);
                var chunkKey = GetTreeChunkKey(x, y);
                if (!chunkTreeLists.TryGetValue(chunkKey, out var trees))
                {
                    trees = new List<TreeInstance>();
                    chunkTreeLists[chunkKey] = trees;
                }

                trees.Add(tree);
                GeneratedTreeCount++;
                ReportPhaseProgress(progressCallback, 0.87f, 0.05f, GeneratedTreeCount, TreeTargetCount, "Planting forests");
            }

            foreach (var treeChunk in chunkTreeLists)
            {
                _treesByChunk[treeChunk.Key] = treeChunk.Value.ToArray();
            }
        }

        private bool CanPlaceTree(int x, int y, out float surfaceHeight)
        {
            var index = GetIndex(x, y);
            surfaceHeight = _heightData[index];
            if (_riverData[index])
            {
                return false;
            }

            if (surfaceHeight < TreeMinHeight || surfaceHeight > TreeMaxHeight)
            {
                return false;
            }

            var left = GetHeight(x - 1, y);
            var right = GetHeight(x + 1, y);
            var back = GetHeight(x, y - 1);
            var front = GetHeight(x, y + 1);
            var minHeight = MathF.Min(MathF.Min(left, right), MathF.Min(back, front));
            var maxHeight = MathF.Max(MathF.Max(left, right), MathF.Max(back, front));
            return maxHeight - minHeight <= 0.06f;
        }

        private TreeInstance CreateTreeInstance(int x, int y, float surfaceHeight, Random random)
        {
            var trunkHeight = random.Next(8, 21);
            var canopyShape = random.NextDouble() < 0.6d ? TreeCanopyShape.Round : TreeCanopyShape.Cone;
            var canopyRadius = canopyShape == TreeCanopyShape.Round ? random.Next(2, 4) : random.Next(2, 5);
            var canopyLayers = canopyShape == TreeCanopyShape.Round ? random.Next(3, 5) : random.Next(4, 6);
            var foliageTone = random.Next(0, 5);
            return new TreeInstance(x, y, surfaceHeight, trunkHeight, canopyRadius, canopyLayers, canopyShape, foliageTone);
        }

        private int GetTreeChunkKey(int x, int y)
        {
            var chunkX = (x / TreeChunkSize) * TreeChunkSize;
            var chunkY = (y / TreeChunkSize) * TreeChunkSize;
            return GetIndex(chunkX, chunkY);
        }

        private void GenerateBaseTerrain(Action<float, string> progressCallback)
        {
            long rowsCompleted = 0;

            Parallel.For(0, Height, y =>
            {
                for (var x = 0; x < Width; x++)
                {
                    var continental = FractalNoise((x + (Seed * 0.37f)) * 0.0032f, (y - (Seed * 0.21f)) * 0.0032f, 5, 2.03f, 0.52f, Seed ^ 0x1f2e3d);
                    var detail = FractalNoise((x - (Seed * 0.19f)) * 0.0091f, (y + (Seed * 0.27f)) * 0.0091f, 4, 2.1f, 0.5f, Seed ^ unchecked((int)0x6a09e667));
                    var ridges = RidgedNoise((x + (Seed * 0.07f)) * 0.013f, (y - (Seed * 0.11f)) * 0.013f, 4, 2.05f, 0.5f, Seed ^ unchecked((int)0xbb67ae85));
                    var peaks = RidgedNoise((x - (Seed * 0.13f)) * 0.024f, (y + (Seed * 0.09f)) * 0.024f, 3, 2.18f, 0.54f, Seed ^ unchecked((int)0xa54ff53a));
                    var basins = FractalNoise((x + 17f) * 0.0014f, (y - 29f) * 0.0014f, 3, 2f, 0.55f, Seed ^ unchecked((int)0x5be0cd19));
                    var height = (continental * 0.56f) + (detail * 0.2f) + (ridges * 0.18f) + (peaks * 0.06f);
                    height = MathHelper.Lerp(height, basins, 0.1f);

                    _heightData[GetIndex(x, y)] = height;
                }

                ReportPhaseProgress(progressCallback, 0f, 0.56f, Interlocked.Increment(ref rowsCompleted), Height, "Generating terrain noise");
            });
        }

        private void NormalizeHeights(Action<float, string> progressCallback)
        {
            var minimum = float.MaxValue;
            var maximum = float.MinValue;
            var minMaxLock = new object();

            Parallel.For(0, Height, () => (Minimum: float.MaxValue, Maximum: float.MinValue), (y, _, local) =>
            {
                var rowOffset = y * Width;
                for (var x = 0; x < Width; x++)
                {
                    var value = _heightData[rowOffset + x];
                    local.Minimum = MathF.Min(local.Minimum, value);
                    local.Maximum = MathF.Max(local.Maximum, value);
                }

                return local;
            }, local =>
            {
                lock (minMaxLock)
                {
                    minimum = MathF.Min(minimum, local.Minimum);
                    maximum = MathF.Max(maximum, local.Maximum);
                }
            });

            var range = MathF.Max(0.0001f, maximum - minimum);
            long rowsCompleted = 0;

            Parallel.For(0, Height, y =>
            {
                var rowOffset = y * Width;
                for (var x = 0; x < Width; x++)
                {
                    var index = rowOffset + x;
                    _heightData[index] = (_heightData[index] - minimum) / range;
                }

                ReportPhaseProgress(progressCallback, 0.56f, 0.12f, Interlocked.Increment(ref rowsCompleted), Height, "Normalizing height map");
            });
        }

        private void ShapeTerrain(Action<float, string> progressCallback)
        {
            long rowsCompleted = 0;

            Parallel.For(0, Height, y =>
            {
                var rowOffset = y * Width;
                for (var x = 0; x < Width; x++)
                {
                    var index = rowOffset + x;
                    var height = _heightData[index];
                    var mountainNoise = RidgedNoise((x + (Seed * 0.05f)) * 0.019f, (y - (Seed * 0.03f)) * 0.019f, 4, 2.08f, 0.5f, Seed ^ unchecked((int)0x1234abcd));
                    var plateauNoise = FractalNoise((x + (Seed * 0.02f)) * 0.0034f, (y - (Seed * 0.04f)) * 0.0034f, 3, 1.92f, 0.56f, Seed ^ unchecked((int)0x51ed270b));
                    var volcanoNoise = RidgedNoise((x - (Seed * 0.09f)) * 0.016f, (y + (Seed * 0.14f)) * 0.016f, 4, 1.95f, 0.6f, Seed ^ unchecked((int)0x27d4eb2d));
                    var craterNoise = RidgedNoise((x + (Seed * 0.17f)) * 0.029f, (y - (Seed * 0.22f)) * 0.029f, 3, 2.14f, 0.48f, Seed ^ unchecked((int)0x85ebca6b));
                    var gorgeNoise = FractalNoise((x - (Seed * 0.11f)) * 0.015f, (y + (Seed * 0.08f)) * 0.015f, 4, 2.05f, 0.53f, Seed ^ unchecked((int)0xc2b2ae35));

                    height = MathF.Pow(height, MathHelper.Lerp(1.08f, 0.84f, _settings.MountainIntensity));
                    height -= MathHelper.Lerp(0.08f, 0.025f, _settings.MountainIntensity);
                    height += MathF.Max(0f, height - 0.64f) * MathHelper.Lerp(0.02f, 0.18f, _settings.MountainIntensity);
                    height += MathF.Max(0f, mountainNoise - 0.58f) * _settings.MountainIntensity * 0.24f;
                    if (_settings.PlateauIntensity > 0f && plateauNoise > MathHelper.Lerp(0.74f, 0.38f, _settings.PlateauIntensity))
                    {
                        var shelfLevels = MathHelper.Lerp(3f, 10f, _settings.PlateauIntensity);
                        var terracedHeight = MathF.Round(height * shelfLevels) / shelfLevels;
                        height = MathHelper.Lerp(height, terracedHeight, _settings.PlateauIntensity * MathHelper.Lerp(0.58f, 0.96f, plateauNoise));
                    }
                    var volcanoMask = MathHelper.Clamp((volcanoNoise - MathHelper.Lerp(0.66f, 0.42f, _settings.VolcanoIntensity)) / MathHelper.Lerp(0.24f, 0.34f, _settings.VolcanoIntensity), 0f, 1f);
                    if (volcanoMask > 0f)
                    {
                        var volcanicShelfLevels = MathHelper.Lerp(5f, 9f, _settings.VolcanoIntensity);
                        var volcanicPlateauHeight = MathF.Round((height + (volcanoMask * 0.08f)) * volcanicShelfLevels) / volcanicShelfLevels;
                        height = MathHelper.Lerp(height, volcanicPlateauHeight, volcanoMask * _settings.VolcanoIntensity * 0.38f);
                        height += volcanoMask * _settings.VolcanoIntensity * MathHelper.Lerp(0.16f, 0.34f, volcanoNoise);
                    }
                    height -= MathF.Max(0f, craterNoise - 0.74f) * _settings.CraterIntensity * 0.18f;
                    height -= MathF.Abs((gorgeNoise * 2f) - 1f) < MathHelper.Lerp(0.02f, 0.16f, _settings.GorgeIntensity)
                        ? _settings.GorgeIntensity * 0.12f
                        : 0f;
                    _heightData[index] = MathHelper.Clamp(height, 0f, 1f);
                }

                ReportPhaseProgress(progressCallback, 0.68f, 0.14f, Interlocked.Increment(ref rowsCompleted), Height, "Shaping mountains and seas");
            });
        }

        private void CarveRivers(Action<float, string> progressCallback)
        {
            var candidates = new List<(int X, int Y, float Height)>();
            var scanRows = Math.Max(1, ((Height - 4) + 5) / 6);
            var scannedRows = 0;

            for (var y = 2; y < Height - 2; y += 6)
            {
                for (var x = 2; x < Width - 2; x += 6)
                {
                    var currentHeight = GetHeight(x, y);
                    if (currentHeight < 0.72f)
                    {
                        continue;
                    }

                    if (currentHeight > GetHeight(x - 1, y) && currentHeight > GetHeight(x + 1, y) && currentHeight > GetHeight(x, y - 1) && currentHeight > GetHeight(x, y + 1))
                    {
                        candidates.Add((x, y, currentHeight));
                    }
                }

                scannedRows++;
                ReportPhaseProgress(progressCallback, 0.82f, 0.05f, scannedRows, scanRows, "Tracing river sources");
            }

            candidates.Sort((left, right) => right.Height.CompareTo(left.Height));

            var targetRiverCount = Math.Max(18, (Width * Height) / 22000);
            var riversCreated = 0;

            foreach (var candidate in candidates)
            {
                if (riversCreated >= targetRiverCount)
                {
                    break;
                }

                if (TraceRiver(candidate.X, candidate.Y))
                {
                    riversCreated++;
                    ReportPhaseProgress(progressCallback, 0.87f, 0.05f, riversCreated, targetRiverCount, "Carving river channels");
                }
            }
        }

        private static void ReportPhaseProgress(Action<float, string> progressCallback, float start, float span, long completed, long total, string status)
        {
            if (progressCallback == null || total <= 0)
            {
                return;
            }

            if (completed < total && (completed % 32) != 0)
            {
                return;
            }

            var progress = start + (span * MathHelper.Clamp(completed / (float)total, 0f, 1f));
            progressCallback(progress, status);
        }

        private static int WrapGridCoordinate(int value, int size)
        {
            var wrapped = value % size;
            return wrapped < 0 ? wrapped + size : wrapped;
        }

        private bool TraceRiver(int startX, int startY)
        {
            var visited = new HashSet<int>();
            var path = new List<int>();
            var currentX = startX;
            var currentY = startY;
            var reachedWater = false;
            var maxSteps = Width + Height;

            while (path.Count < maxSteps)
            {
                var currentIndex = GetWrappedIndex(currentX, currentY);
                if (!visited.Add(currentIndex))
                {
                    break;
                }

                var currentHeight = _heightData[currentIndex];
                if (currentHeight <= SeaLevel)
                {
                    reachedWater = path.Count >= 12;
                    break;
                }

                path.Add(currentIndex);

                var nextStep = FindNextRiverStep(currentX, currentY, visited);
                if (nextStep.X == currentX && nextStep.Y == currentY)
                {
                    _heightData[currentIndex] = MathF.Max(SeaLevel * 0.97f, currentHeight - 0.02f);
                    break;
                }

                if (nextStep.Height >= currentHeight)
                {
                    _heightData[currentIndex] = MathF.Max(SeaLevel * 0.97f, currentHeight - 0.014f);
                }

                currentX = nextStep.X;
                currentY = nextStep.Y;
            }

            if (!reachedWater && path.Count < 18)
            {
                return false;
            }

            for (var index = 0; index < path.Count; index++)
            {
                var cellIndex = path[index];
                _riverData[cellIndex] = true;
                _heightData[cellIndex] = MathF.Max(SeaLevel * 0.92f, _heightData[cellIndex] - 0.018f);

                if (index > path.Count / 3 && index % 7 == 0)
                {
                    var x = cellIndex % Width;
                    var y = cellIndex / Width;
                    WidenRiverBank(x - 1, y);
                    WidenRiverBank(x + 1, y);
                }
            }

            return true;
        }

        private (int X, int Y, float Height) FindNextRiverStep(int x, int y, HashSet<int> visited)
        {
            var bestX = x;
            var bestY = y;
            var bestHeight = float.MaxValue;

            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    var nextX = WrapIndex(x + offsetX, Width);
                    var nextY = WrapIndex(y + offsetY, Height);
                    var nextIndex = GetIndex(nextX, nextY);
                    if (visited.Contains(nextIndex))
                    {
                        continue;
                    }

                    var candidateHeight = _heightData[nextIndex];
                    if (candidateHeight < bestHeight)
                    {
                        bestHeight = candidateHeight;
                        bestX = nextX;
                        bestY = nextY;
                    }
                }
            }

            if (bestHeight == float.MaxValue)
            {
                return (x, y, GetHeight(x, y));
            }

            return (bestX, bestY, bestHeight);
        }

        private void WidenRiverBank(int x, int y)
        {
            var index = GetWrappedIndex(x, y);
            if (_heightData[index] <= SeaLevel + 0.03f)
            {
                _riverData[index] = true;
                _heightData[index] = MathF.Max(SeaLevel * 0.96f, _heightData[index] - 0.008f);
            }
        }

        private float GetHeight(int x, int y)
        {
            return _heightData[GetWrappedIndex(x, y)];
        }

        private bool IsRiver(int x, int y)
        {
            return _riverData[GetWrappedIndex(x, y)];
        }

        private int GetWrappedIndex(int x, int y)
        {
            return GetIndex(WrapIndex(x, Width), WrapIndex(y, Height));
        }

        private int GetIndex(int x, int y)
        {
            return (y * Width) + x;
        }

        private static int WrapIndex(int value, int size)
        {
            var wrapped = value % size;
            return wrapped < 0 ? wrapped + size : wrapped;
        }

        private static float WrapCoordinate(float value, int size)
        {
            var wrapped = value % size;
            return wrapped < 0f ? wrapped + size : wrapped;
        }

        private static Color GetTerrainColor(float height, bool isRiver, PlanetTheme theme)
        {
            if (theme.HasSurfaceWater && isRiver && height >= SeaLevel - 0.02f)
            {
                if (height < SeaLevel + 0.03f)
                {
                    return Color.Lerp(theme.RiverLowColor, Color.Lerp(theme.RiverLowColor, theme.RiverHighColor, 0.45f), (height - (SeaLevel - 0.02f)) / 0.05f);
                }

                return Color.Lerp(Color.Lerp(theme.RiverLowColor, theme.RiverHighColor, 0.35f), theme.RiverHighColor, MathHelper.Clamp((height - (SeaLevel + 0.03f)) / 0.2f, 0f, 1f));
            }

            if (theme.HasSurfaceWater && height < SeaLevel)
            {
                var oceanBlend = MathHelper.Clamp(height / SeaLevel, 0f, 1f);
                return Color.Lerp(theme.WaterLowColor, theme.WaterHighColor, oceanBlend);
            }

            var bands = theme.TerrainBands;
            for (var index = 0; index < bands.Length; index++)
            {
                var band = bands[index];
                if (height <= band.EndHeight || index == bands.Length - 1)
                {
                    var denominator = MathF.Max(0.0001f, band.EndHeight - band.StartHeight);
                    var blend = MathHelper.Clamp((height - band.StartHeight) / denominator, 0f, 1f);
                    return Color.Lerp(band.StartColor, band.EndColor, blend);
                }
            }

            return bands[^1].EndColor;
        }

        private static float FractalNoise(float x, float y, int octaves, float lacunarity, float persistence, int seed)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var total = 0f;
            var amplitudeSum = 0f;

            for (var octave = 0; octave < octaves; octave++)
            {
                total += ValueNoise(x * frequency, y * frequency, seed + (octave * 1013)) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return total / amplitudeSum;
        }

        private static float RidgedNoise(float x, float y, int octaves, float lacunarity, float persistence, int seed)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var total = 0f;
            var amplitudeSum = 0f;

            for (var octave = 0; octave < octaves; octave++)
            {
                var noiseValue = ValueNoise(x * frequency, y * frequency, seed + (octave * 1619));
                var ridge = 1f - MathF.Abs((noiseValue * 2f) - 1f);
                total += ridge * ridge * amplitude;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return total / amplitudeSum;
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            var x0 = (int)MathF.Floor(x);
            var y0 = (int)MathF.Floor(y);
            var x1 = x0 + 1;
            var y1 = y0 + 1;
            var tx = Smooth(x - x0);
            var ty = Smooth(y - y0);

            var top = MathHelper.Lerp(Random(x0, y0, seed), Random(x1, y0, seed), tx);
            var bottom = MathHelper.Lerp(Random(x0, y1, seed), Random(x1, y1, seed), tx);
            return MathHelper.Lerp(top, bottom, ty);
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - (2f * value));
        }

        private static float Random(int x, int y, int seed)
        {
            unchecked
            {
                var hash = seed;
                hash ^= x * 374761393;
                hash = (hash << 13) ^ hash;
                hash ^= y * 668265263;
                hash = (hash * 1274126177) ^ (hash >> 16);
                return (hash & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        public readonly struct TreeInstance
        {
            public TreeInstance(int x, int y, float surfaceHeight, int trunkHeight, int canopyRadius, int canopyLayers, TreeCanopyShape canopyShape, int foliageTone)
            {
                X = x;
                Y = y;
                SurfaceHeight = surfaceHeight;
                TrunkHeight = trunkHeight;
                CanopyRadius = canopyRadius;
                CanopyLayers = canopyLayers;
                CanopyShape = canopyShape;
                FoliageTone = foliageTone;
            }

            public int X { get; }

            public int Y { get; }

            public float SurfaceHeight { get; }

            public int TrunkHeight { get; }

            public int CanopyRadius { get; }

            public int CanopyLayers { get; }

            public TreeCanopyShape CanopyShape { get; }

            public int FoliageTone { get; }
        }

        public enum TreeCanopyShape
        {
            Round,
            Cone
        }
    }
}
