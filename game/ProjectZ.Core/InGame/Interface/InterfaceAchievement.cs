using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Interface
{
    public class InterfaceAchievement : InterfaceElement
    {
        public delegate void BFunction(InterfaceElement element);
        public BFunction ClickFunction;
        private bool _achieved = true;

        public bool Achieved
        {
            get => _achieved;
            set { _achieved = value; ApplyAchievedColors(); }
        }
        public bool Translate = true;
        public string TitleOverride = "";
        public string DescriptionOverride = "";

        private string _titleKey;
        private string _descKey;
        private string _title = "";
        private string _description = "";

        private Texture2D _image;
        public Color ImageColor = Color.White;
        public bool ShowImageFrame = true;
        public Color ImageFrameColor = new Color(255, 255, 255);

        private Texture2D _earnedImage;
        public bool ShowEarnedBadge = true;
        public float EarnedBadgeScale = 0.30f;

        public SpriteFont TitleFont;
        public float TitleScale = 0.55f;
        public float DescriptionScale = 1.0f;

        public int Padding = 2;
        public int TextGap = 4;
        public int TitleSpacing = 1;
        public int ChineseLineSpacing = 4;

        public Color TitleColor;
        public Color DescriptionColor;

        public Color AchievedColor;
        public Color AchievedSelectionColor;
        public Color AchievedTitleColor;
        public Color AchievedDescriptionColor;
        public Color AchievedImageColor;
        public Color AchievedImageFrameColor;

        public Color LockedColor;
        public Color LockedSelectionColor;
        public Color LockedTitleColor = new Color(255, 255, 255);
        public Color LockedDescriptionColor = new Color(255, 255, 255);
        public Color LockedImageColor = new Color(110, 110, 110);
        public Color LockedImageFrameColor = new Color(255, 255, 255);

        private readonly List<string> _wrapLines = new List<string>();
        private string _wrapSource;
        private float _wrapWidth = -1;
        private float _wrapScale = -1;

        // Title marquee
        public bool MarqueeTitle = true;
        public float MarqueeSpeed = 24f;
        public float MarqueeStartDelay = 1200f;
        public float MarqueeEndDelay = 3000f;

        // Description marquee 
        public bool MarqueeDescription = true;
        public float DescMarqueeSpeed = 18f;
        public float DescMarqueeStartDelay = 1200f;
        public float DescMarqueeEndDelay = 3000f;
        public float DescBottomSlack = 0.35f;

        private string _marqueeSource;
        private float _marqueeOverflow;
        private float _marqueeScroll;
        private float _marqueeTimer;
        private int _marqueePhase;

        private string _descMarqueeSource;
        private float _descOverflow;
        private float _descScroll;
        private float _descTimer;
        private int _descPhase;

        // Values configurable via lahdmod.
        private int custom_achievement_color_red = 40;
        private int custom_achievement_color_grn = 64;
        private int custom_achievement_color_blu = 128;
        private int custom_achievement_select_red = 90;
        private int custom_achievement_select_grn = 110;
        private int custom_achievement_select_blu = 170;
        private int custom_achievement_title_red = 255;
        private int custom_achievement_title_grn = 255;
        private int custom_achievement_title_blu = 70;
        private int custom_achievement_locked_title_red = 255;
        private int custom_achievement_locked_title_grn = 255;
        private int custom_achievement_locked_title_blu = 220;
        private int custom_achievement_locked_red = 80;
        private int custom_achievement_locked_grn = 80;
        private int custom_achievement_locked_blu = 80;
        private int custom_achievement_locked_select_red = 120;
        private int custom_achievement_locked_select_grn = 120;
        private int custom_achievement_locked_select_blu = 120;

        public InterfaceAchievement()
        {
            string modFile = Path.Combine(Values.PathLAHDMods, "InterfaceAchievement.lahdmod");
            ModFile.Parse(modFile, this);

            AchievedColor = new Color(custom_achievement_color_red, custom_achievement_color_grn, custom_achievement_color_blu);
            AchievedSelectionColor = new Color(custom_achievement_select_red, custom_achievement_select_grn, custom_achievement_select_blu);
            AchievedTitleColor = new Color(custom_achievement_title_red, custom_achievement_title_grn, custom_achievement_title_blu);
            AchievedDescriptionColor = MainTextColor;
            AchievedImageColor = Color.White;
            AchievedImageFrameColor = new Color(255, 255, 255);

            LockedTitleColor = new Color(custom_achievement_locked_title_red, custom_achievement_locked_title_grn, custom_achievement_locked_title_blu);
            LockedColor = new Color(custom_achievement_locked_red, custom_achievement_locked_grn, custom_achievement_locked_blu);
            LockedSelectionColor = new Color(custom_achievement_locked_select_red, custom_achievement_locked_select_grn, custom_achievement_locked_select_blu);

            TitleFont = Resources.GameHeaderFont;
            CornerRadius = 4.0f;
            Selectable = true;

            _earnedImage = Resources.GetTexture("earned.png");

            ApplyAchievedColors();
        }

        public InterfaceAchievement(Point size, Point margin, Texture2D image, string title, string description, bool translate = true, bool achieved = true) : this()
        {
            Size = size;
            Margin = margin;
            _image = image;
            Translate = translate;

            if (translate)
            {
                _titleKey = title;
                _descKey = description;
            }
            else
            {
                _title = title ?? "";
                _description = description ?? "";
            }

            Achieved = achieved;
        }

        private void ApplyAchievedColors()
        {
            Color            = _achieved ? AchievedColor            : LockedColor;
            SelectionColor   = _achieved ? AchievedSelectionColor   : LockedSelectionColor;
            TitleColor       = _achieved ? AchievedTitleColor       : LockedTitleColor;
            DescriptionColor = _achieved ? AchievedDescriptionColor : LockedDescriptionColor;
            ImageColor       = _achieved ? AchievedImageColor       : LockedImageColor;
            ImageFrameColor  = _achieved ? AchievedImageFrameColor  : LockedImageFrameColor;
        }

        public void SetImage(Texture2D image) => _image = image;
        public void SetTitle(string text) => TitleOverride = text ?? "";
        public void SetDescription(string text) => DescriptionOverride = text ?? "";

        private string ResolveTitle()
        {
            if (!string.IsNullOrEmpty(TitleOverride)) return TitleOverride;
            if (Translate && _titleKey != null) return Game1.LanguageManager.GetString(_titleKey, "error");
            return _title;
        }

        private string ResolveDescription()
        {
            if (!string.IsNullOrEmpty(DescriptionOverride)) return DescriptionOverride;
            if (Translate && _descKey != null) return Game1.LanguageManager.GetString(_descKey, "error");
            return _description;
        }

        public override InputEventReturn PressedButton(CButtons pressedButton)
        {
            if (pressedButton != ControlHandler.ConfirmButton)
                return InputEventReturn.Nothing;

            if (ClickFunction != null)
            {
                Game1.AudioManager.PlaySoundEffect("D360-19-13");
                ClickFunction(this);
                return InputEventReturn.Something;
            }
            return InputEventReturn.Nothing;
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency)
        {
            if (!Visible || Hidden)
                return;

            base.Draw(spriteBatch, drawPosition, scale, transparency);

            var title = ResolveTitle();
            var desc = ResolveDescription();

            int imageSide = Size.Y - Padding * 2;
            bool hasImageArea = _image != null || ShowImageFrame;
            int textX = Padding + (hasImageArea ? imageSide + TextGap : 0);
            int textWidth = Size.X - textX - Padding;

            DrawImage(spriteBatch, drawPosition, scale, transparency, imageSide);

            var titleFont = TitleFont ?? Resources.GameHeaderFont;
            float titleLocalH = titleFont.MeasureString("Ay").Y * TitleScale;
            string safeTitle = FilterForFont(title, titleFont);

            float titleWinLeft  = (int)(drawPosition.X + textX * scale);
            float titleWinWidth = textWidth * scale;
            float titleY        = (int)(drawPosition.Y + Padding * scale);
            float titleGlyphScale = scale * TitleScale;

            UpdateTitleMarquee(safeTitle, titleFont, textWidth);

            if (!MarqueeTitle || _marqueeOverflow <= 0f)
            {
                spriteBatch.DrawString(titleFont, safeTitle,
                    new Vector2(titleWinLeft, titleY),
                    TitleColor * transparency, 0f, Vector2.Zero, titleGlyphScale, SpriteEffects.None, 0f);
            }
            else
            {
                float scrollScreen = _marqueeScroll * scale;
                DrawTitleClipped(spriteBatch, titleFont, safeTitle,
                    titleWinLeft, titleWinWidth, titleWinLeft - scrollScreen,
                    titleY, titleGlyphScale, TitleColor * transparency);
            }

            EnsureWrapped(desc, textWidth);

            float lineLocalH = DrawHelper.MeasureString("Ay").Y * DescriptionScale;

            if (Game1.LanguageManager.CurrentLanguageCode == "chn")
                lineLocalH += ChineseLineSpacing;

            float startY = Padding + titleLocalH + TitleSpacing;

            float winBottomLocal = Size.Y - Padding;
            float windowH = winBottomLocal - startY;
            float contentH = _wrapLines.Count * lineLocalH;

            UpdateDescMarquee(desc, contentH, windowH, lineLocalH);

            float winTopScreen     = (int)(drawPosition.Y + startY * scale);
            float winBottomScreen  = (int)(drawPosition.Y + winBottomLocal * scale);
            float descScrollScreen = _descScroll * scale;
            float lineX            = (int)(drawPosition.X + textX * scale);
            float descScale        = scale * DescriptionScale;
            float lineHScreen      = lineLocalH * scale;

            bool clipping = MarqueeDescription && _descOverflow > 0f;

            for (int i = 0; i < _wrapLines.Count; i++)
            {
                float lineLocalY = startY + i * lineLocalH;
                float lineYScreen = (int)(drawPosition.Y + lineLocalY * scale - descScrollScreen);

                // Skip lines fully outside the window.
                if (lineYScreen + lineHScreen <= winTopScreen) continue;
                if (lineYScreen >= winBottomScreen) continue;

                bool straddles = lineYScreen < winTopScreen || (lineYScreen + lineHScreen) > winBottomScreen;

                if (clipping && straddles)
                    DrawHelper.DrawStringClippedV(spriteBatch, _wrapLines[i],
                        new Vector2(lineX, lineYScreen), DescriptionColor * transparency,
                        descScale, winTopScreen, winBottomScreen);
                else
                    DrawHelper.DrawString(spriteBatch, _wrapLines[i],
                        new Vector2(lineX, lineYScreen), DescriptionColor * transparency,
                        0f, Vector2.Zero, descScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawImage(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency, int imageSide)
        {
            float bx = drawPosition.X + Padding * scale;
            float by = drawPosition.Y + Padding * scale;
            float side = imageSide * scale;

            if (_image != null)
            {
                var src = new Rectangle(0, 0, HdTexture.LogicalWidth(_image), HdTexture.LogicalHeight(_image));
                float ar = src.Width / (float)src.Height;
                float dw = side, dh = side;
                if (ar >= 1f) dh = side / ar; else dw = side * ar;
                float ox = bx + (side - dw) / 2f;
                float oy = by + (side - dh) / 2f;

                spriteBatch.Draw(_image,
                    new Rectangle((int)ox, (int)oy, (int)dw, (int)dh), src, ImageColor * transparency);
            }
            else if (ShowImageFrame)
            {
                DrawFrame(spriteBatch, bx, by, side, ImageFrameColor * (0.6f * transparency), Math.Max(1f, scale));
            }

            if (_achieved && ShowEarnedBadge && _earnedImage != null)
            {
                float badgeH = side * EarnedBadgeScale;
                float badgeAr = _earnedImage.Width / (float)_earnedImage.Height;
                float badgeW = badgeH * badgeAr;
                float badgeX = drawPosition.X + (Size.X - Padding) * scale - badgeW;
                float badgeY = by + side - badgeH;

                spriteBatch.Draw(_earnedImage,
                    new Rectangle((int)badgeX, (int)badgeY, (int)badgeW, (int)badgeH), null, Color.White * transparency);
            }
        }

        private void DrawFrame(SpriteBatch spriteBatch, float x, float y, float side, Color color, float thickness)
        {
            int t = (int)thickness;
            int s = (int)side;
            spriteBatch.Draw(Resources.SprWhite, new Rectangle((int)x, (int)y, s, t), color);
            spriteBatch.Draw(Resources.SprWhite, new Rectangle((int)x, (int)y + s - t, s, t), color);
            spriteBatch.Draw(Resources.SprWhite, new Rectangle((int)x, (int)y, t, s), color);
            spriteBatch.Draw(Resources.SprWhite, new Rectangle((int)x + s - t, (int)y, t, s), color);
        }

        private void EnsureWrapped(string text, float widthLocal)
        {
            if (text == _wrapSource && widthLocal == _wrapWidth && DescriptionScale == _wrapScale)
                return;

            _wrapSource = text;
            _wrapWidth = widthLocal;
            _wrapScale = DescriptionScale;
            _wrapLines.Clear();

            if (string.IsNullOrEmpty(text))
                return;

            // Language files store line breaks as a literal "\n", so convert them to real newlines.
            text = text.Replace("\\n", "\n");

            foreach (var paragraph in text.Split('\n'))
            {
                string current = "";

                // Chinese has no spaces to split on, so wrap character by character.
                if (Game1.LanguageManager.CurrentLanguageCode == "chn")
                {
                    foreach (char c in paragraph)
                    {
                        string test = current + c;
                        float testWidth = DrawHelper.MeasureString(test).X * DescriptionScale;

                        if (testWidth > widthLocal && current.Length > 0)
                        {
                            _wrapLines.Add(current);
                            current = c == ' ' ? "" : c.ToString();
                        }
                        else
                            current = test;
                    }
                }
                // All other languages wrap on spaces.
                else
                {
                    foreach (var w in paragraph.Split(' '))
                    {
                        string test = current.Length == 0 ? w : current + " " + w;
                        float testWidth = DrawHelper.MeasureString(test).X * DescriptionScale;

                        if (testWidth > widthLocal && current.Length > 0)
                        {
                            _wrapLines.Add(current);
                            current = w;
                        }
                        else
                            current = test;
                    }
                }
                _wrapLines.Add(current);
            }
        }

        private string FilterForFont(string input, SpriteFont font)
        {
            if (string.IsNullOrEmpty(input) || font == null)
                return input ?? "";

            var supported = font.Characters;
            var result = new StringBuilder(input.Length);
            foreach (var c in input)
                result.Append(supported.Contains(c) ? c : '?');
            return result.ToString();
        }

        private void UpdateTitleMarquee(string title, SpriteFont font, float windowWidthLocal)
        {
            float fullWidth = font.MeasureString(title).X * TitleScale; // local units
            float overflow = fullWidth - windowWidthLocal;

            if (title != _marqueeSource || overflow != _marqueeOverflow)
            {
                _marqueeSource = title;
                _marqueeOverflow = overflow;
                _marqueeScroll = 0f;
                _marqueeTimer = 0f;
                _marqueePhase = 0;
            }

            if (overflow <= 0f)
            {
                _marqueeScroll = 0f;
                return;
            }

            float dt = Game1.DeltaTime; // ms, same source your page uses

            switch (_marqueePhase)
            {
                case 0: // pause at the start
                    _marqueeTimer += dt;
                    if (_marqueeTimer >= MarqueeStartDelay) { _marqueeTimer = 0f; _marqueePhase = 1; }
                    break;

                case 1: // scroll left to reveal the right side
                    _marqueeScroll += MarqueeSpeed * (dt / 1000f);
                    if (_marqueeScroll >= overflow) { _marqueeScroll = overflow; _marqueeTimer = 0f; _marqueePhase = 2; }
                    break;

                case 2: // pause at the end, then snap back and repeat
                    _marqueeTimer += dt;
                    if (_marqueeTimer >= MarqueeEndDelay) { _marqueeScroll = 0f; _marqueeTimer = 0f; _marqueePhase = 0; }
                    break;
            }
        }

        private void DrawTitleClipped(SpriteBatch spriteBatch, SpriteFont font, string text, float winLeftScreen, float winWidthScreen, float originScreenX, float yScreen, float glyphScale, Color color)
        {
            var glyphs = font.GetGlyphs();
            var tex = font.Texture;
            float winRightScreen = winLeftScreen + winWidthScreen;

            float penX = 0f;
            bool first = true;

            foreach (var c in text)
            {
                if (c == '\r')
                    continue;

                if (!glyphs.TryGetValue(c, out var g))
                {
                    if (font.DefaultCharacter.HasValue && glyphs.TryGetValue(font.DefaultCharacter.Value, out var dg))
                        g = dg;
                    else
                        continue;
                }

                if (first) { penX = Math.Max(g.LeftSideBearing, 0f); first = false; }
                else        penX += font.Spacing + g.LeftSideBearing;

                var src = g.BoundsInTexture;
                float drawLeft = originScreenX + (penX + g.Cropping.X) * glyphScale;
                float drawTop  = yScreen + g.Cropping.Y * glyphScale;

                // Only draw the portion of the glyph that lands inside the window.
                if (src.Width > 0 &&
                    drawLeft + src.Width * glyphScale > winLeftScreen &&
                    drawLeft < winRightScreen)
                {
                    // Trim the left edge.
                    float overLeft = winLeftScreen - drawLeft;
                    if (overLeft > 0f)
                    {
                        int cut = (int)Math.Ceiling(overLeft / glyphScale);
                        if (cut < src.Width) { src.X += cut; src.Width -= cut; drawLeft += cut * glyphScale; }
                        else                  src.Width = 0;
                    }
                    // Trim the right edge.
                    float overRight = (drawLeft + src.Width * glyphScale) - winRightScreen;
                    if (overRight > 0f)
                    {
                        int cut = (int)Math.Ceiling(overRight / glyphScale);
                        src.Width = Math.Max(0, src.Width - cut);
                    }
                    if (src.Width > 0)
                        spriteBatch.Draw(tex, new Vector2((int)drawLeft, (int)drawTop), src, color,
                            0f, Vector2.Zero, glyphScale, SpriteEffects.None, 0f);
                }
                penX += g.Width + g.RightSideBearing;
            }
        }

        private void UpdateDescMarquee(string source, float contentHeightLocal, float windowHeightLocal, float lineHeightLocal)
        {
            float trailingSlack = lineHeightLocal * DescBottomSlack;
            float visibleOverflow = contentHeightLocal - windowHeightLocal - trailingSlack;
            float overflow = (visibleOverflow > 0f)
                ? contentHeightLocal - windowHeightLocal
                : 0f;

            if (source != _descMarqueeSource || overflow != _descOverflow)
            {
                _descMarqueeSource = source;
                _descOverflow = overflow;
                _descScroll = 0f;
                _descTimer = 0f;
                _descPhase = 0;
            }
            if (overflow <= 0f)
            {
                _descScroll = 0f;
                return;
            }
            float dt = Game1.DeltaTime;

            switch (_descPhase)
            {
                case 0:
                {
                    _descTimer += dt;
                    if (_descTimer >= DescMarqueeStartDelay) { _descTimer = 0f; _descPhase = 1; }
                    break;
                }
                case 1:
                {
                    _descScroll += DescMarqueeSpeed * (dt / 1000f);
                    if (_descScroll >= overflow) { _descScroll = overflow; _descTimer = 0f; _descPhase = 2; }
                    break;
                }
                case 2:
                {
                    _descTimer += dt;
                    if (_descTimer >= DescMarqueeEndDelay) { _descScroll = 0f; _descTimer = 0f; _descPhase = 0; }
                    break;
                }
            }
        }
    }
}
