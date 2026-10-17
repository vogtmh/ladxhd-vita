using System;
using System.IO;
using System.Threading;
using GBSPlayer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Base;
using ProjectZ.Base.UI;
using ProjectZ.InGame.Audio;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Pages;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Screens;
using ProjectZ.InGame.Things;

namespace ProjectZ
{
    public class Game1 : Game
    {
        public static Game1 Instance;
        public static GraphicsDeviceManager Graphics;
        public static SpriteBatch SpriteBatch;
        public static AudioManager AudioManager = new AudioManager();
        public static UiManager UiManager = new UiManager();
        public static ScreenManager ScreenManager = new ScreenManager();
        public static PageManager UiPageManager = new PageManager();
        public static Language LanguageManager = new Language();
        public static GameManager GameManager;
        public static GbsPlayer GbsPlayer = new GbsPlayer();
        public static Random RandomNumber = new Random();
        public static CameraField CameraObjects = new CameraField();

        public static IEditorManager EditorManager;
        public static IPlatformFileSystem PlatformFileSystem { get; private set; } = new LocalPlatformFileSystem();
        public static IUserDataPaths UserDataPaths { get; private set; } = new LocalUserDataPaths();
        public static ISharedSaveService SharedSaveService { get; private set; } = new UnavailableSharedSaveService();
        public static IPlatformInput PlatformInput { get; private set; } = new NullPlatformInput();
        public static ITextInputService TextInputService { get; private set; } = new NullTextInputService();
        public static IPlatformWindow PlatformWindow { get; private set; } = new DefaultPlatformWindow(true, true);
        public static IPlatformWindowLifecycle PlatformWindowLifecycle { get; private set; }
        public static IGraphicsCapabilities GraphicsCapabilities { get; private set; } = new GraphicsCapabilities(false, true, true, true);
        public static IPlatformPresentation PlatformPresentation { get; private set; } = new PlatformPresentation(256, false, false, 0);
        public static IFileDialogService FileDialogService { get; private set; } = new UnavailableFileDialogService();
        public static IPhotoPrintService PhotoPrintService { get; private set; } = new UnavailablePhotoPrintService();

        public static int WindowWidth;
        public static int WindowHeight;
        public static int WindowWidthEnd;
        public static int WindowHeightEnd;
        public static int RenderWidth;
        public static int RenderHeight;

        public static bool FullScreen;

        private bool _firstFrameDrawn;
        private bool _usesFixedBackBuffer;
        private readonly bool _requestedEditorMode;

        public static bool FpsSettingChanged;
        private readonly SimpleFps _fpsCounter = new SimpleFps();

        public static double FreezeTime;
        public static float TimeMultiplier;
        public static float DeltaTime;
        public static double TotalTime;
        public static double TotalGameTime;
        public static double TotalGameTimeLast;

        private static DoubleAverage _avgTotalMs = new DoubleAverage(30);
        private static DoubleAverage _avgTimeMult = new DoubleAverage(30);

        public static RenderTarget2D MainRenderTarget;
        private static RenderTarget2D _renderTarget1;
        private static RenderTarget2D _renderTarget2;
        private static RenderTarget2D _finalRenderTarget;
        private static bool _initRenderTargets;

        private const double _startDelayTime = 1.5;
        private double _startDelayElapsed;
        private bool _startDelayFinished;

        public static int MaxGameScale;
        public static int UiScale;
        public static bool ScaleChanged;

        public static bool WasActive;
        public static bool UpdateGame;
        public static bool ForceDialogUpdate;
        public static bool EditorMode;
        public static bool EditorOpen;
        public static bool SaveAndExitGame;
        public static bool AutoLoadSave;
        public static int AutoLoadSlot;

        private static volatile bool _finishedLoading;
        private static volatile bool _isExiting;

        public static string DebugText;
        public static float DebugTimeScale = 1.0f;
        public static bool DebugStepper;
        public static int DebugLightMode;
        public static int DebugBoxMode;
        public static bool DebugMode;
        public static bool ShowDebugText;
        private Vector2 _debugTextSize;

        // Keyboard keys for debug functions.
        public static Keys DebugEditor        = Keys.Escape;
        public static Keys DebugToggleText    = Keys.F1;
        public static Keys DebugToggleMode    = Keys.F2;
        public static Keys DebugBox           = Keys.F3;
        public static Keys DebugReloadMap     = Keys.F4;
        public static Keys DebugSaveKey       = Keys.F5;
        public static Keys DebugLoadKey       = Keys.F6;
        public static Keys DebugShadowKey     = Keys.F9;
        public static Keys DebugStepStart     = Keys.Insert;
        public static Keys DebugStepNext      = Keys.Home;
        public static Keys DebugTimeScaleUp   = Keys.Add;
        public static Keys DebugTimeScaleDown = Keys.Subtract;

        // True when in-game after selecting save file. False at main menu and intro.
        public static bool InProgress;

        // False when resources are loading during intro. True when resources are fully loaded in.
        public static bool FinishedLoading => _finishedLoading;

        // Values configurable via lahdmod.
        private int   max_game_scale   = 20;
        private bool  background_input = false;
        private bool  editor_mode      = false;
        private float pixel_grid_alpha = 0.20f;
        public static float PixelGridAlpha;

        public static Matrix GetMatrix
        {
            get
            {
                var gd = Graphics?.GraphicsDevice;
                if (gd == null || WindowWidth <= 0 || WindowHeight <= 0)
                    return Matrix.Identity;

                // Viewport represents the actual usable physical pixels (e.g. safe area on devices with a "notch")
                var viewport = gd.Viewport;
                return Matrix.CreateScale(
                    (float)viewport.Width / WindowWidth,
                    (float)viewport.Height / WindowHeight, 1f);
            }
        }

        public Game1(bool editorMode, bool loadSave, int loadSlot)
        {
            // Detect when the game is exiting.
            Exiting += OnGameExiting;

            _requestedEditorMode = editorMode;

            // Create the graphics device and set the back buffer width/height.
            Graphics = new GraphicsDeviceManager(this);

        #if DEBUG
            EditorMode = true;
        #endif

            Graphics.GraphicsProfile = GraphicsProfile.HiDef;
            Graphics.PreferredBackBufferWidth = Values.MinWidth * 3;
            Graphics.PreferredBackBufferHeight = Values.MinHeight * 3;

            Graphics.ApplyChanges();

            // Store any command line parameters if available.
            AutoLoadSave = loadSave;
            AutoLoadSlot = loadSlot;

            // Set the content directory.
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            Instance = this;

            // The "advanced" file needs to be cached before loading in GameManager.
            ModFile.LoadAdvancedCache();

            EditorManager = Services.GetService(typeof(IEditorManager)) as IEditorManager;
            PlatformFileSystem = (IPlatformFileSystem)Services.GetService(typeof(IPlatformFileSystem)) ?? PlatformFileSystem;
            UserDataPaths = (IUserDataPaths)Services.GetService(typeof(IUserDataPaths)) ?? UserDataPaths;
            SharedSaveService = (ISharedSaveService)Services.GetService(typeof(ISharedSaveService)) ?? SharedSaveService;
            PlatformInput = (IPlatformInput)Services.GetService(typeof(IPlatformInput)) ?? PlatformInput;
            TextInputService = (ITextInputService)Services.GetService(typeof(ITextInputService)) ?? TextInputService;
            PlatformWindow = (IPlatformWindow)Services.GetService(typeof(IPlatformWindow)) ?? PlatformWindow;
            PlatformWindowLifecycle = Services.GetService(typeof(IPlatformWindowLifecycle)) as IPlatformWindowLifecycle;
            GraphicsCapabilities = (IGraphicsCapabilities)Services.GetService(typeof(IGraphicsCapabilities)) ?? GraphicsCapabilities;
            PlatformPresentation = (IPlatformPresentation)Services.GetService(typeof(IPlatformPresentation)) ?? PlatformPresentation;
            FileDialogService = (IFileDialogService)Services.GetService(typeof(IFileDialogService)) ?? FileDialogService;
            PhotoPrintService = (IPhotoPrintService)Services.GetService(typeof(IPhotoPrintService)) ?? PhotoPrintService;

            _usesFixedBackBuffer = Services.GetService(typeof(IPlatformDisplayConfiguration)) != null;
            GameManager = new GameManager();
            TextInputService.SetEnabled(false);
            Activated += (_, _) => TextInputService.OnGameActivated();
            PlatformWindowLifecycle?.Initialize(this);

            // Try to load in a "Game1.lahdmod" or the settings from the "advanced" file.
            ModFile.Parse(Path.Combine(Values.PathLAHDMods, "Game1.lahdmod"), this);
            MaxGameScale = max_game_scale;
            PixelGridAlpha = pixel_grid_alpha;
            GameSettings.GameScale = max_game_scale + 1;
            GameSettings.SeqScaleAmplify = PlatformPresentation.DefaultSequenceScaleAmplify;
            EditorMode = _requestedEditorMode || editor_mode;

            // Android should default this to "false", while desktop should default it to "true". Set it 
            // before the users settings are loaded so it only applies the first time the option is created.
            GameSettings.ThreadMapLoad = UserDataPaths.DefaultMapThreadLoad;

            if (Services.GetService(typeof(IPlatformDisplayConfiguration)) is IPlatformDisplayConfiguration display)
            {
                var preferredWidth = display.PreferredBackBufferWidth;
                var preferredHeight = display.PreferredBackBufferHeight;

                // Match the former Android constructor fallback when window metrics are unavailable.
                if (preferredWidth <= 0 || preferredHeight <= 0)
                {
                    var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                    preferredWidth = displayMode.Width;
                    preferredHeight = displayMode.Height;
                }

                if (preferredWidth > 0 && preferredHeight > 0)
                {
                    Graphics.PreferredBackBufferWidth = preferredWidth;
                    Graphics.PreferredBackBufferHeight = preferredHeight;
                    Graphics.ApplyChanges();

                }
            }

            base.Initialize();
        }

        protected override void LoadContent()
        {
            // Packaged mod assets are read-only; AndroidActivity creates its writable layout before startup.
            if (UserDataPaths.ShouldCreateModsDirs)
            {
                Directory.CreateDirectory(Values.PathMods);
                Directory.CreateDirectory(Values.PathAnimationMods);
                Directory.CreateDirectory(Values.PathDungeonMods);
                Directory.CreateDirectory(Values.PathGraphicsMods);
                Directory.CreateDirectory(Values.PathMusicMods);
                Directory.CreateDirectory(Values.PathLanguageMods);
                Directory.CreateDirectory(Values.PathLAHDMods);
                Directory.CreateDirectory(Values.PathMapMods);
                Directory.CreateDirectory(Values.PathSoundEffectMods);
            }

            // Hook device reset function & create a new SpriteBatch to draw textures.
            Graphics.GraphicsDevice.DeviceReset += OnDeviceReset;
            SpriteBatch = new SpriteBatch(Graphics.GraphicsDevice);

            // Initialize controller and input handler.
            ControlHandler.Initialize();
            Components.Add(new InputHandler(this));

            // Initialize touch screen controls for mobile devices.
            if (PlatformInput.HasTouchInput)
                VirtualController.Initialize(Graphics.PreferredBackBufferWidth, Graphics.PreferredBackBufferHeight, true);

            // Load the users saved settings.
            SettingsSaveLoad.LoadSettings();
            PlatformWindowLifecycle?.ApplySavedSettings(this);
            AchievementManager.Load();
            AchievementManager.SyncWithShared();

            // Load the Intro Screen and its resources.
            AudioManager.UpdateSoundEffects();
            Resources.LoadIntro(Graphics.GraphicsDevice, Content);
            Resources.LoadShaders(Content);
            ScreenManager.LoadIntro(Content);

            // Start loading the resources that are needed after the intro.
            // Vulkan cannot create GPU resources off the main thread, so load synchronously there.
            bool threadLoad = GameSettings.StreamAssets && GraphicsCapabilities.CanCreateGraphicsResourcesOnWorkerThread;

            if (threadLoad)
                ThreadPool.QueueUserWorkItem(LoadContentThreaded);
            else
                LoadContentThreaded(null);

            // Initialize the GBS Player and load in the Link's Awakening GBS file.
            GbsPlayer.LoadFile(Path.Combine(Values.PathDataFolder, "Music", "awakening.gbs"));
            GbsPlayer.StartThread();

            // set the fps settings of the game
            UpdateFpsSettings();

            // Initialize extra monster hit points set by the user.
            EnemyLives.Initialize();
        }

        private void LoadContentThreaded(Object obj)
        {
            // Works around a strange bug that crashes when the game closes.
            if (_isExiting) return;

            // Load all of the game's resources.
            Resources.LoadBlurEffect(Content);
            Resources.LoadTextures(Graphics.GraphicsDevice, Content);
            Resources.LoadSounds(Content);
            GameManager.Load(Content);

            // Set up all of the GameObject templates.
            GameObjectTemplates.SetUpGameObjects();

            // Finish loading in resources.
            ScreenManager.Load(Content);
            LanguageManager.Load();
            UiPageManager.Load(Content);

            // Set up the editor if enabled.
            if (EditorMode)
                EditorManager?.SetUpEditorUi();

            // Flag that the thread has finished loading in content.
            _finishedLoading = true;

            // Now that everything has been loaded in, make sure the proper language textures are reloaded.
            Resources.RefreshDynamicResources();
        }

        protected override void Update(GameTime gameTime)
        {
            PlatformWindowLifecycle?.Update(this);

            // Startup black screen delay.
            if (!_startDelayFinished)
            {
                _startDelayElapsed += gameTime.ElapsedGameTime.TotalSeconds;

                if (Graphics.GraphicsDevice != null)
                {
                    var pp = Graphics.GraphicsDevice.PresentationParameters;
                    var ppW = GraphicsCapabilities.UsePresentationParametersForSize
                        ? pp.BackBufferWidth
                        : (pp.BackBufferWidth > 0 ? pp.BackBufferWidth : Window.ClientBounds.Width);
                    var ppH = GraphicsCapabilities.UsePresentationParametersForSize
                        ? pp.BackBufferHeight
                        : (pp.BackBufferHeight > 0 ? pp.BackBufferHeight : Window.ClientBounds.Height);
                    if (WindowWidth != ppW || WindowHeight != ppH)
                        RefreshPresentationSize();
                }
                else if (WindowWidth != Window.ClientBounds.Width || WindowHeight != Window.ClientBounds.Height)
                    RefreshPresentationSize();

                if (_startDelayElapsed < _startDelayTime)
                    return;

                _startDelayFinished = true;
            }
            // If exclusive fullscreen mode is enabled.
            if (_firstFrameDrawn)
                PlatformWindowLifecycle?.ApplyInitialFullscreen(this);
            // Prevent input when window is in background.
            if (!background_input)
                WasActive = IsActive;
            // If enabled allow background input.
            else
                WasActive = true;

            // Hide the mouse cursor unless the editor is open.
            IsMouseVisible = EditorMode && EditorOpen;

            // Mute music and sound effects if user disabled on inactive window.
            AudioManager.HandleInactiveWindow(IsActive);

            // Updates the FPS counter.
            _fpsCounter.Update(gameTime);

            // Initialize render targets if thread is finished loading resources and they have not been initialized yet. 
            if (_finishedLoading && !_initRenderTargets)
            {
                _initRenderTargets = true;
                WindowWidth = 0;
                WindowHeight = 0;
                WindowWidthEnd = 0;
                WindowHeightEnd = 0;
            }

            if (GraphicsCapabilities.UsePresentationParametersForSize && Graphics.GraphicsDevice != null)
            {
                var pp = Graphics.GraphicsDevice.PresentationParameters;
                if (WindowWidth != pp.BackBufferWidth || WindowHeight != pp.BackBufferHeight)
                    RefreshPresentationSize();
            }
            else if (WindowWidth != Window.ClientBounds.Width || WindowHeight != Window.ClientBounds.Height)
                RefreshPresentationSize();

            // Update the scale if it has been changed.
            if (ScaleChanged)
                UpdateScale();

            // If the FPS settings has changed then update them.
            if (FpsSettingChanged)
            {
                UpdateFpsSettings();
                FpsSettingChanged = false;
            }

            if (PlatformInput.HasTouchInput)
                VirtualController.Update();

            // Update input from any input devices.
            ControlHandler.Update();

            // Update all render targets.
            UpdateRenderTargets();

            // When the content thread is finished loading.
            if (_finishedLoading)
            {
                if (EditorMode && EditorManager != null)
                {
                    UiManager.Update();
                    EditorManager.EditorUpdate(gameTime);
                }
                // Update the UI.
                UiManager.CurrentScreen = "";
                UiPageManager.Update(gameTime);
            }
            // If editor is enabled and F1 key is pressed.
            if (EditorMode && _finishedLoading && InputHandler.KeyPressed(DebugToggleText))
                ShowDebugText = !ShowDebugText;

            // Debug Stepper (N Key) is not active.
            if (!DebugStepper)
            {
                TimeMultiplier = gameTime.ElapsedGameTime.Ticks / 166667f * DebugTimeScale;
                TotalGameTimeLast = TotalGameTime;

                // limit the game time so that it slows down if the steps are bigger than they would be for 30fps
                // if the timesteps get too big it would be hard (wast of time) to make the logic still function 100% correctly
                if (TimeMultiplier > 2.0f)
                {
                    TimeMultiplier = 2.0f;
                    DeltaTime = (TimeMultiplier * 1000.0f) / 60.0f;
                    TotalTime += (TimeMultiplier * 1000.0) / 60.0;
                    DebugText += "\nLow Framerate";

                    if (UpdateGame)
                        TotalGameTime += (TimeMultiplier * 1000.0) / 60.0;
                }
                else
                {
                    DeltaTime = (float)gameTime.ElapsedGameTime.TotalMilliseconds * DebugTimeScale;
                    TotalTime += gameTime.ElapsedGameTime.TotalMilliseconds * DebugTimeScale;
                    if (UpdateGame)
                        TotalGameTime += gameTime.ElapsedGameTime.TotalMilliseconds * DebugTimeScale;
                }
            }
            // Update the screen manager.
            UpdateGame = true;

            if (!DebugStepper || InputHandler.KeyPressed(Keys.M))
                ScreenManager.Update(gameTime);

            // Pump GBS audio on the game thread
            GbsPlayer.Pump();

            if (_finishedLoading && ShowDebugText)
            {
                DebugText += _fpsCounter.Msg;

                _avgTotalMs.AddValue(gameTime.ElapsedGameTime.TotalMilliseconds);
                _avgTimeMult.AddValue(TimeMultiplier);
                DebugText += $"\ntotal ms:      {_avgTotalMs.Average,6:N3}" +
                             $"\ntime mult:     {_avgTimeMult.Average,6:N3}" +
                             $"\ntime scale:    {DebugTimeScale}" +
                             $"\ntime:          {TotalGameTime}";

                DebugText += "\nHistory Enabled: " + GameManager.SaveManager.HistoryEnabled;

                if (MapManager.ObjLink != null)
                {
                    var pos = MapManager.ObjLink.EntityPosition;

                    DebugText += "\nPosition X: " + pos.X;
                    DebugText += "\nPosition Y: " + pos.Y;
                    DebugText += "\nPosition Z: " + pos.Z;
                    DebugText += "\nCurrentState: " + MapManager.ObjLink.CurrentState;
                }
            }
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            if (!_startDelayFinished)
            {
                Graphics.GraphicsDevice.Clear(Color.Black);
                return;
            }
            _firstFrameDrawn = true;

            _fpsCounter.CountDraw();

            // The intro screen sets this again while drawing if it wants the pixel grid.
            IntroScreen.PixelGridRequested = false;

            ScreenManager.DrawRT(SpriteBatch);
            if (MainRenderTarget == null)
            {
                Graphics.GraphicsDevice.Clear(Color.CadetBlue);
                ScreenManager.Draw(SpriteBatch);
                return;
            }

            Graphics.GraphicsDevice.SetRenderTarget(MainRenderTarget);
            Graphics.GraphicsDevice.Clear(Color.CadetBlue);
            ScreenManager.Draw(SpriteBatch);

            // Detect if the user disabled blur and pass to the shader.
            var blurDisabled = GameSettings.DisableUiBlur;
            var blurParameter = blurDisabled ? 0f : 1f;

            // Blur the screen if it's not disabled.
            if (!blurDisabled)
                BlurImage();

            Graphics.GraphicsDevice.SetRenderTarget(null);
            Graphics.GraphicsDevice.Clear(Color.Black);

            // Preserve the backend-provided viewport, including any platform safe-area offset.
            var backBufferViewport = Graphics.GraphicsDevice.Viewport;

            Graphics.GraphicsDevice.SetRenderTarget(_finalRenderTarget);
            Graphics.GraphicsDevice.Clear(Color.Black);
            var finalWidth = _finalRenderTarget.Width;
            var finalHeight = _finalRenderTarget.Height;

            // The intro screen has its own camera and scale, so it supplies its own grid values.
            var introGrid = IntroScreen.PixelGridRequested;

            // If the Pixel Grid shader is enabled.
            var drawPixelGrid = !EditorOpen && GameSettings.PixelSnapping && GameSettings.PixelGrid && (InProgress || introGrid);

            // If the Pixel Grid shader is enabled, draw the grid over the game world.
            if (drawPixelGrid)
            {
                var gridScale = introGrid ? IntroScreen.PixelGridScale : MapManager.Camera.Scale;
                var gridOffset = introGrid ? IntroScreen.PixelGridOffset : MapManager.Camera.PixelGridOffset;
                Resources.PixelGrid?.Parameters["TextureSize"]?.SetValue(new Vector2(finalWidth / gridScale, finalHeight / gridScale));
                Resources.PixelGrid?.Parameters["GridOpacity"]?.SetValue(pixel_grid_alpha);
                Resources.PixelGrid?.Parameters["Offset"]?.SetValue(gridOffset);
            }
            // Apply the pixel grid if the user enabled it.
            var doPixelGrid = drawPixelGrid ? Resources.PixelGrid : null;

            // Draw the render target with or without the pixel grid.
            SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, doPixelGrid);
            SpriteBatch.Draw(MainRenderTarget, new Rectangle(0, 0, finalWidth, finalHeight), Color.White);
            SpriteBatch.End();

            if (_renderTarget2 != null)
            {
                Resources.BlurEffect.Parameters["sprBlur"].SetValue(_renderTarget2);
                Resources.RoundedCornerBlurEffect.Parameters["sprBlur"].SetValue(_renderTarget2);
            }
            // These are the dimensions SV_Position is normalized against in the current pass.
            var vp = Graphics.GraphicsDevice.Viewport;
            Resources.BlurEffect.Parameters["width"].SetValue(vp.Width);
            Resources.BlurEffect.Parameters["height"].SetValue(vp.Height);
            Resources.RoundedCornerBlurEffect.Parameters["screenWidth"].SetValue(vp.Width);
            Resources.RoundedCornerBlurEffect.Parameters["screenHeight"].SetValue(vp.Height);
            Resources.RoundedCornerBlurEffect.Parameters["blurEnabled"]?.SetValue(blurParameter);

            // Also prevent texture-unit-1 wrap at runtime (belt-and-suspenders).
            Graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
            SpriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, Resources.RoundedCornerBlurEffect, GetMatrix);

            // Rounded ui backgrounds, blurred only when the blur is enabled.
            if (_finishedLoading)
                UiManager.DrawBlur(SpriteBatch);

            if (!blurDisabled)
            {
                // Draw the save icon and rounded corner effect.
                GameManager?.InGameOverlay?.InGameHud?.DrawSaveIcon(SpriteBatch, true);

                // Draw background for the debug text.
                DebugTextBackground();
            }

            SpriteBatch.End();

            // If the blurring effect is disabled still draw the save icon.
            if (blurDisabled)
            {
                SpriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, GetMatrix);
                GameManager?.InGameOverlay?.InGameHud?.DrawSaveIcon(SpriteBatch, false);
                SpriteBatch.End();
            }

            {
                // draw the top part
                SpriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, GetMatrix);

                // draw the ui part
                if (_finishedLoading)
                    UiManager.Draw(SpriteBatch);

                // draw the game ui
                UiPageManager.Draw(SpriteBatch);

                // draw the screen tops
                ScreenManager.DrawTop(SpriteBatch);

                if (PlatformInput.HasTouchInput)
                    VirtualController.Draw(SpriteBatch);

                // draw the debug text
                DrawDebugText();
                DebugText = "";

            #if DEBUG
                if (GameManager.SaveManager.HistoryEnabled)
                    SpriteBatch.Draw(Resources.SprWhite, new Rectangle(0, WindowHeight - 6, WindowWidth, 6), Color.Red);
            #endif
                SpriteBatch.End();
            }

            // Present to backbuffer (with or without colour correction).
            Graphics.GraphicsDevice.SetRenderTarget(null);
            Graphics.GraphicsDevice.Viewport = backBufferViewport;
            Graphics.GraphicsDevice.Clear(Color.Black);

            // Apply GBC Color Correction if the user enabled it.
            var doColorCorrect = GameSettings.ColorCorrection && _finalRenderTarget != null ? Resources.ColorCorrect : null;

            // Draw the final render target.
            SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, doColorCorrect);
            SpriteBatch.Draw(_finalRenderTarget, new Rectangle(0, 0, backBufferViewport.Width, backBufferViewport.Height), Color.White);
            SpriteBatch.End();

            base.Draw(gameTime);
        }

        private void BlurImage()
        {
            if (MainRenderTarget == null || _renderTarget1 == null || _renderTarget2 == null)
                return;

            var blurValue = 0.2f;

            if (Resources.BlurEffectH == null || Resources.BlurEffectV == null)
                return;

            Resources.BlurEffectH.Parameters["pixelX"].SetValue(1.0f / _renderTarget1.Width);
            Resources.BlurEffectV.Parameters["pixelY"].SetValue(1.0f / _renderTarget1.Height);

            var mult0 = blurValue;
            var mult1 = (1 - blurValue * 2) / 2;
            Resources.BlurEffectH.Parameters["mult0"].SetValue(mult0);
            Resources.BlurEffectH.Parameters["mult1"].SetValue(mult1);
            Resources.BlurEffectV.Parameters["mult0"].SetValue(mult0);
            Resources.BlurEffectV.Parameters["mult1"].SetValue(mult1);

            Graphics.GraphicsDevice.SetRenderTarget(_renderTarget2);

            // Note: When drawing directly to the backbuffer, the orientation of the blurred image is correct in all renderers. Now that it's 
            // being drawn to the render target "_finalRenderTarget", for some reason it gets flipped vertically in OpenGL. To fix this, flip 
            // it back to the correct orientation when OpenGL is the renderer. This does not affect Direct-X 11, so do not do the flip for it.
            var verticalFlipBlur = PlatformWindow.VerticalFlipBlur ? SpriteEffects.FlipVertically : SpriteEffects.None;

            SpriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, null, null);
            SpriteBatch.Draw(MainRenderTarget,new Rectangle(0, 0, _renderTarget2.Width, _renderTarget2.Height), null, Color.White, 0f, Vector2.Zero, verticalFlipBlur, 0f);
            SpriteBatch.End();

            for (var i = 0; i < 2; i++)
            {
                // v blur
                Graphics.GraphicsDevice.SetRenderTarget(_renderTarget1);
                SpriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, Resources.BlurEffectV, null);
                SpriteBatch.Draw(_renderTarget2, Vector2.Zero, Color.White);
                SpriteBatch.End();

                // h blur
                Graphics.GraphicsDevice.SetRenderTarget(_renderTarget2);
                SpriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, Resources.BlurEffectH, null);
                SpriteBatch.Draw(_renderTarget1, Vector2.Zero, Color.White);
                SpriteBatch.End();
            }
        }

        public void TriggerFpsSettings()
        {
            if (!IsFixedTimeStep)
            {
                IsFixedTimeStep = true;
                Graphics.SynchronizeWithVerticalRetrace = false;
            }
            else
            {
                IsFixedTimeStep = false;
                Graphics.SynchronizeWithVerticalRetrace = true;
            }
            PlatformWindowLifecycle?.PreparePresentationReset(this);
            Graphics.ApplyChanges();
        }

        public static void ToggleFullscreen()
        {
            PlatformWindowLifecycle?.ToggleFullscreen(Instance);
        }

        public static void ToggleFullscreenFromHotkey()
        {
            GameSettings.ScreenMode = GameSettings.ScreenMode > 0 ? 0 : 1;
            if (UiPageManager.InsideElement.TryGetValue(typeof(VideoSettingsPage), out var page))
                ((VideoSettingsPage)page).SetFullscreenMode(GameSettings.ScreenMode);

            ToggleFullscreen();
            SettingsSaveLoad.SaveSettings();
        }

        public void UpdateFpsSettings()
        {
            IsFixedTimeStep = false;
            Graphics.SynchronizeWithVerticalRetrace = GameSettings.VerticalSync;
            PlatformWindowLifecycle?.PreparePresentationReset(this);
            Graphics.ApplyChanges();
        }

        public void DebugTextBackground()
        {
            if (!ShowDebugText || !_finishedLoading)
                return;

            _debugTextSize = DrawHelper.MeasureString(DebugText);

            SpriteBatch.Draw(_renderTarget2, new Rectangle(0, 0,
                (int)(_debugTextSize.X * 2) + 20, (int)(_debugTextSize.Y * 2) + 20), Color.White);
        }

        public void DrawDebugText()
        {
            if (!ShowDebugText || !_finishedLoading)
                return;

            SpriteBatch.Draw(Resources.SprWhite, new Rectangle(0, 0, (int)(_debugTextSize.X * 2) + 20, (int)(_debugTextSize.Y * 2) + 20), Color.Black * 0.75f);
            DrawHelper.DrawString(SpriteBatch, DebugText, new Vector2(10), Color.White, 0, Vector2.Zero, 2f, SpriteEffects.None, 0);
        }

        private void OnDeviceReset(object sender, EventArgs e)
        {
            // Update render targets when device resets.
            GameManager?.UpdateRenderTargets();
            UpdateRenderTargetSizes(WindowWidth, WindowHeight);
        }

        public void RefreshPresentationSize()
        {
            int w = 0, h = 0;

            if (Graphics.GraphicsDevice == null)
                return;

            var pp = Graphics.GraphicsDevice.PresentationParameters;
            w = pp.BackBufferWidth;
            h = pp.BackBufferHeight;

            if (w <= 0 || h <= 0)
            {
                w = Window.ClientBounds.Width;
                h = Window.ClientBounds.Height;
            }

            if (w <= 0 || h <= 0)
                return;

            // Store the window size and force a scale change.
            WindowWidth = w;
            WindowHeight = h;
            ScaleChanged = true;

        }

        public void ForceRecalculateScaling()
        {
            int w = 0, h = 0;
            if (GraphicsCapabilities.UsePresentationParametersForSize && Graphics.GraphicsDevice != null)
            {
                var pp = Graphics.GraphicsDevice.PresentationParameters;
                w = pp.BackBufferWidth;
                h = pp.BackBufferHeight;
            }
            else
            {
                w = Window.ClientBounds.Width;
                h = Window.ClientBounds.Height;
            }
            if (w <= 0 || h <= 0)
                return;

            // Update the current window dimensions.
            WindowWidth = w;
            WindowHeight = h;

            // Force rescale to correct the size of render targets. 
            ScaleChanged = true;
            UpdateScale();

            // Force the render target resize as well.
            WindowWidthEnd = 0;
            WindowHeightEnd = 0;
            UpdateRenderTargets();
        }

        private void UpdateScale()
        {
            if (Camera.ClassicMode && GameSettings.ClassicScaling)
            {
                // Force integer scale or the field rect will be thrown off. The scaling value is calculated using the original dimensions of the
                // Game Boy version of Link's Awakening, minus the 16 pixels HUD ( 160x144 >> 160x128 ) so higher scaling values can be achieved.
                int gameScale = Math.Max(1, Math.Min(WindowWidth / 160, WindowHeight / 128));

                // Super Game Boy border is enabled. Calculate from the base resolution of the border instead.
                if (GameSettings.ClassicBorder == 2)
                    gameScale = Math.Max(1, Math.Min(WindowWidth / 256, WindowHeight / 224));

                // Send the game scale to the proper places it needs to go.
                MapManager.Camera.Scale = gameScale;
                GameManager.SetGameScale(gameScale);
            }
            else
            {
                // Get the maximum scale and add 1 for auto-scale.
                int maxScale = MaxGameScale + 1;

                // Calculate the game scale that is used for auto scaling.
                float gameScale = MathHelper.Clamp(Math.Min(WindowWidth / 160, WindowHeight / 128), 1, maxScale);
                float usedScale = gameScale;

                if (GameSettings.GameScale == maxScale)
                    usedScale = gameScale / 2;

                // If set to autoscale (Game1.MaxGameScale + 1) used the calculated value; otherwise use the value set by the user.
                MapManager.Camera.Scale = GameSettings.GameScale == maxScale
                    ? MathF.Ceiling(usedScale) 
                    : GameSettings.GameScale;

                // The camera scale uses a float value and can use a fractional scaling value when drawing the world.
                if (MapManager.Camera.Scale < 1)
                {
                    MapManager.Camera.Scale = 1 / (2 - MapManager.Camera.Scale);
                    GameManager.SetGameScale(1);
                }
                // If it's 1x or greater. We use "gameScale" directly here as a float as it allows fractional 
                // values while manually setting the scale only allows upscaling using integer values.
                else
                {
                    float newGameScale = GameSettings.GameScale == maxScale
                        ? MathF.Ceiling(usedScale)
                        : GameSettings.GameScale;
                    GameManager.SetGameScale(newGameScale);
                }
            }
            // Scale of the user interface.
            int interfaceScale = MathHelper.Clamp(Math.Min(WindowWidth / Values.MinWidth, WindowHeight / Values.MinHeight), 1, 11);

            if (GameSettings.UiScale > interfaceScale)
                UiScale = interfaceScale;
            else
                UiScale = GameSettings.UiScale == 0 
                    ? interfaceScale 
                    : MathHelper.Clamp(GameSettings.UiScale, 1, interfaceScale);

            // Call all of the "OnResize" methods to recalculate render targets.
            if (_finishedLoading)
                GameManager?.OnResize();
            UiManager?.OnResize();
            ScreenManager?.OnResize(WindowWidth, WindowHeight);
            UiPageManager?.OnResize(WindowWidth, WindowHeight);

            if (PlatformInput.HasTouchInput)
                VirtualController.Initialize(WindowWidth, WindowHeight, null);

            // When the scale changes, the camera will pan. Snap it on the first frame.
            Camera.SnapCameraOnce = true;

            // This needs to go false or it will run every loop.
            ScaleChanged = false;
        }

        private void UpdateRenderTargets()
        {
            if (WindowWidthEnd == WindowWidth && WindowHeightEnd == WindowHeight)
                return;

            WindowWidthEnd = WindowWidth;
            WindowHeightEnd = WindowHeight;

            UpdateRenderTargetSizes(WindowWidth, WindowHeight);

            ScreenManager.OnResizeEnd(WindowWidth, WindowHeight);

            if (_finishedLoading)
                GameManager?.OnResizeEnd();
        }

        private void UpdateRenderTargetSizes(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            if (_finishedLoading)
            {
                if (Resources.BlurEffect != null)
                {
                    Resources.BlurEffect.Parameters["width"]?.SetValue(width);
                    Resources.BlurEffect.Parameters["height"]?.SetValue(height);
                }
                if (Resources.RoundedCornerBlurEffect != null)
                {
                    Resources.RoundedCornerBlurEffect.Parameters["textureWidth"]?.SetValue(width);
                    Resources.RoundedCornerBlurEffect.Parameters["textureHeight"]?.SetValue(height);
                }
            }
            var blurScale = MathHelper.Clamp(MapManager.Camera.Scale / 2, 1, 10);
            var blurRtWidth = Math.Max(1, (int)(width / blurScale));
            var blurRtHeight = Math.Max(1, (int)(height / blurScale));

            RenderTarget2D newMain = null;
            RenderTarget2D newRt1 = null;
            RenderTarget2D newRt2 = null;
            RenderTarget2D newFin = null;

            try
            {
                newMain = new RenderTarget2D(Graphics.GraphicsDevice, width, height);
                newRt1 = new RenderTarget2D(Graphics.GraphicsDevice, blurRtWidth, blurRtHeight);
                newRt2 = new RenderTarget2D(Graphics.GraphicsDevice, blurRtWidth, blurRtHeight);
                newFin = new RenderTarget2D(Graphics.GraphicsDevice, width, height);
            }
            catch (Exception)
            {
                newMain?.Dispose();
                newRt1?.Dispose();
                newRt2?.Dispose();
                newFin?.Dispose();
                return;
            }
            MainRenderTarget?.Dispose();
            _renderTarget1?.Dispose();
            _renderTarget2?.Dispose();
            _finalRenderTarget?.Dispose();

            MainRenderTarget = newMain;
            _renderTarget1 = newRt1;
            _renderTarget2 = newRt2;
            _finalRenderTarget = newFin;
        }

        private void DisposeRenderTargets()
        {
            // Dispose main render target.
            MainRenderTarget?.Dispose();
            MainRenderTarget = null;

            // Dispose render target 1.
            _renderTarget1?.Dispose();
            _renderTarget1 = null;

            // Dispose render target 2.
            _renderTarget2?.Dispose();
            _renderTarget2 = null;

            _finalRenderTarget?.Dispose();
            _finalRenderTarget = null;
        }

        private void OnGameExiting(object sender, EventArgs e)
        {
            // Save the current window size before shutting down.
            try 
            { 
                SettingsSaveLoad.SaveSettings(); 
            } 
            catch { }

            // Stop the game loop so it doesn't do anything new.
            UpdateGame = false;
            _isExiting = true;

            // Shut down the GBS Player when closing.
            GbsPlayer.OnExit();

            // Try to prevent a crash with OpenGL disposing textures.
            try
            {
                // Dispose all render targets.
                DisposeRenderTargets();
                GameManager?.DisposeRenderTargets(true);

                // Destroy the sprite batch.
                SpriteBatch?.Dispose();
                SpriteBatch = null;

                // Unload all content.
                Content?.Unload();
            }
            catch {  }
        }
    }
}
