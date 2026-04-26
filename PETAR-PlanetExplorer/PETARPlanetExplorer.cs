using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PETAR_PlanetExplorer.Modules.Debug;
using PETAR_PlanetExplorer.Modules.Maps;
using PETAR_PlanetExplorer.Modules.UI;

namespace PETAR_PlanetExplorer
{
    public class PETARPlanetExplorer : Game
    {
        private const int WorldSize = 4096;
        private const int DefaultWorldSeed = 74291;
        private const int DefaultTreeCount = 24000;
        private const int DefaultThemeIndex = 0;
        private const float RenderCubeHeight = 2f;
        private const float RenderFaceOverlap = 0.08f;
        private const float RenderCameraMinEyeY = 10f;
        private const float RenderCameraMaxEyeY = 172f;
        private const float FlightAltitudeMinimum = 0.05f;
        private const float WorldEdgePadding = 100f;
        private const float CameraCollisionExtent = 0.1f;
        private const float FlightClearance = 0.05f;
        private const float TerrainFollowClearance = 0.1f;
        private const float CameraCollisionEyeClearance = 1f;
        private const float ShipRenderDistance = 31.5f;
        private const float ShipRenderVerticalOffset = -5f;
        private const float ShipCollisionForwardOffset = 10.5f;
        private const float ShipCollisionNoseOffset = 13f;
        private const float ShipCollisionHalfWidth = 1.1f;
        private const float ShipCollisionClearance = 0.45f;
        private const float ShipCollisionBottomExtent = 1.25f;
        private const float PayloadCollisionBottomExtent = 6.2f;
        private const float PayloadReleaseYOffset = 6f;
        private const float PayloadDropGravity = 18f;
        private const float PayloadDropHorizontalDrag = 0.12f;
        private const float MissileSpeed = 180f;
        private const float MissileMaxRange = 300f;
        private const float DefaultMissileExplosionRadius = 10f;
        private const int ProtectedTerrainColumnHeight = 4;
        private const int MissileDebrisCount = 28;
        private const float MissileDebrisLifetimeMin = 0.65f;
        private const float MissileDebrisLifetimeMax = 1.25f;
        private const float MaxFlightAltitude = 7000f;
        private const float MaxVerticalSpeed = MaxCruiseSpeed;
        private const float MaxCruiseSpeed = 72f;
        private const float MaxBoostSpeed = 180f;
        private const float MaxTurnSpeed = 0.8f;
        private const float MovementAcceleration = 220f;
        private const float TurnAcceleration = 2.6f;
        private const float VerticalAcceleration = MovementAcceleration;
        private const float MouseLookSensitivity = 0.0032f;
        private const float MaxLookPitch = MathHelper.PiOver2;

        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private SpriteFont _spaceFont;
        private Texture2D _heightMapTexture;
        private Texture2D _pixel;
        private HeightMapFlyoverRenderer _horizontalViewRenderer;
        private Point _horizontalViewRenderSize;
        private ProceduralWorldMap _worldMap;
        private Task<(ProceduralWorldMap WorldMap, Color[] ColorMap)> _worldGenerationTask;
        private Vector2 _flightPosition;
        private float _flightHeading;
        private float _flightPitch;
        private float _flightAltitude;
        private float _forwardVelocity;
        private float _turnVelocity;
        private float _verticalVelocity;
        private float _worldGenerationProgress;
        private float _worldTime;
        private bool _exitRequested;
        private bool _usePlanView = true;
        private bool _terrainFollowMode;
        private bool _mouseLookInitialized;
        private bool _payloadReleased;
        private bool _fallingPayloadActive;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private string _worldGenerationStatus = "Preparing world generation";
        private float _titlePulse;
        private int _selectedThemeIndex = DefaultThemeIndex;
        private WorldGenerationSettings _generationSettings = WorldGenerationSettings.Default;
        private GenerationDialog _generationDialog;
        private GenerationPresetStore _generationPresetStore;
        private FlyoverOverlay _flyoverOverlay;
        private FallingPayloadState _fallingPayload;
        private readonly List<HeightMapFlyoverRenderer.OilPlatformInstance> _oilPlatforms = new();
        private readonly List<HeightMapFlyoverRenderer.MissileDebrisParticle> _missileDebris = new();
        private MissileState? _activeMissile;

        public PETARPlanetExplorer()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 1920;
            _graphics.PreferredBackBufferHeight = 1080;
            _graphics.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;
            _graphics.PreferMultiSampling = true;
            _graphics.SynchronizeWithVerticalRetrace = true;
            Content.RootDirectory = "Content";
            IsFixedTimeStep = true;
            IsMouseVisible = _usePlanView;

            DebugLogger.Debug("PETARPlanetExplorer instance created.");
        }

        protected override void Initialize()
        {
            DebugLogger.Debug("Initializing game systems.");

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _spaceFont = Content.Load<SpriteFont>("Fonts/SpaceFont");

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _generationPresetStore = new GenerationPresetStore(System.IO.Path.Combine(AppContext.BaseDirectory, "generation-preset.json"));
            if (_generationPresetStore.TryLoad(out var loadedSettings))
            {
                _generationSettings = loadedSettings;
                _selectedThemeIndex = Array.IndexOf(PlanetTheme.All, _generationSettings.Theme);
            }

            _flyoverOverlay = new FlyoverOverlay();

            _generationDialog = new GenerationDialog(_generationSettings);

            DebugLogger.Debug("Game content loaded.");
            DebugLogger.Info("Loaded font resource 'Fonts/SpaceFont'.");

            StartWorldGeneration();
        }

        protected override void UnloadContent()
        {
            DebugLogger.Debug("Unloading game content.");

            _heightMapTexture?.Dispose();
            _horizontalViewRenderer?.Dispose();
            _pixel?.Dispose();

            base.UnloadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();

            if (keyboardState.IsKeyDown(Keys.F12) && !_previousKeyboardState.IsKeyDown(Keys.F12))
            {
                _graphics.ToggleFullScreen();
                DebugLogger.Info($"Fullscreen toggled. Enabled: {_graphics.IsFullScreen}.");
            }

            if (keyboardState.IsKeyDown(Keys.F11) && !_previousKeyboardState.IsKeyDown(Keys.F11))
            {
                _usePlanView = !_usePlanView;
                IsMouseVisible = _usePlanView;
                _mouseLookInitialized = false;
                DebugLogger.Info($"View mode toggled. Plan view enabled: {_usePlanView}.");
            }

            if (keyboardState.IsKeyDown(Keys.F10) && !_previousKeyboardState.IsKeyDown(Keys.F10) && _worldGenerationTask == null)
            {
                StartWorldGeneration(Random.Shared.Next(1, int.MaxValue));
            }

            if (keyboardState.IsKeyDown(Keys.F8) && !_previousKeyboardState.IsKeyDown(Keys.F8) && _worldGenerationTask == null)
            {
                _generationDialog.Open(_generationSettings);
                IsMouseVisible = true;
            }

            if (_generationDialog != null && _generationDialog.IsOpen)
            {
                var dialogResult = _generationDialog.HandleInput(keyboardState, _previousKeyboardState, mouseState);
                _generationSettings = _generationDialog.Settings;
                _selectedThemeIndex = Array.IndexOf(PlanetTheme.All, _generationSettings.Theme);
                if (dialogResult == GenerationDialog.DialogResult.Accepted)
                {
                    DebugLogger.Info($"Custom generation settings updated. Theme: {_generationSettings.Theme.DisplayName}, mountains: {_generationSettings.MountainIntensity:0.00}, volcanoes: {_generationSettings.VolcanoIntensity:0.00}, craters: {_generationSettings.CraterIntensity:0.00}, gorges: {_generationSettings.GorgeIntensity:0.00}.");
                    StartWorldGeneration(_generationSettings.Seed);
                }
                else if (dialogResult == GenerationDialog.DialogResult.SavePreset)
                {
                    _generationPresetStore?.Save(_generationSettings);
                    DebugLogger.Info("Generation preset saved.");
                }
                else if (dialogResult == GenerationDialog.DialogResult.LoadPreset && _generationPresetStore != null && _generationPresetStore.TryLoad(out var presetSettings))
                {
                    _generationSettings = presetSettings;
                    _selectedThemeIndex = Array.IndexOf(PlanetTheme.All, _generationSettings.Theme);
                    _generationDialog.Open(_generationSettings);
                    DebugLogger.Info("Generation preset loaded.");
                }

                _previousKeyboardState = keyboardState;
                _previousMouseState = mouseState;
                base.Update(gameTime);
                return;
            }

            IsMouseVisible = _usePlanView;

            if (keyboardState.IsKeyDown(Keys.F9) && !_previousKeyboardState.IsKeyDown(Keys.F9) && _worldMap != null)
            {
                _terrainFollowMode = !_terrainFollowMode;
                _verticalVelocity = 0f;
                if (_terrainFollowMode)
                {
                    _flightAltitude = GetTerrainFollowAltitude(_flightPosition, GetLookDirection());
                }

                DebugLogger.Info($"Terrain follow mode enabled: {_terrainFollowMode}.");
            }

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboardState.IsKeyDown(Keys.Escape))
            {
                if (!_exitRequested)
                {
                    DebugLogger.Info("Exit requested by player input.");
                    _exitRequested = true;
                }

                Exit();
            }

            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _titlePulse += deltaTime * 1.6f;
            _worldTime += deltaTime;

            TryCompleteWorldGeneration();

            if (_worldMap == null)
            {
                _previousKeyboardState = keyboardState;
                base.Update(gameTime);
                return;
            }

            if (!_usePlanView)
            {
                UpdateMouseLook();
            }

            if (keyboardState.IsKeyDown(Keys.P) && !_previousKeyboardState.IsKeyDown(Keys.P))
            {
                TryReleasePayload(GetLookDirection());
            }

            if (!_usePlanView && keyboardState.IsKeyDown(Keys.M) && !_previousKeyboardState.IsKeyDown(Keys.M))
            {
                TryFireMissile(GetLookDirection());
            }

            if (_terrainFollowMode)
            {
                _verticalVelocity = 0f;
            }
            else if (keyboardState.IsKeyDown(Keys.Q) || keyboardState.IsKeyDown(Keys.PageUp) || keyboardState.IsKeyDown(Keys.Space) || keyboardState.IsKeyDown(Keys.R))
            {
                _verticalVelocity = AccelerateTowards(_verticalVelocity, MaxVerticalSpeed, VerticalAcceleration, deltaTime);
            }
            else if (keyboardState.IsKeyDown(Keys.E) || keyboardState.IsKeyDown(Keys.PageDown) || keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl) || keyboardState.IsKeyDown(Keys.F))
            {
                _verticalVelocity = AccelerateTowards(_verticalVelocity, -MaxVerticalSpeed, VerticalAcceleration, deltaTime);
            }
            else
            {
                _verticalVelocity = AccelerateTowards(_verticalVelocity, 0f, VerticalAcceleration, deltaTime);
            }

            var isBoosting = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            var movementInput = 0f;
            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))
            {
                movementInput += 1f;
            }

            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down))
            {
                movementInput -= 1f;
            }

            var turnInput = 0f;
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left))
            {
                turnInput -= 1f;
            }

            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right))
            {
                turnInput += 1f;
            }

            var targetForwardSpeed = movementInput * (isBoosting ? MaxBoostSpeed : MaxCruiseSpeed);
            _forwardVelocity = AccelerateTowards(_forwardVelocity, targetForwardSpeed, MovementAcceleration, deltaTime);
            _turnVelocity = AccelerateTowards(_turnVelocity, turnInput * MaxTurnSpeed, TurnAcceleration, deltaTime);
            _flightHeading += _turnVelocity * deltaTime;
            var lookDirection = GetLookDirection();
            var travelVector = lookDirection * (_forwardVelocity * deltaTime);
            var proposedPosition = ClampFlightPosition(_flightPosition + new Vector2(travelVector.X, travelVector.Z));
            var altitudeDelta = (_verticalVelocity * deltaTime) + travelVector.Y;
            if (_terrainFollowMode)
            {
                _flightPosition = proposedPosition;
                _flightAltitude = GetTerrainFollowAltitude(_flightPosition, lookDirection);
            }
            else
            {
                var proposedAltitude = _flightAltitude + altitudeDelta;
                var proposedMinimumAltitude = GetMinimumFlightAltitude(proposedPosition, lookDirection);
                if (proposedAltitude >= proposedMinimumAltitude)
                {
                    _flightPosition = proposedPosition;
                }
                else
                {
                    var bounceDirection = new Vector2(travelVector.X, travelVector.Z);
                    if (bounceDirection.LengthSquared() > 0.000001f)
                    {
                        bounceDirection.Normalize();
                        _flightPosition = ClampFlightPosition(_flightPosition - (bounceDirection * MathF.Max(0.05f, MathF.Abs(_forwardVelocity) * deltaTime * 0.35f)));
                    }

                    _forwardVelocity = -_forwardVelocity * 0.35f;
                }

                _flightAltitude = MathHelper.Clamp(_flightAltitude + altitudeDelta, GetMinimumFlightAltitude(_flightPosition, lookDirection), MaxFlightAltitude);
            }

            UpdateFallingPayload(deltaTime);
            UpdateMissile(deltaTime);
            UpdateMissileDebris(deltaTime);

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            var altitudeRatio = MathHelper.Clamp((_flightAltitude - FlightAltitudeMinimum) / (MaxFlightAltitude - FlightAltitudeMinimum), 0f, 1f);
            var backgroundColor = _usePlanView
                ? new Color(5, 10, 24)
                : (_worldMap != null && _worldMap.HasSurfaceWater
                    ? Color.Lerp(new Color(135, 206, 235), new Color(9, 28, 66), altitudeRatio)
                    : Color.Black);

            GraphicsDevice.Clear(backgroundColor);

            var viewport = GraphicsDevice.Viewport;
            var center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
            var pulse = 0.5f + (MathF.Sin(_titlePulse) * 0.5f);
            var glowColor = Color.Lerp(new Color(0, 180, 255), new Color(140, 60, 255), pulse) * 0.55f;
            var accentColor = Color.Lerp(new Color(105, 225, 255), Color.White, 0.45f);
            var subtitleColor = Color.Lerp(new Color(255, 150, 70), new Color(255, 225, 130), pulse * 0.65f);
            var shadowColor = new Color(0, 0, 0, 180);

            if (_worldMap == null || _heightMapTexture == null)
            {
                _flyoverOverlay?.DrawLoadingScreen(
                    _spriteBatch,
                    _spaceFont,
                    _pixel,
                    viewport,
                    new LoadingOverlayModel(_worldGenerationProgress, _worldGenerationStatus, glowColor, accentColor, subtitleColor, shadowColor));
                base.Draw(gameTime);
                return;
            }

            var minimapRectangle = new Rectangle(24, 24, 180, 180);
            var title = "PROCEDURAL PLANET FLYOVER";
            var titleOrigin = _spaceFont.MeasureString(title) * 0.5f;
            var titlePosition = new Vector2(center.X, 24f);
            var viewText = _usePlanView ? "Plan" : "Horizon";
            var flightModeText = _terrainFollowMode ? "Terrain follow" : "Manual altitude";
            var payloadStatus = _fallingPayloadActive ? "Payload dropping" : (_payloadReleased ? "Platform deployed" : "Payload attached");
            var controlsText = $"Seed {_worldMap.Seed} // Theme {_worldMap.Theme.DisplayName} // F8 custom world // F11 {viewText} view // F10 regenerate // F9 {flightModeText} // Mouse look // W/S move // Q/E or Space/Ctrl or R/F altitude // A/D turn // Shift boost // P release payload // Heading {MathHelper.ToDegrees(_flightHeading):000} deg";
            var controlsOrigin = _spaceFont.MeasureString(controlsText) * 0.5f;
            var altitudeText = $"Altitude {_flightAltitude:0.00} // Pitch {MathHelper.ToDegrees(_flightPitch):000} deg // Position {_flightPosition.X:0000}, {_flightPosition.Y:0000} // Mode {flightModeText} // {payloadStatus}";

            if (_usePlanView)
            {
                DrawPlanView(center);
            }
            else
            {
                DrawHorizontalView(viewport);
            }

            _flyoverOverlay?.DrawHud(
                _spriteBatch,
                _spaceFont,
                _pixel,
                viewport,
                new FlyoverHudModel(
                    minimapRectangle,
                    title,
                    titlePosition,
                    titleOrigin,
                    controlsText,
                    new Vector2(center.X, viewport.Height - 66f),
                    controlsOrigin,
                    altitudeText,
                    new Vector2(28f, minimapRectangle.Bottom + 18f),
                    _worldMap.Theme.DisplayName,
                    glowColor,
                    accentColor,
                    subtitleColor,
                    shadowColor,
                    _heightMapTexture,
                    _flightPosition,
                    new Point(_worldMap.Width, _worldMap.Height),
                    true));

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            DrawOilPlatformMarkers(minimapRectangle);
            _generationDialog?.Draw(_spriteBatch, _spaceFont, _pixel, viewport);
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawPlanView(Vector2 center)
        {
            var altitudeRatio = MathHelper.Clamp((_flightAltitude - 0.05f) / (MaxFlightAltitude - 0.05f), 0f, 1f);
            var zoom = MathHelper.Lerp(6.4f, 1f, altitudeRatio);
            var transform =
                Matrix.CreateTranslation(new Vector3(-_flightPosition, 0f)) *
                Matrix.CreateRotationZ(-_flightHeading) *
                Matrix.CreateScale(zoom, zoom, 1f) *
                Matrix.CreateTranslation(new Vector3(center, 0f));

            DrawWorldTiles(transform);
        }

        private void DrawHorizontalView(Viewport viewport)
        {
            EnsureHorizontalViewRenderer(viewport);
            _horizontalViewRenderer.Render(_worldMap, _flightPosition, _flightHeading, _flightPitch, _flightAltitude, MaxFlightAltitude, _worldTime, _payloadReleased, _oilPlatforms, GetMissileRenderState(), _missileDebris);
        }

        private void UpdateMouseLook()
        {
            if (!IsActive)
            {
                _mouseLookInitialized = false;
                return;
            }

            var centerX = Window.ClientBounds.Width / 2;
            var centerY = Window.ClientBounds.Height / 2;
            if (!_mouseLookInitialized)
            {
                Mouse.SetPosition(centerX, centerY);
                _mouseLookInitialized = true;
                return;
            }

            var mouseState = Mouse.GetState();
            var deltaX = mouseState.X - centerX;
            var deltaY = mouseState.Y - centerY;
            if (deltaX != 0 || deltaY != 0)
            {
                _flightHeading += deltaX * MouseLookSensitivity;
                _flightPitch = MathHelper.Clamp(_flightPitch - (deltaY * MouseLookSensitivity), -MaxLookPitch, MaxLookPitch);
                Mouse.SetPosition(centerX, centerY);
            }
        }

        private Vector3 GetLookDirection()
        {
            var pitchCos = MathF.Cos(_flightPitch);
            return Vector3.Normalize(new Vector3(
                MathF.Cos(_flightHeading) * pitchCos,
                MathF.Sin(_flightPitch),
                MathF.Sin(_flightHeading) * pitchCos));
        }

        private void EnsureHorizontalViewRenderer(Viewport viewport)
        {
            if (_horizontalViewRenderer != null)
            {
                return;
            }

            _horizontalViewRenderer?.Dispose();
            _horizontalViewRenderer = new HeightMapFlyoverRenderer(GraphicsDevice, _worldMap.Width, _worldMap.Height);
            _horizontalViewRenderSize = Point.Zero;
        }

        private void DrawWorldTiles(Matrix transform)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, transform);

            for (var tileY = -1; tileY <= 1; tileY++)
            {
                for (var tileX = -1; tileX <= 1; tileX++)
                {
                    var tilePosition = new Vector2(tileX * _worldMap.Width, tileY * _worldMap.Height);
                    _spriteBatch.Draw(_heightMapTexture, tilePosition, Color.White);
                }
            }

            _spriteBatch.End();
        }

        private Vector2 ClampFlightPosition(Vector2 position)
        {
            if (_worldMap == null)
            {
                return position;
            }

            return new Vector2(
                MathHelper.Clamp(position.X, WorldEdgePadding, _worldMap.Width - WorldEdgePadding),
                MathHelper.Clamp(position.Y, WorldEdgePadding, _worldMap.Height - WorldEdgePadding));
        }

        private float GetMinimumFlightAltitude(Vector2 position, Vector3 lookDirection)
        {
            var requiredEyeY = MathF.Max(
                GetCollisionSurfaceTopY(position) + CameraCollisionEyeClearance,
                GetShipCollisionRequiredEyeY(position, lookDirection));
            return MathHelper.Clamp(ConvertEyeYToFlightAltitude(requiredEyeY), FlightAltitudeMinimum, MaxFlightAltitude);
        }

        private float GetTerrainFollowAltitude(Vector2 position, Vector3 lookDirection)
        {
            var requiredEyeY = MathF.Max(
                GetCollisionSurfaceTopY(position) + (TerrainFollowClearance * (RenderCameraMaxEyeY - RenderCameraMinEyeY)),
                GetShipCollisionRequiredEyeY(position, lookDirection));
            return MathHelper.Clamp(ConvertEyeYToFlightAltitude(requiredEyeY), FlightAltitudeMinimum, MaxFlightAltitude);
        }

        private float GetShipCollisionRequiredEyeY(Vector2 position, Vector3 lookDirection)
        {
            var horizontalForward = new Vector2(lookDirection.X, lookDirection.Z);
            if (horizontalForward.LengthSquared() < 0.0001f)
            {
                horizontalForward = new Vector2(MathF.Cos(_flightHeading), MathF.Sin(_flightHeading));
            }
            else
            {
                horizontalForward.Normalize();
            }

            var horizontalRight = new Vector2(-horizontalForward.Y, horizontalForward.X);
            var shipCenter = _worldMap.WrapPosition(position + (horizontalForward * ShipCollisionForwardOffset));
            var shipNose = _worldMap.WrapPosition(position + (horizontalForward * ShipCollisionNoseOffset));
            var shipLeft = _worldMap.WrapPosition(shipCenter - (horizontalRight * ShipCollisionHalfWidth));
            var shipRight = _worldMap.WrapPosition(shipCenter + (horizontalRight * ShipCollisionHalfWidth));
            var terrainTopY = MathF.Max(GetCollisionSurfaceTopY(shipCenter), GetCollisionSurfaceTopY(shipNose));
            terrainTopY = MathF.Max(terrainTopY, GetCollisionSurfaceTopY(shipLeft));
            terrainTopY = MathF.Max(terrainTopY, GetCollisionSurfaceTopY(shipRight));

            var shipCenterYOffset = GetShipCenterYOffset(lookDirection);
            var shipBottomExtent = _payloadReleased ? ShipCollisionBottomExtent : PayloadCollisionBottomExtent;
            var shipBottomYOffset = shipCenterYOffset - shipBottomExtent;
            return terrainTopY - shipBottomYOffset + ShipCollisionClearance;
        }

        private void TryReleasePayload(Vector3 lookDirection)
        {
            if (_payloadReleased || _worldMap == null)
            {
                return;
            }

            var horizontalForward = new Vector2(lookDirection.X, lookDirection.Z);
            if (horizontalForward.LengthSquared() < 0.0001f)
            {
                horizontalForward = new Vector2(MathF.Cos(_flightHeading), MathF.Sin(_flightHeading));
            }
            else
            {
                horizontalForward.Normalize();
            }

            var payloadPosition = _worldMap.WrapPosition(_flightPosition + (horizontalForward * ShipCollisionForwardOffset));
            var shipCenterY = ConvertFlightAltitudeToEyeY(_flightAltitude) + GetShipCenterYOffset(lookDirection);
            _fallingPayload = new FallingPayloadState(
                payloadPosition,
                shipCenterY - PayloadReleaseYOffset,
                new Vector2(lookDirection.X * _forwardVelocity, lookDirection.Z * _forwardVelocity),
                _verticalVelocity + (lookDirection.Y * _forwardVelocity) - 3f);
            _fallingPayloadActive = true;
            _payloadReleased = true;
        }

        private void UpdateFallingPayload(float deltaTime)
        {
            if (!_fallingPayloadActive || _worldMap == null)
            {
                return;
            }

            _fallingPayload.VerticalVelocity -= PayloadDropGravity * deltaTime;
            _fallingPayload.HorizontalVelocity *= MathHelper.Clamp(1f - (PayloadDropHorizontalDrag * deltaTime), 0f, 1f);
            _fallingPayload.Position = _worldMap.WrapPosition(_fallingPayload.Position + (_fallingPayload.HorizontalVelocity * deltaTime));
            _fallingPayload.WorldY += _fallingPayload.VerticalVelocity * deltaTime;

            var surfaceY = GetCollisionSurfaceTopY(_fallingPayload.Position);
            if (_fallingPayload.WorldY > surfaceY)
            {
                return;
            }

            var landedCubePosition = _worldMap.WrapPosition(new Vector2(
                MathF.Floor(_fallingPayload.Position.X) + 0.5f,
                MathF.Floor(_fallingPayload.Position.Y) + 0.5f));
            var landedCubeHeight = MathHelper.Max(_worldMap.SampleVoxelHeight(landedCubePosition.X, landedCubePosition.Y), ProceduralWorldMap.SeaLevel);
            var landedCubeTopY = GetTerrainTopY(landedCubeHeight, _worldMap.MaxCubeColumn);
            var isSea = landedCubeHeight <= ProceduralWorldMap.SeaLevel;
            _oilPlatforms.Add(new HeightMapFlyoverRenderer.OilPlatformInstance(landedCubePosition, landedCubeTopY, isSea, _worldTime));
            _fallingPayloadActive = false;
        }

        private void TryFireMissile(Vector3 lookDirection)
        {
            if (_worldMap == null || _usePlanView || _activeMissile.HasValue)
            {
                return;
            }

            var missileDirection = Vector3.Normalize(lookDirection);
            var (shipCenterPosition, shipCenterWorldY) = GetShipWorldCenter(_flightPosition, missileDirection);
            var spawnPosition = _worldMap.WrapPosition(shipCenterPosition + new Vector2(missileDirection.X, missileDirection.Z) * 0.9f);
            var spawnWorldY = shipCenterWorldY + (missileDirection.Y * 0.9f);
            _activeMissile = new MissileState(spawnPosition, spawnWorldY, missileDirection, 0f, DefaultMissileExplosionRadius);
        }

        private void UpdateMissile(float deltaTime)
        {
            if (!_activeMissile.HasValue || _worldMap == null)
            {
                return;
            }

            var missile = _activeMissile.Value;
            var stepDistance = MissileSpeed * deltaTime;
            var nextPosition = _worldMap.WrapPosition(missile.Position + new Vector2(missile.Direction.X, missile.Direction.Z) * stepDistance);
            var nextWorldY = missile.WorldY + (missile.Direction.Y * stepDistance);
            var traveledDistance = missile.TraveledDistance + stepDistance;

            missile = missile with { Position = nextPosition, WorldY = nextWorldY, TraveledDistance = traveledDistance };
            _activeMissile = missile;

            var impactSurfaceY = GetCollisionSurfaceTopY(missile.Position);
            if (missile.WorldY <= impactSurfaceY || traveledDistance >= MissileMaxRange)
            {
                ExplodeMissile(missile);
            }
        }

        private void ExplodeMissile(MissileState missile)
        {
            if (_worldMap == null)
            {
                _activeMissile = null;
                return;
            }

            _worldMap.DestroyTerrainSphere(missile.Position, missile.WorldY, missile.ExplosionRadius, ProtectedTerrainColumnHeight);
            SpawnMissileDebris(missile);
            RefreshWorldVisuals();
            _activeMissile = null;
        }

        private void SpawnMissileDebris(MissileState missile)
        {
            var random = new Random((int)(_worldTime * 1000f) ^ _worldMap.Seed);
            for (var index = 0; index < MissileDebrisCount; index++)
            {
                var yaw = MathHelper.Lerp(0f, MathHelper.TwoPi, (float)random.NextDouble());
                var pitch = MathHelper.Lerp(-0.1f, MathHelper.PiOver2, (float)random.NextDouble());
                var direction = Vector3.Normalize(new Vector3(
                    MathF.Cos(yaw) * MathF.Cos(pitch),
                    MathF.Sin(pitch),
                    MathF.Sin(yaw) * MathF.Cos(pitch)));
                var speed = MathHelper.Lerp(18f, 64f, (float)random.NextDouble());
                var lifetime = MathHelper.Lerp(MissileDebrisLifetimeMin, MissileDebrisLifetimeMax, (float)random.NextDouble());
                _missileDebris.Add(new HeightMapFlyoverRenderer.MissileDebrisParticle(
                    missile.Position,
                    missile.WorldY,
                    MathHelper.Lerp(0.18f, 0.42f, (float)random.NextDouble()),
                    1f));
            }
        }

        private void UpdateMissileDebris(float deltaTime)
        {
            if (_missileDebris.Count == 0)
            {
                return;
            }

            for (var index = _missileDebris.Count - 1; index >= 0; index--)
            {
                var debris = _missileDebris[index];
                var nextAlpha = debris.Alpha - (deltaTime * 0.9f);
                if (nextAlpha <= 0f)
                {
                    _missileDebris.RemoveAt(index);
                    continue;
                }

                _missileDebris[index] = debris with { WorldY = debris.WorldY + (deltaTime * 8f), Alpha = nextAlpha };
            }
        }

        private HeightMapFlyoverRenderer.MissileWorldRenderState? GetMissileRenderState()
        {
            if (!_activeMissile.HasValue)
            {
                return null;
            }

            var missile = _activeMissile.Value;
            return new HeightMapFlyoverRenderer.MissileWorldRenderState(missile.Position, missile.WorldY, missile.Direction);
        }

        private void RefreshWorldVisuals()
        {
            if (_worldMap == null)
            {
                return;
            }

            var colorMap = _worldMap.CreateColorMap();
            _heightMapTexture?.Dispose();
            _heightMapTexture = new Texture2D(GraphicsDevice, _worldMap.Width, _worldMap.Height);
            _heightMapTexture.SetData(colorMap);
            _horizontalViewRenderer?.Dispose();
            _horizontalViewRenderer = null;
            _horizontalViewRenderSize = Point.Zero;
        }

        private static float GetShipCenterYOffset(Vector3 lookDirection)
        {
            var cameraRight = Vector3.Cross(Vector3.Up, lookDirection);
            if (cameraRight.LengthSquared() < 0.0001f)
            {
                cameraRight = Vector3.Cross(Vector3.Forward, lookDirection);
            }

            cameraRight.Normalize();
            var cameraUp = Vector3.Normalize(Vector3.Cross(lookDirection, cameraRight));
            return (lookDirection.Y * ShipRenderDistance) + (cameraUp.Y * ShipRenderVerticalOffset);
        }

        private (Vector2 Position, float WorldY) GetShipWorldCenter(Vector2 cameraPosition, Vector3 lookDirection)
        {
            var cameraRight = Vector3.Cross(Vector3.Up, lookDirection);
            if (cameraRight.LengthSquared() < 0.0001f)
            {
                cameraRight = Vector3.Cross(Vector3.Forward, lookDirection);
            }

            cameraRight.Normalize();
            var cameraUp = Vector3.Normalize(Vector3.Cross(lookDirection, cameraRight));
            var shipOffset = (lookDirection * ShipRenderDistance) + (cameraUp * ShipRenderVerticalOffset);
            return (
                _worldMap.WrapPosition(cameraPosition + new Vector2(shipOffset.X, shipOffset.Z)),
                ConvertFlightAltitudeToEyeY(_flightAltitude) + shipOffset.Y);
        }

        private float GetCollisionSurfaceTopY(Vector2 position)
        {
            var maxTerrainHeight = _worldMap.SampleHeight(position.X, position.Y);
            maxTerrainHeight = MathF.Max(maxTerrainHeight, _worldMap.SampleHeight(position.X - CameraCollisionExtent, position.Y - CameraCollisionExtent));
            maxTerrainHeight = MathF.Max(maxTerrainHeight, _worldMap.SampleHeight(position.X + CameraCollisionExtent, position.Y - CameraCollisionExtent));
            maxTerrainHeight = MathF.Max(maxTerrainHeight, _worldMap.SampleHeight(position.X - CameraCollisionExtent, position.Y + CameraCollisionExtent));
            maxTerrainHeight = MathF.Max(maxTerrainHeight, _worldMap.SampleHeight(position.X + CameraCollisionExtent, position.Y + CameraCollisionExtent));
            return GetTerrainTopY(MathHelper.Max(maxTerrainHeight, ProceduralWorldMap.SeaLevel), _worldMap?.MaxCubeColumn ?? WorldGenerationSettings.DefaultMaxCubeColumn);
        }

        private static float GetTerrainTopY(float surfaceHeight)
        {
            return GetTerrainTopY(surfaceHeight, WorldGenerationSettings.DefaultMaxCubeColumn);
        }

        private static float GetTerrainTopY(float surfaceHeight, int maxCubeColumn)
        {
            var columnHeight = Math.Clamp((int)MathF.Round(surfaceHeight * (maxCubeColumn - 1)), 1, maxCubeColumn - 1);
            var renderVerticalOrigin = ProceduralWorldMap.SeaLevel * (maxCubeColumn - 1f);
            return ((columnHeight - renderVerticalOrigin) * RenderCubeHeight) + RenderFaceOverlap;
        }

        private static float ConvertEyeYToFlightAltitude(float eyeY)
        {
            var eyeRatio = MathHelper.Clamp((eyeY - RenderCameraMinEyeY) / (RenderCameraMaxEyeY - RenderCameraMinEyeY), 0f, 1f);
            return FlightAltitudeMinimum + (eyeRatio * (MaxFlightAltitude - FlightAltitudeMinimum));
        }

        private static float ConvertFlightAltitudeToEyeY(float flightAltitude)
        {
            var altitudeRatio = MathHelper.Clamp((flightAltitude - FlightAltitudeMinimum) / (MaxFlightAltitude - FlightAltitudeMinimum), 0f, 1f);
            return MathHelper.Lerp(RenderCameraMinEyeY, RenderCameraMaxEyeY, altitudeRatio);
        }

        private static float AccelerateTowards(float current, float target, float acceleration, float deltaTime)
        {
            var step = acceleration * deltaTime;
            if (current < target)
            {
                return MathF.Min(current + step, target);
            }

            if (current > target)
            {
                return MathF.Max(current - step, target);
            }

            return current;
        }

        private void StartWorldGeneration()
        {
            StartWorldGeneration(DefaultWorldSeed);
        }

        private void StartWorldGeneration(int worldSeed)
        {
            if (_worldGenerationTask != null)
            {
                return;
            }

            _worldGenerationProgress = 0f;
            _worldGenerationStatus = "Generating terrain noise";
            _worldTime = 0f;
            _payloadReleased = false;
            _fallingPayloadActive = false;
            _oilPlatforms.Clear();
            _activeMissile = null;
            _missileDebris.Clear();
            _forwardVelocity = 0f;
            _turnVelocity = 0f;
            _verticalVelocity = 0f;
            _heightMapTexture?.Dispose();
            _heightMapTexture = null;
            _horizontalViewRenderer?.Dispose();
            _horizontalViewRenderer = null;
            _horizontalViewRenderSize = Point.Zero;
            _mouseLookInitialized = false;
            _worldMap = null;
            var generationSettings = _generationSettings.WithSeed(worldSeed);
            _generationSettings = generationSettings;
            _worldGenerationTask = Task.Run(() =>
            {
                var worldMap = new ProceduralWorldMap(WorldSize, WorldSize, generationSettings.Seed, generationSettings, UpdateWorldGenerationProgress);
                var colorMap = worldMap.CreateColorMap(UpdateWorldGenerationProgress);
                return (worldMap, colorMap);
            });

            DebugLogger.Info($"Started procedural world generation {WorldSize}x{WorldSize} using seed {generationSettings.Seed}, theme {generationSettings.Theme.DisplayName}, mountains {generationSettings.MountainIntensity:0.00}, volcanoes {generationSettings.VolcanoIntensity:0.00}, craters {generationSettings.CraterIntensity:0.00}, gorges {generationSettings.GorgeIntensity:0.00}.");
        }

        private void TryCompleteWorldGeneration()
        {
            if (_worldGenerationTask == null || !_worldGenerationTask.IsCompleted)
            {
                return;
            }

            if (_worldGenerationTask.IsFaulted)
            {
                DebugLogger.Critical("World generation failed.", _worldGenerationTask.Exception?.GetBaseException());
                throw _worldGenerationTask.Exception?.GetBaseException() ?? new InvalidOperationException("World generation failed.");
            }

            var result = _worldGenerationTask.Result;
            _worldMap = result.WorldMap;
            _heightMapTexture?.Dispose();
            _heightMapTexture = new Texture2D(GraphicsDevice, _worldMap.Width, _worldMap.Height);
            _heightMapTexture.SetData(result.ColorMap);

            _horizontalViewRenderer?.Dispose();
            _horizontalViewRenderer = null;
            _horizontalViewRenderSize = Point.Zero;

            _flightPosition = new Vector2(_worldMap.Width * 0.35f, _worldMap.Height * 0.28f);
            _flightHeading = MathF.PI * 0.5f;
            _flightPitch = 0f;
            _payloadReleased = false;
            _fallingPayloadActive = false;
            _oilPlatforms.Clear();
            _activeMissile = null;
            _missileDebris.Clear();
            _flightAltitude = _terrainFollowMode ? GetTerrainFollowAltitude(_flightPosition, GetLookDirection()) : 3000f;
            _worldGenerationProgress = 1f;
            _worldGenerationStatus = "World ready";
            _worldGenerationTask = null;

            DebugLogger.Info($"Generated procedural world map {_worldMap.Width}x{_worldMap.Height} using seed {_worldMap.Seed}.");
        }

        private void UpdateWorldGenerationProgress(float progress, string status)
        {
            _worldGenerationProgress = MathHelper.Clamp(progress, 0f, 1f);
            _worldGenerationStatus = status;
        }

        private void DrawOilPlatformMarkers(Rectangle minimapRectangle)
        {
            for (var index = 0; index < _oilPlatforms.Count; index++)
            {
                var platform = _oilPlatforms[index];
                var pulse = 0.5f + (MathF.Sin((_worldTime * 4.4f) + platform.ActivationTime + (index * 0.35f)) * 0.5f);
                var markerColor = Color.Lerp(new Color(255, 224, 80), new Color(255, 64, 48), pulse);
                var markerX = minimapRectangle.X + (int)((platform.Position.X / _worldMap.Width) * minimapRectangle.Width);
                var markerY = minimapRectangle.Y + (int)((platform.Position.Y / _worldMap.Height) * minimapRectangle.Height);
                var markerSize = 4 + (int)MathF.Round(pulse * 3f);
                var halfSize = markerSize / 2;

                _spriteBatch.Draw(_pixel, new Rectangle(markerX - halfSize, markerY - halfSize, markerSize, markerSize), markerColor);
                _spriteBatch.Draw(_pixel, new Rectangle(markerX - markerSize, markerY, markerSize * 2, 1), markerColor * 0.8f);
                _spriteBatch.Draw(_pixel, new Rectangle(markerX, markerY - markerSize, 1, markerSize * 2), markerColor * 0.8f);
            }
        }

        private struct FallingPayloadState
        {
            public FallingPayloadState(Vector2 position, float worldY, Vector2 horizontalVelocity, float verticalVelocity)
            {
                Position = position;
                WorldY = worldY;
                HorizontalVelocity = horizontalVelocity;
                VerticalVelocity = verticalVelocity;
            }

            public Vector2 Position;

            public float WorldY;

            public Vector2 HorizontalVelocity;

            public float VerticalVelocity;
        }

        private readonly record struct MissileState(Vector2 Position, float WorldY, Vector3 Direction, float TraveledDistance, float ExplosionRadius);

    }
}
