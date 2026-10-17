using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base.UI;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Overlay
{
    public class HUDOverlay
    {
        private readonly ItemSlotOverlay _itemSlotOverlay = new ItemSlotOverlay();

        private readonly UiRectangle _heartBackground;
        private readonly UiRectangle _rupeeBackground;
        private readonly UiRectangle _keyBackground;

        private readonly DictAtlasEntry _saveIcon;

        private Rectangle _gameUiWindow;

        private Point _heartPosition;
        private Point _rubeePosition;
        private Vector2 _saveIconPosition;
        private Point _keyPosition;

        private const int FadeOffsetBackground = 10;
        private const int FadeOffset = 13;

        private const int SaveIconTime = 1000;
        private float _saveIconTransparency;
        private float _saveIconCounter;

        private int _itemsScale;
        private int _heartScale;
        private int _rupeeScale;
        private int _keysScale;
        private int _siconScale;

        public Color ItemsBackgroundColor;
        public Color HeartBackgroundColor;
        public Color RupeeBackgroundColor;
        public Color KeysBackgroundColor;

        // Values configurable via lahdmod.
        private bool  custom_items_show     = true;
        private int   custom_items_scale    = 0;
        private int   custom_items_offsetx  = 0;
        private int   custom_items_offsety  = 0;
        private int   custom_items_red      = 255;
        private int   custom_items_grn      = 255;
        private int   custom_items_blu      = 190;
        private float custom_items_alpha    = 0.65f;
        private bool  custom_heart_show     = true;
        private int   custom_heart_scale    = 0;
        private int   custom_heart_offsetx  = 0;
        private int   custom_heart_offsety  = 0;
        private int   custom_heart_red      = 255;
        private int   custom_heart_grn      = 255;
        private int   custom_heart_blu      = 190;
        private float custom_heart_alpha    = 0.65f;
        private bool  custom_rupee_show     = true;
        private int   custom_rupee_scale    = 0;
        private int   custom_rupee_offsetx  = 0;
        private int   custom_rupee_offsety  = 0;
        private int   custom_rupee_red      = 255;
        private int   custom_rupee_grn      = 255;
        private int   custom_rupee_blu      = 190;
        private float custom_rupee_alpha    = 0.65f;
        private bool  custom_keys_show      = true;
        private int   custom_keys_scale     = 0;
        private int   custom_keys_offsetx   = 0;
        private int   custom_keys_offsety   = 0;
        private int   custom_keys_red       = 255;
        private int   custom_keys_grn       = 255;
        private int   custom_keys_blu       = 190;
        private float custom_keys_alpha     = 0.65f;
        private bool  custom_sicon_show     = true;
        private int   custom_sicon_scale    = 0;
        private int   custom_sicon_offsetx  = 0;
        private int   custom_sicon_offsety  = 0;

        // Public lahdmod values that can be passed to ItemSlotOverlay.
        public bool ShowItems => custom_items_show;
        public int  ItemsOffsetX => custom_items_offsetx;
        public int  ItemsOffsetY => custom_items_offsety;

        public HUDOverlay()
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "HUDOverlay.lahdmod");
            ModFile.Parse(modFile, this);

            // Assemble the colors so they can be referenced.
            ItemsBackgroundColor = new Color(custom_items_red, custom_items_grn, custom_items_blu) * custom_items_alpha;
            HeartBackgroundColor = new Color(custom_heart_red, custom_heart_grn, custom_heart_blu) * custom_heart_alpha;
            RupeeBackgroundColor = new Color(custom_rupee_red, custom_rupee_grn, custom_rupee_blu) * custom_rupee_alpha;
            KeysBackgroundColor  = new Color(custom_keys_red,  custom_keys_grn,  custom_keys_blu)  * custom_keys_alpha;

            // Create the hearts overlay if enabled.
            if (custom_heart_show)
            {
                _heartBackground = new UiRectangle(Rectangle.Empty, "heart", Values.ScreenNameGame, Values.OverlayBackgroundColor, Values.OverlayBackgroundBlurColor, null) { Radius = Values.UiBackgroundRadius, IsHudElement = true };
                Game1.UiManager.AddElement(_heartBackground);
            }
            // Create the rupees overlay if enabled.
            if (custom_rupee_show)
            {
                _rupeeBackground = new UiRectangle(Rectangle.Empty, "rupee", Values.ScreenNameGame, Values.OverlayBackgroundColor, Values.OverlayBackgroundBlurColor, null) { Radius = Values.UiBackgroundRadius, IsHudElement = true };
                Game1.UiManager.AddElement(_rupeeBackground);
            }
            // Create the keys overlay if enabled.
            if (custom_keys_show)
            {
                _keyBackground = new UiRectangle(Rectangle.Empty, "keys", Values.ScreenNameGame, Values.OverlayBackgroundColor, Values.OverlayBackgroundBlurColor, null) { Radius = Values.UiBackgroundRadius, IsHudElement = true };
                Game1.UiManager.AddElement(_keyBackground);
            }
            // Get the save icon sprite if enabled.
            if (custom_sicon_show)
            {
                _saveIcon = Resources.GetSprite("save_icon");
            }
        }

        public void ResolutionChange()
        {
            // Load a custom scale if defined otherwise use the UI scale.
            _itemsScale = custom_items_scale == 0 ? Game1.UiScale : custom_items_scale;
            _heartScale = custom_heart_scale == 0 ? Game1.UiScale : custom_heart_scale;
            _rupeeScale = custom_rupee_scale == 0 ? Game1.UiScale : custom_rupee_scale;
            _keysScale  = custom_keys_scale  == 0 ? Game1.UiScale : custom_keys_scale;
            _siconScale = custom_sicon_scale == 0 ? Game1.UiScale : custom_sicon_scale;
        }

        public void Update(float fadePercentage, float transparency)
        {
            _saveIconCounter -= Game1.DeltaTime;
            if (_saveIconCounter < 0)
                _saveIconCounter = 0;
            _saveIconTransparency = Math.Min(Math.Clamp(_saveIconCounter / 100, 0, 1), Math.Clamp((SaveIconTime - _saveIconCounter) / 100, 0, 1));

            var scale = Math.Min(Game1.WindowWidth / (float)Values.MinWidth, Game1.WindowHeight / (float)Values.MinHeight);

            _gameUiWindow.Width = (int)(Values.MinWidth * scale);
            _gameUiWindow.Height = (int)(Values.MinHeight * scale);

            if (Game1.PlatformPresentation.UseFullWindowHud)
            {
                _gameUiWindow.X = 0;
                _gameUiWindow.Y = 0;
                _gameUiWindow.Width = Game1.WindowWidth;
                _gameUiWindow.Height = Game1.WindowHeight;
            }
            else
            {
                var ar = MathHelper.Clamp(Game1.WindowWidth / (float)Game1.WindowHeight, 1, 2);
                _gameUiWindow.Width = MathHelper.Clamp((int)(Game1.WindowHeight * ar), 0, Game1.WindowWidth);
                _gameUiWindow.Height = MathHelper.Clamp((int)(Game1.WindowWidth / ar), 0, Game1.WindowHeight);
                _gameUiWindow.X = Game1.WindowWidth / 2 - _gameUiWindow.Width / 2;
                _gameUiWindow.Y = Game1.WindowHeight / 2 - _gameUiWindow.Height / 2;
            }

            if (custom_heart_show)
            {
                _heartPosition = new Point(_gameUiWindow.X + 16 * Game1.UiScale + custom_heart_offsetx, _gameUiWindow.Y + 16 * Game1.UiScale + custom_heart_offsety);
                _heartBackground.Rectangle = ItemDrawHelper.GetHeartRectangle(_heartPosition, _heartScale);
                _heartBackground.Rectangle.X -= (int)(fadePercentage * FadeOffsetBackground * _heartScale);
                _heartBackground.BackgroundColor = RupeeBackgroundColor * transparency;
                _heartBackground.BlurColor = Values.OverlayBackgroundBlurColor * transparency;
            }
            if (custom_rupee_show)
            {
                _rubeePosition = new Point(_gameUiWindow.X + _gameUiWindow.Width - ItemDrawHelper.RubeeSize.X * _rupeeScale - 16 * Game1.UiScale + custom_rupee_offsetx, _gameUiWindow.Y + 16 * Game1.UiScale + custom_rupee_offsety);
                _rupeeBackground.Rectangle = ItemDrawHelper.GetRubeeRectangle(new Point(_rubeePosition.X, _rubeePosition.Y), _rupeeScale);
                _rupeeBackground.Rectangle.X += (int)(fadePercentage * FadeOffsetBackground * _rupeeScale);
                _rupeeBackground.BackgroundColor = HeartBackgroundColor * transparency;
                _rupeeBackground.BlurColor = Values.OverlayBackgroundBlurColor * transparency;
            }
            if (custom_keys_show)
            {
                _keyPosition = new Point(_gameUiWindow.X + _gameUiWindow.Width - ItemDrawHelper.KeySize.X * _keysScale - 16 * Game1.UiScale + custom_keys_offsetx, _gameUiWindow.Y + 16 * 2 * _rupeeScale + custom_keys_offsety);
                _keyBackground.Rectangle = ItemDrawHelper.GetKeyRectangle(new Point(_keyPosition.X, _keyPosition.Y), _keysScale);
                _keyBackground.Rectangle.X += (int)(fadePercentage * FadeOffsetBackground * _keysScale);
                if (Game1.GameManager.GetItem("smallkey") is null)
                {
                    _keyBackground.BackgroundColor = Values.OverlayBackgroundColor * 0.0f;
                    _keyBackground.BlurColor = Values.OverlayBackgroundBlurColor * 0.0f;
                }
                else
                {
                    _keyBackground.BackgroundColor = KeysBackgroundColor * transparency ;
                    _keyBackground.BlurColor = Values.OverlayBackgroundBlurColor * transparency;
                }
            }
            // Update overlay position.
            int direction = GameSettings.ItemsOnRight ? 1 : -1;
            _itemSlotOverlay.UpdatePositions(_gameUiWindow, new Point(direction * (int)(fadePercentage * FadeOffsetBackground * Game1.UiScale), 0), _itemsScale);

            // Save icon position.
            if (custom_sicon_show)
            {
                _saveIconPosition = new Vector2(GameSettings.ItemsOnRight 
                    ? _gameUiWindow.X + _saveIcon.SourceRectangle.Width * Game1.UiScale + custom_sicon_offsetx
                    : _gameUiWindow.X + _gameUiWindow.Width - _saveIcon.SourceRectangle.Width * Game1.UiScale - 16 * _siconScale + custom_sicon_offsetx,
                    _gameUiWindow.Y + _gameUiWindow.Height - _saveIcon.SourceRectangle.Height * Game1.UiScale - 16 * _siconScale + custom_sicon_offsety);
            }
            _itemSlotOverlay.SetTransparency(transparency);
        }

        public void DrawTop(SpriteBatch spriteBatch, float fadePercentage, float transparency)
        {
            if (UiManager.HideOverlay) { return; }

            // Draw the item slots sprites & rectangles.
            if (custom_items_show)
                ItemSlotOverlay.Draw(spriteBatch, custom_items_show, _itemSlotOverlay.ItemSlotPosition + new Point(GameSettings.ItemsOnRight ? 1 : -1 * (int)(fadePercentage * FadeOffset * _itemsScale), 0), _itemsScale, transparency);

            // Draw dungeon keys sprites and rectangle.
            if (custom_keys_show)
                ItemDrawHelper.DrawSmallKeys(spriteBatch, _keyPosition + new Point((int)(fadePercentage * FadeOffset * _keysScale), 0), _keysScale, Color.White * transparency);

            // Draw the rupees sprites and rectangle.
            if (custom_rupee_show)
                ItemDrawHelper.DrawRubee(spriteBatch, _rubeePosition + new Point((int)(fadePercentage * FadeOffset * _rupeeScale), 0), _rupeeScale, Color.Black * transparency);

            // Draw the hearts sprites and rectangle.
            if (custom_heart_show)
                ItemDrawHelper.DrawHearts(spriteBatch, _heartPosition - new Point((int)(fadePercentage * FadeOffset * _heartScale), 0), _heartScale, Color.White * transparency);
        }

        public void ShowSaveIcon()
        {
            // When called shows the icon for 1 second.
            _saveIconCounter = SaveIconTime;
        }

        public void DrawSaveIcon(SpriteBatch spriteBatch, bool blurEnabled)
        {
            // Check if the icon should be drawn.
            if (custom_sicon_show)
            {
                // If the blurring effect is enabled draw the background and save icon.
                if (blurEnabled)
                {
                    Resources.RoundedCornerBlurEffect.Parameters["blurColor"].SetValue((Values.OverlayBackgroundBlurColor * _saveIconTransparency).ToVector4());
                    DrawHelper.DrawNormalized(spriteBatch, _saveIcon.Texture, _saveIconPosition, _saveIcon.ScaledRectangle, Values.OverlayBackgroundColor * _saveIconTransparency, _siconScale);
                }
                // If the blurring effect is disabled just draw the icon.
                else
                {
                    DrawHelper.DrawNormalized(spriteBatch, _saveIcon.Texture, _saveIconPosition, _saveIcon.ScaledRectangle, UiRectangle.OpaqueHudColor * _saveIconTransparency, _siconScale);
                }
            }
        }
    }
}
