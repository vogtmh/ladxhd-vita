using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class CheatsSettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _cheatsOptionsList;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        private readonly InterfaceListLayout _toggleInvincibility;
        private readonly InterfaceListLayout _toggleInfiniteRupees;
        private readonly InterfaceListLayout _toggleInfiniteMagicPowder;
        private readonly InterfaceListLayout _toggleInfiniteBombs;
        private readonly InterfaceListLayout _toggleInfiniteArrows;
        private readonly InterfaceListLayout _toggleDisableClipping;
        private readonly InterfaceListLayout _toggleOneHitKills;
        private readonly InterfaceListLayout _toggleGiveAllItems;

        List<string> _tooltips = new List<string>();
        private bool _showTooltip;

        public void SetCheatInvincibility(bool state)   { _toggleInvincibility.Toggle.SetValue(state); }
        public void SetCheatInfiniteRupees(bool state)  { _toggleInfiniteRupees.Toggle.SetValue(state); }
        public void SetCheatInfinitePowder(bool state)  { _toggleInfiniteMagicPowder.Toggle.SetValue(state); }
        public void SetCheatInfiniteBombs(bool state)   { _toggleInfiniteBombs.Toggle.SetValue(state); }
        public void SetCheatInfiniteArrows(bool state)  { _toggleInfiniteArrows.Toggle.SetValue(state); }
        public void SetCheatDisableClipping(bool state) { _toggleDisableClipping.Toggle.SetValue(state); }
        public void SetCheatOneHitKills(bool state)     { _toggleOneHitKills.Toggle.SetValue(state); }
        public void SetCheatGiveAllItems(bool state)    { _toggleGiveAllItems.Toggle.SetValue(state); }

        public CheatsSettingsPage(int width, int height)
        {
            EnableTooltips = true;
            var buttonWidth = 320;
            var buttonHeight = 14;

            // Cheats Settings Layout
            _cheatsOptionsList = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _cheatsOptionsList.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_cheats_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top, Scrollable = false };

            // Toggle: Invincibility
            _toggleInvincibility = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_cheats_invincibility", GameSettings.ChInvincibility, 
                newState => { GameSettings.ChInvincibility = newState; });
            _contentLayout.AddElement(_toggleInvincibility);
            _tooltips.Add("tooltip_cheats_invincibility");

            // Toggle: Infinite Rupees
            _toggleInfiniteRupees = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_cheats_infiniterupees", GameSettings.ChInfinRupees, 
                newState => { GameSettings.ChInfinRupees = newState; });
            _contentLayout.AddElement(_toggleInfiniteRupees);
            _tooltips.Add("tooltip_cheats_infiniterupees");

            // Toggle: Infinite Magic Powder
            _toggleInfiniteMagicPowder = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_cheats_infinitepowder", GameSettings.ChInfinPowder, 
                newState => { GameSettings.ChInfinPowder = newState; });
            _contentLayout.AddElement(_toggleInfiniteMagicPowder);
            _tooltips.Add("tooltip_cheats_infinitepowder");

            // Toggle: Infinite Bombs
            _toggleInfiniteBombs = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_cheats_infinitebombs", GameSettings.ChInfinBombs, 
                newState => { GameSettings.ChInfinBombs = newState; });
            _contentLayout.AddElement(_toggleInfiniteBombs);
            _tooltips.Add("tooltip_cheats_infinitebombs");

            // Toggle: Infinite Arrows
            _toggleInfiniteArrows = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_cheats_infinitearrows", GameSettings.ChInfinArrows, 
                newState => { GameSettings.ChInfinArrows = newState; });
            _contentLayout.AddElement(_toggleInfiniteArrows);
            _tooltips.Add("tooltip_cheats_infinitearrows");

            // Toggle: Disable Clipping
            _toggleDisableClipping = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_cheats_disableclipping", GameSettings.ChNoClipping, 
                newState => { ToggleNoClipping(newState); });
            _contentLayout.AddElement(_toggleDisableClipping);
            _tooltips.Add("tooltip_cheats_disableclipping");

            // Toggle: One Hit Kills
            _toggleOneHitKills = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_cheats_onehitkills", GameSettings.ChOneHitKills, 
                newState => { GameSettings.ChOneHitKills = newState; });
            _contentLayout.AddElement(_toggleOneHitKills);
            _tooltips.Add("tooltip_cheats_onehitkills");

            // Toggle: Give All Items
            _toggleGiveAllItems = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_cheats_giveallitems", GameSettings.ChGiveAllItems, 
                newState => { ToggleGiveAllItems(newState); });
            _contentLayout.AddElement(_toggleGiveAllItems);
            _tooltips.Add("tooltip_cheats_giveallitems");

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _cheatsOptionsList.AddElement(_contentLayout);
            _cheatsOptionsList.AddElement(_bottomBar);
            PageLayout = _cheatsOptionsList;
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

        private void ToggleNoClipping(bool newState)
        {
            GameSettings.ChNoClipping = newState;

            if (Game1.InProgress)
                CheatSystem.ToggleNoClipping();
        }

        private void ToggleGiveAllItems(bool newState)
        {
            GameSettings.ChGiveAllItems = newState;

            if (Game1.InProgress)
            {
                if (newState)
                    CheatSystem.EnableGiveAllItems();
                else
                    CheatSystem.DisableGiveAllItems();
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_cheatsOptionsList, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
