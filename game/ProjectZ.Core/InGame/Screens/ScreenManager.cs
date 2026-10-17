using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Things;
using ProjectZ.InGame.Controls;

namespace ProjectZ.InGame.Screens
{
    public class ScreenManager
    {
        public string CurrentScreenId { get; private set; }

        private readonly List<Screen> _screens = new List<Screen>();
        private readonly List<Screen> _newScreens = new List<Screen>();

        private Screen _currentScreen;
        private Screen _nextScreen;

        private bool _changeScreen;
        private bool _finishedLoading;

        public IntroScreen Intro;

        public void LoadIntro(ContentManager content)
        {
            Intro = new IntroScreen(Values.ScreenNameIntro);
            Intro.Load(content);

            _screens.Add(Intro);

            ChangeScreen(Values.ScreenNameIntro);
        }

        public void Load(ContentManager content)
        {
            // game screens
            _newScreens.Add(new MenuScreen(Values.ScreenNameMenu));
            _newScreens.Add(new GameScreen(Values.ScreenNameGame));

            // editor screens
            if (Game1.EditorMode)
                Game1.EditorManager?.RegisterEditorScreens(_newScreens);

            foreach (var screen in _newScreens)
                screen.Load(content);

            _finishedLoading = true;
        }

        public void Update(GameTime gameTime)
        {
            // add the screens after finishing the loading
            // prevents problems with thread loading
            if (_finishedLoading && _newScreens.Count > 0)
            {
                _screens.AddRange(_newScreens);
                _newScreens.Clear();
            }

            if (_changeScreen)
            {
                _changeScreen = false;
                _currentScreen = _nextScreen;
                _currentScreen.OnLoad();
            }

            _currentScreen.Update(gameTime);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            _currentScreen.Draw(spriteBatch);
        }

        public void DrawTop(SpriteBatch spriteBatch)
        {
            _currentScreen.DrawTop(spriteBatch);
        }

        public void DrawRT(SpriteBatch spriteBatch)
        {
            _currentScreen.DrawRenderTarget(spriteBatch);
        }

        public void OnResize(int newWidth, int newHeight)
        {
            foreach (var screen in _screens)
                screen.OnResize(newWidth, newHeight);
        }

        public void OnResizeEnd(int newWidth, int newHeight)
        {
            foreach (var screen in _screens)
                screen.OnResizeEnd(newWidth, newHeight);
        }

        public void ChangeScreen(string nextScreen)
        {
            CurrentScreenId = nextScreen.ToUpper();
            if (Game1.PlatformInput.HasTouchInput)
                VirtualController.Initialize(Game1.WindowWidth, Game1.WindowHeight, CurrentScreenId != "GAME");

            foreach (var screen in _screens)
            {
                if (screen.Id == CurrentScreenId)
                {
                    _changeScreen = true;
                    _nextScreen = screen;
                    return;
                }
            }
        }

        public Screen GetScreen(string screenId)
        {
            return _screens.FirstOrDefault(t => t.Id == screenId.ToUpper());
        }
    }
}
