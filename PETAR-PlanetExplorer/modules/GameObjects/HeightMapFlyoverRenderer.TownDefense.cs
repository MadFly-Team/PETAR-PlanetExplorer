using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
        private const float TownMegaRingHeight = CubeHeight * 4.2f;
        private const float TownMegaDeckThickness = CubeHeight * 0.8f;
        private const float TownMegaCoreRadius = CubeSize * 3.4f;
        private const float TownMegaArchThickness = CubeSize * 1.25f;
        private const int TownMegaArchSegments = 18;
        private const float TownTowerHeight = CubeHeight * 4.8f;
        private const float TownTowerWidth = CubeSize * 0.26f;
        private const float TownTowerCapWidth = CubeSize * 0.56f;
        private const float TownPadRadius = CubeSize * 0.52f;
        private const float TownTurretBaseWidth = CubeSize * 0.24f;
        private const float TownMissileRailLength = CubeSize * 0.72f;
        private const float TownMissileRailHeight = CubeHeight * 0.1f;
        private const float TownMissileBodyLength = CubeSize * 0.34f;
        private const float TownMissileBodyHeight = CubeHeight * 0.18f;
        private static readonly Color TownTowerColor = new Color(124, 126, 132);
        private static readonly Color TownTowerShadowColor = new Color(72, 74, 82);
        private static readonly Color TownPadColor = new Color(146, 142, 108);
        private static readonly Color TownTurretColor = new Color(178, 180, 186);
        private static readonly Color TownMissileColor = new Color(236, 236, 240);
        private static readonly Color TownMissileNoseColor = new Color(214, 88, 64);
        private static readonly Color TownMegaArchColor = new Color(242, 242, 238);
        private static readonly Color TownMegaArchShadowColor = new Color(188, 188, 184);
        private static readonly Color TownMegaDeckColor = new Color(232, 228, 220);
        private static readonly Color TownMegaGlassColor = new Color(74, 98, 128, 220);
        private static readonly Color TownMegaCoreColor = new Color(42, 86, 182);
        private static readonly Color TownMegaRailColor = new Color(56, 60, 72);

        private readonly VoxelChunk[] _townDefenseChunks = new VoxelChunk[MaxVisibleChunks];

        private void PopulateVisibleTownDefenseChunks(ProceduralWorldMap worldMap, Vector2 cameraPosition, int visibleChunkCount)
        {
            for (var index = 0; index < visibleChunkCount; index++)
            {
                PopulateTownDefenseChunk(_townDefenseChunks[index], _visibleChunkKeys[index], worldMap, cameraPosition);
            }
        }

        private void PopulateTownDefenseChunk(VoxelChunk chunk, ChunkCacheKey cacheKey, ProceduralWorldMap worldMap, Vector2 cameraPosition)
        {
            chunk.Reset();

            IReadOnlyList<Rectangle> townBounds = worldMap.GetTownBounds();
            for (var townIndex = 0; townIndex < townBounds.Count; townIndex++)
            {
                AppendTownMegastructure(chunk, cacheKey, worldMap, townBounds[townIndex]);
            }

            IReadOnlyList<ProceduralWorldMap.TownDefenseSite> sites = worldMap.GetTownDefenseSitesInChunk(cacheKey.StartX, cacheKey.StartY);
            for (var index = 0; index < sites.Count; index++)
            {
                AppendTownDefenseSite(chunk, cacheKey, worldMap, cameraPosition, sites[index]);
            }
        }

        private void AppendTownDefenseSite(VoxelChunk chunk, ChunkCacheKey cacheKey, ProceduralWorldMap worldMap, Vector2 cameraPosition, ProceduralWorldMap.TownDefenseSite site)
        {
            var localX = (WrapWorldCoordinate(site.Position.X - cacheKey.StartX, _worldWidth) - 0.5f) * CubeSize;
            var localZ = (WrapWorldCoordinate(site.Position.Y - cacheKey.StartY, _worldHeight) - 0.5f) * CubeSize;
            var baseHeight = GetCubeBottom(GetColumnHeight(site.SurfaceHeight, worldMap.MaxCubeColumn), worldMap.MaxCubeColumn);
            var towerCenter = new Vector3(localX, baseHeight + (TownTowerHeight * 0.5f), localZ);
            var deckCenter = new Vector3(localX, baseHeight + TownTowerHeight + (CubeHeight * 0.2f), localZ);
            var targetOffset = GetWrappedOffset(cameraPosition - site.Position);
            var trackDirection = targetOffset.LengthSquared() > 0.0001f
                ? Vector2.Normalize(targetOffset)
                : site.FacingDirection;
            var missileYaw = MathF.Atan2(trackDirection.Y, trackDirection.X);
            var radialForward = new Vector3(trackDirection.X, 0f, trackDirection.Y);
            var radialRight = SafeNormalize(Vector3.Cross(Vector3.Up, radialForward), Vector3.Right);
            AppendAxisAlignedCube(chunk, towerCenter, TownTowerWidth, TownTowerHeight, TownTowerShadowColor);
            AppendAxisAlignedCube(chunk, deckCenter, TownTowerCapWidth, CubeHeight * 0.38f, TownTowerColor);
            AppendAxisAlignedCube(chunk, new Vector3(localX, baseHeight + (CubeHeight * 0.18f), localZ), TownPadRadius, CubeHeight * 0.16f, TownPadColor);

            var supportOffsets = new[]
            {
                new Vector3(-TownPadRadius * 0.62f, 0f, -TownPadRadius * 0.62f),
                new Vector3(TownPadRadius * 0.62f, 0f, -TownPadRadius * 0.62f),
                new Vector3(-TownPadRadius * 0.62f, 0f, TownPadRadius * 0.62f),
                new Vector3(TownPadRadius * 0.62f, 0f, TownPadRadius * 0.62f)
            };

            for (var supportIndex = 0; supportIndex < supportOffsets.Length; supportIndex++)
            {
                AppendAxisAlignedCube(chunk, towerCenter + supportOffsets[supportIndex], TownTowerWidth * 0.52f, TownTowerHeight * 0.92f, TownTowerColor);
            }

            var turretBase = deckCenter + new Vector3(0f, CubeHeight * 0.32f, 0f);
            AppendAxisAlignedCube(chunk, turretBase, TownTurretBaseWidth, CubeHeight * 0.34f, TownTurretColor);
            AppendTrackedMissileLauncher(chunk, turretBase + new Vector3(0f, CubeHeight * 0.22f, 0f), missileYaw, radialRight);
        }

        private void AppendTrackedMissileLauncher(VoxelChunk chunk, Vector3 center, float yaw, Vector3 radialRight)
        {
            var forward = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
            var right = SafeNormalize(radialRight, Vector3.Right);
            var leftRailCenter = center - (right * CubeSize * 0.18f);
            var rightRailCenter = center + (right * CubeSize * 0.18f);

            AppendAxisAlignedCube(chunk, center, TownTurretBaseWidth * 0.82f, CubeHeight * 0.24f, TownTowerShadowColor);
            AppendAxisAlignedCube(chunk, leftRailCenter + (forward * CubeSize * 0.12f), TownMissileRailLength, TownMissileRailHeight, TownTowerColor);
            AppendAxisAlignedCube(chunk, rightRailCenter + (forward * CubeSize * 0.12f), TownMissileRailLength, TownMissileRailHeight, TownTowerColor);

            AppendAxisAlignedCube(chunk, leftRailCenter + (forward * CubeSize * 0.28f) + new Vector3(0f, CubeHeight * 0.12f, 0f), TownMissileBodyLength, TownMissileBodyHeight, TownMissileColor);
            AppendAxisAlignedCube(chunk, rightRailCenter + (forward * CubeSize * 0.28f) + new Vector3(0f, CubeHeight * 0.12f, 0f), TownMissileBodyLength, TownMissileBodyHeight, TownMissileColor);
            AppendAxisAlignedCube(chunk, leftRailCenter + (forward * CubeSize * 0.48f) + new Vector3(0f, CubeHeight * 0.16f, 0f), TownMissileBodyHeight * 0.9f, TownMissileBodyHeight * 0.8f, TownMissileNoseColor);
            AppendAxisAlignedCube(chunk, rightRailCenter + (forward * CubeSize * 0.48f) + new Vector3(0f, CubeHeight * 0.16f, 0f), TownMissileBodyHeight * 0.9f, TownMissileBodyHeight * 0.8f, TownMissileNoseColor);
        }

        private void AppendTownMegastructure(VoxelChunk chunk, ChunkCacheKey cacheKey, ProceduralWorldMap worldMap, Rectangle townBounds)
        {
            var centerX = townBounds.Left + (townBounds.Width * 0.5f);
            var centerY = townBounds.Top + (townBounds.Height * 0.5f);
            var centerCellX = WrapGridCoordinate((int)MathF.Floor(centerX), _worldWidth);
            var centerCellY = WrapGridCoordinate((int)MathF.Floor(centerY), _worldHeight);
            var centerChunkX = (centerCellX / ChunkSize) * ChunkSize;
            var centerChunkY = (centerCellY / ChunkSize) * ChunkSize;
            if (cacheKey.StartX != centerChunkX || cacheKey.StartY != centerChunkY)
            {
                return;
            }

            var localCenter = new Vector3(
                (WrapWorldCoordinate(centerX - cacheKey.StartX, _worldWidth) - 0.5f) * CubeSize,
                0f,
                (WrapWorldCoordinate(centerY - cacheKey.StartY, _worldHeight) - 0.5f) * CubeSize);
            var baseHeight = GetCubeBottom(GetColumnHeight(worldMap.SampleVoxelHeight(centerX, centerY), worldMap.MaxCubeColumn), worldMap.MaxCubeColumn);
            var townSpan = Math.Max(townBounds.Width, townBounds.Height) * CubeSize;
            var deckRadiusX = Math.Max(CubeSize * 8f, townBounds.Width * CubeSize * 0.44f);
            var deckRadiusZ = Math.Max(CubeSize * 8f, townBounds.Height * CubeSize * 0.44f);
            var ringY = baseHeight + TownMegaRingHeight;
            var ringThickness = TownMegaDeckThickness;
            var canopyY = ringY + (CubeHeight * 3.8f);
            var archHeight = Math.Max(CubeHeight * 10f, townSpan * 0.22f);
            var coreHeight = archHeight + (CubeHeight * 4f);

            AppendCylinder(chunk, localCenter + new Vector3(0f, baseHeight + (coreHeight * 0.5f), 0f), TownMegaCoreRadius, coreHeight, TownMegaCoreColor, 20);
            AppendDisk(chunk, localCenter + new Vector3(0f, ringY, 0f), deckRadiusX, deckRadiusZ, ringThickness, TownMegaDeckColor, TownMegaGlassColor, 28);
            AppendRailRing(chunk, localCenter + new Vector3(0f, ringY + (ringThickness * 0.75f), 0f), deckRadiusX * 0.98f, deckRadiusZ * 0.98f, CubeHeight * 0.5f, 24);
            AppendArchPair(chunk, localCenter, deckRadiusX, deckRadiusZ, canopyY, archHeight, 0f);
            AppendArchPair(chunk, localCenter, deckRadiusX, deckRadiusZ, canopyY, archHeight, MathHelper.PiOver2);
        }

        private void AppendArchPair(VoxelChunk chunk, Vector3 center, float deckRadiusX, float deckRadiusZ, float canopyY, float archHeight, float angle)
        {
            var axis = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var perpendicular = new Vector2(-axis.Y, axis.X);
            var baseA = center + new Vector3(axis.X * deckRadiusX * 0.92f, 0f, axis.Y * deckRadiusZ * 0.92f);
            var baseB = center - new Vector3(axis.X * deckRadiusX * 0.92f, 0f, axis.Y * deckRadiusZ * 0.92f);
            var apex = center + new Vector3(perpendicular.X * deckRadiusX * 0.18f, canopyY + archHeight, perpendicular.Y * deckRadiusZ * 0.18f);
            AppendCurvedBeam(chunk, baseA, apex, TownMegaArchThickness, TownMegaArchColor, TownMegaArchShadowColor);
            AppendCurvedBeam(chunk, baseB, apex, TownMegaArchThickness, TownMegaArchColor, TownMegaArchShadowColor);
        }

        private void AppendCurvedBeam(VoxelChunk chunk, Vector3 start, Vector3 end, float thickness, Color topColor, Color sideColor)
        {
            var previousLeft = Vector3.Zero;
            var previousRight = Vector3.Zero;
            var previousUp = Vector3.Zero;
            var previousDown = Vector3.Zero;
            var hasPrevious = false;

            for (var segment = 0; segment <= TownMegaArchSegments; segment++)
            {
                var t = segment / (float)TownMegaArchSegments;
                var position = Vector3.Lerp(start, end, t);
                position.Y += MathF.Sin(t * MathF.PI) * (end.Y - start.Y) * 0.18f;

                Vector3 tangent;
                if (segment == TownMegaArchSegments)
                {
                    tangent = position - Vector3.Lerp(start, end, (segment - 1) / (float)TownMegaArchSegments);
                }
                else
                {
                    var nextT = (segment + 1) / (float)TownMegaArchSegments;
                    var nextPosition = Vector3.Lerp(start, end, nextT);
                    nextPosition.Y += MathF.Sin(nextT * MathF.PI) * (end.Y - start.Y) * 0.18f;
                    tangent = nextPosition - position;
                }

                tangent = SafeNormalize(tangent, Vector3.Up);
                var right = SafeNormalize(Vector3.Cross(Vector3.Up, tangent), Vector3.Right) * (thickness * 0.5f);
                var up = Vector3.Up * (thickness * 0.5f);
                var left = position - right;
                var rightPoint = position + right;
                var upPoint = position + up;
                var downPoint = position - up;

                if (hasPrevious)
                {
                    AppendQuad(chunk, previousUp, upPoint, rightPoint, previousRight, topColor);
                    AppendQuad(chunk, previousLeft, left, upPoint, previousUp, topColor);
                    AppendQuad(chunk, previousDown, downPoint, left, previousLeft, sideColor);
                    AppendQuad(chunk, previousRight, rightPoint, downPoint, previousDown, sideColor);
                }

                previousLeft = left;
                previousRight = rightPoint;
                previousUp = upPoint;
                previousDown = downPoint;
                hasPrevious = true;
            }
        }

        private void AppendDisk(VoxelChunk chunk, Vector3 center, float radiusX, float radiusZ, float thickness, Color topColor, Color sideColor, int segments)
        {
            var topCenter = center + new Vector3(0f, thickness * 0.5f, 0f);
            var bottomCenter = center - new Vector3(0f, thickness * 0.5f, 0f);
            for (var index = 0; index < segments; index++)
            {
                var angle0 = index * MathHelper.TwoPi / segments;
                var angle1 = (index + 1) * MathHelper.TwoPi / segments;
                var top0 = topCenter + new Vector3(MathF.Cos(angle0) * radiusX, 0f, MathF.Sin(angle0) * radiusZ);
                var top1 = topCenter + new Vector3(MathF.Cos(angle1) * radiusX, 0f, MathF.Sin(angle1) * radiusZ);
                var bottom0 = bottomCenter + new Vector3(MathF.Cos(angle0) * radiusX * 0.92f, 0f, MathF.Sin(angle0) * radiusZ * 0.92f);
                var bottom1 = bottomCenter + new Vector3(MathF.Cos(angle1) * radiusX * 0.92f, 0f, MathF.Sin(angle1) * radiusZ * 0.92f);
                AppendTriangleFanFace(chunk, topCenter, top0, top1, topColor);
                AppendTriangleFanFace(chunk, bottomCenter, bottom1, bottom0, MultiplyColor(sideColor, 0.78f));
                AppendQuad(chunk, top0, top1, bottom1, bottom0, sideColor);
            }
        }

        private void AppendRailRing(VoxelChunk chunk, Vector3 center, float radiusX, float radiusZ, float railHeight, int segments)
        {
            var topColor = TownMegaRailColor;
            var postColor = MultiplyColor(TownMegaRailColor, 1.15f);
            var previousOuter = Vector3.Zero;
            var previousInner = Vector3.Zero;
            var hasPrevious = false;

            for (var index = 0; index <= segments; index++)
            {
                var angle = index * MathHelper.TwoPi / segments;
                var radial = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                var outer = center + new Vector3(radial.X * radiusX, 0f, radial.Z * radiusZ);
                var inner = center + new Vector3(radial.X * (radiusX - CubeSize * 0.28f), 0f, radial.Z * (radiusZ - CubeSize * 0.28f));
                AppendCylinder(chunk, outer + new Vector3(0f, railHeight * 0.5f, 0f), CubeSize * 0.08f, railHeight, postColor, 6);
                if (hasPrevious)
                {
                    AppendQuad(chunk, previousOuter + new Vector3(0f, railHeight, 0f), outer + new Vector3(0f, railHeight, 0f), inner + new Vector3(0f, railHeight, 0f), previousInner + new Vector3(0f, railHeight, 0f), topColor);
                }

                previousOuter = outer;
                previousInner = inner;
                hasPrevious = true;
            }
        }

        private void AppendCylinder(VoxelChunk chunk, Vector3 center, float radius, float height, Color color, int segments)
        {
            var topCenter = center + new Vector3(0f, height * 0.5f, 0f);
            var bottomCenter = center - new Vector3(0f, height * 0.5f, 0f);
            var sideColor = MultiplyColor(color, 0.82f);
            for (var index = 0; index < segments; index++)
            {
                var angle0 = index * MathHelper.TwoPi / segments;
                var angle1 = (index + 1) * MathHelper.TwoPi / segments;
                var top0 = topCenter + new Vector3(MathF.Cos(angle0) * radius, 0f, MathF.Sin(angle0) * radius);
                var top1 = topCenter + new Vector3(MathF.Cos(angle1) * radius, 0f, MathF.Sin(angle1) * radius);
                var bottom0 = bottomCenter + new Vector3(MathF.Cos(angle0) * radius, 0f, MathF.Sin(angle0) * radius);
                var bottom1 = bottomCenter + new Vector3(MathF.Cos(angle1) * radius, 0f, MathF.Sin(angle1) * radius);
                AppendTriangleFanFace(chunk, topCenter, top0, top1, color);
                AppendTriangleFanFace(chunk, bottomCenter, bottom1, bottom0, MultiplyColor(color, 0.72f));
                AppendQuad(chunk, top0, top1, bottom1, bottom0, sideColor);
            }
        }

        private static void AppendTriangleFanFace(VoxelChunk chunk, Vector3 center, Vector3 edgeA, Vector3 edgeB, Color color)
        {
            chunk.AppendTriangle(center, edgeA, edgeB, color);
        }

    }
}