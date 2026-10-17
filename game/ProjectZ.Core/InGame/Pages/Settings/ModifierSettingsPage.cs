using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class ModifierSettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _modifiersLayout;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        private readonly InterfaceSlider     _sliderEnemyHitPoints;
        private readonly InterfaceSlider     _sliderDamageTaken;
        private readonly InterfaceSlider     _sliderDamageCooldown;
        private readonly InterfaceSlider     _sliderMovementSpeed;
        private readonly InterfaceListLayout _toggleNoHeartDrops;
        private readonly InterfaceListLayout _toggleNoDmgLaunch;
        private readonly InterfaceListLayout _toggleMirrorReflects;

        List<string> _tooltips = new List<string>();
        private bool _showTooltip;

        public void SetEnemyHitPoints(int value)  { _sliderEnemyHitPoints.SetValue(value); EnemyLives.RestoreDefaultHP(); EnemyLives.AddToEnemyHP(value); }
        public void SetDamageTaken(int value)     { _sliderDamageTaken.SetValue(value); }
        public void SetDamageCooldown(int value)  { _sliderDamageCooldown.SetValue(value); ObjLink.CooldownTime = ObjLink.BlinkTime * GameSettings.DmgCooldown; }
        public void SetMovementSpeed(int value)   { _sliderMovementSpeed.SetValue(value); }
        public void SetNoHeartDrops(bool state)   { _toggleNoHeartDrops.Toggle.SetValue(state); }
        public void SetNoDamageLaunch(bool state) { _toggleNoDmgLaunch.Toggle.SetValue(state); }
        public void SetMirrorReflects(bool state) { _toggleMirrorReflects.Toggle.SetValue(state); }

        public ModifierSettingsPage(int width, int height)
        {
            EnableTooltips = true;
            var buttonWidth = 320;
            var buttonHeight = 14;
            var sliderHeight = 12;

            // Modifiers Settings Layout
            _modifiersLayout = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _modifiersLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_mods_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top, Scrollable = true };

            // Slider: Extra Enemy HP
            _sliderEnemyHitPoints = new InterfaceSlider("settings_mods_enemy_hp",
                buttonWidth, sliderHeight, new Point(1, 1), 0, 30, 1, GameSettings.EnemyBonusHP,
                number => { GameSettings.EnemyBonusHP = number; })
                { SetString = number => EnemyHPSliderAdjustment(number) };
            _contentLayout.AddElement(_sliderEnemyHitPoints);
            _tooltips.Add("tooltip_mods_enemy_hp");

            // Slider: Damage Taken Multiplier
            _sliderDamageTaken = new InterfaceSlider( "settings_mods_damage",
                buttonWidth, sliderHeight, new Point(1, 1), 0, 40, 1, GameSettings.DamageFactor,
                number => { GameSettings.DamageFactor = number; })
                { SetString = number => DamageTakenSliderAdjustment(number) };
            _contentLayout.AddElement(_sliderDamageTaken);
            _tooltips.Add("tooltip_mods_damage");

            // Slider: Damage Cooldown (Invincibility Frames)
            _sliderDamageCooldown = new InterfaceSlider("settings_mods_damagecd",
                buttonWidth, sliderHeight, new Point(1, 1), 0, 100, 1, GameSettings.DmgCooldown,
                number => { GameSettings.DmgCooldown = number; })
                { SetString = number => DamageCooldownSliderAdjustment(number) };
            _contentLayout.AddElement(_sliderDamageCooldown);
            _tooltips.Add("tooltip_mods_damagecd");

            // Slider: Movement Speed
            _sliderMovementSpeed = new InterfaceSlider("settings_mods_movespeed",
                buttonWidth, sliderHeight, new Point(1, 1), 0, 10, 1, (int)MathF.Round(GameSettings.MoveSpeedAdded * 10),
                number => { GameSettings.MoveSpeedAdded = number / 10f; })
                { SetString = number => AddedMoveSpeedSliderAdjustment(number) };
            _contentLayout.AddElement(_sliderMovementSpeed);
            _tooltips.Add("tooltip_mods_movespeed");

            // Toggle: No Heart Drops
            _toggleNoHeartDrops = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_mods_nohearts", GameSettings.NoHeartDrops, 
                newState => { GameSettings.NoHeartDrops = newState; });
            _contentLayout.AddElement(_toggleNoHeartDrops);
            _tooltips.Add("tooltip_mods_nohearts");

            // Toggle: No Damage Launch
            _toggleNoDmgLaunch = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_mods_dmglaunch", GameSettings.NoDamageLaunch, 
                newState => { GameSettings.NoDamageLaunch = newState; });
            _contentLayout.AddElement(_toggleNoDmgLaunch);
            _tooltips.Add("tooltip_mods_dmglaunch");

            // Button: Extra Sword Interactions
            _contentLayout.AddElement(new InterfaceButton(
                new Point(buttonWidth, buttonHeight), new Point(1, 2), 
                "settings_mods_swordinteract", element => { Game1.UiPageManager.ChangePage(typeof(SwordInteractPage)); }));
            _tooltips.Add("tooltip_mods_swordinteract");

            // Toggle: Mirror Shield Reflects
            _toggleMirrorReflects = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_mods_mirrorreflect", GameSettings.MirrorReflects, 
                newState => { GameSettings.MirrorReflects = newState; });
            _contentLayout.AddElement(_toggleMirrorReflects);
            _tooltips.Add("tooltip_mods_mirrorreflect");

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _modifiersLayout.AddElement(_contentLayout);
            _modifiersLayout.AddElement(_bottomBar);
            PageLayout = _modifiersLayout;
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

        private string EnemyHPSliderAdjustment(int number)
        {
            EnemyLives.RestoreDefaultHP();
            EnemyLives.AddToEnemyHP(number);
            return ": " + number;
        }

        private string DamageTakenSliderAdjustment(int number)
        {
            return ": " + (number * 0.25) + "x";
        }

        private string DamageCooldownSliderAdjustment(int number)
        {
            // Update the damage cooldown.
            ObjLink.CooldownTime = ObjLink.BlinkTime * GameSettings.DmgCooldown;

            // Return the text to show.
            return ": " + number + "x (" + ObjLink.CooldownTime + "ms)";
        }

        private string AddedMoveSpeedSliderAdjustment(int number)
        {
            // Divide the value by 10 to get the decimal percentage.
            float addmove = (float)(number / 10f);
            int percent = number * 10;
            MapManager.ObjLink.AlterMoveSpeed(addmove);
            return ": " + percent + "%";
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_modifiersLayout, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
