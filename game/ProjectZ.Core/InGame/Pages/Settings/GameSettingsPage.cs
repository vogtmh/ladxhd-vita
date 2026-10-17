using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Screens;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class GameSettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _gameSettingsList;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;
        private readonly ContentManager _content;

        private readonly InterfaceSlider     _sliderSubLanguage;
        private readonly InterfaceSlider     _sliderMenuBricks;
        private readonly InterfaceListLayout _toggleClassicSword;
        private readonly InterfaceListLayout _toggleSavePosition;
        private readonly InterfaceListLayout _toggleAutosave;
        private readonly InterfaceListLayout _toggleSharedStorage;
        private readonly InterfaceListLayout _toggleAchieveNotify;
        private readonly InterfaceListLayout _toggleItemSlotSide;
        private readonly InterfaceListLayout _toggleStreamIntroAssets;
        private readonly InterfaceListLayout _toggleMapThreadedLoads;

        // Get a link to the menu screen so the border texture can be changed.
        MenuScreen _menuScreen => (MenuScreen)Game1.ScreenManager.GetScreen(Values.ScreenNameMenu);

        List<string> _tooltips = new List<string>();
        private bool _showTooltip;

        public void SetMenuBrickTex(int value)    { _sliderMenuBricks.SetValue(value); _menuScreen.SetMenuBorderTexture(_content, value); }
        public void SetClassicSword(bool state)   { _toggleClassicSword.Toggle.SetValue(state); }
        public void SetSavePosition(bool state)   { _toggleSavePosition.Toggle.SetValue(state); }
        public void SetAutomaticSave(bool state)  { _toggleAutosave.Toggle.SetValue(state); }
        public void SetAchieveNotify(bool state)  { _toggleAchieveNotify.Toggle.SetValue(state); }
        public void SetItemSlotsRight(bool state) { _toggleItemSlotSide.Toggle.SetValue(state); }
        public void SetIntroThreading(bool state) { _toggleStreamIntroAssets.Toggle.SetValue(state); }
        public void SetLoadMapThread(bool state)  { _toggleMapThreadedLoads.Toggle.SetValue(state); }

        public GameSettingsPage(int width, int height, ContentManager content)
        {
            _content = content;
            EnableTooltips = true;
            var buttonWidth = 320;

            var buttonHeight = Game1.PlatformPresentation.UseCompactMenus ? 11 : 13;
            var sliderHeight = Game1.PlatformPresentation.UseCompactMenus ? 10 : 11;

            buttonHeight = 14;
            sliderHeight = 12;

            // Game Settings Layout
            _gameSettingsList = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _gameSettingsList.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_game_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top, Scrollable = true };

            // Button: Language
            _contentLayout.AddElement(new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_game_language", PressButtonLanguageChange));
            _tooltips.Add("tooltip_game_language");

            // Slider: Sub-Language
            _sliderSubLanguage = new InterfaceSlider("settings_game_sublanguage",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 2, 1, Game1.LanguageManager.CurrentSubLanguageIndex,
                number => { Game1.LanguageManager.CurrentSubLanguageIndex = number; })
                { SetString = number => LangSliderAdjustment(number) };
            _contentLayout.AddElement(_sliderSubLanguage);
            _tooltips.Add("tooltip_game_sublanguage");

            // Toggle: Classic Sword
            _toggleClassicSword = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_classicsword", GameSettings.ClassicSword, 
                newState => { GameSettings.ClassicSword = newState; });
            _contentLayout.AddElement(_toggleClassicSword);
            _tooltips.Add("tooltip_game_classicsword");

            // Toggle: Items on Right
            _toggleItemSlotSide = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_items_on_right", GameSettings.ItemsOnRight, 
                newState => { GameSettings.ItemsOnRight = newState; });
            _contentLayout.AddElement(_toggleItemSlotSide);
            _tooltips.Add("tooltip_game_itemsonright");

            // Slider: Menu Brick Border
            _sliderMenuBricks = new InterfaceSlider("settings_game_menubricks",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 2, 1, GameSettings.MenuBorder,
                number => { GameSettings.MenuBorder = number; })
                { SetString = number => MenuBorderScaleSliderAdjustment(number) };
            _contentLayout.AddElement(_sliderMenuBricks);
            _tooltips.Add("tooltip_game_menubricks");

            // Toggle: Save Position
            _toggleSavePosition = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_saveposition", GameSettings.StoreSavePos, 
                newState => { GameSettings.StoreSavePos = newState; });
            _contentLayout.AddElement(_toggleSavePosition);
            _tooltips.Add("tooltip_game_saveposition");

            // Toggle: AutoSave
            _toggleAutosave = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_autosave", GameSettings.Autosave, 
                newState => { GameSettings.Autosave = newState; });
            _contentLayout.AddElement(_toggleAutosave);
            _tooltips.Add("tooltip_game_autosave");

            // Toggle: Shared Storage (Android Only)
            if (Game1.SharedSaveService.IsSupported)
            {
                _toggleSharedStorage = InterfaceToggle.GetToggleButton(
                    new Point(buttonWidth, buttonHeight), new Point(5, 2),
                    "settings_game_sharedstorage", GameSettings.SharedStorage,
                    HandleSharedStorageToggle);
                _contentLayout.AddElement(_toggleSharedStorage);
                _tooltips.Add("tooltip_game_sharedstorage");
            }
            // Toggle: Hide Achievement Notifications
            _toggleAchieveNotify = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_hide_achievement", GameSettings.HideAchievement, 
                newState => { GameSettings.HideAchievement = newState; });
            _contentLayout.AddElement(_toggleAchieveNotify);
            _tooltips.Add("tooltip_game_hide_achievement");

            // Toggle: Stream Assets During Intro
            _toggleStreamIntroAssets = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_stream_assets", GameSettings.StreamAssets, 
                newState => { GameSettings.StreamAssets = newState; });
            _contentLayout.AddElement(_toggleStreamIntroAssets);
            _tooltips.Add("tooltip_game_stream_assets");

            // Toggle: Threaded Map Loads
            _toggleMapThreadedLoads = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_threadmapload", GameSettings.ThreadMapLoad, 
                newState => { GameSettings.ThreadMapLoad = newState; });
            _contentLayout.AddElement(_toggleMapThreadedLoads);
            _tooltips.Add("tooltip_game_threadmapload");

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _gameSettingsList.AddElement(_contentLayout);
            _gameSettingsList.AddElement(_bottomBar);
            PageLayout = _gameSettingsList;
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            base.Update(pressedButtons, gameTime);

            // The back button was pressed.
            if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                Game1.UiPageManager.PopPage();

            // The tooltip button was pressed.
            if (ControlHandler.ButtonPressed(CButtons.Y))
            {
                _showTooltip = !_showTooltip;
                if (_showTooltip)
                    Game1.AudioManager.PlaySoundEffect("D360-21-15");
            }
            // Hide the tooltip when pressing anything.
            else if (ControlHandler.AnyButtonPressed())
                _showTooltip = false;
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            // The left button is always the first one selected.
            _bottomBar.Deselect(false);
            _bottomBar.Select(InterfaceElement.Directions.Left, false);
            _bottomBar.Deselect(false);

            PageLayout.Deselect(false);
            PageLayout.Select(InterfaceElement.Directions.Top, false);
        }

        private string LangSliderAdjustment(int number)
        {
            string langKey = number switch
            {
                1 => "settings_game_sublanguage_02",
                2 => "settings_game_sublanguage_03",
                _ => "settings_game_sublanguage_01"
            };
            return " " + Game1.LanguageManager.GetString(langKey, "error");
        }

        public void PressButtonLanguageChange(InterfaceElement element)
        {
            // Update the selected language.
            Game1.LanguageManager.ToggleLanguage();

            // Refresh any textures that need refreshed.
            Resources.RefreshDynamicResources();

            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(CameraCustomPage), out var customCamPage))
            {
                var CameraCustomPage = (CameraCustomPage)customCamPage;
                CameraCustomPage.UpdateAllButtons();
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(ControlSettingsPage), out var controlPage))
            {
                var ControlSettingsPage = (ControlSettingsPage)controlPage;
                ControlSettingsPage.UpdateControllerOverrideText();
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(AchievementsPage), out var achievePage))
            {
                var AchievementsPage = (AchievementsPage)achievePage;
                AchievementsPage.RefreshStrings();
            }
        }

        private string MenuBorderScaleSliderAdjustment(int number)
        {
            // Swap out the menu border with it's replacement.
            _menuScreen.SetMenuBorderTexture(_content, number);

            // Get the text to display on the menu.
            return ": " + number switch
            {
                0 => Game1.LanguageManager.GetString("settings_game_menubricksA", "error"),
                1 => Game1.LanguageManager.GetString("settings_game_menubricksB", "error"),
                2 => Game1.LanguageManager.GetString("settings_game_menubricksC", "error"),
                _ => Game1.LanguageManager.GetString("settings_game_menubricksA", "error")
            };
        }

        private void HandleSharedStorageToggle(bool newState)
        {
            if (!newState)
            {
                GameSettings.SharedStorage = false;
                return;
            }

            if (!Game1.SharedSaveService.HasAccess)
            {
                _toggleSharedStorage.Toggle.SetValue(false);
                Game1.SharedSaveService.RequestAccess();
                return;
            }

            GameSettings.SharedStorage = true;
            for (var slot = 0; slot < SaveStateManager.SaveCount; slot++)
            {
                if (SaveGameSaveLoad.SaveExists(slot))
                    SaveGameSaveLoad.MirrorPairToShared(slot);
            }
            SharedSaveSync.SyncFromSharedIfEnabled();
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_gameSettingsList, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
