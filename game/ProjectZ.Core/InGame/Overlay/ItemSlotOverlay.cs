using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base.UI;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Overlay
{
    class ItemSlotOverlay
    {
        public static Rectangle RecItemselection = new Rectangle(6, 29, 30, 20);

        public static int DistX = 2;
        public static int DistY = 2;

        public Point ItemSlotPosition;
        private static Rectangle[] _itemSlots;
        private static UiRectangle[] _uiBackgroundBoxes;

        private bool? _lastSixButtonsState = null;

        HUDOverlay _hudOverlay => Game1.GameManager.InGameOverlay.InGameHud; 

        public ItemSlotOverlay()
        {
            UpdateButtonLayout();
        }

        private void UpdateButtonLayout()
        {
            // Don't need to update if the setting hasn't changed.
            if (_lastSixButtonsState == GameSettings.SixButtons)
                return;
            _lastSixButtonsState = GameSettings.SixButtons;

            if (GameSettings.SixButtons)
            {
                _itemSlots = new Rectangle[] 
                {
                    new Rectangle(RecItemselection.Width + DistX / 2 - RecItemselection.Width / 2, RecItemselection.Height * 2 + DistY * 2,
                        RecItemselection.Width, RecItemselection.Height / 2 * 2),
                    new Rectangle(RecItemselection.Width + DistX, RecItemselection.Height + DistY,
                        RecItemselection.Width, RecItemselection.Height / 2 * 2),
                    new Rectangle(0, RecItemselection.Height + DistY, 
                        RecItemselection.Width, RecItemselection.Height / 2 * 2), 
                    new Rectangle(RecItemselection.Width + DistX / 2 - RecItemselection.Width / 2, 0,
                        RecItemselection.Width, RecItemselection.Height / 2 * 2),
                    new Rectangle(0, -RecItemselection.Height - DistY,
                        RecItemselection.Width, RecItemselection.Height / 2 * 2),
                    new Rectangle(RecItemselection.Width + DistX, -RecItemselection.Height - DistY,
                        RecItemselection.Width, RecItemselection.Height / 2 * 2)
                };
            }
            else
            {
                _itemSlots = new Rectangle[] 
                {
                    new Rectangle(RecItemselection.Width + DistX / 2 - RecItemselection.Width / 2, RecItemselection.Height * 2 + DistY * 2, 
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(RecItemselection.Width + DistX, RecItemselection.Height + DistY,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(0, RecItemselection.Height + DistY,
                        RecItemselection.Width, RecItemselection.Height),
                    new Rectangle(RecItemselection.Width + DistX / 2 - RecItemselection.Width / 2, 0,
                        RecItemselection.Width, RecItemselection.Height)
                };
            }
            // Create the backgrounds for the item slots.
            CreateItemBackgrounds();
        }

        public void CreateItemBackgrounds()
        {
            if (_hudOverlay != null && _hudOverlay.ShowItems && _uiBackgroundBoxes?.Length != _itemSlots.Length)
            {
                _uiBackgroundBoxes = new UiRectangle[_itemSlots.Length];
                for (var i = 0; i < _uiBackgroundBoxes.Length; i++)
                {
                    _uiBackgroundBoxes[i] = new UiRectangle(_itemSlots[i], "itemBox" + i, Values.ScreenNameGame, Values.OverlayBackgroundColor, Values.OverlayBackgroundBlurColor, null) { Radius = Values.UiBackgroundRadius, IsHudElement = true };
                    Game1.UiManager.AddElement(_uiBackgroundBoxes[i]);
                }
            }
        }

        public void SetTransparency(float transparency)
        {
            if (_hudOverlay == null || !_hudOverlay.ShowItems) return;

            for (var i = 0; i < _itemSlots.Length; i++)
            {
                _uiBackgroundBoxes[i].BackgroundColor = _hudOverlay.ItemsBackgroundColor * transparency;
                _uiBackgroundBoxes[i].BlurColor = Values.OverlayBackgroundBlurColor * transparency;
            }
        }

        public static void Draw(SpriteBatch spriteBatch, bool drawItems, Point position, int scale, float transparency)
        {
            // If the player disabled drawing items.
            if (!drawItems)
                return;

            // Draw the item slots.
            for (var i = 0; i < _itemSlots.Length; i++)
            {
                var slotRectangle = new Rectangle(_itemSlots[i].X, _itemSlots[i].Y, RecItemselection.Width, RecItemselection.Height);
                ItemDrawHelper.DrawItemWithInfo(spriteBatch, Game1.GameManager.Equipment[i], position, slotRectangle, scale, Color.White * transparency);
            }
        }

        public void UpdatePositions(Rectangle uiWindow, Point offset, int scale)
        {
            UpdateButtonLayout();

            // Check if user disabled the items HUD.
            if (_hudOverlay == null || !_hudOverlay.ShowItems) 
                return;

            // Set the position of items based on whether they should be on right or left side of screen.
            ItemSlotPosition = new Point(GameSettings.ItemsOnRight 
                ? uiWindow.X + uiWindow.Width - (RecItemselection.Width * 2 + DistX * 2 + 16) * scale + _hudOverlay.ItemsOffsetX
                : uiWindow.X + 16 * scale + _hudOverlay.ItemsOffsetX, uiWindow.Y + uiWindow.Height - (RecItemselection.Height * 3 + DistY * 2 + 16) * scale + _hudOverlay.ItemsOffsetY);

            // Solves a race condition where "InGameHud" doesn't exist when these should be created.
            if (_uiBackgroundBoxes == null)
                CreateItemBackgrounds();

            // Update the background rectangles.
            for (var i = 0; i < _uiBackgroundBoxes.Length; i++)
            {
                _uiBackgroundBoxes[i].Rectangle = new Rectangle(
                    ItemSlotPosition.X + _itemSlots[i].X * scale + offset.X,
                    ItemSlotPosition.Y + _itemSlots[i].Y * scale + offset.Y,
                    _itemSlots[i].Width * scale, _itemSlots[i].Height * scale);
            }
        }
    }
}
