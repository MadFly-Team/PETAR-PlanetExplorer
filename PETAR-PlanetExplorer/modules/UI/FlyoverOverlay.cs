using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PETAR_PlanetExplorer.Modules.UI
{
    public sealed class FlyoverOverlay
    {
        public void DrawLoadingScreen(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Viewport viewport, LoadingOverlayModel model)
        {
            var center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
            var title = "GENERATING PROCEDURAL WORLD";
            var titleOrigin = font.MeasureString(title) * 0.5f;
            var percentText = $"{model.Progress * 100f:000}%";
            var percentOrigin = font.MeasureString(percentText) * 0.5f;
            var statusOrigin = font.MeasureString(model.Status) * 0.5f;
            var barWidth = Math.Min(620, viewport.Width - 120);
            var barRectangle = new Rectangle((int)(center.X - (barWidth * 0.5f)), (int)center.Y - 12, barWidth, 24);
            var fillWidth = (int)((barRectangle.Width - 8) * MathHelper.Clamp(model.Progress, 0f, 1f));
            var fillRectangle = new Rectangle(barRectangle.X + 4, barRectangle.Y + 4, fillWidth, barRectangle.Height - 8);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(4, 10, 26));
            spriteBatch.Draw(pixel, barRectangle, new Color(12, 24, 50, 220));
            if (fillRectangle.Width > 0)
            {
                spriteBatch.Draw(pixel, fillRectangle, Color.Lerp(new Color(0, 180, 255), new Color(140, 60, 255), model.Progress));
            }

            DrawRectangleFrame(spriteBatch, pixel, barRectangle, new Color(0, 220, 255), 2);
            DrawStyledText(spriteBatch, font, title, new Vector2(center.X, center.Y - 94f), model.GlowColor, model.AccentColor, model.ShadowColor, titleOrigin, 0.52f);
            DrawStyledText(spriteBatch, font, model.Status, new Vector2(center.X, center.Y - 42f), model.GlowColor * 0.35f, model.SubtitleColor, model.ShadowColor, statusOrigin, 0.3f);
            DrawStyledText(spriteBatch, font, percentText, new Vector2(center.X, center.Y + 36f), model.GlowColor * 0.35f, model.AccentColor, model.ShadowColor, percentOrigin, 0.34f);
            spriteBatch.End();
        }

        public void DrawHud(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Viewport viewport, FlyoverHudModel model)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            if (model.DrawCenteredShipMarker)
            {
                DrawCenteredShipMarker(spriteBatch, pixel, new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f));
            }

            spriteBatch.Draw(pixel, model.MinimapRectangle, new Color(3, 8, 18, 200));
            spriteBatch.Draw(model.HeightMapTexture, model.MinimapRectangle, Color.White * 0.92f);
            DrawRectangleFrame(spriteBatch, pixel, model.MinimapRectangle, new Color(0, 220, 255), 2);
            DrawFlightMarker(spriteBatch, pixel, model.MinimapRectangle, model.FlightPosition, model.WorldSize, new Color(255, 234, 112));

            DrawStyledText(spriteBatch, font, model.Title, model.TitlePosition, model.GlowColor, model.AccentColor, model.ShadowColor, model.TitleOrigin, 0.48f);
            DrawStyledText(spriteBatch, font, model.ControlsText, model.ControlsPosition, model.GlowColor * 0.45f, model.SubtitleColor, model.ShadowColor, model.ControlsOrigin, 0.26f);
            DrawStyledText(spriteBatch, font, model.AltitudeText, model.AltitudePosition, model.GlowColor * 0.35f, model.AccentColor, model.ShadowColor, Vector2.Zero, 0.22f);

            spriteBatch.End();
        }

        private static void DrawCenteredShipMarker(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center)
        {
            var shipColor = Color.Lerp(new Color(255, 232, 140), Color.White, 0.35f);
            spriteBatch.Draw(pixel, new Rectangle((int)center.X - 2, (int)center.Y - 14, 4, 28), shipColor);
            spriteBatch.Draw(pixel, new Rectangle((int)center.X - 14, (int)center.Y - 2, 28, 4), shipColor);
            spriteBatch.Draw(pixel, new Rectangle((int)center.X - 1, (int)center.Y - 26, 2, 14), new Color(255, 120, 60));
        }

        private static void DrawFlightMarker(SpriteBatch spriteBatch, Texture2D pixel, Rectangle minimapRectangle, Vector2 flightPosition, Point worldSize, Color markerColor)
        {
            var markerX = minimapRectangle.X + (int)((flightPosition.X / worldSize.X) * minimapRectangle.Width);
            var markerY = minimapRectangle.Y + (int)((flightPosition.Y / worldSize.Y) * minimapRectangle.Height);
            var markerRectangle = new Rectangle(markerX - 3, markerY - 3, 6, 6);

            spriteBatch.Draw(pixel, markerRectangle, markerColor);
            spriteBatch.Draw(pixel, new Rectangle(markerX - 8, markerY, 16, 1), markerColor);
            spriteBatch.Draw(pixel, new Rectangle(markerX, markerY - 8, 1, 16), markerColor);
        }

        private static void DrawRectangleFrame(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rectangle, Color color, int thickness)
        {
            spriteBatch.Draw(pixel, new Rectangle(rectangle.Left, rectangle.Top, rectangle.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rectangle.Left, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rectangle.Left, rectangle.Top, thickness, rectangle.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rectangle.Right - thickness, rectangle.Top, thickness, rectangle.Height), color);
        }

        private static void DrawStyledText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color glowColor, Color mainColor, Color shadowColor, Vector2 origin, float scale)
        {
            var glowOffsets = new[]
            {
                new Vector2(-4f, 0f),
                new Vector2(4f, 0f),
                new Vector2(0f, -4f),
                new Vector2(0f, 4f)
            };

            spriteBatch.DrawString(font, text, position + new Vector2(4f, 4f), shadowColor, 0f, origin, scale, SpriteEffects.None, 0f);
            foreach (var offset in glowOffsets)
            {
                spriteBatch.DrawString(font, text, position + offset, glowColor, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            spriteBatch.DrawString(font, text, position, mainColor, 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }

    public readonly record struct LoadingOverlayModel(float Progress, string Status, Color GlowColor, Color AccentColor, Color SubtitleColor, Color ShadowColor);

    public readonly record struct FlyoverHudModel(
        Rectangle MinimapRectangle,
        string Title,
        Vector2 TitlePosition,
        Vector2 TitleOrigin,
        string ControlsText,
        Vector2 ControlsPosition,
        Vector2 ControlsOrigin,
        string AltitudeText,
        Vector2 AltitudePosition,
        string ThemeName,
        Color GlowColor,
        Color AccentColor,
        Color SubtitleColor,
        Color ShadowColor,
        Texture2D HeightMapTexture,
        Vector2 FlightPosition,
        Point WorldSize,
        bool DrawCenteredShipMarker);
}
