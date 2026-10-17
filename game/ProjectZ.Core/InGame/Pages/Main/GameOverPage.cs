using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class GameOverPage : InterfacePage
    {
        private InterfaceListLayout _pageLayout;
        private InterfaceListLayout _layout0;
        private InterfaceImage _gameOverImage;

        public GameOverPage(int width, int height)
        {
            _pageLayout = new InterfaceListLayout { Size = new Point(width, height), Selectable = true };
            _layout0 = new InterfaceListLayout { Size = new Point(width, 75), ContentAlignment = InterfaceElement.Gravities.Bottom };
            _gameOverImage = new InterfaceImage(Resources.GetSprite("ui game over"), Point.Zero);

            var layout1 = new InterfaceListLayout { Size = new Point(width - 10, 55) };
            var layout2 = new InterfaceListLayout { Size = new Point(width, 75), ContentAlignment = InterfaceElement.Gravities.Top, Selectable = true };

            _pageLayout.AddElement(_layout0);
            _pageLayout.AddElement(layout1);
            _pageLayout.AddElement(layout2);

            _layout0.AddElement(_gameOverImage);
            layout2.AddElement(new InterfaceButton(new Point(85, 20), Point.Zero, "gameover_continue", OnClickContinue) { Margin = new Point(2, 2) });
            layout2.AddElement(new InterfaceButton(new Point(85, 20), Point.Zero, "gameover_quit", OnClickQuit) { Margin = new Point(2, 2) });

            PageLayout = _pageLayout;
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            _gameOverImage.UpdateSprite(Resources.GetSprite("ui game over"));

            // select the "Back to Game" button
            PageLayout.Deselect(false);
            PageLayout.Select(InterfaceElement.Directions.Top, false);

            Game1.AudioManager.ResetMusic();
            Game1.AudioManager.SetMusic(2, 0);

            Game1.AudioManager.SetMusicVolumeMultiplier(1.0f);
            Game1.AudioManager.PlayMusic();

            _pageLayout.Recalculate = true;

            _layout0.Recalculate = true;
            _layout0.Size.Y = 75 - (int)(MapManager.Camera.Scale * 2);
        }

        public void OnClickContinue(InterfaceElement element)
        {
            Game1.UiPageManager.ClearStack();
            Game1.ScreenManager.ChangeScreen(Values.ScreenNameGame);

            ((GameOverSystem)Game1.GameManager.GameSystems[typeof(GameOverSystem)]).EndSystem();

            Game1.GameManager.RespawnPlayer();
        }

        public void OnClickQuit(InterfaceElement element)
        {
            ((GameOverSystem)Game1.GameManager.GameSystems[typeof(GameOverSystem)]).EndSystem();
            Game1.ScreenManager.ChangeScreen(Values.ScreenNameMenu);
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float scale, float transparency)
        {
            PageLayout?.Draw(spriteBatch, position, scale, transparency);
        }
    }
}
