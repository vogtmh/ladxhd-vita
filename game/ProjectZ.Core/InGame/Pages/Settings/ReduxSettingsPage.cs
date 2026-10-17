using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class ReduxSettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _reduxOptionsList;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;
        private readonly ContentManager _content;

        private readonly InterfaceSlider     _sliderMapTeleporter;
        private readonly InterfaceListLayout _toggleVariableFont;
        private readonly InterfaceListLayout _toggleHelperText;
        private readonly InterfaceListLayout _toggleDialogSkip;
        private readonly InterfaceListLayout _toggleUncensored;
        private readonly InterfaceListLayout _toggleUnmissables;
        private readonly InterfaceListLayout _togglePhotosColor;
        private readonly InterfaceListLayout _toggleAnimalDamage;

        private bool _showTooltip;

        public void SetMapTeleportValue(int value)    { _sliderMapTeleporter.SetValue(value); }
        public void SetVariableWidthFont(bool state)  { _toggleVariableFont.Toggle.SetValue(state); PressButtonDialogFontChange(state); }
        public void SetDisableHelperText(bool state)  { _toggleHelperText.Toggle.SetValue(state); PressButtonToggleHelpers(state); }
        public void SetEnableDialogSkip(bool state)   { _toggleDialogSkip.Toggle.SetValue(state); }
        public void SetDisableCensorship(bool state)  { _toggleUncensored.Toggle.SetValue(state); PressButtonToggleUncensored(state); }
        public void SetEnableUnmissables(bool state)  { _toggleUnmissables.Toggle.SetValue(state); }
        public void SetColoredPhotographs(bool state) { _togglePhotosColor.Toggle.SetValue(state); PressButtonTogglePhotosColor(state); }
        public void SetNoAnimalDamage(bool state)     { _toggleAnimalDamage.Toggle.SetValue(state); }

        public ReduxSettingsPage(int width, int height, ContentManager content)
        {
            _content = content;
            EnableTooltips = true;
            var buttonWidth = 320;
            var buttonHeight = 14;
            var sliderHeight = 12;

            // Redux Settings Layout
            _reduxOptionsList = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _reduxOptionsList.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_redux_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top, Scrollable = false };

            // Slider: Map Teleport
            _sliderMapTeleporter = new InterfaceSlider("settings_redux_dungeonteleport",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 3, 1, GameSettings.MapTeleport, 
                number => { GameSettings.MapTeleport = number; })
                { SetString = number => MapTeleportSliderAdjustment(number) };
            _contentLayout.AddElement(_sliderMapTeleporter);

            // Toggle: Variable Width Font Toggle:
            _toggleVariableFont = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_redux_vwfont", GameSettings.VarWidthFont, 
                newState => { PressButtonDialogFontChange(newState); });
            _contentLayout.AddElement(_toggleVariableFont);

            // Toggle: Disable Helper Interactions:
            _toggleHelperText = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_redux_nohelptext", GameSettings.NoHelperText, 
                newState => { PressButtonToggleHelpers(newState); });
            _contentLayout.AddElement(_toggleHelperText);

            // Toggle: Enable Dialog Skip:
            _toggleDialogSkip = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_redux_dialogskip", GameSettings.DialogSkip, 
                newState => { PressButtonToggleDialogSkip(newState); });
            _contentLayout.AddElement(_toggleDialogSkip);

            // Toggle: Disable Censorship:
            _toggleUncensored = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_redux_uncensor", GameSettings.Uncensored, 
                newState => { PressButtonToggleUncensored(newState); });
            _contentLayout.AddElement(_toggleUncensored);

            // Toggle: Enable No Missables:
            _toggleUnmissables = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_redux_unmissables", GameSettings.Unmissables, 
                newState => { PressButtonToggleUnmissables(newState); });
            _contentLayout.AddElement(_toggleUnmissables);

            // Toggle: Colored Photos:
            _togglePhotosColor = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_redux_photoscolor", GameSettings.PhotosColor, 
                newState => { PressButtonTogglePhotosColor(newState); });
            _contentLayout.AddElement(_togglePhotosColor);

            // Toggle: No Animal Damage:
            _toggleAnimalDamage = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_redux_noanimaldmg", GameSettings.NoAnimalDamage, 
                newState => { PressButtonNoAnimalDamage(newState); });
            _contentLayout.AddElement(_toggleAnimalDamage);

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _reduxOptionsList.AddElement(_contentLayout);
            _reduxOptionsList.AddElement(_bottomBar);
            PageLayout = _reduxOptionsList;
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
            // the left button is always the first one selected
            _bottomBar.Deselect(false);
            _bottomBar.Select(InterfaceElement.Directions.Left, false);
            _bottomBar.Deselect(false);

            PageLayout.Deselect(false);
            PageLayout.Select(InterfaceElement.Directions.Top, false);
        }

        public void PressButtonDialogFontChange(bool newState)
        {
            GameSettings.VarWidthFont = newState;
            Game1.GameManager.InGameOverlay.TextboxOverlay.ResolutionChange();
            Game1.UiPageManager.Reload(_content);
        }

        public void PressButtonToggleHelpers(bool newState)
        {
            // Set the new state and refresh the items group.
            GameSettings.NoHelperText = newState;
            Game1.GameManager.ItemManager.Load();
        }

        public void PressButtonToggleDialogSkip(bool newState)
        {
            // Set the new state and refresh the items group.
            GameSettings.DialogSkip = newState;
        }

        public void PressButtonToggleUncensored(bool newState)
        {
            // Set the new state and refresh the fonts and items group.
            GameSettings.Uncensored = newState;
            Game1.GameManager.InGameOverlay.TextboxOverlay.ResolutionChange();
            Game1.GameManager.ItemManager.Load();
        }

        public void PressButtonToggleUnmissables(bool newState) 
        {
            GameSettings.Unmissables = newState;
        }

        public void PressButtonTogglePhotosColor(bool newState) 
        {
            GameSettings.PhotosColor = newState;
            Resources.RefreshDynamicResources();
        }

        public void PressButtonNoAnimalDamage(bool newState) 
        {
            GameSettings.NoAnimalDamage = newState;
        }

        private string MapTeleportSliderAdjustment(int number)
        {
            string option = number switch
            {
                1 => "settings_redux_dungeonteleport_02",
                2 => "settings_redux_dungeonteleport_03",
                3 => "settings_redux_dungeonteleport_04",
                _ => "settings_redux_dungeonteleport_01"
            };
            return " " + Game1.LanguageManager.GetString(option, "error");
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = GetOptionToolip();
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }

        private string GetOptionToolip()
        {
            // Detect back button press by checking the index of the main InterfaceListLayout.
            if (_reduxOptionsList.SelectionIndex == 2)
                return  Game1.LanguageManager.GetString("tooltip_default", "error");

            // Detect the chosen button by checking the content InterfaceListLayout.
            int index = _contentLayout.SelectionIndex;
            string tooltip = "Select an option to view its tooltip.";

            // If the index is 0 it's the slider for Map Teleport so we need to get the sub indexes.
            if (index == 0)
            {
                // Get the currently selected index.
                index = _sliderMapTeleporter.CurrentStep;

                // Use the selected index to determine which tooltip to show.
                switch (index) 
                {
                    case 0:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_dungeonteleport_01", "error"); break; }
                    case 1:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_dungeonteleport_02", "error"); break; }
                    case 2:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_dungeonteleport_03", "error"); break; }
                    case 3:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_dungeonteleport_04", "error"); break; }
                }
                // Display the tooltip in the tooltip window.
                return tooltip;
            }
            // Use the selected index to determine which tooltip to show.
            switch (index) 
            {
                case 1:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_vwfont", "error"); break; }
                case 2:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_nohelptext", "error"); break; }
                case 3:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_dialogskip", "error"); break; }
                case 4:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_uncensor", "error"); break; }
                case 5:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_unmissables", "error"); break; }
                case 6:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_photoscolor", "error"); break; }
                case 7:  { tooltip = Game1.LanguageManager.GetString("tooltip_redux_noanimaldmg", "error"); break; }
            }
            // Display the tooltip in the tooltip window.
            return tooltip;
        }
    }
}
