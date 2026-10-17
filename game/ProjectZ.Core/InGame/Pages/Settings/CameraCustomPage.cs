using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class CameraCustomPage : InterfacePage
    {
        private readonly InterfaceListLayout _cameraCustomList;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        private readonly InterfaceButton _buttonOverworldCamera;
        private readonly InterfaceButton _buttonHousesCamera;
        private readonly InterfaceButton _buttonCavesCamera;
        private readonly InterfaceButton _buttonDungeonsCamera;
        private readonly InterfaceButton _buttonCastleCamera;
        private readonly InterfaceButton _buttonEggCamera;
        private readonly InterfaceButton _button2DMapsCamera;
        private readonly InterfaceButton _buttonBossesCamera;

        List<string> _tooltips = new List<string>();
        private bool _showTooltip;

        public void UpdateAllButtons()
        {
            UpdateButtonText(_buttonOverworldCamera, GameSettings.ClassicOverworld);
            UpdateButtonText(_buttonHousesCamera, GameSettings.ClassicHouses);
            UpdateButtonText(_buttonCavesCamera, GameSettings.ClassicCaves);
            UpdateButtonText(_buttonDungeonsCamera, GameSettings.ClassicDungeons);
            UpdateButtonText(_buttonCastleCamera, GameSettings.ClassicCastle);
            UpdateButtonText(_buttonEggCamera, GameSettings.ClassicEgg);
            UpdateButtonText(_button2DMapsCamera, GameSettings.Classic2DMaps);
            UpdateButtonText(_buttonBossesCamera, GameSettings.ClassicBosses);
        }

        public CameraCustomPage(int width, int height)
        {
            EnableTooltips = true;
            var buttonWidth = 320;
            var buttonHeight = 15;

            // Camera Settings Layout
            _cameraCustomList = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _cameraCustomList.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_camcustom_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top };

            // Button: Overworld Toggle
            _contentLayout.AddElement(_buttonOverworldCamera = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_camcustom_overworld", "ClassicOverworld", UpdateButton));
            _tooltips.Add("tooltip_camcustom_overworld");

            // Button: Houses Toggle
            _contentLayout.AddElement(_buttonHousesCamera = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_camcustom_houses", "ClassicHouses", UpdateButton));
            _tooltips.Add("tooltip_camcustom_houses");

            // Button: Caves Toggle
            _contentLayout.AddElement(_buttonCavesCamera = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_camcustom_caves", "ClassicCaves", UpdateButton));
            _tooltips.Add("tooltip_camcustom_caves");

            // Button: Dungeons Toggle
            _contentLayout.AddElement(_buttonDungeonsCamera = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_camcustom_dungeons", "ClassicDungeons", UpdateButton));
            _tooltips.Add("tooltip_camcustom_dungeons");

            // Button: Castle Toggle
            _contentLayout.AddElement(_buttonCastleCamera = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_camcustom_castle", "ClassicCastle", UpdateButton));
            _tooltips.Add("tooltip_camcustom_castle");

            // Button: Inside Egg Toggle
            _contentLayout.AddElement(_buttonEggCamera = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_camcustom_egg", "ClassicEgg", UpdateButton));
            _tooltips.Add("tooltip_camcustom_egg");

            // Button: 2D Maps Toggle
            _contentLayout.AddElement(_button2DMapsCamera = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_camcustom_2dMaps", "Classic2DMaps", UpdateButton));
            _tooltips.Add("tooltip_camcustom_2dMaps");

            // Button: Boss Fights Toggle
            _contentLayout.AddElement(_buttonBossesCamera = new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_camcustom_bosses", "ClassicBosses", UpdateButton));
            _tooltips.Add("tooltip_camcustom_bosses");

            // Sets the initial text on all buttons.
            UpdateAllButtons();

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _cameraCustomList.AddElement(_contentLayout);
            _cameraCustomList.AddElement(_bottomBar);
            PageLayout = _cameraCustomList;
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

        private void UpdateButton(InterfaceElement element)
        {
            // Get the element as a button.
            InterfaceButton button = (InterfaceButton)element;

            // Get the option we are trying to modify by name.
            bool value = (bool)typeof(GameSettings).GetField(button.GameSetting, BindingFlags.Static | BindingFlags.Public).GetValue(null)!;

            // Invert the value of the option.
            typeof(GameSettings).GetField(button.GameSetting).SetValue(null, !value);

            // The toggle may affect the current map so queue a scale change.
            Game1.ScaleChanged = true;

            // Update the text of the button.
            UpdateButtonText(button, !value);
        }

        private void UpdateButtonText(InterfaceButton button, bool useClassic)
        {
            // Get the current camera type.
            string buttonLabel = Game1.LanguageManager.GetString(button.LabelKey,"");
            string cameraType = useClassic 
                ? Game1.LanguageManager.GetString("settings_camcustom_classic","")
                : Game1.LanguageManager.GetString("settings_camcustom_modern","");

            // Update the text on the button using the override text. 
            button.InsideLabel.OverrideText = buttonLabel + ": " + cameraType.ToString();
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_cameraCustomList, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
