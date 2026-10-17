using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    internal class ControlOnScreenPage : InterfacePage
    {
        private readonly InterfaceListLayout _controlSettingsList;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        private readonly InterfaceSlider _sliderTouchControls;
        private readonly InterfaceSlider _sliderTouchMovement;
        private readonly InterfaceSlider _sliderTouchOpacity;
        private readonly InterfaceSlider _sliderShadowOpacity;
        private readonly InterfaceSlider _sliderTouchScale;
        private readonly InterfaceListLayout _toggleAnalogButtons;
        private readonly InterfaceListLayout _layoutButtons;

        private bool _showTooltip;

        public void SetOnScreenControlsSlider(int value) => _sliderTouchControls.SetValue(value);

        public ControlOnScreenPage(int width, int height)
        {
            EnableTooltips = true;
            var buttonWidth = 320;
            var buttonHeight = 15;
            var sliderHeight = 10;

            // On-Screen Button Settings Layout
            _controlSettingsList = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _controlSettingsList.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_controls_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top, Scrollable = true };

            // Slider: Placement Grid Size
            _sliderTouchControls = new InterfaceSlider("settings_controls_touchgridsize", 
                buttonWidth, sliderHeight, new Point(1, 2), 0, 100, 1, GameSettings.TouchGridSize,
                number => { GameSettings.TouchGridSize = number; })
                { SetString = number => SetOnScreenControlsGridSize(number) };
            _contentLayout.AddElement(_sliderTouchControls);

            // Buttons: Edit Layout / Reset Layout
            _layoutButtons = new InterfaceListLayout { Size = new Point(buttonWidth + 10, buttonHeight + 4), Selectable = true, HorizontalMode = true };
            _layoutButtons.AddElement(new InterfaceButton(new Point(buttonWidth / 2 - 5, buttonHeight), new Point(5, 2),
                "settings_controls_editlayout", element => { _showTooltip = false; VirtualController.BeginEditMode(); }));
            _layoutButtons.AddElement(new InterfaceButton(new Point(buttonWidth / 2 - 5, buttonHeight), new Point(5, 2),
                "settings_controls_resetlayout", element => { VirtualController.ResetLayout(); }));
            _contentLayout.AddElement(_layoutButtons);

            // Slider: On-Screen Controls
            _sliderTouchControls = new InterfaceSlider("settings_controls_onscreenpad", 
                buttonWidth, sliderHeight, new Point(1, 2), 0, 2, 1, GameSettings.TouchControls,
                number => { GameSettings.TouchControls = number; })
                { SetString = number => SetOnScreenControlsVisibility(number) };
            _contentLayout.AddElement(_sliderTouchControls);

            // Slider: Touch Momement Options
            _sliderTouchMovement = new InterfaceSlider("settings_controls_touchmovement", 
                buttonWidth, sliderHeight, new Point(1, 2), 0, 3, 1, GameSettings.TouchMovement,
                number => { GameSettings.TouchMovement = number; })
                { SetString = number => TouchMovementSliderAdjustmentString(number) };
            _contentLayout.AddElement(_sliderTouchMovement);

            // Slider: Fade Opacity
            _sliderTouchOpacity = new InterfaceSlider("settings_controls_fadeopacity", 
                buttonWidth, sliderHeight, new Point(1, 2), 0, 100, 1, GameSettings.TouchOpacity,
                number => { GameSettings.TouchOpacity = number; })
                { SetString = number => FadeOpacitySliderAdjustmentString(number) };
            _contentLayout.AddElement(_sliderTouchOpacity);

            // Slider: Shadow Opacity
            _sliderShadowOpacity = new InterfaceSlider("settings_controls_shadowopacity", 
                buttonWidth, sliderHeight, new Point(1, 2), 0, 100, 1, GameSettings.ShadowOpacity,
                number => { GameSettings.ShadowOpacity = number; })
                { SetString = number => FadeOpacitySliderAdjustmentString(number) };
            _contentLayout.AddElement(_sliderShadowOpacity);

            // Slider: Controls Scaling
            _sliderTouchScale = new InterfaceSlider("settings_controls_onscreenscale", 
                buttonWidth, sliderHeight, new Point(1, 2), 4, 20, 1, GameSettings.TouchScaling,
                number => { GameSettings.TouchScaling = number; })
                { SetString = number => OnScreenScaleSliderAdjustmentString(number) };
            _contentLayout.AddElement(_sliderTouchScale);

            // Toggle: LS/RS Buttons
            _toggleAnalogButtons = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_controls_onscreenanalogbuttons", GameSettings.TouchSticks, 
                newState => { GameSettings.TouchSticks = newState; VirtualController.Initialize(Game1.WindowWidth, Game1.WindowHeight, true); });
            _contentLayout.AddElement(_toggleAnalogButtons);

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _controlSettingsList.AddElement(_contentLayout);
            _controlSettingsList.AddElement(_bottomBar);
            PageLayout = _controlSettingsList;
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            // Layout editor: the menu is hidden and doesn't take input.
            if (VirtualController.EditMode)
            {
                VirtualController.KeepEditModeAlive();

                // Physical controller / keyboard exit (touch users tap the controller button).
                if (ControlHandler.ButtonPressed(ControlHandler.CancelButton) || ControlHandler.ButtonPressed(CButtons.Select))
                    VirtualController.EndEditMode();

                return;
            }

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

        private string SetOnScreenControlsGridSize(int number)
        {
            return ": " + number.ToString();
        }

        private string SetOnScreenControlsVisibility(int number)
        {
            return ": " + Game1.LanguageManager.GetString("settings_controls_onscreenpad_0" + (number + 1).ToString(), "error");
        }

        private string FadeOpacitySliderAdjustmentString(int number)
        {
            // Apply the scaling settings to the controls.
            VirtualController.Initialize(Game1.WindowWidth, Game1.WindowHeight, true);

            // Display the updated value.
            return ": " + (number) + "%";
        }

        private string OnScreenScaleSliderAdjustmentString(int number)
        {
            // Apply the scaling settings to the controls.
            VirtualController.Initialize(Game1.WindowWidth, Game1.WindowHeight, true);

            // Display the updated value.
            return ": " + (number * 5) + "%";
        }

        private string TouchMovementSliderAdjustmentString(int number)
        {
            // Apply the scaling settings to the controls.
            VirtualController.Initialize(Game1.WindowWidth, Game1.WindowHeight, true);

            // Display the updated string.
            return ": " + Game1.LanguageManager.GetString("settings_controls_touchmovement_0" + (number + 1).ToString(), "error");
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Layout editor: skip the menu so the whole screen is free, and show the instructions instead.
            if (VirtualController.EditMode)
            {
                PageTooltip.Draw(spriteBatch, Game1.LanguageManager.GetString("tooltip_controls_editmode", "error"));
                return;
            }

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
            if (_controlSettingsList.SelectionIndex == 2)
                return  Game1.LanguageManager.GetString("tooltip_default", "error");

            // Detect the chosen button by checking the content InterfaceListLayout.
            int index = _contentLayout.SelectionIndex;
            string tooltip = "Select an option to view its tooltip.";

            // If the index is 1 we need to get the horizontal index.
            if (index == 1)
            {
                // Get the currently selected index.
                index = _layoutButtons.SelectionIndex;

                // Use the selected index to determine which tooltip to show.
                switch (index) 
                {
                    case 0:  { tooltip = Game1.LanguageManager.GetString("tooltip_controls_editlayout", "error"); break; }
                    case 1:  { tooltip = Game1.LanguageManager.GetString("tooltip_controls_resetlayout", "error"); break; }
                }
            }
            // If the index is above 0 just use the index directly.
            else
            {
                // Use the selected index to determine which tooltip to show.
                switch (index) 
                {
                    case 0:  { tooltip = Game1.LanguageManager.GetString("tooltip_controls_touchgridsize", "error"); break; }
                    case 2:  { tooltip = Game1.LanguageManager.GetString("tooltip_controls_onscreenpad", "error"); break; }
                    case 3:  { tooltip = Game1.LanguageManager.GetString("tooltip_controls_touchmovement", "error"); break; }
                    case 4:  { tooltip = Game1.LanguageManager.GetString("tooltip_controls_fadeopacity", "error"); break; }
                    case 5:  { tooltip = Game1.LanguageManager.GetString("tooltip_controls_shadowopacity", "error"); break; }
                    case 6:  { tooltip = Game1.LanguageManager.GetString("tooltip_controls_onscreenscale", "error"); break; }
                    case 7:  { tooltip = Game1.LanguageManager.GetString("tooltip_controls_onscreenanalogbuttons", "error"); break; }
                }
            }
            // Display the tooltip in the tooltip window.
            return tooltip;
        }
    }
}
