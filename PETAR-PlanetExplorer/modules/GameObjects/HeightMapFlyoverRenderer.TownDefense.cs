using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
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
    }
}