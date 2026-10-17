using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Pages;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Screens
{
    public class MenuScreen : Screen
    {
        private Matrix _animationMatrix => Game1.GetMatrix * Matrix.CreateScale(Game1.UiScale);
        private Animator _linkAnimation = new Animator();
        private Texture2D _sprBackground;
        private Rectangle _menuRectangle;

        private int _menuWidth;
        private int _menuHeight;
        private int _backgroundWidth;

        private int _leftBar;
        private int _rightBar;
        private int _topBar;
        private int _bottomBar;
        private int _posX;

        static float _scale => Game1.UiPageManager.MenuScale;

        public MenuScreen(string screenId) : base(screenId) { }

        public override void Load(ContentManager content)
        {
            // On Android we use a minimum height of 240 instead of 256. To keep the size consistent
            // across all versions of the game only subtract 16 pixels as opposed to 32 pixels.
            _menuWidth = Values.MinWidth - 32;
            _menuHeight = Values.MinHeight - (Game1.PlatformPresentation.UseCompactMenus ? 16 : 32);

            // Set the menu border.
            SetMenuBorderTexture(content, GameSettings.MenuBorder);

            // Load the Link on the menu.
            _linkAnimation = AnimatorSaveLoad.LoadAnimator("menu_link");
            _linkAnimation.Play("idle");
        }

        public void SetMenuBorderTexture(ContentManager content, int index)
        {
            // Check for a custom menu border texture in the mods folder.
            string backgroundPath = Path.Combine(Values.PathGraphicsMods, "menuBackground.png");

            // Try to load a custom background. GameFS also covers mods packaged inside the Android APK.
            if (index == 0 && GameFS.Exists(backgroundPath))
            {
                using var stream = GameFS.OpenRead(backgroundPath);
                var customBorder = Texture2D.FromStream(Game1.Graphics.GraphicsDevice, stream);

                // A custom border may be an HD upscale so detect its scale from the border size.
                HdTexture.InferScale(customBorder, content.Load<Texture2D>("Menu/menuBackground"), backgroundPath);

                _sprBackground = customBorder;
                return;
            }
            // Fall back to the built-in textures.
            else
            {
                var texture = index switch
                {
                    0 => content.Load<Texture2D>("Menu/menuBackground"),
                    1 => content.Load<Texture2D>("Menu/menuBackgroundB"),
                    2 => content.Load<Texture2D>("Menu/menuBackgroundC"),
                    _ => content.Load<Texture2D>("Menu/menuBackground")
                };
                _sprBackground = texture;
            }
        }

        public override void OnLoad()
        {
            Game1.UiPageManager.ClearStack();
            Game1.UiPageManager.ChangePage(typeof(MainMenuPage), null, PageManager.TransitionAnimation.TopToBottom, PageManager.TransitionAnimation.TopToBottom);

            Game1.AudioManager.ResetMusic();
            Game1.AudioManager.SetMusic(16, 0);

            Game1.AudioManager.SetMusicVolumeMultiplier(1.0f);
            Game1.AudioManager.PlayMusic();
        }

        public override void Update(GameTime gameTime)
        {
            _backgroundWidth = (int)(Math.Ceiling(Game1.WindowWidth / (double)(32 * _scale) + 1) * 32 * _scale);

            _menuRectangle = new Rectangle(
                (int)(Game1.WindowWidth / 2 - _menuWidth * _scale / 2),
                (int)(Game1.WindowHeight / 2 - _menuHeight * _scale / 2), (int)(_menuWidth * _scale), (int)(_menuHeight * _scale));

            _menuRectangle.X = (int)(_menuRectangle.X / _scale * _scale);
            _menuRectangle.Y = (int)(_menuRectangle.Y / _scale * _scale);

            var borderTileWidth = HdTexture.LogicalWidth(_sprBackground);
            var borderTileHeight = HdTexture.LogicalHeight(_sprBackground);

            _topBar = (int)Math.Ceiling((Game1.WindowHeight / 2 - _menuHeight * _scale / 2) / (float)_scale / borderTileHeight) * borderTileHeight;
            _bottomBar = (int)Math.Ceiling((Game1.WindowHeight / 2 - _menuHeight * _scale / 2) / (float)_scale / borderTileHeight) * borderTileHeight;

            _posX = (int)(Math.Ceiling(_menuRectangle.X / (float)_scale / 32) * 32 - _menuRectangle.X / _scale);

            _leftBar = (int)(Math.Ceiling((Game1.WindowWidth / 2 - _menuWidth * _scale / 2) / (float)_scale / borderTileWidth) * borderTileWidth);
            _rightBar = _leftBar;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            // Draw the black background rectangle
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, null, null, Game1.GetMatrix);
            spriteBatch.Draw(Resources.SprWhite, _menuRectangle, Color.Black);

            // Back Button Text
            {
                // Reverse the label "CancelButton" with label "ConfirmButton" if the user has a need for this.
                var button = ControlHandler.ReverseSettingsLabels ? ControlHandler.ConfirmButton : ControlHandler.CancelButton;

                var backStr = "";
                if (ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[ControlHandler.CancelButton].Keys.Length > 0)
                    backStr = ControlHandler.ButtonDictionary[button].Keys[0].ToString();
                if (!ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[ControlHandler.CancelButton].Buttons.Length > 0)
                    backStr = ControlHandler.GetButtonName(ControlHandler.ButtonDictionary[button].Buttons[0]);

                var strType = Game1.LanguageManager.GetString("main_menu_back", "error");
                var backHelp = backStr + " " + strType;
                var backTextSize = DrawHelper.MeasureString(backHelp);
                var backPos = new Vector2(_menuRectangle.X + 2 * _scale, _menuRectangle.Bottom - backTextSize.Y * _scale);

                DrawHelper.DrawString(spriteBatch, backHelp, backPos, InterfaceElement.MainTextColor, 0, Vector2.Zero, _scale, SpriteEffects.None, 0);
            }

            // Select Button Text
            {
                // Reverse the label "ConfirmButton" with label "CancelButton" if the user has a need for this.
                var button = ControlHandler.ReverseSettingsLabels ? ControlHandler.CancelButton : ControlHandler.ConfirmButton;

                var selectStr = "";
                if (ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[ControlHandler.ConfirmButton].Keys.Length > 0)
                    selectStr = ControlHandler.ButtonDictionary[button].Keys[0].ToString();
                if (!ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[ControlHandler.ConfirmButton].Buttons.Length > 0)
                    selectStr = ControlHandler.GetButtonName(ControlHandler.ButtonDictionary[button].Buttons[0]);

                var strType = Game1.LanguageManager.GetString("main_menu_select", "error");
                var inputHelp = selectStr + " " + strType;
                var selectTextSize = DrawHelper.MeasureString(inputHelp);
                var selectPos =  new Vector2(_menuRectangle.Right - (selectTextSize.X + 2) * _scale, _menuRectangle.Bottom - selectTextSize.Y * _scale);

                DrawHelper.DrawString(spriteBatch, inputHelp, selectPos, InterfaceElement.MainTextColor, 0, Vector2.Zero, _scale, SpriteEffects.None, 0);
            }

            // Tooltip Button Text
            if (Game1.UiPageManager.PageHasTooltips())
            {
                // Reverse the label "Y" with label "X" if the user has a need for this.
                var button = ControlHandler.ReverseSettingsLabels ? CButtons.X : CButtons.Y;

                var tooltipStr = "";
                if (ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[CButtons.Y].Keys.Length > 0)
                    tooltipStr = ControlHandler.ButtonDictionary[button].Keys[0].ToString();
                if (!ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[CButtons.Y].Buttons.Length > 0)
                    tooltipStr = ControlHandler.GetButtonName(ControlHandler.ButtonDictionary[button].Buttons[0]);

                var tooltipType = Game1.LanguageManager.GetString("main_menu_tooltip", "error");
                var tooltipHelp = tooltipStr + " " + tooltipType;
                var tooltipTextSize = DrawHelper.MeasureString(tooltipHelp);
                var tooltipPos = new Vector2(_menuRectangle.X + (_menuRectangle.Width - tooltipTextSize.X * _scale) / 2f, _menuRectangle.Bottom - tooltipTextSize.Y * _scale);

                DrawHelper.DrawString(spriteBatch, tooltipHelp, tooltipPos, InterfaceElement.MainTextColor, 0, Vector2.Zero, _scale, SpriteEffects.None, 0);
            }
            spriteBatch.End();
        }

        public override void DrawTop(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, null, null, Game1.GetMatrix);

            // top
            spriteBatch.Draw(_sprBackground, new Rectangle(
                    (int)(-_posX * _scale), (int)(_menuRectangle.Y - _topBar * _scale), _backgroundWidth, (int)(_topBar * _scale)),
                new Rectangle(0, 0, (int)(_backgroundWidth / _scale), _topBar), Color.White);
            // bottom
            spriteBatch.Draw(_sprBackground, new Rectangle(
                    (int)(-_posX * _scale), _menuRectangle.Bottom, _backgroundWidth, (int)(_bottomBar * _scale)),
                new Rectangle(0, 0, (int)(_backgroundWidth / _scale), _bottomBar), Color.White);

            // left
            spriteBatch.Draw(_sprBackground, new Rectangle(
                    (int)(_menuRectangle.X - _leftBar * _scale), _menuRectangle.Y, (int)(_leftBar * _scale), (int)(_menuHeight * _scale)),
                new Rectangle(0, 0, _leftBar, _menuHeight), Color.White);
            // right
            spriteBatch.Draw(_sprBackground, new Rectangle(
                    _menuRectangle.Right, _menuRectangle.Y, (int)(_rightBar * _scale), (int)(_menuHeight * _scale)),
                new Rectangle(0, 0, _rightBar, _menuHeight), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, null, null, _animationMatrix);

            spriteBatch.End();
            spriteBatch.Begin();
        }
    }
}
