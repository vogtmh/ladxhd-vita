using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Screens
{
    public class IntroScreen : Screen
    {
        private Texture2D _sprOcean;
        private Texture2D _sprRain;
        private Texture2D _sprWaves;
        private Texture2D _sprCloud;

        public static Texture2D _sprIntro_eng, _sprIntro_chn, _sprIntro_deu, _sprIntro_esp, _sprIntro_fre, _sprIntro_ind, _sprIntro_ita, _sprIntro_por, _sprIntro_rus, _sprIntro_swe;

        public static Texture2D _sprIntro
        {
            get
            {
                return (Game1.LanguageManager.CurrentLanguageCode) switch
                {
                    // Note that "por" and "pte" are both "Portuguese" and share textures.
                    "chn" => _sprIntro_chn,
                    "deu" => _sprIntro_deu,
                    "esp" => _sprIntro_esp,
                    "fre" => _sprIntro_fre,
                    "ind" => _sprIntro_ind,
                    "ita" => _sprIntro_ita,
                    "por" => _sprIntro_por,
                    "pte" => _sprIntro_por,
                    "rus" => _sprIntro_rus,
                    "swe" => _sprIntro_swe,
                    _     => _sprIntro_eng
                };
            }
        }
        private Animator _loadingAnimator;
        private Animator _marinAnimator;
        private Animator _linkAnimator;
        private Animator _lightAnimation;
        private Animator _linkBoatAnimator;

        private Animator[] _thunder = new Animator[2];
        private Vector2[] _thunderPositions = new Vector2[2];
        private float[] _thunderCounts = { 1000, 2000 };

        private enum States { OceanCamera, OceanPicture, OceanThunder, StrandFading, StrandCamera, StrandMarin, StrandPanning, StrandLogo };

        private States _currentState;

        private int _scale = 1;
        private Vector2 _cameraCenter;

        private Matrix TransformMatrix =>
                                Matrix.CreateTranslation(new Vector3(
                                    -Snap(_cameraCenter.X),
                                    -Snap(_cameraCenter.Y), 0)) *
                                Matrix.CreateScale(_scale) *
                                Matrix.CreateTranslation(new Vector3((int)(Game1.WindowWidth * 0.5f), (int)(Game1.WindowHeight * 0.5f), 0)) * Game1.GetMatrix;

        private float _cameraState;
        private Vector2 _cameraStart;
        private Vector2 _cameraTarget;

        private enum MarinState { WalkSlow, Walk, Run, Stand, Hold, Push, End };

        private MarinState marinState;

        private MarinState[] _marinStates =
        {
            MarinState.WalkSlow, MarinState.Stand, MarinState.Run, MarinState.Stand, MarinState.Walk, MarinState.Stand, MarinState.Walk, MarinState.Stand, MarinState.Walk, MarinState.Stand,
            MarinState.Hold, MarinState.Push, MarinState.Hold, MarinState.Push, MarinState.Hold, MarinState.Push, MarinState.Hold, MarinState.Push, MarinState.End,
        };
        private int[] _marinTimes = {
            1000, 1100, 900, 1300, 200, // stand times
            2000, 500, 2100, 200, 400, 200, 250, 750 // push hold times
        };

        private Vector2[] _marinGoalPositions = { new Vector2(-150, 219), new Vector2(-94, 219), new Vector2(-72, 219), new Vector2(-64, 219), new Vector2(-18, 219) };

        private float _marinStateCounter;
        private int _marinIndex;
        private int _marinWalkIndex;
        private int _marinTimeIndex;

        private readonly Rectangle _oceanCloudRectangle = new Rectangle(0, 0, 32, 32);
        private int _thunderIndex;
        private float _thunderCount = 250;
        private float _thunderTransition;

        private readonly Rectangle _oceanRectangle  = new Rectangle(0, 144, 32, 32);
        private readonly Rectangle _ocean0Rectangle = new Rectangle(0, 288, 32, 16);
        private readonly Rectangle _ocean1Rectangle = new Rectangle(0, 306, 32, 16);
        private readonly Rectangle _ocean2Rectangle = new Rectangle(0, 324, 32, 16);

        private DictAtlasEntry _spriteOceanBoat;

        private Vector2 _oceanBoatPosition;
        private Vector2 _oceanPosition0;
        private Vector2 _oceanPosition1;
        private Vector2 _oceanPosition2;
        private Vector2 _oceanPosition3;

        private readonly int[] _thunderFrame = { 1, 0, 1, 2, 1, 0, 1, 2, 1, 0, 1, 2, 1, 0 };
        private Color _raftSkyColor;
        private Color[] _raftOceanColor;
        private Color _raftLowerColor;
        private Color _islandOcean;
        private Color _titleScreenSky;

        private DictAtlasEntry _spriteBackground;
        private DictAtlasEntry _spriteMountain;
        private DictAtlasEntry _spriteLogo0;
        private DictAtlasEntry _spriteLogo1;
        private DictAtlasEntry _spriteDX;
        private DictAtlasEntry _spriteBigN;

        private float _dxFadeInDelay;
        private float _dxFadeInAlpha;

        private readonly Rectangle _treesRectangle = new Rectangle(0, 320, 32, 46);
        private readonly Rectangle _sandRectangle  = new Rectangle(0, 364, 32, 16);
        private readonly Rectangle _waveRectangle  = new Rectangle(0, 0, 32, 24);

        private Vector2 _logoPosition;
        private float _logoState;
        private float _logoCounter;

        private Vector2 _lightPosition;
        private Vector2[] _lightPositions =
        {
            new Vector2(7, 0),
            new Vector2(41, 0),
            new Vector2(2, 19),
            new Vector2(0, 56),
            new Vector2(34, 9),
            new Vector2(55, 9),
            new Vector2(72, 9),
            new Vector2(96, 9),
            new Vector2(34, 47),
            new Vector2(55, 47),
            new Vector2(72, 47),
            new Vector2(90, 47),
            new Vector2(119, 47),
        };
        private int _lightIndex;

        private Vector2 _treePosition;
        private Vector2 _mountainRightPosition;
        private Vector2 _mountainLeftPosition;
        private Vector2 _cloundLeftPosition;
        private Vector2 _wavePosition;
        private Vector2 _linkPosition;
        private Vector2 _marinPosition;
        private Vector2 _marinGoal;

        private int _currentFrame;
        private float _waveCounter;

        private int _oceanFrameIndex;

        private bool _oceanShorePlay;
        private float _oceanShoreLimit;
        private float _oceanShoreCounter = 3600;

        private int _screenWidth = 160;
        private int _screenHeight = 144;

        private float StrandFadeTime = 600;
        private float _strandFadeCount;
        private float _loadingTransparency = 0.75f;

        private const int Slow = 500;
        private const int Fast = 200;
        private readonly int[] _waveTimes = { Slow, Fast, Fast, Fast, Slow, Fast, Fast, Fast, Fast, Fast };

        private bool _loaded;

        // Values configurable via lahdmod.
        private int intro_sky_red              = 24;
        private int intro_sky_grn              = 56;
        private int intro_sky_blu              = 40;
        private int intro_ocean_red            = 0;
        private int intro_ocean_grn            = 32;
        private int intro_ocean_blu            = 168;
        private int intro_ocean_lightning1_red = 32;
        private int intro_ocean_lightning1_grn = 32;
        private int intro_ocean_lightning1_blu = 168;
        private int intro_ocean_lightning2_red = 96;
        private int intro_ocean_lightning2_grn = 96;
        private int intro_ocean_lightning2_blu = 232;
        private int intro_ocean_bottom_red     = 16;
        private int intro_ocean_bottom_grn     = 0;
        private int intro_ocean_bottom_blu     = 104;
        private int intro_ocean_island_red     = 104;
        private int intro_ocean_island_grn     = 96;
        private int intro_ocean_island_blu     = 248;
        private int title_screen_sky_red       = 248;
        private int title_screen_sky_grn       = 248;
        private int title_screen_sky_blu       = 248;

        public static bool PixelGridRequested;
        public static float PixelGridScale;
        public static Vector2 PixelGridOffset;

        // Whole game pixel when Pixel Snapping is on, otherwise the existing render-pixel rounding.
        private float Snap(float value) => GameSettings.PixelSnapping
            ? MathF.Round(value)
            : (float)(Math.Round(value * _scale) / _scale);

        // Only rounds when Pixel Snapping is on; otherwise the value is passed through unchanged.
        private float SnapFree(float value) => GameSettings.PixelSnapping ? MathF.Round(value) : value;

        private Vector2 SnapFree(Vector2 value) => GameSettings.PixelSnapping
            ? new Vector2(MathF.Round(value.X), MathF.Round(value.Y))
            : value;

        // Calculates how far a parallax layer should be shifted for the current camera position.
        private float ParallaxOffset(float cameraX, float factor, bool roundSubPixel = false)
        {
            if (!GameSettings.PixelSnapping)
                return roundSubPixel
                    ? (float)(Math.Round(cameraX * factor * _scale) / _scale)
                    : cameraX * factor;

            var snapped = MathF.Round(cameraX);
            return snapped - MathF.Round(snapped * (1f - factor));
        }

        // Parallax expressed as an exact ratio of the camera's movement, so the layer steps on a
        // repeating cadence instead of running smooth for a stretch and then hitching once.
        private float ParallaxRatio(float cameraX, int numerator, int denominator)
        {
            if (!GameSettings.PixelSnapping)
                return cameraX * (1f - numerator / (float)denominator);

            var snapped = (int)MathF.Round(cameraX);
            return snapped - FloorDiv(snapped * numerator, denominator);
        }

        // Floor division, so the cadence stays symmetric when the camera goes negative.
        private static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);

        public IntroScreen(string screenId) : base(screenId) 
        { 
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "IntroScreen.lahdmod");
            ModFile.Parse(modFile, this);

            // Set up the color of the sky during the raft scene.
            _raftSkyColor = new Color(intro_sky_red, intro_sky_grn, intro_sky_blu);

            // Set up the color of the ocean during the raft scene. Includes 2 lightning strike variations.
            Color ocean_01 = new Color(intro_ocean_red, intro_ocean_grn, intro_ocean_blu);
            Color ocean_02 = new Color(intro_ocean_lightning1_red, intro_ocean_lightning1_grn, intro_ocean_lightning1_blu);
            Color ocean_03 = new Color(intro_ocean_lightning2_red, intro_ocean_lightning2_grn, intro_ocean_lightning2_blu);
            _raftOceanColor = new Color[] { ocean_01, ocean_02, ocean_03 };

            // Set up the color of the lower part of the ocean during the raft scene.
            _raftLowerColor = new Color(intro_ocean_bottom_red, intro_ocean_bottom_grn, intro_ocean_bottom_blu);

            // Set up the color of the ocean during the scene Marin is walking towards Link.
            _islandOcean = new Color(intro_ocean_island_red, intro_ocean_island_grn, intro_ocean_island_blu);

            // Set up the color of the sky on the title screen.
            _titleScreenSky = new Color(title_screen_sky_red, title_screen_sky_grn, title_screen_sky_blu);
        }

        public override void Load(ContentManager content)
        {
            if (_loaded)
                return;
            _loaded = true;

            _spriteOceanBoat = Resources.GetSprite("intro_boat");
            _spriteBackground = Resources.GetSprite("intro_background");
            _spriteMountain = Resources.GetSprite("intro_mountain");
            _spriteLogo0 = Resources.GetSprite("intro_logo_0");
            _spriteLogo1 = Resources.GetSprite("intro_logo_1");
            _spriteDX = Resources.GetSprite("intro_dx");
            _spriteBigN = Resources.GetSprite("intro_nintendo");

            _mountainLeftPosition.X = -_spriteBackground.SourceRectangle.Width / 2 - _spriteMountain.SourceRectangle.Width;
            _mountainLeftPosition.Y = 0;

            _mountainRightPosition.X = _spriteBackground.SourceRectangle.Width / 2;
            _mountainRightPosition.Y = 0;

            _treePosition.X = 0;
            _treePosition.Y = _spriteBackground.SourceRectangle.Height - 16;

            _wavePosition.X = _treePosition.X;
            _wavePosition.Y = _treePosition.Y + _treesRectangle.Height + 16;

            _logoPosition.X = -_spriteBackground.SourceRectangle.Width / 2 + 16;
            _logoPosition.Y = 3;

            _sprOcean = Resources.GetTexture("ocean.png");
            _sprRain = Resources.GetTexture("rain.png");

            _sprIntro_eng = Resources.GetTexture("intro.png");
            _sprIntro_chn = Resources.GetTexture("intro_chn.png");
            _sprIntro_deu = Resources.GetTexture("intro_deu.png");
            _sprIntro_esp = Resources.GetTexture("intro_esp.png");
            _sprIntro_fre = Resources.GetTexture("intro_fre.png");
            _sprIntro_ind = Resources.GetTexture("intro_ind.png");
            _sprIntro_ita = Resources.GetTexture("intro_ita.png");
            _sprIntro_por = Resources.GetTexture("intro_por.png");
            _sprIntro_rus = Resources.GetTexture("intro_rus.png");
            _sprIntro_swe = Resources.GetTexture("intro_swe.png");

            _sprCloud = Resources.GetTexture("cloud.png");
            _sprWaves = Resources.GetTexture("waves.png");

            _loadingAnimator = AnimatorSaveLoad.LoadAnimator("Intro/loading");
            _loadingAnimator.Play("idle");

            _thunder[0] = AnimatorSaveLoad.LoadAnimator("Intro/thunder");
            _thunder[1] = AnimatorSaveLoad.LoadAnimator("Intro/thunder");

            _linkBoatAnimator = AnimatorSaveLoad.LoadAnimator("Intro/link_boat");
            _marinAnimator = AnimatorSaveLoad.LoadAnimator("Intro/maria");
            _linkAnimator = AnimatorSaveLoad.LoadAnimator("Intro/link");
            _lightAnimation = AnimatorSaveLoad.LoadAnimator("Intro/light");
        }

        public void RefreshIntroResources()
        {
            // Append language extension for some logo textures.
            string texExtension = "";

            // If language is Chinese we use alternate logo textures.
            if (Game1.LanguageManager.CurrentLanguageCode == "chn")
                texExtension = "_chn";

            // Refresh the logo and DX images. This is done when resources are finished
            // loading in and also when the language toggle is changed. 
            _spriteLogo0 = Resources.GetSprite("intro_logo_0" + texExtension);
            _spriteLogo1 = Resources.GetSprite("intro_logo_1" + texExtension);
            _spriteDX = Resources.GetSprite("intro_dx" + texExtension);
        }

        public override void OnLoad()
        {
            Init();
        }

        private void Init()
        {
            _linkAnimator.Play("idle");
            _marinAnimator.Play("walk");
            _lightAnimation.Stop();

            _logoState = 0;
            _dxFadeInDelay = 0;
            _dxFadeInAlpha = 0;

            _currentState = States.OceanCamera;
            _cameraCenter = new Vector2(-220, 55);

            // start playing the prologue music
            Game1.AudioManager.ResetMusic();
            Game1.AudioManager.StopMusic();
            Game1.AudioManager.SetMusic(25, 0);

            Game1.AudioManager.SetMusicVolumeMultiplier(1.0f);
            Game1.AudioManager.PlayMusic();

            // play track for 52sec
            Game1.AudioManager.SetMusicStopTime(51.25f);
        }

        public override void Update(GameTime gameTime)
        {
            if (Game1.FinishedLoading && Game1.AutoLoadSave)
            {
                // Make sure the slot is within range.
                int LoadSlot = Game1.AutoLoadSlot is >= 0 and <= 3 ? Game1.AutoLoadSlot : 0;

                // Set the autoload to false.
                Game1.AutoLoadSave = false;

                if (SaveManager.FileExists(Path.Combine(Values.PathSaveFolder, SaveGameSaveLoad.SaveFileName + LoadSlot.ToString())))
                {
                    // Change to the game screen.
                    Game1.ScreenManager.ChangeScreen(Values.ScreenNameGame);

                    // Load the save file slot.
                    Game1.GameManager.LoadSaveFile(LoadSlot);
                }
            }

            if (Game1.AudioManager.GetMusicStopTimeExpired())
            {
                Game1.AudioManager.SetMusic(0, 0);
                Game1.AudioManager.PlayMusic();
            }

            if (Game1.FinishedLoading && (ControlHandler.ButtonPressed(ControlHandler.ConfirmButton) || ControlHandler.ButtonPressed(CButtons.Start)))
            {
                if (_currentState == States.StrandLogo)
                {
                    Game1.ScreenManager.ChangeScreen(Values.ScreenNameMenu);
                } 
                else
                {
                    Game1.AudioManager.SetMusic(12, 0);

                    _strandFadeCount = 0;
                    _cameraState = 0;
                    _cameraCenter = new Vector2(0, _logoPosition.Y + _spriteLogo0.ScaledRectangle.Height + 5);
                    _cameraStart = _cameraCenter;
                    _cameraTarget = new Vector2(_cameraCenter.X, _logoPosition.Y + _spriteLogo0.ScaledRectangle.Height + 5);
                    _logoCounter = 1500;
                    _currentState = States.StrandPanning;
                    _linkPosition = new Vector2(-1000,-1000);
                    _marinPosition = new Vector2(-1000,-1000);
                    _oceanShorePlay = false;
                    _oceanShoreLimit = 0;
                    _oceanShoreCounter = 3600;
                }
            }
            if (!Game1.FinishedLoading)
                _loadingAnimator.Update();

            if (Game1.FinishedLoading)
            {
                _loadingTransparency = AnimationHelper.MoveToTarget(_loadingTransparency, 0, 0.125f * Game1.TimeMultiplier);
            }
            UpdateOcean();
            UpdateBeach();
            _scale = MathHelper.Clamp(Math.Min(Game1.WindowWidth / _screenWidth, Game1.WindowHeight / _screenHeight), 1, 10);
        }

        private void UpdateOcean()
        {
            if (_currentState == States.OceanCamera)
            {
                // move the camera to the center
                var goalPosition = new Vector2(0, 55);
                var direction = goalPosition - _cameraCenter;
                if (direction.Length() < Game1.TimeMultiplier * 0.25f)
                {
                    _currentState = States.OceanPicture;

                    _linkBoatAnimator.Stop();
                    _linkBoatAnimator.Play("run");
                }
                else
                {
                    direction.Normalize();
                    _cameraCenter += direction * Game1.TimeMultiplier * 0.25f;
                }

                _thunderIndex = 0;
                for (var i = 0; i < _thunder.Length; i++)
                {
                    _thunder[i].Update();
                    if (_thunder[i].IsPlaying)
                        UpdateThunderIndex(_thunder[i].FrameCounter);

                    _thunderCounts[i] -= Game1.DeltaTime;
                    if (_thunderCounts[i] <= 0)
                    {
                        var animation = Game1.RandomNumber.Next(0, 4);
                        _thunder[i].Play("thunder" + animation);
                        _thunderCounts[i] = Game1.RandomNumber.Next(2000, 4000);

                        // play sound effect
                        Game1.AudioManager.PlaySoundEffect("D378-12-0C", true);

                        var randomX = (int)_cameraCenter.X - 150 + Game1.RandomNumber.Next(0, 300);
                        if (animation < 2)
                            _thunderPositions[i] = new Vector2(randomX, 28);
                        else
                            _thunderPositions[i] = new Vector2(randomX, 14);
                    }
                }
                _oceanPosition0 = new Vector2(ParallaxOffset(_cameraCenter.X, 0.1f), 60 + (int)Math.Round(Math.Sin(Game1.TotalTime / 500) * 1));
                _oceanPosition1 = new Vector2(ParallaxOffset(_cameraCenter.X, 0.05f), 60 + (int)Math.Round(Math.Sin(Game1.TotalTime / 500 + 0.1) * 2));
                _oceanPosition2 = new Vector2(ParallaxOffset(_cameraCenter.X, -0.05f), 60 + (int)Math.Round(Math.Sin(Game1.TotalTime / 500 + 0.2) * 3));
                _oceanPosition3 = new Vector2(ParallaxOffset(_cameraCenter.X, -0.25f), 60 + (int)Math.Round(Math.Sin(Game1.TotalTime / 500 + 0.2) * 3));
                _oceanBoatPosition = new Vector2(-16 + _cameraCenter.X * 0.05f, 47 + (int)Math.Round(Math.Sin(Game1.TotalTime / 500) * 2.5));
            }
            else if (_currentState == States.OceanPicture)
            {
                _cameraCenter = new Vector2(0, 0);
                _linkBoatAnimator.Update();

                if (_oceanFrameIndex != _linkBoatAnimator.CurrentFrameIndex &&
                    (_linkBoatAnimator.CurrentFrameIndex == 1 || _linkBoatAnimator.CurrentFrameIndex == 10))
                {
                    Game1.AudioManager.PlaySoundEffect("D378-12-0C", true);
                }
                _oceanFrameIndex = _linkBoatAnimator.CurrentFrameIndex;

                // transition to next state
                if (!_linkBoatAnimator.IsPlaying)
                {
                    _currentState = States.OceanThunder;
                    _thunderTransition = 0;
                    _thunderCount = 250;
                    _thunder[0].Play("null");
                }
            }
            else if (_currentState == States.OceanThunder)
            {
                if (_thunderCount < 0)
                {
                    _thunder[0].Play("thunderboat");

                    var counter = _thunder[0].FrameCounter;
                    UpdateThunderIndex(counter);

                    if (counter > 500)
                    {
                        _thunderTransition = ((float)counter - 500) / 700f;
                        if (_thunderTransition >= 1)
                        {
                            InitBeach();
                            return;
                        }
                    }
                }
                else
                    _thunderCount -= Game1.DeltaTime;

                _thunder[0].Update();

                // center the camera
                _cameraCenter = new Vector2(0, 55);
                _oceanPosition0 = new Vector2(_cameraCenter.X * 0.1f, 60);
                _oceanPosition1 = new Vector2(_cameraCenter.X * 0.05f, 60);
                _oceanPosition2 = new Vector2(-_cameraCenter.X * 0.05f, 60);
                _oceanPosition3 = new Vector2(-_cameraCenter.X * 0.25f, 60);
                _oceanBoatPosition = new Vector2(-16 + _cameraCenter.X * 0.05f, 47);
                _thunderPositions[0] = new Vector2(-2, -17);
            }
        }

        private void UpdateThunderIndex(double time)
        {
            var index = (int)(time / (2000 / 60.0));
            if (index < _thunderFrame.Length)
                _thunderIndex = _thunderFrame[index];
        }

        private void InitBeach()
        {
            _currentState = States.StrandFading;
            _cameraCenter = new Vector2(-400, 210);
            _linkPosition = new Vector2(-8, 225);
            _marinPosition = new Vector2(-250, 219);
            _marinAnimator.Play("stand");
            _marinAnimator.SpeedMultiplier = 1.0f;
            _logoState = 0;
            _strandFadeCount = 3700 + StrandFadeTime;
            _marinIndex = 0;
            _marinWalkIndex = 0;
            _marinTimeIndex = 0;

            NextState();
        }

        private void UpdateBeach()
        {
            if (_currentState != States.StrandFading && _currentState != States.StrandCamera &&
                _currentState != States.StrandMarin && _currentState != States.StrandPanning &&
                _currentState != States.StrandLogo)
                return;

            if (_currentState == States.StrandFading)
            {
                if (!_oceanShorePlay)
                    _oceanShorePlay = true;

                _strandFadeCount -= Game1.DeltaTime;

                if (_strandFadeCount < StrandFadeTime)
                {
                    UpdateMarin();
                }

                if (_strandFadeCount <= 0)
                {
                    _strandFadeCount = 0;
                    _currentState = States.StrandCamera;
                }
            }
            // move camera to marin
            else if (_currentState == States.StrandCamera)
            {
                UpdateMarin();

                // reached marin?
                if (_cameraCenter.X >= _marinPosition.X + 18)
                {
                    _currentState = States.StrandMarin;
                }
            }
            // camera follows marin directly
            else if (_currentState == States.StrandMarin)
            {
                UpdateMarin();

                if (_marinIndex == _marinStates.Length - 1)
                {
                    _cameraState = 0;
                    _cameraStart = _cameraCenter;
                    _cameraTarget = new Vector2(_cameraCenter.X, _logoPosition.Y + _spriteLogo0.ScaledRectangle.Height + 5);
                    _logoCounter = 0;
                    _currentState = States.StrandPanning;
                }
            }
            else if (_currentState == States.StrandPanning)
            {
                _oceanShorePlay = false;

                if (!MoveCamera(0.0014f))
                {
                    _logoCounter += Game1.DeltaTime;

                    if (_logoCounter > 750 && _logoState != 1)
                    {
                        _logoState = AnimationHelper.MoveToTarget(_logoState, 1, 0.05f * Game1.TimeMultiplier);

                        if (_logoState == 1)
                        {
                            Game1.AudioManager.PlaySoundEffect("D378-25-19");
                            _currentState = States.StrandLogo;
                        }
                    }
                }
            }
            else if (_currentState == States.StrandLogo)
            {
                if (!_lightAnimation.IsPlaying)
                {
                    _lightAnimation.Play("idle");
                    _lightIndex = (_lightIndex + Game1.RandomNumber.Next(1, _lightPositions.Length)) % _lightPositions.Length;
                    _lightPosition = _lightPositions[_lightIndex];
                }
            }

            if (_oceanShorePlay)
            {
                _oceanShoreCounter -= Game1.DeltaTime;
                if (_oceanShoreCounter <= 0)
                {
                    _oceanShoreCounter += 2650;
                    if (_oceanShoreLimit <= 9)
                    {
                        Game1.AudioManager.PlaySoundEffect("D378-15-0F");
                        _oceanShoreLimit++;
                    }
                }
            }

            if (_currentState != States.StrandPanning && _currentState != States.StrandLogo && _strandFadeCount < StrandFadeTime)
            {
                UpdateCamera(new Vector2(_marinPosition.X + 18, 210), _currentState == States.StrandMarin ? 1.0f : 0.54f);
            }
            _marinAnimator.Update();
            _linkAnimator.Update();
            _lightAnimation.Update();
            _cloundLeftPosition = new Vector2(-HdTexture.LogicalWidth(_sprCloud), 47);

            // update wave animation
            _waveCounter += Game1.DeltaTime;
            if (_waveCounter > _waveTimes[_currentFrame])
            {
                _waveCounter -= _waveTimes[_currentFrame];
                _currentFrame = (_currentFrame + 1) % 10;
            }
        }

        private void UpdateCamera(Vector2 tragetPosition, float maxSpeed)
        {
            var direction = tragetPosition - _cameraCenter;
            if (direction != Vector2.Zero)
            {
                var distance = direction.Length();

                if (distance <= 0.1f * Game1.TimeMultiplier)
                    _cameraCenter = tragetPosition;
                else
                {
                    var speedMult = Math.Clamp(CameraFunction(distance / 12.5f), 0, maxSpeed);

                    direction.Normalize();
                    var cameraSpeed = direction * speedMult * Game1.TimeMultiplier;

                    _cameraCenter += cameraSpeed;
                }
            }
        }

        private float CameraFunction(float x)
        {
            var y = MathF.Atan(x);

            if (x > 2)
                y += (x - 2) / 2;

            return y + 0.1f;
        }

        private bool MoveCamera(float speed)
        {
            if (_cameraCenter == _cameraTarget)
                return false;

            _cameraState += speed * Game1.TimeMultiplier;
            _cameraState = Math.Clamp(_cameraState, 0, 1);
            _cameraCenter = Vector2.Lerp(_cameraStart, _cameraTarget, 0.5f + MathF.Sin(-MathF.PI * 0.5f + _cameraState * MathF.PI) * 0.5f);

            return true;
        }

        private void UpdateMarin()
        {
            if (marinState == MarinState.Stand)
            {
                _marinAnimator.Play("stand");
                _marinStateCounter -= Game1.DeltaTime;
                if (_marinStateCounter <= 0)
                    NextState();
            }
            else if (marinState == MarinState.Walk || marinState == MarinState.WalkSlow)
            {
                _marinAnimator.Play("move");

                if (_marinPosition.X < _marinGoal.X)
                {
                    _marinPosition.X += Game1.TimeMultiplier * (marinState == MarinState.Walk ? 0.45f : 0.20f);
                }
                else
                {
                    _marinPosition = _marinGoal;
                    NextState();
                }
            }
            else if (marinState == MarinState.Run)
            {
                _marinAnimator.SpeedMultiplier = 2.0f;
                _marinAnimator.Play("move");

                if (_marinPosition.X < _marinGoal.X)
                {
                    _marinPosition.X += Game1.TimeMultiplier * 1.0f;
                }
                else
                {
                    _marinPosition = _marinGoal;
                    _marinAnimator.SpeedMultiplier = 1.0f;
                    NextState();
                }
            }
            else if (marinState == MarinState.Hold)
            {
                _marinAnimator.Play("hold");
                _linkAnimator.Play("idle");

                _marinStateCounter -= Game1.DeltaTime;
                if (_marinStateCounter <= 0)
                    NextState();
            }
            else if (marinState == MarinState.Push)
            {
                _marinAnimator.Play("push");
                _linkAnimator.Play("pushed");

                _marinStateCounter -= Game1.DeltaTime;
                if (_marinStateCounter <= 0)
                    NextState();
            }
        }

        private void NextState()
        {
            marinState = _marinStates[_marinIndex];
            _marinIndex++;

            if (marinState == MarinState.Stand || marinState == MarinState.Hold || marinState == MarinState.Push)
            {
                _marinStateCounter = _marinTimes[_marinTimeIndex];
                _marinTimeIndex++;
            }
            else if (marinState == MarinState.Walk || marinState == MarinState.WalkSlow || marinState == MarinState.Run)
            {
                _marinGoal = _marinGoalPositions[_marinWalkIndex];
                _marinWalkIndex++;
            }
        }
        private void UpdatePixelGrid()
        {
            // The grid can only line up when everything moves in whole pixel increments. At a scale of 1
            // a game pixel is a screen pixel, so a 1px grid line would cover the entire screen.
            if (!GameSettings.PixelSnapping || _scale < 2)
                return;

            // The translation the intro's TransformMatrix applies, in screen pixels.
            var tx = (int)(Game1.WindowWidth * 0.5f) - Snap(_cameraCenter.X) * _scale;
            var ty = (int)(Game1.WindowHeight * 0.5f) - Snap(_cameraCenter.Y) * _scale;

            // How far the top left of the screen sits from a whole game pixel boundary.
            PixelGridScale = _scale;
            PixelGridOffset = new Vector2(((-tx) % _scale) / _scale, ((-ty) % _scale) / _scale);
            PixelGridRequested = true;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            UpdatePixelGrid();

            Game1.Graphics.GraphicsDevice.Clear(_currentState == States.OceanPicture ? Color.Black : _islandOcean);
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, null, null, TransformMatrix);

            DrawOcean(spriteBatch);
            DrawBeach(spriteBatch);

            spriteBatch.End();

            // Draw the loading animation.
            if (_loadingTransparency > 0)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, null, null, Game1.GetMatrix);
                _loadingAnimator.DrawBasic(spriteBatch, new Vector2(
                    Game1.WindowWidth - 2 * _scale, Game1.WindowHeight - 2 * _scale), Color.White * _loadingTransparency, _scale);
                spriteBatch.End();
            }
        }

        private static void DrawRain(SpriteBatch spriteBatch, Texture2D texture, Rectangle destBounds, Vector2 offset)
        {
            int texW = HdTexture.LogicalWidth(texture);
            int texH = HdTexture.LogicalHeight(texture);
            var sourceRectangle = new Rectangle(0, 0, texW, texH);

            float offX = ((offset.X % texW) + texW) % texW;
            float offY = ((offset.Y % texH) + texH) % texH;

            for (float y = destBounds.Top - offY; y < destBounds.Bottom; y += texH)
                for (float x = destBounds.Left - offX; x < destBounds.Right; x += texW)
                    spriteBatch.Draw(texture, new Vector2(x, y), sourceRectangle, Color.White);
        }

        private void DrawOcean(SpriteBatch spriteBatch)
        {
            if (_currentState != States.OceanCamera && _currentState != States.OceanPicture && _currentState != States.OceanThunder)
                return;

            var camX = Snap(_cameraCenter.X);
            var camY = Snap(_cameraCenter.Y);

            var screenLeft   = (int)Math.Floor(camX) - (int)Math.Ceiling(Game1.WindowWidth / (double)_scale / 2.0);
            var screenRight  = (int)Math.Ceiling(camX) + (int)Math.Ceiling(Game1.WindowWidth / (double)_scale / 2.0);
            var screenTop    = (int)Math.Floor(camY) - (int)Math.Ceiling(Game1.WindowHeight / (double)_scale / 2.0);
            var screenBottom = (int)Math.Ceiling(camY) + (int)Math.Ceiling(Game1.WindowHeight / (double)_scale / 2.0);

            var width = screenRight - screenLeft + 2;
            var height = screenBottom - screenTop + 2;
            var cloudOffset = ParallaxOffset(_cameraCenter.X, 0.05f);

            if (_currentState == States.OceanCamera || _currentState == States.OceanThunder)
            {
                // draw the dark cloud
                spriteBatch.Draw(Resources.SprWhite, new Rectangle(screenLeft, screenTop, width, -screenTop), _raftSkyColor);

                //---------------------------------------------------
                // Clouds - Destination Rectangle
                var cloudDestRectX = screenLeft;
                var cloudDestRectY = 0;
                var cloudDestRectW = width;
                var cloudDestRectH = _oceanCloudRectangle.Height;
                var cloudDestRectangle = new Rectangle(cloudDestRectX, cloudDestRectY, cloudDestRectW, cloudDestRectH);

                // Clouds - Source Rectangle
                var cloudSourceRectX = _oceanCloudRectangle.X + screenLeft - (int)cloudOffset;
                var cloudSourceRectY = _oceanCloudRectangle.Y + _thunderIndex * (_oceanCloudRectangle.Height + 16);
                var cloudSourceRectW = width;
                var cloudSourceRectH = _oceanCloudRectangle.Height;
                var cloudSourceRectangle = new Rectangle(cloudSourceRectX, cloudSourceRectY, cloudSourceRectW, cloudSourceRectH);

                // Draw the Clouds.
                spriteBatch.Draw(_sprOcean, cloudDestRectangle, cloudSourceRectangle, Color.White, 0, new Vector2(1 - cloudOffset % 1, 0), SpriteEffects.None, 0);

                //---------------------------------------------------
                // Dark Sky - Source Rectangle
                var darkSkyRectX = screenLeft;
                var darkSkyRectY = _oceanCloudRectangle.Height;
                var darkSkyRectW = width;
                var darkSkyRectH = 64;
                var darkSkyRectangle = new Rectangle(darkSkyRectX, darkSkyRectY, darkSkyRectW, darkSkyRectH);

                // Draw the Dark Sky.
                spriteBatch.Draw(Resources.SprWhite, darkSkyRectangle, _raftOceanColor[_thunderIndex]);

                // The offset coordinates of "ocean.png". 
                var oceanAnimationOffset = _thunderIndex * 70;

                //---------------------------------------------------
                // Ocean Top - Destination Rectangle
                var ocean0DestRectX = screenLeft;
                var ocean0DestRectY = (int)_oceanPosition0.Y;
                var ocean0DestRectW = width;
                var ocean0DestRectH = _ocean0Rectangle.Height;
                var ocean0DestRectangle = new Rectangle(ocean0DestRectX, ocean0DestRectY, ocean0DestRectW, ocean0DestRectH);

                // Ocean Top - Source Rectangle
                var ocean0SourceRectX = _ocean0Rectangle.X + screenLeft - (int)_oceanPosition0.X;
                var ocean0SourceRectY = _ocean0Rectangle.Y + oceanAnimationOffset;
                var ocean0SourceRectW = width;
                var ocean0SourceRectH = _ocean0Rectangle.Height;
                var ocean0SourceRectangle = new Rectangle(ocean0SourceRectX, ocean0SourceRectY, ocean0SourceRectW, ocean0SourceRectH);

                // Draw the Ocean Top
                spriteBatch.Draw(_sprOcean, ocean0DestRectangle, ocean0SourceRectangle, Color.White, 0, new Vector2(1 - _oceanPosition0.X % 1, 0), SpriteEffects.None, 0);

                //---------------------------------------------------
                // Ocean Top-Middle - Destination Rectangle
                var ocean1DestRectX = screenLeft;
                var ocean1DestRectY = (int)_oceanPosition1.Y;
                var ocean1DestRectW = width;
                var ocean1DestRectH = _ocean1Rectangle.Height;
                var ocean1DestRectangle = new Rectangle(ocean1DestRectX, ocean1DestRectY, ocean1DestRectW, ocean1DestRectH);

                // Ocean Top-Middle - Source Rectangle
                var ocean1SourceRectX = _ocean1Rectangle.X + screenLeft - (int)_oceanPosition1.X;
                var ocean1SourceRectY = _ocean1Rectangle.Y + oceanAnimationOffset;
                var ocean1SourceRectW = width;
                var ocean1SourceRectH = _ocean1Rectangle.Height;
                var ocean1SourceRectangle = new Rectangle(ocean1SourceRectX, ocean1SourceRectY, ocean1SourceRectW, ocean1SourceRectH);

                // Draw the Ocean Top-Middle.
                spriteBatch.Draw(_sprOcean, ocean1DestRectangle, ocean1SourceRectangle, Color.White, 0, new Vector2(1 - _oceanPosition1.X % 1, 0), SpriteEffects.None, 0);

                //---------------------------------------------------
                // Draw Link and the raft.
                DrawHelper.DrawNormalized(spriteBatch, _spriteOceanBoat, _oceanBoatPosition, Color.White);

                //---------------------------------------------------
                // Ocean Bottom-Middle - Destination Rectangle
                var ocean2DestRectX = screenLeft;
                var ocean2DestRectY = (int)_oceanPosition2.Y;
                var ocean2DestRectW = width;
                var ocean2DestRectH = _ocean2Rectangle.Height;
                var ocean2DestRectangle = new Rectangle(ocean2DestRectX, ocean2DestRectY, ocean2DestRectW, ocean2DestRectH);

                // Ocean Bottom-Middle - Source Rectangle
                var ocean2SourceRectX = _ocean2Rectangle.X + screenLeft - (int)_oceanPosition2.X;
                var ocean2SourceRectY = _ocean2Rectangle.Y + oceanAnimationOffset;
                var ocean2SourceRectW = width;
                var ocean2SourceRectH = _ocean2Rectangle.Height;
                var ocean2SourceRectangle = new Rectangle(ocean2SourceRectX, ocean2SourceRectY, ocean2SourceRectW, ocean2SourceRectH);

                // Draw the Ocean Bottom-Middle
                spriteBatch.Draw(_sprOcean, ocean2DestRectangle, ocean2SourceRectangle, Color.White, 0, new Vector2(1 - _oceanPosition2.X % 1, 0), SpriteEffects.None, 0);

                //---------------------------------------------------
                // Ocean Bottom-Middle - Destination Rectangle
                var ocean3DestRectX = screenLeft;
                var ocean3DestRectY = (int)_oceanPosition3.Y + 16;
                var ocean3DestRectW = width;
                var ocean3DestRectH = _oceanRectangle.Height;
                var ocean3DestRectangle = new Rectangle(ocean3DestRectX, ocean3DestRectY, ocean3DestRectW, ocean3DestRectH);

                // Ocean Bottom-Middle - Source Rectangle
                var ocean3SourceRectX = _oceanRectangle.X + screenLeft - (int)_oceanPosition3.X;
                var ocean3SourceRectY = _oceanRectangle.Y + _thunderIndex * (_oceanRectangle.Height + 16);
                var ocean3SourceRectW = width;
                var ocean3SourceRectH = _oceanRectangle.Height;
                var ocean3SourceRectangle = new Rectangle(ocean3SourceRectX, ocean3SourceRectY, ocean3SourceRectW, ocean3SourceRectH);

                // Draw the Ocean Bottom.
                spriteBatch.Draw(_sprOcean, ocean3DestRectangle, ocean3SourceRectangle, Color.White, 0, new Vector2(1 - _oceanPosition3.X % 1, 0), SpriteEffects.None, 0);

                //---------------------------------------------------
                // Draw the Dark Ocean.
                spriteBatch.Draw(Resources.SprWhite,
                    new Rectangle(screenLeft, (int)_oceanPosition3.Y + 48, width,
                        screenBottom - ((int)_oceanPosition3.Y + 48)), _raftLowerColor);
            }

            if (_currentState == States.OceanPicture)
            {
                _linkBoatAnimator.Draw(spriteBatch, Vector2.Zero, Color.White);
            }

            // Draw the rain texture.
            var destBounds = new Rectangle(screenLeft, screenTop, width, height);
            var rainOffset = new Vector2(
                SnapFree((float)(Game1.TotalTime / 2.5f + Math.Sin(Game1.TotalTime / 500) * 5)),
                SnapFree((float)Game1.TotalTime / 2.3f));
            DrawRain(spriteBatch, _sprRain, destBounds, -rainOffset);

            if (_currentState == States.OceanCamera)
            {
                // draw the thunder
                for (var i = 0; i < _thunder.Length; i++)
                    _thunder[i].DrawBasic(spriteBatch, _thunderPositions[i] + new Vector2(cloudOffset, 0), Color.White);
            }
            else if (_currentState == States.OceanThunder)
            {
                // draw the thunder on top of the boat
                _thunder[0].DrawBasic(spriteBatch, _thunderPositions[0] + new Vector2(cloudOffset, 0), Color.White);

                if (_thunderTransition > 0)
                {
                    var white = MathHelper.Clamp(_thunderTransition * 1.5f, 0, 1);
                    spriteBatch.Draw(Resources.SprWhite, new Rectangle(screenLeft, screenTop, width, height), Color.White * white);

                    // draw the boat
                    var boatWhite = MathHelper.Clamp(1.5f - _thunderTransition * 1.5f, 0, 1);
                    DrawHelper.DrawNormalized(spriteBatch, _spriteOceanBoat, _oceanBoatPosition, Color.White * boatWhite);
                }
            }
        }

        private void DrawBeach(SpriteBatch spriteBatch)
        {
            if (_currentState != States.StrandFading && _currentState != States.StrandCamera &&
                _currentState != States.StrandMarin && _currentState != States.StrandPanning && 
                _currentState != States.StrandLogo)
                return;

            var screenLeft = (int)Math.Floor(_cameraCenter.X) - (int)Math.Ceiling(Game1.WindowWidth / (double)_scale / 2.0);
            var screenRight = (int)Math.Ceiling(_cameraCenter.X) + (int)Math.Ceiling(Game1.WindowWidth / (double)_scale / 2.0);
            var screenTop = (int)Math.Floor(_cameraCenter.Y) - (int)Math.Ceiling(Game1.WindowHeight / (double)_scale / 2.0);
            var screenBottom = (int)Math.Ceiling(_cameraCenter.Y) + (int)Math.Ceiling(Game1.WindowHeight / (double)_scale / 2.0);
            var width = screenRight - screenLeft + 2;
            var height = screenBottom - screenTop + 1;

            // draw the sky white
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                screenLeft, screenTop, width, -screenTop + 47), _titleScreenSky);

            var mountainOffset = new Vector2(ParallaxOffset(_cameraCenter.X, 0.5f, true), 0);

            // draw the clouds on the left
            var cloudHeight = HdTexture.LogicalHeight(_sprCloud);
            var cloudLeft = -_spriteBackground.ScaledRectangle.Width / 2;
            if (screenLeft < cloudLeft + (int)mountainOffset.X)
                spriteBatch.Draw(_sprCloud,
                    new Rectangle(screenLeft, (int)_cloundLeftPosition.Y, (cloudLeft + (int)mountainOffset.X) - screenLeft, cloudHeight),
                    new Rectangle(screenLeft - (int)mountainOffset.X, 0, (cloudLeft + (int)mountainOffset.X) - screenLeft, cloudHeight),
                    Color.White, 0, new Vector2(-mountainOffset.X % 1, 0), SpriteEffects.None, 0);

            // draw the clouds on the right
            var cloudRight = _spriteBackground.ScaledRectangle.Width / 2;
            if (cloudRight + (int)mountainOffset.X < screenRight)
                spriteBatch.Draw(_sprCloud,
                    new Rectangle(cloudRight + (int)mountainOffset.X, (int)_cloundLeftPosition.Y, screenRight - (cloudRight + (int)mountainOffset.X), cloudHeight),
                    new Rectangle(0, 0, screenRight - (cloudRight + (int)mountainOffset.X), cloudHeight),
                    Color.White, 0, new Vector2(-mountainOffset.X % 1, 0), SpriteEffects.None, 0);

            // draw the top of the mountain
            spriteBatch.Draw(_sprIntro, new Vector2(-_spriteBackground.ScaledRectangle.Width / 2, 0) + mountainOffset, _spriteBackground.ScaledRectangle, Color.White);

            // draw the left side of the mountain
            spriteBatch.Draw(_sprIntro, _mountainLeftPosition + mountainOffset, _spriteMountain.SourceRectangle,
                Color.White, 0, Vector2.Zero, 1, SpriteEffects.FlipHorizontally, 0);

            // draw the right side of the mountain
            spriteBatch.Draw(_sprIntro, _mountainRightPosition + mountainOffset, _spriteMountain.SourceRectangle, Color.White);

            // draw the trees
            var treeOffset = GameSettings.PixelSnapping
                ? new Vector2(ParallaxRatio(_cameraCenter.X, 3, 4), 0)
                : new Vector2(ParallaxOffset(_cameraCenter.X, 0.075f), 0);

            spriteBatch.Draw(_sprWaves,
                new Rectangle(screenLeft, (int)_treePosition.Y, width, _treesRectangle.Height),
                new Rectangle(_treesRectangle.X + screenLeft - (int)treeOffset.X, _treesRectangle.Y, width, _treesRectangle.Height),
                Color.White, 0, new Vector2(1 - treeOffset.X % 1, 0), SpriteEffects.None, 0);

            // draw the strand
            var strandOffset = GameSettings.PixelSnapping
                ? Vector2.Zero
                : new Vector2(ParallaxOffset(_cameraCenter.X, -0.025f), 0);

            spriteBatch.Draw(_sprWaves,
                new Rectangle(screenLeft, (int)_treePosition.Y + _treesRectangle.Height, width, _sandRectangle.Height),
                new Rectangle(_sandRectangle.X + screenLeft - (int)strandOffset.X, _sandRectangle.Y, width, _sandRectangle.Height),
                Color.White, 0, new Vector2(1 - strandOffset.X % 1, 0), SpriteEffects.None, 0);

            // draw the waves
            spriteBatch.Draw(_sprWaves,
                new Rectangle(screenLeft, (int)_wavePosition.Y, width, _waveRectangle.Height),
                new Rectangle(_waveRectangle.X + screenLeft - (int)strandOffset.X, _waveRectangle.Y + _currentFrame * 32, width, _waveRectangle.Height),
                Color.White, 0, new Vector2(1 - strandOffset.X % 1, 0), SpriteEffects.None, 0);

            // draw link and marin
            if (_logoState != 1)
            {
                _linkAnimator.DrawBasic(spriteBatch, SnapFree(_linkPosition), Color.White);
                _marinAnimator.DrawBasic(spriteBatch, SnapFree(_marinPosition), Color.White);
            }
            // draw the logo
            {
                var logoHeight = (int)(_spriteLogo0.SourceRectangle.Height * (MathF.Sin(_logoState * MathF.PI - MathF.PI / 2) * 0.5f + 0.5f));
                logoHeight += logoHeight % 2;

                Rectangle logoAPosition = new Rectangle 
                {
                     X = -_spriteBackground.SourceRectangle.Width / 2 + 16,
                     Y = 3 + _spriteLogo0.SourceRectangle.Height / 2 - logoHeight / 2,
                     Width = _spriteLogo0.SourceRectangle.Width,
                     Height = logoHeight
                };

                spriteBatch.Draw(_spriteLogo0.Texture, logoAPosition, _spriteLogo0.ScaledRectangle, Color.White);

                Rectangle logoBPosition = new Rectangle 
                {
                     X = -_spriteBackground.SourceRectangle.Width / 2 + 16,
                     Y = 3 + _spriteLogo1.SourceRectangle.Height / 2 - logoHeight / 2,
                     Width = _spriteLogo1.SourceRectangle.Width,
                     Height = logoHeight
                };
                spriteBatch.Draw(_spriteLogo1.Texture, logoBPosition, _spriteLogo1.ScaledRectangle, Color.White);

                if (_logoState == 1)
                {
                    Rectangle bigNPosition = new Rectangle
                    {
                         X = -_spriteBackground.SourceRectangle.Width / 2 + 25,
                         Y = _spriteBackground.SourceRectangle.Height / 2 + 4,
                         Width = _spriteBigN.SourceRectangle.Width,
                         Height = _spriteBigN.SourceRectangle.Height
                    };
                    spriteBatch.Draw(_spriteBigN.Texture, bigNPosition, _spriteBigN.ScaledRectangle, Color.White);

                    _dxFadeInDelay += Game1.DeltaTime;
                    if (_dxFadeInDelay > 1000)
                    {
                        // Different languages may require different offsets.
                        int offsetX = -8;
                        int offsetY = 9;

                        // Check for Chinese language and update offsets accordingly.
                        if (Game1.LanguageManager.CurrentLanguageCode == "chn")
                        {
                            offsetX = -6;
                            offsetY = -12;
                        }
                        _dxFadeInAlpha += MathHelper.Clamp(Game1.DeltaTime / 1200.0f, 0, 1);
                        Rectangle dxPosition = new Rectangle 
                        {
                             X = _spriteBackground.SourceRectangle.Width / 4 + offsetX,
                             Y = _spriteBackground.SourceRectangle.Height / 4 + offsetY,
                             Width = _spriteDX.SourceRectangle.Width,
                             Height = _spriteDX.SourceRectangle.Height
                        };
                        spriteBatch.Draw(_spriteDX.Texture, dxPosition, _spriteDX.ScaledRectangle, Color.White * _dxFadeInAlpha); 
                    }
                }
            }
            var lightPosition = _logoPosition + _lightPosition;

            // draw the light around the logo
            if (_lightAnimation.IsPlaying)
                _lightAnimation.DrawBasic(spriteBatch, lightPosition, Color.White);

            // draw the white forground for the fadein
            if (_strandFadeCount > 0)
            {
                var white = MathHelper.Clamp(_strandFadeCount / StrandFadeTime, 0, 1);
                spriteBatch.Draw(Resources.SprWhite, new Rectangle(screenLeft, screenTop, width, height), Color.White * white);
            }
        }
    }
}
