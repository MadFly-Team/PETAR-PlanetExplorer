using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PEPAR.Modules.Debug;
using PEPAR.Modules.Game;
using PEPAR.Modules.Text;

namespace PEPAR
{
    public class PEPAR_Ship : Game
    {
        private const int WindowWidth = 1280;
        private const int WindowHeight = 800;
        private const int TerrainScreenWidth = 640;
        private const int TerrainScreenHeight = 400;
        private const int WorldScreensWide = 80;
        private const int WorldScreensHigh = 12;
        private const int TerrainWidth = TerrainScreenWidth * WorldScreensWide;
        private const int TerrainHeight = TerrainScreenHeight * WorldScreensHigh;
        private const float CameraMoveSpeed = 480f;
        private const float TerrainBlastRadius = 26f;
        private const int StartupChunksPerFrame = 16;
        private const int InitialTerrainSeed = 1337;
        private const int OverviewWidth = 320;
        private const int OverviewHeight = 96;
        private const int OverviewMargin = 16;
        private const double OverviewRefreshIntervalSeconds = 0.2;

        private static readonly TextDisplayStyle StatusStyle = new()
        {
            Color = Color.White,
            Scale = 1.1f,
            ShadowEnabled = true,
            ShadowColor = new Color(0, 0, 32, 220),
            ShadowOffset = new Vector2(1f, 1f),
            Bold = true
        };

        private static readonly TextDisplayStyle InstructionStyle = new()
        {
            Color = Color.LightGreen,
            Scale = 1f,
            ShadowEnabled = true,
            ShadowColor = new Color(0, 0, 0, 220),
            ShadowOffset = new Vector2(1f, 1f)
        };

        private static readonly TextDisplayStyle TerrainStyle = new()
        {
            Color = new Color(255, 220, 130),
            Scale = 1f,
            ShadowEnabled = true,
            ShadowColor = new Color(0, 0, 0, 220),
            ShadowOffset = new Vector2(1f, 1f)
        };

        private static readonly TextDisplayStyle LoadingTitleStyle = new()
        {
            Color = Color.White,
            Scale = 1.5f,
            ShadowEnabled = true,
            ShadowColor = new Color(0, 0, 0, 220),
            ShadowOffset = new Vector2(1f, 1f),
            Bold = true
        };

        private static readonly TextDisplayStyle LoadingTextStyle = new()
        {
            Color = new Color(200, 240, 255),
            Scale = 1f,
            ShadowEnabled = true,
            ShadowColor = new Color(0, 0, 0, 220),
            ShadowOffset = new Vector2(1f, 1f)
        };

        private GraphicsDeviceManager _graphics;
        private Texture2D _backgroundGradientTexture = null!;
        private Texture2D _pixelTexture = null!;
        private Texture2D? _overviewTexture;
        private SpriteBatch _spriteBatch = null!;
        private readonly DebugTextOverlay _debugTextOverlay = new();
        private readonly WormsTerrainDrawer _wormsTerrainDrawer = new();
        private readonly TextDisplayer _textDisplayer = new();
        private Vector2 _cameraPosition;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private int _lastPreloadPercentLogged = -1;
        private int _terrainSeed = InitialTerrainSeed;
        private double _lastOverviewRefreshTime;
        private bool _overviewTextureDirty;
        private bool _firstUpdateLogged;
        private bool _firstDrawLogged;

        public PEPAR_Ship()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = WindowWidth;
            _graphics.PreferredBackBufferHeight = WindowHeight;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            Debug_Logger.LogDebug("PEPAR_Ship constructor completed.", "Game");
        }

        protected override void Initialize()
        {
            Debug_Logger.LogInformation("Initializing game systems.", "Game");
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();

            base.Initialize();

            Debug_Logger.LogInformation("Game initialization finished.", "Game");
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _backgroundGradientTexture = CreateBackgroundGradientTexture();
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData([Color.White]);
            LoadTerrain();
            _textDisplayer.Load(Content);
            _debugTextOverlay.Load(Content);

            Debug_Logger.LogInformation("Content pipeline is ready and SpriteBatch was created.", "Content");
            Debug_Logger.LogInformation($"Worms terrain drawer loaded for {_wormsTerrainDrawer.MapWidth}x{_wormsTerrainDrawer.MapHeight} terrain.", "Terrain");
            Debug_Logger.LogInformation($"Terrain preload started for {_wormsTerrainDrawer.TotalChunkCount} chunks.", "Terrain");
            Debug_Logger.LogInformation($"Terrain seed set to {_terrainSeed}.", "Terrain");
            Debug_Logger.LogInformation("Camera controls ready. Use arrow keys or WASD to scroll the terrain.", "Camera");
            Debug_Logger.LogInformation("Terrain destruction ready. Left-click to blast holes in the ground.", "Terrain");
            Debug_Logger.LogInformation("Standard text displayer loaded.", "Text");
            Debug_Logger.LogInformation("Debug text overlay loaded for on-screen messages.", "Text");
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboardState.IsKeyDown(Keys.Escape))
            {
                Debug_Logger.LogWarning("Exit requested by user input.", "Input");
                Exit();
            }

            if (keyboardState.IsKeyDown(Keys.F12) && !_previousKeyboardState.IsKeyDown(Keys.F12))
            {
                _graphics.IsFullScreen = !_graphics.IsFullScreen;
                _graphics.ApplyChanges();
                Debug_Logger.LogInformation($"Display mode changed to {(_graphics.IsFullScreen ? "fullscreen" : "windowed") }.", "Display");
            }

            if (keyboardState.IsKeyDown(Keys.F11) && !_previousKeyboardState.IsKeyDown(Keys.F11))
            {
                RegenerateTerrain();
            }

            if (!_wormsTerrainDrawer.IsPreloadComplete)
            {
                _wormsTerrainDrawer.PrepareNextChunks(StartupChunksPerFrame);
                LogPreloadProgress();
                _previousKeyboardState = keyboardState;
                _previousMouseState = mouseState;
                base.Update(gameTime);
                return;
            }

            _wormsTerrainDrawer.Update(gameTime);
            if (_wormsTerrainDrawer.ConsumeTerrainVisualsChanged())
            {
                _overviewTextureDirty = true;
            }
            RefreshOverviewTexture(gameTime);

            UpdateCamera(gameTime, keyboardState);

            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                var worldPoint = GetMouseWorldPoint(mouseState);
                _wormsTerrainDrawer.DestroyCircle(worldPoint.ToVector2(), TerrainBlastRadius);
                _overviewTextureDirty = true;
                Debug_Logger.LogInformation($"Terrain blasted at ({worldPoint.X}, {worldPoint.Y}) with radius {TerrainBlastRadius:0}.", "Terrain");
            }

            if (!_firstUpdateLogged)
            {
                Debug_Logger.LogDebug($"First update tick at {gameTime.TotalGameTime.TotalMilliseconds:0} ms.", "GameLoop");
                _firstUpdateLogged = true;
            }

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            if (!_firstDrawLogged)
            {
                Debug_Logger.LogDebug($"First draw frame at {gameTime.TotalGameTime.TotalMilliseconds:0} ms.", "Render");
                _firstDrawLogged = true;
            }

            var worldView = GetWorldView();
            var mouseWorldPoint = GetMouseWorldPoint(Mouse.GetState());
            var cursorTerrainState = _wormsTerrainDrawer.IsSolid(mouseWorldPoint) ? "SOLID" : "AIR";
            var bottomTextY = GraphicsDevice.Viewport.Height - 64f;

            EnsureBackgroundGradientTexture();
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(_backgroundGradientTexture, GraphicsDevice.Viewport.Bounds, Color.White);

            if (!_wormsTerrainDrawer.IsPreloadComplete)
            {
                DrawLoadingScreen();
                _spriteBatch.End();
                base.Draw(gameTime);
                return;
            }

            _wormsTerrainDrawer.Draw(_spriteBatch, worldView, Vector2.Zero);
            DrawOverviewMap(worldView);
            _textDisplayer.Draw(_spriteBatch, "PEPAR STATUS: SYSTEMS ONLINE", new Vector2(16f, bottomTextY), StatusStyle);
            _textDisplayer.Draw(_spriteBatch, $"WORLD {_wormsTerrainDrawer.MapWidth} x {_wormsTerrainDrawer.MapHeight}  |  CAMERA {worldView.X}, {worldView.Y}  |  CURSOR {mouseWorldPoint.X}, {mouseWorldPoint.Y}: {cursorTerrainState}", new Vector2(16f, bottomTextY + 16f), TerrainStyle);
            _textDisplayer.Draw(_spriteBatch, "ARROWS/WASD SCROLL  |  MOUSE1 BLAST  |  F11 REGENERATE  |  F12 TOGGLE FULLSCREEN  |  ESC EXIT", new Vector2(16f, bottomTextY + 30f), InstructionStyle);
            //_debugTextOverlay.Draw(_spriteBatch);
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void UpdateCamera(GameTime gameTime, KeyboardState keyboardState)
        {
            var movement = Vector2.Zero;

            if (keyboardState.IsKeyDown(Keys.Left) || keyboardState.IsKeyDown(Keys.A))
            {
                movement.X -= 1f;
            }

            if (keyboardState.IsKeyDown(Keys.Right) || keyboardState.IsKeyDown(Keys.D))
            {
                movement.X += 1f;
            }

            if (keyboardState.IsKeyDown(Keys.Up) || keyboardState.IsKeyDown(Keys.W))
            {
                movement.Y -= 1f;
            }

            if (keyboardState.IsKeyDown(Keys.Down) || keyboardState.IsKeyDown(Keys.S))
            {
                movement.Y += 1f;
            }

            if (movement != Vector2.Zero)
            {
                movement.Normalize();
                _cameraPosition += movement * CameraMoveSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
                ClampCamera();
            }
        }

        private Rectangle GetWorldView()
        {
            ClampCamera();
            return new Rectangle((int)_cameraPosition.X, (int)_cameraPosition.Y, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }

        private Point GetMouseWorldPoint(MouseState mouseState)
        {
            var worldX = (int)_cameraPosition.X + mouseState.X;
            var worldY = (int)_cameraPosition.Y + mouseState.Y;

            return new Point(
                Math.Clamp(worldX, 0, _wormsTerrainDrawer.MapWidth - 1),
                Math.Clamp(worldY, 0, _wormsTerrainDrawer.MapHeight - 1));
        }

        private void ClampCamera()
        {
            var maxX = Math.Max(0, _wormsTerrainDrawer.MapWidth - GraphicsDevice.Viewport.Width);
            var maxY = Math.Max(0, _wormsTerrainDrawer.MapHeight - GraphicsDevice.Viewport.Height);

            _cameraPosition = new Vector2(
                Math.Clamp(_cameraPosition.X, 0f, maxX),
                Math.Clamp(_cameraPosition.Y, 0f, maxY));
        }

        private void DrawLoadingScreen()
        {
            var progress = Math.Clamp(_wormsTerrainDrawer.PreloadProgress, 0f, 1f);
            var viewport = GraphicsDevice.Viewport;
            var title = "GENERATING TERRAIN BUFFER";
            var status = $"{_wormsTerrainDrawer.PreparedChunkCount} / {_wormsTerrainDrawer.TotalChunkCount} chunks ready ({progress * 100f:0.0}%)";
            var titleSize = _textDisplayer.MeasureString(title, LoadingTitleStyle.Scale);
            var statusSize = _textDisplayer.MeasureString(status, LoadingTextStyle.Scale);
            var barWidth = Math.Min(480, viewport.Width - 80);
            var barHeight = 20;
            var barX = (viewport.Width - barWidth) / 2;
            var barY = (viewport.Height / 2) + 10;
            var fillWidth = (int)((barWidth - 4) * progress);

            _textDisplayer.Draw(_spriteBatch, title, new Vector2((viewport.Width - titleSize.X) / 2f, barY - 54f), LoadingTitleStyle);
            _textDisplayer.Draw(_spriteBatch, status, new Vector2((viewport.Width - statusSize.X) / 2f, barY + 30f), LoadingTextStyle);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(barX, barY, barWidth, barHeight), new Color(12, 18, 34));
            _spriteBatch.Draw(_pixelTexture, new Rectangle(barX + 2, barY + 2, Math.Max(0, fillWidth), barHeight - 4), new Color(70, 220, 255));
        }

        private void DrawOverviewMap(Rectangle worldView)
        {
            if (_overviewTexture is null)
            {
                return;
            }

            var outerBounds = new Rectangle(
                GraphicsDevice.Viewport.Width - OverviewWidth - OverviewMargin,
                OverviewMargin,
                OverviewWidth,
                OverviewHeight);

            _spriteBatch.Draw(_pixelTexture, outerBounds, new Color(0, 0, 0, 160));
            _spriteBatch.Draw(_overviewTexture, outerBounds, Color.White);
            DrawOutline(outerBounds, new Color(220, 240, 255));

            var viewportBox = new Rectangle(
                outerBounds.X + (int)MathF.Round(worldView.X / (float)_wormsTerrainDrawer.MapWidth * outerBounds.Width),
                outerBounds.Y + (int)MathF.Round(worldView.Y / (float)_wormsTerrainDrawer.MapHeight * outerBounds.Height),
                Math.Max(1, (int)MathF.Round(worldView.Width / (float)_wormsTerrainDrawer.MapWidth * outerBounds.Width)),
                Math.Max(1, (int)MathF.Round(worldView.Height / (float)_wormsTerrainDrawer.MapHeight * outerBounds.Height)));

            viewportBox.Width = Math.Min(viewportBox.Width, outerBounds.Width);
            viewportBox.Height = Math.Min(viewportBox.Height, outerBounds.Height);
            viewportBox.X = Math.Clamp(viewportBox.X, outerBounds.Left, outerBounds.Right - viewportBox.Width);
            viewportBox.Y = Math.Clamp(viewportBox.Y, outerBounds.Top, outerBounds.Bottom - viewportBox.Height);

            DrawOutline(viewportBox, Color.White);
        }

        private void DrawOutline(Rectangle rectangle, Color color)
        {
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1), color);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rectangle.X, rectangle.Bottom - 1, rectangle.Width, 1), color);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rectangle.X, rectangle.Y, 1, rectangle.Height), color);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(rectangle.Right - 1, rectangle.Y, 1, rectangle.Height), color);
        }

        private void LoadTerrain()
        {
            _wormsTerrainDrawer.Load(GraphicsDevice, TerrainWidth, TerrainHeight, _terrainSeed);
            _lastPreloadPercentLogged = -1;
            _lastOverviewRefreshTime = 0d;
            _overviewTextureDirty = true;
            _overviewTexture?.Dispose();
            _overviewTexture = null;
            ClampCamera();
        }

        private void RegenerateTerrain()
        {
            _terrainSeed = unchecked(_terrainSeed + 1);
            Debug_Logger.LogInformation($"Terrain regeneration requested with seed {_terrainSeed}.", "Terrain");
            LoadTerrain();
        }

        private void RefreshOverviewTexture(GameTime gameTime)
        {
            if (!_overviewTextureDirty)
            {
                return;
            }

            var totalSeconds = gameTime.TotalGameTime.TotalSeconds;
            if (_wormsTerrainDrawer.HasActiveEffects && totalSeconds - _lastOverviewRefreshTime < OverviewRefreshIntervalSeconds)
            {
                return;
            }

            var overviewPixels = _wormsTerrainDrawer.BuildOverviewPixels(OverviewWidth, OverviewHeight);
            _overviewTexture?.Dispose();
            _overviewTexture = new Texture2D(GraphicsDevice, OverviewWidth, OverviewHeight, false, SurfaceFormat.Color);
            _overviewTexture.SetData(overviewPixels);
            _lastOverviewRefreshTime = totalSeconds;
            _overviewTextureDirty = false;
        }

        private void EnsureBackgroundGradientTexture()
        {
            if (_backgroundGradientTexture.Width != 1 || _backgroundGradientTexture.Height != GraphicsDevice.Viewport.Height)
            {
                _backgroundGradientTexture.Dispose();
                _backgroundGradientTexture = CreateBackgroundGradientTexture();
            }
        }

        private Texture2D CreateBackgroundGradientTexture()
        {
            var height = Math.Max(1, GraphicsDevice.Viewport.Height);
            var pixels = new Color[height];
            var topColor = Color.Black;
            var bottomColor = new Color(135, 206, 250);

            for (var y = 0; y < height; y++)
            {
                var amount = height == 1 ? 1f : y / (float)(height - 1);
                pixels[y] = Color.Lerp(topColor, bottomColor, amount);
            }

            var texture = new Texture2D(GraphicsDevice, 1, height);
            texture.SetData(pixels);
            return texture;
        }

        private void LogPreloadProgress()
        {
            var progressPercent = (int)(_wormsTerrainDrawer.PreloadProgress * 100f);
            if (progressPercent >= 100 && _lastPreloadPercentLogged < 100)
            {
                Debug_Logger.LogInformation("Terrain preload complete.", "Terrain");
                _lastPreloadPercentLogged = 100;
                return;
            }

            if (progressPercent >= _lastPreloadPercentLogged + 2)
            {
                Debug_Logger.LogInformation($"Terrain preload {progressPercent}% complete.", "Terrain");
                _lastPreloadPercentLogged = progressPercent;
            }
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            Debug_Logger.LogInformation("Game is shutting down.", "Game");
            _backgroundGradientTexture.Dispose();
            _overviewTexture?.Dispose();
            _pixelTexture.Dispose();
            _wormsTerrainDrawer.Unload();
            _debugTextOverlay.Unload();
            base.OnExiting(sender, args);
        }
    }
}
