using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Interface
{
    public class InterfaceToggle : InterfaceElement
    {
        public delegate void BFunction(bool toggleState);
        public BFunction ClickFunction;

        private readonly Rectangle _toggleBackgroundRectangle;
        private readonly Rectangle _toggleRectangle;

        private float _toggleAnimationState;
        private float _toggleAnimationCounter;

        private const int ToggleAnimationTime = 100;

        private bool _toggleState;

        public bool ToggleState
        {
            get => _toggleState;
            set => SetValue(value);
        }

        // Default colors that can be overwritten by LAHDMod.
        public Color ColorToggled;
        public Color ColorToggledBackground;
        public Color ColorNotToggled;
        public Color ColorNotToggledBackground;

        // Backup colors for when buttons are disabled/enabled.
        public Color Backup_ColorToggled;
        public Color Backup_ColorToggledBackground;

        public Color Backup_ColorToggled_Disabled;
        public Color Backup_ColorToggledBackground_Disabled;

        // Values configurable via lahdmod.
        private int custom_toggle_color_red           = 40;
        private int custom_toggle_color_grn           = 64;
        private int custom_toggle_color_blu           = 128;
        private int custom_toggle_select_red          = 90;
        private int custom_toggle_select_grn          = 110;
        private int custom_toggle_select_blu          = 170;
        private int custom_toggle_color_disabled_red  = 100;
        private int custom_toggle_color_disabled_grn  = 100;
        private int custom_toggle_color_disabled_blu  = 100;
        private int custom_toggle_select_disabled_red = 150;
        private int custom_toggle_select_disabled_grn = 150;
        private int custom_toggle_select_disabled_blu = 150;
        private int custom_toggle_on_red              = 40;
        private int custom_toggle_on_grn              = 64;
        private int custom_toggle_on_blu              = 128;
        private int custom_toggle_on_bg_red           = 90;
        private int custom_toggle_on_bg_grn           = 110;
        private int custom_toggle_on_bg_blu           = 170;
        private int custom_toggle_off_red             = 79;
        private int custom_toggle_off_grn             = 79;
        private int custom_toggle_off_blu             = 79;
        private int custom_toggle_off_bg_red          = 188;
        private int custom_toggle_off_bg_grn          = 188;
        private int custom_toggle_off_bg_blu          = 188;

        public InterfaceToggle()
        {
            // Try to load a lahdmod to get a custom set of colors.
            string modFile = Path.Combine(Values.PathLAHDMods, "InterfaceToggle.lahdmod");
            ModFile.Parse(modFile, this);

            // Load in whatever the colors are now.
            Color = new Color(custom_toggle_color_red, custom_toggle_color_grn, custom_toggle_color_blu);
            SelectionColor = new Color(custom_toggle_select_red, custom_toggle_select_grn, custom_toggle_select_blu);
            ColorToggled = new Color(custom_toggle_on_red, custom_toggle_on_grn, custom_toggle_on_blu);
            ColorToggledBackground = new Color(custom_toggle_on_bg_red, custom_toggle_on_bg_grn, custom_toggle_on_bg_blu);
            ColorNotToggled = new Color(custom_toggle_off_red, custom_toggle_off_grn, custom_toggle_off_blu);
            ColorNotToggledBackground = new Color(custom_toggle_off_bg_red, custom_toggle_off_bg_grn, custom_toggle_off_bg_blu);

            // Backup colors for when interfaces get toggled.
            Backup_ColorToggled = ColorToggled;
            Backup_ColorToggledBackground = ColorToggledBackground;

            // Backup colors for when interfaces get disabled.
            Backup_ColorToggled_Disabled = new Color(custom_toggle_color_disabled_red, custom_toggle_color_disabled_grn, custom_toggle_color_disabled_blu);
            Backup_ColorToggledBackground_Disabled = new Color(custom_toggle_select_disabled_red, custom_toggle_select_disabled_grn, custom_toggle_select_disabled_blu);
        }

        public InterfaceToggle(Point size, Point margin, bool startState, BFunction clickFunction) : this()
        {
            Size = size;
            Margin = margin;

            _toggleState = startState;

            ClickFunction = clickFunction;

            _toggleBackgroundRectangle = new Rectangle(0, 0, size.X, size.Y);
            _toggleRectangle = new Rectangle(2, 2, size.Y - 4, size.Y - 4);

            _toggleAnimationState = _toggleState ? 1 : 0;
        }

        public static InterfaceListLayout GetToggleButton(Point size, Point margin, string textKey, bool startState, BFunction clickFunction, Color? customColor = null, Color? customSelectionColor = null)
        {
            var toggleLayout = new InterfaceListLayout() 
            { 
                Size = size, 
                Margin = margin, 
                HorizontalMode = true, 
                Selectable = true 
            };

            var toggleSize = new Point((int)(size.Y * 1.75f), size.Y - 2);
            var buttonSize = new Point(size.X - toggleSize.X - 4, size.Y);
            var toggle = new InterfaceToggle(toggleSize, new Point(2, 0), startState, clickFunction);

            // Apply custom colors if provided.
            if (customColor.HasValue)
            {
                toggle.Color = customColor.Value;
                toggle.SelectionColor = customSelectionColor ?? customColor.Value;
            }

            var button = new InterfaceButton(
                buttonSize, 
                new Point(2, 0), 
                textKey, 
                buttonElement => toggle.Toggle());

            // Pass shared colors to button.
            if (customColor.HasValue)
            {
                button.Color = toggle.Color;
                button.SelectionColor = toggle.SelectionColor;
            }

            toggleLayout.AddElement(button);
            toggleLayout.AddElement(toggle);

            return toggleLayout;
        }

        public override InputEventReturn PressedButton(CButtons pressedButton)
        {
            if (!ControlHandler.ButtonPressed(ControlHandler.ConfirmButton))
                return InputEventReturn.Nothing;

            Toggle();

            return ClickFunction != null ? InputEventReturn.Something : InputEventReturn.Nothing;
        }

        public void SetValue(bool state)
        {
            _toggleState = state;

            // Snap the knob instead of leaving it mid-slide until the next Update.
            _toggleAnimationCounter = 0;
            _toggleAnimationState = state ? 1 : 0;
        }

        public void Toggle()
        {
            _toggleState = !_toggleState;
            _toggleAnimationCounter = ToggleAnimationTime;

            ClickFunction?.Invoke(_toggleState);
        }

        public override void Update()
        {
            base.Update();

            // update the toggle animation
            _toggleAnimationCounter -= Game1.DeltaTime;
            if (_toggleAnimationCounter <= 0)
                _toggleAnimationCounter = 0;
            var percentage = (float)Math.Sin((1 - _toggleAnimationCounter / ToggleAnimationTime) * Math.PI / 2);
            _toggleAnimationState = _toggleState ? percentage : 1 - percentage;
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency)
        {
            Resources.RoundedCornerEffect.Parameters["scale"].SetValue(Game1.UiPageManager.MenuScale);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, ActiveRasterizer, Resources.RoundedCornerEffect, Game1.GetMatrix);

            Resources.RoundedCornerEffect.Parameters["radius"].SetValue(4.0f);
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(_toggleBackgroundRectangle.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(_toggleBackgroundRectangle.Height);

            // draw the toggle background
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                (int)(drawPosition.X + _toggleBackgroundRectangle.X * scale),
                (int)(drawPosition.Y + _toggleBackgroundRectangle.Y * scale),
                (int)(_toggleBackgroundRectangle.Width * scale),
                (int)(_toggleBackgroundRectangle.Height * scale)),
                (_toggleState ? ColorToggledBackground : ColorNotToggledBackground) * transparency);

            Resources.RoundedCornerEffect.Parameters["radius"].SetValue(4.0f);
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(_toggleRectangle.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(_toggleRectangle.Height);

            // draw the toggle
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                (int)(drawPosition.X + (_toggleRectangle.X + _toggleAnimationState * (_toggleBackgroundRectangle.Width - _toggleRectangle.Width - 4)) * scale),
                (int)(drawPosition.Y + _toggleRectangle.Y * scale),
                (int)(_toggleRectangle.Width * scale),
                (int)(_toggleRectangle.Height * scale)),
                (_toggleState ? ColorToggled : ColorNotToggled) * transparency);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, ActiveRasterizer, null, Game1.GetMatrix);
        }
    }
}
