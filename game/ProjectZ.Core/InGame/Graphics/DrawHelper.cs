using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Graphics
{
    class DrawHelper
    {
        // more then 1000 will only make sense on a high resolution with a small game scale
        public const int MaxShadowIndices = 1000;
        public static int CurrentShadowIndex;
        public static short[] IndexDataShadow = new short[MaxShadowIndices * 6];
        public static float ShadowHeight;
        public static float ShadowOffset;
        public static Texture2D LastShadowTexture;

        private static Matrix projectionMatrix;
        private static Matrix outMatrix;
        public static VertexPositionPositionColorTexture[] ShadowVertexArray = new VertexPositionPositionColorTexture[MaxShadowIndices * 4];

        public static void StartShadowDrawing()
        {
            LastShadowTexture = null;
            CurrentShadowIndex = 0;
        }

        public static void EndShadowDrawing()
        {
            if (CurrentShadowIndex > 0)
                DrawIndexedDataNew();
        }

        // TODO_End: this should be done using normal spritebatch.Draw()
        public static void DrawShadow(Texture2D sprImage, Vector2 drawPosition, Rectangle sourceRectangle,
            float drawWidth, float drawHeight, bool mirror, float height, float rotation, Color color)
        {

            if (LastShadowTexture != null && (LastShadowTexture != sprImage ||
                CurrentShadowIndex >= MaxShadowIndices || ShadowHeight != height || ShadowOffset != rotation))
            {
                // draw the stored data
                DrawIndexedDataNew();
                CurrentShadowIndex = 0;
            }

            // Coordinates are calculated manually instead of by SpriteBatch, so divide the logical source
            // rectangle by the logical texture size. Using "sprImage.Width" would break HD textures.
            SetVertexPtIndexed(ShadowVertexArray, CurrentShadowIndex * 4, drawPosition, sourceRectangle,
                drawWidth, drawHeight, HdTexture.LogicalWidth(sprImage), HdTexture.LogicalHeight(sprImage), mirror, color);

            SetIndexBuffer(IndexDataShadow, CurrentShadowIndex * 6, CurrentShadowIndex * 4);

            CurrentShadowIndex++;

            ShadowHeight = height;
            ShadowOffset = rotation;
            LastShadowTexture = sprImage;
        }

        public struct VertexPositionPositionColorTexture : IVertexType
        {
            public Vector2 Position;
            public Vector2 TextureCoordinate;
            public Vector2 UpperLeftPosition;
            public Vector2 SourceSize;
            public Color Color;

            public VertexPositionPositionColorTexture(
                Vector2 position, Vector2 textureCoordinate, Vector2 upperLeftPosition, Vector2 sourceSize, Color color)
            {
                Position = position;
                TextureCoordinate = textureCoordinate;
                UpperLeftPosition = upperLeftPosition;
                SourceSize = sourceSize;
                Color = color;
            }

            public static readonly VertexDeclaration VertexDeclaration;

            VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

            static VertexPositionPositionColorTexture()
            {
                var elements = new[]
                {
                    new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
                    new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                    new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
                    new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 2),
                    new VertexElement(32, VertexElementFormat.Color, VertexElementUsage.Color, 0),
                };
                VertexDeclaration = new VertexDeclaration(elements);
            }
        }

        public static void SetVertexPtIndexed(
            VertexPositionPositionColorTexture[] buffer, int index,
            Vector2 position, Rectangle sourceRectangle, float drawWidth, float drawHeight,
            int textureWidth, int textureHeight, bool mirror, Color color)
        {
            var posLeft = position.X;
            var posRight = position.X + drawWidth;// sourceRectangle.Width;
            var posTop = position.Y;
            var posBottom = position.Y + drawHeight;// sourceRectangle.Height;

            var left = (!mirror ? sourceRectangle.X : sourceRectangle.Right) / (float)textureWidth;
            var right = (!mirror ? sourceRectangle.Right : sourceRectangle.X) / (float)textureWidth;
            var top = sourceRectangle.Y / (float)textureHeight;
            var bottom = sourceRectangle.Bottom / (float)textureHeight;

            buffer[index + 0] = new VertexPositionPositionColorTexture(
                new Vector2(posLeft, posTop), new Vector2(left, top), position,
                new Vector2(sourceRectangle.Width, sourceRectangle.Height), color);
            buffer[index + 1] = new VertexPositionPositionColorTexture(
                new Vector2(posRight, posTop), new Vector2(right, top), position,
                new Vector2(sourceRectangle.Width, sourceRectangle.Height), color);
            buffer[index + 2] = new VertexPositionPositionColorTexture(
                new Vector2(posLeft, posBottom), new Vector2(left, bottom), position,
                new Vector2(sourceRectangle.Width, sourceRectangle.Height), color);
            buffer[index + 3] = new VertexPositionPositionColorTexture(
                new Vector2(posRight, posBottom), new Vector2(right, bottom), position,
                new Vector2(sourceRectangle.Width, sourceRectangle.Height), color);
        }

        public static void DrawIndexedDataNew()
        {
            projectionMatrix = Matrix.CreateOrthographicOffCenter(0, 
                Game1.GameManager.CurrentRenderWidth, Game1.GameManager.CurrentRenderHeight, 0, 0, -1);
            outMatrix = MapManager.Camera.TransformMatrix * projectionMatrix;

            Resources.FullShadowEffect.Parameters["xViewProjection"].SetValue(outMatrix);
            Resources.FullShadowEffect.Parameters["height"].SetValue(ShadowHeight);
            Resources.FullShadowEffect.Parameters["offsetX"].SetValue(ShadowOffset);

            foreach (var pass in Resources.FullShadowEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                Game1.Graphics.GraphicsDevice.Textures[0] = LastShadowTexture;
                Game1.Graphics.GraphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList, ShadowVertexArray, 0, CurrentShadowIndex * 4, IndexDataShadow, 0, CurrentShadowIndex * 2);
            }
        }

        public static void SetIndexBuffer(short[] buffer, int position, int offset)
        {
            buffer[position + 0] = (short)(offset + 0);
            buffer[position + 1] = (short)(offset + 1);
            buffer[position + 2] = (short)(offset + 2);

            buffer[position + 3] = (short)(offset + 2);
            buffer[position + 4] = (short)(offset + 1);
            buffer[position + 5] = (short)(offset + 3);
        }

        public static void DrawLight(SpriteBatch spriteBatch, Rectangle lightRectangle, Color lightColor)
        {
            spriteBatch.Draw(Resources.SprLight, lightRectangle, lightColor);
        }

        public static void DrawCenter(SpriteBatch spriteBatch, Texture2D sprTexture, Point offset, Rectangle centerRectangle, Rectangle sourceRectangle, int scale)
        {
            spriteBatch.Draw(sprTexture, new Rectangle(
                offset.X + (centerRectangle.X + centerRectangle.Width / 2 - sourceRectangle.Width / 2) * scale,
                offset.Y + (centerRectangle.Y + centerRectangle.Height / 2 - sourceRectangle.Height / 2) * scale,
                sourceRectangle.Width * scale,
                sourceRectangle.Height * scale), sourceRectangle, Color.White);
        }

        public static void DrawNormalized(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float scale = 1.0f)
        {
            // Snap game pixels if Pixel Snapping is enabled.
            var vecX = GameSettings.PixelSnapping 
                ? MathF.Round(position.X)
                : (float)Math.Round(position.X * MapManager.Camera.Scale) / MapManager.Camera.Scale;
            var vecY = GameSettings.PixelSnapping
                ? MathF.Round(position.Y)
                : (float)Math.Round(position.Y * MapManager.Camera.Scale) / MapManager.Camera.Scale;
            var normalizedPosition = new Vector2(vecX, vecY);
            spriteBatch.Draw(texture, normalizedPosition, sourceRectangle, color, 0, Vector2.Zero, new Vector2(scale), SpriteEffects.None, 0);
        }

        public static void DrawNormalized(SpriteBatch spriteBatch, DictAtlasEntry sprite, Vector2 position, Color color)
        {
            // Snap game pixels if Pixel Snapping is enabled.
            var vecX = GameSettings.PixelSnapping 
                ? MathF.Round(position.X)
                : (float)Math.Round(position.X * MapManager.Camera.Scale) / MapManager.Camera.Scale;
            var vecY = GameSettings.PixelSnapping
                ? MathF.Round(position.Y)
                : (float)Math.Round(position.Y * MapManager.Camera.Scale) / MapManager.Camera.Scale;
            var normalizedPosition = new Vector2(vecX, vecY);
            spriteBatch.Draw(sprite.Texture, normalizedPosition, sprite.SourceRectangle, color, 0, sprite.Origin, Vector2.One, SpriteEffects.None, 0);
        }

        //-------------------------------------------------------------------------------------------------------------------------------------------------
        //
        //  FUNCTIONS FOR BITMAP FONTS
        //
        //-------------------------------------------------------------------------------------------------------------------------------------------------

        public static int LineSpacing => Game1.LanguageManager.CurrentLanguageCode == "chn"
            ? Resources.ChinaFont.LineHeight
            : Resources.GameFont.LineSpacing;

        public static void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            if (Game1.LanguageManager.CurrentLanguageCode == "chn")
                spriteBatch.DrawString(Resources.ChinaFont, text, position, color);
            else
                spriteBatch.DrawString(Resources.GameFont, text, position, color);
        }

        public static void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            if (Game1.LanguageManager.CurrentLanguageCode == "chn")
                spriteBatch.DrawString(Resources.ChinaFont, text, position, color, rotation, origin, new Vector2(scale, scale), effects, layerDepth);
            else
                spriteBatch.DrawString(Resources.GameFont, text, position, color, rotation, origin, scale, effects, layerDepth);
        }

        public static Vector2 MeasureString(string text)
        {
            if (Game1.LanguageManager.CurrentLanguageCode == "chn")
                return Resources.ChinaFont.MeasureString(text);
            try    { return Resources.GameFont.MeasureString(text); }
            catch  { return new Vector2(0, 0); }
        }

        public static void DrawStringClippedV(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale, float clipTop, float clipBottom)
        {
            if (Game1.LanguageManager.CurrentLanguageCode == "chn")
                DrawBitmapFontClippedV(spriteBatch, Resources.ChinaFont, text, position, scale, color, clipTop, clipBottom);
            else
                DrawSpriteFontClippedV(spriteBatch, Resources.GameFont, text, position, scale, color, clipTop, clipBottom);
        }

        private static void DrawSpriteFontClippedV(SpriteBatch sb, SpriteFont font, string text, Vector2 position, float scale, Color color, float clipTop, float clipBottom)
        {
            var glyphs = font.GetGlyphs();
            var tex = font.Texture;
            float penX = 0f;
            bool first = true;

            foreach (var c in text)
            {
                if (c == '\r') continue;

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
                float drawLeft = position.X + (penX + g.Cropping.X) * scale;
                float drawTop  = position.Y + g.Cropping.Y * scale;

                // Trim top edge.
                float overTop = clipTop - drawTop;
                if (overTop > 0f)
                {
                    int cut = (int)Math.Ceiling(overTop / scale);
                    if (cut < src.Height) { src.Y += cut; src.Height -= cut; drawTop += cut * scale; }
                    else                   src.Height = 0;
                }

                // Trim bottom edge.
                float overBot = (drawTop + src.Height * scale) - clipBottom;
                if (overBot > 0f)
                {
                    int cut = (int)Math.Ceiling(overBot / scale);
                    src.Height = Math.Max(0, src.Height - cut);
                }

                if (src.Width > 0 && src.Height > 0)
                    sb.Draw(tex, new Vector2((int)drawLeft, (int)drawTop), src, color,
                        0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                penX += g.Width + g.RightSideBearing;
            }
        }

        private static void DrawBitmapFontClippedV(SpriteBatch sb, BitmapFont font, string text, Vector2 position, float scale, Color color, float clipTop, float clipBottom)
        {
            float h = font.MeasureString(text).Height * scale;
            if (position.Y >= clipTop - 0.5f && position.Y + h <= clipBottom + 0.5f)
                sb.DrawString(font, text, position, color,
                    0f, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0f);
        }
    }
}
