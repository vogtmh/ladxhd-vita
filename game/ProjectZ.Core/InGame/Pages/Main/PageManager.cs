using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    public class PageManager
    {
        public enum TransitionAnimation
        {
            Fade,
            LeftToRight,
            RightToLeft,
            TopToBottom,
            BottomToTop
        }

        public Dictionary<Type, InterfacePage> InsideElement = new Dictionary<Type, InterfacePage>();
        public List<Type> PageStack = new List<Type>();
        private List<Type> _savedMenuStack;

        // Pages that should not be resumed.
        private static readonly Type[] _nonResumablePages =
        {
            typeof(QuitGamePage),
            typeof(ExitGamePage),
            typeof(GameOverPage),
        };

        private TransitionAnimation _transitionOutAnimation;
        private TransitionAnimation _transitionInAnimation;

        private Vector2 _menuPosition;

        private double _transitionCount;
        private float _transitionState;

        private int _width;
        private int _height;
        private int _currentPage;
        private int _nextPage;
        private int _transitionTime;
        private int _transitionDirection;

        private const int TransitionFade = 125;
        private const int TransitionNormal = 200;

        private bool _isTransitioning;
        private bool _showTooltipButton;

        private float _menuScale;
        public float MenuScale => _menuScale;

        // Values configurable via lahdmod.
        private float menu_scale_override = 0;

        public void Load(ContentManager content)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "PageManager.lahdmod");
            ModFile.Parse(modFile, this);

            // PageManager only needs created once, and the custom color set in InterfaceElement only needs set once.
            string modFileB = Path.Combine(Values.PathLAHDMods, "InterfaceElement.lahdmod");
            ModFile.ParseStatic(modFileB, typeof(InterfaceElement));
            InterfaceElement.MainTextColor = new Color(InterfaceElement.label_text_color_red, InterfaceElement.label_text_color_grn, InterfaceElement.label_text_color_blu);

            // On Android we use a minimum height of 240 instead of 256. To keep the size consistent
            // across all versions of the game only subtract 16 pixels as opposed to 32 pixels.
            _width = Values.MinWidth - 32;
            _height = Values.MinHeight - (Game1.PlatformPresentation.UseCompactMenus ? 16 : 32);

            AddPage(new MainMenuPage(_width, _height));
            AddPage(new CopyPage(_width, _height));
            AddPage(new CopyConfirmationPage(_width, _height));
            AddPage(new DeleteSaveSlotPage(_width, _height));
            AddPage(new NewGamePage(_width, _height));
            AddPage(new SettingsPage(_width, _height));
            AddPage(new GameSettingsPage(_width, _height, content));
            AddPage(new VideoSettingsPage(_width, _height));
            AddPage(new GraphicsSettingsPage(_width, _height));
            AddPage(new AudioSettingsPage(_width, _height));
            AddPage(new ControlSettingsPage(_width, _height));
            AddPage(new ControlMappingPage(_width, _height));
            AddPage(new ControlOnScreenPage(_width, _height));
            AddPage(new CameraSettingsPage(_width, _height));
            AddPage(new CameraCustomPage(_width, _height));
            AddPage(new ReduxSettingsPage(_width, _height, content));
            AddPage(new ModifierSettingsPage(_width, _height));
            AddPage(new SwordInteractPage(_width, _height));
            AddPage(new PresetOptionsPage(_width, _height));
            AddPage(new CheatsSettingsPage(_width, _height));
            AddPage(new AchievementsPage(_width, _height));
            AddPage(new AchievementsResetPage(_width, _height));
            AddPage(new GameMenuPage(_width, _height));
            AddPage(new QuitGamePage(_width, _height));
            AddPage(new GameOverPage(_width, _height));
            AddPage(new ExitGamePage(_width, _height));
        }

        public void Reload(ContentManager content)
        {
            var pageTypes = new List<Type>(PageStack.Count);
            foreach (var t in PageStack)
                pageTypes.Add(t);

            PageStack.Clear();
            InsideElement.Clear();
            Load(content);

            for (int i = 0; i < pageTypes.Count; i++)
            {
                var pageType = pageTypes[i];
                PageStack.Add(pageType);
                InsideElement[pageType].OnLoad(null);
            }
        }

        public void OnResize(int newWidth, int newHeight)
        {
            InterfacePage page = GetCurrentPage();
            if (page is not null)
            {
                page.OnResize(newWidth, newHeight);
            }
            // If the value was set override the scaling value.
            if (menu_scale_override > 0)
                _menuScale = menu_scale_override;
            else
                _menuScale = Game1.UiScale;

            var pageWidth  = (Game1.WindowWidth / 2 - _width * _menuScale / 2) / _menuScale * _menuScale;
            var pageHeight = (Game1.WindowHeight / 2 - _height * _menuScale / 2) / _menuScale * _menuScale;

            _menuPosition = new Vector2(pageWidth, pageHeight);
        }

        public virtual void Update(GameTime gameTime)
        {
            if (_isTransitioning)
            {
                _transitionCount += Game1.DeltaTime;

                if (_transitionCount >= _transitionTime)
                {
                    _transitionCount = 0;
                    _isTransitioning = false;

                    // remove the old page after finishing the transition
                    if (_transitionDirection == 1)
                    {
                        if (PageStack.Count > 0)
                            PageStack.RemoveAt(0);
                    }
                    _currentPage = 0;

                    if (PageStack.Count > 0)
                        _showTooltipButton = InsideElement[PageStack[0]].EnableTooltips;
                    else
                        _showTooltipButton = false;
                }
            }

            if (!_isTransitioning && PageStack.Count > _currentPage)
                InsideElement[PageStack[_currentPage]].Update(ControlHandler.GetPressedButtons(), gameTime);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            _transitionState = (float)(Math.Sin(_transitionCount / _transitionTime * Math.PI - Math.PI / 2) + 1) / 2f;

            // draw the current page
            if (PageStack.Count > _currentPage)
            {
                var directionX =
                    _transitionOutAnimation == TransitionAnimation.RightToLeft ? _transitionDirection :
                    _transitionOutAnimation == TransitionAnimation.LeftToRight ? -_transitionDirection : 0;
                var directionY =
                    _transitionOutAnimation == TransitionAnimation.TopToBottom ? -_transitionDirection :
                    _transitionOutAnimation == TransitionAnimation.BottomToTop ? _transitionDirection : 0;
                var transitionOffset = new Vector2(
                    _width * 0.65f * _transitionState * directionX * _menuScale,
                    _height * 0.65f * _transitionState * directionY * _menuScale);

                InsideElement[PageStack[_currentPage]].Draw(spriteBatch,
                    _menuPosition + transitionOffset, _menuScale, 1 - _transitionState);
            }

            if (!_isTransitioning || PageStack.Count <= _nextPage)
                return;

            // draw the next page while transitioning
            var directionXNext =
                _transitionInAnimation == TransitionAnimation.RightToLeft ? -_transitionDirection :
                _transitionInAnimation == TransitionAnimation.LeftToRight ? _transitionDirection : 0;
            var directionYNext =
                _transitionInAnimation == TransitionAnimation.TopToBottom ? _transitionDirection :
                _transitionInAnimation == TransitionAnimation.BottomToTop ? -_transitionDirection : 0;
            var transitionOffsetNext = new Vector2(
                _width * 0.65f * (1 - _transitionState) * directionXNext * _menuScale,
                _height * 0.65f * (1 - _transitionState) * directionYNext * _menuScale);

            InsideElement[PageStack[_nextPage]].Draw(spriteBatch,
                _menuPosition + transitionOffsetNext, _menuScale, _transitionState);
        }

        private void AddPage(InterfacePage element)
        {
            InsideElement.Add(element.GetType(), element);
        }

        public bool ChangePage(Type nextPage, Dictionary<string, object> intent, TransitionAnimation animationIn = TransitionAnimation.RightToLeft, TransitionAnimation animationOut = TransitionAnimation.RightToLeft)
        {
            // do not add the page/restart the animation if it is transitioning out of the page
            if (!_isTransitioning || PageStack.Count <= 0 || nextPage != PageStack[0])
            {
                PageStack.Insert(0, nextPage);

                _transitionCount = 0;
                _transitionState = 0;
            }
            else
            {
                _transitionCount = _transitionTime - _transitionCount;
            }
            _isTransitioning = true;
            _transitionDirection = -1;

            _currentPage = 1;
            _nextPage = 0;

            // onload
            InsideElement[nextPage].OnLoad(intent);

            _transitionInAnimation = animationIn;
            _transitionOutAnimation = animationOut;

            // @HACK
            _transitionTime = _transitionInAnimation == TransitionAnimation.Fade ? TransitionFade : TransitionNormal;

            return true;
        }

        public InterfacePage GetPage(Type pageType)
        {
            return InsideElement[pageType];
        }

        public InterfacePage GetCurrentPage()
        {
            if (PageStack.Count <= 0)
                return null;

            return InsideElement[PageStack[0]];
        }

        public bool ChangePage(Type nextPage)
        {
            return ChangePage(nextPage, null);
        }

        public void PopPage(Dictionary<string, object> intent = null, TransitionAnimation animationIn = TransitionAnimation.RightToLeft, TransitionAnimation animationOut = TransitionAnimation.RightToLeft, bool SkipSound = false)
        {
            if (PageStack.Count <= 0)
                return;

            InsideElement[PageStack[0]].OnPop(intent);

            if (!_isTransitioning)
            {
                _transitionCount = 0;
                _isTransitioning = true;
            }
            else
            {
                PageStack.RemoveAt(0);
                _transitionCount = _transitionTime - _transitionCount;
            }
            _transitionDirection = 1;

            _currentPage = 0;
            _nextPage = 1;

            // onload
            if (PageStack.Count > 1)
                InsideElement[PageStack[1]].OnReturn(intent);

            _transitionInAnimation = animationIn;
            _transitionOutAnimation = animationOut;

            // @HACK
            _transitionTime = _transitionInAnimation == TransitionAnimation.Fade ? TransitionFade : TransitionNormal;

            if (!SkipSound)
                Game1.AudioManager.PlaySoundEffect("D360-18-12");
        }

        public void PopAllPages(TransitionAnimation animationIn = TransitionAnimation.RightToLeft, TransitionAnimation animationOut = TransitionAnimation.RightToLeft)
        {
            PopPage(null, animationIn, animationOut, true);

            // remove everything but the current page
            if (PageStack.Count > 1)
            {
                for (var i = 0; i < PageStack.Count; i++)
                    InsideElement[PageStack[i]].OnPop(null);

                PageStack.RemoveRange(1, PageStack.Count - 1);
            }
        }

        public bool PageHasTooltips()
        {
            // Used by "MenuScreen.cs" to draw tooltip button hint.
            return _showTooltipButton;
        }

        public void ClearStack()
        {
            PageStack.Clear();
        }

        public void SaveMenuStack()
        {
            // Keep a copy of the current menu stack so it can be resumed.
            _savedMenuStack = new List<Type>(PageStack.Count);
            foreach (var t in PageStack)
            {
                // Do not restore certain pages.
                if (Array.IndexOf(_nonResumablePages, t) < 0)
                    _savedMenuStack.Add(t);
            }
        }

        public void ClearSavedMenuStack()
        {
            // Clears the menu stack forcing it to start from GameMenuPage.
            _savedMenuStack = null;
        }

        public bool RestoreMenuStack(Dictionary<string, object> intent, TransitionAnimation animationIn = TransitionAnimation.TopToBottom, TransitionAnimation animationOut = TransitionAnimation.TopToBottom)
        {
            // The stack is empty or null so nothing to restore.
            if (_savedMenuStack == null || _savedMenuStack.Count == 0)
                return false;

            // Rebuild the menu stack from the stored list.
            // _savedMenuStack[0] - Page the player was on when menu was closed.
            PageStack.Clear();
            foreach (var t in _savedMenuStack)
            {
                if (InsideElement.ContainsKey(t))
                    PageStack.Add(t);
            }
            // If the stack is empty just exit now.
            if (PageStack.Count == 0)
                return false;

            // Run "OnLoad" on root page only. Pauses music and leaves selections intact.
            InsideElement[PageStack[PageStack.Count - 1]].OnLoad(intent);

            _transitionCount = 0;
            _transitionState = 0;
            _isTransitioning = true;
            _transitionDirection = -1;

            // Only the page the player was on (index 0) animates in. Setting _currentPage past
            // the end of the stack means nothing animates out, mirroring a fresh menu open.
            _currentPage = PageStack.Count;
            _nextPage = 0;

            _transitionInAnimation = animationIn;
            _transitionOutAnimation = animationOut;
            _transitionTime = _transitionInAnimation == TransitionAnimation.Fade ? TransitionFade : TransitionNormal;

            return true;
        }
    }
}
