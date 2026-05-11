using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PETAR_PlanetExplorer.Modules.Maps;
using PETAR_PlanetExplorer.Modules.Voxels;

namespace PETAR_PlanetExplorer.Performance
{
    [CPUUsageDiagnoser]
    public class VoxelRendererF7Benchmarks
    {
        private readonly MethodInfo _buildVerticesMethod = typeof(VoxelRenderer)
            .GetNestedType("VoxelMeshBuilder", BindingFlags.NonPublic)!
            .GetMethod("BuildVertices", BindingFlags.Public | BindingFlags.Static)!;

        private readonly MethodInfo _warmVisibleMeshCachesMethod = typeof(VoxelRenderer)
            .GetMethod("WarmVisibleMeshCaches", BindingFlags.NonPublic | BindingFlags.Static)!;

        private VoxelWorld _voxelWorld = null!;
        private VoxelChunk _surfaceChunk = null!;
        private VoxelChunk _undergroundChunk = null!;
        private Vector3 _aboveSurfaceCamera;
        private Vector3 _belowSurfaceCamera;
        private float _verticalRenderOrigin;

        [GlobalSetup]
        public void Setup()
        {
            var worldMap = new ProceduralWorldMap(256, 256, 74291);
            _voxelWorld = VoxelWorldBuilder.CreateFromHeightMap(worldMap);
            _verticalRenderOrigin = VoxelWorldBuilder.GetRenderVerticalOrigin(_voxelWorld.HeightLimit);

            var focus = FindFocusColumn(_voxelWorld, worldMap.Width, worldMap.Height);
            var centerChunkX = focus.X / VoxelConstants.ChunkSize;
            var centerChunkY = focus.Y / VoxelConstants.ChunkSize;
            var surfaceChunkZ = focus.TopZ / VoxelConstants.ChunkSize;
            var undergroundChunkZ = focus.TopZ >= VoxelConstants.ChunkSize ? surfaceChunkZ - 1 : 0;

            _surfaceChunk = _voxelWorld.GetOrCreateChunk(new VoxelChunkKey(centerChunkX, centerChunkY, surfaceChunkZ));
            _undergroundChunk = _voxelWorld.GetOrCreateChunk(new VoxelChunkKey(centerChunkX, centerChunkY, undergroundChunkZ));

            _aboveSurfaceCamera = new Vector3(focus.X, (focus.TopZ + 6) * 2f, focus.Y);
            _belowSurfaceCamera = new Vector3(focus.X, Math.Max(4, focus.TopZ - 18) * 2f, focus.Y);
        }

        [IterationSetup]
        public void ResetChunkCaches()
        {
            foreach (var chunk in _voxelWorld.Chunks.Values)
            {
                chunk.MarkDirty();
            }
        }

        [Benchmark]
        public int BuildSurfaceChunkMesh()
        {
            return BuildChunkMesh(_surfaceChunk);
        }

        [Benchmark]
        public int BuildUndergroundChunkMesh()
        {
            return BuildChunkMesh(_undergroundChunk);
        }

        [Benchmark]
        public int WarmVisibleChunkMeshesAboveSurface()
        {
            return (int)_warmVisibleMeshCachesMethod.Invoke(null, new object[] { _voxelWorld, _aboveSurfaceCamera })!;
        }

        [Benchmark]
        public int WarmVisibleChunkMeshesBelowSurface()
        {
            return (int)_warmVisibleMeshCachesMethod.Invoke(null, new object[] { _voxelWorld, _belowSurfaceCamera })!;
        }

        private int BuildChunkMesh(VoxelChunk chunk)
        {
            var vertices = (VertexPositionColor[])_buildVerticesMethod.Invoke(null, new object[] { _voxelWorld, chunk, 1, _verticalRenderOrigin })!;
            return vertices.Length / 3;
        }

        private static (int X, int Y, int TopZ) FindFocusColumn(VoxelWorld voxelWorld, int width, int height)
        {
            var bestX = width / 2;
            var bestY = height / 2;
            var bestTopZ = -1;

            for (var worldY = 0; worldY < height; worldY += 4)
            {
                for (var worldX = 0; worldX < width; worldX += 4)
                {
                    if (!voxelWorld.TryGetTopSolidVoxel(worldX, worldY, out var topZ))
                    {
                        continue;
                    }

                    if (topZ > bestTopZ)
                    {
                        bestX = worldX;
                        bestY = worldY;
                        bestTopZ = topZ;
                    }
                }
            }

            if (bestTopZ < 0)
            {
                throw new InvalidOperationException("Unable to locate a populated voxel column for benchmarking.");
            }

            return (bestX, bestY, bestTopZ);
        }
    }
}
