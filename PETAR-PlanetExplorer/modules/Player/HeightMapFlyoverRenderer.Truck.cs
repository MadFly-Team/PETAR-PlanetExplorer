using System;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
        private const float TruckModelScale = 0.5f;
        private const float TruckRenderLift = CubeHeight * 0.1f;
        private const float TruckWheelRadius = CubeHeight * 1.02f;
        private const float TruckWheelHalfWidth = CubeSize * 0.28f;
        private const float TruckBodyHalfWidth = CubeSize * 1.2f;
        private const float TruckCameraDistance = CubeSize * 7.4f;
        private const float TruckCameraHeight = CubeHeight * 4.3f;
        private const float TruckCameraLookAhead = CubeSize * 3.8f;
        private const float TruckCameraLookHeight = CubeHeight * 1.5f;
        private static readonly Color TruckPrimaryColor = new Color(222, 224, 228);
        private static readonly Color TruckSecondaryColor = new Color(74, 76, 84);
        private static readonly Color TruckAccentColor = new Color(212, 84, 72);
        private static readonly Color TruckWindowColor = new Color(28, 32, 38);
        private static readonly Color TruckFrameColor = new Color(144, 148, 154);
        private static readonly Color TruckWheelColor = new Color(34, 34, 38);
        private static readonly Color TruckHubColor = new Color(164, 166, 172);
        private static readonly Color TruckLampColor = new Color(244, 240, 212);

        private readonly VoxelChunk _truckChunk = new VoxelChunk();

        private void PopulateTruckChunk(Vector2 cameraPosition, TruckWorldRenderState truck)
        {
            _truckChunk.Reset();
            if (!truck.IsVisible)
            {
                return;
            }

            var center = ToLocalWorldPosition(cameraPosition, truck.Position, truck.WorldY + TruckRenderLift);
            var forward = SafeNormalize(new Vector3(MathF.Cos(truck.Heading), 0f, MathF.Sin(truck.Heading)), Vector3.Forward);
            var right = SafeNormalize(Vector3.Cross(Vector3.Up, forward), Vector3.Right);
            var pitchMatrix = Matrix.CreateFromAxisAngle(right, truck.Pitch);
            var up = SafeNormalize(Vector3.TransformNormal(Vector3.Up, pitchMatrix), Vector3.Up);
            forward = SafeNormalize(Vector3.TransformNormal(forward, pitchMatrix), forward);

            AppendTruckBody(_truckChunk, center, right, up, forward, truck.WheelRotation);
        }

        private void AppendTruckBody(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, float wheelRotation)
        {
            AppendTruckLowerHull(chunk, center, right, up, forward);
            AppendTruckUpperHull(chunk, center, right, up, forward);
            AppendTruckCab(chunk, center, right, up, forward);
            AppendTruckRoofPods(chunk, center, right, up, forward);
            AppendTruckRoofTruss(chunk, center, right, up, forward);
            AppendTruckSideModules(chunk, center, right, up, forward);
            AppendTruckLights(chunk, center, right, up, forward);

            var wheelY = -CubeHeight * 0.95f;
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(-TruckBodyHalfWidth * 1.1f, wheelY, -CubeSize * 2.6f), wheelRotation);
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(TruckBodyHalfWidth * 1.1f, wheelY, -CubeSize * 2.6f), wheelRotation);
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(-TruckBodyHalfWidth * 1.1f, wheelY, CubeSize * 0.05f), wheelRotation);
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(TruckBodyHalfWidth * 1.1f, wheelY, CubeSize * 0.05f), wheelRotation);
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(-TruckBodyHalfWidth * 1.1f, wheelY, CubeSize * 2.8f), wheelRotation);
            AppendTruckWheel(chunk, center, right, up, forward, new Vector3(TruckBodyHalfWidth * 1.1f, wheelY, CubeSize * 2.8f), wheelRotation);
        }

        private void AppendTruckLowerHull(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, -CubeHeight * 0.08f, -CubeSize * 0.28f), TruckBodyHalfWidth * 1.08f, CubeHeight * 0.48f, TruckBodyHalfWidth * 0.72f, CubeHeight * 0.52f, CubeSize * 4.95f, TruckSecondaryColor, TruckFrameColor, MultiplyColor(TruckSecondaryColor, 0.72f));
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, CubeHeight * 0.2f, -CubeSize * 0.38f), TruckBodyHalfWidth * 0.95f, CubeHeight * 0.24f, TruckBodyHalfWidth * 0.82f, CubeHeight * 0.3f, CubeSize * 4.55f, MultiplyColor(TruckSecondaryColor, 0.86f), TruckFrameColor, MultiplyColor(TruckSecondaryColor, 0.68f));
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, -CubeHeight * 0.58f, CubeSize * 0.15f), TruckBodyHalfWidth * 1.02f, CubeHeight * 0.1f, TruckBodyHalfWidth * 0.78f, CubeHeight * 0.08f, CubeSize * 4.45f, MultiplyColor(TruckAccentColor, 0.42f), MultiplyColor(TruckAccentColor, 0.52f), MultiplyColor(TruckAccentColor, 0.3f));
        }

        private void AppendTruckUpperHull(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, CubeHeight * 0.92f, CubeSize * 0.2f), TruckBodyHalfWidth * 0.94f, CubeHeight * 0.82f, TruckBodyHalfWidth * 0.82f, CubeHeight * 0.74f, CubeSize * 4.2f, TruckPrimaryColor, TruckFrameColor, MultiplyColor(TruckSecondaryColor, 0.74f));
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, CubeHeight * 1.05f, CubeSize * 0.25f), TruckBodyHalfWidth * 0.72f, CubeHeight * 0.54f, TruckBodyHalfWidth * 0.7f, CubeHeight * 0.5f, CubeSize * 1.05f, TruckWindowColor, TruckWindowColor, TruckWindowColor);
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, CubeHeight * 1.56f, CubeSize * 0.2f), TruckBodyHalfWidth * 0.86f, CubeHeight * 0.12f, TruckBodyHalfWidth * 0.76f, CubeHeight * 0.12f, CubeSize * 4.05f, TruckFrameColor, TruckFrameColor, TruckFrameColor);
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, CubeHeight * 0.94f, CubeSize * 0.25f), TruckBodyHalfWidth * 0.2f, CubeHeight * 0.94f, TruckBodyHalfWidth * 0.18f, CubeHeight * 0.94f, CubeSize * 0.9f, TruckPrimaryColor, TruckAccentColor, TruckPrimaryColor);
        }

        private void AppendTruckCab(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            AppendTruckWedge(chunk, center, right, up, forward, new Vector3(0f, CubeHeight * 0.76f, CubeSize * 4.15f), TruckBodyHalfWidth * 1.02f, CubeHeight * 1.28f, CubeSize * 2.25f, CubeSize * 0.34f, TruckPrimaryColor, TruckFrameColor);
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, CubeHeight * 1.42f, CubeSize * 3.15f), TruckBodyHalfWidth * 0.8f, CubeHeight * 0.52f, TruckBodyHalfWidth * 0.36f, CubeHeight * 0.22f, CubeSize * 0.92f, TruckWindowColor, TruckWindowColor, TruckWindowColor);
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, CubeHeight * 0.58f, CubeSize * 5.18f), TruckBodyHalfWidth * 0.94f, CubeHeight * 0.18f, TruckBodyHalfWidth * 0.54f, CubeHeight * 0.22f, CubeSize * 0.34f, TruckSecondaryColor, TruckFrameColor, TruckSecondaryColor);
        }

        private void AppendTruckSideModules(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var sideX = side * TruckBodyHalfWidth * 1.02f;
                AppendTruckLoft(chunk, center, right, up, forward, new Vector3(sideX, CubeHeight * 0.8f, CubeSize * 2.65f), CubeSize * 0.44f, CubeHeight * 0.62f, CubeSize * 0.22f, CubeHeight * 0.44f, CubeSize * 1.22f, TruckPrimaryColor, TruckFrameColor, TruckSecondaryColor);
                AppendTruckLoft(chunk, center, right, up, forward, new Vector3(sideX * 0.96f, CubeHeight * 1.02f, CubeSize * 2.65f), CubeSize * 0.26f, CubeHeight * 0.22f, CubeSize * 0.12f, CubeHeight * 0.14f, CubeSize * 0.78f, MultiplyColor(TruckWindowColor, 0.92f), TruckWindowColor, TruckWindowColor);
                AppendOrientedCylinder(chunk, TransformTruckPoint(center, right, up, forward, new Vector3(sideX * 0.98f, CubeHeight * 0.86f, -CubeSize * 1.82f)), forward, right, up, CubeSize * 0.72f, CubeSize * 0.34f, CubeHeight * 0.38f, TruckPrimaryColor, TruckFrameColor, 12);
            }
        }

        private void AppendTruckRoofPods(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            AppendOrientedCylinder(chunk, TransformTruckPoint(center, right, up, forward, new Vector3(0f, CubeHeight * 2.2f, -CubeSize * 1.55f)), forward, right, up, CubeSize * 0.82f, CubeSize * 0.84f, CubeHeight * 0.3f, TruckPrimaryColor, TruckAccentColor, 14);
            AppendOrientedCylinder(chunk, TransformTruckPoint(center, right, up, forward, new Vector3(0f, CubeHeight * 2.2f, CubeSize * 1.9f)), forward, right, up, CubeSize * 0.8f, CubeSize * 0.8f, CubeHeight * 0.28f, TruckPrimaryColor, TruckAccentColor, 14);
        }

        private void AppendTruckRoofTruss(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            var beamColor = MultiplyColor(TruckFrameColor, 1.1f);
            var startLeft = TransformTruckPoint(center, right, up, forward, new Vector3(-TruckBodyHalfWidth * 0.78f, CubeHeight * 1.92f, CubeSize * 3.35f));
            var startRight = TransformTruckPoint(center, right, up, forward, new Vector3(TruckBodyHalfWidth * 0.78f, CubeHeight * 1.92f, CubeSize * 3.35f));
            var endLeft = TransformTruckPoint(center, right, up, forward, new Vector3(-TruckBodyHalfWidth * 0.76f, CubeHeight * 1.92f, -CubeSize * 2.8f));
            var endRight = TransformTruckPoint(center, right, up, forward, new Vector3(TruckBodyHalfWidth * 0.76f, CubeHeight * 1.92f, -CubeSize * 2.8f));
            AppendTruckBeam(chunk, startLeft, endLeft, CubeSize * 0.12f, beamColor);
            AppendTruckBeam(chunk, startRight, endRight, CubeSize * 0.12f, beamColor);
            AppendTruckBeam(chunk, startLeft, startRight, CubeSize * 0.08f, beamColor);
            AppendTruckBeam(chunk, endLeft, endRight, CubeSize * 0.08f, beamColor);

            for (var index = 0; index < 5; index++)
            {
                var t0 = index / 5f;
                var t1 = (index + 1) / 5f;
                var leftA = Vector3.Lerp(startLeft, endLeft, t0);
                var leftB = Vector3.Lerp(startLeft, endLeft, t1);
                var rightA = Vector3.Lerp(startRight, endRight, t0);
                var rightB = Vector3.Lerp(startRight, endRight, t1);
                AppendTruckBeam(chunk, leftA, rightB, CubeSize * 0.06f, beamColor);
                AppendTruckBeam(chunk, rightA, leftB, CubeSize * 0.06f, beamColor);
            }
        }

        private void AppendTruckLights(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward)
        {
            AppendTruckLoft(chunk, center, right, up, forward, new Vector3(0f, CubeHeight * 0.32f, CubeSize * 5.56f), CubeSize * 0.92f, CubeHeight * 0.08f, CubeSize * 0.74f, CubeHeight * 0.08f, CubeSize * 0.06f, TruckLampColor, TruckLampColor, TruckLampColor);
            AppendOrientedCylinder(chunk, TransformTruckPoint(center, right, up, forward, new Vector3(-TruckBodyHalfWidth * 0.86f, CubeHeight * 0.06f, CubeSize * 5.46f)), right, up, forward, CubeSize * 0.12f, CubeHeight * 0.16f, CubeHeight * 0.16f, TruckLampColor, TruckLampColor, 10);
            AppendOrientedCylinder(chunk, TransformTruckPoint(center, right, up, forward, new Vector3(TruckBodyHalfWidth * 0.86f, CubeHeight * 0.06f, CubeSize * 5.46f)), right, up, forward, CubeSize * 0.12f, CubeHeight * 0.16f, CubeHeight * 0.16f, TruckLampColor, TruckLampColor, 10);
        }

        private void AppendTruckWheel(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 localOffset, float wheelRotation)
        {
            var wheelCenter = TransformTruckPoint(center, right, up, forward, localOffset);
            AppendOrientedCylinder(chunk, wheelCenter, right, up, forward, TruckWheelHalfWidth, TruckWheelRadius, TruckWheelRadius, TruckWheelColor, MultiplyColor(TruckWheelColor, 0.88f), 16);
            AppendOrientedCylinder(chunk, wheelCenter, right, up, forward, TruckWheelHalfWidth * 0.72f, TruckWheelRadius * 0.62f, TruckWheelRadius * 0.62f, TruckHubColor, MultiplyColor(TruckHubColor, 0.88f), 14);

            for (var side = -1; side <= 1; side += 2)
            {
                var faceCenter = wheelCenter + (right * (side * ScaleTruck(TruckWheelHalfWidth * 0.94f)));
                for (var spoke = 0; spoke < 5; spoke++)
                {
                    var angle = wheelRotation + (spoke * MathHelper.TwoPi / 5f);
                    var spokeTip = faceCenter + (forward * MathF.Cos(angle) * ScaleTruck(TruckWheelRadius * 0.58f)) + (up * MathF.Sin(angle) * ScaleTruck(TruckWheelRadius * 0.58f));
                    AppendTruckBeam(chunk, faceCenter, spokeTip, CubeSize * 0.08f, TruckFrameColor);
                }
            }
        }

        private void AppendTruckWedge(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 localCenter, float rearHalfWidth, float height, float length, float frontHalfWidth, Color topColor, Color sideColor)
        {
            localCenter = ScaleTruck(localCenter);
            rearHalfWidth = ScaleTruck(rearHalfWidth);
            height = ScaleTruck(height);
            length = ScaleTruck(length);
            frontHalfWidth = ScaleTruck(frontHalfWidth);
            var bottomY = -height * 0.5f;
            var topY = height * 0.5f;
            var backZ = -length * 0.5f;
            var frontZ = length * 0.5f;
            var rearLeftBottom = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(-rearHalfWidth, bottomY, backZ));
            var rearRightBottom = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(rearHalfWidth, bottomY, backZ));
            var rearLeftTop = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(-rearHalfWidth * 0.82f, topY, backZ + (length * 0.08f)));
            var rearRightTop = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(rearHalfWidth * 0.82f, topY, backZ + (length * 0.08f)));
            var frontLeftBottom = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(-frontHalfWidth, bottomY * 0.88f, frontZ));
            var frontRightBottom = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(frontHalfWidth, bottomY * 0.88f, frontZ));
            var frontLeftTop = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(-frontHalfWidth * 0.66f, topY * 0.28f, frontZ));
            var frontRightTop = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(frontHalfWidth * 0.66f, topY * 0.28f, frontZ));

            AppendQuad(chunk, rearLeftTop, rearRightTop, frontRightTop, frontLeftTop, topColor);
            AppendQuad(chunk, rearRightBottom, rearLeftBottom, frontLeftBottom, frontRightBottom, MultiplyColor(sideColor, 0.68f));
            AppendQuad(chunk, rearLeftBottom, rearLeftTop, frontLeftTop, frontLeftBottom, sideColor);
            AppendQuad(chunk, rearRightTop, rearRightBottom, frontRightBottom, frontRightTop, sideColor);
            AppendQuad(chunk, rearLeftBottom, rearRightBottom, rearRightTop, rearLeftTop, MultiplyColor(sideColor, 0.88f));
            AppendQuad(chunk, frontLeftTop, frontRightTop, frontRightBottom, frontLeftBottom, MultiplyColor(sideColor, 0.8f));
        }

        private void AppendTruckLoft(VoxelChunk chunk, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 localCenter, float rearHalfWidth, float rearHalfHeight, float frontHalfWidth, float frontHalfHeight, float halfLength, Color topColor, Color sideColor, Color bottomColor)
        {
            localCenter = ScaleTruck(localCenter);
            rearHalfWidth = ScaleTruck(rearHalfWidth);
            rearHalfHeight = ScaleTruck(rearHalfHeight);
            frontHalfWidth = ScaleTruck(frontHalfWidth);
            frontHalfHeight = ScaleTruck(frontHalfHeight);
            halfLength = ScaleTruck(halfLength);
            var lbb = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(-rearHalfWidth, -rearHalfHeight, -halfLength));
            var rbb = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(rearHalfWidth, -rearHalfHeight, -halfLength));
            var lbf = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(-frontHalfWidth, -frontHalfHeight, halfLength));
            var rbf = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(frontHalfWidth, -frontHalfHeight, halfLength));
            var ltb = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(-rearHalfWidth, rearHalfHeight, -halfLength));
            var rtb = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(rearHalfWidth, rearHalfHeight, -halfLength));
            var ltf = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(-frontHalfWidth, frontHalfHeight, halfLength));
            var rtf = TransformLocalPoint(center, right, up, forward, localCenter + new Vector3(frontHalfWidth, frontHalfHeight, halfLength));

            AppendQuad(chunk, ltf, rtf, rtb, ltb, topColor);
            AppendQuad(chunk, rbb, rbf, lbf, lbb, bottomColor);
            AppendQuad(chunk, lbf, ltf, ltb, lbb, sideColor);
            AppendQuad(chunk, rtb, rtf, rbf, rbb, sideColor);
            AppendQuad(chunk, ltb, rtb, rbb, lbb, MultiplyColor(sideColor, 0.84f));
            AppendQuad(chunk, rtf, ltf, lbf, rbf, MultiplyColor(sideColor, 1.04f));
        }

        private void AppendOrientedCylinder(VoxelChunk chunk, Vector3 center, Vector3 axis, Vector3 radialX, Vector3 radialY, float halfLength, float radiusX, float radiusY, Color shellColor, Color capColor, int segments)
        {
            axis = SafeNormalize(axis, Vector3.Forward);
            radialX = SafeNormalize(radialX, Vector3.Right);
            radialY = SafeNormalize(radialY, Vector3.Up);
            halfLength = ScaleTruck(halfLength);
            radiusX = ScaleTruck(radiusX);
            radiusY = ScaleTruck(radiusY);
            var startCenter = center - (axis * halfLength);
            var endCenter = center + (axis * halfLength);

            for (var index = 0; index < segments; index++)
            {
                var angle0 = index * MathHelper.TwoPi / segments;
                var angle1 = (index + 1) * MathHelper.TwoPi / segments;
                var ring0 = (radialX * MathF.Cos(angle0) * radiusX) + (radialY * MathF.Sin(angle0) * radiusY);
                var ring1 = (radialX * MathF.Cos(angle1) * radiusX) + (radialY * MathF.Sin(angle1) * radiusY);
                var s0 = startCenter + ring0;
                var s1 = startCenter + ring1;
                var e0 = endCenter + ring0;
                var e1 = endCenter + ring1;
                AppendQuad(chunk, s0, e0, e1, s1, shellColor);
                chunk.AppendTriangle(startCenter, s1, s0, capColor);
                chunk.AppendTriangle(endCenter, e0, e1, capColor);
            }
        }

        private void AppendTruckBeam(VoxelChunk chunk, Vector3 start, Vector3 end, float thickness, Color color)
        {
            thickness = ScaleTruck(thickness);
            var direction = SafeNormalize(end - start, Vector3.Forward);
            var beamRight = SafeNormalize(Vector3.Cross(Vector3.Up, direction), Vector3.Right) * (thickness * 0.5f);
            var beamUp = SafeNormalize(Vector3.Cross(direction, beamRight), Vector3.Up) * (thickness * 0.5f);
            var s0 = start - beamRight + beamUp;
            var s1 = start + beamRight + beamUp;
            var s2 = start + beamRight - beamUp;
            var s3 = start - beamRight - beamUp;
            var e0 = end - beamRight + beamUp;
            var e1 = end + beamRight + beamUp;
            var e2 = end + beamRight - beamUp;
            var e3 = end - beamRight - beamUp;
            AppendQuad(chunk, s0, e0, e1, s1, color);
            AppendQuad(chunk, s1, e1, e2, s2, MultiplyColor(color, 0.92f));
            AppendQuad(chunk, s2, e2, e3, s3, MultiplyColor(color, 0.78f));
            AppendQuad(chunk, s3, e3, e0, s0, MultiplyColor(color, 0.86f));
        }

        private static float ScaleTruck(float value)
        {
            return value * TruckModelScale;
        }

        private static Vector3 ScaleTruck(Vector3 value)
        {
            return value * TruckModelScale;
        }

        private static Vector3 TransformTruckPoint(Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 localOffset)
        {
            return TransformLocalPoint(center, right, up, forward, ScaleTruck(localOffset));
        }
    }
}
