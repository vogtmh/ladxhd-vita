using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Overlay
{
    public class DungeonOverlay
    {
        private RenderTarget2D _renderTarget;

        private Animator _animationPlayer = new Animator();

        private Rectangle _backgroundTop;
        private Rectangle _backgroundBottom;

        private static readonly Point DungeonPoint = new Point(6, 0);

        private Rectangle _mapRectangle = new Rectangle(DungeonPoint.X, DungeonPoint.Y, 12, 20);
        private Rectangle _compassRectangle = new Rectangle(DungeonPoint.X + 12, DungeonPoint.Y, 12, 20);
        private Rectangle _stonebreakRectangle = new Rectangle(DungeonPoint.X + 24, DungeonPoint.Y, 12, 20);
        private Rectangle _nightmareKeyPosition = new Rectangle(DungeonPoint.X + 36, DungeonPoint.Y, 12, 20);
        private Rectangle _smallKeyPosition = new Rectangle(DungeonPoint.X + 48, DungeonPoint.Y, 24, 20);

        private Point _recMap = new Point(2, 160);
        private Point _hintMap = new Point(2, 170);
        private Point _mapPosition = new Point(6, 24);

        private int _tileWidth = 8;
        private int _tileHeight = 8;

        private int _mapTile = 3;
        private int _undiscoveredTile = 4;

        private int _width;
        private int _height;

        private float _scale => Game1.GameManager.InGameOverlay.Scale;

        public DungeonOverlay(int width, int height)
        {
            _width = (int)(width * _scale);
            _height = (int)(height * _scale);

            _backgroundTop = new Rectangle(0, 0, width, 20);
            _backgroundBottom = new Rectangle(0, 20 + 2, width, height - 20 - 2);
        }

        public void ResolutionChanged()
        {
            _width = Game1.WindowWidth;
            _height = Game1.WindowHeight;
            UpdateRenderTarget();
        }

        public void UpdateRenderTarget()
        {
            if (_renderTarget == null || _renderTarget.Width != _width || _renderTarget.Height != _height)
            {
                _renderTarget?.Dispose();
                _renderTarget = new RenderTarget2D(Game1.Graphics.GraphicsDevice, _width, _height);
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
        public void Load()
        {
            _animationPlayer = AnimatorSaveLoad.LoadAnimator("dungeonPlayer");
            _animationPlayer.Play("idle");
        }

        public void OnFocus()
        {
            var level = 0;
            while (true)
            {
                var name = Game1.GameManager.MapManager.CurrentMap.LocationName + "_" + level;
                if (!Game1.GameManager.DungeonMaps.TryGetValue(name, out var normalMap) || normalMap == null)
                    break;

                level++;

                if (normalMap.Overrides == null)
                    continue;

                var dungeonMap = GetAlternativeMap(name) ?? normalMap;

                // check the override map and override unlocked tiles
                foreach (var map in normalMap.Overrides)
                {
                    if (Game1.GameManager.SaveManager.GetString(map.SaveKey, "0") == "1")
                        dungeonMap.Tiles[map.PosX, map.PosY].TileIndex = map.TileIndex;
                }
            }
        }

        public void Update()
        {
            if (!Game1.GameManager.MapManager.CurrentMap.IsDungeon)
                return;

            _animationPlayer.Update();
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle drawPosition, Color color)
        {
            if (_renderTarget == null || !Game1.GameManager.MapManager.CurrentMap.IsDungeon) return;
            var destRect = new Rectangle(drawPosition.X, drawPosition.Y, _renderTarget.Width, _renderTarget.Height);
            spriteBatch.Draw(_renderTarget, destRect, color);
        }

        public void DrawOnRenderTarget(SpriteBatch spriteBatch)
        {
            if (_renderTarget == null || !Game1.GameManager.MapManager.CurrentMap.IsDungeon)
                return;

            Game1.Graphics.GraphicsDevice.SetRenderTarget(_renderTarget);
            Game1.Graphics.GraphicsDevice.Clear(Color.Transparent);

            // Draw the background.
            spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, Resources.RoundedCornerEffect, Matrix.CreateScale(_scale));

            Resources.RoundedCornerEffect.Parameters["scale"].SetValue(_scale);
            Resources.RoundedCornerEffect.Parameters["radius"].SetValue(3f);

            // If the user used custom alpha, force that. Otherwise use the alpha set when the color as constructed.
            bool userCustomAlpha = Game1.GameManager.InGameOverlay.UserCustomAlpha;

            // Set the colors to be used by the inventory window.
            Color backColor = !userCustomAlpha && GameSettings.DisableUiBlur
                ? new Color(Game1.GameManager.InGameOverlay.InventoryBackgroundColorBot, 1.0f)
                : Game1.GameManager.InGameOverlay.InventoryBackgroundColorBot;

            // Draw the dungeon overlay windows.
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(_backgroundTop.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(_backgroundTop.Height);
            spriteBatch.Draw(Resources.SprWhite, _backgroundTop, backColor);
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(_backgroundBottom.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(_backgroundBottom.Height);
            spriteBatch.Draw(Resources.SprWhite, _backgroundBottom, backColor);

            if (Game1.GameManager.GetItem("dmap") == null)
                DrawBackground(spriteBatch, Point.Zero, new Rectangle(_mapRectangle.X + _mapRectangle.Width / 2, _mapRectangle.Bottom - 5, 4, 2), 1);
            if (Game1.GameManager.GetItem("compass") == null)
                DrawBackground(spriteBatch, Point.Zero, new Rectangle(_compassRectangle.X + _compassRectangle.Width / 2, _compassRectangle.Bottom - 5, 4, 2), 1);
            if (Game1.GameManager.GetItem("stonebeak") == null)
                DrawBackground(spriteBatch, Point.Zero, new Rectangle(_stonebreakRectangle.X + _stonebreakRectangle.Width / 2, _stonebreakRectangle.Bottom - 5, 4, 2), 1);
            if (Game1.GameManager.GetItem("nightmarekey") == null)
                DrawBackground(spriteBatch, Point.Zero, new Rectangle(_nightmareKeyPosition.X + _nightmareKeyPosition.Width / 2, _nightmareKeyPosition.Bottom - 5, 4, 2), 1);
            if (Game1.GameManager.GetItem("smallkey") == null)
                DrawBackground(spriteBatch, Point.Zero, new Rectangle(_smallKeyPosition.X + _smallKeyPosition.Width / 2, _smallKeyPosition.Bottom - 5, 4, 2), 1);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, Matrix.CreateScale(_scale));

            var offset = new Point(0, 0);

            ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("dmap"), offset, _mapRectangle, 1, Color.White);
            ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("compass"), offset, _compassRectangle, 1, Color.White);
            ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("stonebeak"), offset, _stonebreakRectangle, 1, Color.White);
            ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("nightmarekey"), offset, _nightmareKeyPosition, 1, Color.White);
            ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("smallkey"), offset, _smallKeyPosition, 1, Color.White);

            // Draw the dungeon map.
            var level = 0;
            var hasMap = Game1.GameManager.GetItem("dmap") != null;
            var hasCompass = Game1.GameManager.GetItem("compass") != null;

            while (true)
            {
                // does the map exist?
                var name = Game1.GameManager.MapManager.CurrentMap.LocationName + "_" + level;
                if (!Game1.GameManager.DungeonMaps.TryGetValue(name, out var normalMap))
                    break;

                var dungeonMap = GetAlternativeMap(name) ?? normalMap;

                var posX = _mapPosition.X + dungeonMap.OffsetX;
                var posY = _mapPosition.Y + dungeonMap.OffsetY;

                // draw the map
                for (var y = 0; y < dungeonMap.Tiles.GetLength(1); y++)
                {
                    for (var x = 0; x < dungeonMap.Tiles.GetLength(0); x++)
                    {
                        int tileIndex;
                        // use the discovery state from the normal map
                        if (!normalMap.Tiles[x, y].DiscoveryState && dungeonMap.Tiles[x, y].TileIndex > 4)
                            tileIndex = hasMap ? _mapTile : _undiscoveredTile;
                        else
                            tileIndex = dungeonMap.Tiles[x, y].TileIndex;

                        spriteBatch.Draw(Resources.SprMiniMap,
                            new Rectangle(
                                posX + x * _tileWidth, posY + y * _tileHeight,
                                _tileWidth, _tileHeight),
                            new Rectangle(
                                _recMap.X + (tileIndex - 1) * (_tileWidth + 2),
                                _recMap.Y, _tileWidth, _tileHeight), Color.White);

                        // draw the hints on the map if the player has a compass
                        if (hasCompass)
                        {
                            tileIndex = dungeonMap.Tiles[x, y].HintTileIndex;

                            if (tileIndex > 0)
                            {
                                // chest opened or boss defeated?
                                if (Game1.GameManager.SaveManager.GetString(dungeonMap.Tiles[x, y].HintKey) == "1")
                                    continue;
                                //tileIndex += 1;

                                spriteBatch.Draw(Resources.SprMiniMap,
                                    new Rectangle(
                                        posX + x * _tileWidth, posY + y * _tileHeight,
                                        _tileWidth, _tileHeight),
                                    new Rectangle(
                                        _hintMap.X + (tileIndex - 1) * (_tileWidth + 2),
                                        _hintMap.Y, _tileWidth, _tileHeight), Color.White);
                            }
                        }
                    }
                }
                // draw the position indicator
                if (Game1.GameManager.MapManager.CurrentMap.LocationFullName == name)
                {
                    var position = new Vector2(
                        posX + Game1.GameManager.PlayerDungeonPosition.X * 8 + 1,
                        posY + Game1.GameManager.PlayerDungeonPosition.Y * 8 + 1);
                    _animationPlayer.Draw(spriteBatch, position, Color.White);
                }

                level++;
            }

            spriteBatch.End();
        }

        private GameManager.MiniMap GetAlternativeMap(string name)
        {
            // this allows for map file switching by adding a postfix to the dungeon name
            var mapName = name + Game1.GameManager.SaveManager.GetString(name + "_map", "");
            Game1.GameManager.DungeonMaps.TryGetValue(mapName, out var altMap);

            return altMap;
        }

        private void DrawBackground(SpriteBatch spriteBatch, Point offset, Rectangle rectangle, float radius)
        {
            Resources.RoundedCornerEffect.Parameters["radius"].SetValue(radius);
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(rectangle.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(rectangle.Height);

            spriteBatch.Draw(Resources.SprWhite, new Rectangle(offset.X + rectangle.X, offset.Y + rectangle.Y, rectangle.Width, rectangle.Height), Color.Black * 0.25f);
        }
    }
}
