using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Components
{
    class BodyDrawComponent : DrawComponent
    {
        public int WaterOutlineOffsetY;

        public bool WaterOutline = true;
        public bool DeepWaterOutline = false;
        public bool Grass = true;

        private readonly BodyComponent _body;

        public delegate void DrawFunc(SpriteBatch spriteBatch);
        private DrawFunc _draw;

        private static Rectangle _sourceGrass;
        private static Rectangle _sourceWater;

        public BodyDrawComponent(BodyComponent body, DrawFunc draw, int layer)
            : base(layer, body.Position)
        {
            _body = body;
            _draw = draw;

            Draw = DrawFunction;

            if (_sourceGrass == Rectangle.Empty)
                _sourceGrass = Resources.SourceRectangle("grass");
            if (_sourceWater == Rectangle.Empty)
                _sourceWater = Resources.SourceRectangle("water");
        }

        public BodyDrawComponent(BodyComponent body, CSprite sprite, int layer)
            : this(body, sprite.Draw, layer) { }

        public void DrawFunction(SpriteBatch spriteBatch)
        {
            if (!IsActive)
                return;

            var isOnWater = _body.CurrentFieldState.HasFlag(MapStates.FieldStates.Water) && WaterOutline ||
                            _body.CurrentFieldState.HasFlag(MapStates.FieldStates.DeepWater) && DeepWaterOutline;

            // Draw the water stuff.
            if (_body.IsActive && isOnWater && _body.IsGrounded && _body.Position.Z <= 0)
            {
                var rawWaterX = _body.Position.X + _body.OffsetX + _body.Width / 2f - 6;
                var rawWaterY = _body.Position.Y - _body.Position.Z + _body.OffsetY + _body.Height + WaterOutlineOffsetY;
                var waterX = GameSettings.PixelSnapping ? MathF.Round(rawWaterX) : rawWaterX;
                var waterY = GameSettings.PixelSnapping ? MathF.Round(rawWaterY) : rawWaterY;

                spriteBatch.Draw(Resources.SprObjects, new Vector2(waterX, waterY - 6),
                    new Rectangle(_sourceWater.X, _sourceWater.Y + (Game1.TotalGameTime % 133 > 66 ? 9 : 0), _sourceWater.Width, _sourceWater.Height / 2), Color.White);

                _draw(spriteBatch);

                spriteBatch.Draw(Resources.SprObjects, new Vector2(waterX, waterY - 2),
                    new Rectangle(_sourceWater.X, _sourceWater.Y + _sourceWater.Height / 2 + (Game1.TotalGameTime % 133 > 66 ? 9 : 0), _sourceWater.Width, _sourceWater.Height / 2), Color.White);
            }
            else
            {
                _draw(spriteBatch);
            }

            // Draw grass if the body is standing on grass.
            if (_body.IsActive && Grass && _body.CurrentFieldState.HasFlag(MapStates.FieldStates.Grass) && _body.Position.Z < 4)
            {
                var flip = (_body.Position.X + _body.Position.Y) % 8 > 4;
                var rawGrassX = _body.Position.X + _body.OffsetX + _body.Width / 2f;
                var rawGrassY = _body.Position.Y + _body.OffsetY + _body.Height - 8;
                var grassX = GameSettings.PixelSnapping ? MathF.Round(rawGrassX) : rawGrassX;
                var grassY = GameSettings.PixelSnapping ? MathF.Round(rawGrassY) : rawGrassY;

                spriteBatch.Draw(Resources.SprObjects, new Vector2(grassX - 8, grassY), _sourceGrass, Color.White,
                    0, Vector2.Zero, Vector2.One, !flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
                spriteBatch.Draw(Resources.SprObjects, new Vector2(grassX, grassY), _sourceGrass, Color.White,
                    0, Vector2.Zero, Vector2.One, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            }
        }
    }
}
