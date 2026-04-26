using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed partial class HeightMapFlyoverRenderer
    {
        private static readonly Color MissileBodyColor = new Color(238, 238, 244);
        private static readonly Color MissileNoseColor = new Color(255, 120, 72);
        private static readonly Color MissileDebrisColor = new Color(96, 88, 82);

        private readonly VoxelChunk[] _missileChunks = new VoxelChunk[1] { new VoxelChunk() };
        private readonly VoxelChunk[] _missileDebrisChunks = new VoxelChunk[1] { new VoxelChunk() };

        public void PopulateMissileEffects(Vector2 cameraPosition, MissileWorldRenderState? missile, IReadOnlyList<MissileDebrisParticle> debris)
        {
            _missileChunks[0].Reset();
            _missileDebrisChunks[0].Reset();

            if (missile.HasValue)
            {
                var localPosition = ToLocalWorldPosition(cameraPosition, missile.Value.Position, missile.Value.WorldY);
                AppendMissile(_missileChunks[0], new MissileRenderState(localPosition, missile.Value.Direction));
            }

            if (debris == null)
            {
                return;
            }

            for (var index = 0; index < debris.Count; index++)
            {
                var localPosition = ToLocalWorldPosition(cameraPosition, debris[index].Position, debris[index].WorldY);
                AppendMissileDebris(_missileDebrisChunks[0], new MissileDebrisRenderState(localPosition, debris[index].Size, debris[index].Alpha));
            }
        }

        public void DrawMissileEffects()
        {
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                if (_missileChunks[0].VertexCount > 0)
                {
                    _graphicsDevice.BlendState = BlendState.Opaque;
                    _graphicsDevice.DepthStencilState = DepthStencilState.Default;
                    _effect.World = Matrix.Identity;
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _missileChunks[0].Vertices, 0, _missileChunks[0].VertexCount / 3);
                }

                if (_missileDebrisChunks[0].VertexCount > 0)
                {
                    _graphicsDevice.BlendState = BlendState.AlphaBlend;
                    _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                    _effect.World = Matrix.Identity;
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _missileDebrisChunks[0].Vertices, 0, _missileDebrisChunks[0].VertexCount / 3);
                }
            }
        }

        private void AppendMissile(VoxelChunk chunk, MissileRenderState missile)
        {
            var forward = SafeNormalize(missile.Direction, Vector3.Forward);
            var right = SafeNormalize(Vector3.Cross(Vector3.Up, forward), Vector3.Right);
            var up = SafeNormalize(Vector3.Cross(forward, right), Vector3.Up);
            AppendAxisAlignedCube(chunk, missile.Position, CubeSize * 0.22f, CubeHeight * 0.32f, MissileBodyColor);
            AppendAxisAlignedCube(chunk, missile.Position + (forward * CubeSize * 0.24f) + (up * CubeHeight * 0.08f), CubeSize * 0.14f, CubeHeight * 0.22f, MissileNoseColor);
            AppendAxisAlignedCube(chunk, missile.Position - (forward * CubeSize * 0.2f), CubeSize * 0.1f, CubeHeight * 0.14f, MissileNoseColor);
            AppendAxisAlignedCube(chunk, missile.Position + (right * CubeSize * 0.18f), CubeSize * 0.08f, CubeHeight * 0.08f, MissileBodyColor);
            AppendAxisAlignedCube(chunk, missile.Position - (right * CubeSize * 0.18f), CubeSize * 0.08f, CubeHeight * 0.08f, MissileBodyColor);
        }

        private void AppendMissileDebris(VoxelChunk chunk, MissileDebrisRenderState debris)
        {
            var alpha = (byte)Math.Clamp(debris.Alpha * 255f, 0f, 255f);
            AppendAxisAlignedCube(chunk, debris.Position, debris.Size, MathHelper.Max(debris.Size * 0.7f, CubeHeight * 0.08f), new Color(MissileDebrisColor.R, MissileDebrisColor.G, MissileDebrisColor.B, alpha));
        }

        private Vector3 ToLocalWorldPosition(Vector2 cameraPosition, Vector2 worldPosition, float worldY)
        {
            var wrappedOffset = GetWrappedOffset(worldPosition - cameraPosition);
            return new Vector3(wrappedOffset.X * CubeSize, worldY, wrappedOffset.Y * CubeSize);
        }

        private readonly record struct MissileRenderState(Vector3 Position, Vector3 Direction);

        private readonly record struct MissileDebrisRenderState(Vector3 Position, float Size, float Alpha);

        public readonly record struct MissileWorldRenderState(Vector2 Position, float WorldY, Vector3 Direction);

        public readonly record struct MissileDebrisParticle(Vector2 Position, float WorldY, float Size, float Alpha);
    }
}
