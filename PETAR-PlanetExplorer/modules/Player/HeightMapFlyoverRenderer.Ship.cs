using System;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
        private const float ShipCubeSize = CubeSize * 0.24f;
        private const float ShipCubeHeight = CubeHeight * 0.26f;
        private const float ShipCameraDistanceStopped = CubeSize * 8.1f;
        private const float ShipVerticalOffset = -5f;
        private const float ShipHorizontalOffset = 0f;
        private const float ShipRollLimit = 0.5f;
        private const float PayloadCubeSize = ShipCubeSize * 0.72f;
        private const float PayloadCubeHeight = ShipCubeHeight * 0.88f;
        private const float PayloadRopeLength = CubeSize * 0.95f;
        private const float PayloadSpringStrength = 10f;
        private const float PayloadDamping = 2.8f;
        private const float PayloadAccelerationResponse = 0.22f;
        private const float PayloadThrustLag = 1.2f;
        private const float PayloadGravity = 12f;
        private const int MaxEngineParticles = 48;
        private const float EngineParticleLifetimeMin = 0.4f;
        private const float EngineParticleLifetimeMax = 0.85f;
        private const float EngineParticleBackwardSpeed = CubeSize * 2.8f;
        private const float EngineParticleSpread = ShipCubeSize * 0.45f;
        private const float EngineParticleGravity = 5.5f;
        private static readonly Color ShipPrimaryColor = new Color(222, 222, 228);
        private static readonly Color ShipAccentColor = new Color(78, 78, 86);
        private static readonly Color ShipCockpitColor = new Color(28, 34, 44);
        private static readonly Color ShipTrimColor = new Color(246, 146, 86);
        private static readonly Color ShipGlowColor = new Color(255, 180, 72);
        private static readonly Color ShipShadowColor = new Color(34, 40, 52);
        private static readonly Color RopeColor = new Color(164, 148, 128);
        private static readonly Color PayloadPrimaryColor = new Color(154, 154, 160);
        private static readonly Color PayloadBorderColor = new Color(36, 36, 40);
        private static readonly Color EngineParticleHotColor = new Color(236, 54, 44, 220);
        private static readonly Color EngineParticleCoolColor = new Color(52, 52, 56, 0);

        private readonly VoxelChunk _shipChunk;
        private readonly VoxelChunk _shipParticleChunk;
        private ShipState _ship;
        private EngineParticle[] _engineParticles;
        private Random _shipRandom;
        private Vector2 _lastShipCameraPosition;
        private float _lastShipHeading;
        private float _lastShipUpdateTime;
        private int _shipSeed = int.MinValue;

        private void EnsureShipInitialized(ProceduralWorldMap worldMap, Vector2 cameraPosition, float heading, float time)
        {
            if (_shipSeed == worldMap.Seed && _engineParticles != null)
            {
                return;
            }

            _shipSeed = worldMap.Seed;
            _shipRandom = new Random(worldMap.Seed ^ unchecked((int)0x6e624eb7));
            _engineParticles = new EngineParticle[MaxEngineParticles];
            _ship = new ShipState
            {
                CameraDistance = ShipCameraDistanceStopped,
                PayloadOffset = new Vector3(0f, -PayloadRopeLength, 0f),
                RopeExtension = 1f
            };
            _lastShipCameraPosition = cameraPosition;
            _lastShipHeading = heading;
            _lastShipUpdateTime = time;
        }

        private void UpdateShip(Vector2 cameraPosition, float heading, float time, bool payloadReleased)
        {
            if (_engineParticles == null)
            {
                return;
            }

            var deltaTime = time - _lastShipUpdateTime;
            if (deltaTime < 0f || deltaTime > 1f)
            {
                deltaTime = 0f;
            }

            var cameraOffset = GetWrappedOffset(cameraPosition - _lastShipCameraPosition);
            var velocity = deltaTime > 0f ? cameraOffset / deltaTime : Vector2.Zero;
            var acceleration = deltaTime > 0f ? (velocity - _ship.Velocity) / deltaTime : Vector2.Zero;
            var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
            var right = new Vector2(-forward.Y, forward.X);
            var headingDelta = WrapAngle(heading - _lastShipHeading);
            var turnRate = deltaTime > 0f ? headingDelta / deltaTime : 0f;
            var speed = velocity.Length();
            var thrust = MathHelper.Clamp(speed / 12f, 0f, 1f);
            var targetRoll = MathHelper.Clamp((turnRate * 0.16f) + (Vector2.Dot(velocity, right) * 0.02f), -ShipRollLimit, ShipRollLimit);
            var targetCameraDistance = ShipCameraDistanceStopped;

            if (payloadReleased && !_ship.PayloadReleased)
            {
                _ship.PayloadReleased = true;
                _ship.ReleasedPayloadActive = true;
                _ship.RopeExtension = 1f;
                _ship.ReleasedPayloadOffset = _ship.PayloadOffset + new Vector3(0f, -PayloadCubeHeight * 0.25f, -ShipCubeSize * 0.1f);
                _ship.ReleasedPayloadVelocity = new Vector3(0f, -CubeSize * 0.45f, -MathHelper.Lerp(CubeSize * 0.5f, CubeSize * 1.2f, thrust));
            }

            if (deltaTime > 0f)
            {
                _ship.CameraDistance = targetCameraDistance;
                _ship.Roll = MathHelper.Lerp(_ship.Roll, targetRoll, MathHelper.Clamp(deltaTime * 4.5f, 0f, 1f));

                if (!_ship.PayloadReleased)
                {
                    var payloadForce = new Vector3(
                        -Vector2.Dot(acceleration, right) * PayloadAccelerationResponse,
                        -PayloadGravity,
                        -Vector2.Dot(acceleration, forward) * PayloadAccelerationResponse);
                    var payloadRest = new Vector3(0f, -PayloadRopeLength, -thrust * PayloadThrustLag);
                    var payloadAcceleration = ((payloadRest - _ship.PayloadOffset) * PayloadSpringStrength) + payloadForce - (_ship.PayloadVelocity * PayloadDamping);
                    _ship.PayloadVelocity += payloadAcceleration * deltaTime;
                    _ship.PayloadOffset += _ship.PayloadVelocity * deltaTime;

                    var maxPayloadDistance = PayloadRopeLength * 1.5f;
                    var payloadDistance = _ship.PayloadOffset.Length();
                    if (payloadDistance > maxPayloadDistance)
                    {
                        _ship.PayloadOffset *= maxPayloadDistance / payloadDistance;
                        var outward = Vector3.Dot(_ship.PayloadVelocity, Vector3.Normalize(_ship.PayloadOffset));
                        if (outward > 0f)
                        {
                            _ship.PayloadVelocity -= Vector3.Normalize(_ship.PayloadOffset) * outward;
                        }
                    }

                    _ship.RopeExtension = MathHelper.Clamp(_ship.RopeExtension + (deltaTime * 6f), 0f, 1f);
                }
                else
                {
                    _ship.RopeExtension = MathHelper.Clamp(_ship.RopeExtension - (deltaTime * 2.8f), 0f, 1f);
                    UpdateReleasedPayload(deltaTime, thrust);
                }

                UpdateEngineParticles(deltaTime);
                if (thrust > 0.08f || MathF.Abs(turnRate) > 0.3f)
                {
                    _ship.EngineParticleAccumulator += deltaTime * MathHelper.Lerp(5f, 24f, MathHelper.Max(thrust, MathHelper.Clamp(MathF.Abs(turnRate) / 4f, 0f, 1f)));
                    while (_ship.EngineParticleAccumulator >= 1f)
                    {
                        _ship.EngineParticleAccumulator -= 1f;
                        SpawnEngineParticle(new Vector3(-ShipCubeSize * 1.15f, -ShipCubeHeight * 1.05f, 0f), thrust);
                        SpawnEngineParticle(new Vector3(ShipCubeSize * 1.15f, -ShipCubeHeight * 1.05f, 0f), thrust);
                        SpawnEngineParticle(new Vector3(0f, -ShipCubeHeight * 1.18f, ShipCubeSize * 0.18f), thrust);
                    }
                }
            }

            _ship.Velocity = velocity;
            _lastShipCameraPosition = cameraPosition;
            _lastShipHeading = heading;
            _lastShipUpdateTime = time;
        }

        private float GetShipCameraDistance()
        {
            return _ship.CameraDistance > 0f ? _ship.CameraDistance : ShipCameraDistanceStopped;
        }

        private void UpdateReleasedPayload(float deltaTime, float thrust)
        {
            if (!_ship.ReleasedPayloadActive)
            {
                return;
            }

            _ship.ReleasedPayloadVelocity += new Vector3(0f, -PayloadGravity * 0.9f, -MathHelper.Lerp(CubeSize * 0.6f, CubeSize * 1.4f, thrust)) * deltaTime;
            _ship.ReleasedPayloadVelocity *= MathHelper.Clamp(1f - (deltaTime * 0.45f), 0f, 1f);
            _ship.ReleasedPayloadOffset += _ship.ReleasedPayloadVelocity * deltaTime;

            if (_ship.ReleasedPayloadOffset.Y < (-PayloadRopeLength - (CubeSize * 18f)) || _ship.ReleasedPayloadOffset.Z < -(CubeSize * 26f))
            {
                _ship.ReleasedPayloadActive = false;
            }
        }

        private void UpdateEngineParticles(float deltaTime)
        {
            for (var index = 0; index < _engineParticles.Length; index++)
            {
                ref var particle = ref _engineParticles[index];
                if (!particle.Active)
                {
                    continue;
                }

                particle.Age += deltaTime;
                if (particle.Age >= particle.Lifetime)
                {
                    particle = default;
                    continue;
                }

                particle.Velocity += new Vector3(0f, -EngineParticleGravity, -CubeSize * 0.12f) * deltaTime;
                particle.Position += particle.Velocity * deltaTime;
            }
        }

        private void SpawnEngineParticle(Vector3 origin, float thrust)
        {
            for (var index = 0; index < _engineParticles.Length; index++)
            {
                if (_engineParticles[index].Active)
                {
                    continue;
                }

                var lateralJitter = MathHelper.Lerp(-EngineParticleSpread, EngineParticleSpread, (float)_shipRandom.NextDouble());
                var verticalJitter = MathHelper.Lerp(-EngineParticleSpread * 0.35f, EngineParticleSpread * 0.45f, (float)_shipRandom.NextDouble());
                _engineParticles[index] = new EngineParticle
                {
                    Active = true,
                    Position = origin + new Vector3(lateralJitter, verticalJitter, 0f),
                    Velocity = new Vector3(
                        lateralJitter * 2.2f,
                        MathHelper.Lerp(-1.2f, 1.4f, (float)_shipRandom.NextDouble()),
                        -MathHelper.Lerp(EngineParticleBackwardSpeed * 0.7f, EngineParticleBackwardSpeed * 1.15f, thrust + ((float)_shipRandom.NextDouble() * (1f - thrust)))),
                    Age = 0f,
                    Lifetime = MathHelper.Lerp(EngineParticleLifetimeMin, EngineParticleLifetimeMax, (float)_shipRandom.NextDouble())
                };
                return;
            }
        }

        private void PopulateShipChunks(Vector3 shipCenter, Vector3 baseRight, Vector3 baseUp, Vector3 baseForward)
        {
            _shipChunk.Reset();
            _shipParticleChunk.Reset();

            var rollMatrix = Matrix.CreateFromAxisAngle(baseForward, _ship.Roll);
            var shipRight = Vector3.Normalize(Vector3.TransformNormal(baseRight, rollMatrix));
            var shipUp = Vector3.Normalize(Vector3.TransformNormal(baseUp, rollMatrix));
            var shipForward = Vector3.Normalize(baseForward);

            AppendShipBody(_shipChunk, shipCenter, shipRight, shipUp, shipForward);
            AppendPayload(_shipChunk, shipCenter, shipRight, shipUp, shipForward);
            AppendEngineParticles(_shipParticleChunk, shipCenter, shipRight, shipUp, shipForward);
        }

        private void AppendShipBody(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            AppendShipMainHull(chunk, center, right, up, forward);
            AppendShipCanopy(chunk, center, right, up, forward);
            AppendShipCanopyFrameDetails(chunk, center, right, up, forward);
            AppendShipEnginePods(chunk, center, right, up, forward);
            AppendShipRearFins(chunk, center, right, up, forward);
            AppendShipSurfaceIntakes(chunk, center, right, up, forward);
            AppendShipEngineRings(chunk, center, right, up, forward);
            AppendShipPanelLines(chunk, center, right, up, forward);
            AppendShipGlowPanels(chunk, center, right, up, forward);
        }

        private void AppendShipMainHull(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            Span<float> sectionZ = stackalloc float[] { -5.6f, -4.1f, -2.5f, -0.7f, 1.3f, 3.2f, 5.4f };
            Span<float> sectionWidth = stackalloc float[] { 0.42f, 1.18f, 2.42f, 3.08f, 2.74f, 1.54f, 0.18f };
            Span<float> sectionTop = stackalloc float[] { 0.18f, 0.34f, 0.52f, 0.72f, 0.88f, 0.46f, 0.12f };
            Span<float> sectionSide = stackalloc float[] { -0.04f, 0.04f, 0.16f, 0.26f, 0.22f, 0.08f, -0.02f };
            Span<float> sectionBottom = stackalloc float[] { -0.10f, -0.22f, -0.34f, -0.42f, -0.36f, -0.2f, -0.04f };
            Span<Vector3> previousProfile = stackalloc Vector3[7];
            Span<Vector3> currentProfile = stackalloc Vector3[7];
            Span<Vector3> previousBelly = stackalloc Vector3[3];
            Span<Vector3> currentBelly = stackalloc Vector3[3];

            CreateHullSectionProfile(previousProfile, sectionZ[0], sectionWidth[0], sectionTop[0], sectionSide[0], sectionBottom[0]);
            CreateHullBellyProfile(previousBelly, sectionZ[0], sectionWidth[0], sectionBottom[0]);
            for (var index = 1; index < sectionZ.Length; index++)
            {
                CreateHullSectionProfile(currentProfile, sectionZ[index], sectionWidth[index], sectionTop[index], sectionSide[index], sectionBottom[index]);
                CreateHullBellyProfile(currentBelly, sectionZ[index], sectionWidth[index], sectionBottom[index]);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[0], previousProfile[1], currentProfile[1], currentProfile[0], ShipShadowColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[1], previousProfile[2], currentProfile[2], currentProfile[1], ShipAccentColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[2], previousProfile[3], currentProfile[3], currentProfile[2], ShipPrimaryColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[3], previousProfile[4], currentProfile[4], currentProfile[3], ShipPrimaryColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[4], previousProfile[5], currentProfile[5], currentProfile[4], ShipAccentColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[5], previousProfile[6], currentProfile[6], currentProfile[5], ShipShadowColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousBelly[0], previousBelly[1], currentBelly[1], currentBelly[0], ShipShadowColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousBelly[1], previousBelly[2], currentBelly[2], currentBelly[1], ShipShadowColor);
                currentProfile.CopyTo(previousProfile);
                currentBelly.CopyTo(previousBelly);
            }

            for (var ridge = -1; ridge <= 4; ridge++)
            {
                var start = new Vector3(-ShipCubeSize * 0.08f, ShipCubeHeight * 0.5f, ridge * ShipCubeSize * 0.9f);
                var end = new Vector3(ShipCubeSize * 0.08f, ShipCubeHeight * 0.5f, (ridge + 1) * ShipCubeSize * 0.9f);
                AppendShipRibbon(chunk, center, right, up, forward, start, end, ShipCubeSize * 0.08f, ShipTrimColor);
            }
        }

        private void AppendShipCanopy(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            Span<float> canopyZ = stackalloc float[] { -0.2f, 0.8f, 1.8f, 2.8f, 3.5f };
            Span<float> canopyWidth = stackalloc float[] { 1.26f, 1.56f, 1.46f, 1.04f, 0.46f };
            Span<float> canopyBase = stackalloc float[] { 0.48f, 0.62f, 0.72f, 0.62f, 0.38f };
            Span<float> canopyTop = stackalloc float[] { 1.1f, 1.34f, 1.46f, 1.16f, 0.62f };
            Span<Vector3> previousProfile = stackalloc Vector3[5];
            Span<Vector3> currentProfile = stackalloc Vector3[5];

            CreateCanopySectionProfile(previousProfile, canopyZ[0], canopyWidth[0], canopyBase[0], canopyTop[0]);
            for (var index = 1; index < canopyZ.Length; index++)
            {
                CreateCanopySectionProfile(currentProfile, canopyZ[index], canopyWidth[index], canopyBase[index], canopyTop[index]);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[0], previousProfile[1], currentProfile[1], currentProfile[0], ShipAccentColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[1], previousProfile[2], currentProfile[2], currentProfile[1], ShipCockpitColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[2], previousProfile[3], currentProfile[3], currentProfile[2], ShipCockpitColor);
                AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[3], previousProfile[4], currentProfile[4], currentProfile[3], ShipAccentColor);
                currentProfile.CopyTo(previousProfile);
            }

            for (var canopyFrame = 0; canopyFrame < canopyZ.Length - 1; canopyFrame++)
            {
                var frameStart = new Vector3(0f, canopyTop[canopyFrame] * ShipCubeHeight, canopyZ[canopyFrame] * ShipCubeSize);
                var frameEnd = new Vector3(0f, canopyTop[canopyFrame + 1] * ShipCubeHeight, canopyZ[canopyFrame + 1] * ShipCubeSize);
                AppendShipRibbon(chunk, center, right, up, forward, frameStart, frameEnd, ShipCubeSize * 0.06f, ShipShadowColor);
            }
        }

        private void AppendShipEnginePods(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                Span<float> podZ = stackalloc float[] { -4.4f, -3.3f, -2.1f, -0.8f, 0.6f };
                Span<float> podRadius = stackalloc float[] { 0.62f, 0.9f, 1.02f, 0.9f, 0.52f };
                Span<float> podTop = stackalloc float[] { 0.18f, 0.28f, 0.34f, 0.24f, 0.08f };
                Span<float> podBottom = stackalloc float[] { -0.24f, -0.28f, -0.34f, -0.28f, -0.14f };
                Span<Vector3> previousProfile = stackalloc Vector3[5];
                Span<Vector3> currentProfile = stackalloc Vector3[5];
                var podOffsetX = side * ShipCubeSize * 3.22f;

                CreatePodSectionProfile(previousProfile, podOffsetX, podZ[0], podRadius[0], podTop[0], podBottom[0], side);
                for (var index = 1; index < podZ.Length; index++)
                {
                    CreatePodSectionProfile(currentProfile, podOffsetX, podZ[index], podRadius[index], podTop[index], podBottom[index], side);
                    AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[0], previousProfile[1], currentProfile[1], currentProfile[0], ShipShadowColor);
                    AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[1], previousProfile[2], currentProfile[2], currentProfile[1], ShipPrimaryColor);
                    AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[2], previousProfile[3], currentProfile[3], currentProfile[2], ShipAccentColor);
                    AppendShipProfileStrip(chunk, center, right, up, forward, previousProfile[3], previousProfile[4], currentProfile[4], currentProfile[3], ShipShadowColor);
                    currentProfile.CopyTo(previousProfile);
                }

                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(podOffsetX - (side * ShipCubeSize * 0.06f), -ShipCubeHeight * 0.08f, -ShipCubeSize * 3.74f),
                    new Vector3(podOffsetX + (side * ShipCubeSize * 0.06f), -ShipCubeHeight * 0.08f, -ShipCubeSize * 3.74f),
                    ShipCubeHeight * 0.16f,
                    ShipGlowColor);
            }
        }

        private void AppendShipCanopyFrameDetails(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(-ShipCubeSize * 1.18f, ShipCubeHeight * 0.7f, -ShipCubeSize * 0.08f),
                new Vector3(-ShipCubeSize * 0.54f, ShipCubeHeight * 1.12f, ShipCubeSize * 2.92f),
                ShipCubeHeight * 0.045f,
                ShipShadowColor);
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(ShipCubeSize * 1.18f, ShipCubeHeight * 0.7f, -ShipCubeSize * 0.08f),
                new Vector3(ShipCubeSize * 0.54f, ShipCubeHeight * 1.12f, ShipCubeSize * 2.92f),
                ShipCubeHeight * 0.045f,
                ShipShadowColor);
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(-ShipCubeSize * 0.24f, ShipCubeHeight * 1.18f, ShipCubeSize * 0.26f),
                new Vector3(ShipCubeSize * 0.24f, ShipCubeHeight * 1.18f, ShipCubeSize * 0.26f),
                ShipCubeHeight * 0.04f,
                ShipShadowColor);
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(0f, ShipCubeHeight * 1.08f, ShipCubeSize * 0.42f),
                new Vector3(0f, ShipCubeHeight * 1.28f, ShipCubeSize * 3.02f),
                ShipCubeHeight * 0.03f,
                ShipTrimColor);
        }

        private void AppendShipRearFins(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var rootFront = new Vector3(side * ShipCubeSize * 1.62f, ShipCubeHeight * 0.52f, -ShipCubeSize * 1.58f);
                var rootRear = new Vector3(side * ShipCubeSize * 1.72f, ShipCubeHeight * 0.5f, -ShipCubeSize * 3.6f);
                var tipRear = new Vector3(side * ShipCubeSize * 2.28f, ShipCubeHeight * 1.82f, -ShipCubeSize * 4.72f);
                var innerTip = new Vector3(side * ShipCubeSize * 1.92f, ShipCubeHeight * 1.42f, -ShipCubeSize * 3.46f);
                AppendShipProfileStrip(chunk, center, right, up, forward, rootFront, rootRear, tipRear, innerTip, ShipAccentColor);
                AppendShipTriangleLocal(chunk, center, right, up, forward, rootFront, innerTip, new Vector3(side * ShipCubeSize * 1.18f, ShipCubeHeight * 0.92f, -ShipCubeSize * 2.22f), ShipShadowColor);
            }
        }

        private void AppendShipGlowPanels(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(-ShipCubeSize * 0.18f, ShipCubeHeight * 0.22f, ShipCubeSize * 5.02f),
                new Vector3(ShipCubeSize * 0.18f, ShipCubeHeight * 0.22f, ShipCubeSize * 5.02f),
                ShipCubeHeight * 0.22f,
                ShipGlowColor);
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(-ShipCubeSize * 0.12f, ShipCubeHeight * 0.46f, ShipCubeSize * 2.66f),
                new Vector3(ShipCubeSize * 0.12f, ShipCubeHeight * 0.46f, ShipCubeSize * 2.66f),
                ShipCubeHeight * 0.14f,
                ShipGlowColor);
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(-ShipCubeSize * 0.84f, ShipCubeHeight * 0.26f, ShipCubeSize * 3.58f),
                new Vector3(ShipCubeSize * 0.84f, ShipCubeHeight * 0.34f, ShipCubeSize * 3.18f),
                ShipCubeHeight * 0.1f,
                ShipGlowColor);
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(-ShipCubeSize * 0.42f, ShipCubeHeight * 0.58f, ShipCubeSize * 1.22f),
                new Vector3(ShipCubeSize * 0.42f, ShipCubeHeight * 0.62f, ShipCubeSize * 0.74f),
                ShipCubeHeight * 0.08f,
                ShipGlowColor);
            for (var side = -1; side <= 1; side += 2)
            {
                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 2.38f, -ShipCubeHeight * 0.18f, ShipCubeSize * 2.18f),
                    new Vector3(side * ShipCubeSize * 2.18f, -ShipCubeHeight * 0.06f, ShipCubeSize * 2.64f),
                    ShipCubeHeight * 0.18f,
                    ShipGlowColor);
                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 1.9f, ShipCubeHeight * 0.04f, ShipCubeSize * 1.64f),
                    new Vector3(side * ShipCubeSize * 2.46f, ShipCubeHeight * 0.1f, ShipCubeSize * 0.48f),
                    ShipCubeHeight * 0.08f,
                    ShipGlowColor);
                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 2.64f, ShipCubeHeight * 0.06f, -ShipCubeSize * 0.96f),
                    new Vector3(side * ShipCubeSize * 2.82f, ShipCubeHeight * 0.02f, -ShipCubeSize * 2.42f),
                    ShipCubeHeight * 0.07f,
                    ShipGlowColor);
                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 2.82f, -ShipCubeHeight * 0.08f, -ShipCubeSize * 3.54f),
                    new Vector3(side * ShipCubeSize * 2.98f, -ShipCubeHeight * 0.08f, -ShipCubeSize * 4.18f),
                    ShipCubeHeight * 0.09f,
                    ShipGlowColor);
            }
        }

        private void AppendShipSurfaceIntakes(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                AppendShipQuadLocal(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 1.86f, ShipCubeHeight * 0.14f, ShipCubeSize * 1.98f),
                    new Vector3(side * ShipCubeSize * 2.48f, ShipCubeHeight * 0.08f, ShipCubeSize * 1.74f),
                    new Vector3(side * ShipCubeSize * 2.18f, -ShipCubeHeight * 0.16f, ShipCubeSize * 0.74f),
                    new Vector3(side * ShipCubeSize * 1.62f, -ShipCubeHeight * 0.04f, ShipCubeSize * 1.12f),
                    ShipShadowColor);
                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 1.98f, ShipCubeHeight * 0.08f, ShipCubeSize * 1.88f),
                    new Vector3(side * ShipCubeSize * 1.84f, -ShipCubeHeight * 0.08f, ShipCubeSize * 1.02f),
                    ShipCubeHeight * 0.03f,
                    ShipTrimColor);
            }
        }

        private void AppendShipEngineRings(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var outerTop = new Vector3(side * ShipCubeSize * 3.02f, ShipCubeHeight * 0.18f, -ShipCubeSize * 3.68f);
                var outerBottom = new Vector3(side * ShipCubeSize * 3.02f, -ShipCubeHeight * 0.26f, -ShipCubeSize * 3.68f);
                var innerTop = new Vector3(side * ShipCubeSize * 2.72f, ShipCubeHeight * 0.14f, -ShipCubeSize * 3.38f);
                var innerBottom = new Vector3(side * ShipCubeSize * 2.72f, -ShipCubeHeight * 0.18f, -ShipCubeSize * 3.38f);
                AppendShipQuadLocal(chunk, center, right, up, forward, outerTop, innerTop, innerBottom, outerBottom, ShipAccentColor);
                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 2.92f, -ShipCubeHeight * 0.06f, -ShipCubeSize * 3.58f),
                    new Vector3(side * ShipCubeSize * 2.76f, -ShipCubeHeight * 0.06f, -ShipCubeSize * 3.44f),
                    ShipCubeHeight * 0.06f,
                    ShipGlowColor);
            }
        }

        private void AppendShipPanelLines(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(-ShipCubeSize * 0.06f, ShipCubeHeight * 0.58f, ShipCubeSize * 4.74f),
                new Vector3(ShipCubeSize * 0.06f, ShipCubeHeight * 0.48f, -ShipCubeSize * 2.94f),
                ShipCubeHeight * 0.032f,
                ShipTrimColor);
            AppendShipRibbon(
                chunk,
                center,
                right,
                up,
                forward,
                new Vector3(-ShipCubeSize * 0.14f, ShipCubeHeight * 0.2f, ShipCubeSize * 3.92f),
                new Vector3(ShipCubeSize * 0.14f, ShipCubeHeight * 0.2f, ShipCubeSize * 1.54f),
                ShipCubeHeight * 0.024f,
                ShipTrimColor);

            for (var side = -1; side <= 1; side += 2)
            {
                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 0.84f, ShipCubeHeight * 0.44f, ShipCubeSize * 4.18f),
                    new Vector3(side * ShipCubeSize * 2.18f, ShipCubeHeight * 0.22f, ShipCubeSize * 0.92f),
                    ShipCubeHeight * 0.03f,
                    ShipTrimColor);
                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 1.12f, ShipCubeHeight * 0.18f, ShipCubeSize * 2.22f),
                    new Vector3(side * ShipCubeSize * 2.78f, ShipCubeHeight * 0.04f, -ShipCubeSize * 1.34f),
                    ShipCubeHeight * 0.026f,
                    ShipTrimColor);
                AppendShipRibbon(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 1.64f, ShipCubeHeight * 0.54f, -ShipCubeSize * 1.86f),
                    new Vector3(side * ShipCubeSize * 2.12f, ShipCubeHeight * 1.12f, -ShipCubeSize * 3.98f),
                    ShipCubeHeight * 0.03f,
                    ShipTrimColor);
            }

            for (var side = -1; side <= 1; side += 2)
            {
                AppendShipQuadLocal(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 2.42f, ShipCubeHeight * 0.08f, ShipCubeSize * 1.96f),
                    new Vector3(side * ShipCubeSize * 2.62f, ShipCubeHeight * 0.08f, ShipCubeSize * 1.82f),
                    new Vector3(side * ShipCubeSize * 2.54f, ShipCubeHeight * 0.16f, ShipCubeSize * 1.58f),
                    new Vector3(side * ShipCubeSize * 2.34f, ShipCubeHeight * 0.16f, ShipCubeSize * 1.7f),
                    ShipTrimColor);
                AppendShipQuadLocal(
                    chunk,
                    center,
                    right,
                    up,
                    forward,
                    new Vector3(side * ShipCubeSize * 2.74f, ShipCubeHeight * 0.02f, -ShipCubeSize * 2.24f),
                    new Vector3(side * ShipCubeSize * 2.92f, ShipCubeHeight * 0.02f, -ShipCubeSize * 2.38f),
                    new Vector3(side * ShipCubeSize * 2.82f, ShipCubeHeight * 0.12f, -ShipCubeSize * 2.62f),
                    new Vector3(side * ShipCubeSize * 2.66f, ShipCubeHeight * 0.12f, -ShipCubeSize * 2.48f),
                    ShipTrimColor);
            }
        }

        private static void CreateHullSectionProfile(Span<Vector3> profile, float z, float halfWidth, float topY, float sideY, float bottomY)
        {
            var zWorld = z * ShipCubeSize;
            var widthWorld = halfWidth * ShipCubeSize;
            var topWorld = topY * ShipCubeHeight;
            var sideWorld = sideY * ShipCubeHeight;
            var bottomWorld = bottomY * ShipCubeHeight;
            profile[0] = new Vector3(-widthWorld * 0.34f, bottomWorld, zWorld);
            profile[1] = new Vector3(-widthWorld, sideWorld - (ShipCubeHeight * 0.08f), zWorld);
            profile[2] = new Vector3(-widthWorld * 0.74f, sideWorld + ((topWorld - sideWorld) * 0.7f), zWorld);
            profile[3] = new Vector3(0f, topWorld, zWorld);
            profile[4] = new Vector3(widthWorld * 0.74f, sideWorld + ((topWorld - sideWorld) * 0.7f), zWorld);
            profile[5] = new Vector3(widthWorld, sideWorld - (ShipCubeHeight * 0.08f), zWorld);
            profile[6] = new Vector3(widthWorld * 0.34f, bottomWorld, zWorld);
        }

        private static void CreateHullBellyProfile(Span<Vector3> profile, float z, float halfWidth, float bottomY)
        {
            var zWorld = z * ShipCubeSize;
            var widthWorld = halfWidth * ShipCubeSize;
            var bottomWorld = bottomY * ShipCubeHeight;
            profile[0] = new Vector3(-widthWorld * 0.34f, bottomWorld, zWorld);
            profile[1] = new Vector3(0f, bottomWorld - (ShipCubeHeight * 0.18f), zWorld);
            profile[2] = new Vector3(widthWorld * 0.34f, bottomWorld, zWorld);
        }

        private static void CreateCanopySectionProfile(Span<Vector3> profile, float z, float halfWidth, float baseY, float topY)
        {
            var zWorld = z * ShipCubeSize;
            var widthWorld = halfWidth * ShipCubeSize;
            var baseWorld = baseY * ShipCubeHeight;
            var topWorld = topY * ShipCubeHeight;
            profile[0] = new Vector3(-widthWorld * 0.78f, baseWorld, zWorld);
            profile[1] = new Vector3(-widthWorld * 0.42f, baseWorld + ((topWorld - baseWorld) * 0.74f), zWorld);
            profile[2] = new Vector3(0f, topWorld, zWorld);
            profile[3] = new Vector3(widthWorld * 0.42f, baseWorld + ((topWorld - baseWorld) * 0.74f), zWorld);
            profile[4] = new Vector3(widthWorld * 0.78f, baseWorld, zWorld);
        }

        private static void CreatePodSectionProfile(Span<Vector3> profile, float centerX, float z, float radius, float topY, float bottomY, int side)
        {
            var zWorld = z * ShipCubeSize;
            var centerWorldX = centerX;
            var radiusWorld = radius * ShipCubeSize;
            var topWorld = topY * ShipCubeHeight;
            var bottomWorld = bottomY * ShipCubeHeight;
            var innerX = centerWorldX - (side * radiusWorld * 0.42f);
            var outerX = centerWorldX + (side * radiusWorld * 0.58f);
            profile[0] = new Vector3(innerX, bottomWorld, zWorld);
            profile[1] = new Vector3(innerX, bottomWorld + ((topWorld - bottomWorld) * 0.58f), zWorld);
            profile[2] = new Vector3(centerWorldX + (side * radiusWorld * 0.08f), topWorld, zWorld);
            profile[3] = new Vector3(outerX, bottomWorld + ((topWorld - bottomWorld) * 0.64f), zWorld);
            profile[4] = new Vector3(outerX, bottomWorld, zWorld);
        }

        private void AppendShipProfileStrip(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 a0, Vector3 a1, Vector3 b1, Vector3 b0, Color color)
        {
            AppendShipQuadLocal(chunk, center, right, up, forward, a0, a1, b1, b0, color);
        }

        private void AppendShipRibbon(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 start, Vector3 end, float halfWidth, Color color)
        {
            var localDirection = end - start;
            if (localDirection.LengthSquared() < 0.0001f)
            {
                return;
            }

            localDirection.Normalize();
            var localSide = Vector3.Cross(localDirection, Vector3.Up);
            if (localSide.LengthSquared() < 0.0001f)
            {
                localSide = Vector3.Cross(localDirection, Vector3.Right);
            }

            localSide.Normalize();
            localSide *= halfWidth;
            AppendShipQuadLocal(chunk, center, right, up, forward, start - localSide, start + localSide, end + localSide, end - localSide, color);
        }

        private void AppendShipQuadLocal(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color color)
        {
            AppendQuad(
                chunk,
                TransformLocalPoint(center, right, up, forward, v0),
                TransformLocalPoint(center, right, up, forward, v1),
                TransformLocalPoint(center, right, up, forward, v2),
                TransformLocalPoint(center, right, up, forward, v3),
                color);
        }

        private void AppendShipTriangleLocal(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 v0, Vector3 v1, Vector3 v2, Color color)
        {
            AppendSafeTriangle(
                chunk,
                TransformLocalPoint(center, right, up, forward, v0),
                TransformLocalPoint(center, right, up, forward, v1),
                TransformLocalPoint(center, right, up, forward, v2),
                color);
        }

        private void AppendPayload(VoxelChunk chunk, Vector3 shipCenter, Vector3 right, Vector3 up, Vector3 forward)
        {
            var ropeAnchorOffset = ShipCubeSize * 0.9f;
            var anchorLeft = TransformLocalPoint(shipCenter, right, up, forward, new Vector3(-ropeAnchorOffset, -ShipCubeHeight * 0.4f, -ShipCubeSize * 0.2f));
            var anchorRight = TransformLocalPoint(shipCenter, right, up, forward, new Vector3(ropeAnchorOffset, -ShipCubeHeight * 0.4f, -ShipCubeSize * 0.2f));
            var retractedLeft = TransformLocalPoint(shipCenter, right, up, forward, new Vector3(-ropeAnchorOffset * 0.28f, -ShipCubeHeight * 0.26f, -ShipCubeSize * 0.08f));
            var retractedRight = TransformLocalPoint(shipCenter, right, up, forward, new Vector3(ropeAnchorOffset * 0.28f, -ShipCubeHeight * 0.26f, -ShipCubeSize * 0.08f));

            if (!_ship.PayloadReleased)
            {
                var payloadCenter = TransformLocalPoint(shipCenter, right, up, forward, _ship.PayloadOffset + new Vector3(0f, -PayloadCubeHeight * 0.25f, -ShipCubeSize * 0.1f));
                var payloadLeft = TransformLocalPoint(shipCenter, right, up, forward, _ship.PayloadOffset + new Vector3(-PayloadCubeSize * 0.3f, PayloadCubeHeight * 0.45f, 0f));
                var payloadRight = TransformLocalPoint(shipCenter, right, up, forward, _ship.PayloadOffset + new Vector3(PayloadCubeSize * 0.3f, PayloadCubeHeight * 0.45f, 0f));

                AppendRope(chunk, anchorLeft, payloadLeft);
                AppendRope(chunk, anchorRight, payloadRight);
                AppendPayloadCrate(chunk, payloadCenter);
                return;
            }

            Vector3 ropeLeftEnd = retractedLeft;
            Vector3 ropeRightEnd = retractedRight;
            if (_ship.ReleasedPayloadActive)
            {
                var droppedLeft = TransformLocalPoint(shipCenter, right, up, forward, _ship.ReleasedPayloadOffset + new Vector3(-PayloadCubeSize * 0.3f, PayloadCubeHeight * 0.45f, 0f));
                var droppedRight = TransformLocalPoint(shipCenter, right, up, forward, _ship.ReleasedPayloadOffset + new Vector3(PayloadCubeSize * 0.3f, PayloadCubeHeight * 0.45f, 0f));
                ropeLeftEnd = Vector3.Lerp(retractedLeft, droppedLeft, _ship.RopeExtension);
                ropeRightEnd = Vector3.Lerp(retractedRight, droppedRight, _ship.RopeExtension);

                var droppedCenter = TransformLocalPoint(shipCenter, right, up, forward, _ship.ReleasedPayloadOffset);
                AppendPayloadCrate(chunk, droppedCenter);
            }

            if (Vector3.DistanceSquared(anchorLeft, ropeLeftEnd) > 0.0001f)
            {
                AppendRope(chunk, anchorLeft, ropeLeftEnd);
            }

            if (Vector3.DistanceSquared(anchorRight, ropeRightEnd) > 0.0001f)
            {
                AppendRope(chunk, anchorRight, ropeRightEnd);
            }
        }

        private void AppendPayloadCrate(VoxelChunk chunk, Vector3 payloadCenter)
        {
            AppendAxisAlignedCube(chunk, payloadCenter, PayloadCubeSize * 1.34f, PayloadCubeHeight * 1.18f, PayloadBorderColor);
            AppendAxisAlignedCube(chunk, payloadCenter, PayloadCubeSize * 1.08f, PayloadCubeHeight * 0.92f, PayloadPrimaryColor);
        }

        private void AppendRope(VoxelChunk chunk, Vector3 start, Vector3 end)
        {
            var distance = Vector3.Distance(start, end);
            var segments = Math.Max(3, (int)MathF.Ceiling(distance / (ShipCubeSize * 0.75f)));
            for (var segment = 0; segment <= segments; segment++)
            {
                var t = segment / (float)segments;
                var point = Vector3.Lerp(start, end, t);
                AppendAxisAlignedCube(chunk, point, ShipCubeSize * 0.22f, ShipCubeHeight * 0.3f, RopeColor);
            }
        }

        private void AppendEngineParticles(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            if (_engineParticles == null)
            {
                return;
            }

            for (var index = 0; index < _engineParticles.Length; index++)
            {
                var particle = _engineParticles[index];
                if (!particle.Active)
                {
                    continue;
                }

                var lifeT = MathHelper.Clamp(particle.Age / Math.Max(0.001f, particle.Lifetime), 0f, 1f);
                var color = Color.Lerp(EngineParticleHotColor, EngineParticleCoolColor, lifeT);
                var alpha = (byte)Math.Clamp((1f - lifeT) * 220f, 0f, 255f);
                color = new Color(color.R, color.G, color.B, alpha);
                var size = MathHelper.Lerp(ShipCubeSize * 0.55f, ShipCubeSize * 0.2f, lifeT);
                var position = TransformLocalPoint(center, right, up, forward, particle.Position);
                AppendAxisAlignedCube(chunk, position, size, MathHelper.Max(size * 0.7f, ShipCubeHeight * 0.15f), color);
            }
        }

        private static Vector3 TransformLocalPoint(Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 localOffset)
        {
            return center + (right * localOffset.X) + (up * localOffset.Y) + (forward * localOffset.Z);
        }

        private void AppendShipCube(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 localOffset, Color color, float cubeSize, float cubeHeight)
        {
            AppendAxisAlignedCube(chunk, TransformLocalPoint(center, right, up, forward, localOffset), cubeSize, cubeHeight, color);
        }

        private struct ShipState
        {
            public float CameraDistance;
            public Vector2 Velocity;
            public float Roll;
            public Vector3 PayloadOffset;
            public Vector3 PayloadVelocity;
            public bool PayloadReleased;
            public float RopeExtension;
            public bool ReleasedPayloadActive;
            public Vector3 ReleasedPayloadOffset;
            public Vector3 ReleasedPayloadVelocity;
            public float EngineParticleAccumulator;
        }

        private struct EngineParticle
        {
            public bool Active;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Lifetime;
        }
    }
}
