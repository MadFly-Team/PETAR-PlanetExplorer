using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using PETAR_PlanetExplorer.Modules.Maps;
using Microsoft.Xna.Framework;
using Microsoft.VSDiagnostics;

namespace PETAR_PlanetExplorer.Performance
{
    [CPUUsageDiagnoser]
    public class HeightMapFlyoverRendererBuildBenchmark
    {
        private const int MaxVisibleChunks = 324;
        private readonly MethodInfo _buildVisibleChunksMethod = typeof(HeightMapFlyoverRenderer).GetMethod("BuildVisibleChunks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _worldWidthField = typeof(HeightMapFlyoverRenderer).GetField("_worldWidth", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _worldHeightField = typeof(HeightMapFlyoverRenderer).GetField("_worldHeight", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _chunksField = typeof(HeightMapFlyoverRenderer).GetField("_chunks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _waterChunksField = typeof(HeightMapFlyoverRenderer).GetField("_waterChunks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private ProceduralWorldMap _worldMap = null!;
        private HeightMapFlyoverRenderer _renderer = null!;
        private Vector2 _cameraPosition;
        private Vector2 _forward;
        private Vector2 _right;
        private float _maxDistance;
        private float _viewWidth;
        private float _time;
        [GlobalSetup]
        public void Setup()
        {
            _worldMap = new ProceduralWorldMap(4096, 4096, 74291);
            _renderer = CreateHeadlessRenderer(_worldMap.Width, _worldMap.Height);
            _cameraPosition = new Vector2(_worldMap.Width * 0.35f, _worldMap.Height * 0.28f);
            var heading = MathF.PI * 0.5f;
            var altitude = 0.22f;
            var altitudeRatio = MathHelper.Clamp((altitude - 0.05f) / 63.95f, 0f, 1f);
            _forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
            _right = new Vector2(-_forward.Y, _forward.X);
            _maxDistance = MathHelper.Lerp(180f, 360f, altitudeRatio);
            _viewWidth = MathHelper.Lerp(144f, 320f, altitudeRatio);
            _time = 120f;
        }

        [Benchmark]
        public int BuildVisibleChunks()
        {
            return (int)_buildVisibleChunksMethod.Invoke(_renderer, new object[] { _worldMap, _cameraPosition, _forward, _right, _maxDistance, _viewWidth, _time })!;
        }

        private HeightMapFlyoverRenderer CreateHeadlessRenderer(int worldWidth, int worldHeight)
        {
            var renderer = (HeightMapFlyoverRenderer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HeightMapFlyoverRenderer));
            _worldWidthField.SetValue(renderer, worldWidth);
            _worldHeightField.SetValue(renderer, worldHeight);
            _chunksField.SetValue(renderer, CreateChunkArray());
            _waterChunksField.SetValue(renderer, CreateChunkArray());
            return renderer;
        }

        private static Array CreateChunkArray()
        {
            var voxelChunkType = typeof(HeightMapFlyoverRenderer).GetNestedType("VoxelChunk", BindingFlags.NonPublic)!;
            var chunks = Array.CreateInstance(voxelChunkType, MaxVisibleChunks);
            for (var index = 0; index < MaxVisibleChunks; index++)
            {
                chunks.SetValue(Activator.CreateInstance(voxelChunkType, true), index);
            }

            return chunks;
        }
    }

    [CPUUsageDiagnoser]
    public class HeightMapFlyoverRendererWaterCacheMissBenchmark
    {
        private const int MaxVisibleChunks = 324;
        private readonly MethodInfo _buildVisibleChunksMethod = typeof(HeightMapFlyoverRenderer).GetMethod("BuildVisibleChunks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _worldWidthField = typeof(HeightMapFlyoverRenderer).GetField("_worldWidth", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _worldHeightField = typeof(HeightMapFlyoverRenderer).GetField("_worldHeight", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _chunksField = typeof(HeightMapFlyoverRenderer).GetField("_chunks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _waterChunksField = typeof(HeightMapFlyoverRenderer).GetField("_waterChunks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private ProceduralWorldMap _worldMap = null!;
        private HeightMapFlyoverRenderer _renderer = null!;
        private Vector2 _cameraPosition;
        private Vector2 _forward;
        private Vector2 _right;
        private float _maxDistance;
        private float _viewWidth;
        private float _cachedTime;
        private float _cacheMissTime;

        [GlobalSetup]
        public void Setup()
        {
            _worldMap = new ProceduralWorldMap(4096, 4096, 74291);
            _renderer = CreateHeadlessRenderer(_worldMap.Width, _worldMap.Height);
            _cameraPosition = new Vector2(_worldMap.Width * 0.35f, _worldMap.Height * 0.28f);
            var heading = MathF.PI * 0.5f;
            var altitude = 0.22f;
            var altitudeRatio = MathHelper.Clamp((altitude - 0.05f) / 63.95f, 0f, 1f);
            _forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
            _right = new Vector2(-_forward.Y, _forward.X);
            _maxDistance = MathHelper.Lerp(180f, 360f, altitudeRatio);
            _viewWidth = MathHelper.Lerp(144f, 320f, altitudeRatio);
            _cachedTime = 120f;
            _cacheMissTime = 120.06f;
        }

        [IterationSetup]
        public void PrimePreviousWaterBucket()
        {
            InvokeBuildVisibleChunks(_cachedTime);
        }

        [Benchmark]
        public int WaterCacheMissFrame()
        {
            return InvokeBuildVisibleChunks(_cacheMissTime);
        }

        private int InvokeBuildVisibleChunks(float time)
        {
            return (int)_buildVisibleChunksMethod.Invoke(_renderer, new object[] { _worldMap, _cameraPosition, _forward, _right, _maxDistance, _viewWidth, time })!;
        }

        private HeightMapFlyoverRenderer CreateHeadlessRenderer(int worldWidth, int worldHeight)
        {
            var renderer = (HeightMapFlyoverRenderer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HeightMapFlyoverRenderer));
            _worldWidthField.SetValue(renderer, worldWidth);
            _worldHeightField.SetValue(renderer, worldHeight);
            _chunksField.SetValue(renderer, CreateChunkArray());
            _waterChunksField.SetValue(renderer, CreateChunkArray());
            return renderer;
        }

        private static Array CreateChunkArray()
        {
            var voxelChunkType = typeof(HeightMapFlyoverRenderer).GetNestedType("VoxelChunk", BindingFlags.NonPublic)!;
            var chunks = Array.CreateInstance(voxelChunkType, MaxVisibleChunks);
            for (var index = 0; index < MaxVisibleChunks; index++)
            {
                chunks.SetValue(Activator.CreateInstance(voxelChunkType, true), index);
            }

            return chunks;
        }
    }

    [CPUUsageDiagnoser]
    public class HeightMapFlyoverRendererHighAltitudeWaterCacheMissBenchmark
    {
        private const int MaxVisibleChunks = 324;
        private readonly MethodInfo _buildVisibleChunksMethod = typeof(HeightMapFlyoverRenderer).GetMethod("BuildVisibleChunks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _worldWidthField = typeof(HeightMapFlyoverRenderer).GetField("_worldWidth", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _worldHeightField = typeof(HeightMapFlyoverRenderer).GetField("_worldHeight", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _chunksField = typeof(HeightMapFlyoverRenderer).GetField("_chunks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly FieldInfo _waterChunksField = typeof(HeightMapFlyoverRenderer).GetField("_waterChunks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private ProceduralWorldMap _worldMap = null!;
        private HeightMapFlyoverRenderer _renderer = null!;
        private Vector2 _cameraPosition;
        private Vector2 _forward;
        private Vector2 _right;
        private float _maxDistance;
        private float _viewWidth;
        private float _cachedTime;
        private float _cacheMissTime;

        [GlobalSetup]
        public void Setup()
        {
            _worldMap = new ProceduralWorldMap(4096, 4096, 74291);
            _renderer = CreateHeadlessRenderer(_worldMap.Width, _worldMap.Height);
            _cameraPosition = new Vector2(_worldMap.Width * 0.35f, _worldMap.Height * 0.28f);
            var heading = MathF.PI * 0.5f;
            var altitude = 400f;
            var altitudeRatio = MathHelper.Clamp((altitude - 0.05f) / 511.95f, 0f, 1f);
            _forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
            _right = new Vector2(-_forward.Y, _forward.X);
            _maxDistance = MathHelper.Lerp(180f, 360f, altitudeRatio);
            _viewWidth = MathHelper.Lerp(144f, 320f, altitudeRatio);
            _cachedTime = 120f;
            _cacheMissTime = 120.06f;
        }

        [IterationSetup]
        public void PrimePreviousWaterBucket()
        {
            InvokeBuildVisibleChunks(_cachedTime);
        }

        [Benchmark]
        public int HighAltitudeWaterCacheMissFrame()
        {
            return InvokeBuildVisibleChunks(_cacheMissTime);
        }

        private int InvokeBuildVisibleChunks(float time)
        {
            return (int)_buildVisibleChunksMethod.Invoke(_renderer, new object[] { _worldMap, _cameraPosition, _forward, _right, _maxDistance, _viewWidth, time })!;
        }

        private HeightMapFlyoverRenderer CreateHeadlessRenderer(int worldWidth, int worldHeight)
        {
            var renderer = (HeightMapFlyoverRenderer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HeightMapFlyoverRenderer));
            _worldWidthField.SetValue(renderer, worldWidth);
            _worldHeightField.SetValue(renderer, worldHeight);
            _chunksField.SetValue(renderer, CreateChunkArray());
            _waterChunksField.SetValue(renderer, CreateChunkArray());
            return renderer;
        }

        private static Array CreateChunkArray()
        {
            var voxelChunkType = typeof(HeightMapFlyoverRenderer).GetNestedType("VoxelChunk", BindingFlags.NonPublic)!;
            var chunks = Array.CreateInstance(voxelChunkType, MaxVisibleChunks);
            for (var index = 0; index < MaxVisibleChunks; index++)
            {
                chunks.SetValue(Activator.CreateInstance(voxelChunkType, true), index);
            }

            return chunks;
        }
    }
}