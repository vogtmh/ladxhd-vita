using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class AchievementsPage : InterfacePage
    {
        private readonly InterfaceListLayout _mainLayout;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        private InterfaceAchievement[] _achievement = new InterfaceAchievement[AchievementManager.Count];
        private InterfaceLabel _counterLabel;

        private Texture2D[] _images = new Texture2D[AchievementManager.Count];
        private string[,] _descriptions = new string[AchievementManager.Count, 2];

        private static readonly (CButtons Button, int Step)[] _scrollButtons =
        {
            (CButtons.LT,   -10),
            (CButtons.RT,    10),
            (CButtons.LB,    -5),
            (CButtons.RB,     5),
            (CButtons.Left,  -2),
            (CButtons.Right,  2),
        };
        private CButtons _repeatButton;
        private int _repeatStep;
        private bool _repeatActive;
        private bool _hasRepeat;
        private float _repeatCounter;

        private float _repeatDelay = 500;
        private float _repeatFrequency = 125;
        private bool _firstLoad;

        public AchievementsPage(int width, int height)
        {
            var itemWidth = 320;
            var itemHeight = 50;
            var saveButtonRec = new Point(204, 32);

            AchievementsSetup();

            _mainLayout = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };

            // Header row: centered title with an "Earned: x/y" counter in the right corner.
            var headerHeight = (int)(height * Values.MenuHeaderSize);
            int counterWidth = 90;
            var headerLayout = new InterfaceListLayout
            {
                Size = new Point(itemWidth, headerHeight),
                ContentAlignment = InterfaceElement.Gravities.Left,
                HorizontalMode = true
            };
            // Add a dummy spacer and the header text.
            headerLayout.AddElement(new InterfaceLabel("", new Point(counterWidth, headerHeight), new Point(0, 0)) { Translate = false });
            headerLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "achievements_header", new Point(itemWidth - counterWidth * 2, headerHeight), new Point(0, 0)));

            // The label which shows the achievement earned count.
            _counterLabel = new InterfaceLabel("", new Point(counterWidth, headerHeight), new Point(0, 11))
            {
                Translate = false,
                TextScale = Game1.UiScale > 2 ? 0.8f : 1.0f,
                TextAlignment = InterfaceElement.Gravities.Right | InterfaceElement.Gravities.Bottom
            };
            headerLayout.AddElement(_counterLabel);
            _mainLayout.AddElement(headerLayout);

            // Scrollable content.
            _contentLayout = new InterfaceListLayout
            {
                Size = new Point(width, (int)(height * Values.MenuContentSize) - 12),
                Selectable = true,
                ContentAlignment = InterfaceElement.Gravities.Top,
                Scrollable = true
            };

            // Add each achievement to the scrollable window.
            for (int i = 0; i < AchievementManager.Count; i++)
            {
                _contentLayout.AddElement(_achievement[i] = new InterfaceAchievement(
                    new Point(itemWidth, itemHeight), new Point(0, 3), _images[i],
                    _descriptions[i, 0], _descriptions[i, 1],
                    translate: false, achieved: AchievementManager.IsEarned(i)));
            }
            // Bottom Bar
            _bottomBar = new InterfaceListLayout
            {
                Size = new Point(saveButtonRec.X, (int)(height * Values.MenuFooterSize)),
                HorizontalMode = true,
                Selectable = true
            };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 0), "settings_menu_back", element => Game1.UiPageManager.PopPage()));
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 0), "achievements_reset", element => Game1.UiPageManager.ChangePage(typeof(AchievementsResetPage))));
            _mainLayout.AddElement(_contentLayout);
            _mainLayout.AddElement(_bottomBar);
            PageLayout = _mainLayout;
            UpdateCounter();
        }

        private void AchievementsSetup()
        {
            var forceEnglish = (Game1.LanguageManager.CurrentLanguageCode == "chn");

            // Load the achievement images and strings.
            for (int i = 0; i < AchievementManager.Count; i++)
            {
                _images[i] = Resources.GetTexture("achievement" + i.ToString() +".png");
                _descriptions[i, 0] = Game1.LanguageManager.GetString("achieveName" + i.ToString(), "error", false, forceEnglish);
                _descriptions[i, 1] = Game1.LanguageManager.GetString("achieveDesc" + i.ToString(), "error");
            }
        }

        public void RefreshStrings()
        {
            AchievementsSetup();
            for (int i = 0; i < _achievement.Length; i++)
            {
                _achievement[i].SetTitle(_descriptions[i, 0]);
                _achievement[i].SetDescription(_descriptions[i, 1]);
            }
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            RefreshAchievedStates();

            if (!_firstLoad)
            {
                _firstLoad = true;
                _bottomBar.Deselect(false);
                _bottomBar.Select(InterfaceElement.Directions.Left, false);
                _bottomBar.Deselect(false);

                PageLayout.Deselect(false);
                PageLayout.Select(InterfaceElement.Directions.Top, false);
            }

            UpdateCounter();
        }

        public override void OnReturn(Dictionary<string, object> intent)
        {
            base.OnReturn(intent);
            RefreshAchievedStates();
        }

        public void RefreshAchievedStates()
        {
            for (int i = 0; i < _achievement.Length; i++)
                _achievement[i].Achieved = AchievementManager.IsEarned(i);

            UpdateCounter();
        }

        private void UpdateCounter()
        {
            int earned = 0;
            for (int i = 0; i < AchievementManager.Count; i++)
                if (AchievementManager.IsEarned(i))
                    earned++;

            var earnedText = Game1.LanguageManager.GetString("achievements_earned", "error");
            _counterLabel.SetText(earnedText + ": " + earned + "/" + AchievementManager.Count);
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            base.Update(pressedButtons, gameTime);

            _counterLabel.TextScale = Game1.UiScale > 2 ? 0.8f : 1.0f;

            void ResetRepeat()
            {
                _hasRepeat = false;
                _repeatActive = false;
                _repeatCounter = 0f;
            }

            if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
            {
                Game1.UiPageManager.PopPage();
                return;
            }
            bool barFocused = !_contentLayout.Selected;

            foreach (var (button, step) in _scrollButtons)
            {
                if (barFocused && (button == CButtons.Left || button == CButtons.Right))
                    continue;

                if (ControlHandler.ButtonPressed(button))
                {
                    if (DoScroll(step))
                    {
                        _repeatButton = button;
                        _repeatStep = step;
                        _repeatCounter = 0f;
                        _repeatActive = false;
                        _hasRepeat = true;
                    }
                    else
                    {
                        ResetRepeat();
                    }
                    return;
                }
            }
            if (barFocused && (_repeatButton == CButtons.Left || _repeatButton == CButtons.Right))
            {
                ResetRepeat();
                return;
            }

            if (!_hasRepeat || !ControlHandler.ButtonDown(_repeatButton))
            {
                ResetRepeat();
                return;
            }

            _repeatCounter += Game1.DeltaTime;

            if (!_repeatActive)
            {
                if (_repeatCounter >= _repeatDelay)
                {
                    _repeatActive = true;
                    _repeatCounter = 0f;
                    if (!DoScroll(_repeatStep))
                        ResetRepeat();
                }
            }
            else if (_repeatCounter >= _repeatFrequency)
            {
                _repeatCounter -= _repeatFrequency;
                if (!DoScroll(_repeatStep))
                    ResetRepeat();
            }
        }

        private bool DoScroll(int step)
        {
            int dir = step < 0 ? -1 : 1;

            if (_contentLayout.Selected)
            {
                if (_contentLayout.MoveSelection(step, true) != 0)
                    return true;
                FocusBar();
                return false;
            }
            FocusContent(dir > 0 ? InterfaceElement.Directions.Top : InterfaceElement.Directions.Down);
            return false;
        }

        private void FocusBar()
        {
            _contentLayout.Deselect(true);
            _mainLayout.SetSelectionIndex(_mainLayout.Elements.IndexOf(_bottomBar));
            _bottomBar.Select(InterfaceElement.Directions.Top, true);
            Game1.AudioManager.PlaySoundEffect("D360-10-0A");
        }

        private void FocusContent(InterfaceElement.Directions edge)
        {
            _bottomBar.Deselect(true);
            _mainLayout.SetSelectionIndex(_mainLayout.Elements.IndexOf(_contentLayout));
            _contentLayout.Select(edge, true);
            Game1.AudioManager.PlaySoundEffect("D360-10-0A");
        }
    }
}
