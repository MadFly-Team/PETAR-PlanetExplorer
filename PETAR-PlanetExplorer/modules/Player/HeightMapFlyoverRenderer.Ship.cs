using System;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
        private const float ShipCubeSize = CubeSize * 0.24f;
        private const float ShipCubeHeight = CubeHeight * 0.26f;
        private const float ShipCameraDistance = CubeSize * 10.5f;
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
        private static readonly Color ShipCockpitColor = new Color(116, 150, 188);
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
            for (var ringZ = -2; ringZ <= 2; ringZ++)
            {
                var ringDepthOffset = ringZ * ShipCubeSize * 0.78f;
                var ringRadius = ringZ == 0 ? 4 : (Math.Abs(ringZ) == 1 ? 3 : 2);
                for (var ringX = -ringRadius; ringX <= ringRadius; ringX++)
                {
                    var edge = Math.Abs(ringX) == ringRadius;
                    var bandColor = edge ? ShipAccentColor : ShipPrimaryColor;
                    AppendShipCube(
                        chunk,
                        center,
                        right,
                        up,
                        forward,
                        new Vector3(ringX * ShipCubeSize * 0.9f, 0f, ringDepthOffset),
                        bandColor,
                        ShipCubeSize * (edge ? 0.95f : 1.08f),
                        ShipCubeHeight * (edge ? 0.8f : 0.92f));
                }
            }

            for (var domeZ = -1; domeZ <= 1; domeZ++)
            {
                for (var domeX = -1; domeX <= 1; domeX++)
                {
                    var domeOffset = new Vector3(domeX * ShipCubeSize * 0.7f, ShipCubeHeight * 0.88f, domeZ * ShipCubeSize * 0.7f);
                    var domeSize = (domeX == 0 && domeZ == 0) ? ShipCubeSize * 1.05f : ShipCubeSize * 0.82f;
                    AppendShipCube(chunk, center, right, up, forward, domeOffset, ShipCockpitColor, domeSize, ShipCubeHeight * 0.8f);
                }
            }

            for (var undersideZ = -1; undersideZ <= 1; undersideZ++)
            {
                for (var undersideX = -2; undersideX <= 2; undersideX++)
                {
                    if (Math.Abs(undersideX) + Math.Abs(undersideZ) > 2)
                    {
                        continue;
                    }

                    AppendShipCube(
                        chunk,
                        center,
                        right,
                        up,
                        forward,
                        new Vector3(undersideX * ShipCubeSize * 0.65f, -ShipCubeHeight * 0.5f, undersideZ * ShipCubeSize * 0.65f),
                        ShipAccentColor,
                        ShipCubeSize * 0.72f,
                        ShipCubeHeight * 0.52f);
                }
            }

            AppendShipCube(chunk, center, right, up, forward, new Vector3(0f, -ShipCubeHeight * 0.72f, 0f), ShipPrimaryColor, ShipCubeSize * 0.95f, ShipCubeHeight * 0.6f);
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
