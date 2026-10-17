using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class ControlSettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _controlSettingsList;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        private readonly InterfaceSlider     _sliderDeadZone;
        private readonly InterfaceButton     _controllerType;
        private readonly InterfaceListLayout _toggleTriggersScale;
        private readonly InterfaceListLayout _toggleSixButtons;
        private readonly InterfaceListLayout _toggleSwapButtons;
        private readonly InterfaceListLayout _toggleClassicMove;
        private readonly InterfaceListLayout _toggleDigitalAnalog;

        private float _controlCooldown = 0f;
        List<string> _tooltips = new List<string>();
        private bool _showTooltip;

        public void SetDeadZoneValue(int value)  { _sliderDeadZone.SetValue(value); }
        public void SetTriggerScale(bool state)  { _toggleTriggersScale.Toggle.SetValue(state); }
        public void SetSixButtons(bool state)    { _toggleSixButtons.Toggle.SetValue(state); }
        public void SetSwapButtons(bool state)   { _toggleSwapButtons.Toggle.SetValue(state); ControlHandler.SetConfirmCancelButtons(); }
        public void SetClassicMove(bool state)   { _toggleClassicMove.Toggle.SetValue(state); }
        public void SetDigitalAnalog(bool state) { _toggleDigitalAnalog.Toggle.SetValue(state); }

        public void UpdateControllerOverrideText()
        {
            // The "OverrideText" is stored so if the language is changed then the text also needs to be updated.
            string UpdateText = Game1.LanguageManager.GetString("settings_controls_gamepad", "error") + ": " + GameSettings.Controller;

            // Update the label with the properly translated textu.
            _controllerType.InsideLabel.OverrideText = UpdateText;
        }

        public ControlSettingsPage(int width, int height)
        {
            
            EnableTooltips = true;
            var compact = Game1.PlatformPresentation.UseCompactMenus;
            var buttonWidth = 320;
            var buttonHeight = compact ? 12 : 14;
            var sliderHeight = compact ? 10 : 12;

            // Control Settings Layout
            _controlSettingsList = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _controlSettingsList.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_controls_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top, Scrollable = compact ? true : false };

            // Slider: Deadzone
            _sliderDeadZone = new InterfaceSlider("settings_controls_deadzone", 
                buttonWidth, sliderHeight, new Point(1, 2), 0, 100, 1, (int)MathF.Round(GameSettings.DeadZone * 100),
                number => { GameSettings.DeadZone = (float)(number * 0.01); })
                { SetString = number => ": " + number + "%" };
            _contentLayout.AddElement(_sliderDeadZone);
            _tooltips.Add("tooltip_controls_deadzone");

            if (Game1.PlatformInput.HasTouchInput)
            {
                _contentLayout.AddElement(new InterfaceButton(
                    new Point(buttonWidth, buttonHeight), new Point(1, 2),
                    "settings_controls_onscreen", element => { Game1.UiPageManager.ChangePage(typeof(ControlOnScreenPage)); }));
                _tooltips.Add("tooltip_controls_onscreen");
            }

            // Button: Controller Type
            _contentLayout.AddElement(_controllerType = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "", PressButtonSetController));
            UpdateControllerOverrideText();
            _tooltips.Add("tooltip_controls_gamepad");

            // Button: Remap Settings
            _contentLayout.AddElement(new InterfaceButton(
                new Point(buttonWidth, buttonHeight), new Point(1, 2), 
                "settings_controls_remap", element => { Game1.UiPageManager.ChangePage(typeof(ControlMappingPage)); }));
            _tooltips.Add("tooltip_controls_remap");

            // Toggle: Triggers Scale Game
            _toggleTriggersScale = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_controls_triggersscale", GameSettings.TriggersScale, 
                newState => { GameSettings.TriggersScale = newState; ReloadVirtualController(); });
            _contentLayout.AddElement(_toggleTriggersScale);
            _tooltips.Add("tooltip_controls_triggersscale");

            // Toggle: Toggle Six Buttons
            _toggleSixButtons = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_controls_sixbuttons", GameSettings.SixButtons, 
                newState => { UpdateSixButtonToggle(newState); ReloadVirtualController(); });
            _contentLayout.AddElement(_toggleSixButtons);
            _tooltips.Add("tooltip_controls_sixbuttons");

            // Toggle: Swap Confirm & Cancel
            _toggleSwapButtons = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_controls_swapbuttons", GameSettings.SwapButtons, 
                newState => { _controlCooldown = 500f; GameSettings.SwapButtons = newState; ControlHandler.SetConfirmCancelButtons(); });
            _contentLayout.AddElement(_toggleSwapButtons);
            _tooltips.Add("tooltip_controls_swapconfirm");

            // Toggle: Classic Movement
            _toggleClassicMove = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_controls_classicmove", GameSettings.OldMovement, 
                newState => { GameSettings.OldMovement = newState; });
            _contentLayout.AddElement(_toggleClassicMove);
            _tooltips.Add("tooltip_controls_classicmove");

            // Toggle: Digital Analog
            _toggleDigitalAnalog = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_controls_digitalanalog", GameSettings.DigitalAnalog, 
                newState => { GameSettings.DigitalAnalog = newState; });
            _contentLayout.AddElement(_toggleDigitalAnalog);
            _tooltips.Add("tooltip_controls_digitalanalog");

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _controlSettingsList.AddElement(_contentLayout);
            _controlSettingsList.AddElement(_bottomBar);
            PageLayout = _controlSettingsList;
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            base.Update(pressedButtons, gameTime);

            if (_controlCooldown > 0f)
                _controlCooldown -= Game1.DeltaTime;

            // The back button was pressed.
            if (_controlCooldown <= 0f && ControlHandler.ButtonPressed(ControlHandler.CancelButton))
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

        public void PressButtonSetController(InterfaceElement element)
        {
            // Push forward the index +1 and loop back around.
            int index = Array.IndexOf(ControlHandler.ControllerNames, GameSettings.Controller);
            index = (index + 1) % ControlHandler.ControllerNames.Length;
            GameSettings.Controller = ControlHandler.ControllerNames[index];
            ControlHandler.SetControllerIndex();

            // Override the button text with this fancy hack.
            UpdateControllerOverrideText();

            // Update the buttons on the controller page.
            ControlMappingPage.UpdateLabels();
        }

        public void ReloadVirtualController()
        {
            if (Game1.PlatformInput.HasTouchInput)
                VirtualController.Initialize(Game1.WindowWidth, Game1.WindowHeight, true);
        }
        
        public void UpdateSixButtonToggle(bool newState)
        {
            // Enable or disable the six inventory button state.
            GameSettings.SixButtons = newState;

            // The number of inventory slots needs to be upated now so the game knows to enable/disable the top front buttons immediately.
            Values.HandItemSlots = newState ? 6 : 4;

            // Update the number of equippable buttons.
            Game1.GameManager.InGameOverlay.UpdateInventoryButtons(newState);

            // If currently in-game then update equipment. Fixes sword/shield remaining or not being equipped if equipped to L/R buttons.
            if (Game1.InProgress)
                Game1.GameManager.UpdateEquipment();

            // Move the LB and RB buttons near the face buttons on the virtual controller.
            ReloadVirtualController();
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_controlSettingsList, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
