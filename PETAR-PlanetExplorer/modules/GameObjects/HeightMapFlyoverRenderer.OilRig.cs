using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
        private const float PlatformDeckSize = CubeSize * 0.52f;
        private const float PlatformDeckHeight = CubeHeight * 0.42f;
        private const float PlatformSupportSize = CubeSize * 0.16f;
        private const float PlatformPumpBeamLength = CubeSize * 0.72f;
        private const float PlatformPumpAnimationRate = 2.4f;
        private const float PlatformSmokeRiseRate = CubeHeight * 1.4f;
        private static readonly Color PlatformMetalColor = new Color(132, 134, 140);
        private static readonly Color PlatformDarkMetalColor = new Color(58, 60, 66);
        private static readonly Color PlatformAccentColor = new Color(182, 148, 54);
        private static readonly Color PlatformSmokeColor = new Color(84, 84, 90, 120);

        private readonly VoxelChunk[] _platformChunks;
        private readonly VoxelChunk[] _platformSmokeChunks;

        private void PopulateVisiblePlatformChunks(ProceduralWorldMap worldMap, IReadOnlyList<OilPlatformInstance> oilPlatforms, int visibleChunkCount, float time)
        {
            for (var index = 0; index < visibleChunkCount; index++)
            {
                PopulatePlatformChunk(_platformChunks[index], _platformSmokeChunks[index], _visibleChunkKeys[index], worldMap, oilPlatforms, time);
            }
        }

        private void PopulatePlatformChunk(VoxelChunk platformChunk, VoxelChunk smokeChunk, ChunkCacheKey cacheKey, ProceduralWorldMap worldMap, IReadOnlyList<OilPlatformInstance> oilPlatforms, float time)
        {
            platformChunk.Reset();
            smokeChunk.Reset();
            if (oilPlatforms == null)
            {
                return;
            }

            for (var index = 0; index < oilPlatforms.Count; index++)
            {
                var platform = oilPlatforms[index];
                var platformChunkX = WrapChunkStart((int)MathF.Floor(platform.Position.X / ChunkSize), _worldWidth);
                var platformChunkY = WrapChunkStart((int)MathF.Floor(platform.Position.Y / ChunkSize), _worldHeight);
                if (platformChunkX != cacheKey.StartX || platformChunkY != cacheKey.StartY)
                {
                    continue;
                }

                AppendOilPlatform(platformChunk, smokeChunk, cacheKey, worldMap, platform, time);
            }
        }

        private void AppendOilPlatform(VoxelChunk platformChunk, VoxelChunk smokeChunk, ChunkCacheKey cacheKey, ProceduralWorldMap worldMap, OilPlatformInstance platform, float time)
        {
            var localX = (WrapWorldCoordinate(platform.Position.X - cacheKey.StartX, _worldWidth) - 0.5f) * CubeSize;
            var localZ = (WrapWorldCoordinate(platform.Position.Y - cacheKey.StartY, _worldHeight) - 0.5f) * CubeSize;
            var foundationY = platform.LandedWorldY;
            var deckBottomY = foundationY + (platform.IsSea ? CubeHeight * 3.4f : CubeHeight * 2.1f);
            var deckCenter = new Vector3(localX, deckBottomY + (PlatformDeckHeight * 0.5f), localZ);
            var supportBottomY = platform.IsSea
                ? foundationY - (CubeHeight * 0.2f)
                : foundationY;
            var pumpWave = MathF.Sin((time * PlatformPumpAnimationRate) + platform.ActivationTime + (platform.Position.X * 0.013f) + (platform.Position.Y * 0.011f));

            AppendAxisAlignedCube(platformChunk, deckCenter, PlatformDeckSize * 2.4f, PlatformDeckHeight, PlatformDarkMetalColor);
            AppendAxisAlignedCube(platformChunk, deckCenter + new Vector3(0f, PlatformDeckHeight * 0.65f, 0f), PlatformDeckSize * 2.05f, PlatformDeckHeight * 0.65f, PlatformMetalColor);
            AppendAxisAlignedCube(platformChunk, deckCenter + new Vector3(0f, PlatformDeckHeight * 0.32f, PlatformDeckSize * 0.95f), PlatformDeckSize * 0.48f, PlatformDeckHeight * 0.72f, PlatformAccentColor);

            var supportOffsets = new[]
            {
                new Vector3(-PlatformDeckSize * 0.78f, 0f, -PlatformDeckSize * 0.78f),
                new Vector3(PlatformDeckSize * 0.78f, 0f, -PlatformDeckSize * 0.78f),
                new Vector3(-PlatformDeckSize * 0.78f, 0f, PlatformDeckSize * 0.78f),
                new Vector3(PlatformDeckSize * 0.78f, 0f, PlatformDeckSize * 0.78f)
            };

            for (var supportIndex = 0; supportIndex < supportOffsets.Length; supportIndex++)
            {
                var supportTopY = deckBottomY;
                var supportHeight = MathF.Max(CubeHeight * 1.4f, supportTopY - supportBottomY);
                var supportCenter = new Vector3(localX, supportBottomY + (supportHeight * 0.5f), localZ) + supportOffsets[supportIndex];
                AppendAxisAlignedCube(platformChunk, supportCenter, PlatformSupportSize, supportHeight, PlatformDarkMetalColor);
            }

            var towerCenter = deckCenter + new Vector3(-PlatformDeckSize * 0.3f, CubeHeight * 1.45f, 0f);
            AppendAxisAlignedCube(platformChunk, towerCenter, PlatformSupportSize * 1.6f, CubeHeight * 2.8f, PlatformDarkMetalColor);

            var beamCenter = deckCenter + new Vector3(PlatformDeckSize * 0.2f, CubeHeight * 2.75f + (pumpWave * CubeHeight * 0.12f), 0f);
            AppendAxisAlignedCube(platformChunk, beamCenter, PlatformPumpBeamLength, PlatformDeckHeight * 0.45f, PlatformMetalColor);
            AppendAxisAlignedCube(platformChunk, beamCenter + new Vector3(PlatformPumpBeamLength * 0.48f, -(pumpWave * CubeHeight * 0.38f), 0f), PlatformSupportSize * 1.25f, CubeHeight * 1.1f, PlatformDarkMetalColor);
            AppendAxisAlignedCube(platformChunk, beamCenter + new Vector3(-PlatformPumpBeamLength * 0.44f, pumpWave * CubeHeight * 0.26f, 0f), PlatformSupportSize * 1.18f, CubeHeight * 0.95f, PlatformAccentColor);

            var stackCenter = deckCenter + new Vector3(PlatformDeckSize * 0.76f, CubeHeight * 1.6f, -PlatformDeckSize * 0.28f);
            AppendAxisAlignedCube(platformChunk, stackCenter, PlatformSupportSize * 1.4f, CubeHeight * 2.3f, PlatformDarkMetalColor);

            for (var smokeIndex = 0; smokeIndex < 3; smokeIndex++)
            {
                var smokeT = ((time * 0.36f) + (platform.ActivationTime * 0.09f) + (smokeIndex * 0.27f)) % 1f;
                if (smokeT < 0f)
                {
                    smokeT += 1f;
                }

                var smokeCenter = stackCenter + new Vector3(
                    MathF.Sin((time * 0.9f) + smokeIndex + platform.Position.X * 0.02f) * (PlatformDeckSize * 0.14f),
                    (CubeHeight * 1.4f) + (smokeT * PlatformSmokeRiseRate * 3.2f),
                    MathF.Cos((time * 0.7f) + smokeIndex + platform.Position.Y * 0.02f) * (PlatformDeckSize * 0.12f));
                var smokeSize = MathHelper.Lerp(PlatformSupportSize * 1.25f, PlatformSupportSize * 0.65f, smokeT);
                var smokeAlpha = (byte)Math.Clamp((1f - smokeT) * PlatformSmokeColor.A, 0f, 255f);
                AppendAxisAlignedCube(smokeChunk, smokeCenter, smokeSize, smokeSize * 1.2f, new Color(PlatformSmokeColor.R, PlatformSmokeColor.G, PlatformSmokeColor.B, smokeAlpha));
            }
        }

        public readonly struct OilPlatformInstance
        {
            public OilPlatformInstance(Vector2 position, float landedWorldY, bool isSea, float activationTime)
            {
                Position = position;
                LandedWorldY = landedWorldY;
                IsSea = isSea;
                ActivationTime = activationTime;
            }

            public Vector2 Position { get; }
            public float LandedWorldY { get; }
            public bool IsSea { get; }
            public float ActivationTime { get; }
        }
    }
}
