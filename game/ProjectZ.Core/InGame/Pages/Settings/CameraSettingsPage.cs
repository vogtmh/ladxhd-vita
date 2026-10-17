using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class CameraSettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _cameraOptionsList;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;
        private readonly InterfaceSlider     _sliderCameraType;
        private readonly InterfaceSlider     _sliderFogEdgeSharp;
        private readonly InterfaceSlider     _sliderFogOpacity;
        private readonly InterfaceSlider     _sliderFogFadeTime;
        private readonly InterfaceSlider     _sliderCameraBorder;
        private readonly InterfaceSlider     _sliderBorderOpacity;
        private readonly InterfaceSlider     _sliderBorderBias;
        private readonly InterfaceButton     _buttonCustomSettings;
        private readonly InterfaceListLayout _toggleModernFogOfWar;
        private readonly InterfaceListLayout _toggleModernFogDungeon;
        private readonly InterfaceListLayout _toggleClassicScrolling;
        private readonly InterfaceListLayout _toggleClassicScaling;
        private readonly InterfaceListLayout _toggleClassicItemDrops;
        private readonly InterfaceListLayout _toggleCameraLock;
        private readonly InterfaceListLayout _toggleCameraSmooth;

        List<string> _tooltips = new List<string>();
        private bool _showTooltip;
        private int _lastCameraSetting;

        public void SetCameraActiveType(int value)   { _sliderCameraType.SetValue(value); }
        public void SetModernFogEnabled(bool state)  { _toggleModernFogOfWar.Toggle.SetValue(state); }
        public void SetModernFogDungeons(bool state) { _toggleModernFogDungeon.Toggle.SetValue(state); }
        public void SetModernFogSharpness(int value) { _sliderFogEdgeSharp.SetValue(value); }
        public void SetModernFogOpacity(int value)   { _sliderFogOpacity.SetValue(value); }
        public void SetModernFogFadeTime(int value)  { _sliderFogFadeTime.SetValue(value); }
        public void SetClassicCamBorder(int value)   { _sliderCameraBorder.SetValue(value); }
        public void SetClassicBorderAlpha(int value) { _sliderBorderOpacity.SetValue(value); }
        public void SetClassicBorderBias(int value)  { _sliderBorderBias.SetValue(value); }
        public void SetClassicScrolling(bool state)  { _toggleClassicScrolling.Toggle.SetValue(state); }
        public void SetClassicScaleLock(bool state)  { _toggleClassicScaling.Toggle.SetValue(state); }
        public void SetClassicItemDrops(bool state)  { _toggleClassicItemDrops.Toggle.SetValue(state); }
        public void SetCameraLock(bool state)        { _toggleCameraLock.Toggle.SetValue(state); }
        public void SetCameraSmoothCam(bool state)   { _toggleCameraSmooth.Toggle.SetValue(state); }

        public CameraSettingsPage(int width, int height)
        {
            EnableTooltips = true;
            var buttonWidth = 320;
            var buttonHeight = 14;
            var sliderHeight = 12;

            // Camera Settings Layout
            _cameraOptionsList = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _cameraOptionsList.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_camera_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top, Scrollable = true };

            // Slider: Camera Type
            _sliderCameraType = new InterfaceSlider("settings_camera_cameratype",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 2, 1, GameSettings.CameraMode, 
                number => { GameSettings.CameraMode = number; Game1.ScaleChanged = true; Camera.SnapCameraTimer = 20f; UpdateCameraSettings(false); }) 
                { SetString = number => SetCameraSetting(number) };

            // Button: Custom Camera Settings
            _buttonCustomSettings = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(1, 2), 
                "settings_camera_custommenu", element => { Game1.UiPageManager.ChangePage(typeof(CameraCustomPage)); });

            // Toggle: Modern Map Discovery Fog
            _toggleModernFogOfWar = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_camera_fogdiscovery", GameSettings.ModernFogEnabled, 
                newState => { GameSettings.ModernFogEnabled = newState; });

            // Toggle: Modern Fog in Dungeons
            _toggleModernFogDungeon = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_camera_fogdungeons", GameSettings.ModernFogDungeon, 
                newState => { GameSettings.ModernFogDungeon = newState; });

            // Slider: Modern Fog Edge Sharpness
            _sliderFogEdgeSharp = new InterfaceSlider("settings_camera_fogedgesharp",
                buttonWidth, sliderHeight, new Point(1, 2), 1, 100, 1, (int)MathF.Round(GameSettings.ModernFogSharp * 100),
                number => { GameSettings.ModernFogSharp = (float)(number * 0.01); })
                { SetString = number => SetPercentageValueText(number) };

            // Slider: Modern Fog Opacity
            _sliderFogOpacity = new InterfaceSlider("settings_camera_fogopacity",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 100, 1, (int)MathF.Round(GameSettings.ModernFogOpacity * 100),
                number => { GameSettings.ModernFogOpacity = (float)(number * 0.01); })
                { SetString = number => SetPercentageValueText(number) };

            // Slider: Modern Fog Fade Time
            _sliderFogFadeTime = new InterfaceSlider("settings_camera_fogfadetime",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 2000, 25, GameSettings.ModernFogFade,
                number => { GameSettings.ModernFogFade = number; })
                { SetString = number => ModernFogFadeTimeAdjustment(number) };

            // Slider: Classic Camera Border
            _sliderCameraBorder = new InterfaceSlider("settings_camera_camborder",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 2, 1, GameSettings.ClassicBorder, 
                number => { GameSettings.ClassicBorder = number; Game1.ScaleChanged = true; Camera.SnapCameraTimer = 20f; }) 
                { SetString = number => ClassicBorderAdjustment(number) };

            // Slider: Classic Border Blackout Amount
            _sliderBorderOpacity = new InterfaceSlider("settings_camera_blackpercent",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 100, 5, (int)MathF.Round(GameSettings.ClassicAlpha * 100),
                number => { GameSettings.ClassicAlpha = (float)(number * 0.01); })
                { SetString = number => SetPercentageValueText(number) };

            // Slider: Classic Border Bias
            _sliderBorderBias = new InterfaceSlider("settings_camera_bias",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 2, 1, GameSettings.ClassicBias,
                number => { GameSettings.ClassicBias = number; })
                { SetString = number => ClassicBorderBiasAdjustment(number) };

            // Toggle: Classic Scrolling
            _toggleClassicScrolling = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_camera_classicscrolling", GameSettings.ClassicScrolling, 
                newState => { GameSettings.ClassicScrolling = newState; Game1.ScaleChanged = true; Camera.SnapCameraTimer = 20f; });

            // Toggle: Classic Scale Lock
            _toggleClassicScaling = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_camera_classicscaling", GameSettings.ClassicScaling, 
                newState => { GameSettings.ClassicScaling = newState; Game1.ScaleChanged = true; Camera.SnapCameraTimer = 20f; });

            // Toggle: Classic Persistent Item Drops
            _toggleClassicItemDrops = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_camera_persistentitems", GameSettings.ClassicItemDrop, 
                newState => { GameSettings.ClassicItemDrop = newState; });

            // Toggle: Camera Lock
            _toggleCameraLock = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_camera_cameralock", GameSettings.CameraLock, 
                newState => { GameSettings.CameraLock = newState; ReloadVirtualController(); });

            // Toggle: Smooth Camera
            _toggleCameraSmooth = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_camera_smoothcamera", GameSettings.SmoothCamera, 
                newState => { GameSettings.SmoothCamera = newState; });

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _cameraOptionsList.AddElement(_contentLayout);
            _cameraOptionsList.AddElement(_bottomBar);
            PageLayout = _cameraOptionsList;

            // Camera Type is always present on all menus.
            _contentLayout.AddElement(_sliderCameraType);

            // Update the camera settings and button colors.
            UpdateCameraSettings(true);
            UpdateInterfaceColors();
        }

        private string SetCameraSetting(int number)
        {
            // The camera has changed so the game scale must also be upated.
            Game1.ScaleChanged = true;

            // Toggling classic camera "grays out" some options depending on its state.
            UpdateInterfaceColors();

            // Set the text to the camera selected.
            return ": " + number switch
            {
                0 => Game1.LanguageManager.GetString("settings_camera_cameratypeA", "error"),
                1 => Game1.LanguageManager.GetString("settings_camera_cameratypeB", "error"),
                2 => Game1.LanguageManager.GetString("settings_camera_cameratypeC", "error"),
                _ => throw new System.Runtime.CompilerServices.SwitchExpressionException()
            };
        }

        public void UpdateCameraSettings(bool firstRun)
        {
            // Don't try to do anything unless the settings actually changed.
            if (!firstRun && _lastCameraSetting == GameSettings.CameraMode)
                return;

            // Clear the tooltips array and add back the camera type option.
            _tooltips.Clear();
            _tooltips.Add("tooltip_camera_cameratype");

            // Remove all the potentially shown options.
            _contentLayout.RemoveElement(_buttonCustomSettings);
            _contentLayout.RemoveElement(_toggleModernFogOfWar);
            _contentLayout.RemoveElement(_toggleModernFogDungeon);
            _contentLayout.RemoveElement(_sliderFogEdgeSharp);
            _contentLayout.RemoveElement(_sliderFogOpacity);
            _contentLayout.RemoveElement(_sliderFogFadeTime);
            _contentLayout.RemoveElement(_sliderCameraBorder);
            _contentLayout.RemoveElement(_sliderBorderOpacity);
            _contentLayout.RemoveElement(_sliderBorderBias);
            _contentLayout.RemoveElement(_toggleClassicScrolling);
            _contentLayout.RemoveElement(_toggleClassicScaling);
            _contentLayout.RemoveElement(_toggleClassicItemDrops);
            _contentLayout.RemoveElement(_toggleCameraLock);
            _contentLayout.RemoveElement(_toggleCameraSmooth);

            // Modern Camera: Add modern settings only.
            if (GameSettings.CameraMode == 0)
            {
                _contentLayout.AddElement(_toggleModernFogOfWar);
                _contentLayout.AddElement(_toggleModernFogDungeon);
                _contentLayout.AddElement(_sliderFogEdgeSharp);
                _contentLayout.AddElement(_sliderFogOpacity);
                _contentLayout.AddElement(_sliderFogFadeTime);
                _contentLayout.AddElement(_toggleCameraLock);
                _contentLayout.AddElement(_toggleCameraSmooth);
                _tooltips.Add("tooltip_camera_fogdiscovery");
                _tooltips.Add("tooltip_camera_fogdungeons");
                _tooltips.Add("tooltip_camera_fogedgesharp");
                _tooltips.Add("tooltip_camera_fogopacity");
                _tooltips.Add("tooltip_camera_fogfadetime");
                _tooltips.Add("tooltip_camera_cameralock");
                _tooltips.Add("tooltip_camera_smoothcamera");
            }
            // Classic Camera: Add classic settings only.
            else if (GameSettings.CameraMode == 1)
            {
                _contentLayout.AddElement(_sliderCameraBorder);
                _contentLayout.AddElement(_sliderBorderOpacity);
                _contentLayout.AddElement(_sliderBorderBias);
                _contentLayout.AddElement(_toggleClassicScrolling);
                _contentLayout.AddElement(_toggleClassicScaling);
                _contentLayout.AddElement(_toggleClassicItemDrops);
                _contentLayout.AddElement(_toggleCameraSmooth);
                _tooltips.Add("tooltip_camera_camborder");
                _tooltips.Add("tooltip_camera_blackpercent");
                _tooltips.Add("tooltip_camera_bias");
                _tooltips.Add("tooltip_camera_classicscrolling");
                _tooltips.Add("tooltip_camera_classicscaling");
                _tooltips.Add("tooltip_camera_persistentitems");
                _tooltips.Add("tooltip_camera_smoothcamera");
            }
            // Custom Camera: Add all camera settings at once.
            else if (GameSettings.CameraMode == 2)
            {
                _contentLayout.AddElement(_buttonCustomSettings);
                _contentLayout.AddElement(_toggleModernFogOfWar);
                _contentLayout.AddElement(_toggleModernFogDungeon);
                _contentLayout.AddElement(_sliderFogEdgeSharp);
                _contentLayout.AddElement(_sliderFogOpacity);
                _contentLayout.AddElement(_sliderFogFadeTime);
                _contentLayout.AddElement(_sliderCameraBorder);
                _contentLayout.AddElement(_sliderBorderOpacity);
                _contentLayout.AddElement(_sliderBorderBias);
                _contentLayout.AddElement(_toggleClassicScrolling);
                _contentLayout.AddElement(_toggleClassicScaling);
                _contentLayout.AddElement(_toggleClassicItemDrops);
                _contentLayout.AddElement(_toggleCameraLock);
                _contentLayout.AddElement(_toggleCameraSmooth);
                _tooltips.Add("tooltip_camera_custommenu");
                _tooltips.Add("tooltip_camera_fogdiscovery");
                _tooltips.Add("tooltip_camera_fogdungeons");
                _tooltips.Add("tooltip_camera_fogedgesharp");
                _tooltips.Add("tooltip_camera_fogopacity");
                _tooltips.Add("tooltip_camera_fogfadetime");
                _tooltips.Add("tooltip_camera_camborder");
                _tooltips.Add("tooltip_camera_blackpercent");
                _tooltips.Add("tooltip_camera_bias");
                _tooltips.Add("tooltip_camera_classicscrolling");
                _tooltips.Add("tooltip_camera_classicscaling");
                _tooltips.Add("tooltip_camera_persistentitems");
                _tooltips.Add("tooltip_camera_cameralock");
                _tooltips.Add("tooltip_camera_smoothcamera");
            }
            // Store the last used camera mode.
            _lastCameraSetting = GameSettings.CameraMode;
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

        public void ReloadVirtualController()
        {
            if (Game1.PlatformInput.HasTouchInput)
                VirtualController.Initialize(Game1.WindowWidth, Game1.WindowHeight, true);
        }

        public void UpdateInterfaceColors()
        {
            // Gray out options based on camera. No options get grayed out when set to "Customize Camera".
            bool modern = GameSettings.CameraMode != 1;
            bool classic = GameSettings.CameraMode != 0;

            // The only option that grays out when set to "Classic".
            _toggleCameraLock.ToggleElementColors(modern);

            // These options get grayed out when set to "Modern".
            _toggleClassicScaling.ToggleElementColors(classic);
            _sliderCameraBorder.ToggleSliderColors(classic);
            _sliderBorderOpacity.ToggleSliderColors(classic);
            _sliderBorderBias.ToggleSliderColors(classic);
        }

        private string SetPercentageValueText(int number)
        {
            return ": " + number + "%";
        }

        private string ModernFogFadeTimeAdjustment(int number)
        {
            return ": " + number + "ms";
        }

        private string ClassicBorderAdjustment(int number)
        {
            return ": " + number switch
            {
                0 => Game1.LanguageManager.GetString("tooltip_camera_camborderA", "error"),
                1 => Game1.LanguageManager.GetString("tooltip_camera_camborderB", "error"),
                2 => Game1.LanguageManager.GetString("tooltip_camera_camborderC", "error"),
                _ => Game1.LanguageManager.GetString("tooltip_camera_camborderA", "error")
            };
        }

        private string ClassicBorderBiasAdjustment(int number)
        {
            return ": " + number switch
            {
                0 => Game1.LanguageManager.GetString("tooltip_camera_biasA", "error"),
                1 => Game1.LanguageManager.GetString("tooltip_camera_biasB", "error"),
                2 => Game1.LanguageManager.GetString("tooltip_camera_biasC", "error"),
                _ => Game1.LanguageManager.GetString("tooltip_camera_biasA", "error")
            };
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_cameraOptionsList, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
