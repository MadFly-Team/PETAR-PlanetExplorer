using System;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
        private const float BirdCubeSize = CubeSize / 32f;
        private const float BirdCubeHeight = CubeHeight / 32f;
        private const int BirdMinCount = 50;
        private const int BirdMaxCount = 100;
        private const int BirdBodyCubeCount = 4;
        private const int BirdWingCubeCount = 10;
        private const float BirdMinSpeed = 2.5f;
        private const float BirdMaxSpeed = 6f;
        private const float BirdMinLevel = 34f;
        private const float BirdMaxLevel = 48f;
        private const float BirdTerrainClearance = 3f;
        private const float BirdRespawnPadding = ChunkSize * 6f;
        private const float BirdTurnRate = 0.45f;
        private const float BirdSwoopDepth = 5f;
        private const float BirdSwoopRate = 1.4f;
        private static readonly Color BirdBodyColor = Color.White;
        private static readonly Color BirdHeadColor = new Color(220, 48, 48);

        private BirdInstance[] _birds;
        private Random _birdRandom;
        private int _birdSeed = int.MinValue;
        private float _lastBirdUpdateTime;

        private void PopulateBirdChunk(VoxelChunk birdChunk, ChunkCacheKey cacheKey, ProceduralWorldMap worldMap, float time)
        {
            birdChunk.Reset();
            if (_birds == null)
            {
                return;
            }

            for (var index = 0; index < _birds.Length; index++)
            {
                ref var bird = ref _birds[index];
                var birdChunkX = WrapChunkStart((int)MathF.Floor(bird.Position.X / ChunkSize), _worldWidth);
                var birdChunkY = WrapChunkStart((int)MathF.Floor(bird.Position.Y / ChunkSize), _worldHeight);
                if (birdChunkX != cacheKey.StartX || birdChunkY != cacheKey.StartY)
                {
                    continue;
                }

                AppendBird(birdChunk, cacheKey, worldMap, bird, time);
            }
        }

        private void EnsureBirdsInitialized(ProceduralWorldMap worldMap, Vector2 cameraPosition, Vector2 forward, Vector2 right, float maxDistance, float viewWidth, float time)
        {
            if (_birdSeed == worldMap.Seed && _birds != null)
            {
                return;
            }

            _birdSeed = worldMap.Seed;
            _birdRandom = new Random(worldMap.Seed ^ unchecked((int)0x45d9f3b));
            _birds = new BirdInstance[_birdRandom.Next(BirdMinCount, BirdMaxCount + 1)];
            for (var index = 0; index < _birds.Length; index++)
            {
                _birds[index] = CreateBird(worldMap, cameraPosition, forward, right, maxDistance, viewWidth);
            }

            _lastBirdUpdateTime = time;
        }

        private void UpdateBirds(ProceduralWorldMap worldMap, Vector2 cameraPosition, Vector2 forward, Vector2 right, float maxDistance, float viewWidth, float time)
        {
            if (_birds == null)
            {
                return;
            }

            var deltaTime = time - _lastBirdUpdateTime;
            if (deltaTime < 0f || deltaTime > 1f)
            {
                deltaTime = 0f;
            }

            _lastBirdUpdateTime = time;
            for (var index = 0; index < _birds.Length; index++)
            {
                ref var bird = ref _birds[index];
                var headingDelta = MathF.Sin((time * bird.TurnFrequency) + bird.TurnOffset) * BirdTurnRate * deltaTime;
                bird.Heading += headingDelta;
                var travel = new Vector2(MathF.Cos(bird.Heading), MathF.Sin(bird.Heading)) * (bird.Speed * deltaTime);
                bird.Position = new Vector2(
                    WrapWorldCoordinate(bird.Position.X + travel.X, _worldWidth),
                    WrapWorldCoordinate(bird.Position.Y + travel.Y, _worldHeight));

                var terrainLevel = worldMap.SampleHeight(bird.Position.X, bird.Position.Y) * (worldMap.MaxCubeColumn - 1);
                bird.Level = MathF.Max(bird.Level, terrainLevel + BirdTerrainClearance);

                var wrappedOffset = GetWrappedOffset(bird.Position - cameraPosition);
                var forwardDistance = Vector2.Dot(wrappedOffset, forward);
                var lateralDistance = MathF.Abs(Vector2.Dot(wrappedOffset, right));
                if (forwardDistance < -BirdRespawnPadding || forwardDistance > maxDistance + BirdRespawnPadding || lateralDistance > viewWidth + BirdRespawnPadding)
                {
                    bird = CreateBird(worldMap, cameraPosition, forward, right, maxDistance, viewWidth);
                }
            }
        }

        private BirdInstance CreateBird(ProceduralWorldMap worldMap, Vector2 cameraPosition, Vector2 forward, Vector2 right, float maxDistance, float viewWidth)
        {
            var distance = MathHelper.Lerp(ChunkSize * 3f, maxDistance * 0.95f, (float)_birdRandom.NextDouble());
            var lateral = MathHelper.Lerp(-viewWidth * 0.75f, viewWidth * 0.75f, (float)_birdRandom.NextDouble());
            var spawnPosition = cameraPosition + (forward * distance) + (right * lateral);
            spawnPosition = new Vector2(WrapWorldCoordinate(spawnPosition.X, _worldWidth), WrapWorldCoordinate(spawnPosition.Y, _worldHeight));
            var heading = MathF.Atan2(forward.Y, forward.X) + MathHelper.Lerp(-0.45f, 0.45f, (float)_birdRandom.NextDouble());
            var speed = MathHelper.Lerp(BirdMinSpeed, BirdMaxSpeed, (float)_birdRandom.NextDouble());
            var terrainLevel = worldMap.SampleHeight(spawnPosition.X, spawnPosition.Y) * (worldMap.MaxCubeColumn - 1);
            var level = MathF.Max(MathHelper.Lerp(BirdMinLevel, BirdMaxLevel, (float)_birdRandom.NextDouble()), terrainLevel + BirdTerrainClearance);
            var flapOffset = MathHelper.Lerp(0f, MathHelper.TwoPi, (float)_birdRandom.NextDouble());
            var turnOffset = MathHelper.Lerp(0f, MathHelper.TwoPi, (float)_birdRandom.NextDouble());
            var turnFrequency = MathHelper.Lerp(0.45f, 0.95f, (float)_birdRandom.NextDouble());
            var swoopOffset = MathHelper.Lerp(0f, MathHelper.TwoPi, (float)_birdRandom.NextDouble());
            return new BirdInstance(spawnPosition, heading, speed, level, flapOffset, turnOffset, turnFrequency, swoopOffset);
        }

        private void AppendBird(VoxelChunk chunk, ChunkCacheKey cacheKey, ProceduralWorldMap worldMap, BirdInstance bird, float time)
        {
            var localX = WrapWorldCoordinate(bird.Position.X - cacheKey.StartX, _worldWidth);
            var localZ = WrapWorldCoordinate(bird.Position.Y - cacheKey.StartY, _worldHeight);
            var bodyCenter = new Vector3(localX * CubeSize, 0f, localZ * CubeSize);
            var forward = new Vector3(MathF.Cos(bird.Heading), 0f, MathF.Sin(bird.Heading));
            var right = new Vector3(-forward.Z, 0f, forward.X);
            var swoop = MathF.Sin((time * BirdSwoopRate) + bird.SwoopOffset) * BirdSwoopDepth;
            var terrainLevel = worldMap.SampleHeight(bird.Position.X, bird.Position.Y) * (worldMap.MaxCubeColumn - 1);
            var bodyBaseY = GetCubeBottom(MathF.Max(bird.Level + swoop, terrainLevel + BirdTerrainClearance), worldMap.MaxCubeColumn);
            var flap = MathF.Sin((time * 9f) + bird.FlapOffset);

            for (var bodyIndex = 0; bodyIndex < BirdBodyCubeCount; bodyIndex++)
            {
                var bodyOffset = (bodyIndex - ((BirdBodyCubeCount - 1) * 0.5f)) * (BirdCubeSize * 1.2f);
                var cubeCenter = bodyCenter + (forward * bodyOffset);
                AppendTreeCube(chunk, cubeCenter, bodyBaseY, BirdCubeSize, BirdCubeHeight, bodyIndex == BirdBodyCubeCount - 1 ? BirdHeadColor : BirdBodyColor);
            }

            var wingAnchor = bodyCenter - (forward * (BirdCubeSize * 0.35f));
            for (var wingIndex = 0; wingIndex < BirdWingCubeCount; wingIndex++)
            {
                var wingOffset = (wingIndex - ((BirdWingCubeCount - 1) * 0.5f)) * (BirdCubeSize * 1.1f);
                var normalizedSpan = MathF.Abs(wingOffset) / MathF.Max(BirdCubeSize, ((BirdWingCubeCount - 1) * BirdCubeSize * 0.55f));
                var wingLift = flap * normalizedSpan * (BirdCubeSize * 1.8f);
                var wingCenter = wingAnchor + (right * wingOffset);
                AppendTreeCube(chunk, wingCenter, bodyBaseY + wingLift, BirdCubeSize, BirdCubeHeight, BirdBodyColor);
            }
        }

        private struct BirdInstance
        {
            public BirdInstance(Vector2 position, float heading, float speed, float level, float flapOffset, float turnOffset, float turnFrequency, float swoopOffset)
            {
                Position = position;
                Heading = heading;
                Speed = speed;
                Level = level;
                FlapOffset = flapOffset;
                TurnOffset = turnOffset;
                TurnFrequency = turnFrequency;
                SwoopOffset = swoopOffset;
            }

            public Vector2 Position;
            public float Heading;
            public float Speed;
            public float Level;
            public float FlapOffset;
            public float TurnOffset;
            public float TurnFrequency;
            public float SwoopOffset;
        }
    }
}
