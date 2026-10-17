using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Graphics
{
    class ItemDrawHelper
    {
        private static DictAtlasEntry SpriteLetter;
        private static DictAtlasEntry SpriteHeart;
        private static DictAtlasEntry SpriteRubee;
        private static DictAtlasEntry SpriteKey;

        private static Rectangle RecLetters;
        private static Rectangle RecHeart;
        private static Rectangle RecRubee;
        private static Rectangle RecKey;

        private static int _heartsDistanceA = 1;
        private static int _heartsDistanceB = 1;

        private static readonly int LetterMargin = 1;

        public static Point RubeeSize;
        public static Point KeySize;

        private static int _paddingHud = 4;

        private static Color _relictColorOne = new Color(0, 135, 115);
        private static Color _relictColorTwo = new Color(255, 255, 255);

        private static Rectangle _recRelicts = new Rectangle(224, 128, 16, 16);

        private static float _colorCounter;
        private const int ColorTime = 8000;

        public static int LetterWidth = 6;
        public static int LetterHeight = 6;

        private static float _rubyCounter;
        private static float _rubyTime;
        private static float _rubySoundIndex;
        private static int _rubyStart;
        private static int _rubyEnd;
        private static int _rubyCount;
        private static bool _rubyAnimation;

        private static float _heartCounter;
        private static int _heartCount;
        private static bool _heartAnimation;
        private static bool _heartSounds;

        public static Color[] CloakColors = { new Color(16, 173, 66), new Color(24, 132, 255), new Color(255, 8, 41) };

        // Values configurable via lahdmod.
        static int  tunic_red_r = 255;
        static int  tunic_red_g = 8;
        static int  tunic_red_b = 41;
        static int  tunic_grn_r = 16;
        static int  tunic_grn_g = 173;
        static int  tunic_grn_b = 66;
        static int  tunic_blu_r = 24;
        static int  tunic_blu_g = 132;
        static int  tunic_blu_b = 255;
        static bool instrument_color_cycle = true;

        public static void Load()
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ItemDrawHelper.lahdmod");

            ModFile.ParseStatic(modFile, typeof(ItemDrawHelper));

            Color redCloak = new Color(tunic_red_r, tunic_red_g, tunic_red_b);
            Color grnCloak = new Color(tunic_grn_r, tunic_grn_g, tunic_grn_b);
            Color bluCloak = new Color(tunic_blu_r, tunic_blu_g, tunic_blu_b);

            CloakColors = new Color[] { grnCloak, bluCloak, redCloak };

            if (!instrument_color_cycle)
            {
                _relictColorOne = Color.Transparent;
                _relictColorTwo = Color.Transparent;
            }
            SpriteLetter = Resources.GetSprite("ui letter");
            SpriteHeart = Resources.GetSprite("ui heart");
            SpriteRubee = Resources.GetSprite("ui ruby");
            SpriteKey = Resources.GetSprite("ui key");

            RecLetters = SpriteLetter.ScaledRectangle;
            RecHeart = SpriteHeart.ScaledRectangle;
            RecRubee = SpriteRubee.ScaledRectangle;
            RecKey = SpriteKey.ScaledRectangle;

            RubeeSize = new Point((RecLetters.Width + LetterMargin) * 3 + RecRubee.Width, RecRubee.Height);
            KeySize = new Point((RecLetters.Width + LetterMargin) * 3 + RecKey.Width, RecKey.Height);
        }

        public static void RefreshImagesUI()
        {
            // There isn't really a need to do this as these don't change from language to language, but just in case mods
            // or something else in the future want to swap out these textures let's reload them anyway.
            SpriteLetter = Resources.GetSprite("ui letter");
            SpriteHeart = Resources.GetSprite("ui heart");
            SpriteRubee = Resources.GetSprite("ui ruby");
            SpriteKey = Resources.GetSprite("ui key");
        }

        public static void Init()
        {
            _rubyAnimation = false;
            _heartAnimation = false;

            var item = Game1.GameManager.GetItem("ruby");
            if (item != null)
                _rubyCount = item.Count;
            else
                _rubyCount = 0;

            _heartCount = Game1.GameManager.CurrentHealth;
        }

        public static void Update()
        {
            if (instrument_color_cycle)
            {
                // Cycles the instruments through colors.
                _colorCounter += Game1.DeltaTime;

                var upTime = ColorTime / 3;
                var timeR = (_colorCounter - upTime) % ColorTime;
                var timeG = (_colorCounter) % ColorTime;
                var timeB = (_colorCounter - upTime * 2) % ColorTime;

                // Rotates through the color wheel.
                var colorR = MathHelper.Clamp(MathF.Abs(timeR - upTime) / (upTime / 2.25f) - 1, 0, 1);
                var colorG = MathHelper.Clamp(MathF.Abs(timeG - upTime) / (upTime / 2.25f) - 1, 0, 1);
                var colorB = MathHelper.Clamp(MathF.Abs(timeB - upTime) / (upTime / 2.25f) - 1, 0, 1);

                _relictColorOne = new Color(
                    MathHelper.Clamp((byte)(colorR * 120), 0, 255),
                    MathHelper.Clamp((byte)(colorG * 80), 0, 255),
                    MathHelper.Clamp((byte)(colorB * 140), 0, 255));

                _relictColorTwo = new Color(
                    MathHelper.Clamp((byte)(colorR * 220), 0, 255),
                    MathHelper.Clamp((byte)(colorG * 180), 0, 255),
                    MathHelper.Clamp((byte)(colorB * 240), 0, 255));
            }
            UpdateRubyAnimation();
            UpdateHeartAnimation();
        }

        public static void InstantRupeeCollection(int value)
        {
            _rubyCount = value;
            _rubyEnd = value;
            _rubyAnimation = false;
        }

        private static void UpdateRubyAnimation()
        {
            var item = Game1.GameManager.GetItem("ruby");
            var realCount = 0;
            if (item != null)
                realCount = item.Count;

            if (!Game1.GameManager.InGameOverlay.TextboxOverlay.IsOpen &&
                !Game1.GameManager.InGameOverlay.MenuIsOpen())
            {
                // start the animation?
                if (!_rubyAnimation)
                {
                    if (_rubyCount + 1 < realCount)
                    {
                        _rubyCounter = 0;
                        _rubyAnimation = true;

                        _rubyStart = _rubyCount;
                        _rubyEnd = realCount;

                        _rubySoundIndex = 0;

                        _rubyCount++;
                        _rubyTime = 32 * (realCount - _rubyCount);
                        if (_rubyTime > 2000)
                            _rubyTime = 2000;
                    }
                    else if (_rubyCount - 1 > realCount)
                    {
                        _rubyCounter = 0;
                        _rubyAnimation = true;

                        _rubyStart = _rubyCount;
                        _rubyEnd = realCount;

                        _rubySoundIndex = 0;

                        _rubyCount--;
                        _rubyTime = 32 * (_rubyCount - realCount);
                        if (_rubyTime > 2000)
                            _rubyTime = 2000;
                    }
                    else
                        _rubyCount = realCount;
                }

                if (_rubyAnimation && _rubyCount != _rubyEnd)
                {
                    _rubyCounter += Game1.DeltaTime;
                    if (_rubyCounter > _rubyTime)
                        _rubyCounter = _rubyTime;

                    _rubyCount = (int)MathHelper.Lerp(_rubyStart, _rubyEnd, _rubyCounter / _rubyTime);

                    // ruby sound every 2 frames
                    if ((_rubySoundIndex + 1) * 32 <= _rubyCounter)
                    {
                        _rubySoundIndex++;
                        Game1.AudioManager.PlaySoundEffect("D370-05-05");
                    }
                }
                else
                {
                    _rubyAnimation = false;
                }
            }
        }

        public static void EnableHeartAnimationSound()
        {
            _heartSounds = true;
        }

        private static void UpdateHeartAnimation()
        {
            var realCount = Game1.GameManager.CurrentHealth;

            if (!Game1.GameManager.InGameOverlay.TextboxOverlay.IsOpen &&
                !Game1.GameManager.InGameOverlay.MenuIsOpen())
            {
                // start the animation?
                if (!_heartAnimation)
                {
                    if (_heartCount + 4 < realCount)
                    {
                        _heartCounter = 0;
                        _heartAnimation = true;
                        if (!_heartSounds)
                            _heartCount += 4;
                        else
                            _heartCounter = 200;
                    }
                    else
                        _heartCount = realCount;
                }

                if (_heartAnimation && _heartCount != realCount)
                {
                    _heartCounter += Game1.DeltaTime;

                    if (_heartCounter > 200)
                    {
                        _heartCounter -= 200;
                        _heartCount += 4;

                        if (_heartSounds)
                            Game1.AudioManager.PlaySoundEffect("D370-06-06");

                        if (_heartCount >= realCount)
                        {
                            _heartCount = realCount;
                            _heartAnimation = false;
                            _heartSounds = false;
                        }
                    }
                }
            }
        }

        public static void DrawLevel(SpriteBatch spriteBatch, int posX, int posY, int number, int scale, Color textColor)
        {
            // add L- if the number is < 0
            // L- 0 1 2 3...
            var letter0 = number < 0 ? 0 : number / 10 + 1;
            var letter1 = number < 0 ? -number + 1 : number % 10 + 1;

            spriteBatch.Draw(SpriteLetter.Texture, new Rectangle(posX, posY, RecLetters.Width * scale, RecLetters.Height * scale),
                new Rectangle(RecLetters.X + letter0 * (RecLetters.Width + (int)SpriteLetter.Scale), RecLetters.Y, RecLetters.Width, RecLetters.Height), textColor);
            spriteBatch.Draw(SpriteLetter.Texture, new Rectangle(posX + RecLetters.Width * scale, posY, RecLetters.Width * scale, RecLetters.Height * scale),
                new Rectangle(RecLetters.X + letter1 * (RecLetters.Width + (int)SpriteLetter.Scale), RecLetters.Y, RecLetters.Width, RecLetters.Height), textColor);
        }

        public static void DrawNumber(SpriteBatch spriteBatch, int posX, int posY, int number, int length, int scale, Color textColor)
        {
            for (var i = 0; i < length; i++)
            {
                var letter = number / (int)Math.Pow(10, length - i - 1) % 10 + 1;

                spriteBatch.Draw(SpriteLetter.Texture, new Rectangle(
                        posX + (RecLetters.Width + LetterMargin) * i * scale,
                        posY, RecLetters.Width * scale, RecLetters.Height * scale),
                    new Rectangle(RecLetters.X + letter * (RecLetters.Width + (int)SpriteLetter.Scale), RecLetters.Y, RecLetters.Width, RecLetters.Height), textColor);
            }
        }

        public static void DrawItem(SpriteBatch spriteBatch, GameItem item, Vector2 position, Color color, int scale, bool mapSprite = false)
        {
            if (item == null)
                return;

            var baseItem = item.SourceRectangle.HasValue ? item : Game1.GameManager.ItemManager[item.Name];

            Rectangle sourceRectangle;
            DictAtlasEntry sprite;

            if (baseItem.MapSprite != null && mapSprite)
            {
                sprite = baseItem.MapSprite;
                sourceRectangle = baseItem.MapSprite.ScaledRectangle;
            }
            else
            {
                sprite = baseItem.Sprite;
                sourceRectangle = baseItem.Sprite.ScaledRectangle;
            }
            // Item is a rupee that blinks.
            if (item.Name == "ruby" && item.AnimateSprite)
            {
                var frameLength = 10 / 60f * 1000;
                sourceRectangle.X += (int)((Game1.TotalGameTime % (frameLength * 4)) / frameLength) * (sourceRectangle.Width + sprite.TextureScale);
            }
            // Check if a piece of power is currently active.
            if (Game1.GameManager.PieceOfPowerIsActive && (item.Name == "sword1" || item.Name == "sword2" || item.Name == "sword1PoP" || item.Name == "sword2PoP"))
            {
                // The sword being held needs a slight Y offset to be accurate.
                var offsetY = item.Name.Contains("PoP") ? 1 : 0;

                // The rate the sword blinks.
                if (Game1.TotalTime % (16 / 0.06) >= 8 / 0.06)
                {
                    // Get the blink sprite. Inventory sword and sword being held are different.
                    var swordSprite = item.Name.Contains("PoP")
                        ? Resources.GetSprite("pop_blink")
                        : Resources.GetSprite("swordBlink");

                    // Get the rectangle of the replacement.
                    sourceRectangle = swordSprite.SourceRectangle;
                }
                DrawHelper.DrawNormalized(spriteBatch, sprite.Texture, new Vector2(position.X, position.Y - offsetY), sourceRectangle, color, scale * sprite.Scale);
            }
            // Blink a piece of power currently sitting in the game world.
            else if (item.Name == "pieceOfPower")
            {
                if (Game1.TotalTime % (16 / 0.06) >= 8 / 0.06)
                    sourceRectangle.X += sourceRectangle.Width + sprite.TextureScale;

                DrawHelper.DrawNormalized(spriteBatch, sprite.Texture, new Vector2(position.X, position.Y), sourceRectangle, color, scale * sprite.Scale);
            }
            // If the player has a blue tunic then draw it as blue.
            else if (item.Name == "cloakBlue")
            {
                var transparency = color.A / 255f;
                DrawHelper.DrawNormalized(spriteBatch, sprite.Texture, new Vector2(position.X, position.Y), sourceRectangle, new Color(253, 188, 140) * transparency, scale * sprite.Scale);
                DrawHelper.DrawNormalized(spriteBatch, sprite.Texture, new Vector2(position.X, position.Y),
                    new Rectangle(sourceRectangle.X, sourceRectangle.Y + sourceRectangle.Height, sourceRectangle.Width, sourceRectangle.Height), CloakColors[1] * transparency, scale * sprite.Scale);
            }
            // If the player has a red tunic then draw it as red.
            else if (item.Name == "cloakRed")
            {
                var transparency = color.A / 255f;
                DrawHelper.DrawNormalized(spriteBatch, sprite.Texture, new Vector2(position.X, position.Y), sourceRectangle, new Color(253, 188, 140) * transparency, scale * sprite.Scale);
                DrawHelper.DrawNormalized(spriteBatch, sprite.Texture, new Vector2(position.X, position.Y),
                    new Rectangle(sourceRectangle.X, sourceRectangle.Y + sourceRectangle.Height, sourceRectangle.Width, sourceRectangle.Height), CloakColors[2] * transparency, scale * sprite.Scale);
            }
            // If it's any other item except an instrument simply just draw it.
            else if (!item.Instrument)
            {
                DrawHelper.DrawNormalized(spriteBatch, sprite.Texture, new Vector2(position.X, position.Y), sourceRectangle, color, scale * sprite.Scale);
            }
            // If it's an instrument it has its own specialized draw function.
            else
            {
                // Snap game pixels if Pixel Snapping is enabled.
                var vecX = GameSettings.PixelSnapping 
                    ? MathF.Round(position.X)
                    : (float)Math.Round(position.X * MapManager.Camera.Scale) / MapManager.Camera.Scale;
                var vecY = GameSettings.PixelSnapping
                    ? MathF.Round(position.Y)
                    : (float)Math.Round(position.Y * MapManager.Camera.Scale) / MapManager.Camera.Scale;
                var normalizedPosition = new Vector2(vecX, vecY);
                DrawInstrument(spriteBatch, sprite, normalizedPosition);
            }
        }

        public static void DrawInstrument(SpriteBatch spriteBatch, DictAtlasEntry sprite, Vector2 position)
        {
            var rectangle = sprite.ScaledRectangle;

            spriteBatch.Draw(Resources.SprItem, position,
                new Rectangle(rectangle.X + 0, rectangle.Y, rectangle.Width, rectangle.Height), Color.White, 0, Vector2.Zero, sprite.Scale, SpriteEffects.None, 0);

            spriteBatch.Draw(Resources.SprItem, position,
                new Rectangle(rectangle.X + 16 * sprite.TextureScale, rectangle.Y, rectangle.Width, rectangle.Height), _relictColorOne, 0, Vector2.Zero, sprite.Scale, SpriteEffects.None, 0); ;

            spriteBatch.Draw(Resources.SprItem, position,
                new Rectangle(rectangle.X + 32 * sprite.TextureScale, rectangle.Y, rectangle.Width, rectangle.Height), _relictColorTwo, 0, Vector2.Zero, sprite.Scale, SpriteEffects.None, 0);
        }

        public static void DrawInstrument(SpriteBatch spriteBatch, DictAtlasEntry sprite, Vector2 position, float uiScale)
        {
            var rectangle = sprite.ScaledRectangle;
            var finalScale = sprite.Scale * uiScale;

            spriteBatch.Draw(Resources.SprItem, position,
                new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height),
                Color.White, 0, Vector2.Zero, finalScale, SpriteEffects.None, 0);

            spriteBatch.Draw(Resources.SprItem, position,
                new Rectangle(rectangle.X + 16 * sprite.TextureScale, rectangle.Y, rectangle.Width, rectangle.Height),
                _relictColorOne, 0, Vector2.Zero, finalScale, SpriteEffects.None, 0);

            spriteBatch.Draw(Resources.SprItem, position,
                new Rectangle(rectangle.X + 32 * sprite.TextureScale, rectangle.Y, rectangle.Width, rectangle.Height),
                _relictColorTwo, 0, Vector2.Zero, finalScale, SpriteEffects.None, 0);
        }

        public static void DrawRelictBackground(SpriteBatch spriteBatch, Vector2 position)
        {
            // draw the background
            spriteBatch.Draw(Resources.SprItem, position, _recRelicts, _relictColorOne);
        }

        public static void DrawItemWithInfo(SpriteBatch spriteBatch, GameItemCollected itemCollected, Point offset, Rectangle rectangle, int scale, Color color, bool forceMapSprite = false)
        {
            if (itemCollected == null)
                return;

            var item = Game1.GameManager.ItemManager[itemCollected.Name];

            Rectangle sourceRectangle;
            if (item.SourceRectangle.HasValue)
                sourceRectangle = item.SourceRectangle.Value;
            else
            {
                // at least the base item needs to have a source rectangle
                var baseItem = Game1.GameManager.ItemManager[item.Name];
                sourceRectangle = baseItem.SourceRectangle.Value;
            }

            var width = sourceRectangle.Width;

            if (item.Level > 0)
                width += 1 + RecLetters.Width * 2 + LetterMargin;
            else if (item.MaxCount != 1)
                width += 1 + RecLetters.Width * item.DrawLength + LetterMargin;

            var itemPosition = new Point(
                offset.X + rectangle.X * scale + rectangle.Width * scale / 2 - width / 2 * scale,
                offset.Y + rectangle.Y * scale + rectangle.Height * scale / 2 - sourceRectangle.Height / 2 * scale);

            var textPosition = new Point(
                itemPosition.X + (sourceRectangle.Width + 1) * scale,
                itemPosition.Y + sourceRectangle.Height * scale - RecLetters.Height * scale);

            // draw the item
            DrawItem(spriteBatch, item, new Vector2(itemPosition.X, itemPosition.Y), color, scale, forceMapSprite);

            if (item.Level > 0)
            {
                // draw the level of the item
                DrawLevel(spriteBatch,
                    textPosition.X, textPosition.Y,
                    -item.Level, scale, Color.Black * (color.A / 255f));
            }
            else if (item.MaxCount != 1)
            {
                // draw the count of the item
                DrawNumber(spriteBatch,
                    textPosition.X, textPosition.Y,
                    itemCollected.Count, item.DrawLength, scale, Color.Black * (color.A / 255f));
            }
        }

        public static Rectangle GetRubeeRectangle(Point position, int scale)
        {
            return new Rectangle(
                position.X - _paddingHud * scale, position.Y - _paddingHud * scale,
                ((RecLetters.Width + LetterMargin) * 3 + RecRubee.Width + _paddingHud * 2) * scale,
                (RecLetters.Height + _paddingHud * 2) * scale);
        }

        public static void DrawRubee(SpriteBatch spriteBatch, Point position, int scale, Color color)
        {
            // draw the number
            DrawNumber(spriteBatch, position.X, position.Y, _rubyCount, 3, scale, color);

            // draw the rubee count
            spriteBatch.Draw(SpriteRubee.Texture, new Rectangle(
                    position.X + (RecLetters.Width + LetterMargin) * 3 * scale,
                    position.Y - 1 * scale,
                    RecRubee.Width * scale + 1,
                    RecRubee.Height * scale), RecRubee, Color.White * (color.A / 255f));
        }

        public static Rectangle GetKeyRectangle(Point position, int scale)
        {
            return new Rectangle(
                position.X - _paddingHud * scale, position.Y - _paddingHud * scale,
                ((RecLetters.Width + LetterMargin) * 3 + RecKey.Width + _paddingHud * 2) * scale,
                (RecLetters.Height + _paddingHud * 2) * scale);
        }
        public static void DrawSmallKeys(SpriteBatch spriteBatch, Point position, int scale, Color color)
        {
            var keyItem = Game1.GameManager.GetItem("smallkey");

            if (keyItem == null) return;

            // draw the key icon
            spriteBatch.Draw(SpriteKey.Texture, new Rectangle(
                    position.X + (RecLetters.Width + LetterMargin) * 3 * scale,
                    position.Y - 2 * scale,
                    RecKey.Width * scale,
                    RecKey.Height * scale), RecKey, Color.White * (color.A / 255f));

            // draw the number
            DrawNumber(spriteBatch, position.X, position.Y, keyItem.Count, 3, scale, Color.Black * (color.A / 255f));
        }

        public static Rectangle GetHeartRectangle(Point position, int scale)
        {
            var width = MathHelper.Clamp(Game1.GameManager.MaxHearts, 0, 7);
            var height = (int)Math.Ceiling(Game1.GameManager.MaxHearts / 7.0f);
            return new Rectangle(position.X - _paddingHud * scale, position.Y - _paddingHud * scale,
                (width * RecHeart.Width + (width - 1) + _paddingHud * 2) * scale,
                (RecHeart.Height * height + _paddingHud * 2) * scale);
        }

        public static void DrawHearts(SpriteBatch spriteBatch, Point position, int scale, Color color)
        {
            // draw the hearts
            for (var i = 0; i < Game1.GameManager.MaxHearts; i++)
            {
                var heartValue = _heartCount - i * 4;
                var type = 0;

                if (heartValue <= 0)
                    type = 4;
                else if (heartValue <= 3)
                    type = 4 - heartValue;

                spriteBatch.Draw(SpriteHeart.Texture, new Rectangle(
                        position.X + (RecHeart.Width + _heartsDistanceA) * (i % 7) * scale,
                        position.Y + (RecHeart.Width + _heartsDistanceA) * (i / 7) * scale,
                        RecHeart.Width * scale,
                        RecHeart.Height * scale),
                    new Rectangle(
                        RecHeart.X + type * (RecHeart.Width + (int)(_heartsDistanceB * SpriteHeart.Scale)),
                        RecHeart.Y, RecHeart.Width, RecHeart.Height), color);
            }
        }
    }
}
