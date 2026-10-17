using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Graphics;

namespace ProjectZ.InGame.GameObjects.Base.Components
{
    class DrawShadowSpriteComponent : DrawShadowComponent
    {
        public Texture2D Texture;
        public Vector2 DrawOffset;

        public CPosition Position;
        public Rectangle SourceRectangle;

        public Color Color = Color.White;
        public CSprite Sprite;
        public DictAtlasEntry SpriteId;

        public float Width;
        public float Height;

        private float? ShadowHeight;
        private float? ShadowRotation;

        public DrawShadowSpriteComponent(CSprite sprite, string spriteId, CPosition position, float? shadowHeight = null, float? shadowRotation = null)
        {
            Sprite = sprite;
            SpriteId = Resources.GetSprite(spriteId);
            Texture = SpriteId.Texture;
            Position = position;
            SourceRectangle = SpriteId.SourceRectangle;

            Width = SpriteId.SourceRectangle.Width;
            Height = SpriteId.SourceRectangle.Height;
            DrawOffset = -SpriteId.Origin;

            ShadowHeight = shadowHeight;
            ShadowRotation = shadowRotation;

            Draw = DrawShadow;
        }

        public DrawShadowSpriteComponent(CSprite sprite, Texture2D texture, CPosition position, Rectangle sourceRectangle, Vector2 drawOffset, float? shadowHeight = null, float? shadowRotation = null)
        {
            Sprite = sprite;
            Texture = texture;
            Position = position;
            SourceRectangle = sourceRectangle;

            Width = sourceRectangle.Width;
            Height = sourceRectangle.Height;
            DrawOffset = drawOffset;

            ShadowHeight = shadowHeight;
            ShadowRotation = shadowRotation;

            Draw = DrawShadow;
        }

        public DrawShadowSpriteComponent(CSprite sprite, Texture2D texture, CPosition position, Rectangle sourceRectangle, Vector2 drawOffset, int width, int height)
        {
            Sprite = sprite;
            Texture = texture;
            Position = position;
            SourceRectangle = sourceRectangle;

            DrawOffset = drawOffset;
            Width = width;
            Height = height;

            ShadowHeight = 1.0f;
            ShadowRotation = 0.0f;

            Draw = DrawShadow;
        }

        private void DrawShadow(SpriteBatch spriteBatch)
        {
            if (!IsActive || (Sprite != null && !Sprite.IsVisible))
                return;

            var position = new Vector2(Position.X + DrawOffset.X, Position.Y + DrawOffset.Y);
            DrawHelper.DrawShadow(Texture, position, SourceRectangle, Width, Height, false,
                ShadowHeight ?? Owner.Map.ShadowHeight, ShadowRotation ?? Owner.Map.ShadowRotation, Color);
        }
    }
}
