using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
        private const float TreeTrunkCubeSize = CubeSize * 0.16f;
        private const float TreeTrunkCubeHeight = CubeHeight * 0.12f;
        private const float TreeLeafCubeSize = CubeSize * 0.3f;
        private const float TreeLeafCubeHeight = CubeHeight * 0.55f;
        private static readonly Color[] TreeLeafColors =
        {
            new Color(34, 92, 48),
            new Color(42, 116, 56),
            new Color(54, 136, 66),
            new Color(68, 154, 80),
            new Color(86, 176, 98)
        };
        private static readonly Color TreeTrunkColor = new Color(70, 46, 28);

        private Vector3 GetTreeLocalPosition(int worldX, int worldY)
        {
            return new Vector3(
                ((worldX % ChunkSize) + 0.5f) * CubeSize,
                0f,
                ((worldY % ChunkSize) + 0.5f) * CubeSize);
        }

        private void AppendTree(VoxelChunk chunk, ProceduralWorldMap.TreeInstance tree)
        {
            var localPosition = GetTreeLocalPosition(tree.X, tree.Y);
            var baseColumnHeight = GetColumnHeight(tree.SurfaceHeight);
            var treeBaseY = GetCubeBottom(baseColumnHeight) + FaceOverlap;
            for (var trunkLevel = 0; trunkLevel < tree.TrunkHeight; trunkLevel++)
            {
                AppendTreeCube(chunk, localPosition, treeBaseY + (trunkLevel * TreeTrunkCubeHeight), TreeTrunkCubeSize, TreeTrunkCubeHeight, TreeTrunkColor);
            }

            var canopyBaseY = treeBaseY + (tree.TrunkHeight * TreeTrunkCubeHeight);
            for (var canopyLayer = 0; canopyLayer < tree.CanopyLayers; canopyLayer++)
            {
                var layerRadius = tree.CanopyShape == ProceduralWorldMap.TreeCanopyShape.Round
                    ? System.Math.Max(2, tree.CanopyRadius - (canopyLayer / 3))
                    : System.Math.Max(2, tree.CanopyRadius - (canopyLayer / 2));
                AppendTreeCanopyLayer(chunk, tree, localPosition, canopyBaseY + (canopyLayer * TreeLeafCubeHeight), layerRadius, tree.CanopyShape == ProceduralWorldMap.TreeCanopyShape.Round && canopyLayer == tree.CanopyLayers - 1, canopyLayer);
            }
        }

        private void AppendTreeCanopyLayer(VoxelChunk chunk, ProceduralWorldMap.TreeInstance tree, Vector3 trunkCenter, float bottomY, int radius, bool includeTopBud, int canopyLayer)
        {
            for (var offsetZ = -radius; offsetZ <= radius; offsetZ++)
            {
                for (var offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if ((offsetX * offsetX) + (offsetZ * offsetZ) > (radius * radius) + (radius / 2f))
                    {
                        continue;
                    }

                    var leafCenter = new Vector3(
                        trunkCenter.X + (offsetX * TreeLeafCubeSize),
                        trunkCenter.Y,
                        trunkCenter.Z + (offsetZ * TreeLeafCubeSize));
                    AppendTreeCube(chunk, leafCenter, bottomY, TreeLeafCubeSize, TreeLeafCubeHeight, GetTreeLeafColor(tree, canopyLayer, offsetX, offsetZ));
                }
            }

            if (includeTopBud)
            {
                AppendTreeCube(chunk, trunkCenter, bottomY + TreeLeafCubeHeight, TreeLeafCubeSize, TreeLeafCubeHeight, GetTreeLeafColor(tree, canopyLayer + 1, 0, 0));
            }
        }

        private static Color GetTreeLeafColor(ProceduralWorldMap.TreeInstance tree, int canopyLayer, int offsetX, int offsetZ)
        {
            var baseIndex = System.Math.Clamp(tree.FoliageTone, 0, TreeLeafColors.Length - 1);
            var variation = (int)System.MathF.Round((System.MathF.Sin((tree.X * 0.19f) + (tree.Y * 0.13f) + (canopyLayer * 0.71f) + (offsetX * 0.37f) + (offsetZ * 0.29f)) + 1f));
            return TreeLeafColors[System.Math.Clamp(baseIndex + variation - 1, 0, TreeLeafColors.Length - 1)];
        }

        private void AppendTreeCube(VoxelChunk chunk, Vector3 localPosition, float bottomY, float cubeSize, float cubeHeight, Color color)
        {
            var half = (cubeSize * 0.5f) + (FaceOverlap * 0.1f);
            bottomY -= FaceOverlap * 0.1f;
            var topY = bottomY + cubeHeight + (FaceOverlap * 0.2f);

            var v0 = new Vector3(localPosition.X - half, topY, localPosition.Z - half);
            var v1 = new Vector3(localPosition.X + half, topY, localPosition.Z - half);
            var v2 = new Vector3(localPosition.X + half, topY, localPosition.Z + half);
            var v3 = new Vector3(localPosition.X - half, topY, localPosition.Z + half);
            AppendQuad(chunk, v0, v1, v2, v3, color);

            AppendMiniCubeSide(chunk, localPosition, bottomY, topY, Side.Left, color, half);
            AppendMiniCubeSide(chunk, localPosition, bottomY, topY, Side.Right, color, half);
            AppendMiniCubeSide(chunk, localPosition, bottomY, topY, Side.Back, color, half);
            AppendMiniCubeSide(chunk, localPosition, bottomY, topY, Side.Front, color, half);
        }

        private void AppendMiniCubeSide(VoxelChunk chunk, Vector3 localPosition, float bottomY, float topY, Side side, Color color, float half)
        {
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
    }
}
