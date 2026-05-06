using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer : IDisposable
    {
        private const int ChunkSize = 16;
        private const int DefaultChunkHeight = 64;
        private const int MaxVisibleChunks = 324;
        private const float CubeSize = 3f;
        private const float CubeHeight = 2f;
        private const float FaceOverlap = 0.08f;
        private const float AmbientLight = 0.42f;
        private const float DiffuseLight = 0.58f;
        private const float ShadowStrength = 0.48f;
        private const int ShadowSampleDistance = 8;
        private const int ShadowSampleStride = 2;
        private const float WaterAnimationQuantizationStep = 0.05f;
        private const int ReducedDetailStride = 2;
        private const int NearDetailWaterRefreshBudget = 48;
        private const int WaterColorQuantizationLevels = 32;
        private const float WaterCullPadding = ChunkSize * 1.5f;
        private const float VenusBubbleMinHeightWorldUnits = 0f;
        private const float VenusBubblePeakHeightWorldUnits = 100f;
        private const float VenusBubbleDropDistanceWorldUnits = 100f;
        private const float VenusBubbleRadiusWorldUnits = 10.2f;
        private const int VenusBubbleColumnStride = 22;
        private const float VenusBubbleSpawnThreshold = 0.988f;
        private const float VenusBubbleCycleSpeed = 0.018f;
        private const int CloudCellStride = 4;
        private const float CloudLayerOffset = 11f;
        private const float CloudThickness = CubeHeight * 2f;
        private const float CloudCoverageThreshold = 0.66f;
        private const float CloudDriftSpeed = 12f;
        private const float MaxCameraPitch = MathHelper.PiOver2 - 0.01f;
        private const int VolcanoSmokeStride = 4;
        private const float VolcanoSmokeRiseRate = CubeHeight * 1.9f;
        private const byte ShoreLeftMask = 1 << 0;
        private const byte ShoreRightMask = 1 << 1;
        private const byte ShoreBackMask = 1 << 2;
        private const byte ShoreFrontMask = 1 << 3;
        private static readonly Color SoilColor = new Color(92, 68, 46);
        private static readonly Color WaterLowColor = new Color(12, 74, 86, 150);
        private static readonly Color WaterHighColor = new Color(56, 156, 150, 112);
        private static readonly Color[] WaterColorRamp = BuildWaterColorRamp();
        private static readonly Color VenusBubbleCoreColor = new Color(72, 196, 92, 126);
        private static readonly Color VenusBubbleGlowColor = new Color(124, 238, 138, 88);
        private static readonly Color CloudLowerLowColor = new Color(64, 68, 76, 18);
        private static readonly Color CloudLowerHighColor = new Color(92, 96, 104, 32);
        private static readonly Color CloudUpperLowColor = new Color(224, 228, 236, 92);
        private static readonly Color CloudUpperHighColor = new Color(255, 255, 255, 144);
        private static readonly Color VolcanoSmokeColor = new Color(78, 74, 70, 118);
        private static readonly Vector3 EarthFogLowColor = new Vector3(0.5294118f, 0.80784315f, 0.92156863f);
        private static readonly Vector3 EarthFogHighColor = new Vector3(0.03529412f, 0.10980392f, 0.25882354f);
        private static readonly Vector3 SpaceFogColor = Vector3.Zero;
        private static readonly Vector3 SunLightDirection = Vector3.Normalize(new Vector3(-0.52f, 0.74f, -0.42f));
        private static readonly Vector2 SunShadowDirection = Vector2.Normalize(new Vector2(SunLightDirection.X, SunLightDirection.Z));

        private readonly GraphicsDevice _graphicsDevice;
        private readonly int _worldWidth;
        private readonly int _worldHeight;
        private readonly BasicEffect _effect;
        private readonly VoxelChunk _emptyWaterChunk;
        private readonly VoxelChunk[] _chunks;
        private readonly VoxelChunk[] _waterChunks;
        private readonly VoxelChunk[] _cloudChunks;
        private readonly VoxelChunk[] _birdChunks;
        private readonly VoxelChunk[] _volcanoSmokeChunks;
        private Dictionary<ChunkCacheKey, VoxelChunk> _terrainChunkCache;
        private Dictionary<ChunkCacheKey, WaterChunkCacheEntry> _waterChunkCache;
        private Matrix[] _visibleChunkWorlds;
        private ChunkCacheKey[] _visibleChunkKeys;
        private int _activeWaterTimeBucket = int.MinValue;
        private int _nearDetailRefreshCursor;
        private float _truckCameraAvoidanceLift;
        private float _truckCameraAvoidanceVelocity;
        private bool _useGouraudShading;

        public HeightMapFlyoverRenderer(GraphicsDevice graphicsDevice, int worldWidth, int worldHeight)
        {
            _graphicsDevice = graphicsDevice;
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                TextureEnabled = false,
                FogEnabled = true
            };

            _emptyWaterChunk = new VoxelChunk();
            _chunks = new VoxelChunk[MaxVisibleChunks];
            _waterChunks = new VoxelChunk[MaxVisibleChunks];
            _cloudChunks = new VoxelChunk[MaxVisibleChunks];
            _birdChunks = new VoxelChunk[MaxVisibleChunks];
            _volcanoSmokeChunks = new VoxelChunk[MaxVisibleChunks];
            _terrainChunkCache = new Dictionary<ChunkCacheKey, VoxelChunk>();
            _waterChunkCache = new Dictionary<ChunkCacheKey, WaterChunkCacheEntry>();
            _visibleChunkWorlds = new Matrix[MaxVisibleChunks];
            _visibleChunkKeys = new ChunkCacheKey[MaxVisibleChunks];
            _shipChunk = new VoxelChunk();
            _shipParticleChunk = new VoxelChunk(); 
            _platformChunks = new VoxelChunk[MaxVisibleChunks];
            _platformSmokeChunks = new VoxelChunk[MaxVisibleChunks];
            for (var index = 0; index < _chunks.Length; index++)
            {
                _chunks[index] = new VoxelChunk();
                _waterChunks[index] = new VoxelChunk();
                _cloudChunks[index] = new VoxelChunk();
                _birdChunks[index] = new VoxelChunk();
                _volcanoSmokeChunks[index] = new VoxelChunk();
                _platformChunks[index] = new VoxelChunk();
                _platformSmokeChunks[index] = new VoxelChunk();
                _townDefenseChunks[index] = new VoxelChunk();
            }
        }

        public bool UseGouraudShading => _useGouraudShading;

        public void SetUseGouraudShading(bool useGouraudShading)
        {
            if (_useGouraudShading == useGouraudShading)
            {
                return;
            }

            _useGouraudShading = useGouraudShading;
            _terrainChunkCache?.Clear();
        }

        public void Render(ProceduralWorldMap worldMap, Vector2 cameraPosition, float heading, float pitch, float altitude, float maxFlightAltitude, float time, bool payloadReleased, IReadOnlyList<OilPlatformInstance> oilPlatforms, MissileWorldRenderState? missile, IReadOnlyList<MissileDebrisParticle> missileDebris)
        {
            Render(
                worldMap,
                new WorldViewState(
                    cameraPosition,
                    heading,
                    pitch,
                    altitude,
                    maxFlightAltitude,
                    true,
                    new TruckWorldRenderState(Vector2.Zero, 0f, 0f, 0f, 0f, false),
                    false,
                    CubeSize * 7.4f),
                time,
                payloadReleased,
                oilPlatforms,
                missile,
                missileDebris);
        }

        public void Render(ProceduralWorldMap worldMap, WorldViewState viewState, float time, bool payloadReleased, IReadOnlyList<OilPlatformInstance> oilPlatforms, MissileWorldRenderState? missile, IReadOnlyList<MissileDebrisParticle> missileDebris)
        {
            _graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, 1f, 0);
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            var cameraPosition = viewState.CameraPosition;
            var heading = float.IsFinite(viewState.Heading) ? WrapAngle(viewState.Heading) : 0f;
            var pitch = MathHelper.Clamp(float.IsFinite(viewState.Pitch) ? viewState.Pitch : 0f, -MaxCameraPitch, MaxCameraPitch);
            var altitude = float.IsFinite(viewState.Altitude) ? viewState.Altitude : 0.05f;
            var maxFlightAltitude = MathF.Max(0.05f, float.IsFinite(viewState.MaxFlightAltitude) ? viewState.MaxFlightAltitude : 0.05f);
            var altitudeRatio = viewState.UseTruckCamera
                ? 0f
                : MathHelper.Clamp((altitude - 0.05f) / MathF.Max(0.0001f, maxFlightAltitude - 0.05f), 0f, 1f);
            var pitchCos = MathF.Cos(pitch);
            var forward3 = SafeNormalize(new Vector3(MathF.Cos(heading) * pitchCos, MathF.Sin(pitch), MathF.Sin(heading) * pitchCos), Vector3.Forward);
            var forward = new Vector2(forward3.X, forward3.Z);
            if (forward.LengthSquared() < 0.0001f)
            {
                forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
            }
            else
            {
                forward.Normalize();
            }

            var right = new Vector2(-forward.Y, forward.X);
            var cameraEyeY = viewState.UseTruckCamera ? viewState.Truck.WorldY : MathHelper.Lerp(10f, 172f, altitudeRatio);
            var maxDistance = viewState.UseTruckCamera ? 190f : MathHelper.Lerp(180f, 360f, altitudeRatio);
            var viewWidth = viewState.UseTruckCamera ? 156f : MathHelper.Lerp(144f, 320f, altitudeRatio);
            var lookDistance = viewState.UseTruckCamera ? CubeSize * 3.8f : MathHelper.Lerp(92f, 192f, altitudeRatio) * CubeSize;
            var truckCameraDistance = viewState.TruckCameraDistance;
            var targetTruckCameraLift = viewState.UseTruckCamera
                ? ComputeTruckCameraLift(worldMap, viewState.Truck, forward3, pitch, truckCameraDistance)
                : 0f;
            var liftDelta = targetTruckCameraLift - _truckCameraAvoidanceLift;
            var liftAcceleration = liftDelta * 0.16f;
            _truckCameraAvoidanceVelocity = (_truckCameraAvoidanceVelocity * 0.78f) + liftAcceleration;
            _truckCameraAvoidanceLift += _truckCameraAvoidanceVelocity;
            if (MathF.Abs(liftDelta) < 0.01f && MathF.Abs(_truckCameraAvoidanceVelocity) < 0.01f)
            {
                _truckCameraAvoidanceLift = targetTruckCameraLift;
                _truckCameraAvoidanceVelocity = 0f;
            }
            var cameraEye = viewState.UseTruckCamera
                ? new Vector3(-(forward3.X * truckCameraDistance), cameraEyeY + _truckCameraAvoidanceLift - (MathF.Sin(pitch) * truckCameraDistance), -(forward3.Z * truckCameraDistance))
                : new Vector3(0f, cameraEyeY, 18f);
            var lookTarget = viewState.UseTruckCamera
                ? new Vector3(forward3.X * lookDistance, viewState.Truck.WorldY + CubeHeight * 1.5f, forward3.Z * lookDistance)
                : cameraEye + (forward3 * lookDistance);
            var cameraRight3 = Vector3.Cross(Vector3.Up, forward3);
            if (cameraRight3.LengthSquared() < 0.0001f)
            {
                cameraRight3 = Vector3.Cross(Vector3.Forward, forward3);
            }

            cameraRight3 = SafeNormalize(cameraRight3, Vector3.Right);
            var cameraUp = SafeNormalize(Vector3.Cross(forward3, cameraRight3), Vector3.Up);
            var view = Matrix.CreateLookAt(
                cameraEye,
                lookTarget,
                cameraUp);
            var projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(56f),
                Math.Max(0.1f, _graphicsDevice.Viewport.AspectRatio),
                1f,
                2000f);

            _effect.World = Matrix.Identity;
            _effect.View = view;
            _effect.Projection = projection;
            _effect.FogColor = worldMap.HasSurfaceWater
                ? Vector3.Lerp(EarthFogLowColor, EarthFogHighColor, altitudeRatio)
                : SpaceFogColor;
            var highAltitudeFogT = MathHelper.Clamp((altitudeRatio - 0.7f) / 0.3f, 0f, 1f);
            var fogStart = MathHelper.Lerp(220f, 300f, altitudeRatio) * 0.85f;
            var fogEnd = (maxDistance * MathHelper.Lerp(2.4f, 3.1f, altitudeRatio)) * 0.85f;
            _effect.FogStart = MathHelper.Lerp(fogStart, 140f * 0.85f, highAltitudeFogT);
            _effect.FogEnd = MathHelper.Lerp(fogEnd, maxDistance * 1.05f * 0.85f, highAltitudeFogT);

            if (worldMap.HasBirds)
            {
                EnsureBirdsInitialized(worldMap, cameraPosition, forward, right, maxDistance, viewWidth, time);
                UpdateBirds(worldMap, cameraPosition, forward, right, maxDistance, viewWidth, time);
            }
            _shipChunk.Reset();
            _shipParticleChunk.Reset();
            if (viewState.RenderShip)
            {
                EnsureShipInitialized(worldMap, cameraPosition, heading, time);
                UpdateShip(cameraPosition, heading, time, payloadReleased);
            }

            var downwardViewT = MathHelper.Clamp((-forward3.Y - 0.55f) / 0.4f, 0f, 1f) * MathHelper.Clamp((altitudeRatio - 0.82f) / 0.18f, 0f, 1f);
            var visibleChunkCount = BuildVisibleChunks(worldMap, cameraPosition, forward, right, maxDistance, viewWidth, time, downwardViewT);
            PopulateVisiblePlatformChunks(worldMap, oilPlatforms, visibleChunkCount, time);
            PopulateVisibleTownDefenseChunks(worldMap, cameraPosition, visibleChunkCount);
            PopulateVisibleVolcanoSmokeChunks(worldMap, visibleChunkCount, time);
            PopulateMissileEffects(cameraPosition, missile, missileDebris);
            if (viewState.RenderShip)
            {
                PopulateShipChunks(
                    cameraEye + (forward3 * ShipCameraDistance) + (cameraUp * ShipVerticalOffset) + (cameraRight3 * ShipHorizontalOffset),
                    cameraRight3,
                    cameraUp,
                    forward3);
            }

            PopulateTruckChunk(cameraPosition, viewState.Truck);

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                for (var index = 0; index < visibleChunkCount; index++)
                {
                    var chunk = _chunks[index];
                    if (chunk.VertexCount == 0)
                    {
                        continue;
                    }

                    _graphicsDevice.BlendState = BlendState.Opaque;
                    _graphicsDevice.DepthStencilState = DepthStencilState.Default;
                    _effect.World = _visibleChunkWorlds[index];
                    pass.Apply();

                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        chunk.Vertices,
                        0,
                        chunk.VertexCount / 3);
                }
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                for (var index = 0; index < visibleChunkCount; index++)
                {
                    var chunk = _waterChunks[index];
                    if (chunk.VertexCount == 0)
                    {
                        continue;
                    }

                    _graphicsDevice.BlendState = BlendState.AlphaBlend;
                    _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                    _graphicsDevice.RasterizerState = RasterizerState.CullNone;
                    _effect.World = _visibleChunkWorlds[index];
                    pass.Apply();

                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        chunk.Vertices,
                        0,
                        chunk.VertexCount / 3);
                }
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                for (var index = 0; index < visibleChunkCount; index++)
                {
                    var chunk = _volcanoSmokeChunks[index];
                    if (chunk.VertexCount == 0)
                    {
                        continue;
                    }

                    _graphicsDevice.BlendState = BlendState.AlphaBlend;
                    _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                    _effect.World = _visibleChunkWorlds[index];
                    pass.Apply();

                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        chunk.Vertices,
                        0,
                        chunk.VertexCount / 3);
                }
            }

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                for (var index = 0; index < visibleChunkCount; index++)
                {
                    var chunk = _townDefenseChunks[index];
                    if (chunk.VertexCount == 0)
                    {
                        continue;
                    }

                    _graphicsDevice.BlendState = BlendState.Opaque;
                    _graphicsDevice.DepthStencilState = DepthStencilState.Default;
                    _effect.World = _visibleChunkWorlds[index];
                    pass.Apply();

                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        chunk.Vertices,
                        0,
                        chunk.VertexCount / 3);
                }
            }

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                for (var index = 0; index < visibleChunkCount; index++)
                {
                    var chunk = _platformChunks[index];
                    if (chunk.VertexCount == 0)
                    {
                        continue;
                    }

                    _graphicsDevice.BlendState = BlendState.Opaque;
                    _graphicsDevice.DepthStencilState = DepthStencilState.Default;
                    _effect.World = _visibleChunkWorlds[index];
                    pass.Apply();

                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        chunk.Vertices,
                        0,
                        chunk.VertexCount / 3);
                }
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                for (var index = 0; index < visibleChunkCount; index++)
                {
                    var chunk = _platformSmokeChunks[index];
                    if (chunk.VertexCount == 0)
                    {
                        continue;
                    }

                    _graphicsDevice.BlendState = BlendState.AlphaBlend;
                    _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                    _effect.World = _visibleChunkWorlds[index];
                    pass.Apply();

                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        chunk.Vertices,
                        0,
                        chunk.VertexCount / 3);
                }
            }

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                for (var index = 0; index < visibleChunkCount; index++)
                {
                    var chunk = _cloudChunks[index];
                    if (chunk.VertexCount == 0)
                    {
                        continue;
                    }

                    _graphicsDevice.BlendState = BlendState.NonPremultiplied;
                    _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                    _effect.FogEnabled = false;
                    _effect.World = _visibleChunkWorlds[index];
                    pass.Apply();

                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        chunk.Vertices,
                        0,
                        chunk.VertexCount / 3);
                }
            }

            _effect.FogEnabled = true;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                for (var index = 0; index < visibleChunkCount; index++)
                {
                    var chunk = _birdChunks[index];
                    if (chunk.VertexCount == 0)
                    {
                        continue;
                    }

                    _graphicsDevice.BlendState = BlendState.Opaque;
                    _graphicsDevice.DepthStencilState = DepthStencilState.Default;
                    _effect.World = _visibleChunkWorlds[index];
                    pass.Apply();

                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        chunk.Vertices,
                        0,
                        chunk.VertexCount / 3);
                }
            }

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                if (_shipChunk.VertexCount == 0)
                {
                    continue;
                }

                _graphicsDevice.BlendState = BlendState.Opaque;
                _graphicsDevice.DepthStencilState = DepthStencilState.Default;
                _effect.World = Matrix.Identity;
                pass.Apply();

                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _shipChunk.Vertices,
                    0,
                    _shipChunk.VertexCount / 3);
            }

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                if (_truckChunk.VertexCount == 0)
                {
                    continue;
                }

                _graphicsDevice.BlendState = BlendState.Opaque;
                _graphicsDevice.DepthStencilState = DepthStencilState.Default;
                _effect.World = Matrix.Identity;
                pass.Apply();

                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _truckChunk.Vertices,
                    0,
                    _truckChunk.VertexCount / 3);
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                if (_shipParticleChunk.VertexCount == 0)
                {
                    continue;
                }

                _effect.World = Matrix.Identity;
                pass.Apply();

                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _shipParticleChunk.Vertices,
                    0,
                    _shipParticleChunk.VertexCount / 3);
            }

            DrawMissileEffects();
        }

        private float ComputeTruckCameraLift(ProceduralWorldMap worldMap, TruckWorldRenderState truck, Vector3 forward3, float pitch, float desiredDistance)
        {
            var lookTargetY = truck.WorldY + (CubeHeight * 1.5f);
            var steps = Math.Max(4, (int)MathF.Ceiling(desiredDistance / CubeSize));
            var requiredLift = 0f;
            for (var step = 1; step <= steps; step++)
            {
                var t = step / (float)steps;
                var sampleDistance = desiredDistance * t;
                var samplePosition = worldMap.WrapPosition(truck.Position - new Vector2(forward3.X, forward3.Z) * sampleDistance);
                var sampleY = truck.WorldY - (MathF.Sin(pitch) * sampleDistance);
                var terrainHeight = MathHelper.Max(worldMap.SampleHeight(samplePosition.X, samplePosition.Y), ProceduralWorldMap.SeaLevel);
                var terrainColumn = Math.Clamp((int)MathF.Round(terrainHeight * (worldMap.MaxCubeColumn - 1)), 1, worldMap.MaxCubeColumn - 1);
                var terrainOrigin = ProceduralWorldMap.SeaLevel * (worldMap.MaxCubeColumn - 1f);
                var terrainY = ((terrainColumn - terrainOrigin) * CubeHeight) + FaceOverlap;
                var clearanceY = MathHelper.Lerp(lookTargetY, sampleY, t);
                var blockedHeight = (terrainY + (CubeHeight * 0.35f)) - clearanceY;
                if (blockedHeight > 0f)
                {
                    requiredLift = MathF.Max(requiredLift, blockedHeight / MathF.Max(0.1f, t));
                }
            }

            return requiredLift;
        }

        public void Dispose()
        {
            _effect.Dispose();
        }

        private int BuildVisibleChunks(ProceduralWorldMap worldMap, Vector2 cameraPosition, Vector2 forward, Vector2 right, float maxDistance, float viewWidth, float time, float downwardViewT)
        {
            EnsureRuntimeCaches();
            var quantizedWaterTime = GetQuantizedWaterTime(time, out var waterTimeBucket);
            if (waterTimeBucket != _activeWaterTimeBucket)
            {
                _activeWaterTimeBucket = waterTimeBucket;
                _nearDetailRefreshCursor = 0;
            }

            var waterCullT = MathHelper.Clamp((maxDistance - 270f) / 90f, 0f, 1f);
            var waterMaxDistance = MathHelper.Lerp(maxDistance + ChunkSize, (maxDistance * 0.55f) + ChunkSize, waterCullT);
            var waterViewWidth = MathHelper.Lerp(viewWidth, viewWidth * 0.5f, waterCullT);
            var downwardCoverageRadius = MathHelper.Lerp(MathF.Max(viewWidth, maxDistance) * 0.9f, maxDistance + (viewWidth * 1.15f), downwardViewT);
            var terrainCoverageRadius = MathHelper.Lerp(maxDistance + viewWidth, downwardCoverageRadius * 1.35f, downwardViewT);
            var waterCoverageRadius = MathHelper.Lerp(waterMaxDistance + waterViewWidth, downwardCoverageRadius * 1.1f, downwardViewT);
            var chunkRadius = (int)MathF.Ceiling(terrainCoverageRadius / ChunkSize) + 2;
            var cameraChunkX = (int)MathF.Floor(cameraPosition.X / ChunkSize);
            var cameraChunkY = (int)MathF.Floor(cameraPosition.Y / ChunkSize);
            var candidates = new ChunkCandidate[MaxVisibleChunks];
            var candidateCount = 0;
            var nearDetailVisibleCount = 0;
            var halfWorldWidth = _worldWidth * 0.5f;
            var halfWorldHeight = _worldHeight * 0.5f;
            var downwardView = downwardViewT > 0f;
            var inverseMaxDistance = 1f / Math.Max(1f, maxDistance);
            var inverseWaterMaxDistance = 1f / Math.Max(1f, waterMaxDistance);

            for (var chunkY = cameraChunkY - chunkRadius; chunkY <= cameraChunkY + chunkRadius; chunkY++)
            {
                for (var chunkX = cameraChunkX - chunkRadius; chunkX <= cameraChunkX + chunkRadius; chunkX++)
                {
                    var chunkCenter = new Vector2((chunkX * ChunkSize) + (ChunkSize * 0.5f), (chunkY * ChunkSize) + (ChunkSize * 0.5f));
                    var offsetX = chunkCenter.X - cameraPosition.X;
                    if (offsetX > halfWorldWidth)
                    {
                        offsetX -= _worldWidth;
                    }
                    else if (offsetX < -halfWorldWidth)
                    {
                        offsetX += _worldWidth;
                    }

                    var offsetY = chunkCenter.Y - cameraPosition.Y;
                    if (offsetY > halfWorldHeight)
                    {
                        offsetY -= _worldHeight;
                    }
                    else if (offsetY < -halfWorldHeight)
                    {
                        offsetY += _worldHeight;
                    }

                    var forwardDistance = (offsetX * forward.X) + (offsetY * forward.Y);
                    var radialDistanceSquared = (offsetX * offsetX) + (offsetY * offsetY);
                    var terrainVisible = downwardView
                        ? radialDistanceSquared <= (terrainCoverageRadius + ChunkSize) * (terrainCoverageRadius + ChunkSize)
                        : forwardDistance >= -ChunkSize && forwardDistance <= maxDistance + ChunkSize;
                    if (!terrainVisible)
                    {
                        continue;
                    }

                    var depthT = MathHelper.Clamp(forwardDistance * inverseMaxDistance, 0f, 1f);
                    var lateralLimit = MathHelper.Lerp(ChunkSize * 4f, viewWidth, MathF.Pow(depthT, 0.82f));
                    var lateralDistance = MathF.Abs((offsetX * right.X) + (offsetY * right.Y));
                    var terrainWidthVisible = downwardView
                        ? radialDistanceSquared <= (terrainCoverageRadius + ChunkSize) * (terrainCoverageRadius + ChunkSize)
                        : lateralDistance <= lateralLimit + ChunkSize;
                    if (!terrainWidthVisible)
                    {
                        continue;
                    }

                    var waterDepthT = MathHelper.Clamp(forwardDistance * inverseWaterMaxDistance, 0f, 1f);
                    var waterLateralLimit = MathHelper.Lerp(ChunkSize * 4f, waterViewWidth, MathF.Pow(waterDepthT, 0.82f));
                    var waterVisible =
                        (downwardView
                            ? radialDistanceSquared <= (waterCoverageRadius + WaterCullPadding) * (waterCoverageRadius + WaterCullPadding)
                            : forwardDistance >= -WaterCullPadding &&
                              forwardDistance <= waterMaxDistance + WaterCullPadding &&
                              lateralDistance <= waterLateralLimit + WaterCullPadding);
                    var waterReducedDetail = waterVisible && waterCullT > 0f &&
                        (downwardView
                            ? radialDistanceSquared > (waterCoverageRadius * 0.58f) * (waterCoverageRadius * 0.58f)
                            : forwardDistance > waterMaxDistance * 0.58f || lateralDistance > waterViewWidth * 0.52f);

                    var candidateScore = downwardView
                        ? radialDistanceSquared
                        : forwardDistance + (lateralDistance * 0.35f);
                    InsertCandidate(candidates, ref candidateCount, chunkX, chunkY, candidateScore, waterVisible, waterReducedDetail);
                }
            }

            for (var index = 0; index < candidateCount; index++)
            {
                var candidate = candidates[index];
                var cacheKey = GetChunkCacheKey(candidate.ChunkX, candidate.ChunkY);
                _visibleChunkKeys[index] = cacheKey;
                _visibleChunkWorlds[index] = Matrix.CreateTranslation(GetChunkTranslation(cameraPosition, candidate.ChunkX, candidate.ChunkY));
                _chunks[index] = GetOrCreateTerrainChunk(worldMap, cacheKey);
            }

            for (var index = 0; index < candidateCount; index++)
            {
                var candidate = candidates[index];
                var cacheKey = _visibleChunkKeys[index];
                if (candidate.WaterVisible)
                {
                    var shouldRefreshNearDetailWater = candidate.WaterReducedDetail || (nearDetailVisibleCount >= _nearDetailRefreshCursor && nearDetailVisibleCount < _nearDetailRefreshCursor + NearDetailWaterRefreshBudget);
                    if (worldMap.HasSurfaceWater)
                    {
                        _waterChunks[index] = GetOrCreateWaterChunk(worldMap, cacheKey, quantizedTime: quantizedWaterTime, timeBucket: waterTimeBucket, reducedDetail: candidate.WaterReducedDetail, refreshOutdatedChunk: shouldRefreshNearDetailWater);
                        if (!candidate.WaterReducedDetail)
                        {
                            nearDetailVisibleCount++;
                        }
                    }
                    else
                    {
                        _waterChunks[index] = _emptyWaterChunk;
                    }
                }
                else
                {
                    _waterChunks[index] = _emptyWaterChunk;
                }

                if (_cloudChunks != null && worldMap.HasSurfaceWater)
                {
                    PopulateCloudChunk(_cloudChunks[index], cacheKey.StartX, cacheKey.StartY, worldMap, time);
                }
                else if (_cloudChunks != null)
                {
                    _cloudChunks[index].Reset();
                }

                if (_birdChunks != null && worldMap.HasBirds)
                {
                    PopulateBirdChunk(_birdChunks[index], cacheKey, worldMap, time);
                }
                else if (_birdChunks != null)
                {
                    _birdChunks[index].Reset();
                }

            }

            if (nearDetailVisibleCount > _nearDetailRefreshCursor)
            {
                _nearDetailRefreshCursor = Math.Min(_nearDetailRefreshCursor + NearDetailWaterRefreshBudget, nearDetailVisibleCount);
            }

            return candidateCount;
        }

        private void PopulateVisibleVolcanoSmokeChunks(ProceduralWorldMap worldMap, int visibleChunkCount, float time)
        {
            for (var index = 0; index < visibleChunkCount; index++)
            {
                PopulateVolcanoSmokeChunk(_volcanoSmokeChunks[index], _visibleChunkKeys[index], worldMap, time);
            }
        }

        private void PopulateVolcanoSmokeChunk(VoxelChunk smokeChunk, ChunkCacheKey cacheKey, ProceduralWorldMap worldMap, float time)
        {
            smokeChunk.Reset();
            for (var localZ = 0; localZ < ChunkSize; localZ += VolcanoSmokeStride)
            {
                for (var localX = 0; localX < ChunkSize; localX += VolcanoSmokeStride)
                {
                    var worldX = cacheKey.StartX + localX;
                    var worldY = cacheKey.StartY + localZ;
                    if (!worldMap.HasVolcanoVent(worldX, worldY))
                    {
                        continue;
                    }

                    AppendVolcanoSmoke(smokeChunk, worldMap, localX, localZ, worldX, worldY, time);
                }
            }
        }

        private void AppendVolcanoSmoke(VoxelChunk smokeChunk, ProceduralWorldMap worldMap, int localX, int localZ, int worldX, int worldY, float time)
        {
            var localPosition = GetChunkLocalPosition(localX + 0.5f, localZ + 0.5f);
            var ventHeight = GetCubeBottom(GetColumnHeight(worldMap.SampleVoxelHeight(worldX, worldY), worldMap.MaxCubeColumn), worldMap.MaxCubeColumn) + (CubeHeight * 0.9f);
            for (var smokeIndex = 0; smokeIndex < 4; smokeIndex++)
            {
                var smokeT = ((time * 0.28f) + (smokeIndex * 0.22f) + (worldX * 0.013f) + (worldY * 0.017f)) % 1f;
                if (smokeT < 0f)
                {
                    smokeT += 1f;
                }

                var smokeCenter = new Vector3(
                    localPosition.X + (MathF.Sin((time * 0.72f) + smokeIndex + (worldX * 0.05f)) * CubeSize * 0.3f),
                    ventHeight + (CubeHeight * 0.8f) + (smokeT * VolcanoSmokeRiseRate * 3.5f),
                    localPosition.Z + (MathF.Cos((time * 0.64f) + smokeIndex + (worldY * 0.05f)) * CubeSize * 0.3f));
                var smokeSize = MathHelper.Lerp(CubeSize * 0.58f, CubeSize * 0.26f, smokeT);
                var smokeAlpha = (byte)Math.Clamp((1f - smokeT) * VolcanoSmokeColor.A, 0f, 255f);
                AppendAxisAlignedCube(smokeChunk, smokeCenter, smokeSize, smokeSize * 1.35f, new Color(VolcanoSmokeColor.R, VolcanoSmokeColor.G, VolcanoSmokeColor.B, smokeAlpha));
            }
        }

        private void EnsureRuntimeCaches()
        {
            _terrainChunkCache ??= new Dictionary<ChunkCacheKey, VoxelChunk>();
            _waterChunkCache ??= new Dictionary<ChunkCacheKey, WaterChunkCacheEntry>();
            _visibleChunkWorlds ??= new Matrix[MaxVisibleChunks];
            _visibleChunkKeys ??= new ChunkCacheKey[MaxVisibleChunks];
        }

        private VoxelChunk GetOrCreateTerrainChunk(ProceduralWorldMap worldMap, ChunkCacheKey cacheKey)
        {
            if (_terrainChunkCache.TryGetValue(cacheKey, out var chunk))
            {
                return chunk;
            }

            chunk = BuildTerrainChunk(worldMap, cacheKey);
            _terrainChunkCache[cacheKey] = chunk;
            return chunk;
        }

        private VoxelChunk BuildTerrainChunk(ProceduralWorldMap worldMap, ChunkCacheKey cacheKey)
        {
            var chunk = new VoxelChunk();
            PopulateTerrainChunk(chunk, worldMap, cacheKey.StartX, cacheKey.StartY);
            return chunk;
        }

        private VoxelChunk GetOrCreateWaterChunk(ProceduralWorldMap worldMap, ChunkCacheKey cacheKey, float quantizedTime, int timeBucket, bool reducedDetail, bool refreshOutdatedChunk)
        {
            if (_waterChunkCache.TryGetValue(cacheKey, out var cacheEntry))
            {
                if (cacheEntry.TimeBucket == timeBucket && cacheEntry.ReducedDetail == reducedDetail)
                {
                    return cacheEntry.Chunk;
                }

                EnsureWaterStaticData(worldMap, cacheKey, cacheEntry);
                if (!refreshOutdatedChunk)
                {
                    return cacheEntry.Chunk;
                }

                PopulateWaterChunk(cacheEntry.Chunk, worldMap, cacheKey.StartX, cacheKey.StartY, quantizedTime, reducedDetail, cacheEntry.NearDetailSurfaceHeights, cacheEntry.NearDetailShoreMasks, cacheEntry.ReducedSurfaceHeights);
                cacheEntry.TimeBucket = timeBucket;
                cacheEntry.ReducedDetail = reducedDetail;
                return cacheEntry.Chunk;
            }

            var chunk = new VoxelChunk();
            var newCacheEntry = BuildWaterChunkCacheEntry(worldMap, cacheKey, chunk, timeBucket, reducedDetail);
            PopulateWaterChunk(newCacheEntry.Chunk, worldMap, cacheKey.StartX, cacheKey.StartY, quantizedTime, reducedDetail, newCacheEntry.NearDetailSurfaceHeights, newCacheEntry.NearDetailShoreMasks, newCacheEntry.ReducedSurfaceHeights);
            _waterChunkCache[cacheKey] = newCacheEntry;
            return chunk;
        }

        private WaterChunkCacheEntry BuildWaterChunkCacheEntry(ProceduralWorldMap worldMap, ChunkCacheKey cacheKey, VoxelChunk chunk, int timeBucket, bool reducedDetail)
        {
            BuildNearDetailWaterData(worldMap, cacheKey.StartX, cacheKey.StartY, out var nearDetailSurfaceHeights, out var nearDetailShoreMasks);
            var reducedSurfaceHeights = BuildReducedSurfaceHeights(nearDetailSurfaceHeights);
            return new WaterChunkCacheEntry(chunk, timeBucket, reducedDetail, nearDetailSurfaceHeights, nearDetailShoreMasks, reducedSurfaceHeights);
        }

        private void EnsureWaterStaticData(ProceduralWorldMap worldMap, ChunkCacheKey cacheKey, WaterChunkCacheEntry cacheEntry)
        {
            if (cacheEntry.NearDetailSurfaceHeights != null && cacheEntry.NearDetailShoreMasks != null && cacheEntry.ReducedSurfaceHeights != null)
            {
                return;
            }

            BuildNearDetailWaterData(worldMap, cacheKey.StartX, cacheKey.StartY, out var nearDetailSurfaceHeights, out var nearDetailShoreMasks);
            cacheEntry.NearDetailSurfaceHeights = nearDetailSurfaceHeights;
            cacheEntry.NearDetailShoreMasks = nearDetailShoreMasks;
            cacheEntry.ReducedSurfaceHeights = BuildReducedSurfaceHeights(nearDetailSurfaceHeights);
        }

        private void BuildNearDetailWaterData(ProceduralWorldMap worldMap, int chunkStartX, int chunkStartY, out float[] surfaceHeights, out byte[] shoreMasks)
        {
            var paddedHeights = new float[(ChunkSize + 2) * (ChunkSize + 2)];
            for (var sampleZ = -1; sampleZ <= ChunkSize; sampleZ++)
            {
                for (var sampleX = -1; sampleX <= ChunkSize; sampleX++)
                {
                    var worldX = WrapGridCoordinate(chunkStartX + sampleX, _worldWidth);
                    var worldY = WrapGridCoordinate(chunkStartY + sampleZ, _worldHeight);
                    paddedHeights[GetPaddedSampleIndex(sampleX + 1, sampleZ + 1)] = worldMap.SampleVoxelHeight(worldX, worldY);
                }
            }

            surfaceHeights = new float[ChunkSize * ChunkSize];
            shoreMasks = new byte[ChunkSize * ChunkSize];
            for (var localZ = 0; localZ < ChunkSize; localZ++)
            {
                for (var localX = 0; localX < ChunkSize; localX++)
                {
                    var sampleIndex = GetWaveSampleIndex(localX, localZ);
                    surfaceHeights[sampleIndex] = paddedHeights[GetPaddedSampleIndex(localX + 1, localZ + 1)];

                    var shoreMask = (byte)0;
                    if (paddedHeights[GetPaddedSampleIndex(localX, localZ + 1)] >= ProceduralWorldMap.SeaLevel)
                    {
                        shoreMask |= ShoreLeftMask;
                    }

                    if (paddedHeights[GetPaddedSampleIndex(localX + 2, localZ + 1)] >= ProceduralWorldMap.SeaLevel)
                    {
                        shoreMask |= ShoreRightMask;
                    }

                    if (paddedHeights[GetPaddedSampleIndex(localX + 1, localZ)] >= ProceduralWorldMap.SeaLevel)
                    {
                        shoreMask |= ShoreBackMask;
                    }

                    if (paddedHeights[GetPaddedSampleIndex(localX + 1, localZ + 2)] >= ProceduralWorldMap.SeaLevel)
                    {
                        shoreMask |= ShoreFrontMask;
                    }

                    shoreMasks[sampleIndex] = shoreMask;
                }
            }
        }

        private void AppendAxisAlignedCube(VoxelChunk chunk, Vector3 center, float cubeSize, float cubeHeight, Color color)
        {
            AppendTreeCube(chunk, center, center.Y - (cubeHeight * 0.5f), cubeSize, cubeHeight, color);
        }

        private float[] BuildReducedSurfaceHeights(float[] nearDetailSurfaceHeights)
        {
            var reducedGridSize = ChunkSize / ReducedDetailStride;
            var reducedSurfaceHeights = new float[reducedGridSize * reducedGridSize];
            for (var reducedZ = 0; reducedZ < reducedGridSize; reducedZ++)
            {
                for (var reducedX = 0; reducedX < reducedGridSize; reducedX++)
                {
                    var minimumSurfaceHeight = float.MaxValue;
                    var baseLocalX = reducedX * ReducedDetailStride;
                    var baseLocalZ = reducedZ * ReducedDetailStride;
                    for (var blockZ = 0; blockZ < ReducedDetailStride; blockZ++)
                    {
                        for (var blockX = 0; blockX < ReducedDetailStride; blockX++)
                        {
                            minimumSurfaceHeight = MathF.Min(minimumSurfaceHeight, nearDetailSurfaceHeights[GetWaveSampleIndex(baseLocalX + blockX, baseLocalZ + blockZ)]);
                        }
                    }

                    reducedSurfaceHeights[GetReducedWaveSampleIndex(reducedX, reducedZ)] = minimumSurfaceHeight;
                }
            }

            return reducedSurfaceHeights;
        }

        private void PopulateTerrainChunk(VoxelChunk chunk, ProceduralWorldMap worldMap, int chunkStartX, int chunkStartY)
        {
            chunk.Reset();

            for (var localZ = 0; localZ < ChunkSize; localZ++)
            {
                for (var localX = 0; localX < ChunkSize; localX++)
                {
                    var worldX = WrapWorldCoordinate(chunkStartX + localX + 0.5f, _worldWidth);
                    var worldY = WrapWorldCoordinate(chunkStartY + localZ + 0.5f, _worldHeight);
                    var localPosition = GetChunkLocalPosition(localX, localZ);

                    var surfaceHeight = worldMap.SampleVoxelHeight(worldX, worldY);
                    var columnHeight = GetColumnHeight(surfaceHeight, worldMap.MaxCubeColumn);
                    if (columnHeight <= 0)
                    {
                        continue;
                    }

                    var leftNeighbor = GetColumnHeight(worldMap.SampleVoxelHeight(worldX - 1f, worldY), worldMap.MaxCubeColumn);
                    var rightNeighbor = GetColumnHeight(worldMap.SampleVoxelHeight(worldX + 1f, worldY), worldMap.MaxCubeColumn);
                    var backNeighbor = GetColumnHeight(worldMap.SampleVoxelHeight(worldX, worldY - 1f), worldMap.MaxCubeColumn);
                    var frontNeighbor = GetColumnHeight(worldMap.SampleVoxelHeight(worldX, worldY + 1f), worldMap.MaxCubeColumn);
                    var topBlockHeight = MathF.Max(surfaceHeight, ProceduralWorldMap.SeaLevel + 0.025f);
                    var isShadowed = IsShadowed(worldMap, worldX, worldY, columnHeight);
                    var topBaseColor = worldMap.SampleSurfaceColor(worldX, worldY);
                    var topColor = ShadeFace(topBaseColor, Vector3.Up, isShadowed);
                    var leftColor = ShadeFace(SoilColor, Vector3.Left, isShadowed);
                    var rightColor = ShadeFace(SoilColor, Vector3.Right, isShadowed);
                    var backColor = ShadeFace(SoilColor, Vector3.Backward, isShadowed);
                    var frontColor = ShadeFace(SoilColor, Vector3.Forward, isShadowed);

                    if (_useGouraudShading)
                    {
                        var topCornerColors = GetTopCornerColors(worldMap, worldX, worldY, topBaseColor, isShadowed);
                        AppendTopFace(chunk, localPosition, columnHeight, worldMap.MaxCubeColumn, topCornerColors);
                        AppendSideFaces(chunk, localPosition, columnHeight, leftNeighbor, worldMap.MaxCubeColumn, Side.Left, GetSideFaceCornerColors(leftColor, topCornerColors[3], topCornerColors[0]));
                        AppendSideFaces(chunk, localPosition, columnHeight, rightNeighbor, worldMap.MaxCubeColumn, Side.Right, GetSideFaceCornerColors(rightColor, topCornerColors[1], topCornerColors[2]));
                        AppendSideFaces(chunk, localPosition, columnHeight, backNeighbor, worldMap.MaxCubeColumn, Side.Back, GetSideFaceCornerColors(backColor, topCornerColors[0], topCornerColors[1]));
                        AppendSideFaces(chunk, localPosition, columnHeight, frontNeighbor, worldMap.MaxCubeColumn, Side.Front, GetSideFaceCornerColors(frontColor, topCornerColors[2], topCornerColors[3]));
                    }
                    else
                    {
                        AppendTopFace(chunk, localPosition, columnHeight, worldMap.MaxCubeColumn, topColor);
                        AppendSideFaces(chunk, localPosition, columnHeight, leftNeighbor, worldMap.MaxCubeColumn, Side.Left, leftColor);
                        AppendSideFaces(chunk, localPosition, columnHeight, rightNeighbor, worldMap.MaxCubeColumn, Side.Right, rightColor);
                        AppendSideFaces(chunk, localPosition, columnHeight, backNeighbor, worldMap.MaxCubeColumn, Side.Back, backColor);
                        AppendSideFaces(chunk, localPosition, columnHeight, frontNeighbor, worldMap.MaxCubeColumn, Side.Front, frontColor);
                    }
                }
            }

            foreach (ProceduralWorldMap.TreeInstance tree in worldMap.GetTreesInChunk(chunkStartX, chunkStartY))
            {
                AppendTree(chunk, tree);
            }
        }

        private void PopulateWaterChunk(VoxelChunk waterChunk, ProceduralWorldMap worldMap, int chunkStartX, int chunkStartY, float time, bool reducedDetail, float[] nearDetailSurfaceHeights, byte[] nearDetailShoreMasks, float[] reducedSurfaceHeights)
        {
            waterChunk.Reset();
            var cellStride = reducedDetail ? ReducedDetailStride : 1;
            var waveSampleStride = reducedDetail ? ReducedDetailStride : 1;
            var waveGridWidth = reducedDetail ? (ChunkSize / ReducedDetailStride) : ChunkSize;
            var waterThickness = CubeHeight * 0.92f;
            var baseWaterY = GetCubeBottom(ProceduralWorldMap.SeaLevel * (DefaultChunkHeight - 1), DefaultChunkHeight);
            Span<int> wrappedSampleX = stackalloc int[ChunkSize + 2];
            Span<int> wrappedSampleY = stackalloc int[ChunkSize + 2];
            Span<float> waveHeights = reducedDetail
                ? stackalloc float[(ChunkSize / ReducedDetailStride) * (ChunkSize / ReducedDetailStride)]
                : stackalloc float[ChunkSize * ChunkSize];

            for (var sampleX = -1; sampleX <= ChunkSize; sampleX++)
            {
                wrappedSampleX[sampleX + 1] = WrapGridCoordinate(chunkStartX + sampleX, _worldWidth);
            }

            for (var sampleZ = -1; sampleZ <= ChunkSize; sampleZ++)
            {
                wrappedSampleY[sampleZ + 1] = WrapGridCoordinate(chunkStartY + sampleZ, _worldHeight);
            }

            if (!reducedDetail)
            {
                for (var sampleZ = 0; sampleZ < ChunkSize; sampleZ++)
                {
                    var wrappedWorldY = wrappedSampleY[sampleZ + 1] + 0.5f;
                    for (var sampleX = 0; sampleX < ChunkSize; sampleX++)
                    {
                        waveHeights[(sampleZ * waveGridWidth) + sampleX] = ComputeWaterWave(wrappedSampleX[sampleX + 1] + 0.5f, wrappedWorldY, time);
                    }
                }
            }
            else
            {
                for (var sampleZ = 0; sampleZ < ChunkSize; sampleZ += cellStride)
                {
                    for (var sampleX = 0; sampleX < ChunkSize; sampleX += cellStride)
                    {
                        var sampleWorldX = GetReducedDetailSampleCoordinate(wrappedSampleX, sampleX, cellStride, _worldWidth);
                        var sampleWorldY = GetReducedDetailSampleCoordinate(wrappedSampleY, sampleZ, cellStride, _worldHeight);
                        waveHeights[((sampleZ / cellStride) * waveGridWidth) + (sampleX / cellStride)] = ComputeWaterWave(sampleWorldX, sampleWorldY, time);
                    }
                }
            }

            for (var localZ = 0; localZ < ChunkSize; localZ += cellStride)
            {
                var waveSampleZ = localZ / waveSampleStride;
                for (var localX = 0; localX < ChunkSize; localX += cellStride)
                {
                    var blockWidth = Math.Min(cellStride, ChunkSize - localX);
                    var blockDepth = Math.Min(cellStride, ChunkSize - localZ);
                    var centerX = localX + (blockWidth * 0.5f);
                    var centerZ = localZ + (blockDepth * 0.5f);
                    var localPosition = GetChunkLocalPosition(centerX, centerZ);
                    var sampleIndex = (waveSampleZ * waveGridWidth) + (localX / waveSampleStride);
                    var waveHeight = waveHeights[sampleIndex];

                    if (reducedDetail)
                    {
                        AppendWaterCell(
                            waterChunk,
                            localPosition,
                            reducedSurfaceHeights![sampleIndex],
                            0,
                            waveHeight,
                            blockWidth,
                            blockDepth,
                            false,
                            DefaultChunkHeight,
                            baseWaterY,
                            waterThickness);
                        continue;
                    }

                    AppendWaterCell(
                        waterChunk,
                        localPosition,
                        nearDetailSurfaceHeights![sampleIndex],
                        nearDetailShoreMasks![sampleIndex],
                        waveHeight,
                        1,
                        1,
                        true,
                        DefaultChunkHeight,
                        baseWaterY,
                        waterThickness);
                }
            }

            if (worldMap.Theme == PlanetTheme.Venus)
            {
                AppendVenusBubbleColumns(waterChunk, worldMap, chunkStartX, chunkStartY, time, worldMap.MaxCubeColumn);
            }

        }

        private void AppendVenusBubbleColumns(VoxelChunk waterChunk, ProceduralWorldMap worldMap, int chunkStartX, int chunkStartY, float time, int chunkHeight)
        {
            for (var columnZ = 1; columnZ < ChunkSize; columnZ += VenusBubbleColumnStride)
            {
                for (var columnX = 1; columnX < ChunkSize; columnX += VenusBubbleColumnStride)
                {
                    var worldX = chunkStartX + columnX;
                    var worldY = chunkStartY + columnZ;
                    if (worldMap.SampleVoxelHeight(worldX + 0.5f, worldY + 0.5f) >= ProceduralWorldMap.SeaLevel)
                    {
                        continue;
                    }

                    var bubbleSeed = (MathF.Sin((worldX * 0.173f) + (worldY * 0.117f)) + 1f) * 0.5f;
                    if (bubbleSeed < VenusBubbleSpawnThreshold)
                    {
                        continue;
                    }

                    var cycleOffset = ((MathF.Sin((worldX * 0.081f) + (worldY * 0.067f)) + 1f) * 0.5f);
                    var cycle = ((time * VenusBubbleCycleSpeed) + cycleOffset) % 1f;
                    if (cycle < 0f)
                    {
                        cycle += 1f;
                    }

                    var riseT = cycle < 0.9f
                        ? cycle / 0.9f
                        : 1f - MathHelper.Clamp((cycle - 0.9f) / 0.1f, 0f, 1f);
                    var dropT = cycle < 0.9f
                        ? 0f
                        : MathHelper.Clamp((cycle - 0.9f) / 0.1f, 0f, 1f);
                    var baseWaterY = GetCubeBottom(ProceduralWorldMap.SeaLevel * (chunkHeight - 1), chunkHeight);
                    var centerY = baseWaterY + VenusBubbleMinHeightWorldUnits + (riseT * VenusBubblePeakHeightWorldUnits) - (dropT * VenusBubbleDropDistanceWorldUnits);
                    var pulse = 0.9f + (0.1f * MathF.Sin((time * 0.24f) + (worldX * 0.13f) + (worldY * 0.11f)));
                    var radius = VenusBubbleRadiusWorldUnits * pulse;
                    var localPosition = GetChunkLocalPosition(columnX + 0.5f, columnZ + 0.5f);
                    var bubbleCenter = new Vector3(localPosition.X, centerY, localPosition.Z);

                    AppendAxisAlignedCube(waterChunk, bubbleCenter, radius, radius * 0.88f, VenusBubbleCoreColor);
                    AppendAxisAlignedCube(waterChunk, bubbleCenter + new Vector3(0f, radius * 0.34f, 0f), radius * 0.68f, radius * 0.52f, VenusBubbleGlowColor);
                    AppendAxisAlignedCube(waterChunk, bubbleCenter + new Vector3(radius * 0.24f, -radius * 0.22f, -radius * 0.18f), radius * 0.44f, radius * 0.32f, VenusBubbleGlowColor);
                }
            }
        }

        private void PopulateCloudChunk(VoxelChunk cloudChunk, int chunkStartX, int chunkStartY, ProceduralWorldMap worldMap, float time)
        {
            cloudChunk.Reset();
            for (var localZ = 0; localZ < ChunkSize; localZ += CloudCellStride)
            {
                for (var localX = 0; localX < ChunkSize; localX += CloudCellStride)
                {
                    var blockWidth = Math.Min(CloudCellStride, ChunkSize - localX);
                    var blockDepth = Math.Min(CloudCellStride, ChunkSize - localZ);
                    var centerX = localX + ((blockWidth - 1) * 0.5f);
                    var centerZ = localZ + ((blockDepth - 1) * 0.5f);
                    var worldX = WrapWorldCoordinate(chunkStartX + centerX + 0.5f, _worldWidth);
                    var worldY = WrapWorldCoordinate(chunkStartY + centerZ + 0.5f, _worldHeight);
                    var density = SampleCloudDensity(worldX, worldY, time);
                    if (density <= CloudCoverageThreshold)
                    {
                        continue;
                    }

                    var densityT = MathHelper.Clamp((density - CloudCoverageThreshold) / (1f - CloudCoverageThreshold), 0f, 1f);
                    var lowerCloudColor = Color.Lerp(CloudLowerLowColor, CloudLowerHighColor, densityT);
                    var upperCloudColor = Color.Lerp(CloudUpperLowColor, CloudUpperHighColor, densityT);
                    var cloudHeight = GetCubeBottom((worldMap.MaxCubeColumn + CloudLayerOffset) + (ComputeCloudHeightOffset(worldX, worldY) * 3f), worldMap.MaxCubeColumn);
                    AppendCloudVolume(cloudChunk, GetChunkLocalPosition(centerX, centerZ), blockWidth, blockDepth, cloudHeight, lowerCloudColor, upperCloudColor);
                }
            }
        }

        private static int GetWaveSampleIndex(int sampleX, int sampleZ)
        {
            return (sampleZ * ChunkSize) + sampleX;
        }

        private static int GetPaddedSampleIndex(int sampleX, int sampleZ)
        {
            return (sampleZ * (ChunkSize + 2)) + sampleX;
        }

        private static int GetReducedWaveSampleIndex(int sampleX, int sampleZ)
        {
            return (sampleZ * (ChunkSize / ReducedDetailStride)) + sampleX;
        }

        private static float GetReducedDetailSampleCoordinate(ReadOnlySpan<int> wrappedSamples, int localIndex, int cellStride, int size)
        {
            var startCoordinate = wrappedSamples[localIndex + 1];
            var sampleOffset = (cellStride * 0.5f) - 0.5f;
            return WrapWorldCoordinate(startCoordinate + sampleOffset, size);
        }

        private static Vector3 GetChunkLocalPosition(float localX, float localZ)
        {
            return new Vector3(localX * CubeSize, 0f, localZ * CubeSize);
        }

        private void AppendCloudVolume(VoxelChunk chunk, Vector3 localPosition, int cellWidth, int cellDepth, float y, Color lowerColor, Color upperColor)
        {
            var layerGap = CubeHeight * 0.08f;
            var layerHeight = (CloudThickness - layerGap) * 0.5f;
            var layerOffset = (layerHeight + layerGap) * 0.5f;
            AppendCloudLayerVolume(chunk, localPosition, cellWidth, cellDepth, y - layerOffset, layerHeight, lowerColor);
            AppendCloudLayerVolume(chunk, localPosition, cellWidth, cellDepth, y + layerOffset, layerHeight, upperColor);
        }

        private void AppendCloudLayerVolume(VoxelChunk chunk, Vector3 localPosition, int cellWidth, int cellDepth, float y, float height, Color color)
        {
            var halfX = (CubeSize * cellWidth * 0.56f) + FaceOverlap;
            var halfZ = (CubeSize * cellDepth * 0.56f) + FaceOverlap;
            var topY = y + (height * 0.5f);
            var bottomY = y - (height * 0.5f);
            var undersideColor = new Color(color.R, color.G, color.B, (byte)Math.Max(18, color.A / 2));

            var topV0 = new Vector3(localPosition.X - halfX, topY, localPosition.Z - halfZ);
            var topV1 = new Vector3(localPosition.X + halfX, topY, localPosition.Z - halfZ);
            var topV2 = new Vector3(localPosition.X + halfX, topY, localPosition.Z + halfZ);
            var topV3 = new Vector3(localPosition.X - halfX, topY, localPosition.Z + halfZ);
            AppendQuad(chunk, topV0, topV1, topV2, topV3, color);

            var bottomV0 = new Vector3(localPosition.X - halfX, bottomY, localPosition.Z + halfZ);
            var bottomV1 = new Vector3(localPosition.X + halfX, bottomY, localPosition.Z + halfZ);
            var bottomV2 = new Vector3(localPosition.X + halfX, bottomY, localPosition.Z - halfZ);
            var bottomV3 = new Vector3(localPosition.X - halfX, bottomY, localPosition.Z - halfZ);
            AppendQuad(chunk, bottomV0, bottomV1, bottomV2, bottomV3, undersideColor);

            AppendQuad(chunk, topV3, topV2, bottomV1, bottomV0, undersideColor);
            AppendQuad(chunk, topV1, topV0, bottomV3, bottomV2, undersideColor);
            AppendQuad(chunk, topV0, topV3, bottomV0, bottomV3, undersideColor);
            AppendQuad(chunk, topV2, topV1, bottomV2, bottomV1, undersideColor);
        }

        private static float SampleCloudDensity(float worldX, float worldY, float time)
        {
            var windOffset = GetCloudWindOffset(time);
            var sampleX = worldX + windOffset.X;
            var sampleY = worldY + windOffset.Y;
            var broad = (MathF.Sin((sampleX * 0.013f) + (sampleY * 0.009f)) + 1f) * 0.5f;
            var streaks = (MathF.Cos((sampleX * 0.021f) - (sampleY * 0.017f)) + 1f) * 0.5f;
            var puffs = (MathF.Sin((sampleX * 0.031f) + (sampleY * 0.028f) + 1.7f) + 1f) * 0.5f;
            return (broad * 0.52f) + (streaks * 0.33f) + (puffs * 0.15f);
        }

        private static Vector2 GetCloudWindOffset(float time)
        {
            var windAngle = (MathF.Sin(time * 0.019f) * 1.15f) + (MathF.Cos(time * 0.011f) * 0.75f);
            var windStrength = 0.9f + (0.35f * MathF.Sin(time * 0.007f));
            return new Vector2(MathF.Cos(windAngle), MathF.Sin(windAngle)) * (time * CloudDriftSpeed * windStrength);
        }

        private static float ComputeCloudHeightOffset(float worldX, float worldY)
        {
            return (MathF.Sin((worldX * 0.017f) - (worldY * 0.013f)) + 1f) * 0.5f;
        }

        private ChunkCacheKey GetChunkCacheKey(int chunkX, int chunkY)
        {
            return new ChunkCacheKey(WrapChunkStart(chunkX, _worldWidth), WrapChunkStart(chunkY, _worldHeight));
        }

        private Vector3 GetChunkTranslation(Vector2 cameraPosition, int chunkX, int chunkY)
        {
            var chunkOrigin = new Vector2(chunkX * ChunkSize, chunkY * ChunkSize);
            var wrappedOffset = GetWrappedOffset(chunkOrigin - cameraPosition);
            return new Vector3(wrappedOffset.X * CubeSize, 0f, wrappedOffset.Y * CubeSize);
        }

        private static int WrapChunkStart(int chunkCoordinate, int size)
        {
            var wrapped = (chunkCoordinate * ChunkSize) % size;
            return wrapped < 0 ? wrapped + size : wrapped;
        }

        private static int WrapGridCoordinate(int value, int size)
        {
            var wrapped = value % size;
            return wrapped < 0 ? wrapped + size : wrapped;
        }

        private static float GetQuantizedWaterTime(float time, out int timeBucket)
        {
            timeBucket = (int)MathF.Floor(time / WaterAnimationQuantizationStep);
            return timeBucket * WaterAnimationQuantizationStep;
        }

        private static int GetColumnHeight(float surfaceHeight, int chunkHeight = DefaultChunkHeight)
        {
            return Math.Clamp((int)MathF.Round(surfaceHeight * (chunkHeight - 1)), 1, chunkHeight - 1);
        }

        private static float GetCubeBottom(int level, int chunkHeight = DefaultChunkHeight)
        {
            return (level - GetVerticalOrigin(chunkHeight)) * CubeHeight;
        }

        private static float GetCubeBottom(float level, int chunkHeight = DefaultChunkHeight)
        {
            return (level - GetVerticalOrigin(chunkHeight)) * CubeHeight;
        }

        private static float GetVerticalOrigin(int chunkHeight)
        {
            return ProceduralWorldMap.SeaLevel * (chunkHeight - 1);
        }

        private void AppendTopFace(VoxelChunk chunk, Vector3 localPosition, int columnHeight, int chunkHeight, Color color)
        {
            var half = (CubeSize * 0.5f) + FaceOverlap;
            var topY = GetCubeBottom(columnHeight, chunkHeight) + FaceOverlap;
            var v0 = new Vector3(localPosition.X - half, topY, localPosition.Z - half);
            var v1 = new Vector3(localPosition.X + half, topY, localPosition.Z - half);
            var v2 = new Vector3(localPosition.X + half, topY, localPosition.Z + half);
            var v3 = new Vector3(localPosition.X - half, topY, localPosition.Z + half);
            AppendQuad(chunk, v0, v1, v2, v3, color);
        }

        private void AppendTopFace(VoxelChunk chunk, Vector3 localPosition, int columnHeight, int chunkHeight, ReadOnlySpan<Color> cornerColors)
        {
            var half = (CubeSize * 0.5f) + FaceOverlap;
            var topY = GetCubeBottom(columnHeight, chunkHeight) + FaceOverlap;
            var v0 = new Vector3(localPosition.X - half, topY, localPosition.Z - half);
            var v1 = new Vector3(localPosition.X + half, topY, localPosition.Z - half);
            var v2 = new Vector3(localPosition.X + half, topY, localPosition.Z + half);
            var v3 = new Vector3(localPosition.X - half, topY, localPosition.Z + half);
            AppendQuad(chunk, v0, cornerColors[0], v1, cornerColors[1], v2, cornerColors[2], v3, cornerColors[3]);
        }

        private void AppendSideFaces(VoxelChunk chunk, Vector3 localPosition, int columnHeight, int neighborHeight, int chunkHeight, Side side, Color color)
        {
            if (neighborHeight >= columnHeight)
            {
                return;
            }

            var startLevel = Math.Max(0, neighborHeight);
            AppendColumnSide(chunk, localPosition, startLevel, columnHeight, chunkHeight, side, color);
        }

        private void AppendSideFaces(VoxelChunk chunk, Vector3 localPosition, int columnHeight, int neighborHeight, int chunkHeight, Side side, ReadOnlySpan<Color> cornerColors)
        {
            if (neighborHeight >= columnHeight)
            {
                return;
            }

            var startLevel = Math.Max(0, neighborHeight);
            AppendColumnSide(chunk, localPosition, startLevel, columnHeight, chunkHeight, side, cornerColors);
        }

        private void AppendColumnSide(VoxelChunk chunk, Vector3 localPosition, int startLevel, int endLevel, int chunkHeight, Side side, Color color)
        {
            var half = (CubeSize * 0.5f) + FaceOverlap;
            var bottomY = GetCubeBottom(startLevel, chunkHeight) - FaceOverlap;
            var topY = GetCubeBottom(endLevel, chunkHeight) + FaceOverlap;

            Vector3 v0;
            Vector3 v1;
            Vector3 v2;
            Vector3 v3;

            switch (side)
            {
                case Side.Left:
                    v0 = new Vector3(localPosition.X - half, topY, localPosition.Z + half);
                    v1 = new Vector3(localPosition.X - half, topY, localPosition.Z - half);
                    v2 = new Vector3(localPosition.X - half, bottomY, localPosition.Z - half);
                    v3 = new Vector3(localPosition.X - half, bottomY, localPosition.Z + half);
                    break;
                case Side.Right:
                    v0 = new Vector3(localPosition.X + half, topY, localPosition.Z - half);
                    v1 = new Vector3(localPosition.X + half, topY, localPosition.Z + half);
                    v2 = new Vector3(localPosition.X + half, bottomY, localPosition.Z + half);
                    v3 = new Vector3(localPosition.X + half, bottomY, localPosition.Z - half);
                    break;
                case Side.Back:
                    v0 = new Vector3(localPosition.X + half, topY, localPosition.Z - half);
                    v1 = new Vector3(localPosition.X - half, topY, localPosition.Z - half);
                    v2 = new Vector3(localPosition.X - half, bottomY, localPosition.Z - half);
                    v3 = new Vector3(localPosition.X + half, bottomY, localPosition.Z - half);
                    break;
                default:
                    v0 = new Vector3(localPosition.X - half, topY, localPosition.Z + half);
                    v1 = new Vector3(localPosition.X + half, topY, localPosition.Z + half);
                    v2 = new Vector3(localPosition.X + half, bottomY, localPosition.Z + half);
                    v3 = new Vector3(localPosition.X - half, bottomY, localPosition.Z + half);
                    break;
            }

            AppendQuad(chunk, v0, v1, v2, v3, color);
        }

        private void AppendColumnSide(VoxelChunk chunk, Vector3 localPosition, int startLevel, int endLevel, int chunkHeight, Side side, ReadOnlySpan<Color> cornerColors)
        {
            var half = (CubeSize * 0.5f) + FaceOverlap;
            var bottomY = GetCubeBottom(startLevel, chunkHeight) - FaceOverlap;
            var topY = GetCubeBottom(endLevel, chunkHeight) + FaceOverlap;

            Vector3 v0;
            Vector3 v1;
            Vector3 v2;
            Vector3 v3;

            switch (side)
            {
                case Side.Left:
                    v0 = new Vector3(localPosition.X - half, topY, localPosition.Z + half);
                    v1 = new Vector3(localPosition.X - half, topY, localPosition.Z - half);
                    v2 = new Vector3(localPosition.X - half, bottomY, localPosition.Z - half);
                    v3 = new Vector3(localPosition.X - half, bottomY, localPosition.Z + half);
                    break;
                case Side.Right:
                    v0 = new Vector3(localPosition.X + half, topY, localPosition.Z - half);
                    v1 = new Vector3(localPosition.X + half, topY, localPosition.Z + half);
                    v2 = new Vector3(localPosition.X + half, bottomY, localPosition.Z + half);
                    v3 = new Vector3(localPosition.X + half, bottomY, localPosition.Z - half);
                    break;
                case Side.Back:
                    v0 = new Vector3(localPosition.X + half, topY, localPosition.Z - half);
                    v1 = new Vector3(localPosition.X - half, topY, localPosition.Z - half);
                    v2 = new Vector3(localPosition.X - half, bottomY, localPosition.Z - half);
                    v3 = new Vector3(localPosition.X + half, bottomY, localPosition.Z - half);
                    break;
                default:
                    v0 = new Vector3(localPosition.X - half, topY, localPosition.Z + half);
                    v1 = new Vector3(localPosition.X + half, topY, localPosition.Z + half);
                    v2 = new Vector3(localPosition.X + half, bottomY, localPosition.Z + half);
                    v3 = new Vector3(localPosition.X - half, bottomY, localPosition.Z + half);
                    break;
            }

            AppendQuad(chunk, v0, cornerColors[0], v1, cornerColors[1], v2, cornerColors[2], v3, cornerColors[3]);
        }

        private static void AppendQuad(VoxelChunk chunk, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color color)
        {
            AppendSafeTriangle(chunk, v0, v1, v2, color);
            AppendSafeTriangle(chunk, v0, v2, v3, color);
        }

        private static void AppendQuad(VoxelChunk chunk, Vector3 v0, Color c0, Vector3 v1, Color c1, Vector3 v2, Color c2, Vector3 v3, Color c3)
        {
            AppendSafeTriangle(chunk, v0, c0, v1, c1, v2, c2);
            AppendSafeTriangle(chunk, v0, c0, v2, c2, v3, c3);
        }

        private static void AppendSafeTriangle(VoxelChunk chunk, Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c) || IsDegenerateTriangle(a, b, c))
            {
                return;
            }

            chunk.AppendTriangle(a, b, c, color);
        }

        private static void AppendSafeTriangle(VoxelChunk chunk, Vector3 a, Color colorA, Vector3 b, Color colorB, Vector3 c, Color colorC)
        {
            if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c) || IsDegenerateTriangle(a, b, c))
            {
                return;
            }

            chunk.AppendTriangle(a, colorA, b, colorB, c, colorC);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }

        private static bool IsDegenerateTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;
            return Vector3.Cross(ab, ac).LengthSquared() < 0.000001f;
        }

        private Color[] GetTopCornerColors(ProceduralWorldMap worldMap, float worldX, float worldY, Color baseColor, bool isShadowed)
        {
            return
            [
                ShadeFace(baseColor, ComputeTerrainNormal(worldMap, worldX - 0.5f, worldY - 0.5f), isShadowed),
                ShadeFace(baseColor, ComputeTerrainNormal(worldMap, worldX + 0.5f, worldY - 0.5f), isShadowed),
                ShadeFace(baseColor, ComputeTerrainNormal(worldMap, worldX + 0.5f, worldY + 0.5f), isShadowed),
                ShadeFace(baseColor, ComputeTerrainNormal(worldMap, worldX - 0.5f, worldY + 0.5f), isShadowed)
            ];
        }

        private static Color[] GetSideFaceCornerColors(Color faceColor, Color topLeftColor, Color topRightColor)
        {
            return
            [
                Color.Lerp(faceColor, topLeftColor, 0.72f),
                Color.Lerp(faceColor, topRightColor, 0.72f),
                faceColor,
                faceColor
            ];
        }

        private static Vector3 ComputeTerrainNormal(ProceduralWorldMap worldMap, float worldX, float worldY)
        {
            var left = worldMap.SampleVoxelHeight(worldX - 1f, worldY);
            var right = worldMap.SampleVoxelHeight(worldX + 1f, worldY);
            var back = worldMap.SampleVoxelHeight(worldX, worldY - 1f);
            var front = worldMap.SampleVoxelHeight(worldX, worldY + 1f);
            var xSlope = (right - left) * CubeHeight;
            var zSlope = (front - back) * CubeHeight;
            return SafeNormalize(new Vector3(-xSlope, CubeSize * 2f, -zSlope), Vector3.Up);
        }

        private void AppendWaterCell(VoxelChunk chunk, Vector3 localPosition, float surfaceHeight, byte shoreMask, float waveHeight, int cellWidth, int cellDepth, bool renderSides, int chunkHeight, float baseWaterY, float waterThickness)
        {
            if (surfaceHeight >= ProceduralWorldMap.SeaLevel)
            {
                return;
            }

            var halfX = (CubeSize * cellWidth * 0.5f) + FaceOverlap;
            var halfZ = (CubeSize * cellDepth * 0.5f) + FaceOverlap;
            var topY = baseWaterY + waveHeight + FaceOverlap;
            var bottomY = topY - waterThickness;
            var waveColorT = MathHelper.Clamp((waveHeight / (CubeHeight * 0.42f) * 0.5f) + 0.5f, 0f, 1f);
            var waterColorIndex = Math.Clamp((int)MathF.Round(waveColorT * (WaterColorQuantizationLevels - 1)), 0, WaterColorQuantizationLevels - 1);
            var waterColor = WaterColorRamp[waterColorIndex];

            var v0 = new Vector3(localPosition.X - halfX, topY, localPosition.Z - halfZ);
            var v1 = new Vector3(localPosition.X + halfX, topY, localPosition.Z - halfZ);
            var v2 = new Vector3(localPosition.X + halfX, topY, localPosition.Z + halfZ);
            var v3 = new Vector3(localPosition.X - halfX, topY, localPosition.Z + halfZ);
            AppendQuad(chunk, v0, v1, v2, v3, waterColor);

            if (!renderSides)
            {
                return;
            }

            if ((shoreMask & ShoreLeftMask) != 0)
            {
                AppendWaterSide(chunk, localPosition, bottomY, topY, Side.Left, waterColor, halfX, halfZ);
            }

            if ((shoreMask & ShoreRightMask) != 0)
            {
                AppendWaterSide(chunk, localPosition, bottomY, topY, Side.Right, waterColor, halfX, halfZ);
            }

            if ((shoreMask & ShoreBackMask) != 0)
            {
                AppendWaterSide(chunk, localPosition, bottomY, topY, Side.Back, waterColor, halfX, halfZ);
            }

            if ((shoreMask & ShoreFrontMask) != 0)
            {
                AppendWaterSide(chunk, localPosition, bottomY, topY, Side.Front, waterColor, halfX, halfZ);
            }
        }

        private void AppendWaterSide(VoxelChunk chunk, Vector3 localPosition, float bottomY, float topY, Side side, Color color, float halfX, float halfZ)
        {
            Vector3 v0;
            Vector3 v1;
            Vector3 v2;
            Vector3 v3;

            switch (side)
            {
                case Side.Left:
                    v0 = new Vector3(localPosition.X - halfX, topY, localPosition.Z + halfZ);
                    v1 = new Vector3(localPosition.X - halfX, topY, localPosition.Z - halfZ);
                    v2 = new Vector3(localPosition.X - halfX, bottomY, localPosition.Z - halfZ);
                    v3 = new Vector3(localPosition.X - halfX, bottomY, localPosition.Z + halfZ);
                    break;
                case Side.Right:
                    v0 = new Vector3(localPosition.X + halfX, topY, localPosition.Z - halfZ);
                    v1 = new Vector3(localPosition.X + halfX, topY, localPosition.Z + halfZ);
                    v2 = new Vector3(localPosition.X + halfX, bottomY, localPosition.Z + halfZ);
                    v3 = new Vector3(localPosition.X + halfX, bottomY, localPosition.Z - halfZ);
                    break;
                case Side.Back:
                    v0 = new Vector3(localPosition.X + halfX, topY, localPosition.Z - halfZ);
                    v1 = new Vector3(localPosition.X - halfX, topY, localPosition.Z - halfZ);
                    v2 = new Vector3(localPosition.X - halfX, bottomY, localPosition.Z - halfZ);
                    v3 = new Vector3(localPosition.X + halfX, bottomY, localPosition.Z - halfZ);
                    break;
                default:
                    v0 = new Vector3(localPosition.X - halfX, topY, localPosition.Z + halfZ);
                    v1 = new Vector3(localPosition.X + halfX, topY, localPosition.Z + halfZ);
                    v2 = new Vector3(localPosition.X + halfX, bottomY, localPosition.Z + halfZ);
                    v3 = new Vector3(localPosition.X - halfX, bottomY, localPosition.Z + halfZ);
                    break;
            }

            AppendQuad(chunk, v0, v1, v2, v3, color);
        }

        private float ComputeWaterWave(float worldX, float worldY, float time)
        {
            return ComputeWaterWaveCore(worldX, worldY, time);
        }

        private static float ComputeWaterWaveCore(float worldX, float worldY, float time)
        {
            var primaryTravel = MathF.Sin(((worldX * 0.065f) + (worldY * 0.022f)) - (time * 0.92f));
            var secondaryTravel = MathF.Sin(((worldX * 0.028f) - (worldY * 0.054f)) - (time * 0.56f));
            var swell = MathF.Cos(((worldX + worldY) * 0.018f) - (time * 0.34f));
            var chop = MathF.Sin(((worldX * 0.11f) - (worldY * 0.08f)) - (time * 1.28f));
            var wave =
                (primaryTravel * 0.42f) +
                (secondaryTravel * 0.24f) +
                (swell * 0.18f) +
                (chop * 0.16f);
            return wave * (CubeHeight * 0.72f);
        }

        private static Color[] BuildWaterColorRamp()
        {
            var colors = new Color[WaterColorQuantizationLevels];
            for (var index = 0; index < colors.Length; index++)
            {
                var t = index / (float)(colors.Length - 1);
                colors[index] = Color.Lerp(WaterLowColor, WaterHighColor, t);
            }

            return colors;
        }

        private static Color ShadeFace(Color baseColor, Vector3 normal, bool isShadowed)
        {
            var diffuse = MathHelper.Clamp(Vector3.Dot(normal, SunLightDirection), 0f, 1f);
            var brightness = AmbientLight + (DiffuseLight * diffuse);
            if (isShadowed)
            {
                brightness *= 1f - ShadowStrength;
            }

            return MultiplyColor(baseColor, brightness);
        }

        private static bool IsShadowed(ProceduralWorldMap worldMap, float worldX, float worldY, int columnHeight)
        {
            var verticalStep = SunLightDirection.Y / MathF.Max(0.001f, SunShadowDirection.Length());
            for (var step = 1; step <= ShadowSampleDistance; step += ShadowSampleStride)
            {
                var sampleX = worldX + (SunShadowDirection.X * step);
                var sampleY = worldY + (SunShadowDirection.Y * step);
                var sampleHeight = GetColumnHeight(worldMap.SampleVoxelHeight(sampleX, sampleY), worldMap.MaxCubeColumn);
                var lightHeight = columnHeight + (verticalStep * step);
                if (sampleHeight > lightHeight)
                {
                    return true;
                }
            }

            return false;
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

        private void InsertCandidate(ChunkCandidate[] candidates, ref int candidateCount, int chunkX, int chunkY, float score, bool waterVisible, bool waterReducedDetail)
        {
            var insertIndex = candidateCount;
            if (candidateCount < candidates.Length)
            {
                candidateCount++;
            }
            else if (score >= candidates[candidates.Length - 1].Score)
            {
                return;
            }
            else
            {
                insertIndex = candidates.Length - 1;
            }

            while (insertIndex > 0 && candidates[insertIndex - 1].Score > score)
            {
                if (insertIndex < candidates.Length)
                {
                    candidates[insertIndex] = candidates[insertIndex - 1];
                }

                insertIndex--;
            }

            candidates[insertIndex] = new ChunkCandidate(chunkX, chunkY, score, waterVisible, waterReducedDetail);
        }

        private Vector2 GetWrappedOffset(Vector2 offset)
        {
            if (offset.X > _worldWidth * 0.5f)
            {
                offset.X -= _worldWidth;
            }
            else if (offset.X < -_worldWidth * 0.5f)
            {
                offset.X += _worldWidth;
            }

            if (offset.Y > _worldHeight * 0.5f)
            {
                offset.Y -= _worldHeight;
            }
            else if (offset.Y < -_worldHeight * 0.5f)
            {
                offset.Y += _worldHeight;
            }

            return offset;
        }

        private static float WrapWorldCoordinate(float value, int size)
        {
            var wrapped = value % size;
            return wrapped < 0f ? wrapped + size : wrapped;
        }

        private static float WrapAngle(float angle)
        {
            while (angle > MathF.PI)
            {
                angle -= MathHelper.TwoPi;
            }

            while (angle < -MathF.PI)
            {
                angle += MathHelper.TwoPi;
            }

            return angle;
        }

        private static Vector3 SafeNormalize(Vector3 vector, Vector3 fallback)
        {
            if (!float.IsFinite(vector.X) || !float.IsFinite(vector.Y) || !float.IsFinite(vector.Z))
            {
                return fallback;
            }

            var lengthSquared = vector.LengthSquared();
            if (lengthSquared < 0.0001f)
            {
                return fallback;
            }

            return vector / MathF.Sqrt(lengthSquared);
        }

        private enum Side
        {
            Left,
            Right,
            Back,
            Front
        }

        private readonly struct ChunkCandidate
        {
            public ChunkCandidate(int chunkX, int chunkY, float score, bool waterVisible, bool waterReducedDetail)
            {
                ChunkX = chunkX;
                ChunkY = chunkY;
                Score = score;
                WaterVisible = waterVisible;
                WaterReducedDetail = waterReducedDetail;
            }

            public int ChunkX { get; }

            public int ChunkY { get; }

            public float Score { get; }

            public bool WaterVisible { get; }

            public bool WaterReducedDetail { get; }
        }

        private readonly struct ChunkCacheKey : IEquatable<ChunkCacheKey>
        {
            public ChunkCacheKey(int startX, int startY)
            {
                StartX = startX;
                StartY = startY;
            }

            public int StartX { get; }

            public int StartY { get; }

            public bool Equals(ChunkCacheKey other)
            {
                return StartX == other.StartX && StartY == other.StartY;
            }

            public override bool Equals(object obj)
            {
                return obj is ChunkCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(StartX, StartY);
            }
        }

        private sealed class WaterChunkCacheEntry
        {
            public WaterChunkCacheEntry(VoxelChunk chunk, int timeBucket, bool reducedDetail, float[] nearDetailSurfaceHeights, byte[] nearDetailShoreMasks, float[] reducedSurfaceHeights)
            {
                Chunk = chunk;
                TimeBucket = timeBucket;
                ReducedDetail = reducedDetail;
                NearDetailSurfaceHeights = nearDetailSurfaceHeights;
                NearDetailShoreMasks = nearDetailShoreMasks;
                ReducedSurfaceHeights = reducedSurfaceHeights;
            }

            public VoxelChunk Chunk { get; }

            public int TimeBucket { get; set; }

            public bool ReducedDetail { get; set; }

            public float[] NearDetailSurfaceHeights { get; set; }

            public byte[] NearDetailShoreMasks { get; set; }

            public float[] ReducedSurfaceHeights { get; set; }
        }

        private sealed class VoxelChunk
        {
            private const int InitialVertexCapacity = 4096;
            private VertexPositionColor[] _vertices;

            public VoxelChunk()
            {
                _vertices = new VertexPositionColor[InitialVertexCapacity];
            }

            public VertexPositionColor[] Vertices => _vertices;

            public int VertexCount { get; private set; }

            public void Reset()
            {
                VertexCount = 0;
            }

            public void AppendTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                EnsureCapacity(3);
                Vertices[VertexCount++] = new VertexPositionColor(a, color);
                Vertices[VertexCount++] = new VertexPositionColor(b, color);
                Vertices[VertexCount++] = new VertexPositionColor(c, color);
            }

            public void AppendTriangle(Vector3 a, Color colorA, Vector3 b, Color colorB, Vector3 c, Color colorC)
            {
                EnsureCapacity(3);
                Vertices[VertexCount++] = new VertexPositionColor(a, colorA);
                Vertices[VertexCount++] = new VertexPositionColor(b, colorB);
                Vertices[VertexCount++] = new VertexPositionColor(c, colorC);
            }

            private void EnsureCapacity(int additionalVertices)
            {
                var requiredCapacity = VertexCount + additionalVertices;
                if (requiredCapacity <= Vertices.Length)
                {
                    return;
                }

                var newCapacity = Math.Max(requiredCapacity, Vertices.Length * 2);
                Array.Resize(ref _vertices, newCapacity);
            }
        }

        public readonly record struct WorldViewState(Vector2 CameraPosition, float Heading, float Pitch, float Altitude, float MaxFlightAltitude, bool RenderShip, TruckWorldRenderState Truck, bool UseTruckCamera, float TruckCameraDistance);

        public readonly record struct TruckWorldRenderState(Vector2 Position, float WorldY, float Heading, float WheelRotation, float Pitch, bool IsVisible);
    }
}
