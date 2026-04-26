using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PEPAR.Modules.Game
{
    internal sealed class WormsTerrainDrawer
    {
        private const int DefaultChunkSize = 256;
        private const float CenterFlatteningStrength = 0.82f;
        private const float MountainBandHeightPercent = 0.24f;
        private const float ReferenceBlastRadius = 26f;
        private const int ReferenceBlastParticleCount = 200;
        private const float ParticleGravity = 720f/2f;
        private const float ParticleLifetimeSeconds = 10.4f;
        private const float ParticleMinSize = 2f;
        private const float ParticleMaxSize = 4f;
        private const float ExplosionBloomLifetimeSeconds = 0.24f;
        private readonly Dictionary<Point, Texture2D> _chunkTextures = [];
        private readonly Dictionary<int, Color> _depositPixelMap = [];
        private readonly HashSet<int> _depositChunkKeys = [];
        private readonly ConcurrentQueue<Point> _pendingChunkBuilds = [];
        private readonly ConcurrentQueue<ChunkPixelBuffer> _completedChunkBuilds = [];
        private readonly List<TerrainBlast> _terrainBlasts = [];
        private readonly List<TerrainDeposit> _terrainDeposits = [];
        private readonly List<TerrainParticle> _terrainParticles = [];
        private readonly List<ExplosionBloom> _explosionBlooms = [];
        private GraphicsDevice? _graphicsDevice;
        private Texture2D? _particleTexture;
        private Texture2D? _bloomTexture;
        private Random? _particleRandom;
        private CancellationTokenSource? _chunkBuildCancellationTokenSource;
        private Task[] _chunkBuildWorkers = [];
        private int _chunkSize = DefaultChunkSize;
        private int _trackedDepositCount = -1;
        private int _seed;

        internal int MapWidth { get; private set; }

        internal int MapHeight { get; private set; }

        internal int TotalChunkCount { get; private set; }

        internal int PreparedChunkCount { get; private set; }

        internal float PreloadProgress => TotalChunkCount == 0 ? 1f : PreparedChunkCount / (float)TotalChunkCount;

        internal bool IsPreloadComplete => PreparedChunkCount >= TotalChunkCount;

        internal bool TerrainVisualsChanged { get; private set; }

        internal bool HasActiveEffects => _terrainParticles.Count > 0 || _explosionBlooms.Count > 0;

        internal void Load(GraphicsDevice graphicsDevice, int mapWidth, int mapHeight, int? seed = null, int chunkSize = DefaultChunkSize)
        {
            Unload();

            _graphicsDevice = graphicsDevice;
            MapWidth = Math.Max(1, mapWidth);
            MapHeight = Math.Max(1, mapHeight);
            _seed = seed ?? Environment.TickCount;
            _particleRandom = new Random(unchecked(_seed * 397) ^ 0x5f3759df);
            _chunkSize = Math.Clamp(chunkSize, 64, 512);
            TotalChunkCount = 0;
            PreparedChunkCount = 0;
            TerrainVisualsChanged = true;
            InitializeChunkQueue();
            _terrainBlasts.Clear();
            _terrainDeposits.Clear();
            _depositPixelMap.Clear();
            _depositChunkKeys.Clear();
            _trackedDepositCount = 0;
            _terrainParticles.Clear();
            _explosionBlooms.Clear();
            _particleTexture?.Dispose();
            _particleTexture = new Texture2D(graphicsDevice, 1, 1);
            _particleTexture.SetData([Color.White]);
            _bloomTexture?.Dispose();
            _bloomTexture = CreateRadialTexture(graphicsDevice, 64, 0.68f);
            StartChunkBuildWorkers();
        }

        internal Rectangle GetMapBounds()
        {
            return new Rectangle(0, 0, MapWidth, MapHeight);
        }

        internal void Draw(SpriteBatch spriteBatch, Rectangle worldView, Vector2 screenPosition)
        {
            if (_graphicsDevice is null || MapWidth <= 0 || MapHeight <= 0)
            {
                return;
            }

            var clippedWorldView = Rectangle.Intersect(worldView, new Rectangle(0, 0, MapWidth, MapHeight));
            if (clippedWorldView.Width <= 0 || clippedWorldView.Height <= 0)
            {
                return;
            }

            var startChunkX = clippedWorldView.Left / _chunkSize;
            var endChunkX = (clippedWorldView.Right - 1) / _chunkSize;
            var startChunkY = clippedWorldView.Top / _chunkSize;
            var endChunkY = (clippedWorldView.Bottom - 1) / _chunkSize;

            for (var chunkY = startChunkY; chunkY <= endChunkY; chunkY++)
            {
                for (var chunkX = startChunkX; chunkX <= endChunkX; chunkX++)
                {
                    var chunkTexture = GetOrCreateChunkTexture(chunkX, chunkY);
                    var worldPosition = new Vector2(chunkX * _chunkSize, chunkY * _chunkSize);
                    var drawPosition = screenPosition + worldPosition - clippedWorldView.Location.ToVector2();
                    spriteBatch.Draw(chunkTexture, drawPosition, Color.White);
                }
            }

            DrawBlooms(spriteBatch, clippedWorldView, screenPosition);
            DrawParticles(spriteBatch, clippedWorldView, screenPosition);
        }

        internal void Update(GameTime gameTime)
        {
            var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            UpdateBlooms(elapsedSeconds);

            if (_terrainParticles.Count == 0)
            {
                return;
            }

            for (var index = _terrainParticles.Count - 1; index >= 0; index--)
            {
                var particle = _terrainParticles[index];
                particle.Age += elapsedSeconds;
                if (particle.Age >= particle.Lifetime)
                {
                    _terrainParticles.RemoveAt(index);
                    continue;
                }

                particle.Velocity = new Vector2(particle.Velocity.X * 0.996f, particle.Velocity.Y + (ParticleGravity * elapsedSeconds));
                var nextPosition = particle.Position + (particle.Velocity * elapsedSeconds);
                var nextPoint = new Point((int)MathF.Round(nextPosition.X), (int)MathF.Round(nextPosition.Y));
                if (nextPoint.X < 0 || nextPoint.Y < 0 || nextPoint.X >= MapWidth || nextPoint.Y >= MapHeight)
                {
                    _terrainParticles.RemoveAt(index);
                    continue;
                }

                if (IsSolid(nextPoint))
                {
                    particle.BouncesRemaining--;
                    if (particle.BouncesRemaining < 0)
                    {
                        _terrainParticles.RemoveAt(index);
                        continue;
                    }

                    particle.Position = new Vector2(nextPosition.X, nextPosition.Y - 1f);
                    particle.Velocity = new Vector2(particle.Velocity.X * 0.45f, -MathF.Abs(particle.Velocity.Y) * 0.22f);
                    particle.Age += elapsedSeconds * 0.35f;
                }
                else
                {
                    particle.Position = nextPosition;
                }
            }

        }

        internal bool ConsumeTerrainVisualsChanged()
        {
            var changed = TerrainVisualsChanged;
            TerrainVisualsChanged = false;
            return changed;
        }

        internal int PrepareNextChunks(int chunkCount)
        {
            if (_graphicsDevice is null || chunkCount <= 0)
            {
                return 0;
            }

            var preparedChunks = 0;
            while (preparedChunks < chunkCount && _completedChunkBuilds.TryDequeue(out var chunkBuffer))
            {
                if (!_chunkTextures.ContainsKey(chunkBuffer.Key))
                {
                    _chunkTextures[chunkBuffer.Key] = CreateChunkTexture(chunkBuffer);
                }

                PreparedChunkCount++;
                preparedChunks++;
            }

            return preparedChunks;
        }

        internal bool IsSolid(Point worldPoint)
        {
            return IsSolid(worldPoint.X, worldPoint.Y);
        }

        internal bool IsSolid(int worldX, int worldY)
        {
            if (worldX < 0 || worldY < 0 || worldX >= MapWidth || worldY >= MapHeight)
            {
                return false;
            }

            if (!IsGeneratedSolid(worldX, worldY))
            {
                return false;
            }

            return !IsDestroyed(worldX, worldY);
        }

        internal void DestroyCircle(Vector2 worldPosition, float radius)
        {
            if (_graphicsDevice is null || radius <= 0f)
            {
                return;
            }

            var clampedCenter = new Vector2(
                Math.Clamp(worldPosition.X, 0f, MapWidth - 1f),
                Math.Clamp(worldPosition.Y, 0f, MapHeight - 1f));

            var blast = new TerrainBlast(clampedCenter, radius);
            _terrainBlasts.Add(blast);
            RemoveDeposits(blast.Bounds);
            InvalidateChunks(blast.Bounds);
            TerrainVisualsChanged = true;
            EmitBlastParticles(blast);
        }

        internal void Unload()
        {
            StopChunkBuildWorkers();

            foreach (var chunkTexture in _chunkTextures.Values)
            {
                chunkTexture.Dispose();
            }

            _chunkTextures.Clear();
            ClearPendingChunkBuilds();
            ClearCompletedChunkBuilds();
            _terrainBlasts.Clear();
            _terrainDeposits.Clear();
            _depositPixelMap.Clear();
            _depositChunkKeys.Clear();
            _trackedDepositCount = 0;
            _terrainParticles.Clear();
            _explosionBlooms.Clear();
            _particleTexture?.Dispose();
            _particleTexture = null;
            _bloomTexture?.Dispose();
            _bloomTexture = null;
            TotalChunkCount = 0;
            PreparedChunkCount = 0;
        }

        internal Color[] BuildOverviewPixels(int width, int height)
        {
            var overviewWidth = Math.Max(1, width);
            var overviewHeight = Math.Max(1, height);
            var pixels = new Color[overviewWidth * overviewHeight];
            var worldXs = new int[overviewWidth];
            var surfaceHeights = new int[overviewWidth];

            EnsureDepositPixelMap();

            for (var overviewX = 0; overviewX < overviewWidth; overviewX++)
            {
                worldXs[overviewX] = Math.Min(MapWidth - 1, (int)MathF.Round((overviewX / (float)Math.Max(1, overviewWidth - 1)) * (MapWidth - 1)));
                surfaceHeights[overviewX] = GetSurfaceHeight(worldXs[overviewX]);
            }

            for (var overviewY = 0; overviewY < overviewHeight; overviewY++)
            {
                var worldY = Math.Min(MapHeight - 1, (int)MathF.Round((overviewY / (float)Math.Max(1, overviewHeight - 1)) * (MapHeight - 1)));
                for (var overviewX = 0; overviewX < overviewWidth; overviewX++)
                {
                    var worldX = worldXs[overviewX];
                    var depositColor = GetDepositColorAt(worldX, worldY);
                    if (depositColor is Color settledColor)
                    {
                        pixels[(overviewY * overviewWidth) + overviewX] = settledColor;
                        continue;
                    }

                    var surfaceHeight = surfaceHeights[overviewX];
                    pixels[(overviewY * overviewWidth) + overviewX] = worldY >= surfaceHeight && !IsDestroyed(worldX, worldY)
                        ? GetTerrainColor(worldX, worldY, surfaceHeight)
                        : Color.Transparent;
                }
            }

            return pixels;
        }

        private Texture2D GetOrCreateChunkTexture(int chunkX, int chunkY)
        {
            var key = new Point(chunkX, chunkY);
            if (_chunkTextures.TryGetValue(key, out var chunkTexture))
            {
                return chunkTexture;
            }

            chunkTexture = BuildChunkTexture(chunkX, chunkY);
            _chunkTextures[key] = chunkTexture;
            return chunkTexture;
        }

        private Texture2D BuildChunkTexture(int chunkX, int chunkY)
        {
            return CreateChunkTexture(BuildChunkPixelBuffer(chunkX, chunkY));
        }

        private ChunkPixelBuffer BuildChunkPixelBuffer(int chunkX, int chunkY)
        {
            var chunkStartX = chunkX * _chunkSize;
            var chunkStartY = chunkY * _chunkSize;
            var chunkWidth = Math.Min(_chunkSize, MapWidth - chunkStartX);
            var chunkHeight = Math.Min(_chunkSize, MapHeight - chunkStartY);
            var pixels = new Color[chunkWidth * chunkHeight];
            var surfaceHeights = new int[chunkWidth];
            var hasDepositsInChunk = ChunkHasDeposits(chunkX, chunkY);

            EnsureDepositPixelMap();

            for (var localX = 0; localX < chunkWidth; localX++)
            {
                surfaceHeights[localX] = GetSurfaceHeight(chunkStartX + localX);
            }

            for (var localY = 0; localY < chunkHeight; localY++)
            {
                var worldY = chunkStartY + localY;
                for (var localX = 0; localX < chunkWidth; localX++)
                {
                    var worldX = chunkStartX + localX;
                    var surfaceHeight = surfaceHeights[localX];
                    var depositColor = hasDepositsInChunk ? GetDepositColorAt(worldX, worldY) : null;
                    var isGeneratedSolid = IsGeneratedSolid(worldX, worldY, surfaceHeight);
                    var isSolid = (isGeneratedSolid && !IsDestroyed(worldX, worldY)) || depositColor is not null;
                    pixels[(localY * chunkWidth) + localX] = isSolid
                        ? (depositColor ?? GetTerrainColor(worldX, worldY, surfaceHeight))
                        : Color.Transparent;
                }
            }

            return new ChunkPixelBuffer(new Point(chunkX, chunkY), chunkWidth, chunkHeight, pixels);
        }

        private void BurnParticleIntoTerrain(TerrainParticle particle)
        {
            var depositPoint = FindDepositPoint(particle.Position);
            if (depositPoint is null)
            {
                return;
            }

            var depositRadius = Math.Clamp(particle.Size * 0.3f, 1f, 2f);
            var deposit = new TerrainDeposit(depositPoint.Value.ToVector2(), depositRadius, particle.Color);
            _terrainDeposits.Add(deposit);
            AddDepositToPixelMap(deposit);
            InvalidateChunks(deposit.Bounds);
            TerrainVisualsChanged = true;
        }

        private Point? FindDepositPoint(Vector2 position)
        {
            var worldX = (int)MathF.Round(position.X);
            var worldY = (int)MathF.Round(position.Y);
            if (worldX < 0 || worldX >= MapWidth)
            {
                return null;
            }

            var startY = Math.Clamp(worldY, 0, MapHeight - 1);
            for (var offset = 0; offset <= 12; offset++)
            {
                var candidateY = startY + offset;
                if (candidateY >= MapHeight)
                {
                    break;
                }

                if (IsSolid(worldX, candidateY))
                {
                    return new Point(worldX, Math.Max(0, candidateY - 1));
                }
            }

            return startY >= 0 && startY < MapHeight ? new Point(worldX, startY) : null;
        }

        private Color? GetDepositColorAt(int worldX, int worldY)
        {
            EnsureDepositPixelMap();
            return _depositPixelMap.TryGetValue(GetDepositKey(worldX, worldY), out var color) ? color : null;
        }

        private void RemoveDeposits(Rectangle bounds)
        {
            for (var index = _terrainDeposits.Count - 1; index >= 0; index--)
            {
                if (_terrainDeposits[index].Bounds.Intersects(bounds))
                {
                    _terrainDeposits.RemoveAt(index);
                }
            }

            for (var worldY = bounds.Top; worldY < bounds.Bottom; worldY++)
            {
                for (var worldX = bounds.Left; worldX < bounds.Right; worldX++)
                {
                    _depositPixelMap.Remove(GetDepositKey(worldX, worldY));
                }
            }

            RebuildDepositChunkKeys();
            _trackedDepositCount = _terrainDeposits.Count;
        }

        private void EnsureDepositPixelMap()
        {
            if (_trackedDepositCount == _terrainDeposits.Count)
            {
                return;
            }

            _depositPixelMap.Clear();
            _depositChunkKeys.Clear();
            for (var index = 0; index < _terrainDeposits.Count; index++)
            {
                AddDepositToPixelMap(_terrainDeposits[index]);
            }

            _trackedDepositCount = _terrainDeposits.Count;
        }

        private void AddDepositToPixelMap(TerrainDeposit deposit)
        {
            var startX = Math.Max(0, deposit.Bounds.Left);
            var endX = Math.Min(MapWidth, deposit.Bounds.Right);
            var startY = Math.Max(0, deposit.Bounds.Top);
            var endY = Math.Min(MapHeight, deposit.Bounds.Bottom);

            AddDepositChunkKeys(deposit.Bounds);

            for (var worldY = startY; worldY < endY; worldY++)
            {
                for (var worldX = startX; worldX < endX; worldX++)
                {
                    if (deposit.Contains(new Vector2(worldX, worldY)))
                    {
                        _depositPixelMap[GetDepositKey(worldX, worldY)] = deposit.Color;
                    }
                }
            }

            _trackedDepositCount = _terrainDeposits.Count;
        }

        private bool ChunkHasDeposits(int chunkX, int chunkY)
        {
            EnsureDepositPixelMap();
            return _depositChunkKeys.Contains(GetChunkKey(chunkX, chunkY));
        }

        private void AddDepositChunkKeys(Rectangle bounds)
        {
            var startChunkX = Math.Max(0, bounds.Left / _chunkSize);
            var endChunkX = Math.Max(0, (Math.Max(bounds.Left, bounds.Right - 1)) / _chunkSize);
            var startChunkY = Math.Max(0, bounds.Top / _chunkSize);
            var endChunkY = Math.Max(0, (Math.Max(bounds.Top, bounds.Bottom - 1)) / _chunkSize);

            for (var chunkY = startChunkY; chunkY <= endChunkY; chunkY++)
            {
                for (var chunkX = startChunkX; chunkX <= endChunkX; chunkX++)
                {
                    _depositChunkKeys.Add(GetChunkKey(chunkX, chunkY));
                }
            }
        }

        private void RebuildDepositChunkKeys()
        {
            _depositChunkKeys.Clear();
            for (var index = 0; index < _terrainDeposits.Count; index++)
            {
                AddDepositChunkKeys(_terrainDeposits[index].Bounds);
            }
        }

        private int GetDepositKey(int worldX, int worldY)
        {
            return (worldY * MapWidth) + worldX;
        }

        private static int GetChunkKey(int chunkX, int chunkY)
        {
            return HashCode.Combine(chunkX, chunkY);
        }

        private Texture2D CreateChunkTexture(ChunkPixelBuffer chunkBuffer)
        {
            var texture = new Texture2D(_graphicsDevice!, chunkBuffer.Width, chunkBuffer.Height, false, SurfaceFormat.Color);
            texture.SetData(chunkBuffer.Pixels);
            return texture;
        }

        private void StartChunkBuildWorkers()
        {
            StopChunkBuildWorkers();

            if (TotalChunkCount <= 0)
            {
                return;
            }

            var workerCount = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);
            _chunkBuildCancellationTokenSource = new CancellationTokenSource();
            _chunkBuildWorkers = new Task[workerCount];

            for (var workerIndex = 0; workerIndex < workerCount; workerIndex++)
            {
                _chunkBuildWorkers[workerIndex] = Task.Run(() => BuildChunkBuffers(_chunkBuildCancellationTokenSource.Token));
            }
        }

        private void StopChunkBuildWorkers()
        {
            if (_chunkBuildCancellationTokenSource is not null)
            {
                _chunkBuildCancellationTokenSource.Cancel();

                try
                {
                    Task.WaitAll(_chunkBuildWorkers);
                }
                catch (AggregateException)
                {
                }

                _chunkBuildCancellationTokenSource.Dispose();
                _chunkBuildCancellationTokenSource = null;
            }

            _chunkBuildWorkers = [];
        }

        private void BuildChunkBuffers(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _pendingChunkBuilds.TryDequeue(out var chunkKey))
            {
                var chunkBuffer = BuildChunkPixelBuffer(chunkKey.X, chunkKey.Y);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _completedChunkBuilds.Enqueue(chunkBuffer);
            }
        }

        private void ClearCompletedChunkBuilds()
        {
            while (_completedChunkBuilds.TryDequeue(out _))
            {
            }
        }

        private void ClearPendingChunkBuilds()
        {
            while (_pendingChunkBuilds.TryDequeue(out _))
            {
            }
        }

        private bool IsGeneratedSolid(int worldX, int worldY)
        {
            var surfaceHeight = GetSurfaceHeight(worldX);
            return IsGeneratedSolid(worldX, worldY, surfaceHeight);
        }

        private bool IsGeneratedSolid(int worldX, int worldY, int surfaceHeight)
        {
            if (worldY < surfaceHeight)
            {
                return false;
            }

            return true;
        }

        private void EmitBlastParticles(TerrainBlast blast)
        {
            if (_particleRandom is null)
            {
                return;
            }

            var particleCount = Math.Max(1, (int)MathF.Round((blast.Radius / ReferenceBlastRadius) * ReferenceBlastParticleCount));
            for (var index = 0; index < particleCount; index++)
            {
                var angle = (float)(_particleRandom.NextDouble() * MathF.Tau);
                var distance = blast.Radius * 0.12f * MathF.Sqrt((float)_particleRandom.NextDouble());
                var spawnOffset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                var spawnPosition = blast.Center + spawnOffset;
                var speed = MathHelper.Lerp(80f, 240f, (float)_particleRandom.NextDouble());
                var drift = new Vector2(MathHelper.Lerp(-20f, 20f, (float)_particleRandom.NextDouble()), MathHelper.Lerp(-40f, 10f, (float)_particleRandom.NextDouble()));
                var velocity = (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed) + drift;
                _terrainParticles.Add(new TerrainParticle(
                    spawnPosition,
                    velocity,
                    GetParticleColor(_particleRandom),
                    MathHelper.Lerp(1f, ParticleLifetimeSeconds, (float)_particleRandom.NextDouble()),
                    _particleRandom.Next(0, 2),
                    MathHelper.Lerp(ParticleMinSize, ParticleMaxSize, (float)_particleRandom.NextDouble())));
            }

            _explosionBlooms.Add(new ExplosionBloom(
                blast.Center,
                blast.Radius * 3.25f,
                ExplosionBloomLifetimeSeconds,
                new Color(255, 240, 210)));
        }

        private void DrawParticles(SpriteBatch spriteBatch, Rectangle worldView, Vector2 screenPosition)
        {
            if (_particleTexture is null || _terrainParticles.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _terrainParticles.Count; index++)
            {
                var particle = _terrainParticles[index];
                var point = new Point((int)MathF.Round(particle.Position.X), (int)MathF.Round(particle.Position.Y));
                if (!worldView.Contains(point))
                {
                    continue;
                }

                var normalizedAge = Math.Clamp(particle.Age / particle.Lifetime, 0f, 1f);
                var alpha = 1f - normalizedAge;
                var drawColor = WithPremultipliedAlpha(particle.Color, alpha);
                var drawPosition = screenPosition + particle.Position - worldView.Location.ToVector2();
                spriteBatch.Draw(
                    _particleTexture,
                    new Rectangle(
                        (int)MathF.Round(drawPosition.X - (particle.Size * 0.5f)),
                        (int)MathF.Round(drawPosition.Y - (particle.Size * 0.5f)),
                        Math.Max(1, (int)MathF.Round(particle.Size)),
                        Math.Max(1, (int)MathF.Round(particle.Size))),
                    drawColor);
            }
        }

        private void UpdateBlooms(float elapsedSeconds)
        {
            for (var index = _explosionBlooms.Count - 1; index >= 0; index--)
            {
                var bloom = _explosionBlooms[index];
                bloom.Age += elapsedSeconds;
                if (bloom.Age >= bloom.Lifetime)
                {
                    _explosionBlooms.RemoveAt(index);
                }
            }
        }

        private void DrawBlooms(SpriteBatch spriteBatch, Rectangle worldView, Vector2 screenPosition)
        {
            if (_bloomTexture is null || _explosionBlooms.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _explosionBlooms.Count; index++)
            {
                var bloom = _explosionBlooms[index];
                var bounds = new Rectangle(
                    (int)MathF.Round(bloom.Position.X - bloom.Radius),
                    (int)MathF.Round(bloom.Position.Y - bloom.Radius),
                    (int)MathF.Round(bloom.Radius * 2f),
                    (int)MathF.Round(bloom.Radius * 2f));

                if (!worldView.Intersects(bounds))
                {
                    continue;
                }

                var normalizedAge = Math.Clamp(bloom.Age / bloom.Lifetime, 0f, 1f);
                var intensity = 1f - normalizedAge;
                var drawColor = WithPremultipliedAlpha(bloom.Color, (140f / 255f) * intensity * intensity);
                var drawPosition = screenPosition + bloom.Position - worldView.Location.ToVector2();
                spriteBatch.Draw(
                    _bloomTexture,
                    drawPosition,
                    null,
                    drawColor,
                    0f,
                    new Vector2(_bloomTexture.Width * 0.5f, _bloomTexture.Height * 0.5f),
                    (bloom.Radius * 2f) / _bloomTexture.Width,
                    SpriteEffects.None,
                    0f);
            }
        }

        private static Texture2D CreateRadialTexture(GraphicsDevice graphicsDevice, int size, float edgeSoftness)
        {
            var textureSize = Math.Max(2, size);
            var pixels = new Color[textureSize * textureSize];
            var center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            var radius = textureSize * 0.5f;

            for (var y = 0; y < textureSize; y++)
            {
                for (var x = 0; x < textureSize; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    var normalized = Math.Clamp(distance / radius, 0f, 1f);
                    var falloff = Math.Clamp(normalized / Math.Max(0.01f, edgeSoftness), 0f, 1f);
                    var alpha = normalized >= 1f ? 0f : 1f - Smooth(falloff);
                    var alphaByte = (byte)MathF.Round(Math.Clamp(alpha, 0f, 1f) * 255f);
                    pixels[(y * textureSize) + x] = new Color(alphaByte, alphaByte, alphaByte, alphaByte);
                }
            }

            var texture = new Texture2D(graphicsDevice, textureSize, textureSize);
            texture.SetData(pixels);
            return texture;
        }

        private static Color WithPremultipliedAlpha(Color color, float alpha)
        {
            var clampedAlpha = Math.Clamp(alpha, 0f, 1f);
            return new Color(
                (byte)MathF.Round(color.R * clampedAlpha),
                (byte)MathF.Round(color.G * clampedAlpha),
                (byte)MathF.Round(color.B * clampedAlpha),
                (byte)MathF.Round(255f * clampedAlpha));
        }

        private static Color GetParticleColor(Random random)
        {
            return random.Next(3) switch
            {
                0 => new Color(121, 87, 52),
                1 => new Color(102, 72, 46),
                _ => new Color(84, 58, 38)
            };
        }

        private bool IsDestroyed(int worldX, int worldY)
        {
            var point = new Vector2(worldX, worldY);
            for (var index = _terrainBlasts.Count - 1; index >= 0; index--)
            {
                if (_terrainBlasts[index].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private Color GetTerrainColor(int worldX, int worldY)
        {
            var surfaceHeight = GetSurfaceHeight(worldX);
            return GetTerrainColor(worldX, worldY, surfaceHeight);
        }

        private Color GetTerrainColor(int worldX, int worldY, int surfaceHeight)
        {
            var depthFromSurface = worldY - surfaceHeight;
            if (depthFromSurface <= 3)
            {
                return new Color(124, 175, 86);
            }

            if (depthFromSurface <= 12)
            {
                return new Color(121, 87, 52);
            }

            var rockNoise = FractalNoise2D(worldX * 0.065f, worldY * 0.065f, 2, 2f, 0.5f);
            return rockNoise > 0.55f
                ? new Color(84, 58, 38)
                : new Color(102, 72, 46);
        }

        private int GetSurfaceHeight(int worldX)
        {
            var center = GetRawSurfaceHeight(worldX);
            var nearLeft = GetRawSurfaceHeight(worldX - 28f);
            var nearRight = GetRawSurfaceHeight(worldX + 28f);
            var midLeft = GetRawSurfaceHeight(worldX - 64f);
            var midRight = GetRawSurfaceHeight(worldX + 64f);
            var farLeft = GetRawSurfaceHeight(worldX - 128f);
            var farRight = GetRawSurfaceHeight(worldX + 128f);
            var smoothedSurface = (center * 0.14f)
                + ((nearLeft + nearRight) * 0.23f)
                + ((midLeft + midRight) * 0.09f)
                + ((farLeft + farRight) * 0.11f);
            return (int)Math.Clamp(smoothedSurface, MapHeight * 0.02f, MapHeight * 0.97f);
        }

        private float GetRawSurfaceHeight(float worldX)
        {
            var baseHeight = MapHeight * 0.57f;
            var edgeAction = GetEdgeActionFactor(worldX);
            var terrainVariance = MathHelper.Lerp(0.72f, 1.35f, edgeAction);
            var continentNoise = FractalNoise1D(worldX * 0.00042f, 4, 2f, 0.5f);
            var ridgeNoise = FractalNoise1D(worldX * 0.00095f, 3, 2f, 0.5f);
            var sharpNoise = RidgedNoise1D(worldX * 0.0018f, 2, 2f, 0.45f);
            var mountainBandRegion = ValueNoise1D((worldX + 701f) * 0.00016f);
            var mountainBandMask = Smooth(Math.Clamp((mountainBandRegion - 0.36f) / 0.44f, 0f, 1f));
            var mountainBandShape = FractalNoise1D((worldX - 211f) * 0.00031f, 3, 2f, 0.5f);
            var surface = baseHeight;
            surface += (continentNoise - 0.5f) * (MapHeight * 0.5f) * terrainVariance;
            surface += (ridgeNoise - 0.5f) * (MapHeight * 0.16f) * terrainVariance;
            surface += mountainBandMask * ((mountainBandShape - 0.44f) * (MapHeight * MountainBandHeightPercent)) * MathHelper.Lerp(0.8f, 1.1f, edgeAction);
            surface -= sharpNoise * (MapHeight * 0.012f) * MathHelper.Lerp(0.35f, 0.7f, edgeAction);
            return surface;
        }

        private float GetEdgeActionFactor(float worldX)
        {
            if (MapWidth <= 1)
            {
                return 1f;
            }

            var normalizedX = Math.Clamp(worldX / (MapWidth - 1f), 0f, 1f);
            var centerDistance = MathF.Abs((normalizedX - 0.5f) * 2f);
            return Smooth(centerDistance);
        }


        private float RidgedNoise1D(float x, int octaves, float lacunarity, float gain)
        {
            return 1f - MathF.Abs((FractalNoise1D(x, octaves, lacunarity, gain) * 2f) - 1f);
        }

        private float FractalNoise1D(float x, int octaves, float lacunarity, float gain)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var value = 0f;
            var amplitudeSum = 0f;

            for (var octave = 0; octave < octaves; octave++)
            {
                value += ValueNoise1D(x * frequency) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return amplitudeSum <= 0f ? 0f : value / amplitudeSum;
        }

        private float FractalNoise2D(float x, float y, int octaves, float lacunarity, float gain)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var value = 0f;
            var amplitudeSum = 0f;

            for (var octave = 0; octave < octaves; octave++)
            {
                value += ValueNoise2D(x * frequency, y * frequency) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return amplitudeSum <= 0f ? 0f : value / amplitudeSum;
        }

        private float ValueNoise1D(float x)
        {
            var x0 = (int)MathF.Floor(x);
            var x1 = x0 + 1;
            var t = Smooth(x - x0);
            return MathHelper.Lerp(Random01(x0, 0), Random01(x1, 0), t);
        }

        private float ValueNoise2D(float x, float y)
        {
            var x0 = (int)MathF.Floor(x);
            var y0 = (int)MathF.Floor(y);
            var x1 = x0 + 1;
            var y1 = y0 + 1;
            var tx = Smooth(x - x0);
            var ty = Smooth(y - y0);

            var topLeft = Random01(x0, y0);
            var topRight = Random01(x1, y0);
            var bottomLeft = Random01(x0, y1);
            var bottomRight = Random01(x1, y1);
            var top = MathHelper.Lerp(topLeft, topRight, tx);
            var bottom = MathHelper.Lerp(bottomLeft, bottomRight, tx);
            return MathHelper.Lerp(top, bottom, ty);
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - (2f * value));
        }

        private void InitializeChunkQueue()
        {
            ClearPendingChunkBuilds();
            ClearCompletedChunkBuilds();

            var chunkColumns = (int)Math.Ceiling(MapWidth / (float)_chunkSize);
            var chunkRows = (int)Math.Ceiling(MapHeight / (float)_chunkSize);
            TotalChunkCount = chunkColumns * chunkRows;

            for (var chunkY = 0; chunkY < chunkRows; chunkY++)
            {
                for (var chunkX = 0; chunkX < chunkColumns; chunkX++)
                {
                    _pendingChunkBuilds.Enqueue(new Point(chunkX, chunkY));
                }
            }
        }

        private void InvalidateChunks(Rectangle bounds)
        {
            var clippedBounds = Rectangle.Intersect(bounds, GetMapBounds());
            if (clippedBounds.Width <= 0 || clippedBounds.Height <= 0)
            {
                return;
            }

            var startChunkX = clippedBounds.Left / _chunkSize;
            var endChunkX = (clippedBounds.Right - 1) / _chunkSize;
            var startChunkY = clippedBounds.Top / _chunkSize;
            var endChunkY = (clippedBounds.Bottom - 1) / _chunkSize;

            for (var chunkY = startChunkY; chunkY <= endChunkY; chunkY++)
            {
                for (var chunkX = startChunkX; chunkX <= endChunkX; chunkX++)
                {
                    var key = new Point(chunkX, chunkY);
                    if (_chunkTextures.Remove(key, out var chunkTexture))
                    {
                        chunkTexture.Dispose();
                    }
                }
            }
        }

        private float Random01(int x, int y)
        {
            unchecked
            {
                var hash = _seed;
                hash ^= x * 374761393;
                hash = (hash << 13) ^ hash;
                hash += y * 668265263;
                hash = (hash * 1274126177) ^ (hash >> 16);
                return (hash & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        private readonly record struct ChunkPixelBuffer(Point Key, int Width, int Height, Color[] Pixels);

        private sealed class TerrainParticle(Vector2 position, Vector2 velocity, Color color, float lifetime, int bouncesRemaining, float size)
        {
            internal Vector2 Position { get; set; } = position;

            internal Vector2 Velocity { get; set; } = velocity;

            internal Color Color { get; } = color;

            internal float Lifetime { get; } = lifetime;

            internal float Age { get; set; }

            internal int BouncesRemaining { get; set; } = bouncesRemaining;

            internal float Size { get; } = size;
        }

        private sealed class ExplosionBloom(Vector2 position, float radius, float lifetime, Color color)
        {
            internal Vector2 Position { get; } = position;

            internal float Radius { get; } = radius;

            internal float Lifetime { get; } = lifetime;

            internal Color Color { get; } = color;

            internal float Age { get; set; }
        }

        private readonly record struct TerrainDeposit(Vector2 Center, float Radius, Color Color)
        {
            internal Rectangle Bounds { get; } = new Rectangle(
                (int)MathF.Floor(Center.X - Radius),
                (int)MathF.Floor(Center.Y - Radius),
                Math.Max(1, (int)MathF.Ceiling(Radius * 2f)),
                Math.Max(1, (int)MathF.Ceiling(Radius * 2f)));

            internal bool Contains(Vector2 point)
            {
                return Vector2.DistanceSquared(Center, point) <= Radius * Radius;
            }
        }

        private readonly record struct TerrainBlast(Vector2 Center, float Radius)
        {
            internal Rectangle Bounds { get; } = new Rectangle(
                (int)MathF.Floor(Center.X - Radius),
                (int)MathF.Floor(Center.Y - Radius),
                Math.Max(1, (int)MathF.Ceiling(Radius * 2f)),
                Math.Max(1, (int)MathF.Ceiling(Radius * 2f)));

            internal bool Contains(Vector2 point)
            {
                return Vector2.DistanceSquared(Center, point) <= Radius * Radius;
            }
        }
    }
}
