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
        private const byte FeatureNone = 0;
        private const byte FeatureDevelopment = 1;
        private const byte FeatureDevelopmentRoad = 2;
        private const byte FeatureDevelopmentRoadCenter = 3;
        private const byte FeatureTown = 4;
        private const byte FeatureTownRoad = 5;
        private const byte FeatureTownRoadCenter = 6;
        private const int DevelopmentMinSize = 30;
        private const int DevelopmentMaxSize = 50;
        private const int DevelopmentMinRoads = 2;
        private const int DevelopmentMaxRoads = 4;
        private const int DevelopmentRoadWidth = 4;
        private const int DevelopmentRoadBoundaryRoundaboutRadius = 5;
        private const int RoadsidePlotSize = 20;
        private const int TownMinSize = 20;
        private const int TownMaxSize = 50;
        private const int TownRoadConnectionCount = 2;
        private const int TownDevelopmentExclusionRadius = 20;
        private const int DevelopmentPadSlopeRadius = 8;
        private const int DevelopmentRoadSlopeRadius = 6;

        private readonly float[] _heightData;
        private readonly bool[] _riverData;
        private readonly byte[] _featureData;
        private readonly WorldGenerationSettings _settings;
        private Dictionary<int, TreeInstance[]> _treesByChunk;
        private Dictionary<int, TownDefenseSite[]> _townDefenseSitesByChunk;
        private List<Vector2> _developmentSiteCenters;
        private List<Rectangle> _developmentSiteBounds;
        private List<Rectangle> _townBounds;
        private List<TownTrafficLinkInfo> _townTrafficLinks;

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
            _featureData = new byte[width * height];
            _treesByChunk = new Dictionary<int, TreeInstance[]>();
            _townDefenseSitesByChunk = new Dictionary<int, TownDefenseSite[]>();
            _developmentSiteCenters = new List<Vector2>();
            _developmentSiteBounds = new List<Rectangle>();
            _townBounds = new List<Rectangle>();
            _townTrafficLinks = new List<TownTrafficLinkInfo>();

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

        public IReadOnlyList<TownDefenseSite> GetTownDefenseSitesInChunk(int chunkStartX, int chunkStartY)
        {
            var wrappedChunkX = WrapGridCoordinate(chunkStartX, Width);
            var wrappedChunkY = WrapGridCoordinate(chunkStartY, Height);
            var chunkKey = GetIndex(wrappedChunkX, wrappedChunkY);
            return _townDefenseSitesByChunk.TryGetValue(chunkKey, out var sites)
                ? sites
                : Array.Empty<TownDefenseSite>();
        }

        public IReadOnlyList<Vector2> GetDevelopmentSiteCenters()
        {
            return _developmentSiteCenters;
        }

        public IReadOnlyList<Rectangle> GetDevelopmentSiteBounds()
        {
            return _developmentSiteBounds;
        }

        public IReadOnlyList<Rectangle> GetTownBounds()
        {
            return _townBounds;
        }

        public IReadOnlyList<TownTrafficLinkInfo> GetTownTrafficLinks()
        {
            return _townTrafficLinks;
        }

        public Vector2 WrapPosition(Vector2 position)
        {
            return new Vector2(WrapCoordinate(position.X, Width), WrapCoordinate(position.Y, Height));
        }

        public bool TryFindNearestRoadCenter(Vector2 position, int searchRadius, out Vector2 roadCenter)
        {
            var centerX = WrapGridCoordinate((int)MathF.Round(position.X), Width);
            var centerY = WrapGridCoordinate((int)MathF.Round(position.Y), Height);
            var bestDistanceSquared = float.MaxValue;
            var bestRoadCenter = Vector2.Zero;
            var found = false;

            for (var offsetY = -searchRadius; offsetY <= searchRadius; offsetY++)
            {
                for (var offsetX = -searchRadius; offsetX <= searchRadius; offsetX++)
                {
                    var sampleX = WrapGridCoordinate(centerX + offsetX, Width);
                    var sampleY = WrapGridCoordinate(centerY + offsetY, Height);
                    if (!IsRoadCenterFeature(_featureData[GetIndex(sampleX, sampleY)]))
                    {
                        continue;
                    }

                    var delta = GetWrappedOffset(new Vector2(sampleX + 0.5f, sampleY + 0.5f) - position);
                    var distanceSquared = delta.LengthSquared();
                    if (distanceSquared >= bestDistanceSquared)
                    {
                        continue;
                    }

                    bestDistanceSquared = distanceSquared;
                    bestRoadCenter = WrapPosition(position + delta);
                    found = true;
                }
            }

            roadCenter = bestRoadCenter;
            return found;
        }

        public bool TryFindRoadCenterAlongDirection(Vector2 position, int direction, int forwardSearchDistance, int lateralSearchRadius, out Vector2 roadCenter)
        {
            var centerX = WrapGridCoordinate((int)MathF.Round(position.X), Width);
            var centerY = WrapGridCoordinate((int)MathF.Round(position.Y), Height);
            var alongAxisVertical = (direction & 1) == 0;
            var stepX = direction switch
            {
                1 => 1,
                3 => -1,
                _ => 0
            };
            var stepY = direction switch
            {
                0 => -1,
                2 => 1,
                _ => 0
            };

            for (var forwardStep = 0; forwardStep <= forwardSearchDistance; forwardStep++)
            {
                var baseX = WrapGridCoordinate(centerX + (stepX * forwardStep), Width);
                var baseY = WrapGridCoordinate(centerY + (stepY * forwardStep), Height);
                for (var lateralDistance = 0; lateralDistance <= lateralSearchRadius; lateralDistance++)
                {
                    if (TryGetRoadCenterAtDirectionalOffset(baseX, baseY, alongAxisVertical, lateralDistance, out roadCenter) ||
                        (lateralDistance > 0 && TryGetRoadCenterAtDirectionalOffset(baseX, baseY, alongAxisVertical, -lateralDistance, out roadCenter)))
                    {
                        return true;
                    }
                }
            }

            roadCenter = Vector2.Zero;
            return false;
        }

        public bool TryBuildRoadCenterPath(Vector2 start, Vector2 end, int maxSearchDistance, out List<Vector2> waypoints)
        {
            waypoints = null;
            var startX = WrapGridCoordinate((int)MathF.Floor(start.X), Width);
            var startY = WrapGridCoordinate((int)MathF.Floor(start.Y), Height);
            var endX = WrapGridCoordinate((int)MathF.Floor(end.X), Width);
            var endY = WrapGridCoordinate((int)MathF.Floor(end.Y), Height);
            var startIndex = GetIndex(startX, startY);
            var endIndex = GetIndex(endX, endY);
            var frontier = new Queue<int>();
            var visited = new HashSet<int> { startIndex };
            var cameFrom = new Dictionary<int, int>();
            frontier.Enqueue(startIndex);

            while (frontier.Count > 0)
            {
                var currentIndex = frontier.Dequeue();
                if (currentIndex == endIndex)
                {
                    waypoints = ReconstructRoadCenterPath(cameFrom, endIndex);
                    return true;
                }

                var currentX = currentIndex % Width;
                var currentY = currentIndex / Width;
                for (var direction = 0; direction < 4; direction++)
                {
                    var nextX = direction switch
                    {
                        0 => currentX,
                        1 => currentX + 1,
                        2 => currentX,
                        _ => currentX - 1
                    };
                    var nextY = direction switch
                    {
                        0 => currentY - 1,
                        1 => currentY,
                        2 => currentY + 1,
                        _ => currentY
                    };

                    nextX = WrapGridCoordinate(nextX, Width);
                    nextY = WrapGridCoordinate(nextY, Height);
                    var nextIndex = GetIndex(nextX, nextY);
                    if (visited.Contains(nextIndex) || !IsRoadCenterFeature(_featureData[nextIndex]))
                    {
                        continue;
                    }

                    var distanceFromStart = GetWrappedOffset(new Vector2(nextX + 0.5f, nextY + 0.5f) - start).Length();
                    if (distanceFromStart > maxSearchDistance)
                    {
                        continue;
                    }

                    visited.Add(nextIndex);
                    cameFrom[nextIndex] = currentIndex;
                    frontier.Enqueue(nextIndex);
                }
            }

            return false;
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
                    colors[index] = GetTerrainColor(_heightData[index], _riverData[index], _featureData[index], Theme);
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
            return GetTerrainColor(height, isRiver, FeatureNone, Theme);
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
            var wrappedX = WrapGridCoordinate((int)MathF.Floor(x), Width);
            var wrappedY = WrapGridCoordinate((int)MathF.Floor(y), Height);
            return GetTerrainColor(SampleHeight(x, y), river, _featureData[GetIndex(wrappedX, wrappedY)], Theme);
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

            GenerateDevelopmentSites(progressCallback);
            GenerateDistributedTownNetwork(progressCallback);
            BuildTownDefenseSites();
            GenerateTrees(progressCallback);
            progressCallback?.Invoke(0.92f, "Terrain ready");
        }

        private void BuildTownDefenseSites()
        {
            _townDefenseSitesByChunk.Clear();
            var visited = new bool[_featureData.Length];
            var chunkSites = new Dictionary<int, List<TownDefenseSite>>();
            var floodQueue = new Queue<Point>();

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var startIndex = GetIndex(x, y);
                    if (visited[startIndex] || _featureData[startIndex] != FeatureTown)
                    {
                        continue;
                    }

                    visited[startIndex] = true;
                    floodQueue.Enqueue(new Point(x, y));

                    var minX = x;
                    var maxX = x;
                    var minY = y;
                    var maxY = y;
                    var cellCount = 0;

                    while (floodQueue.Count > 0)
                    {
                        var point = floodQueue.Dequeue();
                        cellCount++;
                        minX = Math.Min(minX, point.X);
                        maxX = Math.Max(maxX, point.X);
                        minY = Math.Min(minY, point.Y);
                        maxY = Math.Max(maxY, point.Y);

                        TryVisitTownNeighbor(point.X - 1, point.Y, visited, floodQueue);
                        TryVisitTownNeighbor(point.X + 1, point.Y, visited, floodQueue);
                        TryVisitTownNeighbor(point.X, point.Y - 1, visited, floodQueue);
                        TryVisitTownNeighbor(point.X, point.Y + 1, visited, floodQueue);
                    }

                    if (cellCount < 64)
                    {
                        continue;
                    }

                    var centerX = (minX + maxX + 1) * 0.5f;
                    var centerY = (minY + maxY + 1) * 0.5f;
                    AddTownDefenseSite(chunkSites, centerX, minY + 0.5f, new Vector2(0f, -1f));
                    AddTownDefenseSite(chunkSites, maxX + 0.5f, centerY, new Vector2(1f, 0f));
                    AddTownDefenseSite(chunkSites, centerX, maxY + 0.5f, new Vector2(0f, 1f));
                    AddTownDefenseSite(chunkSites, minX + 0.5f, centerY, new Vector2(-1f, 0f));
                }
            }

            foreach (var chunkSite in chunkSites)
            {
                _townDefenseSitesByChunk[chunkSite.Key] = chunkSite.Value.ToArray();
            }
        }

        private void TryVisitTownNeighbor(int x, int y, bool[] visited, Queue<Point> floodQueue)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }

            var index = GetIndex(x, y);
            if (visited[index] || _featureData[index] != FeatureTown)
            {
                return;
            }

            visited[index] = true;
            floodQueue.Enqueue(new Point(x, y));
        }

        private void AddTownDefenseSite(Dictionary<int, List<TownDefenseSite>> chunkSites, float x, float y, Vector2 facingDirection)
        {
            var wrappedX = WrapCoordinate(x, Width);
            var wrappedY = WrapCoordinate(y, Height);
            var sampleX = WrapGridCoordinate((int)MathF.Floor(wrappedX), Width);
            var sampleY = WrapGridCoordinate((int)MathF.Floor(wrappedY), Height);
            var chunkX = (sampleX / TreeChunkSize) * TreeChunkSize;
            var chunkY = (sampleY / TreeChunkSize) * TreeChunkSize;
            var chunkKey = GetIndex(chunkX, chunkY);
            if (!chunkSites.TryGetValue(chunkKey, out var sites))
            {
                sites = new List<TownDefenseSite>();
                chunkSites[chunkKey] = sites;
            }

            sites.Add(new TownDefenseSite(new Vector2(wrappedX, wrappedY), _heightData[GetIndex(sampleX, sampleY)], facingDirection));
        }

        private void GenerateDistributedTownNetwork(Action<float, string> progressCallback)
        {
            _townBounds.Clear();
            _townTrafficLinks.Clear();
            var densityRatio = (_settings.TownDensity - WorldGenerationSettings.MinimumTownDensity) /
                (float)(WorldGenerationSettings.MaximumTownDensity - WorldGenerationSettings.MinimumTownDensity);
            var targetTownCount = (int)MathF.Round(MathHelper.Lerp(0f, 140f, densityRatio));
            if (targetTownCount <= 0)
            {
                ReportPhaseProgress(progressCallback, 0.89f, 0.02f, 1, 1, "Laying out towns and roads");
                return;
            }

            var roadSeed = new Random(Seed ^ unchecked((int)0x2b53aee1));
            var targetHeight = Math.Clamp((int)MathF.Round((SeaLevel * (MaxCubeColumn - 1)) + 3f), 1, MaxCubeColumn - 1) / (float)(MaxCubeColumn - 1);
            var townsPlaced = 0;
            var attempts = Math.Max(targetTownCount * 4, 64);

            for (var attempt = 0; attempt < attempts && townsPlaced < targetTownCount; attempt++)
            {
                var width = roadSeed.Next(TownMinSize, TownMaxSize + 1);
                var height = roadSeed.Next(TownMinSize, TownMaxSize + 1);
                var centerX = roadSeed.Next(width + 8, Width - width - 8);
                var centerY = roadSeed.Next(height + 8, Height - height - 8);
                var bounds = new Rectangle(centerX - (width / 2), centerY - (height / 2), width, height);
                if (!IsTownAreaAvailable(bounds))
                {
                    continue;
                }

                FlattenTownPad(bounds, targetHeight);
                _townBounds.Add(bounds);
                townsPlaced++;
            }

            GenerateTownConnections(targetHeight, roadSeed);

            ReportPhaseProgress(progressCallback, 0.89f, 0.02f, townsPlaced, Math.Max(1, targetTownCount), "Laying out towns and roads");
        }

        private void GenerateDevelopmentSites(Action<float, string> progressCallback)
        {
            if (Width < 96 || Height < 96)
            {
                return;
            }

            _developmentSiteCenters.Clear();
            _developmentSiteBounds.Clear();

            var random = new Random(Seed ^ unchecked((int)0x45c3d2e1));
            var targetHeight = Math.Clamp((int)MathF.Round((SeaLevel * (MaxCubeColumn - 1)) + 3f), 1, MaxCubeColumn - 1) / (float)(MaxCubeColumn - 1);
            var siteCount = 1 + (random.NextDouble() < 0.55d ? 1 : 0) + (random.NextDouble() < 0.2d ? 1 : 0);
            var sitesPlaced = 0;
            var minSpacing = DevelopmentMaxSize + 24;
            var placedSites = new List<Rectangle>();
            var centerMinX = Math.Max(DevelopmentMaxSize, (int)(Width * 0.24f));
            var centerMaxX = Math.Min(Width - DevelopmentMaxSize - 1, (int)(Width * 0.76f));
            var centerMinY = Math.Max(DevelopmentMaxSize, (int)(Height * 0.24f));
            var centerMaxY = Math.Min(Height - DevelopmentMaxSize - 1, (int)(Height * 0.76f));
            var maxAttempts = 24;

            for (var attempt = 0; attempt < maxAttempts && sitesPlaced < siteCount; attempt++)
            {
                var padWidth = random.Next(DevelopmentMinSize, DevelopmentMaxSize + 1);
                var padHeight = random.Next(DevelopmentMinSize, DevelopmentMaxSize + 1);
                var centerX = random.Next(centerMinX, centerMaxX + 1);
                var centerY = random.Next(centerMinY, centerMaxY + 1);
                var startX = centerX - (padWidth / 2);
                var startY = centerY - (padHeight / 2);
                var bounds = new Rectangle(startX, startY, padWidth, padHeight);

                if (bounds.Left < 6 || bounds.Top < 6 || bounds.Right >= Width - 6 || bounds.Bottom >= Height - 6)
                {
                    continue;
                }

                var overlaps = false;
                for (var index = 0; index < placedSites.Count; index++)
                {
                    var expanded = ExpandRectangle(placedSites[index], minSpacing);
                    if (expanded.Intersects(bounds))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (overlaps)
                {
                    continue;
                }

                FlattenDevelopmentPad(bounds, targetHeight);
                GenerateDevelopmentRoads(bounds, targetHeight, random, isTownRoad: false);
                placedSites.Add(bounds);
                _developmentSiteBounds.Add(bounds);
                _developmentSiteCenters.Add(new Vector2(bounds.Left + (bounds.Width * 0.5f), bounds.Top + (bounds.Height * 0.5f)));
                sitesPlaced++;
            }

            ReportPhaseProgress(progressCallback, 0.87f, 0.03f, sitesPlaced, Math.Max(1, siteCount), "Placing development sites");
        }

        private void FlattenTownPad(Rectangle bounds, float targetHeight)
        {
            var outerBounds = ExpandRectangle(bounds, DevelopmentPadSlopeRadius);
            var innerLeft = bounds.Left;
            var innerTop = bounds.Top;
            var innerRight = bounds.Right - 1;
            var innerBottom = bounds.Bottom - 1;

            for (var y = Math.Max(0, outerBounds.Top); y < Math.Min(Height, outerBounds.Bottom); y++)
            {
                for (var x = Math.Max(0, outerBounds.Left); x < Math.Min(Width, outerBounds.Right); x++)
                {
                    var index = GetIndex(x, y);
                    var distanceX = DistanceOutsideRange(x, innerLeft, innerRight);
                    var distanceY = DistanceOutsideRange(y, innerTop, innerBottom);
                    var edgeDistance = MathF.Sqrt((distanceX * distanceX) + (distanceY * distanceY));
                    if (edgeDistance > DevelopmentPadSlopeRadius)
                    {
                        continue;
                    }

                    var blend = edgeDistance <= 0f ? 1f : 1f - MathHelper.Clamp(edgeDistance / DevelopmentPadSlopeRadius, 0f, 1f);
                    _heightData[index] = MathHelper.Lerp(_heightData[index], targetHeight, blend);
                    _riverData[index] = false;
                    if (edgeDistance <= 0f)
                    {
                        _featureData[index] = FeatureTown;
                    }
                }
            }
        }

        private void GenerateDevelopmentRoads(Rectangle bounds, float targetHeight, Random random, bool isTownRoad)
        {
            var roadCount = random.Next(DevelopmentMinRoads, DevelopmentMaxRoads + 1);
            var usedDirections = new HashSet<int>();

            for (var roadIndex = 0; roadIndex < roadCount; roadIndex++)
            {
                var baseDirection = random.Next(4);
                var direction = baseDirection;
                for (var attempts = 0; attempts < 4 && !usedDirections.Add(direction); attempts++)
                {
                    direction = (baseDirection + attempts + 1) & 3;
                }

                var start = GetRoadStart(bounds, direction, random);
                var current = start;
                var directionVector = GetDirectionVector(direction);
                var nextTurnDistance = isTownRoad ? random.Next(6, 12) : int.MaxValue;
                var nextPlotDistance = random.Next(18, 36);
                var steps = 0;
                var maxSteps = Width + Height;
                Point? boundaryRoundaboutCenter = null;

                while (steps < maxSteps && IsInsideMap(current))
                {
                    if (isTownRoad && IsNearDevelopmentAreaOrRoad(current, TownDevelopmentExclusionRadius))
                    {
                        break;
                    }

                    CarveRoadCell(current.X, current.Y, targetHeight, direction, isTownRoad);
                    steps++;

                    if (isTownRoad)
                    {
                        nextTurnDistance--;
                        if (nextTurnDistance <= 0)
                        {
                            var turn = random.NextDouble() < 0.5d ? -1 : 1;
                            direction = (direction + turn + 4) & 3;
                            directionVector = GetDirectionVector(direction);
                            nextTurnDistance = random.Next(5, 11);
                        }
                    }

                    if (!isTownRoad)
                    {
                        nextPlotDistance--;
                        if (nextPlotDistance <= 0)
                        {
                            TryCreateRoadsidePlot(current, direction, targetHeight, random);
                            nextPlotDistance = random.Next(22, 42);
                        }
                    }

                    var next = current + directionVector;
                    if (!IsInsideMap(next))
                    {
                        boundaryRoundaboutCenter = ClampRoundaboutCenter(current, direction);
                        break;
                    }

                    current = next;
                }

                if (boundaryRoundaboutCenter.HasValue)
                {
                    CarveBoundaryRoundabout(boundaryRoundaboutCenter.Value, targetHeight, isTownRoad);
                }
            }
        }

        private void GenerateTownConnections(float targetHeight, Random random)
        {
            if (_townBounds.Count == 0)
            {
                return;
            }

            var usedConnections = new HashSet<long>();
            for (var townIndex = 0; townIndex < _townBounds.Count; townIndex++)
            {
                var townCenter = GetBoundsCenter(_townBounds[townIndex]);
                var connectionsCreated = 0;
                var candidates = BuildTownConnectionCandidates(townIndex, townCenter);
                for (var candidateIndex = 0; candidateIndex < candidates.Count && connectionsCreated < TownRoadConnectionCount; candidateIndex++)
                {
                    var candidate = candidates[candidateIndex];
                    var connectionKey = 0L;
                    if (candidate.TargetTownIndex >= 0)
                    {
                        connectionKey = GetTownConnectionKey(townIndex, candidate.TargetTownIndex);
                        if (usedConnections.Contains(connectionKey))
                        {
                            continue;
                        }
                    }

                    var sourceDirection = GetDirectionToward(_townBounds[townIndex], candidate.Target);
                    var targetBounds = candidate.TargetTownIndex >= 0
                        ? _townBounds[candidate.TargetTownIndex]
                        : default(Rectangle?);
                    if (TryCarveTownConnection(_townBounds[townIndex], targetBounds, candidate.Target, targetHeight, out var sourceRoadCenter, out var targetRoadCenter, out var connectionPath))
                    {
                        connectionsCreated++;
                        if (candidate.TargetTownIndex >= 0)
                        {
                            connectionPath = TrimConnectionPath(connectionPath, sourceRoadCenter, targetRoadCenter);
                            usedConnections.Add(connectionKey);
                            var targetDirection = GetDirectionToward(_townBounds[candidate.TargetTownIndex], GetBoundsCenter(_townBounds[townIndex]));
                            _townTrafficLinks.Add(new TownTrafficLinkInfo(townIndex, sourceDirection, candidate.TargetTownIndex, targetDirection, sourceRoadCenter, targetRoadCenter, connectionPath));
                            var reversePath = new List<Vector2>(connectionPath);
                            reversePath.Reverse();
                            _townTrafficLinks.Add(new TownTrafficLinkInfo(candidate.TargetTownIndex, targetDirection, townIndex, sourceDirection, targetRoadCenter, sourceRoadCenter, reversePath));
                        }
                    }
                }
            }
        }

        private List<TownRoadConnectionCandidate> BuildTownConnectionCandidates(int townIndex, Point townCenter)
        {
            var candidates = new List<TownRoadConnectionCandidate>();
            for (var otherTownIndex = 0; otherTownIndex < _townBounds.Count; otherTownIndex++)
            {
                if (otherTownIndex == townIndex)
                {
                    continue;
                }

                var otherCenter = GetBoundsCenter(_townBounds[otherTownIndex]);
                var deltaX = otherCenter.X - townCenter.X;
                var deltaY = otherCenter.Y - townCenter.Y;
                var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
                candidates.Add(new TownRoadConnectionCandidate(otherTownIndex, otherCenter, distanceSquared));
            }

            candidates.Sort((left, right) => left.Score.CompareTo(right.Score));
            var boundaryCandidates = new[]
            {
                new TownRoadConnectionCandidate(-1, new Point(townCenter.X, 1), townCenter.Y * townCenter.Y),
                new TownRoadConnectionCandidate(-1, new Point(Width - 2, townCenter.Y), (Width - 1 - townCenter.X) * (Width - 1 - townCenter.X)),
                new TownRoadConnectionCandidate(-1, new Point(townCenter.X, Height - 2), (Height - 1 - townCenter.Y) * (Height - 1 - townCenter.Y)),
                new TownRoadConnectionCandidate(-1, new Point(1, townCenter.Y), townCenter.X * townCenter.X)
            };
            Array.Sort(boundaryCandidates, (left, right) => left.Score.CompareTo(right.Score));
            candidates.AddRange(boundaryCandidates);
            return candidates;
        }

        private bool TryCarveTownConnection(Rectangle bounds, Rectangle? targetBounds, Point target, float targetHeight, out Vector2 sourceRoadCenter, out Vector2 targetRoadCenter, out List<Vector2> connectionPath)
        {
            var current = GetRoadStart(bounds, GetDirectionToward(bounds, target), new Random(Seed ^ (target.X * 397) ^ (target.Y * 7919)));
            Point? firstRoadPointOutsideSource = null;
            var firstRoadDirectionOutsideSource = -1;
            Point? lastRoadPointInsideSource = null;
            var lastRoadDirectionInsideSource = -1;
            Point? lastRoadPointOutsideTarget = null;
            var lastRoadDirectionOutsideTarget = -1;
            connectionPath = new List<Vector2>();
            var maxSteps = Width + Height;
            for (var steps = 0; steps < maxSteps; steps++)
            {
                if (!IsInsideMap(current) || IsBlockedTownConnectionCell(current, bounds, targetBounds, target))
                {
                    sourceRoadCenter = Vector2.Zero;
                    targetRoadCenter = Vector2.Zero;
                    connectionPath = new List<Vector2>();
                    return false;
                }

                var direction = GetTownConnectionStepDirection(current, target);
                CarveRoadCell(current.X, current.Y, targetHeight, direction, isTownRoad: true);
                if (bounds.Contains(current))
                {
                    lastRoadPointInsideSource = current;
                    lastRoadDirectionInsideSource = direction;
                }
                else if (!firstRoadPointOutsideSource.HasValue)
                {
                    firstRoadPointOutsideSource = current;
                    firstRoadDirectionOutsideSource = direction;
                }

                connectionPath.Add(GetCellCenterPosition(current));
                if (!targetBounds.HasValue || !targetBounds.Value.Contains(current))
                {
                    lastRoadPointOutsideTarget = current;
                    lastRoadDirectionOutsideTarget = direction;
                }

                if (Math.Abs(current.X - target.X) <= 1 && Math.Abs(current.Y - target.Y) <= 1)
                {
                    var resolvedSourcePoint = firstRoadPointOutsideSource ?? lastRoadPointInsideSource ?? current;
                    var resolvedSourceDirection = firstRoadDirectionOutsideSource >= 0
                        ? firstRoadDirectionOutsideSource
                        : (lastRoadDirectionInsideSource >= 0 ? lastRoadDirectionInsideSource : direction);
                    sourceRoadCenter = GetCarvedRoadCenterPosition(resolvedSourcePoint, resolvedSourceDirection);
                    var resolvedTargetPoint = lastRoadPointOutsideTarget ?? current;
                    targetRoadCenter = GetCarvedRoadCenterPosition(resolvedTargetPoint, lastRoadDirectionOutsideTarget >= 0 ? lastRoadDirectionOutsideTarget : direction);
                    return true;
                }

                var deltaX = target.X - current.X;
                var deltaY = target.Y - current.Y;
                if (Math.Abs(deltaX) >= Math.Abs(deltaY))
                {
                    current.X += Math.Sign(deltaX);
                }
                else
                {
                    current.Y += Math.Sign(deltaY);
                }
            }

            sourceRoadCenter = Vector2.Zero;
            targetRoadCenter = Vector2.Zero;
            connectionPath = new List<Vector2>();
            return false;
        }

        private static int GetTownConnectionStepDirection(Point current, Point target)
        {
            var deltaX = target.X - current.X;
            var deltaY = target.Y - current.Y;
            if (Math.Abs(deltaX) >= Math.Abs(deltaY))
            {
                return deltaX >= 0 ? 1 : 3;
            }

            return deltaY >= 0 ? 2 : 0;
        }

        private static Vector2 GetCarvedRoadCenterPosition(Point roadCell, int direction)
        {
            var alongAxisVertical = (direction & 1) == 0;
            return alongAxisVertical
                ? new Vector2(roadCell.X - 0.5f, roadCell.Y + 0.5f)
                : new Vector2(roadCell.X + 0.5f, roadCell.Y - 0.5f);
        }

        private static Vector2 GetCellCenterPosition(Point cell)
        {
            return new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
        }

        private static List<Vector2> TrimConnectionPath(IReadOnlyList<Vector2> connectionPath, Vector2 sourceRoadCenter, Vector2 targetRoadCenter)
        {
            if (connectionPath == null || connectionPath.Count == 0)
            {
                return new List<Vector2>();
            }

            if (sourceRoadCenter == Vector2.Zero || targetRoadCenter == Vector2.Zero)
            {
                return new List<Vector2>(connectionPath);
            }

            var startIndex = FindNearestPathIndex(connectionPath, sourceRoadCenter);
            var endIndex = FindNearestPathIndex(connectionPath, targetRoadCenter);
            if (startIndex > endIndex)
            {
                (startIndex, endIndex) = (endIndex, startIndex);
            }

            var trimmedPath = new List<Vector2>((endIndex - startIndex) + 3)
            {
                sourceRoadCenter
            };

            for (var pathIndex = startIndex; pathIndex <= endIndex; pathIndex++)
            {
                var waypoint = connectionPath[pathIndex];
                if (trimmedPath.Count > 0 && Vector2.DistanceSquared(trimmedPath[^1], waypoint) <= 0.01f)
                {
                    continue;
                }

                trimmedPath.Add(waypoint);
            }

            if (trimmedPath.Count == 0 || Vector2.DistanceSquared(trimmedPath[^1], targetRoadCenter) > 0.01f)
            {
                trimmedPath.Add(targetRoadCenter);
            }

            return trimmedPath;
        }

        private static int FindNearestPathIndex(IReadOnlyList<Vector2> connectionPath, Vector2 target)
        {
            var bestIndex = 0;
            var bestDistanceSquared = float.MaxValue;
            for (var index = 0; index < connectionPath.Count; index++)
            {
                var distanceSquared = Vector2.DistanceSquared(connectionPath[index], target);
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        private bool IsBlockedTownConnectionCell(Point current, Rectangle sourceBounds, Rectangle? targetBounds, Point target)
        {
            if (sourceBounds.Contains(current) ||
                (targetBounds.HasValue && targetBounds.Value.Contains(current)) ||
                Math.Abs(current.X - target.X) <= 1 && Math.Abs(current.Y - target.Y) <= 1)
            {
                return false;
            }

            var feature = _featureData[GetIndex(current.X, current.Y)];
            return feature == FeatureDevelopment ||
                feature == FeatureDevelopmentRoad ||
                feature == FeatureDevelopmentRoadCenter ||
                feature == FeatureTown ||
                feature == FeatureTownRoad ||
                feature == FeatureTownRoadCenter;
        }

        private static long GetTownConnectionKey(int firstTownIndex, int secondTownIndex)
        {
            var min = Math.Min(firstTownIndex, secondTownIndex);
            var max = Math.Max(firstTownIndex, secondTownIndex);
            return ((long)min << 32) | (uint)max;
        }

        private static Point GetBoundsCenter(Rectangle bounds)
        {
            return new Point(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2));
        }

        private int GetDirectionToward(Rectangle bounds, Point target)
        {
            return GetDirectionToward(GetBoundsCenter(bounds), target);
        }

        private static int GetDirectionToward(Point start, Point target)
        {
            var deltaX = target.X - start.X;
            var deltaY = target.Y - start.Y;
            if (Math.Abs(deltaX) >= Math.Abs(deltaY))
            {
                return deltaX >= 0 ? 1 : 3;
            }

            return deltaY >= 0 ? 2 : 0;
        }

        private void TryCreateRoadsidePlot(Point roadPosition, int direction, float targetHeight, Random random)
        {
            var perpendicularDirection = random.NextDouble() < 0.5d ? -1 : 1;
            var offset = (DevelopmentRoadWidth / 2) + (RoadsidePlotSize / 2) + 3;
            var plotCenter = (direction & 1) == 0
                ? new Point(roadPosition.X + (offset * perpendicularDirection), roadPosition.Y)
                : new Point(roadPosition.X, roadPosition.Y + (offset * perpendicularDirection));
            var bounds = new Rectangle(
                plotCenter.X - (RoadsidePlotSize / 2),
                plotCenter.Y - (RoadsidePlotSize / 2),
                RoadsidePlotSize,
                RoadsidePlotSize);

            if (bounds.Left < 4 || bounds.Top < 4 || bounds.Right >= Width - 4 || bounds.Bottom >= Height - 4)
            {
                return;
            }

            if (!IsFeatureAreaAvailable(bounds, allowRoadOverlap: false))
            {
                return;
            }

            FlattenDevelopmentPad(bounds, targetHeight);
        }

        private Point GetRoadStart(Rectangle bounds, int direction, Random random)
        {
            return direction switch
            {
                0 => new Point(bounds.Left + (bounds.Width / 2) + random.Next(-(bounds.Width / 3), (bounds.Width / 3) + 1), bounds.Top - 1),
                1 => new Point(bounds.Right, bounds.Top + (bounds.Height / 2) + random.Next(-(bounds.Height / 3), (bounds.Height / 3) + 1)),
                2 => new Point(bounds.Left + (bounds.Width / 2) + random.Next(-(bounds.Width / 3), (bounds.Width / 3) + 1), bounds.Bottom),
                _ => new Point(bounds.Left - 1, bounds.Top + (bounds.Height / 2) + random.Next(-(bounds.Height / 3), (bounds.Height / 3) + 1))
            };
        }

        private static Point GetDirectionVector(int direction)
        {
            return direction switch
            {
                0 => new Point(0, -1),
                1 => new Point(1, 0),
                2 => new Point(0, 1),
                _ => new Point(-1, 0)
            };
        }

        private Point ClampRoundaboutCenter(Point roadEnd, int direction)
        {
            var offset = direction switch
            {
                0 => new Point(0, -DevelopmentRoadBoundaryRoundaboutRadius),
                1 => new Point(DevelopmentRoadBoundaryRoundaboutRadius, 0),
                2 => new Point(0, DevelopmentRoadBoundaryRoundaboutRadius),
                _ => new Point(-DevelopmentRoadBoundaryRoundaboutRadius, 0)
            };

            return new Point(
                Math.Clamp(roadEnd.X + offset.X, DevelopmentRoadBoundaryRoundaboutRadius + 1, Width - DevelopmentRoadBoundaryRoundaboutRadius - 2),
                Math.Clamp(roadEnd.Y + offset.Y, DevelopmentRoadBoundaryRoundaboutRadius + 1, Height - DevelopmentRoadBoundaryRoundaboutRadius - 2));
        }

        private bool IsInsideMap(Point position)
        {
            return position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
        }

        private bool IsNearDevelopmentAreaOrRoad(Point position, int radius)
        {
            var minX = Math.Max(0, position.X - radius);
            var maxX = Math.Min(Width - 1, position.X + radius);
            var minY = Math.Max(0, position.Y - radius);
            var maxY = Math.Min(Height - 1, position.Y + radius);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var feature = _featureData[GetIndex(x, y)];
                    if (feature == FeatureDevelopment ||
                        feature == FeatureDevelopmentRoad ||
                        feature == FeatureDevelopmentRoadCenter)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void CarveBoundaryRoundabout(Point center, float targetHeight, bool isTownRoad)
        {
            var radius = DevelopmentRoadBoundaryRoundaboutRadius;
            var ringHalfWidth = DevelopmentRoadWidth * 0.5f;
            for (var y = center.Y - radius - DevelopmentRoadSlopeRadius; y <= center.Y + radius + DevelopmentRoadSlopeRadius; y++)
            {
                for (var x = center.X - radius - DevelopmentRoadSlopeRadius; x <= center.X + radius + DevelopmentRoadSlopeRadius; x++)
                {
                    if (x < 0 || x >= Width || y < 0 || y >= Height)
                    {
                        continue;
                    }

                    var dx = x - center.X;
                    var dy = y - center.Y;
                    var radialDistance = MathF.Sqrt((dx * dx) + (dy * dy));
                    var distanceFromRing = MathF.Abs(radialDistance - radius);
                    if (distanceFromRing > ringHalfWidth + DevelopmentRoadSlopeRadius)
                    {
                        continue;
                    }

                    var blend = distanceFromRing <= ringHalfWidth
                        ? 1f
                        : 1f - MathHelper.Clamp((distanceFromRing - ringHalfWidth) / DevelopmentRoadSlopeRadius, 0f, 1f);
                    var index = GetIndex(x, y);
                    _heightData[index] = MathHelper.Lerp(_heightData[index], targetHeight, blend);
                    _riverData[index] = false;
                    if (distanceFromRing <= ringHalfWidth && _featureData[index] != FeatureDevelopment && _featureData[index] != FeatureTown)
                    {
                        _featureData[index] = distanceFromRing <= 1f
                            ? (isTownRoad ? FeatureTownRoadCenter : FeatureDevelopmentRoadCenter)
                            : (isTownRoad ? FeatureTownRoad : FeatureDevelopmentRoad);
                    }
                }
            }
        }

        private void CarveRoadCell(int centerX, int centerY, float targetHeight, int direction, bool isTownRoad)
        {
            var lateralStart = -DevelopmentRoadWidth / 2;
            var lateralEnd = lateralStart + DevelopmentRoadWidth - 1;
            var alongAxisVertical = (direction & 1) == 0;

            for (var lateral = lateralStart - DevelopmentRoadSlopeRadius; lateral <= lateralEnd + DevelopmentRoadSlopeRadius; lateral++)
            {
                var absoluteDistance = DistanceOutsideRange(lateral, lateralStart, lateralEnd);
                if (absoluteDistance > DevelopmentRoadSlopeRadius)
                {
                    continue;
                }

                var blend = absoluteDistance <= 0f ? 1f : 1f - MathHelper.Clamp(absoluteDistance / (float)DevelopmentRoadSlopeRadius, 0f, 1f);
                var x = alongAxisVertical ? centerX + lateral : centerX;
                var y = alongAxisVertical ? centerY : centerY + lateral;
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                {
                    continue;
                }

                var index = GetIndex(x, y);
                _heightData[index] = MathHelper.Lerp(_heightData[index], targetHeight, blend);
                _riverData[index] = false;
                if (absoluteDistance <= 0f &&
                    _featureData[index] != FeatureDevelopment &&
                    _featureData[index] != FeatureTown &&
                    (!isTownRoad || (_featureData[index] != FeatureDevelopmentRoad && _featureData[index] != FeatureDevelopmentRoadCenter)))
                {
                    var isCenterStrip = lateral >= -1 && lateral <= 0;
                    _featureData[index] = isCenterStrip
                        ? (isTownRoad ? FeatureTownRoadCenter : FeatureDevelopmentRoadCenter)
                        : (isTownRoad ? FeatureTownRoad : FeatureDevelopmentRoad);
                }
            }
        }

        private void FlattenDevelopmentPad(Rectangle bounds, float targetHeight)
        {
            var centerX = bounds.Left + (bounds.Width / 2f);
            var centerY = bounds.Top + (bounds.Height / 2f);
            var outerBounds = ExpandRectangle(bounds, DevelopmentPadSlopeRadius);
            var innerLeft = bounds.Left;
            var innerTop = bounds.Top;
            var innerRight = bounds.Right - 1;
            var innerBottom = bounds.Bottom - 1;

            for (var y = Math.Max(0, outerBounds.Top); y < Math.Min(Height, outerBounds.Bottom); y++)
            {
                for (var x = Math.Max(0, outerBounds.Left); x < Math.Min(Width, outerBounds.Right); x++)
                {
                    var index = GetIndex(x, y);
                    var distanceX = DistanceOutsideRange(x, innerLeft, innerRight);
                    var distanceY = DistanceOutsideRange(y, innerTop, innerBottom);
                    var edgeDistance = MathF.Sqrt((distanceX * distanceX) + (distanceY * distanceY));
                    if (edgeDistance > DevelopmentPadSlopeRadius)
                    {
                        continue;
                    }

                    var blend = edgeDistance <= 0f ? 1f : 1f - MathHelper.Clamp(edgeDistance / DevelopmentPadSlopeRadius, 0f, 1f);
                    _heightData[index] = MathHelper.Lerp(_heightData[index], targetHeight, blend);
                    _riverData[index] = false;
                    if (edgeDistance <= 0f)
                    {
                        _featureData[index] = FeatureDevelopment;
                    }
                }
            }
        }

        private static Rectangle ExpandRectangle(Rectangle bounds, int amount)
        {
            return new Rectangle(bounds.X - amount, bounds.Y - amount, bounds.Width + (amount * 2), bounds.Height + (amount * 2));
        }

        private bool IsFeatureAreaAvailable(Rectangle bounds, bool allowRoadOverlap)
        {
            var occupiedCells = 0;
            var sampledCells = 0;
            for (var y = bounds.Top; y < bounds.Bottom; y += 2)
            {
                for (var x = bounds.Left; x < bounds.Right; x += 2)
                {
                    var feature = _featureData[GetIndex(x, y)];
                    if (feature == FeatureDevelopment || (!allowRoadOverlap && feature != FeatureNone))
                    {
                        occupiedCells++;
                    }

                    sampledCells++;
                }
            }

            return occupiedCells <= Math.Max(2, sampledCells / 12);
        }

        private bool IsTownAreaAvailable(Rectangle bounds)
        {
            if (!IsFeatureAreaAvailable(bounds, allowRoadOverlap: false))
            {
                return false;
            }

            for (var index = 0; index < _developmentSiteBounds.Count; index++)
            {
                if (ExpandRectangle(_developmentSiteBounds[index], TownDevelopmentExclusionRadius).Intersects(bounds))
                {
                    return false;
                }
            }

            var exclusionBounds = ExpandRectangle(bounds, TownDevelopmentExclusionRadius);
            for (var y = Math.Max(0, exclusionBounds.Top); y < Math.Min(Height, exclusionBounds.Bottom); y++)
            {
                for (var x = Math.Max(0, exclusionBounds.Left); x < Math.Min(Width, exclusionBounds.Right); x++)
                {
                    var feature = _featureData[GetIndex(x, y)];
                    if (feature == FeatureDevelopment ||
                        feature == FeatureDevelopmentRoad ||
                        feature == FeatureDevelopmentRoadCenter ||
                        feature == FeatureTownRoad ||
                        feature == FeatureTownRoadCenter)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static float DistanceOutsideRange(int value, int min, int max)
        {
            if (value < min)
            {
                return min - value;
            }

            if (value > max)
            {
                return value - max;
            }

            return 0f;
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

            if (_featureData[index] != FeatureNone)
            {
                return false;
            }

            for (var offsetY = -2; offsetY <= 2; offsetY++)
            {
                for (var offsetX = -2; offsetX <= 2; offsetX++)
                {
                    if (_featureData[GetWrappedIndex(x + offsetX, y + offsetY)] != FeatureNone)
                    {
                        return false;
                    }
                }
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

        private List<Vector2> ReconstructRoadCenterPath(Dictionary<int, int> cameFrom, int endIndex)
        {
            var indices = new List<int> { endIndex };
            var currentIndex = endIndex;
            while (cameFrom.TryGetValue(currentIndex, out var previousIndex))
            {
                indices.Add(previousIndex);
                currentIndex = previousIndex;
            }

            indices.Reverse();
            var waypoints = new List<Vector2>(indices.Count);
            for (var index = 0; index < indices.Count; index++)
            {
                var cellIndex = indices[index];
                var x = cellIndex % Width;
                var y = cellIndex / Width;
                waypoints.Add(new Vector2(x + 0.5f, y + 0.5f));
            }

            return waypoints;
        }

        private bool TryGetRoadCenterAtDirectionalOffset(int baseX, int baseY, bool alongAxisVertical, int lateralOffset, out Vector2 roadCenter)
        {
            var sampleX = alongAxisVertical ? WrapGridCoordinate(baseX + lateralOffset, Width) : baseX;
            var sampleY = alongAxisVertical ? baseY : WrapGridCoordinate(baseY + lateralOffset, Height);
            if (IsRoadCenterFeature(_featureData[GetIndex(sampleX, sampleY)]))
            {
                roadCenter = new Vector2(sampleX + 0.5f, sampleY + 0.5f);
                return true;
            }

            roadCenter = Vector2.Zero;
            return false;
        }

        private Vector2 GetWrappedOffset(Vector2 offset)
        {
            if (offset.X > Width * 0.5f)
            {
                offset.X -= Width;
            }
            else if (offset.X < -Width * 0.5f)
            {
                offset.X += Width;
            }

            if (offset.Y > Height * 0.5f)
            {
                offset.Y -= Height;
            }
            else if (offset.Y < -Height * 0.5f)
            {
                offset.Y += Height;
            }

            return offset;
        }

        private static bool IsRoadCenterFeature(byte featureType)
        {
            return featureType == FeatureTownRoadCenter || featureType == FeatureDevelopmentRoadCenter;
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

        private static Color GetTerrainColor(float height, bool isRiver, byte featureType, PlanetTheme theme)
        {
            if (featureType == FeatureDevelopmentRoad)
            {
                return new Color(118, 126, 136);
            }

            if (featureType == FeatureDevelopmentRoadCenter)
            {
                return new Color(182, 188, 198);
            }

            if (featureType == FeatureDevelopment)
            {
                return new Color(72, 120, 214);
            }

            if (featureType == FeatureTown)
            {
                return new Color(146, 128, 96);
            }

            if (featureType == FeatureTownRoad)
            {
                return new Color(232, 198, 72);
            }

            if (featureType == FeatureTownRoadCenter)
            {
                return new Color(248, 238, 182);
            }

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

        public readonly struct TownDefenseSite
        {
            public TownDefenseSite(Vector2 position, float surfaceHeight, Vector2 facingDirection)
            {
                Position = position;
                SurfaceHeight = surfaceHeight;
                FacingDirection = facingDirection;
            }

            public Vector2 Position { get; }

            public float SurfaceHeight { get; }

            public Vector2 FacingDirection { get; }
        }

        public readonly record struct TownTrafficLinkInfo(int SourceTownIndex, int SourceDirection, int TargetTownIndex, int TargetDirection, Vector2 SourceRoadCenter, Vector2 TargetRoadCenter, IReadOnlyList<Vector2> ConnectionPath);

        private readonly record struct TownRoadConnectionCandidate(int TargetTownIndex, Point Target, int Score);

        public enum TreeCanopyShape
        {
            Round,
            Cone
        }
    }
}
