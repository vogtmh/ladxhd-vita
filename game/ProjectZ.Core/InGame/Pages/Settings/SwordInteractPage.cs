using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class SwordInteractPage : InterfacePage
    {
        private readonly InterfaceListLayout _swordSettingsList;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        private readonly InterfaceListLayout _toggleSwordGrabNormal;
        private readonly InterfaceListLayout _toggleSwordGrabStatic;
        private readonly InterfaceListLayout _toggleSwordGrabFairy;
        private readonly InterfaceListLayout _toggleSwordGrabKeys;
        private readonly InterfaceListLayout _toggleBounceBoomerang;
        private readonly InterfaceListLayout _toggleBounceBombs;
        private readonly InterfaceListLayout _toggleSwordBlock;
        private readonly InterfaceListLayout _toggleSwordSmashPots;
        private readonly InterfaceListLayout _toggleBeamCutsShrubs;

        public void SetSwordCollectNormal(bool state)   { _toggleSwordGrabNormal.Toggle.SetValue(state); }
        public void SetSwordCollectStatic(bool state)   { _toggleSwordGrabStatic.Toggle.SetValue(state); }
        public void SetSwordCollectFairy(bool state)    { _toggleSwordGrabFairy.Toggle.SetValue(state); }
        public void SetSwordCollectKeys(bool state)     { _toggleSwordGrabKeys.Toggle.SetValue(state); }
        public void SetSwordBounceBoomerang(bool state) { _toggleBounceBoomerang.Toggle.SetValue(state); }
        public void SetSwordBounceBombs(bool state)     { _toggleBounceBombs.Toggle.SetValue(state); }
        public void SetSwordBlockProjectile(bool state) { _toggleSwordBlock.Toggle.SetValue(state); }
        public void SetSwordSmashesPots(bool state)     { _toggleSwordSmashPots.Toggle.SetValue(state); }
        public void SetSwordBeamCutsShrubs(bool state)  { _toggleBeamCutsShrubs.Toggle.SetValue(state); }

        List<string> _tooltips = new List<string>();
        private bool _showTooltip;

        public SwordInteractPage(int width, int height)
        {
            EnableTooltips = true;
            var buttonWidth = 320;
            var buttonHeight = 13;

            // Game Settings Layout
            _swordSettingsList = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _swordSettingsList.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_sword_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top };

            // Toggle: Collect Items
            _toggleSwordGrabNormal = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_sword_grabnormal", GameSettings.SwGrabNormal, 
                newState => { GameSettings.SwGrabNormal = newState; });
            _contentLayout.AddElement(_toggleSwordGrabNormal);
            _tooltips.Add("tooltip_sword_grabnormal");

            // Toggle: Collect Static Items
            _toggleSwordGrabStatic = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_sword_grabworlditem", GameSettings.SwGrabWorldItem, 
                newState => { GameSettings.SwGrabWorldItem = newState; });
            _contentLayout.AddElement(_toggleSwordGrabStatic);
            _tooltips.Add("tooltip_sword_grabworlditem");

            // Toggle: Collect Fairies
            _toggleSwordGrabFairy = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_sword_grabfairy", GameSettings.SwGrabFairy, 
                newState => { GameSettings.SwGrabFairy = newState; });
            _contentLayout.AddElement(_toggleSwordGrabFairy);
            _tooltips.Add("tooltip_sword_grabfairy");

            // Toggle: Collect Small Keys
            _toggleSwordGrabKeys = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_sword_grabsmallkeys", GameSettings.SwGrabSmallKey, 
                newState => { GameSettings.SwGrabSmallKey = newState; });
            _contentLayout.AddElement(_toggleSwordGrabKeys);
            _tooltips.Add("tooltip_sword_grabsmallkeys");

            // Toggle: Bounce Boomerang
            _toggleBounceBoomerang = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_sword_boomerang", GameSettings.SwBoomerang, 
                newState => { GameSettings.SwBoomerang = newState; });
            _contentLayout.AddElement(_toggleBounceBoomerang);
            _tooltips.Add("tooltip_sword_boomerang");

            // Toggle: Bounce Bombs
            _toggleBounceBombs = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_sword_bouncebombs", GameSettings.SwSmackBombs, 
                newState => { GameSettings.SwSmackBombs = newState; });
            _contentLayout.AddElement(_toggleBounceBombs);
            _tooltips.Add("tooltip_sword_bouncebombs");

            // Toggle: Sword Block Projectiles
            _toggleSwordBlock = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_sword_swordblock", GameSettings.SwMissileBlock, 
                newState => { GameSettings.SwMissileBlock = newState; });
            _contentLayout.AddElement(_toggleSwordBlock);
            _tooltips.Add("tooltip_sword_swordblock");

            // Toggle: Smash Pots & Skulls
            _toggleSwordSmashPots = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_sword_breakpots", GameSettings.SwBreakPots, 
                newState => { GameSettings.SwBreakPots = newState; });
            _contentLayout.AddElement(_toggleSwordSmashPots);
            _tooltips.Add("tooltip_sword_breakpots");

            // Toggle: Beam Cuts Grass & Bushes
            _toggleBeamCutsShrubs = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_sword_beamshrubs", GameSettings.SwBeamShrubs, 
                newState => { GameSettings.SwBeamShrubs = newState; });
            _contentLayout.AddElement(_toggleBeamCutsShrubs);
            _tooltips.Add("tooltip_sword_beamshrubs");

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { SwordPageBackButton(); }));
            _swordSettingsList.AddElement(_contentLayout);
            _swordSettingsList.AddElement(_bottomBar);
            PageLayout = _swordSettingsList;
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            base.Update(pressedButtons, gameTime);

            // The back button was pressed.
            if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                SwordPageBackButton();
            
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

        private void SwordPageBackButton()
        {
            Game1.GameManager.ItemManager.Load(); 
            Game1.UiPageManager.PopPage();
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

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_swordSettingsList, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
