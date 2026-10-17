using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Interface
{
    public class InterfaceSlider : InterfaceElement
    {
        public delegate void BFunction(int number);
        public BFunction NumberChanged;

        public delegate string StringFunction(int number);
        public StringFunction SetString;

        public string Text { get; set; }
        private string TextPostfix;

        public int CurrentStep;
        public int Start;
        public int End;
        public int StepSize;

        private static float _scrollCounter;
        private static int _scrollStartTime = 350;
        private static int _scrollTime = 75;

        private Rectangle _sliderBackgroundRectangle;
        private Rectangle _sliderRectangle;

        private Vector2 _drawOffset;
        private Vector2 _textSize;

        private Point _sliderSize = new Point(2, 2);
        private string _textKey;

        private float _stepWidth;
        private float _animationCounter;
        private int _animationTime = 66;
        private float _animationStepPosition;
        private float _animationStepStart;

        private int _sliderHeight = 3;
        private int _lastStep = -1;
        private int _steps;
        private bool _updateText;

        // Default colors that can be overwritten by LAHDMod.
        public Color ColorSlider;
        public Color ColorSliderBackground;
        public Color TextColor;

        // Backup colors for when buttons are disabled/enabled.
        public Color Backup_ColorSlider;
        public Color Backup_ColorSliderBackground;

        // Values configurable via lahdmod.
        private int custom_slider_color_red           = 40;
        private int custom_slider_color_grn           = 64;
        private int custom_slider_color_blu           = 128;
        private int custom_slider_select_red          = 90;
        private int custom_slider_select_grn          = 110;
        private int custom_slider_select_blu          = 170;
        private int custom_slider_color_disabled_red  = 100;
        private int custom_slider_color_disabled_grn  = 100;
        private int custom_slider_color_disabled_blu  = 100;
        private int custom_slider_select_disabled_red = 150;
        private int custom_slider_select_disabled_grn = 150;
        private int custom_slider_select_disabled_blu = 150;
        private int custom_slider_color_bg_red        = 79;
        private int custom_slider_color_bg_grn        = 79;
        private int custom_slider_color_bg_blu        = 79;

        public InterfaceSlider()
        {
            // Try to load a lahdmod to get a custom set of colors.
            string modFile = Path.Combine(Values.PathLAHDMods, "InterfaceSlider.lahdmod");
            ModFile.Parse(modFile, this);

            // Load in whatever the colors are now.
            ColorSlider           = new Color(custom_slider_color_red, custom_slider_color_grn, custom_slider_color_blu);
            ColorSliderBackground = new Color(custom_slider_color_bg_red, custom_slider_color_bg_grn, custom_slider_color_bg_blu);
            SelectionColor        = new Color(custom_slider_select_red, custom_slider_select_grn, custom_slider_select_blu);
            TextColor             = MainTextColor;

            // Backup colors for when interfaces get toggled.
            Backup_ColorSlider = ColorSlider;
            Backup_ColorSliderBackground = SelectionColor;

            Selectable = true;
        }

        public InterfaceSlider(string key, int width, int baseHeight, Point margin, int start, int end, int stepSize, int current, BFunction numberChanged) : this()
        {
            Size = new Point(width, baseHeight + _sliderSize.Y * 4);
            Margin = margin;

            Start = start;
            End = end;
            StepSize = stepSize;
            NumberChanged = numberChanged;

            _sliderBackgroundRectangle = new Rectangle(_sliderSize.X * 2, 2, width - _sliderSize.X * 4, _sliderHeight);

            _sliderRectangle = new Rectangle(
                _sliderSize.X, _sliderBackgroundRectangle.Y - _sliderSize.Y,
                _sliderHeight + _sliderSize.X * 2, _sliderHeight + _sliderSize.Y * 2);

            _steps = End - Start + 1;
            _stepWidth = (_sliderBackgroundRectangle.Width - 4) / ((float)_steps - 1);

            CurrentStep = MathHelper.Clamp(current - Start, 0, _steps - 1);

            _animationStepPosition = _stepWidth * CurrentStep;

            if (key != null)
            {
                _textKey = key;
                UpdateLanguageText();
            }
        }

        public void UpdateStepSize(int start, int end, int stepSize)
        {
            if (start == Start && end == End)
                return;

            _updateText = true;

            var currentValue = Start + CurrentStep;

            Start = start;
            End = end;
            StepSize = stepSize;

            _steps = End - Start + 1;
            _stepWidth = (_sliderBackgroundRectangle.Width - 4) / ((float)_steps - 1);

            CurrentStep = MathHelper.Clamp(currentValue - Start, 0, _steps - 1);

            _animationStepStart = _stepWidth * CurrentStep;
            _animationStepPosition = _stepWidth * CurrentStep;
        }

        public void SetValue(int value)
        {
            CurrentStep = MathHelper.Clamp(value - Start, 0, _steps - 1);

            // Keep "_lastStep" in sync so "Draw" doesn't force a redundant refresh.
            _lastStep = CurrentStep;

            // Snap the knob instead of sliding it in from the old position.
            _animationCounter = 0;
            _animationStepStart = _stepWidth * CurrentStep;
            _animationStepPosition = _stepWidth * CurrentStep;

            // Update the string on the slider.
            UpdateLanguageText();
        }

        public override InputEventReturn PressedButton(CButtons pressedButton)
        {
            _lastStep = CurrentStep;

            // Add a slight delay before repeating the last press when held.
            if (_scrollCounter < 0)
                _scrollCounter += _scrollTime;

            // Detect a button press and repeat the last action when held.
            if (ControlHandler.ButtonDown(CButtons.Left) || ControlHandler.ButtonDown(CButtons.Right) ||
                ControlHandler.ButtonDown(CButtons.LB) || ControlHandler.ButtonDown(CButtons.RB) ||
                ControlHandler.ButtonDown(CButtons.LT) || ControlHandler.ButtonDown(CButtons.RT))
            {
                _scrollCounter -= Game1.DeltaTime;
            }
            else
                _scrollCounter = _scrollStartTime;

            // Get the button that was pressed.
            bool Pressed(CButtons b) => ControlHandler.ButtonPressed(b) || (ControlHandler.ButtonDown(b) && _scrollCounter < 0);

            // The pressed button determines the direction and step multiplier.
            (int direction, int multiplier) =
                Pressed(CButtons.Left)  ? (-1, 1) :
                Pressed(CButtons.Right) ? ( 1, 1) :
                Pressed(CButtons.LB)    ? (-1, 5) :
                Pressed(CButtons.RB)    ? ( 1, 5) :
                Pressed(CButtons.LT)    ? (-1, 10) :
                Pressed(CButtons.RT)    ? ( 1, 10) :
                (0, 0);

            // If no button has been pressed then return.
            if (direction == 0)
                return InputEventReturn.Nothing;

            // Move the slider position based on the direction and step calculated.
            CurrentStep = MathHelper.Clamp(CurrentStep + direction * StepSize * multiplier, 0, _steps - 1);

            // Start the slider animation.
            _animationCounter = _animationTime;
            _animationStepStart = _stepWidth * _lastStep;

            // Run the associated function for when the value has changed.
            NumberChanged?.Invoke(Start + CurrentStep);

            // Play the "click" sound effect.
            Game1.AudioManager.PlaySoundEffect("D360-10-0A");
            return InputEventReturn.Something;
        }

        public void SetText(string strText)
        {
            Text = strText;

            if (SetString != null)
                TextPostfix = SetString(Start + CurrentStep);

            _textSize = DrawHelper.MeasureString(Text + TextPostfix);

            if (Size != Point.Zero)
                _drawOffset = new Vector2(Size.X / 2 - _textSize.X / 2, _textSize.Y);
        }

        public void UpdateLanguageText()
        {
            SetText(Game1.LanguageManager.GetString(_textKey, "error"));
        }

        public override void Update()
        {
            base.Update();

            _animationCounter -= Game1.DeltaTime;
            if (_animationCounter < 0)
                _animationCounter = 0;

            _animationStepPosition = MathHelper.Lerp(_animationStepStart, _stepWidth * CurrentStep,
                (float)Math.Sin((1 - _animationCounter / _animationTime) * Math.PI / 2));
        }

        public void ToggleSliderColors(bool disableSetting)
        {
            if (disableSetting)
            {
                ColorSlider = new Color(custom_slider_color_red, custom_slider_color_grn, custom_slider_color_blu);
                SelectionColor = new Color(custom_slider_select_red, custom_slider_select_grn, custom_slider_select_blu);
            }
            else
            {
                ColorSlider = new Color(custom_slider_color_disabled_red, custom_slider_color_disabled_grn, custom_slider_color_disabled_blu);
                SelectionColor = new Color(custom_slider_select_disabled_red, custom_slider_select_disabled_grn, custom_slider_select_disabled_blu);
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency)
        {
            base.Draw(spriteBatch, drawPosition, scale, transparency);

            Resources.RoundedCornerEffect.Parameters["scale"].SetValue(Game1.UiPageManager.MenuScale);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, ActiveRasterizer, Resources.RoundedCornerEffect, Game1.GetMatrix);

            Resources.RoundedCornerEffect.Parameters["radius"].SetValue(2.0f);
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(_sliderBackgroundRectangle.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(_sliderBackgroundRectangle.Height);

            // draw the toggle background line
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                (int)(drawPosition.X + _sliderBackgroundRectangle.X * scale),
                (int)(drawPosition.Y + _sliderBackgroundRectangle.Y * scale + _drawOffset.Y * scale),
                (int)(_sliderBackgroundRectangle.Width * scale),
                (int)(_sliderBackgroundRectangle.Height * scale)), ColorSliderBackground * transparency);

            Resources.RoundedCornerEffect.Parameters["radius"].SetValue(2.0f);
            Resources.RoundedCornerEffect.Parameters["width"].SetValue(_sliderRectangle.Width);
            Resources.RoundedCornerEffect.Parameters["height"].SetValue(_sliderRectangle.Height);

            // draw the slider 
            var sliderPosition = (_sliderRectangle.X + _animationStepPosition);
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                (int)(drawPosition.X + sliderPosition * scale),
                (int)(drawPosition.Y + _sliderRectangle.Y * scale + _drawOffset.Y * scale),
                (int)(_sliderRectangle.Width * scale),
                (int)(_sliderRectangle.Height * scale)), ColorSlider * transparency);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, ActiveRasterizer, null, Game1.GetMatrix);

            if (_textKey != null && Game1.LanguageManager.GetString(_textKey, "error") != Text || CurrentStep != _lastStep || _updateText)
                UpdateLanguageText();
            _updateText = false;

            if (Text == null)
                return;

            // draw the text
            DrawHelper.DrawString(spriteBatch, Text + TextPostfix, new Vector2((int)(drawPosition.X + _drawOffset.X * scale), (int)(drawPosition.Y + scale)), TextColor * transparency, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
    }
}
