using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Overlay
{
    public class MapOverlay
    {
        public bool IsSelected;

        private RenderTarget2D _renderTarget;

        private readonly Rectangle _recMap = new Rectangle(8, 0, 144, 144);
        private readonly Rectangle _recHide = new Rectangle(167, 103, 9, 9);
        private readonly Rectangle _recIcon = new Rectangle(161, 1, 30, 30);

        private Point _selectionPosition;
        private Point _iconPosition;

        private Animator _animationPlayer = new Animator();
        private Animator _animationSelection = new Animator();

        private readonly int[,] _mapIcons = new int[16, 16];

        private readonly string[,] _mapDialog = new string[,] {
        { "map_tal_tal", "map_tal_tal", "map_tal_tal", "map_tal_tal", "map_tal_tal", "map_tal_tal", "map_wind_fishs_egg", "map_mt_tamaranch", "map_owl_bridge", "map_tal_tal", "map_hen_house", "map_tal_tal", "map_tal_tal", "map_tal_tal", "map_level_7", "map_tal_tal" },
        { "map_level_8","map_telephone_booth","map_tal_tal","map_tal_tal","map_tal_tal","map_tal_tal","map_owl_fish","map_owl_mountain","map_tal_tal","map_tal_tal","map_tal_tal","map_tal_tal","map_tal_tal","map_tal_tal","map_tal_tal","map_tal_tal" },
        { "map_goponga_swamp","map_goponga_swamp","map_goponga_swamp","map_goponga_swamp","map_level_2","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_level_4","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights" },
        { "map_weird_mr_write","map_telephone_booth","map_goponga_swamp","map_goponga_swamp","map_goponga_swamp","map_tal_tal_heights","map_owl_tal_tal_heights","map_photo_both","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_tal_tal_heights","map_raft_shop" },
        { "map_mysterious_woods","map_owl_woods","map_mysterious_woods","map_mysterious_woods","map_koholint_prairie","map_crazy_tracy","map_tabahl_wasteland","map_tabahl_wasteland","map_kanalet_castle","map_kanalet_castle","map_kanalet_castle","map_telephone_booth","map_rapids_ride","map_rapids_ride","map_rapids_ride","map_rapids_ride" },
        { "map_mysterious_woods","map_mysterious_woods","map_mysterious_woods","map_mysterious_woods","map_koholint_prairie","map_koholint_prairie","map_tabahl_wasteland","map_tabahl_wasteland","map_kanalet_castle","map_kanalet_castle","map_kanalet_castle","map_kanalet_castle","map_rapids_ride","map_rapids_ride","map_rapids_ride","map_rapids_ride" },
        { "map_mysterious_woods","map_mysterious_woods","map_mysterious_woods","map_mysterious_woods","map_owl_prairie","map_witchs_hut","map_cementry","map_cementry","map_kanalet_castle","map_kanalet_castle","map_kanalet_castle","map_kanalet_castle","map_rapids_ride","map_rapids_ride","map_rapids_ride","map_rapids_ride" },
        { "map_mysterious_woods","map_mysterious_woods","map_mysterious_woods","map_mysterious_woods","map_koholint_prairie","map_koholint_prairie","map_cementry","map_cementry","map_kanalet_castle","map_kanalet_castle","map_kanalet_castle","map_kanalet_castle","map_rapids_ride","map_rapids_ride","map_rapids_ride","map_rapids_ride" },
        { "map_owl_woods_entry","map_fishing_pond","map_quadruplets_house","map_dream_shrine","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_telephone_booth","map_ukuku_prairie","map_seashell_mansion","map_ukuku_prairie","map_level_6","map_face_shrine","map_rapids_ride","map_rapids_ride" },
        { "map_mysterious_woods","map_mysterious_woods","map_mabe_village","map_town_tool_shop","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_owl_level_6_post","map_owl_level_6","map_rapids_ride","map_rapids_ride"},
        { "map_mabe_village","map_madam_meow","map_marin_tarin","map_mabe_village","map_telephone_booth","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_owl_shrine","map_face_shrine","map_face_shrine","map_face_shrine" },
        { "map_village_library","map_old_man_house","map_telephone_booth","map_trendy_game","map_ukuku_prairie","map_level_3","map_owl_level_3","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_ukuku_prairie","map_face_shrine","map_face_shrine","map_face_shrine","map_face_shrine"},
        { "map_south_village","map_south_village","map_south_village","map_south_village","map_signpost_maze","map_signpost_maze","map_pothole_field","map_pothole_field","map_maraths_bay","map_maraths_bay","map_maraths_bay","map_maraths_bay","map_animal_village","map_animal_village","map_yarna_desert","map_yarna_desert"},
        { "map_south_village","map_south_village","map_owl_level_1","map_level_1","map_signpost_maze","map_signpost_maze","map_richards_villa","map_pothole_field","map_maraths_bay","map_level_5","map_maraths_bay","map_telephone_booth","map_animal_village","map_animal_village","map_yarna_desert","map_yarna_desert"},
        { "map_toronbo_shores","map_toronbo_shores","map_toronbo_shores","map_banana_house","map_toronbo_shores","map_toronbo_shores","map_maraths_bay","map_maraths_bay","map_telephone_booth","map_maraths_bay","map_maraths_bay","map_maraths_bay","map_bay_east","map_bay_east","map_owl_desert","map_yarna_desert"},
        { "map_toronbo_shores","map_toronbo_shores","map_owl_shore","map_toronbo_shores","map_toronbo_shores","map_toronbo_shores","map_house_bay","map_maraths_bay","map_maraths_bay","map_maraths_bay","map_maraths_bay","map_maraths_bay","map_bay_east","map_bay_east","map_yarna_desert","map_yarna_desert"}};

        private float _animationCount;
        private float _animationState;

        private int _width;
        private int _height;
        private int _margin;
        private int _renderScale = 1;

        private int _iconAnimationDirection;
        private int _shownSelection;

        private double _buttonDownCounter;

        private bool _iconAnimationRunning;
        private bool _fullMap;

        private float _scale => Game1.GameManager.InGameOverlay.Scale;

        Animator[] _animTeleporters = new Animator[4];
        Animator[] _animDungeons = new Animator[8];
        Animator _manboPond = new Animator();

        Point[] _dungeonPosition = new Point[8];
        Point _manboPosition = new Point(5, 4);

        public Point SelectionPosition { get => _selectionPosition; }

        public MapOverlay(int width, int height, int margin, bool fullMap)
        {
            _width = width;
            _height = height;
            _margin = margin;
            _fullMap = fullMap;

            // Icons:
            // 1: shop, 2: ?, 3: cave, 4: owl
            _mapIcons[6,0]   = 2;
            _mapIcons[8,0]   = 4;
            _mapIcons[10,0]  = 2;
            _mapIcons[14,0]  = 3;

            _mapIcons[0,1]   = 3;
            _mapIcons[1,1]   = 2;
            _mapIcons[6,1]   = 4;
            _mapIcons[7,1]   = 4;

            _mapIcons[4,2]   = 3;
            _mapIcons[11,2]  = 3;

            _mapIcons[0,3]   = 2;
            _mapIcons[1,3]   = 2;
            _mapIcons[6,3]   = 4;
            _mapIcons[7,3]   = 1;
            _mapIcons[15,3]  = 1;

            _mapIcons[1,4]   = 4;
            _mapIcons[5,4]   = 1;
            _mapIcons[11,4]  = 2;

            _mapIcons[4,6]   = 4;
            _mapIcons[5,6]   = 1;

            _mapIcons[0,8]   = 4;
            _mapIcons[1,8]   = 1;
            _mapIcons[2,8]   = 2;
            _mapIcons[3,8]   = 2;
            _mapIcons[8,8]   = 2;
            _mapIcons[10,8]  = 2;
            _mapIcons[12,8]  = 3;

            _mapIcons[3,9]   = 1;
            _mapIcons[12,9]  = 4;
            _mapIcons[13,9]  = 4;

            _mapIcons[1,10]  = 2;
            _mapIcons[2,10]  = 2;
            _mapIcons[4,10]  = 2;
            _mapIcons[12,10] = 4;

            _mapIcons[0,11]  = 2;
            _mapIcons[1,11]  = 2;
            _mapIcons[2,11]  = 2;
            _mapIcons[3,11]  = 1;
            _mapIcons[5,11]  = 3;
            _mapIcons[6,11]  = 4;

            _mapIcons[2,13]  = 4;
            _mapIcons[3,13]  = 3;
            _mapIcons[6,13]  = 2;
            _mapIcons[9,13]  = 3;
            _mapIcons[11,13] = 2;

            _mapIcons[3,14]  = 2;
            _mapIcons[8,14]  = 2;
            _mapIcons[14,14] = 4;

            _mapIcons[2,15]  = 4;
            _mapIcons[6,15]  = 2;

            // Positions of Dungeons on the minimap 1-8.
            _dungeonPosition[0] = new Point(3, 13);
            _dungeonPosition[1] = new Point(4, 2);
            _dungeonPosition[2] = new Point(5, 11);
            _dungeonPosition[3] = new Point(11, 2);
            _dungeonPosition[4] = new Point(9, 13);
            _dungeonPosition[5] = new Point(12, 8);
            _dungeonPosition[6] = new Point(14, 0);
            _dungeonPosition[7] = new Point(0, 1);
        }

        public void Load()
        {
            // Load the player and selection icons and play their animation.
            _animationPlayer = AnimatorSaveLoad.LoadAnimator("mapPlayer");
            _animationSelection = AnimatorSaveLoad.LoadAnimator("mapSelector");
            _animationPlayer.Play("idle");
            _animationSelection.Play("idle");

            // Load the dungeon icons and play their animation.
            for (int i = 0; i < 8; i++)
            {
                _animDungeons[i] = AnimatorSaveLoad.LoadAnimator("mapDungeon");
                _animDungeons[i].Play("idle");
            }
            // Load the icon for Manbo's Pond.
            _manboPond = AnimatorSaveLoad.LoadAnimator("mapManboPond");
            _manboPond.Play("idle");

            // Load the overworld teleporter icons and play their animation.
            for (int i = 0; i < 4; i++)
            {
                _animTeleporters[i] = AnimatorSaveLoad.LoadAnimator("mapTeleporter");
                _animTeleporters[i].Play("idle");
            }
            // Try to load a custom map overlay file.
            string customMapOverlay = Path.Combine(Values.ResolvedMods, "MapOverlay.data");
            LoadMapDataFromFile(customMapOverlay);
        }

        public void LoadMapDataFromFile(string path)
        {
            try
            {
                // Check if the file path is invalid or it doesn't exist.
                if (string.IsNullOrEmpty(path) || !GameFS.Exists(path))
                    return;

                // If it exists load the custom map overlay file.
                LoadMapData(GameFS.ReadAllText(path));
            }
            catch { }
        }

        public void LoadMapData(string content)
        {
            // If the text file is empty return.
            if (string.IsNullOrWhiteSpace(content))
                return;

            // Get the number of rows and columns.
            int height = _mapDialog.GetLength(0);
            int width  = _mapDialog.GetLength(1);

            // Create lists to store the data.
            var dialogRows  = new System.Collections.Generic.List<string[]>();
            var iconEntries = new System.Collections.Generic.List<(int X, int Y, int Value)>();
            var dungeonEntries = new System.Collections.Generic.List<(int Index, int X, int Y)>();

            // Tracks the type of data we are attempting to import.
            bool hasDialog = false;
            bool hasIcons  = false;
            bool hasDungeons = false;
            string section = null;

            // Loop through the lines in the document.
            foreach (var rawLine in content.Split('\n'))
            {
                // Trim any leading or trailing white space.
                var line = rawLine.Trim();

                // Skip blanks and comments.
                if (line.Length == 0 || line[0] == ';' || line[0] == '#' || (line.Length >= 2 && line[0] == '/' && line[1] == '/'))
                    continue;

                // Section header.
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    if (section == "MapDialog") hasDialog = true;
                    else if (section == "MapIcons") hasIcons = true;
                    else if (section == "DungeonPosition") hasDungeons = true;
                    continue;
                }
                // We're in the "MapDialog" section.
                if (section == "MapDialog")
                {
                    var cells = line.Split(',');
                    for (int i = 0; i < cells.Length; i++)
                        cells[i] = cells[i].Trim();
                    dialogRows.Add(cells);
                }
                // We're in the "MapIcons" section.
                else if (section == "MapIcons")
                {
                    var eq = line.IndexOf('=');
                    if (eq < 0)
                        continue;

                    var coords = line.Substring(0, eq).Split(',');
                    if (coords.Length != 2 ||
                        !int.TryParse(coords[0].Trim(), out int ix) ||
                        !int.TryParse(coords[1].Trim(), out int iy) ||
                        !int.TryParse(line.Substring(eq + 1).Trim(), out int value))
                        continue;

                    if (ix < 0 || ix >= width || iy < 0 || iy >= height)
                        continue;

                    iconEntries.Add((ix, iy, value));
                }
                // We're in the "DungeonPosition" section.
                else if (section == "DungeonPosition")
                {
                    var eq = line.IndexOf('=');
                    if (eq < 0)
                        continue;

                    // Left of '=' is the dungeon index.
                    if (!int.TryParse(line.Substring(0, eq).Trim(), out int index))
                        continue;

                    // Right of '=' is the "x,y" point.
                    var coords = line.Substring(eq + 1).Split(',');
                    if (coords.Length != 2 ||
                        !int.TryParse(coords[0].Trim(), out int dx) ||
                        !int.TryParse(coords[1].Trim(), out int dy))
                        continue;

                    // Index must be a valid dungeon slot.
                    if (index < 0 || index >= _dungeonPosition.Length)
                        continue;

                    // Point must be on the minimap grid.
                    if (dx < 0 || dx >= width || dy < 0 || dy >= height)
                        continue;

                    dungeonEntries.Add((index, dx, dy));
                }
            }
            // If the file has "MapDialog" values.
            if (hasDialog)
            {
                if (dialogRows.Count != height)
                    return;
                foreach (var row in dialogRows)
                    if (row.Length != width)
                        return;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        _mapDialog[y, x] = dialogRows[y][x];
            }
            // If the file has "MapIcons" values.
            if (hasIcons)
            {
                Array.Clear(_mapIcons, 0, _mapIcons.Length);
                foreach (var e in iconEntries)
                    _mapIcons[e.X, e.Y] = e.Value;
            }
            // If the file has "DungeonPosition" values.
            if (hasDungeons)
            {
                foreach (var d in dungeonEntries)
                    _dungeonPosition[d.Index] = new Point(d.X, d.Y);
            }
        }

        public void UpdateRenderTarget()
        {
            // The map is drawn into this render target which is then drawn to the screen. This used to render
            // it at 144x144 making it impossible to make use of a high resolution texture. We now resize the
            // render target using the texture scale, then size it back down to keep the texture's detail.
            var renderScale = Math.Max(1, HdTexture.GetScale(Resources.SprMiniMap));

            if (_renderTarget == null || _renderScale != renderScale || _renderTarget.Width != _width * renderScale || _renderTarget.Height != _height * renderScale)
            {
                _renderTarget?.Dispose();
                _renderScale = renderScale;
                _renderTarget = new RenderTarget2D(Game1.Graphics.GraphicsDevice, _width * renderScale, _height * renderScale);
            }
        }

        public void DisposeRenderTargets()
        {
            try
            {
                _renderTarget?.Dispose(); 
                _renderTarget = null;
            }
            catch { }
        }

        private bool TileIsDiscovered(Point tilePosition)
        {
            if (tilePosition.X >= 0 && tilePosition.Y >= 0 &&
                tilePosition.X < Game1.GameManager.MapVisibility.GetLength(0) &&
                tilePosition.Y < Game1.GameManager.MapVisibility.GetLength(1) &&
                (_fullMap || Game1.GameManager.MapVisibility[tilePosition.X, tilePosition.Y]))
            {
                return true;
            }
            return false;
        }

        public void Update()
        {
            // Update player and selection animations.
            _animationPlayer.Update();
            _animationSelection.Update();

            // Update dungeon icons if enabled.
            if (GameSettings.MapTeleport > 0)
                for (int i = 0; i < 8; i++)
                    _animDungeons[i].Update();

            // Update the overworld teleporter icons if enabled.
            if (GameSettings.MapTeleport > 0)
                for (int i = 0; i < 4; i++)
                    _animTeleporters[i].Update();

            // Update the manbo's pond icon.
            _manboPond.Update();

            var mapIcon = _mapIcons[_selectionPosition.X, _selectionPosition.Y];

            // For owl icons we only show the icon if the owl key was already set.
            if (mapIcon == 4 && Game1.GameManager.SaveManager.GetString(_mapDialog[_selectionPosition.Y, _selectionPosition.X], "0") != "1")
                mapIcon = 0;

            // Show a large map icon if the tile contains one and it's discovered.
            if ((mapIcon != _shownSelection || (mapIcon != 0 && !IsSelected)) && !_iconAnimationRunning)
            {
                if (TileIsDiscovered(_selectionPosition) && mapIcon != 0 && _shownSelection == 0 && IsSelected)
                    PlayStartAnimation();
                else
                    PlayStopAnimation();
            }
            // update the icon run animation
            if (_iconAnimationRunning)
            {
                _animationCount += Game1.DeltaTime / 100f * _iconAnimationDirection;

                if (_animationCount >= Math.PI / 2)
                {
                    _iconAnimationRunning = false;
                    _animationCount = (float)(Math.PI / 2);
                }
                else if (_animationCount < 0)
                {
                    _iconAnimationRunning = false;
                    _shownSelection = 0;
                }

                _animationState = (float)Math.Sin(_animationCount);
            }
            if (!IsSelected)
                return;

            if (!Game1.GameManager.InGameOverlay.TextboxOverlay.IsOpen)
                UpdateInput();
        }

        private void UpdateInput()
        {
            if (ControlHandler.ButtonDown(CButtons.Left) || ControlHandler.ButtonDown(CButtons.Right) ||
                ControlHandler.ButtonDown(CButtons.Up) || ControlHandler.ButtonDown(CButtons.Down))
                _buttonDownCounter -= Game1.DeltaTime;
            else
                _buttonDownCounter = 225;

            if (ControlHandler.ButtonPressed(CButtons.Left) || (ControlHandler.ButtonDown(CButtons.Left) && _buttonDownCounter < 0))
            {
                _buttonDownCounter += 50;
                MoveSelection(_selectionPosition + new Point(-1, 0));
            }
            if (ControlHandler.ButtonPressed(CButtons.Right) || (ControlHandler.ButtonDown(CButtons.Right) && _buttonDownCounter < 0))
            {
                _buttonDownCounter += 50;
                MoveSelection(_selectionPosition + new Point(1, 0));
            }
            if (ControlHandler.ButtonPressed(CButtons.Up) || (ControlHandler.ButtonDown(CButtons.Up) && _buttonDownCounter < 0))
            {
                _buttonDownCounter += 50;
                MoveSelection(_selectionPosition + new Point(0, -1));
            }
            if (ControlHandler.ButtonPressed(CButtons.Down) || (ControlHandler.ButtonDown(CButtons.Down) && _buttonDownCounter < 0))
            {
                _buttonDownCounter += 50;
                MoveSelection(_selectionPosition + new Point(0, 1));
            }
            if (ControlHandler.ButtonPressed(ControlHandler.ConfirmButton))
            {
                if (0 <= _selectionPosition.X && _selectionPosition.X < _mapDialog.GetLength(1) &&
                    0 <= _selectionPosition.Y && _selectionPosition.Y < _mapDialog.GetLength(0) &&
                    TileIsDiscovered(_selectionPosition))
                {
                    Game1.GameManager.RunDialog(_mapDialog[_selectionPosition.Y, _selectionPosition.X]);
                }
                else
                {
                    Game1.AudioManager.PlaySoundEffect("D360-09-09");
                }
            }
            if (ControlHandler.ButtonPressed(CButtons.X))
            {
                // Teleporting is only allowed from the inventory map or the full map.
                var validMapState = Game1.GameManager.InGameOverlay.InventoryState || _fullMap;

                if (!validMapState ||
                    !Game1.GameManager.InGameOverlay.TryGetMapTeleportTarget(_selectionPosition, out var teleportPoint))
                    return;

                var body = MapManager.ObjLink.Body;

                Camera.SnapCameraTimer = 100f;

                Game1.GameManager.InGameOverlay.ToggleInventoryMap();
                Game1.GameManager.InGameOverlay.CloseOverlay();

                body.Position.Set(teleportPoint);
                body.Velocity = Vector3.Zero;
                body.VelocityTarget = Vector2.Zero;
                MapManager.ObjLink.Direction = 3;

                var followers = MapManager.ObjLink.Followers;
                if (followers.Count > 0)
                {
                    foreach (var follower in followers)
                    {
                        var followerPos = new Vector2(teleportPoint.X, teleportPoint.Y - 16);
                        follower.EntityPosition.Set(followerPos);
                        follower.SetFacingDirection(3);
                    }
                }

                var explosionAnimation = new ObjAnimator(MapManager.ObjLink.Map, (int)teleportPoint.X, (int)teleportPoint.Y - 8, Values.LayerTop, "Particles/pieceOfPowerExplosion", "run", true);
                MapManager.ObjLink.Map.Objects.SpawnObject(explosionAnimation);
                Game1.AudioManager.PlaySoundEffect("D360-27-1B");
                MapManager.ObjLink.ManboTeleport = false;
            }
        }

        public void PlayStartAnimation()
        {
            _iconPosition = new Point(8, 8);

            _iconPosition.X += _selectionPosition.X >= _mapIcons.GetLength(0) / 2 ? 8 : _mapIcons.GetLength(0) * 8 - _recIcon.Width - 8;
            _iconPosition.Y += _selectionPosition.Y >= _mapIcons.GetLength(1) / 2 ? _mapIcons.GetLength(1) * 8 - _recIcon.Height - 8 : 8;

            _animationCount = 0;
            _iconAnimationDirection = 1;
            _iconAnimationRunning = true;
            _shownSelection = _mapIcons[_selectionPosition.X, _selectionPosition.Y];
        }

        public void PlayStopAnimation()
        {
            _animationCount = (float)(Math.PI / 2);
            _iconAnimationDirection = -1;
            _iconAnimationRunning = true;
        }

        public void OnFocus()
        {
            _shownSelection = 0;
            _animationState = 0;

            if (Game1.GameManager.PlayerMapPosition != null)
                _selectionPosition = Game1.GameManager.PlayerMapPosition.Value;
        }

        public void MoveSelection(Point newPosition)
        {
            if (newPosition.X < 0)
                newPosition.X += Game1.GameManager.MapVisibility.GetLength(0);
            if (newPosition.X >= Game1.GameManager.MapVisibility.GetLength(0))
                newPosition.X -= Game1.GameManager.MapVisibility.GetLength(0);
            if (newPosition.Y < 0)
                newPosition.Y += Game1.GameManager.MapVisibility.GetLength(1);
            if (newPosition.Y >= Game1.GameManager.MapVisibility.GetLength(1))
                newPosition.Y -= Game1.GameManager.MapVisibility.GetLength(1);

            // Only move the selection if the new position is visible.
            if (TileIsDiscovered(newPosition) || Game1.GameManager.InGameOverlay.MapFreeNavigation)
            {
                Game1.AudioManager.PlaySoundEffect("D360-10-0A");
                _selectionPosition = newPosition;
                _animationSelection.Stop();
                _animationSelection.Play("idle");
            }
            // Play a "bump" sound and do not move the cursor.
            else
            {
                Game1.AudioManager.PlaySoundEffect("D360-09-09");
            }
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle drawPosition, Color color, Matrix? matrix = null)
        {
            if (_renderTarget == null)
                return;

            // Screen-space scale for rounded corners.
            Resources.RoundedCornerEffect.Parameters["scale"].SetValue(_scale);
            Resources.RoundedCornerEffect.Parameters["radius"].SetValue(2f);
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(_width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(_height);

            // Draw the render target with the shader.
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, Resources.RoundedCornerEffect, matrix);
            spriteBatch.Draw(_renderTarget, drawPosition.Location.ToVector2(), null, color, 0f, Vector2.Zero, _scale / _renderScale, SpriteEffects.None, 0f);
            spriteBatch.End();

            // Draw overlay icons.
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, matrix);

            if (Game1.GameManager.PlayerMapPosition != null)
            {
                var mapRectangle = new Point(drawPosition.X + _margin, drawPosition.Y + _margin);

                // Draw the dungeon icons if enabled.
                if (((GameSettings.MapTeleport == 1 || GameSettings.MapTeleport == 3) || (GameSettings.MapTeleport == 2 && MapManager.ObjLink.ManboTeleport)) && IsSelected && MapManager.ObjLink.Map.IsOverworld)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var hasInstrument = Game1.GameManager.GetItem("instrument" + i);
                        if (hasInstrument == null || hasInstrument.Count < 1)
                            continue;

                        Vector2 animLocation = new Vector2(
                            mapRectangle.X + (8 + _dungeonPosition[i].X * 8 + 1) * _scale, 
                            mapRectangle.Y + (8 + _dungeonPosition[i].Y * 8 + 1) * _scale);
                        _animDungeons[i].DrawBasic(spriteBatch, animLocation, color, _scale);
                    }
                }
                // Draw the overworld teleporter icons if discovered.
                if (((GameSettings.MapTeleport == 1 || GameSettings.MapTeleport == 3) || (GameSettings.MapTeleport == 2 && MapManager.ObjLink.ManboTeleport)) && IsSelected && MapManager.ObjLink.Map.IsOverworld)
                {
                    foreach (var entry in Game1.GameManager.InGameOverlay.TeleportMap)
                    {
                        // Only overworld teleporters use Level 100+.
                        if (entry.Value.Level < 100 || entry.Value.Level > 103)
                            continue;

                        int id = entry.Value.Level - 100;

                        // Guard against an out-of-range animator index.
                        if (id < 0 || id >= _animTeleporters.Length)
                            continue;

                        // Only draw the icon if this teleporter has been discovered.
                        if (Game1.GameManager.SaveManager.GetString("unlocked_teleporter_" + id) != "1")
                            continue;

                        Vector2 animLocation = new Vector2(
                            mapRectangle.X + (8 + entry.Key.X * 8 + 1) * _scale,
                            mapRectangle.Y + (8 + entry.Key.Y * 8 + 1) * _scale);
                        _animTeleporters[id].DrawBasic(spriteBatch, animLocation, color, _scale);
                    }
                }
                // Draw the icon for Manbo's Pond / Crazy Tracy's Health Spa.
                if (GameSettings.MapTeleport >= 2 && MapManager.ObjLink.ManboTeleport)
                {
                    Vector2 manboLocation = new Vector2(
                            mapRectangle.X + (8 + _manboPosition.X * 8 + 1) * _scale,
                            mapRectangle.Y + (8 + _manboPosition.Y * 8 + 1) * _scale);
                    _manboPond.DrawBasic(spriteBatch, manboLocation, color, _scale);
                }
                // Draw the player icon.
                var position = new Vector2(
                    mapRectangle.X + (8 + Game1.GameManager.PlayerMapPosition.Value.X * 8 + 2) * _scale,
                    mapRectangle.Y + (8 + Game1.GameManager.PlayerMapPosition.Value.Y * 8 + 2) * _scale);
                _animationPlayer.DrawBasic(spriteBatch, position, color, _scale);

                // Draw the selection icon.
                if (IsSelected)
                {
                    position = new Vector2(
                        mapRectangle.X + (8 + _selectionPosition.X * 8 + 1) * _scale,
                        mapRectangle.Y + (8 + _selectionPosition.Y * 8 + 1) * _scale);
                    _animationSelection.DrawBasic(spriteBatch, position, color, _scale);
                }
                // Draw the large selection icon on top of everything else.
                if (_shownSelection > 0)
                {
                    var iconPosition = new Point(
                        mapRectangle.X + (int)(_iconPosition.X * _scale),
                        mapRectangle.Y + (int)(_iconPosition.Y * _scale));
                    DrawIcon(spriteBatch, iconPosition, _shownSelection, _scale, _animationState);
                }
            }
            spriteBatch.End();
        }

        public void DrawRenderTarget(SpriteBatch spriteBatch)
        {
            // Ensure RT exists every time before we use it
            UpdateRenderTarget();

            // If it's still null then fail.
            if (_renderTarget == null)
                return;

            // Update render target.
            Game1.Graphics.GraphicsDevice.SetRenderTarget(_renderTarget);
            Game1.Graphics.GraphicsDevice.Clear(Color.Transparent);

            // Draw the map using a scaling matrix.
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, Matrix.CreateScale(_renderScale));
            DrawMap(spriteBatch);
            spriteBatch.End();

            // Note: OpenGL needs this to avoid weird state bugs.
            Game1.Graphics.GraphicsDevice.SetRenderTarget(null);
        }

        public void DrawMap(SpriteBatch spriteBatch)
        {
            var mapRectangle = new Point(_margin, _margin);

            // draw the map
            spriteBatch.Draw(Resources.SprMiniMap, new Rectangle(mapRectangle.X, mapRectangle.Y, _recMap.Width, _recMap.Height), _recMap, Color.White);

            // overlay the not discovered parts of the map
            if (!_fullMap)
            {
                for (var x = 0; x < 16; x++)
                {
                    for (var y = 0; y < 16; y++)
                    {
                        if (!Game1.GameManager.MapVisibility[x, y])
                            spriteBatch.Draw(Resources.SprMiniMap, new Rectangle(
                                mapRectangle.X + 8 + x * 8,
                                mapRectangle.Y + 8 + y * 8,
                                _recHide.Width, _recHide.Height), _recHide, Color.White);
                    }
                }
            }
        }

        public void DrawIcon(SpriteBatch spriteBatch, Point position, int icon, float scale, float animationPercentage)
        {
            var width = (int)(_recIcon.Width * scale * animationPercentage) / 2;
            var height = (int)(_recIcon.Height * scale * animationPercentage) / 2;
            var posX = position.X + (int)(_recIcon.Width * scale / 2) - width;
            var posY = position.Y + (int)(_recIcon.Height * scale / 2) - height;

            spriteBatch.Draw(Resources.SprMiniMap, new Rectangle(posX, posY, width * 2, height * 2), new Rectangle(_recIcon.X + _recIcon.Width * (icon - 1), _recIcon.Y, _recIcon.Width, _recIcon.Height), Color.White);
        }
    }
}
