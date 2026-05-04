using System;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
        private const float TruckCubeSize = CubeSize * 0.125f;
        private const float TruckCubeHeight = CubeHeight * 0.125f;
        private const float TruckBodyLengthStep = TruckCubeSize * 0.92f;
        private const float TruckBodyWidthStep = TruckCubeSize * 0.94f;
        private const float TruckBodyHeightStep = TruckCubeHeight * 0.95f;
        private const float TruckCameraDistance = CubeSize * 7.4f;
        private const float TruckCameraHeight = CubeHeight * 4.3f;
        private const float TruckCameraLookAhead = CubeSize * 3.8f;
        private const float TruckCameraLookHeight = CubeHeight * 1.5f;
        private static readonly Color TruckBodyColor = new Color(156, 164, 172);
        private static readonly Color TruckCabColor = new Color(208, 118, 62);
        private static readonly Color TruckWindowColor = new Color(116, 156, 196);
        private static readonly Color TruckTrimColor = new Color(62, 64, 68);
        private static readonly Color TruckWheelColor = new Color(28, 28, 30);
        private static readonly Color TruckHubColor = new Color(142, 142, 148);

        private readonly VoxelChunk _truckChunk = new VoxelChunk();

        private void PopulateTruckChunk(Vector2 cameraPosition, TruckWorldRenderState truck)
        {
            _truckChunk.Reset();
            if (!truck.IsVisible)
            {
                return;
            }

            var center = ToLocalWorldPosition(cameraPosition, truck.Position, truck.WorldY);
            var forward = SafeNormalize(new Vector3(MathF.Cos(truck.Heading), 0f, MathF.Sin(truck.Heading)), Vector3.Forward);
            var right = SafeNormalize(Vector3.Cross(Vector3.Up, forward), Vector3.Right);
            var pitchMatrix = Matrix.CreateFromAxisAngle(right, truck.Pitch);
            var up = SafeNormalize(Vector3.TransformNormal(Vector3.Up, pitchMatrix), Vector3.Up);
            forward = SafeNormalize(Vector3.TransformNormal(forward, pitchMatrix), forward);

            AppendTruckBody(_truckChunk, center, right, up, forward, truck.WheelRotation);
        }

        private void AppendTruckBody(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, float wheelRotation)
        {
            for (var bedZ = -10; bedZ <= 6; bedZ++)
            {
                for (var bedX = -2; bedX <= 2; bedX++)
                {
                    AppendOrientedCube(
                        chunk,
                        center,
                        right,
                        up,
                        forward,
                        new Vector3(bedX * TruckBodyWidthStep, TruckBodyHeightStep * 1.2f, bedZ * TruckBodyLengthStep),
                        TruckBodyColor,
                        TruckCubeSize,
                        TruckCubeHeight);
                }
            }

            for (var railZ = -10; railZ <= 4; railZ += 2)
            {
                AppendOrientedCube(chunk, center, right, up, forward, new Vector3(-3f * TruckBodyWidthStep, TruckBodyHeightStep * 2.15f, railZ * TruckBodyLengthStep), TruckTrimColor, TruckCubeSize * 0.82f, TruckCubeHeight * 0.92f);
                AppendOrientedCube(chunk, center, right, up, forward, new Vector3(3f * TruckBodyWidthStep, TruckBodyHeightStep * 2.15f, railZ * TruckBodyLengthStep), TruckTrimColor, TruckCubeSize * 0.82f, TruckCubeHeight * 0.92f);
            }

            for (var cabZ = 7; cabZ <= 10; cabZ++)
            {
                for (var cabX = -2; cabX <= 2; cabX++)
                {
                    for (var cabY = 1; cabY <= 4; cabY++)
                    {
                        var isWindow = cabY >= 3 && Math.Abs(cabX) <= 1 && cabZ >= 8;
                        var color = isWindow ? TruckWindowColor : TruckCabColor;
                        AppendOrientedCube(
                            chunk,
                            center,
                            right,
                            up,
                            forward,
                            new Vector3(cabX * TruckBodyWidthStep, cabY * TruckBodyHeightStep, cabZ * TruckBodyLengthStep),
                            color,
                            TruckCubeSize,
                            TruckCubeHeight);
                    }
                }
            }

            AppendOrientedCube(chunk, center, right, up, forward, new Vector3(0f, TruckBodyHeightStep * 4.7f, 11f * TruckBodyLengthStep), TruckCabColor, TruckCubeSize * 1.6f, TruckCubeHeight * 0.88f);
            AppendOrientedCube(chunk, center, right, up, forward, new Vector3(0f, TruckBodyHeightStep * 1.05f, -11f * TruckBodyLengthStep), TruckTrimColor, TruckCubeSize * 2.6f, TruckCubeHeight * 0.72f);

            var wheelYOffset = -TruckCubeHeight * 1.35f;
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(-3.45f * TruckBodyWidthStep, wheelYOffset, -7.2f * TruckBodyLengthStep), wheelRotation);
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(3.45f * TruckBodyWidthStep, wheelYOffset, -7.2f * TruckBodyLengthStep), wheelRotation);
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(-3.45f * TruckBodyWidthStep, wheelYOffset, 7.8f * TruckBodyLengthStep), wheelRotation);
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(3.45f * TruckBodyWidthStep, wheelYOffset, 7.8f * TruckBodyLengthStep), wheelRotation);
        }

        private void AppendTruckWheel(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 localOffset, float wheelRotation)
        {
            var wheelCenter = TransformLocalPoint(center, right, up, forward, localOffset);
            var tireRadius = TruckCubeHeight * 3.2f;
            var tireHalfWidth = TruckCubeSize * 0.82f;
            var treadSize = TruckCubeSize * 1.15f;
            var treadHeight = TruckCubeHeight * 1.15f;

            for (var side = -1; side <= 1; side += 2)
            {
                var sideOffset = right * (side * tireHalfWidth);
                for (var segment = 0; segment < 8; segment++)
                {
                    var angle = wheelRotation + (segment * MathHelper.TwoPi / 8f);
                    var radialOffset = (forward * MathF.Cos(angle) * tireRadius) + (up * MathF.Sin(angle) * tireRadius);
                    AppendAxisAlignedCube(chunk, wheelCenter + sideOffset + radialOffset, treadSize, treadHeight, TruckWheelColor);
                }
            }

            AppendAxisAlignedCube(chunk, wheelCenter, TruckCubeSize * 0.95f, TruckCubeHeight * 0.95f, TruckHubColor);
        }
        private void AppendOrientedCube(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 localOffset, Color color, float cubeSize, float cubeHeight)
        {
            AppendAxisAlignedCube(chunk, TransformLocalPoint(center, right, up, forward, localOffset), cubeSize, cubeHeight, color);
        }
    }
}