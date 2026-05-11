using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PETAR_PlanetExplorer.Modules.Voxels
{
    public sealed class VoxelRenderer : IDisposable
    {
        public const float HorizontalWorldScale = 3f;
        private const float HorizontalCubeSize = HorizontalWorldScale;
        private const float VerticalCubeSize = 2f;
        private const float AmbientLight = 0.42f;
        private const float DiffuseLight = 0.58f;
        private static readonly Vector3 EarthFogLowColor = new Vector3(0.5294118f, 0.80784315f, 0.92156863f);
        private static readonly Vector3 EarthFogHighColor = new Vector3(0.03529412f, 0.10980392f, 0.25882354f);
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
                TextureEnabled = false,
                FogEnabled = true
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

            var verticalRenderOrigin = VoxelWorldBuilder.GetRenderVerticalOrigin(voxelWorld.HeightLimit) * VerticalCubeSize;
            var altitudeRatio = MathHelper.Clamp(cameraPosition.Y / 172f, 0f, 1f);
            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            _effect.FogColor = Vector3.Lerp(EarthFogLowColor, EarthFogHighColor, altitudeRatio);
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            var frustum = new BoundingFrustum(view * projection);
            var maxViewDistance = GetMaxViewDistance(cameraPosition.Y);
            var fogDistance = GetFogDistance(cameraPosition.Y);
            var maxViewDistanceSquared = maxViewDistance * maxViewDistance;
            var highAltitudeFogT = MathHelper.Clamp((altitudeRatio - 0.7f) / 0.3f, 0f, 1f);
            var fogStart = MathHelper.Lerp(140f, 190f, altitudeRatio) * 0.72f;
            var fogEnd = (fogDistance * MathHelper.Lerp(1.65f, 2.15f, altitudeRatio)) * 0.72f;
            _effect.FogStart = MathHelper.Lerp(fogStart, 82f * 0.72f, highAltitudeFogT);
            _effect.FogEnd = MathHelper.Lerp(fogEnd, fogDistance * 0.72f, highAltitudeFogT);

            foreach (var chunkEntry in voxelWorld.Chunks)
            {
                var chunk = chunkEntry.Value;
                var chunkBounds = GetChunkBounds(chunk, verticalRenderOrigin);
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
                var vertices = GetOrBuildChunkMesh(voxelWorld, chunk, lodStep, verticalRenderOrigin);

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
            var verticalRenderOrigin = VoxelWorldBuilder.GetRenderVerticalOrigin(voxelWorld.HeightLimit) * VerticalCubeSize;
            var maxViewDistance = GetMaxViewDistance(cameraPosition.Y) + (VoxelConstants.ChunkSize * HorizontalCubeSize * 2f);
            var maxViewDistanceSquared = maxViewDistance * maxViewDistance;
            foreach (var chunkEntry in voxelWorld.Chunks)
            {
                var chunk = chunkEntry.Value;
                var chunkBounds = GetChunkBounds(chunk, verticalRenderOrigin);
                var chunkCenter = chunkBounds.Min + ((chunkBounds.Max - chunkBounds.Min) * 0.5f);
                var distanceSquared = Vector3.DistanceSquared(cameraPosition, chunkCenter);
                if (distanceSquared > maxViewDistanceSquared)
                {
                    continue;
                }

                var targetLodStep = GetLodStep(distanceSquared);
                var warmedFallback = false;
                for (var lodIndex = 0; lodIndex < WarmupLodSteps.Length; lodIndex++)
                {
                    var lodStep = WarmupLodSteps[lodIndex];
                    if (lodStep != targetLodStep)
                    {
                        if (warmedFallback || lodStep <= targetLodStep)
                        {
                            continue;
                        }

                        warmedFallback = true;
                    }

                    if (chunk.TryGetCachedMesh(lodStep, out _) && !chunk.IsDirty)
                    {
                        continue;
                    }

                    GetOrBuildChunkMesh(voxelWorld, chunk, lodStep, verticalRenderOrigin);
                    warmedMeshes++;
                }
            }

            return warmedMeshes;
        }

        private static BoundingBox GetChunkBounds(VoxelChunk chunk, float verticalRenderOrigin)
        {
            var halfExtents = new Vector3(HorizontalCubeSize * 0.5f, VerticalCubeSize * 0.5f, HorizontalCubeSize * 0.5f);
            var min = ToRenderPosition(
                chunk.Key.X * VoxelConstants.ChunkSize,
                chunk.Key.Y * VoxelConstants.ChunkSize,
                chunk.Key.Z * VoxelConstants.ChunkSize,
                verticalRenderOrigin) - halfExtents;
            var max = min + new Vector3(
                VoxelConstants.ChunkSize * HorizontalCubeSize,
                VoxelConstants.ChunkSize * VerticalCubeSize,
                VoxelConstants.ChunkSize * HorizontalCubeSize);
            return new BoundingBox(min, max);
        }

        private static float GetMaxViewDistance(float cameraHeight)
        {
            var altitudeRatio = MathHelper.Clamp(cameraHeight / 172f, 0f, 1f);
            return MathHelper.Lerp(320f, 700f, altitudeRatio);
        }

        private static float GetFogDistance(float cameraHeight)
        {
            var altitudeRatio = MathHelper.Clamp(cameraHeight / 172f, 0f, 1f);
            return MathHelper.Lerp(160f, 280f, altitudeRatio);
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

        private static VertexPositionColor[] GetOrBuildChunkMesh(VoxelWorld voxelWorld, VoxelChunk chunk, int lodStep, float verticalRenderOrigin)
        {
            if (!chunk.TryGetCachedMesh(lodStep, out var vertices) || chunk.IsDirty)
            {
                vertices = VoxelMeshBuilder.BuildVertices(voxelWorld, chunk, lodStep, verticalRenderOrigin);
                chunk.UpdateCachedMesh(lodStep, vertices);
            }

            return vertices;
        }

        public void Dispose()
        {
            _effect.Dispose();
        }

        private static Vector3 ToRenderPosition(float voxelX, float voxelY, float voxelZ, float verticalRenderOrigin)
        {
            return new Vector3(voxelX * HorizontalCubeSize, (voxelZ * VerticalCubeSize) - verticalRenderOrigin, voxelY * HorizontalCubeSize);
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
            private static readonly MaterialFaceColors[] FaceColorsByMaterial = CreateFaceColorsByMaterial();
            public static VertexPositionColor[] BuildVertices(VoxelWorld voxelWorld, VoxelChunk chunk, int lodStep, float verticalRenderOrigin)
            {
                lodStep = Math.Max(1, lodStep);
                var cellCountPerAxis = VoxelConstants.ChunkSize / lodStep;
                var estimatedMaxVisibleFaces = cellCountPerAxis * cellCountPerAxis * 6;
                var builder = new List<VertexPositionColor>(estimatedMaxVisibleFaces * 6);
                var chunkWorldX = chunk.Key.X * VoxelConstants.ChunkSize;
                var chunkWorldY = chunk.Key.Y * VoxelConstants.ChunkSize;
                var chunkWorldZ = chunk.Key.Z * VoxelConstants.ChunkSize;
                var chunkOrigin = ToRenderPosition(
                    chunkWorldX,
                    chunkWorldY,
                    chunkWorldZ,
                    verticalRenderOrigin);
                var cubeWidth = HorizontalCubeSize * lodStep;
                var cubeHeight = VerticalCubeSize * lodStep;
                var centerOffset = (lodStep - 1) * 0.5f;

                for (var z = 0; z < VoxelConstants.ChunkSize; z += lodStep)
                {
                    for (var y = 0; y < VoxelConstants.ChunkSize; y += lodStep)
                    {
                        for (var x = 0; x < VoxelConstants.ChunkSize; x += lodStep)
                        {
                            AppendCellVertices(builder, voxelWorld, chunk, chunkOrigin, chunkWorldX, chunkWorldY, chunkWorldZ, x, y, z, lodStep, cubeWidth, cubeHeight, centerOffset);
                        }
                    }
                }

                return builder.ToArray();
            }

            private static void AppendCellVertices(List<VertexPositionColor> builder, VoxelWorld voxelWorld, VoxelChunk chunk, Vector3 chunkOrigin, int chunkWorldX, int chunkWorldY, int chunkWorldZ, int startX, int startY, int startZ, int lodStep, float cubeWidth, float cubeHeight, float centerOffset)
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
                                var childCubeWidth = HorizontalCubeSize * childStep;
                                var childCubeHeight = VerticalCubeSize * childStep;
                                var childCenterOffset = (childStep - 1) * 0.5f;
                                AppendCellVertices(builder, voxelWorld, chunk, chunkOrigin, chunkWorldX, chunkWorldY, chunkWorldZ, x, y, z, childStep, childCubeWidth, childCubeHeight, childCenterOffset);
                            }
                        }
                    }

                    return;
                }

                var worldX = chunkWorldX + startX;
                var worldY = chunkWorldY + startY;
                var worldZ = chunkWorldZ + startZ;
                var cubePosition = chunkOrigin + ToRenderPosition(
                    startX + centerOffset,
                    startY + centerOffset,
                    startZ + centerOffset,
                    0f);
                AppendVisibleFaces(builder, voxelWorld, chunk, cubePosition, startX, startY, startZ, worldX, worldY, worldZ, block.Material, lodStep, cubeWidth, cubeHeight);
            }

            private static CellFillState GetCellFillState(VoxelChunk chunk, int startX, int startY, int startZ, int lodStep, out VoxelBlock block)
            {
                block = chunk.GetBlock(startX, startY, startZ);
                if (lodStep == 1)
                {
                    return block.IsSolid ? CellFillState.Solid : CellFillState.Empty;
                }

                var endZ = Math.Min(startZ + lodStep, VoxelConstants.ChunkSize);
                var endY = Math.Min(startY + lodStep, VoxelConstants.ChunkSize);
                var endX = Math.Min(startX + lodStep, VoxelConstants.ChunkSize);
                if (block.IsSolid)
                {
                    for (var z = startZ; z < endZ; z++)
                    {
                        for (var y = startY; y < endY; y++)
                        {
                            for (var x = startX; x < endX; x++)
                            {
                                if (x == startX && y == startY && z == startZ)
                                {
                                    continue;
                                }

                                var candidate = chunk.GetBlock(x, y, z);
                                if (!candidate.IsSolid || !block.Equals(candidate))
                                {
                                    return CellFillState.Mixed;
                                }
                            }
                        }
                    }

                    return CellFillState.Solid;
                }

                block = default;
                var foundSolid = false;
                for (var z = startZ; z < endZ; z++)
                {
                    for (var y = startY; y < endY; y++)
                    {
                        for (var x = startX; x < endX; x++)
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

            private static void AppendVisibleFaces(List<VertexPositionColor> builder, VoxelWorld voxelWorld, VoxelChunk chunk, Vector3 position, int localX, int localY, int localZ, int worldX, int worldY, int worldZ, VoxelMaterial material, int lodStep, float cubeWidth, float cubeHeight)
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
                var faceColors = FaceColorsByMaterial[(int)material];

                foreach (var neighbor in NeighborFaces)
                {
                    if (HasSolidNeighbor(voxelWorld, chunk, localX, localY, localZ, worldX, worldY, worldZ, neighbor.X, neighbor.Y, neighbor.Z, lodStep))
                    {
                        continue;
                    }

                    switch (neighbor.Face)
                    {
                        case Face.Front:
                            AppendQuad(builder, ltf, rtf, rbf, lbf, faceColors.Front);
                            break;
                        case Face.Back:
                            AppendQuad(builder, rbb, rtb, ltb, lbb, faceColors.Back);
                            break;
                        case Face.Left:
                            AppendQuad(builder, lbf, ltf, ltb, lbb, faceColors.Left);
                            break;
                        case Face.Right:
                            AppendQuad(builder, rtb, rtf, rbf, rbb, faceColors.Right);
                            break;
                        case Face.Top:
                            AppendQuad(builder, ltb, ltf, rtf, rtb, faceColors.Top);
                            break;
                        case Face.Bottom:
                            AppendQuad(builder, rbb, rbf, lbf, lbb, faceColors.Bottom);
                            break;
                    }
                }
            }

            private static MaterialFaceColors[] CreateFaceColorsByMaterial()
            {
                var faceColors = new MaterialFaceColors[Enum.GetValues<VoxelMaterial>().Length];
                foreach (var material in Enum.GetValues<VoxelMaterial>())
                {
                    var baseColor = GetColor(material);
                    faceColors[(int)material] = new MaterialFaceColors(
                        ShadeFace(baseColor, Vector3.Forward),
                        ShadeFace(baseColor, Vector3.Backward),
                        ShadeFace(baseColor, Vector3.Left),
                        ShadeFace(baseColor, Vector3.Right),
                        ShadeFace(baseColor, Vector3.Up),
                        ShadeFace(baseColor, Vector3.Down));
                }

                return faceColors;
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

            private static bool HasSolidNeighbor(VoxelWorld voxelWorld, VoxelChunk chunk, int localX, int localY, int localZ, int worldX, int worldY, int worldZ, int offsetX, int offsetY, int offsetZ, int lodStep)
            {
                var neighborLocalX = localX + (offsetX < 0 ? offsetX : offsetX * lodStep);
                var neighborLocalY = localY + (offsetY < 0 ? offsetY : offsetY * lodStep);
                var neighborLocalZ = localZ + (offsetZ < 0 ? offsetZ : offsetZ * lodStep);
                if (lodStep == 1)
                {
                    if ((uint)neighborLocalX < VoxelConstants.ChunkSize
                        && (uint)neighborLocalY < VoxelConstants.ChunkSize
                        && (uint)neighborLocalZ < VoxelConstants.ChunkSize)
                    {
                        return chunk.GetBlock(neighborLocalX, neighborLocalY, neighborLocalZ).IsSolid;
                    }

                    if (TryGetNeighborChunk(voxelWorld, chunk, offsetX, offsetY, offsetZ, out var neighborChunk))
                    {
                        return neighborChunk.GetBlock(
                            neighborLocalX < 0 ? VoxelConstants.ChunkSize - 1 : neighborLocalX >= VoxelConstants.ChunkSize ? 0 : neighborLocalX,
                            neighborLocalY < 0 ? VoxelConstants.ChunkSize - 1 : neighborLocalY >= VoxelConstants.ChunkSize ? 0 : neighborLocalY,
                            neighborLocalZ < 0 ? VoxelConstants.ChunkSize - 1 : neighborLocalZ >= VoxelConstants.ChunkSize ? 0 : neighborLocalZ).IsSolid;
                    }

                    return false;
                }

                var sampleBaseX = worldX + (offsetX < 0 ? offsetX : offsetX * lodStep);
                var sampleBaseY = worldY + (offsetY < 0 ? offsetY : offsetY * lodStep);
                var sampleBaseZ = worldZ + (offsetZ < 0 ? offsetZ : offsetZ * lodStep);

                if (offsetX != 0)
                {
                    if (neighborLocalX >= 0 && neighborLocalX < VoxelConstants.ChunkSize)
                    {
                        for (var y = 0; y < lodStep; y++)
                        {
                            for (var z = 0; z < lodStep; z++)
                            {
                                if (chunk.GetBlock(neighborLocalX, localY + y, localZ + z).IsSolid)
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    }

                    if (IsRangeWithinChunk(localY, lodStep)
                        && IsRangeWithinChunk(localZ, lodStep)
                        && TryGetNeighborChunk(voxelWorld, chunk, offsetX, 0, 0, out var neighborChunk))
                    {
                        return HasSolidOnXPlane(neighborChunk, neighborLocalX < 0 ? VoxelConstants.ChunkSize - 1 : 0, localY, localZ, lodStep);
                    }

                    for (var y = 0; y < lodStep; y++)
                    {
                        for (var z = 0; z < lodStep; z++)
                        {
                            if (voxelWorld.IsSolid(sampleBaseX, sampleBaseY + y, sampleBaseZ + z))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }

                if (offsetY != 0)
                {
                    if (neighborLocalY >= 0 && neighborLocalY < VoxelConstants.ChunkSize)
                    {
                        for (var x = 0; x < lodStep; x++)
                        {
                            for (var z = 0; z < lodStep; z++)
                            {
                                if (chunk.GetBlock(localX + x, neighborLocalY, localZ + z).IsSolid)
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    }

                    if (IsRangeWithinChunk(localX, lodStep)
                        && IsRangeWithinChunk(localZ, lodStep)
                        && TryGetNeighborChunk(voxelWorld, chunk, 0, offsetY, 0, out var neighborChunk))
                    {
                        return HasSolidOnYPlane(neighborChunk, localX, neighborLocalY < 0 ? VoxelConstants.ChunkSize - 1 : 0, localZ, lodStep);
                    }

                    for (var x = 0; x < lodStep; x++)
                    {
                        for (var z = 0; z < lodStep; z++)
                        {
                            if (voxelWorld.IsSolid(sampleBaseX + x, sampleBaseY, sampleBaseZ + z))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }

                if (neighborLocalZ >= 0 && neighborLocalZ < VoxelConstants.ChunkSize)
                {
                    for (var x = 0; x < lodStep; x++)
                    {
                        for (var y = 0; y < lodStep; y++)
                        {
                            if (chunk.GetBlock(localX + x, localY + y, neighborLocalZ).IsSolid)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }

                if (IsRangeWithinChunk(localX, lodStep)
                    && IsRangeWithinChunk(localY, lodStep)
                    && TryGetNeighborChunk(voxelWorld, chunk, 0, 0, offsetZ, out var topBottomNeighborChunk))
                {
                    return HasSolidOnZPlane(topBottomNeighborChunk, localX, localY, neighborLocalZ < 0 ? VoxelConstants.ChunkSize - 1 : 0, lodStep);
                }

                for (var x = 0; x < lodStep; x++)
                {
                    for (var y = 0; y < lodStep; y++)
                    {
                        if (voxelWorld.IsSolid(sampleBaseX + x, sampleBaseY + y, sampleBaseZ))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private static bool TryGetNeighborChunk(VoxelWorld voxelWorld, VoxelChunk chunk, int offsetX, int offsetY, int offsetZ, out VoxelChunk neighborChunk)
            {
                return voxelWorld.TryGetChunk(new VoxelChunkKey(chunk.Key.X + offsetX, chunk.Key.Y + offsetY, chunk.Key.Z + offsetZ), out neighborChunk);
            }

            private static bool IsRangeWithinChunk(int start, int length)
            {
                return start >= 0 && start + length <= VoxelConstants.ChunkSize;
            }

            private static bool HasSolidOnXPlane(VoxelChunk chunk, int planeX, int startY, int startZ, int lodStep)
            {
                for (var y = 0; y < lodStep; y++)
                {
                    for (var z = 0; z < lodStep; z++)
                    {
                        if (chunk.GetBlock(planeX, startY + y, startZ + z).IsSolid)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private static bool HasSolidOnYPlane(VoxelChunk chunk, int startX, int planeY, int startZ, int lodStep)
            {
                for (var x = 0; x < lodStep; x++)
                {
                    for (var z = 0; z < lodStep; z++)
                    {
                        if (chunk.GetBlock(startX + x, planeY, startZ + z).IsSolid)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private static bool HasSolidOnZPlane(VoxelChunk chunk, int startX, int startY, int planeZ, int lodStep)
            {
                for (var x = 0; x < lodStep; x++)
                {
                    for (var y = 0; y < lodStep; y++)
                    {
                        if (chunk.GetBlock(startX + x, startY + y, planeZ).IsSolid)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private static bool IsWithinChunk(int x, int y, int z)
            {
                return x >= 0 && x < VoxelConstants.ChunkSize
                    && y >= 0 && y < VoxelConstants.ChunkSize
                    && z >= 0 && z < VoxelConstants.ChunkSize;
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

            private readonly record struct MaterialFaceColors(
                Color Front,
                Color Back,
                Color Left,
                Color Right,
                Color Top,
                Color Bottom);
        }
    }
}
