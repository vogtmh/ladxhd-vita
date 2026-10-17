using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectZ.InGame.Graphics;

namespace ProjectZ.InGame.Interface
{
    public class InterfaceLabel : InterfaceElement
    {
        // A cached version of the font if fed to the parameter (used for header text).
        private SpriteFont _font;
        private SpriteFont Font => _font ?? Resources.GameFont;
        private bool UseChinaFont => _font == null && Game1.LanguageManager.CurrentLanguageCode == "chn";
        public string Text { get; set; }

        public bool Translate = true;

        public Gravities TextAlignment
        {
            get { return _textAlignment; }
            set
            {
                _textAlignment = value;
                if (Text != null)
                    SetText(Text);
            }
        }

        private float _textScale = 1.0f;
        public float TextScale
        {
            get { return _textScale; }
            set
            {
                _textScale = value;
                if (Text != null)
                    SetText(Text);
            }
        }

        // The InterfaceLabel seems to always want to reference a language string, so let's
        // have a way to override the text and put whatever we want here whenever we want.
        public string OverrideText = "";

        private Vector2 _drawOffset;
        private Vector2 _textSize;

        private Gravities _textAlignment = Gravities.Center;

        private readonly string _textKey;

        public Color TextColor;

        public InterfaceLabel(SpriteFont font, string key, Point size, Point margin)
        {
            // Load in whatever the colors are now.
            TextColor = MainTextColor;

            _font = font;
            Size = size;
            Margin = margin;

            if (string.IsNullOrEmpty(key))
                return;

            _textKey = key;
            UpdateLanguageText();
        }

        public InterfaceLabel(string key, Point size, Point margin) : this(null, key, size, margin) { }

        public InterfaceLabel(string key) : this(key, Point.Zero, Point.Zero)
        {
            Size = new Point((int)_textSize.X, (int)_textSize.Y);
        }

        private string FilterUnsupportedCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // BitmapFont doesn't have a Characters collection, but it won't crash on unknown chars
            // so just return the string as-is when using ChinaFont
            if (UseChinaFont)
                return input;

            var supported = _font?.Characters ?? Resources.GameFont.Characters;
            var result = new System.Text.StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (supported.Contains(c))
                    result.Append(c);
                else
                    result.Append('?');
            }
            return result.ToString();
        }

        public void SetText(string strText)
        {
            Text = FilterUnsupportedCharacters(strText);

            try
            {
                _textSize = UseChinaFont
                    ? Resources.ChinaFont.MeasureString(Text)
                    : (_font ?? Resources.GameFont).MeasureString(Text);
            }
            catch
            {
                Text = "";
                _textSize = Vector2.Zero;
            }

            if (Size != Point.Zero)
            {
                var scaledW = _textSize.X * _textScale;
                var scaledH = _textSize.Y * _textScale;

                _drawOffset = new Vector2(Size.X / 2 - scaledW / 2, Size.Y / 2 - scaledH / 2);

                if ((TextAlignment & Gravities.Left) != 0)
                    _drawOffset.X = 0;
                else if ((TextAlignment & Gravities.Right) != 0)
                    _drawOffset.X = Size.X - scaledW;

                if ((TextAlignment & Gravities.Top) != 0)
                    _drawOffset.Y = 0;
                else if ((TextAlignment & Gravities.Bottom) != 0)
                    _drawOffset.Y = Size.Y - scaledH;
            }
        }

        public void UpdateLanguageText()
        {
            // If override text was not set then use the text fed into the parameter.
            if (string.IsNullOrEmpty(OverrideText))
            {
                // The text is a "key" to pull up the "value" from the language dictionary. Compare the
                // text found in the "value" against placeholder tags and replace ones that are found.
                string setText = Game1.LanguageManager.GetString(_textKey, "error");
                SetText(setText);
            }
            // If override text was used, then use it directly without using it as a "key".
            else
                SetText(OverrideText);
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency)
        {
            base.Draw(spriteBatch, drawPosition, scale, transparency);

            if (OverrideText != "" || (Translate && _textKey != null && Game1.LanguageManager.GetString(_textKey, "error") != Text))
                UpdateLanguageText();

            if (Text == null)
                return;

            if (UseChinaFont)
                spriteBatch.DrawString(Resources.ChinaFont, Text, new Vector2((int)(drawPosition.X + _drawOffset.X * scale), (int)(drawPosition.Y + (_drawOffset.Y + 1) * scale)), TextColor * transparency, 0, Vector2.Zero, new Vector2(scale * _textScale, scale * _textScale), SpriteEffects.None, 0);
            else
                spriteBatch.DrawString(_font ?? Resources.GameFont, Text, new Vector2((int)(drawPosition.X + _drawOffset.X * scale), (int)(drawPosition.Y + (_drawOffset.Y + 1) * scale)), TextColor * transparency, 0, Vector2.Zero, new Vector2(scale * _textScale), SpriteEffects.None, 0);
        }
    }
}
