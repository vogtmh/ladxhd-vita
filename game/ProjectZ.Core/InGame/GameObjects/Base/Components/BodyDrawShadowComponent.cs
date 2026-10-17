using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Components
{
    class BodyDrawShadowComponent : DrawShadowComponent
    {
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;

        public float? Height;
        public float? Rotation;

        public Func<float> HeightOverride;
        public int ShadowWidth = 8;
        public int ShadowHeight = 4;
        public int OffsetY = 0;
        public float Transparency = 1;

        public BodyDrawShadowComponent(BodyComponent body, CSprite sprite)
        {
            _body = body;
            _sprite = sprite;
            Draw = SpriteDrawFunction;
        }

        public void SpriteDrawFunction(SpriteBatch spriteBatch)
        {
            if (!IsActive || !_sprite.IsVisible)
                return;

            // Draw the sprite's shadow.
            var multSprite = 1 - _body.Position.Z / 10f;
            if (multSprite > 0 && Owner.Map != null)
                _sprite.DrawShadow(spriteBatch, Color.White * Transparency * multSprite, -1, Height ?? Owner.Map.ShadowHeight, Rotation ?? Owner.Map.ShadowRotation);

            // Use the real Z, or the override when the host is visually airborne without it.
            var shadowZ = _body.Position.Z;
            if (shadowZ <= 0 && HeightOverride != null)
                shadowZ = HeightOverride();

            // Draw the circle shadow below the body.
            if (shadowZ > 0)
            {
                var mult = MathHelper.Clamp(shadowZ / 1f, 0, 1);
                DrawHelper.DrawShadow(Resources.SprItem, new Vector2(
                        _body.BodyBox.Box.X + _body.BodyBox.Box.Width / 2f - ShadowWidth / 2f,
                        _body.BodyBox.Box.Y + _body.BodyBox.Box.Height - ShadowHeight + OffsetY),
                    new Rectangle(1, 218, 8, 4), ShadowWidth, ShadowHeight, false, 1.0f, 0.0f, Color.White * Transparency * mult);
            }
        }
    }
}
