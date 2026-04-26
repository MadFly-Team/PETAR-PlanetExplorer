using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PETAR_PlanetExplorer.Modules.Maps;

namespace PETAR_PlanetExplorer.Modules.UI
{
    public sealed class GenerationDialog
    {
        private const float FontScale = 0.33333334f;
        private const int SliderStepCount = 20;
        private const int SliderLeft = 200;
        private const int SliderWidth = 250;
        private readonly SliderField[] _sliderFields;
        private readonly OptionField _themeField;
        private MouseState _previousMouseState;
        private string _seedText;
        private int _selectedIndex;
        private Rectangle _saveButtonBounds;
        private Rectangle _loadButtonBounds;
        private Rectangle _generateButtonBounds;
        private Rectangle _cancelButtonBounds;
        private Rectangle _themeTrackBounds;
        private readonly Rectangle[] _sliderTrackBounds;

        public GenerationDialog(WorldGenerationSettings settings)
        {
            Settings = settings ?? WorldGenerationSettings.Default;
            _themeField = new OptionField("Theme", PlanetTheme.All, Settings.Theme);
            _sliderFields =
            [
                new SliderField("Mountains", Settings.MountainIntensity),
                new SliderField("Plateaus", Settings.PlateauIntensity),
                new SliderField("Volcanoes", Settings.VolcanoIntensity),
                new SliderField("Craters", Settings.CraterIntensity),
                new SliderField("Gorges", Settings.GorgeIntensity),
                new SliderField("Max Columns", GetMaxColumnSliderValue(Settings.MaxCubeColumn))
            ];
            _sliderTrackBounds = new Rectangle[_sliderFields.Length];
            _seedText = Settings.Seed.ToString();
        }

        public WorldGenerationSettings Settings { get; private set; }

        public bool IsOpen { get; private set; }

        public void Open(WorldGenerationSettings settings)
        {
            Settings = settings ?? WorldGenerationSettings.Default;
            _themeField.SelectedOption = Settings.Theme;
            _sliderFields[0].Value = Settings.MountainIntensity;
            _sliderFields[1].Value = Settings.PlateauIntensity;
            _sliderFields[2].Value = Settings.VolcanoIntensity;
            _sliderFields[3].Value = Settings.CraterIntensity;
            _sliderFields[4].Value = Settings.GorgeIntensity;
            _sliderFields[5].Value = GetMaxColumnSliderValue(Settings.MaxCubeColumn);
            _seedText = Settings.Seed.ToString();
            _selectedIndex = 0;
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public DialogResult HandleInput(KeyboardState current, KeyboardState previous, MouseState mouseState)
        {
            if (!IsOpen)
            {
                return DialogResult.None;
            }

            var mouseResult = HandleMouseInput(mouseState);
            if (mouseResult != DialogResult.None)
            {
                _previousMouseState = mouseState;
                return mouseResult;
            }

            if (Pressed(current, previous, Keys.Escape))
            {
                Close();
                return DialogResult.Cancelled;
            }

            if (Pressed(current, previous, Keys.Up))
            {
                _selectedIndex = (_selectedIndex + FieldCount - 1) % FieldCount;
            }
            else if (Pressed(current, previous, Keys.Down))
            {
                _selectedIndex = (_selectedIndex + 1) % FieldCount;
            }
            else if (Pressed(current, previous, Keys.Left))
            {
                AdjustSelectedField(-1);
            }
            else if (Pressed(current, previous, Keys.Right))
            {
                AdjustSelectedField(1);
            }
            else if (Pressed(current, previous, Keys.Enter))
            {
                ApplySettings();
                Close();
                return DialogResult.Accepted;
            }

            HandleSeedTyping(current, previous);

            ApplySettings();
            _previousMouseState = mouseState;
            return DialogResult.None;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Viewport viewport)
        {
            if (!IsOpen)
            {
                return;
            }

            var dialogWidth = 600;
            var dialogHeight = 520;
            var dialogBounds = new Rectangle(
                (viewport.Width - dialogWidth) / 2,
                (viewport.Height - dialogHeight) / 2,
                dialogWidth,
                dialogHeight);
            var contentX = dialogBounds.X + 26;
            var contentY = dialogBounds.Y + 22;
            var rowHeight = 42;
            var labelColor = new Color(220, 228, 238);
            var selectedColor = new Color(255, 240, 164);
            var sliderTrackColor = new Color(58, 78, 106);
            var sliderFillColor = new Color(108, 194, 255);
            var backgroundColor = new Color(10, 16, 24, 228);
            var borderColor = new Color(0, 220, 255);

            spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 140));
            spriteBatch.Draw(pixel, dialogBounds, backgroundColor);
            DrawFrame(spriteBatch, pixel, dialogBounds, borderColor, 2);
            spriteBatch.DrawString(font, "Custom World Generation", new Vector2(contentX, contentY), Color.White, 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);

            DrawSeedField(spriteBatch, font, pixel, contentX, contentY + 54, labelColor, selectedColor, sliderTrackColor);
            DrawThemeField(spriteBatch, font, pixel, contentX, contentY + 54 + rowHeight, labelColor, selectedColor, sliderTrackColor, sliderFillColor);
            for (var index = 0; index < _sliderFields.Length; index++)
            {
                DrawSliderField(spriteBatch, font, pixel, contentX, contentY + 54 + ((index + 2) * rowHeight), _sliderFields[index], index + 2, labelColor, selectedColor, sliderTrackColor, sliderFillColor);
            }

            DrawActionButtons(spriteBatch, font, pixel, dialogBounds, contentY, rowHeight, labelColor, selectedColor);
            spriteBatch.DrawString(font, "Up/Down select  Left/Right adjust  Mouse drag sliders  Enter generate  Esc cancel", new Vector2(contentX, dialogBounds.Bottom - 34), new Color(190, 202, 214), 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);
        }

        private int FieldCount => _sliderFields.Length + 2;

        private void AdjustSelectedField(int direction)
        {
            if (_selectedIndex == 0)
            {
                return;
            }

            if (_selectedIndex == 1)
            {
                _themeField.Adjust(direction);
                return;
            }

            var slider = _sliderFields[_selectedIndex - 2];
            slider.Value = Math.Clamp(slider.Value + (direction / (float)SliderStepCount), 0f, 1f);
        }

        private void ApplySettings()
        {
            var parsedSeed = ParseSeed();
            Settings = Settings
                .WithSeed(parsedSeed)
                .WithTheme(_themeField.SelectedOption)
                .WithMountainIntensity(_sliderFields[0].Value)
                .WithPlateauIntensity(_sliderFields[1].Value)
                .WithVolcanoIntensity(_sliderFields[2].Value)
                .WithCraterIntensity(_sliderFields[3].Value)
                .WithGorgeIntensity(_sliderFields[4].Value)
                .WithMaxCubeColumn(GetMaxCubeColumn(_sliderFields[5].Value));
        }

        private DialogResult HandleMouseInput(MouseState mouseState)
        {
            if (!LeftClicked(mouseState))
            {
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    UpdateSliderFromMouse(mouseState);
                }

                return DialogResult.None;
            }

            var mousePoint = mouseState.Position;
            if (_saveButtonBounds.Contains(mousePoint))
            {
                return DialogResult.SavePreset;
            }

            if (_loadButtonBounds.Contains(mousePoint))
            {
                return DialogResult.LoadPreset;
            }

            if (_generateButtonBounds.Contains(mousePoint))
            {
                ApplySettings();
                Close();
                return DialogResult.Accepted;
            }

            if (_cancelButtonBounds.Contains(mousePoint))
            {
                Close();
                return DialogResult.Cancelled;
            }

            if (UpdateThemeFromMouse(mouseState))
            {
                return DialogResult.None;
            }

            UpdateSliderFromMouse(mouseState);
            return DialogResult.None;
        }

        private bool UpdateThemeFromMouse(MouseState mouseState)
        {
            if (!_themeTrackBounds.Contains(mouseState.Position) || PlanetTheme.All.Length == 0)
            {
                return false;
            }

            _selectedIndex = 1;
            var relativeX = Math.Clamp(mouseState.X - _themeTrackBounds.X, 0, Math.Max(0, _themeTrackBounds.Width - 1));
            var themeIndex = Math.Clamp((int)((relativeX / (float)Math.Max(1, _themeTrackBounds.Width)) * PlanetTheme.All.Length), 0, PlanetTheme.All.Length - 1);
            _themeField.SelectedOption = PlanetTheme.All[themeIndex];
            return true;
        }

        private void UpdateSliderFromMouse(MouseState mouseState)
        {
            for (var index = 0; index < _sliderFields.Length; index++)
            {
                var track = _sliderTrackBounds[index];
                if (!track.Contains(mouseState.Position))
                {
                    continue;
                }

                _selectedIndex = index + 2;
                _sliderFields[index].Value = Math.Clamp((mouseState.X - track.X) / (float)track.Width, 0f, 1f);
                return;
            }
        }

        private void HandleSeedTyping(KeyboardState current, KeyboardState previous)
        {
            if (_selectedIndex != 0)
            {
                return;
            }

            foreach (var key in current.GetPressedKeys())
            {
                if (previous.IsKeyDown(key))
                {
                    continue;
                }

                if (key is >= Keys.D0 and <= Keys.D9)
                {
                    _seedText += (key - Keys.D0).ToString();
                }
                else if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
                {
                    _seedText += (key - Keys.NumPad0).ToString();
                }
                else if (key == Keys.Back && _seedText.Length > 0)
                {
                    _seedText = _seedText[..^1];
                }
            }

            if (string.IsNullOrWhiteSpace(_seedText))
            {
                _seedText = WorldGenerationSettings.DefaultSeed.ToString();
            }
        }

        private int ParseSeed()
        {
            return int.TryParse(_seedText, out var seed)
                ? Math.Max(1, seed)
                : WorldGenerationSettings.DefaultSeed;
        }

        private void DrawSeedField(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, int x, int y, Color labelColor, Color selectedColor, Color fieldColor)
        {
            var isSelected = _selectedIndex == 0;
            var color = isSelected ? selectedColor : labelColor;
            spriteBatch.DrawString(font, "Seed", new Vector2(x, y), color, 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);
            var fieldBounds = new Rectangle(x + SliderLeft, y - 2, SliderWidth, 22);
            spriteBatch.Draw(pixel, fieldBounds, fieldColor);
            DrawFrame(spriteBatch, pixel, fieldBounds, color, 1);
            spriteBatch.DrawString(font, _seedText, new Vector2(fieldBounds.X + 6, y), Color.White, 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);
        }

        private void DrawThemeField(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, int x, int y, Color labelColor, Color selectedColor, Color trackColor, Color fillColor)
        {
            var isSelected = _selectedIndex == 1;
            var color = isSelected ? selectedColor : labelColor;
            spriteBatch.DrawString(font, _themeField.Label, new Vector2(x, y), color, 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, _themeField.SelectedOption.DisplayName, new Vector2(x + SliderLeft, y), color, 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);

            var track = new Rectangle(x + SliderLeft, y + 20, SliderWidth, 8);
            _themeTrackBounds = track;
            spriteBatch.Draw(pixel, track, trackColor);
            var themeIndex = Array.IndexOf(PlanetTheme.All, _themeField.SelectedOption);
            var fillWidth = (int)MathF.Round(((themeIndex + 1) / (float)PlanetTheme.All.Length) * track.Width);
            spriteBatch.Draw(pixel, new Rectangle(track.X, track.Y, Math.Max(8, fillWidth), track.Height), fillColor);
        }

        private void DrawSliderField(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, int x, int y, SliderField field, int visualIndex, Color labelColor, Color selectedColor, Color trackColor, Color fillColor)
        {
            var isSelected = _selectedIndex == visualIndex;
            var color = isSelected ? selectedColor : labelColor;
            spriteBatch.DrawString(font, field.Label, new Vector2(x, y), color, 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, $"{field.Value:0.00}", new Vector2(x + SliderLeft, y), color, 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);

            var track = new Rectangle(x + SliderLeft, y + 20, SliderWidth, 8);
            _sliderTrackBounds[visualIndex - 2] = track;
            spriteBatch.Draw(pixel, track, trackColor);
            var fillWidth = (int)MathF.Round(field.Value * track.Width);
            spriteBatch.Draw(pixel, new Rectangle(track.X, track.Y, Math.Max(6, fillWidth), track.Height), fillColor);
        }

        private void DrawActionButtons(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Rectangle dialogBounds, int contentY, int rowHeight, Color labelColor, Color selectedColor)
        {
            var buttonY = dialogBounds.Y + contentY + 54 + ((_sliderFields.Length + 2) * rowHeight) + 12;
            _saveButtonBounds = new Rectangle(dialogBounds.X + 26, buttonY, 120, 28);
            _loadButtonBounds = new Rectangle(dialogBounds.X + 160, buttonY, 120, 28);
            _generateButtonBounds = new Rectangle(dialogBounds.Right - 266, buttonY, 120, 28);
            _cancelButtonBounds = new Rectangle(dialogBounds.Right - 132, buttonY, 106, 28);

            DrawButton(spriteBatch, font, pixel, _saveButtonBounds, "Save Preset", labelColor);
            DrawButton(spriteBatch, font, pixel, _loadButtonBounds, "Load Preset", labelColor);
            DrawButton(spriteBatch, font, pixel, _generateButtonBounds, "Generate", selectedColor);
            DrawButton(spriteBatch, font, pixel, _cancelButtonBounds, "Cancel", labelColor);
        }

        private static void DrawButton(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Rectangle bounds, string text, Color color)
        {
            spriteBatch.Draw(pixel, bounds, new Color(28, 36, 48, 255));
            DrawFrame(spriteBatch, pixel, bounds, color, 1);
            var textSize = font.MeasureString(text) * FontScale;
            var textPosition = new Vector2(bounds.Center.X - (textSize.X * 0.5f), bounds.Center.Y - (textSize.Y * 0.5f));
            spriteBatch.DrawString(font, text, textPosition, color, 0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);
        }

        private static void DrawFrame(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color color, int thickness)
        {
            spriteBatch.Draw(pixel, new Rectangle(bounds.Left, bounds.Top, bounds.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Left, bounds.Bottom - thickness, bounds.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Right - thickness, bounds.Top, thickness, bounds.Height), color);
        }

        private bool LeftClicked(MouseState mouseState)
        {
            return mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
        }

        private static float GetMaxColumnSliderValue(int maxCubeColumn)
        {
            return (maxCubeColumn - WorldGenerationSettings.MinimumMaxCubeColumn) /
                (float)(WorldGenerationSettings.MaximumMaxCubeColumn - WorldGenerationSettings.MinimumMaxCubeColumn);
        }

        private static int GetMaxCubeColumn(float sliderValue)
        {
            var span = WorldGenerationSettings.MaximumMaxCubeColumn - WorldGenerationSettings.MinimumMaxCubeColumn;
            return WorldGenerationSettings.MinimumMaxCubeColumn + (int)MathF.Round(Math.Clamp(sliderValue, 0f, 1f) * span);
        }

        private static bool Pressed(KeyboardState current, KeyboardState previous, Keys key)
        {
            return current.IsKeyDown(key) && !previous.IsKeyDown(key);
        }

        public enum DialogResult
        {
            None,
            Accepted,
            Cancelled,
            SavePreset,
            LoadPreset
        }

        private sealed class OptionField
        {
            public OptionField(string label, PlanetTheme[] options, PlanetTheme selectedOption)
            {
                Label = label;
                Options = options;
                SelectedOption = selectedOption;
            }

            public string Label { get; }

            public PlanetTheme[] Options { get; }

            public PlanetTheme SelectedOption { get; set; }

            public void Adjust(int direction)
            {
                var index = Array.IndexOf(Options, SelectedOption);
                if (index < 0)
                {
                    index = 0;
                }

                index = (index + direction + Options.Length) % Options.Length;
                SelectedOption = Options[index];
            }
        }

        private sealed class SliderField
        {
            public SliderField(string label, float value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }

            public float Value { get; set; }
        }
    }
}
