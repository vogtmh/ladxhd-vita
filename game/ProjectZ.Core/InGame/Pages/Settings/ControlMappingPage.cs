using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Base;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;
using static ProjectZ.InGame.Interface.InterfaceElement;

namespace ProjectZ.InGame.Pages
{
    class ControlMappingPage : InterfacePage
    {
        private readonly InterfaceListLayout[] _remapButtons;
        private readonly InterfaceListLayout _bottomBar;

        // Being able to reference a static field makes updating the label text much easier down the road.
        public static InterfaceLabel[] _buttonLabels = new InterfaceLabel[14];
        private InterfaceLabel _remapTimerLabel;

        private CButtons _selectedButton;
        private bool _updateButton;
        private double _remapTimer;
        private const double RemapTimeout = 5.0;
        private int _lastControllerIndex = ControlHandler.ControllerIndex;

        // The dummy button is created just to extract the color from it.
        private InterfaceButton _dummyButton = new InterfaceButton();
        private Color _rowHeaderColor;

        public ControlMappingPage(int width, int height)
        {
            // Get the color from the dummy button.
            _rowHeaderColor = _dummyButton.Color;

            // Control Settings Layout
            var controlLayout = new InterfaceListLayout { Size = new Point(width, height - 16), Selectable = true };
            controlLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_controls_remapheader", new Point(width - 50, (int)(height * Values.MenuHeaderSize)), new Point(0, -10)));

            var controllerHeight = (int)(height * Values.MenuContentSize);

            var buttonWidth  = 65;
            var labelWidth   = 117;
            var labelHeight  = 10;
            var headerHeight = 12;
            var timerWidth   = 25;

            var remapHeader = new InterfaceListLayout { AutoSize = true, Margin = new Point(0, 1), HorizontalMode = true, CornerRadius = 0, Color = _rowHeaderColor };
            remapHeader.AddElement(new InterfaceListLayout() { Size = new Point(buttonWidth, headerHeight) });
            remapHeader.AddElement(new InterfaceLabel("settings_controls_keyboad", new Point(labelWidth, headerHeight), new Point(0, 0)));
            remapHeader.AddElement(new InterfaceLabel("settings_controls_gamepad", new Point(labelWidth, headerHeight), new Point(0, 0)));
            remapHeader.AddElement(new InterfaceLabel("", new Point(timerWidth, headerHeight), new Point(0, 0)));
            controlLayout.AddElement(remapHeader);

            var remapButtons = new InterfaceListLayout { AutoSize = true, Margin = new Point(2, 0), Selectable = true };
            _remapButtons = new InterfaceListLayout[Enum.GetValues(typeof(CButtons)).Length - 3];
            var index = 0;

            foreach (CButtons eButton in Enum.GetValues(typeof(CButtons)))
            {
                if (eButton == CButtons.None || eButton == CButtons.LS || eButton == CButtons.RS)
                    continue;

                // Override the button text when we reach the face and top buttons.
                string overrideText = "";
                if (index is >= 4 and <= 13)
                    overrideText =  ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, index - 4];

                // Most buttons are pulled from language files except for when override text is not empty.
                _remapButtons[index] = new InterfaceListLayout { Size = new Point(buttonWidth + labelWidth * 2 + timerWidth, labelHeight), HorizontalMode = true };

                _remapButtons[index].AddElement(_buttonLabels[index] = new InterfaceLabel("settings_controls_" + eButton, new Point(buttonWidth, labelHeight), Point.Zero)
                    { CornerRadius = 0, Color = _rowHeaderColor, OverrideText = overrideText });

                _remapButtons[index].AddElement(new InterfaceLabel("error", new Point(labelWidth, labelHeight), new Point(0, 0)) { Translate = false });
                _remapButtons[index].AddElement(new InterfaceLabel("error", new Point(labelWidth, labelHeight), new Point(0, 0)) { Translate = false });
                _remapButtons[index].AddElement(new InterfaceLabel("error", new Point(timerWidth, labelHeight), new Point(0, 0)) { TextAlignment = Gravities.Left, Translate = false });

                var capturedIndex = index;
                var remapButton = new InterfaceButton(new Point(buttonWidth + labelWidth * 2 + timerWidth, labelHeight), new Point(0, 0), _remapButtons[index],
                    element => { StartButtonRemap(eButton, capturedIndex); })
                    { CornerRadius = 0, Color = Color.Transparent };

                remapButtons.AddElement(remapButton);
                remapButtons.AddElement(new InterfaceListLayout() { Size = new Point(1, 1) });

                index++;
            }
            // Bottom Bar / Reset Button / Back Button:
            _bottomBar = new InterfaceListLayout { Size = new Point(width - 50, (int)(height * Values.MenuFooterSize) - 20), HorizontalMode = true, Selectable = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(64, 18), new Point(2, 4), "settings_controls_style_01", SetControlStyle1));
            _bottomBar.AddElement(new InterfaceButton(new Point(64, 18), new Point(2, 4), "settings_controls_style_02", SetControlStyle2));
            _bottomBar.AddElement(new InterfaceButton(new Point(64, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            controlLayout.AddElement(remapButtons);
            controlLayout.AddElement(_bottomBar);
            PageLayout = controlLayout;

            // Force an update of the UI.
            UpdateUi();
        }

        private void StartButtonRemap(CButtons eButton, int index)
        {
            _updateButton = true;
            _selectedButton = eButton;
            _remapTimer = RemapTimeout;
            _remapTimerLabel = (InterfaceLabel)_remapButtons[index].Elements[3];
            ((InterfaceLabel)_remapButtons[index].Elements[1]).SetText("??");
            ((InterfaceLabel)_remapButtons[index].Elements[2]).SetText("??");
            _remapTimerLabel.SetText(((int)_remapTimer).ToString());
        }

        public static void UpdateLabels()
        {
            for (int index = 0; index < _buttonLabels.Length ; index++)
            {
                string overrideText = "";

                if (index is >= 4 and <= 13)
                    overrideText = ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, index - 4];

                if (overrideText != "")
                    _buttonLabels[index].OverrideText = overrideText;
            }
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            // We only want to force an update if the controller has changed.
            if (_lastControllerIndex != ControlHandler.ControllerIndex)
            {
                UpdateUi();
                _lastControllerIndex = ControlHandler.ControllerIndex;
            }

            // the left button is always the first one selected
            _bottomBar.Deselect(false);
            _bottomBar.Select(Directions.Right, false);
            _bottomBar.Deselect(false);

            PageLayout.Deselect(false);
            PageLayout.Select(Directions.Top, false);
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            if (_updateButton)
            {
                _remapTimer -= gameTime.ElapsedGameTime.TotalSeconds;

                _remapTimerLabel.SetText(((int)_remapTimer + 1).ToString());

                var pressedKeys = InputHandler.GetPressedKeys();
                if (pressedKeys.Count > 0)
                {
                    _updateButton = false;
                    var currentKey = ControlHandler.ButtonDictionary[_selectedButton].Keys[0];
                    if (pressedKeys[0] != currentKey)
                        UpdateKeyboard(_selectedButton, pressedKeys[0]);
                    UpdateUi();
                }

                var pressedGamepadButtons = InputHandler.GetPressedButtons();
                if (pressedGamepadButtons.Count > 0)
                {
                    _updateButton = false;
                    var currentButton = ControlHandler.ButtonDictionary[_selectedButton].Buttons[0];
                    if (pressedGamepadButtons[0] != currentButton)
                        UpdateButton(_selectedButton, pressedGamepadButtons[0]);
                    UpdateUi();
                }

                if (_remapTimer <= 0)
                {
                    _updateButton = false;
                    UpdateUi();
                }

                InputHandler.ResetInputState();

                // Only let the UI process input if we are still waiting for a remap
                if (_updateButton)
                    base.Update(pressedButtons, gameTime);
            }
            else
            {
                base.Update(pressedButtons, gameTime);

                if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                    Game1.UiPageManager.PopPage();
            }
        }

        public void UpdateUi()
        {
            var buttonNr = 0;

            // This method is responsible for displaying the keyboard and controller buttons.
            foreach (var bEntry in ControlHandler.ButtonDictionary)
            {
                if (bEntry.Key == CButtons.LS || bEntry.Key == CButtons.RS)
                    continue;

                var str = "";

                for (var j = 0; j < bEntry.Value.Keys.Length; j++)
                    str += bEntry.Value.Keys[j];

                ((InterfaceLabel)_remapButtons[buttonNr].Elements[1]).SetText(str);

                str = " ";
                for (var j = 0; j < bEntry.Value.Buttons.Length; j++)
                    str += ControlHandler.GetButtonName(bEntry.Value.Buttons[j]);

                ((InterfaceLabel)_remapButtons[buttonNr].Elements[2]).SetText(str);
                ((InterfaceLabel)_remapButtons[buttonNr].Elements[3]).SetText("");

                buttonNr++;
            }
        }

        private void UpdateKeyboard(CButtons buttonIndex, Keys newKey)
        {
            foreach (var button in ControlHandler.ButtonDictionary)
                if (button.Value.Keys[0] == newKey && button.Key != buttonIndex)
                    button.Value.Keys[0] = ControlHandler.ButtonDictionary[_selectedButton].Keys[0];

            ControlHandler.ButtonDictionary[_selectedButton].Keys = new Keys[] { newKey };
        }

        private void UpdateButton(CButtons buttonIndex, Buttons newButton)
        {
            foreach (var button in ControlHandler.ButtonDictionary)
                if (button.Value.Buttons[0] == newButton && button.Key != buttonIndex)
                    button.Value.Buttons[0] = ControlHandler.ButtonDictionary[_selectedButton].Buttons[0];

            ControlHandler.ButtonDictionary[_selectedButton].Buttons = new Buttons[] { newButton };
        }

        private void SetControlStyle1(InterfaceElement element)
        {
            ControlHandler.SetControlStyle1();
            UpdateUi();
            InputHandler.ResetInputState();
        }

        private void SetControlStyle2(InterfaceElement element)
        {
            ControlHandler.SetControlStyle2();
            UpdateUi();
            InputHandler.ResetInputState();
        }

    }
}
