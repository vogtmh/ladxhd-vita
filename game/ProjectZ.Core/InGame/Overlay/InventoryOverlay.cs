using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Overlay
{
    class InventoryOverlay
    {
        private const int Margin = 6;

        private RenderTarget2D _renderTarget;

        private readonly Rectangle _background0 = new Rectangle(0, 0, 268, 24);
        private readonly Rectangle _background1 = new Rectangle(0, 26, 268, 182);

        private readonly Rectangle _keyRectangle = new Rectangle(94, 6, 20, 114);
        private readonly Rectangle[] _keyPositions = new Rectangle[5];

        private readonly Rectangle _relictsRectangle = new Rectangle(118, 6, 144, 22);
        private readonly Point _relictPosition = new Point(123, 9);
        private readonly Rectangle[] _relicOffsets = new Rectangle[8];

        private readonly DictAtlasEntry[] _ocarinaFaces = new DictAtlasEntry[3];

        private readonly Point _heartsPosition = new Point(6, 5);
        private readonly Point _rubeePosition;

        private readonly Rectangle _flipperRectangle;
        private readonly Rectangle _potionRectangle;

        private readonly Rectangle _tradeStuffRectangle = new Rectangle(6, 6, 84, 22);
        private Rectangle _tradeRectangle;
        private Rectangle _shellRectangle;
        private Rectangle _leafRectangle;

        private readonly Point _tunicPosition;
        private readonly Point _heartPiecePosition;

        private Point _itemSlotsPosition;

        private readonly Rectangle _tunicRectangle = new Rectangle(180, 31, 16, 15);
        private readonly Rectangle _tunicColorRectangle = new Rectangle(198, 37, 14, 10);
        private readonly Rectangle _heartPiecesRectangle = new Rectangle(4, 72, 16, 14);

        private int _itemSlotWidth;

        public static Rectangle RecItemselection = new Rectangle(0, 0, 30, 20);

        private Rectangle[] _itemSlots;

        private string[] _itemSlotString;

        private int _selectedItemSlot;

        private Point _itemRectangleSize;
        private readonly Point _itemRecMargin = new Point(0, 0);

        private Point _equipmentPosition;
        private readonly Rectangle _itemsRectangle = new Rectangle(6, 124, 108, 52);

        private int _width;
        private int _height;

        private float _selectionCounter;
        private const int SelectionTime = 125;
        private bool _selectionButtonPressed;

        private float _scale => Game1.GameManager.InGameOverlay.Scale;

        public InventoryOverlay(int width, int height)
        {
            _width = (int)(width * _scale);
            _height = (int)(height * _scale);

            _rubeePosition = new Point(width - ItemDrawHelper.RubeeSize.X - Margin, 10);

            var blockPosition = 97;
            _tunicPosition = new Point(blockPosition, 5);
            _heartPiecePosition = new Point(blockPosition += 34, 6);
            _flipperRectangle = new Rectangle(blockPosition += 18, 0, 12, _background0.Height);
            _potionRectangle = new Rectangle(blockPosition += 12, 0, 12, _background0.Height);

            // Key positions.
            for (var i = 0; i < 5; i++)
                _keyPositions[i] = new Rectangle(96, 12 + i * 21, 16, 16);

            // Instrument positions.
            for (var i = 0; i < 8; i++)
                _relicOffsets[i] = new Rectangle(i * 17, 0, 16, 16);

            _ocarinaFaces[0] = Resources.GetSprite("ocarina1");
            _ocarinaFaces[1] = Resources.GetSprite("ocarina2");
            _ocarinaFaces[2] = Resources.GetSprite("ocarina3");

            // Set the initial button layout which creates 4 or 6 equippable buttons.
            UpdateButtonLayout(GameSettings.SixButtons);
        }

        public void UpdateButtonLayout(bool sixButtons)
        {
            // If the setting was set to use six buttons.
            if (sixButtons)
            {
                var pos = new Point(8,2);
                _itemSlots = new Rectangle[] 
                {
                    new Rectangle(RecItemselection.Width + pos.X / 2 - RecItemselection.Width / 2,
                        RecItemselection.Height * 2 + pos.Y * 2,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(RecItemselection.Width + pos.X, RecItemselection.Height + pos.Y,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(0, RecItemselection.Height + pos.Y,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(RecItemselection.Width + pos.X / 2 - RecItemselection.Width / 2, 0,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(0, -RecItemselection.Height - pos.Y,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(RecItemselection.Width + pos.X, -RecItemselection.Height - pos.Y,
                        RecItemselection.Width, RecItemselection.Height)
                };
                _itemSlotWidth = 3;
                _itemSlotsPosition = new Point(14, 55);
                _itemRectangleSize = new Point(36, 26);
                _equipmentPosition = new Point(6, 124);
            }
            // The setting was set to use four buttons.
            else
            {
                var pos = new Point(8,5);
                _itemSlots = new Rectangle[] 
                {
                    new Rectangle(RecItemselection.Width + pos.X / 2 - RecItemselection.Width / 2,
                        RecItemselection.Height * 2 + pos.Y * 2,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(RecItemselection.Width + pos.X, RecItemselection.Height + pos.Y,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(0, RecItemselection.Height + pos.Y,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(RecItemselection.Width + pos.X / 2 - RecItemselection.Width / 2, 0,
                        RecItemselection.Width, RecItemselection.Height)
                };
                _itemSlotWidth = 4;
                _itemSlotsPosition = new Point(14, 41);
                _itemRectangleSize = new Point(27, 26);
                _equipmentPosition = new Point(6, 123);
            }
            // Update the number of equippable items.
            _itemSlotString = new string[_itemSlots.Length];
            Values.HandItemSlots = _itemSlots.Length;
        }

        public void ResolutionChanged()
        {
            _width = Game1.WindowWidth;
            _height = Game1.WindowHeight;
            UpdateRenderTarget();
        }

        public void UpdateRenderTarget()
        {
            // Keep render target at base logical size (no scaling).
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

        public void UpdateMenu()
        {
            for (var i = 0; i < _itemSlots.Length; i++)
            {
                if (ControlHandler.ButtonPressed((CButtons)((int)CButtons.A * Math.Pow(2, i))))
                {
                    Game1.AudioManager.PlaySoundEffect("D360-19-13");
                    Game1.GameManager.ChangeItem(i, _selectedItemSlot + Values.HandItemSlots);
                }
            }
            var selectionOffset = 0;

            var direction = ControlHandler.GetMoveVector2();
            if (direction.Length() > 0)
            {
                _selectionCounter -= Game1.DeltaTime;
                if (_selectionCounter <= 0 || !_selectionButtonPressed)
                {
                    _selectionCounter += SelectionTime;

                    var dir = AnimationHelper.GetDirection(direction);
                    if (dir == 0)
                        selectionOffset -= 1;
                    else if (dir == 1)
                        selectionOffset -= _itemSlotWidth;
                    else if (dir == 2)
                        selectionOffset += 1;
                    else if (dir == 3)
                        selectionOffset += _itemSlotWidth;

                    Game1.AudioManager.PlaySoundEffect("D360-10-0A");
                }
                _selectionButtonPressed = true;
            }
            else
            {
                _selectionButtonPressed = false;
                _selectionCounter = SelectionTime;
            }

            // update the selected ocarina song
            var selectedItem = Game1.GameManager.Equipment[Values.HandItemSlots + _selectedItemSlot];

            if (selectedItem != null && selectedItem.Name == "ocarina")
            {
                if ((selectionOffset == -1 || selectionOffset == 1) &&
                    MoveOcarinaSelection(selectionOffset))
                    selectionOffset = 0;
            }

            _selectedItemSlot += selectionOffset;

            var slots = GameManager.EquipmentSlots - _itemSlots.Length;
            if (_selectedItemSlot < 0)
                _selectedItemSlot += slots;
            if (_selectedItemSlot >= slots)
                _selectedItemSlot = _selectedItemSlot % slots;
        }

        private bool MoveOcarinaSelection(int direction)
        {
            var previousSong = Game1.GameManager.SelectedOcarinaSong;

            for (var i = 0; i < Game1.GameManager.Equipment.Length; i++)
            {
                Game1.GameManager.SelectedOcarinaSong += direction;
                if (Game1.GameManager.SelectedOcarinaSong < 0 ||
                    Game1.GameManager.SelectedOcarinaSong >= _ocarinaFaces.Length)
                {
                    Game1.GameManager.SelectedOcarinaSong = previousSong;
                    return false;
                }
                if (Game1.GameManager.SelectedOcarinaSong != -1 &&
                    Game1.GameManager.OcarinaSongs[Game1.GameManager.SelectedOcarinaSong] == 1)
                    return Game1.GameManager.SelectedOcarinaSong != previousSong;
            }
            Game1.GameManager.SelectedOcarinaSong = -1;
            return false;
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle drawPosition, Color color)
        {
            if (_renderTarget == null) return;
            var destRect = new Rectangle(drawPosition.X, drawPosition.Y, _renderTarget.Width, _renderTarget.Height);
            spriteBatch.Draw(_renderTarget, destRect, color);
        }

        public void DrawRT(SpriteBatch spriteBatch)
        {
            var device = Game1.Graphics.GraphicsDevice;
            device.SetRenderTarget(_renderTarget);
            device.Clear(Color.Transparent);

            Resources.RoundedCornerEffect.Parameters["scale"].SetValue(1f * _scale);
            Resources.RoundedCornerEffect.Parameters["radius"].SetValue(3f);

            // Draw main background.
            spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, Resources.RoundedCornerEffect, Matrix.CreateScale(_scale));

            // Do not use user custom alpha if not defined or if "Disable UI Blur" is enabled.
            bool skipCustomAlpha = !Game1.GameManager.InGameOverlay.UserCustomAlpha || GameSettings.DisableUiBlur;

            // Set the colors to be used by the inventory window.
            Color backColorTop = skipCustomAlpha
                ? new Color(Game1.GameManager.InGameOverlay.InventoryBackgroundColorTop, 1.0f)
                : Game1.GameManager.InGameOverlay.InventoryBackgroundColorTop;
            Color backColorBot = skipCustomAlpha
                ? new Color(Game1.GameManager.InGameOverlay.InventoryBackgroundColorBot, 1.0f)
                : Game1.GameManager.InGameOverlay.InventoryBackgroundColorBot;

            // Draw the inventory window.
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(_background0.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(_background0.Height);
            spriteBatch.Draw(Resources.SprWhite, _background0, backColorTop);
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(_background1.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(_background1.Height);
            spriteBatch.Draw(Resources.SprWhite, _background1, backColorBot);

            spriteBatch.End();

            // Draw item backgrounds.
            spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, Resources.RoundedCornerEffect, Matrix.CreateScale(_scale));

            var offset = new Point(_background1.X, _background1.Y);

            for (var i = 0; i < _itemSlots.Length; i++)
                DrawBackground(spriteBatch, offset + _itemSlotsPosition, _itemSlots[i]);

            DrawBackground(spriteBatch, offset, _keyRectangle);
            DrawBackground(spriteBatch, offset, _relictsRectangle);
            DrawBackground(spriteBatch, offset, _itemsRectangle);
            DrawBackground(spriteBatch, offset, _tradeStuffRectangle);

            // Draw item selection box.
            var selectionPosition = new Point(
                (_itemsRectangle.X + _selectedItemSlot % _itemSlotWidth * (_itemRectangleSize.X + _itemRecMargin.X)),
                (_itemsRectangle.Y + _selectedItemSlot / _itemSlotWidth * (_itemRectangleSize.Y + _itemRecMargin.Y)));
            DrawBackground(spriteBatch, offset + selectionPosition, new Rectangle(0, 0, _itemRectangleSize.X, _itemRectangleSize.Y));

            // Draw missing items/equipment markers.
            for (var i = 0; i < Game1.GameManager.Equipment.Length - Values.HandItemSlots; i++)
            {
                var slotRectangle = new Rectangle(
                    i % _itemSlotWidth * (_itemRectangleSize.X + _itemRecMargin.X) + _itemRectangleSize.X / 2 - 2,
                    i / _itemSlotWidth * (_itemRectangleSize.Y + _itemRecMargin.Y) + _itemRectangleSize.Y - 8, 4, 2);

                if (Game1.GameManager.Equipment[Values.HandItemSlots + i] == null)
                    DrawBackground(spriteBatch, offset + _equipmentPosition, slotRectangle, 1);
            }

            // Key background dots.
            for (var i = 0; i < 5; i++)
            {
                var slotRectangle = new Rectangle(
                    _keyPositions[i].X + _keyPositions[i].Width / 2 - 2,
                    _keyPositions[i].Y + _keyPositions[i].Height - 2, 4, 2);

                var itemKey = Game1.GameManager.GetItem("dkey" + (i + 1));
                if (itemKey == null)
                    DrawBackground(spriteBatch, offset, slotRectangle, 1);
            }

            // Relic missing dots.
            for (var i = 0; i < _relicOffsets.Length; i++)
            {
                var name = "instrument" + i;
                var hasItem = Game1.GameManager.GetItem(name) != null;

                if (!hasItem)
                {
                    var position = new Point(_relictPosition.X + _relicOffsets[i].X + _relicOffsets[i].Width / 2 - 2,
                                             _relictPosition.Y + _relicOffsets[i].Bottom - 2);
                    DrawBackground(spriteBatch, offset + position, new Rectangle(0, 0, 4, 2), 1);
                }
            }
            spriteBatch.End();

            // Draw icons, hearts, etc.
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, Matrix.CreateScale(_scale));

            var heartOffset = new Point(0, Game1.GameManager.MaxHearts > 7 ? 0 : 4);
            ItemDrawHelper.DrawHearts(spriteBatch, _heartsPosition + heartOffset, 1, Color.White);

            DrawTunic(spriteBatch, _tunicPosition);
            DrawHeartContainer(spriteBatch, _heartPiecePosition);
            ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("flippers"), Point.Zero, _flipperRectangle, 1, Color.White);
            ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("potion"), Point.Zero, _potionRectangle, 1, Color.White);
            ItemDrawHelper.DrawRubee(spriteBatch, _rubeePosition, 1, Color.Black);

            var offsetBottom = new Point(_background1.X, _background1.Y);

            // Draw trade/special items.
            {
                var width = 0;
                var hasTradeItem = false;
                for (var i = 0; i < 15; i++)
                {
                    hasTradeItem = Game1.GameManager.GetItem("trade" + i) != null;
                    if (hasTradeItem)
                    {
                        width += 28;
                        break;
                    }
                }
                var itemShell = Game1.GameManager.GetItem("shell");
                var itemLeaf = Game1.GameManager.GetItem("goldLeaf");
                if (itemShell != null) width += 28;
                if (itemLeaf != null) width += 28;

                var posX = _tradeStuffRectangle.Width / 2 - width / 2;
                _tradeRectangle = new Rectangle(6 + posX, 6, 28, 22);

                if (hasTradeItem) posX += 28;
                _shellRectangle = new Rectangle(6 + posX, 6, 28, 22);

                if (itemShell != null) posX += 28;
                _leafRectangle = new Rectangle(6 + posX, 6, 28, 22);

                DrawTradeItem(spriteBatch, offsetBottom, 1);
                ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("shell"), offsetBottom, _shellRectangle, 1, Color.White);
                ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("goldLeaf"), offsetBottom, _leafRectangle, 1, Color.White, !GameSettings.ClassicSprites);
            }
            DrawEquipment(spriteBatch, offsetBottom + _equipmentPosition);

            // Draw slot labels and items.
            for (int y = 0; y < _itemSlots.Length; y++)
                _itemSlotString[y] = ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, y];

            for (var i = 0; i < _itemSlots.Length; i++)
            {
                ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.Equipment[i], offsetBottom + _itemSlotsPosition, _itemSlots[i], 1, Color.White);
                DrawHelper.DrawString(spriteBatch, _itemSlotString[i], new Vector2(offsetBottom.X + _itemSlotsPosition.X + _itemSlots[i].Right - 4, offsetBottom.Y + _itemSlotsPosition.Y + _itemSlots[i].Bottom - 5), Color.Black);
            }
            for (var i = 0; i < 5; i++)
                ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.GetItem("dkey" + (i + 1)), offsetBottom, _keyPositions[i], 1, Color.White);

            DrawRelicts(spriteBatch, offsetBottom + _relictPosition);

            spriteBatch.End();

            // Reset render targets.
            device.SetRenderTarget(null);
        }

        private void DrawBackground(SpriteBatch spriteBatch, Point offset, Rectangle rectangle, float radius = 3f)
        {
            Resources.RoundedCornerEffect.Parameters["radius"].SetValue(radius);
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(rectangle.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(rectangle.Height);

            spriteBatch.Draw(Resources.SprWhite, new Rectangle(offset.X + rectangle.X, offset.Y + rectangle.Y, rectangle.Width, rectangle.Height), Color.Black * 0.15f);
        }

        public void DrawEquipment(SpriteBatch spriteBatch, Point drawPosition)
        {
            // draw the collected items
            for (var i = 0; i < Game1.GameManager.Equipment.Length - Values.HandItemSlots; i++)
            {
                var slotRectangle = new Rectangle(
                    i % _itemSlotWidth * (_itemRectangleSize.X + _itemRecMargin.X),
                    i / _itemSlotWidth * (_itemRectangleSize.Y + _itemRecMargin.Y),
                    _itemRectangleSize.X, 
                    _itemRectangleSize.Y);

                // draw the item
                var itemIndex = i + Values.HandItemSlots;
                var offsetY = _selectedItemSlot == i ? -1 : 0;

                if (_selectedItemSlot == i &&
                    Game1.GameManager.Equipment[itemIndex] != null &&
                    Game1.GameManager.Equipment[itemIndex].Name == "ocarina")
                {
                    var hasSong = false;
                    for (var j = 0; j < Game1.GameManager.OcarinaSongs.Length; j++)
                        if (Game1.GameManager.OcarinaSongs[j] != 0)
                        {
                            hasSong = true;
                            break;
                        }

                    if (hasSong)
                        continue;
                }
                ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.Equipment[itemIndex], new Point(drawPosition.X, drawPosition.Y + offsetY + 1), slotRectangle, 1, Color.White);
            }

            // Draw the selected ocarina song.
            var selectedItem = Game1.GameManager.Equipment[Values.HandItemSlots + _selectedItemSlot];
            if (selectedItem != null && selectedItem.Name == "ocarina")
            {
                var selectedSong = Game1.GameManager.SelectedOcarinaSong;
                if (selectedSong != -1)
                {
                    var hasSong = Game1.GameManager.OcarinaSongs[selectedSong] == 1;
                    var position = new Vector2(
                        drawPosition.X + (_selectedItemSlot % _itemSlotWidth * (_itemRectangleSize.X + _itemRecMargin.X)) +
                        _itemRectangleSize.X / 2 - _ocarinaFaces[selectedSong].ScaledRectangle.Width / 2,
                        drawPosition.Y + (_selectedItemSlot / _itemSlotWidth * (_itemRectangleSize.Y + _itemRecMargin.Y)) +
                        _itemRectangleSize.Y / 2 - _ocarinaFaces[selectedSong].ScaledRectangle.Height / 2);

                    DrawHelper.DrawNormalized(spriteBatch, _ocarinaFaces[selectedSong], position, hasSong ? Color.White : Color.Gray);
                }
            }
        }

        public void DrawRelicts(SpriteBatch spriteBatch, Point drawPosition)
        {
            // draw the relicts
            for (var i = 0; i < _relicOffsets.Length; i++)
            {
                var name = "instrument" + i;
                var hasItem = Game1.GameManager.GetItem(name) != null;
                var item = Game1.GameManager.ItemManager[name];

                if (hasItem)
                    ItemDrawHelper.DrawInstrument(spriteBatch, item.Sprite, new Vector2(
                        drawPosition.X + _relicOffsets[i].X, drawPosition.Y + _relicOffsets[i].Y));
            }
        }

        public void DrawHeartContainer(SpriteBatch spriteBatch, Point drawPosition)
        {
            // heartMeter
            var item = Game1.GameManager.GetItem("heartMeter");
            var count = 0;

            if (item != null)
                count = item.Count;

            // draw the heart container
            spriteBatch.Draw(Resources.SprItem, new Rectangle(
                    drawPosition.X, drawPosition.Y, _heartPiecesRectangle.Width, _heartPiecesRectangle.Height),
                new Rectangle(_heartPiecesRectangle.X + (_heartPiecesRectangle.Width + 2) * count,
                    _heartPiecesRectangle.Y, _heartPiecesRectangle.Width, _heartPiecesRectangle.Height), Color.White);
        }

        public void DrawTradeItem(SpriteBatch spriteBatch, Point drawPosition, int scale)
        {
            // draw the current trade item
            for (var i = 0; i < 15; i++)
            {
                var hasItem = Game1.GameManager.GetItem("trade" + i) != null;

                if (!hasItem)
                    continue;

                // draw the key
                DrawHelper.DrawCenter(spriteBatch, Resources.SprItem,
                    drawPosition, _tradeRectangle, Game1.GameManager.ItemManager["trade" + i].SourceRectangle.Value, scale);

                break;
            }
        }

        public void DrawTunic(SpriteBatch spriteBatch, Point drawPosition)
        {
            spriteBatch.Draw(Resources.SprItem, new Rectangle(
                    drawPosition.X,
                    drawPosition.Y + 2,
                    _tunicColorRectangle.Width, _tunicColorRectangle.Height),
                new Rectangle(
                    _tunicColorRectangle.X,
                    _tunicColorRectangle.Y + Game1.GameManager.CloakType * (_tunicColorRectangle.Height + 1),
                    _tunicColorRectangle.Width, _tunicColorRectangle.Height), Color.White);

            var skirtPosition = new Point(drawPosition.X + _tunicColorRectangle.Width + 1, drawPosition.Y);

            spriteBatch.Draw(Resources.SprItem, new Rectangle(
                skirtPosition.X, skirtPosition.Y,
                _tunicRectangle.Width, _tunicRectangle.Height), _tunicRectangle, Color.White);

            spriteBatch.Draw(Resources.SprItem, new Rectangle(
                    skirtPosition.X, skirtPosition.Y,
                    _tunicRectangle.Width, _tunicRectangle.Height),
                new Rectangle(_tunicRectangle.X, _tunicRectangle.Y + _tunicRectangle.Height,
                    _tunicRectangle.Width, _tunicRectangle.Height), Game1.GameManager.InGameOverlay.InventoryTunicColors[Game1.GameManager.CloakType]);
        }
    }
}
