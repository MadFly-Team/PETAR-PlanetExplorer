using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PETAR_PlanetExplorer.Modules.Voxels
{
    public sealed class VoxelRenderer : IDisposable
    {
        private const float HorizontalCubeSize = 1f;
        private const float VerticalCubeSize = 2f;
        private const float AmbientLight = 0.42f;
        private const float DiffuseLight = 0.58f;
        private static readonly int[] WarmupLodSteps = [1, 2, 4];
        private static readonly Vector3 SunLightDirection = Vector3.Normalize(new Vector3(-0.52f, 0.74f, -0.42f));

        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;

        public VoxelRenderer(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                TextureEnabled = false
            };
        }

        public void Render(VoxelWorld voxelWorld, Matrix view, Matrix projection)
        {
            Render(voxelWorld, view, projection, Vector3.Zero);
        }

        public void Render(VoxelWorld voxelWorld, Matrix view, Matrix projection, Vector3 cameraPosition)
        {
            if (voxelWorld == null)
            {
                return;
            }

            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            var frustum = new BoundingFrustum(view * projection);
            var maxViewDistance = GetMaxViewDistance(cameraPosition.Y);
            var maxViewDistanceSquared = maxViewDistance * maxViewDistance;

            foreach (var chunkEntry in voxelWorld.Chunks)
            {
                var chunk = chunkEntry.Value;
                var chunkBounds = GetChunkBounds(chunk);
                if (frustum.Contains(chunkBounds) == ContainmentType.Disjoint)
                {
                    continue;
                }

                var chunkCenter = chunkBounds.Min + ((chunkBounds.Max - chunkBounds.Min) * 0.5f);
                var distanceSquared = Vector3.DistanceSquared(cameraPosition, chunkCenter);
                if (distanceSquared > maxViewDistanceSquared)
                {
                    continue;
                }

                var lodStep = GetLodStep(distanceSquared);
                var vertices = GetOrBuildChunkMesh(voxelWorld, chunk, lodStep);

                if (vertices.Length == 0)
                {
                    continue;
                }

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
                }
            }
        }

        internal static int WarmVisibleMeshCaches(VoxelWorld voxelWorld, Vector3 cameraPosition)
        {
            if (voxelWorld == null)
            {
                return 0;
            }

            var warmedMeshes = 0;
            var maxViewDistance = GetMaxViewDistance(cameraPosition.Y) + (VoxelConstants.ChunkSize * HorizontalCubeSize * 2f);
            var maxViewDistanceSquared = maxViewDistance * maxViewDistance;
            foreach (var chunkEntry in voxelWorld.Chunks)
            {
                var chunk = chunkEntry.Value;
                var chunkBounds = GetChunkBounds(chunk);
                var chunkCenter = chunkBounds.Min + ((chunkBounds.Max - chunkBounds.Min) * 0.5f);
                var distanceSquared = Vector3.DistanceSquared(cameraPosition, chunkCenter);
                if (distanceSquared > maxViewDistanceSquared)
                {
                    continue;
                }

                for (var lodIndex = 0; lodIndex < WarmupLodSteps.Length; lodIndex++)
                {
                    var lodStep = WarmupLodSteps[lodIndex];
                    if (chunk.TryGetCachedMesh(lodStep, out _) && !chunk.IsDirty)
                    {
                        continue;
                    }

                    GetOrBuildChunkMesh(voxelWorld, chunk, lodStep);
                    warmedMeshes++;
                }
            }

            return warmedMeshes;
        }

        private static BoundingBox GetChunkBounds(VoxelChunk chunk)
        {
            var halfExtents = new Vector3(HorizontalCubeSize * 0.5f, VerticalCubeSize * 0.5f, HorizontalCubeSize * 0.5f);
            var min = ToRenderPosition(
                chunk.Key.X * VoxelConstants.ChunkSize,
                chunk.Key.Y * VoxelConstants.ChunkSize,
                chunk.Key.Z * VoxelConstants.ChunkSize) - halfExtents;
            var max = min + new Vector3(
                VoxelConstants.ChunkSize * HorizontalCubeSize,
                VoxelConstants.ChunkSize * VerticalCubeSize,
                VoxelConstants.ChunkSize * HorizontalCubeSize);
            return new BoundingBox(min, max);
        }

        private static float GetMaxViewDistance(float cameraHeight)
        {
            return MathHelper.Lerp(240f, 1200f, MathHelper.Clamp(cameraHeight / Math.Max(1f, VoxelConstants.WorldHeight), 0f, 1f));
        }

        private static int GetLodStep(float distanceSquared)
        {
            if (distanceSquared > 640f * 640f)
            {
                return 4;
            }

            if (distanceSquared > 280f * 280f)
            {
                return 2;
            }

            return 1;
        }

        private static VertexPositionColor[] GetOrBuildChunkMesh(VoxelWorld voxelWorld, VoxelChunk chunk, int lodStep)
        {
            if (!chunk.TryGetCachedMesh(lodStep, out var vertices) || chunk.IsDirty)
            {
                vertices = VoxelMeshBuilder.BuildVertices(voxelWorld, chunk, lodStep);
                chunk.UpdateCachedMesh(lodStep, vertices);
            }

            return vertices;
        }

        public void Dispose()
        {
            _effect.Dispose();
        }

        private static Vector3 ToRenderPosition(float voxelX, float voxelY, float voxelZ)
        {
            return new Vector3(voxelX * HorizontalCubeSize, voxelZ * VerticalCubeSize, voxelY * HorizontalCubeSize);
        }

        private static class VoxelMeshBuilder
        {
            private static readonly (int X, int Y, int Z, Face Face)[] NeighborFaces =
            [
                (0, 1, 0, Face.Front),
                (0, -1, 0, Face.Back),
                (-1, 0, 0, Face.Left),
                (1, 0, 0, Face.Right),
                (0, 0, 1, Face.Top),
                (0, 0, -1, Face.Bottom)
            ];

            public static VertexPositionColor[] BuildVertices(VoxelWorld voxelWorld, VoxelChunk chunk, int lodStep)
            {
                lodStep = Math.Max(1, lodStep);
                var builder = new List<VertexPositionColor>();
                var chunkOrigin = ToRenderPosition(
                    chunk.Key.X * VoxelConstants.ChunkSize,
                    chunk.Key.Y * VoxelConstants.ChunkSize,
                    chunk.Key.Z * VoxelConstants.ChunkSize);

                for (var z = 0; z < VoxelConstants.ChunkSize; z += lodStep)
                {
                    for (var y = 0; y < VoxelConstants.ChunkSize; y += lodStep)
                    {
                        for (var x = 0; x < VoxelConstants.ChunkSize; x += lodStep)
                        {
                            AppendCellVertices(builder, voxelWorld, chunk, chunkOrigin, x, y, z, lodStep);
                        }
                    }
                }

                return builder.ToArray();
            }

            private static void AppendCellVertices(List<VertexPositionColor> builder, VoxelWorld voxelWorld, VoxelChunk chunk, Vector3 chunkOrigin, int startX, int startY, int startZ, int lodStep)
            {
                var fillState = GetCellFillState(chunk, startX, startY, startZ, lodStep, out var block);
                if (fillState == CellFillState.Empty)
                {
                    return;
                }

                if (fillState == CellFillState.Mixed && lodStep > 1)
                {
                    var childStep = Math.Max(1, lodStep / 2);
                    for (var z = startZ; z < Math.Min(startZ + lodStep, VoxelConstants.ChunkSize); z += childStep)
                    {
                        for (var y = startY; y < Math.Min(startY + lodStep, VoxelConstants.ChunkSize); y += childStep)
                        {
                            for (var x = startX; x < Math.Min(startX + lodStep, VoxelConstants.ChunkSize); x += childStep)
                            {
                                AppendCellVertices(builder, voxelWorld, chunk, chunkOrigin, x, y, z, childStep);
                            }
                        }
                    }

                    return;
                }

                var color = GetColor(block.Material);
                var worldX = (chunk.Key.X * VoxelConstants.ChunkSize) + startX;
                var worldY = (chunk.Key.Y * VoxelConstants.ChunkSize) + startY;
                var worldZ = (chunk.Key.Z * VoxelConstants.ChunkSize) + startZ;
                var cubeWidth = HorizontalCubeSize * lodStep;
                var cubeHeight = VerticalCubeSize * lodStep;
                var cubePosition = chunkOrigin + ToRenderPosition(
                    startX + ((lodStep - 1) * 0.5f),
                    startY + ((lodStep - 1) * 0.5f),
                    startZ + ((lodStep - 1) * 0.5f));
                AppendVisibleFaces(builder, voxelWorld, cubePosition, worldX, worldY, worldZ, color, lodStep, cubeWidth, cubeHeight);
            }

            private static CellFillState GetCellFillState(VoxelChunk chunk, int startX, int startY, int startZ, int lodStep, out VoxelBlock block)
            {
                block = default;
                var foundSolid = false;
                for (var z = startZ; z < Math.Min(startZ + lodStep, VoxelConstants.ChunkSize); z++)
                {
                    for (var y = startY; y < Math.Min(startY + lodStep, VoxelConstants.ChunkSize); y++)
                    {
                        for (var x = startX; x < Math.Min(startX + lodStep, VoxelConstants.ChunkSize); x++)
                        {
                            var candidate = chunk.GetBlock(x, y, z);
                            if (!candidate.IsSolid)
                            {
                                if (foundSolid)
                                {
                                    return CellFillState.Mixed;
                                }

                                continue;
                            }

                            if (!foundSolid)
                            {
                                block = candidate;
                                foundSolid = true;
                                continue;
                            }

                            if (!block.Equals(candidate))
                            {
                                return CellFillState.Mixed;
                            }
                        }
                    }
                }

                return foundSolid ? CellFillState.Solid : CellFillState.Empty;
            }

            private static void AppendVisibleFaces(List<VertexPositionColor> builder, VoxelWorld voxelWorld, Vector3 position, int worldX, int worldY, int worldZ, Color color, int lodStep, float cubeWidth, float cubeHeight)
            {
                var halfWidth = cubeWidth * 0.5f;
                var halfHeight = cubeHeight * 0.5f;
                var left = position.X - halfWidth;
                var right = position.X + halfWidth;
                var bottom = position.Y - halfHeight;
                var top = position.Y + halfHeight;
                var back = position.Z - halfWidth;
                var front = position.Z + halfWidth;

                var lbb = new Vector3(left, bottom, back);
                var lbf = new Vector3(left, bottom, front);
                var ltb = new Vector3(left, top, back);
                var ltf = new Vector3(left, top, front);
                var rbb = new Vector3(right, bottom, back);
                var rbf = new Vector3(right, bottom, front);
                var rtb = new Vector3(right, top, back);
                var rtf = new Vector3(right, top, front);
                var frontColor = ShadeFace(color, Vector3.Forward);
                var backColor = ShadeFace(color, Vector3.Backward);
                var leftColor = ShadeFace(color, Vector3.Left);
                var rightColor = ShadeFace(color, Vector3.Right);
                var topColor = ShadeFace(color, Vector3.Up);
                var bottomColor = ShadeFace(color, Vector3.Down);

                foreach (var neighbor in NeighborFaces)
                {
                    if (HasSolidNeighbor(voxelWorld, worldX, worldY, worldZ, neighbor.X, neighbor.Y, neighbor.Z, lodStep))
                    {
                        continue;
                    }

                    switch (neighbor.Face)
                    {
                        case Face.Front:
                            AppendQuad(builder, ltf, rtf, rbf, lbf, frontColor);
                            break;
                        case Face.Back:
                            AppendQuad(builder, rbb, rtb, ltb, lbb, backColor);
                            break;
                        case Face.Left:
                            AppendQuad(builder, lbf, ltf, ltb, lbb, leftColor);
                            break;
                        case Face.Right:
                            AppendQuad(builder, rtb, rtf, rbf, rbb, rightColor);
                            break;
                        case Face.Top:
                            AppendQuad(builder, ltb, ltf, rtf, rtb, topColor);
                            break;
                        case Face.Bottom:
                            AppendQuad(builder, rbb, rbf, lbf, lbb, bottomColor);
                            break;
                    }
                }
            }

            private static Color ShadeFace(Color baseColor, Vector3 normal)
            {
                var diffuse = MathHelper.Clamp(Vector3.Dot(normal, SunLightDirection), 0f, 1f);
                var brightness = AmbientLight + (DiffuseLight * diffuse);
                return MultiplyColor(baseColor, brightness);
            }

            private static Color MultiplyColor(Color color, float factor)
            {
                factor = MathHelper.Max(0f, factor);
                return new Color(
                    (byte)Math.Clamp(color.R * factor, 0f, 255f),
                    (byte)Math.Clamp(color.G * factor, 0f, 255f),
                    (byte)Math.Clamp(color.B * factor, 0f, 255f),
                    color.A);
            }

            private static bool HasSolidNeighbor(VoxelWorld voxelWorld, int worldX, int worldY, int worldZ, int offsetX, int offsetY, int offsetZ, int lodStep)
            {
                for (var z = 0; z < lodStep; z++)
                {
                    for (var y = 0; y < lodStep; y++)
                    {
                        for (var x = 0; x < lodStep; x++)
                        {
                            var sampleX = worldX + x + (offsetX < 0 ? offsetX : offsetX * lodStep);
                            var sampleY = worldY + y + (offsetY < 0 ? offsetY : offsetY * lodStep);
                            var sampleZ = worldZ + z + (offsetZ < 0 ? offsetZ : offsetZ * lodStep);
                            if (voxelWorld.IsSolid(sampleX, sampleY, sampleZ))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            private static void AppendQuad(List<VertexPositionColor> builder, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                builder.Add(new VertexPositionColor(a, color));
                builder.Add(new VertexPositionColor(b, color));
                builder.Add(new VertexPositionColor(c, color));
                builder.Add(new VertexPositionColor(a, color));
                builder.Add(new VertexPositionColor(c, color));
                builder.Add(new VertexPositionColor(d, color));
            }

            private static Color GetColor(VoxelMaterial material)
            {
                return material switch
                {
                    VoxelMaterial.Grass => new Color(72, 148, 84),
                    VoxelMaterial.Soil => new Color(110, 84, 58),
                    VoxelMaterial.Sand => new Color(198, 182, 126),
                    VoxelMaterial.Snow => new Color(232, 236, 244),
                    _ => new Color(132, 128, 124)
                };
            }

            private enum Face
            {
                Front,
                Back,
                Left,
                Right,
                Top,
                Bottom
            }

            private enum CellFillState
            {
                Empty,
                Solid,
                Mixed
            }
        }
    }
}
