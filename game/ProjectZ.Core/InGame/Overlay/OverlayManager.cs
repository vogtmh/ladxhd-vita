using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.Base.UI;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Overlay.Sequences;
using ProjectZ.InGame.Pages;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Overlay
{
    public class OverlayManager
    {
        public float HudTransparency = 1;
        public bool DisableOverlayToggle;
        public bool DisableInventoryToggle;

        enum MenuState
        {
            None, Menu, Inventory, PhotoBook, GameSequence
        }

        enum GameScaleDirection: int
        {
            Decrease = -1, 
            Increase = 1
        }

        private MenuState _currentMenuState = MenuState.None;
        private MenuState _lastMenuState = MenuState.None;

        public bool InventoryState { get => _currentMenuState == MenuState.Inventory; }

        public TextboxOverlay TextboxOverlay;
        public HUDOverlay InGameHud;
        public AchievementOverlay AchievementOverlay;

        private InventoryOverlay _inventoryOverlay;
        private MapOverlay _mapOverlay;
        private DungeonOverlay _dungeonOverlay;
        private PhotoOverlay _photoOverlay;

        private Dictionary<string, GameSequence> _gameSequences = new Dictionary<string, GameSequence>();
        private string _currentSequenceName;

        private RenderTarget2D _menuRenderTarget2D;
        private UiRectangle _blurRectangle;

        private Rectangle _recInventory;
        private Rectangle _recMap;
        private Rectangle _recMapCenter;
        private Rectangle _recDungeon;

        private Vector2 _menuPosition;

        private Point _inventorySize;
        private Point _mapSize;
        private Point _dungeonSize;
        private Point _overlaySize;

        private Color _overlayFadeColor = new Color(255, 255, 255, 255);
        private double _fadeCount;
        private float _fadeAnimationPercentage;

        private float _hudState = 1;
        private float _hudPercentage;
        private bool _hideHud;

        private readonly int _marginMap = 0;
        private readonly int _margin = 2;
        private readonly int _fadeTime = 200;
        private const int ChangeTime = 125;
        private int _fadeDir;
        private float _scale;
        private float _changeCount;

        private int _overlayWidth;
        private int _overlayHeight;

        private bool _fading;
        private bool _updateInventory = true;
        private bool _isChanging;
        private bool _mapOpened;

        private int _scaleButtonCount;
        private bool _scaleButtonDown;
        private float _scaleButtonTimer;
        private float _scaleButtonPeriod;

        // The scale here is passed to MapOverlay, InventoryOverlay, and DungeonOverlay.
        public float Scale => _scale;

        // Public references to the inventory colors.
        public Color InventoryBackgroundColorTop;
        public Color InventoryBackgroundColorBot;
        public Color[] InventoryTunicColors;

        // Overworld Warp Points if enabling "Map Teleport" Redux Option.
        public Dictionary<Point, (int Level, Vector2 Teleport)> TeleportMap = new()
        {
        //-------------------------------------------------------------------------------
        //  [Tile Position]     = (Level, Teleport Position(X, Y))
        //-------------------------------------------------------------------------------
            [new Point(5, 4)]   = (0, new Vector2(872, 606)),     
            [new Point(3, 13)]  = (1, new Vector2(600, 1720)),       // Dungeon 1
            [new Point(4, 2)]   = (2, new Vector2(736, 340)),        // Dungeon 2
            [new Point(5, 11)]  = (3, new Vector2(920, 1472)),       // Dungeon 3
            [new Point(11, 2)]  = (4, new Vector2(1848, 320)),       // Dungeon 4
            [new Point(9, 13)]  = (5, new Vector2(1544, 1728)),      // Dungeon 5
            [new Point(12, 8)]  = (6, new Vector2(1992, 1120)),      // Dungeon 6
            [new Point(14, 0)]  = (7, new Vector2(2344, 96)),        // Dungeon 7
            [new Point(0, 1)]   = (8, new Vector2(104, 192)),        // Dungeon 8
            [new Point(5, 9)]   = (100, new Vector2(920.5f, 1248)),  // Teleporter 0
            [new Point(1, 0)]   = (101, new Vector2(280.5f, 96)),    // Teleporter 1
            [new Point(12, 2)]  = (102, new Vector2(2040.5f, 350)),  // Teleporter 2
            [new Point(12, 14)] = (103, new Vector2(2040.5f, 1888))  // Teleporter 3
        };

        // Values configurable via lahdmod.
        private bool  map_free_navigation            = false;
        private float inventory_scale_override       = 0;
        private int   inv_background_color_top_red   = 255;
        private int   inv_background_color_top_grn   = 255;
        private int   inv_background_color_top_blu   = 220;
        private float inv_background_color_top_alpha = 0.00f;
        private int   inv_background_color_bot_red   = 255;
        private int   inv_background_color_bot_grn   = 255;
        private int   inv_background_color_bot_blu   = 220;
        private float inv_background_color_bot_alpha = 0.00f;
        public  bool  inv_override_opaque_alpha      = false;
        private int   inv_tunic_green_color_red      = 16;
        private int   inv_tunic_green_color_grn      = 168;
        private int   inv_tunic_green_color_blu      = 64;
        private int   inv_tunic_blue_color_red       = 0;
        private int   inv_tunic_blue_color_grn       = 38;
        private int   inv_tunic_blue_color_blu       = 255;
        private int   inv_tunic_red_color_red        = 255;
        private int   inv_tunic_red_color_grn        = 0;
        private int   inv_tunic_red_color_blu        = 0;

        // Global references to lahdmod values.
        public bool UserCustomAlpha => inv_override_opaque_alpha;
        public bool MapFreeNavigation => map_free_navigation;

        public OverlayManager()
        {
            _blurRectangle = (UiRectangle)Game1.UiManager.AddElement(new UiRectangle(Rectangle.Empty, "background", Values.ScreenNameGame, Color.Transparent, Color.Transparent, null), true);

            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "OverlayManager.lahdmod");
            ModFile.Parse(modFile, this);

            // If the user set an alpha value use that, otherwise fall back to default value.
            float topAlpha = inv_background_color_top_alpha == 0.00 ? 0.75f : inv_background_color_top_alpha;
            float botAlpha = inv_background_color_bot_alpha == 0.00 ? 0.75f : inv_background_color_bot_alpha;

            // Set the colors to whatever they currently are.
            InventoryBackgroundColorTop = new Color(inv_background_color_top_red, inv_background_color_top_grn, inv_background_color_top_blu) * topAlpha;
            InventoryBackgroundColorBot = new Color(inv_background_color_bot_red, inv_background_color_bot_grn, inv_background_color_bot_blu) * botAlpha;

            // Create an array with the tunic colors.
            Color redTunic = new Color(inv_tunic_red_color_red, inv_tunic_red_color_grn, inv_tunic_red_color_blu);
            Color grnTunic = new Color(inv_tunic_green_color_red, inv_tunic_green_color_grn, inv_tunic_green_color_blu);
            Color bluTunic = new Color(inv_tunic_blue_color_red, inv_tunic_blue_color_grn, inv_tunic_blue_color_blu);
            InventoryTunicColors = new Color[]{ grnTunic, bluTunic, redTunic };
        }

        public void Load(ContentManager content)
        {
            // Add all game sequences to the stack.
            _gameSequences["map"] = new MapOverlaySequence();
            _gameSequences["marinBeach"] = new MarinBeachSequence();
            _gameSequences["marinCliff"] = new MarinCliffSequence();
            _gameSequences["towerCollapse"] = new TowerCollapseSequence();
            _gameSequences["shrine"] = new ShrineSequence();
            _gameSequences["picture"] = new PictureSequence();
            _gameSequences["photo"] = new PhotoSequence();
            _gameSequences["bowWow"] = new BowWowSequence();
            _gameSequences["castle"] = new CastleSequence();
            _gameSequences["gravestone"] = new GravestoneSequence();
            _gameSequences["weatherBird"] = new WeatherBirdSequence();
            _gameSequences["final"] = new FinalSequence();
            _gameSequences["painting"] = new PaintingSequence();

            // Set the size of the UI elements.
            _mapSize = new Point(144 + 2 * _marginMap, 144 + 2 * _marginMap);
            _dungeonSize = new Point(80, 106);
            _inventorySize = new Point(268, 208);
            _overlaySize = new Point(_inventorySize.X + _margin + _dungeonSize.X, _inventorySize.Y);

            // Set up the overlays.
            AchievementOverlay = new AchievementOverlay();
            TextboxOverlay = new TextboxOverlay();
            InGameHud = new HUDOverlay();
            _mapOverlay = new MapOverlay(_mapSize.X, _mapSize.Y, _marginMap, false);
            _inventoryOverlay = new InventoryOverlay(_inventorySize.X, _inventorySize.Y);
            _dungeonOverlay = new DungeonOverlay(_dungeonSize.X, _dungeonSize.Y);
            _photoOverlay = new PhotoOverlay();

            // Load up all of the overlays.
            _mapOverlay.Load();
            _dungeonOverlay.Load();
            _photoOverlay.Load();
        }

        public void RefreshPhotoOverlay()
        {
            // Used to swap between sepia/colored photos.
            _photoOverlay.Reload();
        }

        public void OnLoad()
        {
            CloseOverlay();
            _hideHud = false;
            _fadeCount = 0;
            TextboxOverlay.Init();

            // A new game session should always open the menu at the root page.
            Game1.UiPageManager.ClearSavedMenuStack();
        }

        public void Update()
        {
            // Update the fade in/out effect.
            UpdateFade();

            // Update the HUD (hearts, rupees, keys, save icon).
            InGameHud.Update(_hudPercentage, (1 - _hudPercentage) * HudTransparency);

            // Update achievement overlay.
            AchievementOverlay.Update();

            // Do not allow options or inventory to open during map transitions. This works around a bug on some Android devices where pressing "Start" to select a save
            // file from the file selection screen immediately opens the inventory before loading the game world. This takes advantage of the fact a map transition takes
            // place when first loading a save file. Aside from the early exit, the stuff above this at least needs to happen during transitions to prevent other bugs.
            if (MapManager.ObjLink?.IsTransitioning == true)
            {
                _currentMenuState = MenuState.None;
                return;
            }
            // See if the inventory was disabled in "script.zScript".
            bool disableOptions = Game1.GameManager.SaveManager.GetString("disable_options", "0") == "1";
            bool disableInventory = Game1.GameManager.SaveManager.GetString("disable_inventory", "0") == "1";

            // Toggle Game Options Menu Overlay
            if ((_currentMenuState == MenuState.None || _currentMenuState == MenuState.Menu) && ControlHandler.ButtonPressed(CButtons.Select) && !disableOptions)
                ToggleState(MenuState.Menu);

            // Toggle the Inventory / Map Overlay
            if ((_currentMenuState == MenuState.None || _currentMenuState == MenuState.Inventory) && ControlHandler.ButtonPressed(CButtons.Start) && 
                !disableInventory && !DisableInventoryToggle && !_hideHud && !TextboxOverlay.IsOpen)
                ToggleState(MenuState.Inventory);

            // Use the inventory disable to identify moments to lock the free camera.
            if (!Camera.ClassicMode && (_currentMenuState == MenuState.GameSequence || disableInventory || DisableInventoryToggle))
                MapManager.CameraOffset = Vector2.Zero;

            // Update the textbox and peform button scale change if a menu is currently not visible.
            if (_currentMenuState == MenuState.None)
            {
                // Update the textbox overlay.
                TextboxOverlay.Update();

                // Scale with bumper/trigger presses if enabled by the user.
                if (GameSettings.TriggersScale)
                    ButtonScaleChange();
            }
            // The menu is currently open so pause updating the game.
            else if (_currentMenuState == MenuState.Menu)
            {
                Game1.UpdateGame = false;
            }
            // The inventory menu is open so pause updating the game.
            else if (_currentMenuState == MenuState.Inventory)
            {
                Game1.UpdateGame = false;

                // Detect if the map screen transition is taking place.
                if (_isChanging)
                {
                    // Transition between map and inventory screens.
                    _changeCount += (_updateInventory ? 1 : -1) * Game1.DeltaTime;
                    if (_changeCount >= ChangeTime || _changeCount < 0)
                    {
                        _isChanging = false;
                        _changeCount = _updateInventory ? ChangeTime : 0;
                        _updateInventory = !_updateInventory;
                    }
                }
                // A transition between inventory and map is not taking place.
                else
                {
                    // Check if the select button has been pressed. 
                    if (ControlHandler.ButtonPressed(CButtons.Select) && !TextboxOverlay.IsOpen)
                        ToggleInventoryMap();

                    // Update the inventory menu (cursor movement, item selections, etc).
                    if (_updateInventory)
                        _inventoryOverlay.UpdateMenu();

                    // Map overlay is currently in the forefront.
                    _mapOverlay.IsSelected = !_updateInventory;
                    _mapOverlay.Update();

                    // Update the textbox overlay (map area descriptions).
                    TextboxOverlay.Update();
                    _dungeonOverlay.Update();
                }
            }
            // Photo overlay is currently open.
            else if (_currentMenuState == MenuState.PhotoBook)
            {
                // Pause updating the game.
                Game1.UpdateGame = false;

                // Update the textbox overlay.
                TextboxOverlay.Update();

                // When textbox overlay is closed update photo overlay.
                if (!TextboxOverlay.IsOpen)
                    _photoOverlay.Update();
            }
            // Sequence overlay is currently open (Marin on the beach, Mr.Write Christine picture, painting, etc.).
            else if (_currentMenuState == MenuState.GameSequence)
            {
                // Pause updating the game but force dialog updates.
                Game1.ForceDialogUpdate = true;
                Game1.UpdateGame = false;

                // Update the textbox overlay and game sequences.
                TextboxOverlay.Update();
                _gameSequences[_currentSequenceName].Update();
            }
            // Allow overlays and inventory to be closed/opened again via button press.
            DisableOverlayToggle = false;
            DisableInventoryToggle = false;
        }

        private void ResetGameScale()
        {
            int maxScale = Game1.MaxGameScale + 1;
            GameSettings.GameScale = maxScale;
            Game1.ScaleChanged = true;
        }

        private void ChangeGameScale(GameScaleDirection scaleDirection)
        {
            // Get the maximum scale and add 1 for auto-scale.
            int maxScale = Game1.MaxGameScale + 1;
            int curScale = GameSettings.GameScale;

            // Do not adjust the scale when classic camera is active.
            if (Camera.ClassicMode && GameSettings.ClassicScaling)
                return;

            // When autoscaling is set, match the scaling value so it can move up and down smoothly.
            if (GameSettings.GameScale == maxScale)
            {
                float gameScale = MathHelper.Clamp(Math.Min(Game1.WindowWidth / 160, Game1.WindowHeight / 128), 1, maxScale);
                gameScale = gameScale / 2;
                GameSettings.GameScale = (int)MathF.Ceiling(gameScale);
            }
            // Set the new scale using the passed parameter.
            int newScale = GameSettings.GameScale + (int)scaleDirection;

            // Do not let the scale fall outside the slider range.
            if (newScale >= -3 && newScale < maxScale)
            {
                // Apply the new scaling value.
                GameSettings.GameScale = newScale;

                // Apply current scaling settings.
                Game1.ScaleChanged = true;
            }
            // Do not try to apply scaling settings if we hit scale bedrock or ceiling.
            else
            {
                GameSettings.GameScale = curScale;
            }
        }

        public void ButtonScaleChange()
        {
            // Increase the timer if one of the scaling buttons are held.
            if (_scaleButtonDown)
                _scaleButtonTimer += Game1.DeltaTime;

            // Decrease the game scale with the bumpers or triggers.
            if (GameSettings.SixButtons
                ? ControlHandler.ButtonPressed(CButtons.LT)
                : ControlHandler.ButtonPressed(CButtons.LB))
            {
                ChangeGameScale(GameScaleDirection.Decrease);
                _scaleButtonDown = true;
                _scaleButtonTimer = -425f;
                _scaleButtonPeriod = 75;
            }

            // Increase the game scale with the bumpers or triggers.
            if (GameSettings.SixButtons
                ? ControlHandler.ButtonPressed(CButtons.RT)
                : ControlHandler.ButtonPressed(CButtons.RB))
            {
                ChangeGameScale(GameScaleDirection.Increase);
                _scaleButtonDown = true;
                _scaleButtonTimer = -425f;
                _scaleButtonPeriod = 75;
            }

            // Rapid fire decrease the game scale while the bumpers or triggers is held every 75ms.
            bool heldLeft = GameSettings.SixButtons
                ? ControlHandler.ButtonDown(CButtons.LT)
                : ControlHandler.ButtonDown(CButtons.LB);

            if (_scaleButtonDown && _scaleButtonTimer > _scaleButtonPeriod && heldLeft)
            {
                ChangeGameScale(GameScaleDirection.Decrease);
                _scaleButtonTimer = 0;
                _scaleButtonCount++;
            }

            // Rapid fire increase the game scale while the bumpers or triggers is held every 75ms.
            bool heldRight = GameSettings.SixButtons
                ? ControlHandler.ButtonDown(CButtons.RT)
                : ControlHandler.ButtonDown(CButtons.RB);

            if (_scaleButtonDown && _scaleButtonTimer > _scaleButtonPeriod && heldRight)
            {
                ChangeGameScale(GameScaleDirection.Increase);
                _scaleButtonTimer = 0;
                _scaleButtonCount++;
            }

            // When both bumpers or triggers are held, reset the game scale to the maximum scale which is Auto-Scaling.
            if (GameSettings.SixButtons
                ? (ControlHandler.ButtonDown(CButtons.RT) && ControlHandler.ButtonDown(CButtons.LT))
                : (ControlHandler.ButtonDown(CButtons.RB) && ControlHandler.ButtonDown(CButtons.LB)))
            {
                ResetGameScale();
            }

            // The longer the button is held down, the faster the "zoom" will get. The left value in the switch represents
            // how many scaling iterations have passed, the right value represents how many milliseconds between iterations.
            _scaleButtonPeriod = _scaleButtonCount switch
            {
                <  5  => 75,
                <  8  => 60,
                <  12 => 55,
                <  15 => 40,
                <  18 => 25,
                <  21 => 10,
                >= 24 =>  5,
                _ => _scaleButtonPeriod
            };

            // When either button is released, reset the repeat variables.
            if (GameSettings.SixButtons
                ? (ControlHandler.ButtonReleased(CButtons.LT) || ControlHandler.ButtonReleased(CButtons.RT))
                : (ControlHandler.ButtonReleased(CButtons.LB) || ControlHandler.ButtonReleased(CButtons.RB)))
            {
                _scaleButtonDown = false;
                _scaleButtonTimer = 0;
                _scaleButtonCount = 0;
            }
        }

        public bool TryGetMapTeleportTarget(Point node, out Vector2 target)
        {
            target = Vector2.Zero;

            // All map teleports require the overworld and teleporting to be enabled at all.
            if (!MapManager.ObjLink.Map.IsOverworld || GameSettings.MapTeleport == 0)
                return false;

            if (!TeleportMap.TryGetValue(node, out var data))
                return false;

            int level = data.Level;

            // Manbo's Pond (Level 0): needs the Manbo flag and mode >= 2.
            if (level == 0)
            {
                if (!MapManager.ObjLink.ManboTeleport || GameSettings.MapTeleport < 2)
                    return false;
                target = data.Teleport;
                return true;
            }

            // Dungeons (Level 1-8): need the matching instrument.
            if (level >= 1 && level <= 8)
            {
                bool dungeonsEnabled = GameSettings.MapTeleport == 1 || GameSettings.MapTeleport == 3
                                       || (GameSettings.MapTeleport == 2 && MapManager.ObjLink.ManboTeleport);
                if (!dungeonsEnabled)
                    return false;

                var instrument = Game1.GameManager.GetItem("instrument" + (level - 1));
                if (instrument == null || instrument.Count < 1)
                    return false;

                target = data.Teleport;
                return true;
            }

            // Overworld teleporters (Level 100 + id): gated purely on discovery.
            if (level >= 100 && level <= 103)
            {
                int id = level - 100;

                // Decide which modes expose teleporters. Defaulting to the dungeon predicate
                // so they appear alongside dungeon warps; change to (MapTeleport == 3) to limit to "full".
                bool teleportersEnabled = GameSettings.MapTeleport == 1 || GameSettings.MapTeleport == 3
                                          || (GameSettings.MapTeleport == 2 && MapManager.ObjLink.ManboTeleport);
                if (!teleportersEnabled)
                    return false;

                if (Game1.GameManager.SaveManager.GetString("unlocked_teleporter_" + id) != "1")
                    return false;

                target = data.Teleport;
                return true;
            }

            return false;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw the game UI; fade out with overlay fade in.
            InGameHud.DrawTop(spriteBatch, _hudPercentage, (1 - _hudPercentage) * HudTransparency);

            // Draw the achievement overlay.
            AchievementOverlay.Draw(spriteBatch);

            // Draw the Textbox overlay.
            TextboxOverlay.DrawTop(spriteBatch);

            // Draw the Inventory / Map Screen / Photo Overlay / Game Sequence.
            if (_fadeAnimationPercentage > 0)
            {
                // If the current or last menu state is the inventory.
                if (_currentMenuState == MenuState.Inventory || _lastMenuState == MenuState.Inventory)
                {
                    // Draw the menu on the screen.
                    var menuY = 25 * _scale * (1 - _fadeAnimationPercentage);
                    var menuColor = Color.White * _fadeAnimationPercentage;

                    // Try to align the inventory while the dungeon panel is on the side of it.
                    // When the resololution is not wide enough move the inventory to the left.
                    int dungeonOffset;

                    if (!Game1.GameManager.MapManager.CurrentMap.IsDungeon)
                        dungeonOffset = (int)((_margin + _dungeonSize.X) * _scale / 2);
                    else
                        dungeonOffset = (int)(Math.Clamp((_margin + _dungeonSize.X) * _scale / 2, -16, 
                            Math.Max(-16, (Game1.WindowWidth - _overlayWidth) / 2 - 8)));

                    spriteBatch.Draw(_menuRenderTarget2D, new Rectangle(
                        (int)_menuPosition.X + dungeonOffset, (int)(_menuPosition.Y - menuY), _overlayWidth, _overlayHeight), menuColor);
                }
                // Draw the Photobook overlay.
                else if (_currentMenuState == MenuState.PhotoBook || _lastMenuState == MenuState.PhotoBook)
                    _photoOverlay.Draw(spriteBatch, _fadeAnimationPercentage);

                // Draw the current game sequence.
                else if (_currentMenuState == MenuState.GameSequence || _lastMenuState == MenuState.GameSequence)
                    _gameSequences[_currentSequenceName].Draw(spriteBatch, _fadeAnimationPercentage);

                // Draw the inventory screen.
                if (_currentMenuState == MenuState.Inventory)
                {
                    // Draw the map toggle button and label.
                    var mapStart = "";
                    if (ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[CButtons.Select].Keys.Length > 0)
                        mapStart = ControlHandler.ButtonDictionary[CButtons.Select].Keys[0].ToString();
                    if (!ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[CButtons.Select].Buttons.Length > 0)
                        mapStart = ControlHandler.GetButtonName(ControlHandler.ButtonDictionary[CButtons.Select].Buttons[0]);

                    var mapString = mapStart + ": " + Game1.LanguageManager.GetString(_updateInventory ? "overlay_map" : "overlay_inventory", "error");
                    var mapDrawPos = new Vector2(8 * _scale, Game1.WindowHeight - 16 * _scale);

                    DrawHelper.DrawString(spriteBatch, mapString, mapDrawPos, InterfaceElement.MainTextColor * _fadeAnimationPercentage, 0, Vector2.Zero, _scale, SpriteEffects.None, 0);

                    // When navigating the map, get the currently selected map position.
                    var nodeSelected = _mapOverlay.SelectionPosition;

                    // If we're in map mode and one of the dungeons are selected.
                    if (!_updateInventory && TryGetMapTeleportTarget(nodeSelected, out _))
                    {
                        // Get the selected dungeon and check if the instrument has been collected.
                        int dungeonLevel = TeleportMap[nodeSelected].Level - 1;
                        var hasInstrument = Game1.GameManager.GetItem("instrument" + dungeonLevel);

                        // If instrument has not been collected don't draw the text.
                        if (dungeonLevel < 99 && (hasInstrument == null || hasInstrument.Count < 1))
                            return;

                        // Reverse the label "X" with label "Y" if the user has a need for this.
                        var button = ControlHandler.ReverseTeleportLabels ? CButtons.Y : CButtons.X;

                        // Get the correct button to display next to the text.
                        var teleStart = "";
                        if (ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[CButtons.X].Keys.Length > 0)
                            teleStart = ControlHandler.ButtonDictionary[button].Keys[0].ToString();
                        if (!ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[CButtons.X].Buttons.Length > 0)
                            teleStart = ControlHandler.GetButtonName(ControlHandler.ButtonDictionary[button].Buttons[0]);

                        // Set up the string to display.
                        var teleString = teleStart + ": " + Game1.LanguageManager.GetString("overlay_teleport", "error");
                        var teleTextSize = DrawHelper.MeasureString(teleString);
                        var teleDrawPos = new Vector2(Game1.WindowWidth - (teleTextSize.X + 6) * _scale, Game1.WindowHeight - 16 * _scale);

                        // Draw the teleport button and label.
                        DrawHelper.DrawString(spriteBatch, teleString, teleDrawPos, InterfaceElement.MainTextColor * _fadeAnimationPercentage, 0, Vector2.Zero, _scale, SpriteEffects.None, 0);
                    }
                }
            }
        }

        private void EnsureMenuRenderTarget()
        {
            // Skip creation if width/height is invalid (can happen during resize or at startup).
            if (_overlayWidth <= 0 || _overlayHeight <= 0)
                return;

            if (_menuRenderTarget2D == null
                || _menuRenderTarget2D.IsDisposed
                || _menuRenderTarget2D.Width != _overlayWidth
                || _menuRenderTarget2D.Height != _overlayHeight)
            {
                try
                {
                    _menuRenderTarget2D?.Dispose();
                    _menuRenderTarget2D = new RenderTarget2D(Game1.Graphics.GraphicsDevice,
                        Math.Max(1, _overlayWidth),
                        Math.Max(1, _overlayHeight));
                }
                catch (Exception ex)
                {
                    // optional: log for debugging
                    System.Diagnostics.Debug.WriteLine($"MenuRenderTarget creation failed: {ex.Message}");
                    _menuRenderTarget2D = null;
                }
            }
        }

        public void DrawRenderTarget(SpriteBatch spriteBatch)
        {
            // If the fade percentage is above zero and a game sequence is currently visible, draw it with current render target.
            if (_fadeAnimationPercentage > 0 && (_currentMenuState == MenuState.GameSequence || _lastMenuState == MenuState.GameSequence))
                _gameSequences[_currentSequenceName].DrawRT(spriteBatch);

            // If the inventory is currently visible.
            if (_currentMenuState == MenuState.Inventory)
            {
                // Ensure the render target exists and has valid size
                EnsureMenuRenderTarget();

                // Don't try drawing if RT is currently null.
                if (_menuRenderTarget2D == null)
                    return;

                // Draw the various overlays.
                _mapOverlay.DrawRenderTarget(spriteBatch);
                _inventoryOverlay.DrawRT(spriteBatch);
                _dungeonOverlay.DrawOnRenderTarget(spriteBatch);

                Game1.Graphics.GraphicsDevice.SetRenderTarget(_menuRenderTarget2D);
                Game1.Graphics.GraphicsDevice.Clear(Color.Transparent);

                // Draw the inventory.
                DrawInventory(spriteBatch);
            }
        }

        public void UpdateRenderTarget()
        {
            EnsureMenuRenderTarget();

            _inventoryOverlay.UpdateRenderTarget();
            _mapOverlay.UpdateRenderTarget();
            _dungeonOverlay.UpdateRenderTarget();

            UpdateOverlayDimensions();
        }

        public void DisposeRenderTargets()
        {
            try
            {
                _inventoryOverlay?.DisposeRenderTargets();
                _mapOverlay?.DisposeRenderTargets();
                _dungeonOverlay?.DisposeRenderTargets();

                _menuRenderTarget2D?.Dispose(); 
                _menuRenderTarget2D = null;
            }
            catch { }
        }

        private void UpdateOverlayDimensions()
        {
            // If the user forced a scale via lahdmod use that otherwise use the UI scale.
            if (inventory_scale_override > 0)
                _scale = inventory_scale_override;
            else
                _scale = Game1.UiScale;

            // Recalculate the size of the inventory.
            _recInventory = new Rectangle(0, 0, (int)(_inventorySize.X * _scale), (int)(_inventorySize.Y * _scale));

            // Recalculate the size of the map stuff.
            _recMap = new Rectangle(
                (int)(_recInventory.Right - 6 * _scale - _mapSize.X * _scale),
                (int)(_recInventory.Bottom - 6 * _scale - _mapSize.Y * _scale), 
                (int)(_mapSize.X * _scale), 
                (int)(_mapSize.Y * _scale));
            _recMapCenter = new Rectangle(
                (int)(_recInventory.Width / 2 - _mapSize.X / 2 * _scale),
                (int)(_recInventory.Height / 2 - _mapSize.Y / 2 * _scale), 
                (int)(_mapSize.X * _scale), 
                (int)(_mapSize.Y * _scale));
            _recDungeon = new Rectangle(
                (int)(_recInventory.Right + _margin * _scale),
                (int)(_recInventory.Bottom - _dungeonSize.Y * _scale),
                (int)(_dungeonSize.X * _scale), 
                (int)(_dungeonSize.Y * _scale));
        }

        private void DrawInventory(SpriteBatch spriteBatch)
        {
            // Recalculate the dimensions in case the UI scale has changed.
            UpdateOverlayDimensions();

            spriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.PointClamp, null, null, null, null);

            var percentage = MathF.Sin(-MathF.PI / 2 + (_changeCount / ChangeTime) * MathF.PI) * 0.5f + 0.5f;

            // Draw the inventory overlay.
            _inventoryOverlay.Draw(spriteBatch, _recInventory, Color.White * (1 - percentage));

            spriteBatch.End();

            // Draw the map onto the inventory screen.
            var mapRectangle = new Rectangle(
                (int)MathHelper.Lerp(_recMap.X, _recMapCenter.X, percentage),
                (int)MathHelper.Lerp(_recMap.Y, _recMapCenter.Y, percentage), _recMap.Width, _recMap.Height);
            _mapOverlay.Draw(spriteBatch, mapRectangle, Color.White);

            spriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.PointClamp);

            // Draw the dungeon map.
            _dungeonOverlay.Draw(spriteBatch, _recDungeon, Color.White * (1 - percentage));

            spriteBatch.End();
        }

        public void UpdateInventoryButtons(bool sixButtons)
        {
            // Updates the number of equippable buttons between 4 and 6 buttons.
            _inventoryOverlay.UpdateButtonLayout(sixButtons);
        }

        public void ResolutionChanged()
        {
            TextboxOverlay?.ResolutionChange();
            InGameHud?.ResolutionChange();
            _inventoryOverlay?.ResolutionChanged();
            _dungeonOverlay?.ResolutionChanged();

            _blurRectangle.Rectangle.Width = Game1.WindowWidth;
            _blurRectangle.Rectangle.Height = Game1.WindowHeight;

            // If the user forced a scale via lahdmod use that otherwise use the UI scale.
            if (inventory_scale_override > 0)
                _scale = inventory_scale_override;
            else
                _scale = Game1.UiScale;

            // Render at actual screen resolution (not native resolution)
            _overlayWidth = (int)(_overlaySize.X * _scale);
            _overlayHeight = (int)(_overlaySize.Y * _scale);

            _menuPosition = new Vector2(
                Game1.WindowWidth / 2 - _overlayWidth / 2, 
                Game1.WindowHeight / 2 - _overlayHeight / 2);

            EnsureMenuRenderTarget();
        }

        public void OpenPhotoOverlay()
        {
            // Open photo overlay and set the menu state.
            _photoOverlay.OnOpen();
            Game1.GameManager.StartDialogPath("photo_book_text");
            SetState(MenuState.PhotoBook);
        }

        public void StartSequence(string name)
        {
            // Exit if the sequence doesn't exist in the dictionary.
            if (!_gameSequences.ContainsKey(name))
                return;

            // Start the sequence and set the menu state.
            _currentSequenceName = name;
            _gameSequences[_currentSequenceName].OnStart();
            SetState(MenuState.GameSequence);
        }

        public GameSequence GetCurrentGameSequence()
        {
            // Get the current game sequence.
            if (_currentSequenceName != null && _gameSequences.ContainsKey(_currentSequenceName))
                return _gameSequences[_currentSequenceName];

            return null;
        }

        public void ToggleInventoryMap()
        {
            _isChanging = true;

            if (!_mapOpened)
                Game1.AudioManager.PlaySoundEffect("D360-19-13");

            _mapOpened = !_mapOpened;
        }

        public bool UpdateCameraAndAnimation()
        {
            return (_currentMenuState != MenuState.Inventory && TextboxOverlay.IsOpen) || _currentMenuState == MenuState.GameSequence;
        }

        public void HideHud(bool hidden)
        {
            _hideHud = hidden;
        }

        private void UpdateFade()
        {
            // Update the fade in/out effect.
            if (_fading)
            {
                _fadeCount += Game1.DeltaTime * _fadeDir;

                // Fade in/out has finished.
                if (_fadeCount <= 0 || _fadeCount >= _fadeTime)
                {
                    _fading = false;
                    _fadeCount = MathHelper.Clamp((float)_fadeCount, 0, _fadeTime);
                }
            }
            // Make the background slightly darker when blur is disabled.
            var darkness = GameSettings.DisableUiBlur ? 0.65f : 0.50f;

            // Update the fade percentage.
            var fadePercentage = (float)_fadeCount / _fadeTime;
            _fadeAnimationPercentage = (float)Math.Sin(Math.PI / 2 * fadePercentage);
            _blurRectangle.BackgroundColor = Color.Black * darkness * _fadeAnimationPercentage;
            _blurRectangle.BlurColor = _overlayFadeColor * _fadeAnimationPercentage;

            if (_fadeAnimationPercentage <= 0 && _currentSequenceName != null && _currentMenuState == MenuState.None)
                _currentSequenceName = null;

            // Hide the HUD.
            if (TextboxOverlay.IsOpen || _currentMenuState != MenuState.None || _hideHud)
            {
                _hudState = AnimationHelper.MoveToTarget(_hudState, 1, 0.1f * Game1.TimeMultiplier);
            }
            else if (!Game1.GameManager.DialogIsRunning() && (Game1.UpdateGame || Game1.ForceDialogUpdate))
            {
                _hudState = AnimationHelper.MoveToTarget(_hudState, 0, 0.1f * Game1.TimeMultiplier);
            }
            // Update the HUD percentage.
            _hudPercentage = (float)Math.Sin(Math.PI / 2 * _hudState);
        }

        private void ToggleState(MenuState newState)
        {
            if (_currentMenuState == MenuState.None)
            {
                SetState(newState);
            }
            else
            {
                _mapOpened = false;
                CloseOverlay();
            }
        }

        private bool AndroidDpadSwapState(MenuState state)
        {
            if (state == MenuState.Menu || state == MenuState.Inventory || state == MenuState.PhotoBook)
                return true;

            if (state == MenuState.GameSequence && _currentSequenceName == "map")
                return true;

            return false;
        }

        private void SetState(MenuState newState)
        {
            // Don't change the state if a textbox is open.
            if (TextboxOverlay.IsOpen || DisableOverlayToggle)
                return;

            // If it's the inventory or the menu that was opened.
            if (newState == MenuState.Inventory || newState == MenuState.Menu)
            {
                // Pause the currently playing soundeffects.
                Game1.AudioManager.PauseSoundEffects();

                // Play the menu opening sound when opening the options menu or inventory menu.
                Game1.AudioManager.PlaySoundEffect("D360-17-11");
            }
            // The inventory open was opened.
            if (newState == MenuState.Inventory)
            {
                _isChanging = false;
                _changeCount = 0;
                _updateInventory = true;

                _mapOverlay.OnFocus();
                _dungeonOverlay.OnFocus();
            }
            // The options menu was opened.
            else if (newState == MenuState.Menu)
            {
                // Re-open to the page the player was last on; fall back to the root menu page.
                if (!Game1.UiPageManager.RestoreMenuStack(null, PageManager.TransitionAnimation.TopToBottom, PageManager.TransitionAnimation.TopToBottom))
                    Game1.UiPageManager.ChangePage(typeof(GameMenuPage), null, PageManager.TransitionAnimation.TopToBottom, PageManager.TransitionAnimation.TopToBottom);

                // Refresh the achievements page to reflect any new achievements that were earned.
                if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(AchievementsPage), out var achievementsPage))
                {
                    var AchievementsPage = (AchievementsPage)achievementsPage;
                    AchievementsPage.RefreshAchievedStates();
                }
            }
            _fading = true;
            _fadeDir = 1;
            _lastMenuState = _currentMenuState;
            _currentMenuState = newState;

            if (Game1.PlatformInput.HasTouchInput && AndroidDpadSwapState(newState))
                VirtualController.Initialize(Game1.WindowWidth, Game1.WindowHeight, true);

        }

        public void CloseOverlay()
        {
            // Remember the page the player was on so the menu re-opens to it next time.
            if (_currentMenuState == MenuState.Menu)
                Game1.UiPageManager.SaveMenuStack();

            // Play the menu closing sound when closing the options menu or inventory menu.
            if (_currentMenuState == MenuState.Inventory || _currentMenuState == MenuState.Menu)
                Game1.AudioManager.PlaySoundEffect("D360-18-12");

            if (Game1.PlatformInput.HasTouchInput && AndroidDpadSwapState(_currentMenuState))
                VirtualController.Initialize(Game1.WindowWidth, Game1.WindowHeight, false);

            _fading = true;
            _fadeDir = -1;
            _lastMenuState = _currentMenuState;
            _currentMenuState = MenuState.None;

            // Store last input state and close all open pages.
            InputHandler.ResetInputState();
            Game1.UiPageManager.PopAllPages(PageManager.TransitionAnimation.TopToBottom, PageManager.TransitionAnimation.TopToBottom);

            // Resume the sound effects.
            Game1.AudioManager.ContinueSoundEffects();
        }

        public bool MenuIsOpen()
        {
            // Store the menu state when opening the menu.
            return _currentMenuState == MenuState.Menu;
        }
    }
}
