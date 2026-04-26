using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace PEPAR.Modules.Text
{
    internal sealed class TextDisplayStyle
    {
        internal Color Color { get; init; } = Color.White;
        internal float Scale { get; init; } = 1f;
        internal bool ShadowEnabled { get; init; } = true;
        internal Color ShadowColor { get; init; } = new Color(0, 0, 0, 220);
        internal Vector2 ShadowOffset { get; init; } = new(1f, 1f);
        internal bool Bold { get; init; }
    }

    internal sealed class TextDisplayer
    {
        private SpriteFont? _font;

        internal void Load(ContentManager content, string fontAssetName = "Modules/Text/SpaceMono")
        {
            _font = content.Load<SpriteFont>(fontAssetName);
        }

        internal Vector2 MeasureString(string text, float scale = 1f)
        {
            if (_font is null || string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            return _font.MeasureString(text) * scale;
        }

        internal float GetLineHeight(float scale = 1f)
        {
            return _font is null ? 0f : _font.LineSpacing * scale;
        }

        internal void Draw(SpriteBatch spriteBatch, string? text, Vector2 position, TextDisplayStyle? style = null)
        {
            if (_font is null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            style ??= new TextDisplayStyle();
            var boldOffset = style.Bold ? new Vector2(MathF.Max(0.5f, style.Scale * 0.5f), 0f) : Vector2.Zero;

            if (style.ShadowEnabled)
            {
                spriteBatch.DrawString(_font, text, position + style.ShadowOffset, style.ShadowColor, 0f, Vector2.Zero, style.Scale, SpriteEffects.None, 0f);

                if (style.Bold)
                {
                    spriteBatch.DrawString(_font, text, position + style.ShadowOffset + boldOffset, style.ShadowColor, 0f, Vector2.Zero, style.Scale, SpriteEffects.None, 0f);
                }
            }

            if (style.Bold)
            {
                spriteBatch.DrawString(_font, text, position + boldOffset, style.Color, 0f, Vector2.Zero, style.Scale, SpriteEffects.None, 0f);
            }

            spriteBatch.DrawString(_font, text, position, style.Color, 0f, Vector2.Zero, style.Scale, SpriteEffects.None, 0f);
        }
    }
}
