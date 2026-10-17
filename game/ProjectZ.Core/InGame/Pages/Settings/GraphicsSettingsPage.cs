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
    internal class GraphicsSettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _graphicsSettingsLayout;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        private readonly InterfaceSlider     _sliderSeqAmplifier;
        private readonly InterfaceListLayout _toggleColorCorrection;
        private readonly InterfaceListLayout _toggleDynamicShadows;
        private readonly InterfaceListLayout _toggleFogEffects;
        private readonly InterfaceListLayout _toggleGlobalLighting;
        private readonly InterfaceListLayout _toggleObjectLighting;
        private readonly InterfaceListLayout _toggleScreenShake;
        private readonly InterfaceListLayout _toggleExScreenShake;
        private readonly InterfaceListLayout _toggleClassicSprites;

        List<string> _tooltips = new List<string>();
        private bool _showTooltip;

        public void SetSeqScaleAmplifier(int value) { _sliderSeqAmplifier.SetValue(value); }
        public void SetColorCorrection(bool state)  { _toggleColorCorrection.Toggle.SetValue(state); }
        public void SetDynamicShadows(bool state)   { _toggleDynamicShadows.Toggle.SetValue(state); }
        public void SetForestFogEffects(bool state) { _toggleFogEffects.Toggle.SetValue(state); }
        public void SetGlobalLighting(bool state)   { _toggleGlobalLighting.Toggle.SetValue(state); }
        public void SetObjectLighting(bool state)   { _toggleObjectLighting.Toggle.SetValue(state); }
        public void SetCamScreenShake(bool state)   { _toggleScreenShake.Toggle.SetValue(state); }
        public void SetCamExScreenShake(bool state) { _toggleExScreenShake.Toggle.SetValue(state); }
        public void SetClassicSprites(bool state)   { _toggleClassicSprites.Toggle.SetValue(state); }

        public GraphicsSettingsPage(int width, int height)
        {
            EnableTooltips = true;
            var buttonWidth = 320;
            var buttonHeight = 14;
            var sliderHeight = 12;

            // Graphics Settings Layout
            _graphicsSettingsLayout = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _graphicsSettingsLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_graphics_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top, Scrollable = true };

            // Slider: Sequence Scale Amplifier
            _sliderSeqAmplifier = new InterfaceSlider("settings_graphics_sequencescale",
                buttonWidth, sliderHeight, new Point(1, 2), 0, 3, 1, GameSettings.SeqScaleAmplify, 
                number => { GameSettings.SeqScaleAmplify = number; })
                { SetString = number => SequenceScaleSliderAdjustmentString(number) };
            _contentLayout.AddElement(_sliderSeqAmplifier);
            _tooltips.Add("tooptip_graphics_sequencescale");

            // Toggle: Color Correction
            _toggleColorCorrection = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_colorcorrect", GameSettings.ColorCorrection,
                newState => GameSettings.ColorCorrection = newState);
            _contentLayout.AddElement(_toggleColorCorrection);
            _tooltips.Add("tooltip_graphics_colorcorrect");

            // Toggle: Dynamic Shadows
            _toggleDynamicShadows = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_shadow", GameSettings.EnableShadows,
                newState => GameSettings.EnableShadows = newState);
            _contentLayout.AddElement(_toggleDynamicShadows);
            _tooltips.Add("tooltip_graphics_shadows");

            // Toggle: Fog Effects
            _toggleFogEffects = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_fogeffects", GameSettings.FogEffects,
                newState => GameSettings.FogEffects = newState);
            _contentLayout.AddElement(_toggleFogEffects);
            _tooltips.Add("tooltip_graphics_fogeffects");

            // Toggle: Global Lighting
            _toggleGlobalLighting = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_globallights", GameSettings.GlobalLights,
                newState => GameSettings.GlobalLights = newState);
            _contentLayout.AddElement(_toggleGlobalLighting);
            _tooltips.Add("tooltip_graphics_nogloballights");

            // Toggle: Object Lighting
            _toggleObjectLighting = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_objectlights", GameSettings.ObjectLights,
                newState => GameSettings.ObjectLights = newState);
            _contentLayout.AddElement(_toggleObjectLighting);
            _tooltips.Add("tooltip_graphics_noobjectlights");

            // Toggle: Screen-Shake
            _toggleScreenShake = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_screenshake", GameSettings.ScreenShake, 
                newState => { GameSettings.ScreenShake = newState; });
            _contentLayout.AddElement(_toggleScreenShake);
            _tooltips.Add("tooltip_graphics_screenshake");

            // Toggle: Extra Screen-Shake
            _toggleExScreenShake = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_exscreenshake", GameSettings.ExScreenShake, 
                newState => { GameSettings.ExScreenShake = newState; });
            _contentLayout.AddElement(_toggleExScreenShake);
            _tooltips.Add("tooltip_graphics_exscreenshake");

            // Toggle: Classic Item Sprites
            _toggleClassicSprites = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_classicsprites", GameSettings.ClassicSprites, 
                newState => { PressButtonToggleClassicItemSprites(newState); });
            _contentLayout.AddElement(_toggleClassicSprites);
            _tooltips.Add("tooltip_graphics_classicsprites");

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _graphicsSettingsLayout.AddElement(_contentLayout);
            _graphicsSettingsLayout.AddElement(_bottomBar);
            PageLayout = _graphicsSettingsLayout;
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

        private string SequenceScaleSliderAdjustmentString(int number)
        {
            return ": +" + number;
        }

        public void PressButtonToggleClassicItemSprites(bool newState) 
        {
            // Toggle the setting with the new value.
            GameSettings.ClassicSprites = newState;

            // Get both Link and the boomerang object.
            var Link = MapManager.ObjLink;
            var Boomerang = MapManager.ObjLink.Boomerang;

            // If they have been created already toggle the animator used.
            if (Link != null && Boomerang != null)
                Boomerang.ToggleAnimator(newState);

            // Rebuild the item list to swap out sprites.
            Game1.GameManager.ItemManager.Load();
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_graphicsSettingsLayout, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
