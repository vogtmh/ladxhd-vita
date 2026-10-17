using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjPowder : GameObject
    {
        private Rectangle _sourceRectangle = new Rectangle(1, 145, 4, 4);

        private Vector3[] _points = new Vector3[3];
        private Vector3[] _velocity = new Vector3[3];
        private float[] _live = new float[3];

        private bool _damage;
        private Box _hitBox;

        // Values configurable via lahdmod.
        private int   powder_damage  = 2;
        private float powder_gravity = 5.00f;
        private bool  light_source   = true;
        private int   light_red      = 230;
        private int   light_grn      = 230;
        private int   light_blu      = 255;
        private float light_bright   = 0.35f;
        private int   light_size     = 20;

        public ObjPowder(Map.Map map, float posX, float posY, float posZ, bool playerPowder) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjPowder.lahdmod");
            ModFile.Parse(modFile, this);

            EntityPosition = new CPosition(posX, posY, posZ);
            EntitySize = new Rectangle(-8, -16, 16, 16);
             
            _points[0] = new Vector3(posX, posY, posZ + 7);
            _points[1] = new Vector3(posX - 1, posY, posZ + 6);
            _points[2] = new Vector3(posX + 1, posY, posZ + 6);

            _velocity[0] = new Vector3(0, 0, 0);
            _velocity[1] = new Vector3(-Game1.RandomNumber.Next(50, 150) / 1000f, 0, 0);
            _velocity[2] = new Vector3(Game1.RandomNumber.Next(50, 150) / 1000f, 0, 0);

            _live[0] = 1;
            _live[1] = 1;
            _live[2] = 1;

            // play sound effect
            if (playerPowder)
                Game1.AudioManager.PlaySoundEffect("D360-05-05", true);
            else
                _damage = true;

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
        }

        private Box GetPowderAttackBox()
        {
            var key = MapManager.ObjLink.Direction;
            var offsets = key switch
            {
                1 => ( -3, -13, +10, +16),
                2 => ( -8, -10, +16, +10),
                3 => ( -7, -10, +10, +16),
                _ => ( -8, -10, +16, +10)
            };
            var (xOff, yOff, wOff, hOff) = offsets;

            return new Box(
                EntityPosition.X + xOff,
                EntityPosition.Y + yOff, 0,
                wOff, hOff, 8);
        }

        public void Update()
        {
            var finishedFalling = true;
            for (var i = 0; i < _points.Length; i++)
            {
                // If it hasn't dealt damage yet, deal damage.
                if (_points[i].Z <= 4.5 && !_damage)
                {
                    _damage = true;
                    _hitBox = GetPowderAttackBox();
                    Map.Objects.Hit(this, new Vector2(EntityPosition.X, EntityPosition.Y), _hitBox, HitType.MagicPowder, powder_damage, false, false);
                }
                if (_points[i].Z <= 0)
                {
                    _points[i].Z = 0;
                    _live[i] -= 0.1f * Game1.TimeMultiplier;
                }
                else
                {
                    _points[i] += _velocity[i] * Game1.TimeMultiplier;
                    _velocity[i].Z += powder_gravity / 100 * Game1.TimeMultiplier * -1;
                }
                if (_live[i] > 0)
                    finishedFalling = false;
                else
                    _live[i] = 0;
            }
            if (finishedFalling)
                Map.Objects.DeleteObjects.Add(this);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            for (var i = 0; i < _points.Length; i++)
            {
                var rawPosition = new Vector2(
                    _points[i].X - _sourceRectangle.Width / 2,
                    _points[i].Y - _sourceRectangle.Height - _points[i].Z);
                var position = GameSettings.PixelSnapping
                    ? new Vector2(MathF.Round(rawPosition.X), MathF.Round(rawPosition.Y))
                    : rawPosition;
                spriteBatch.Draw(Resources.SprItem, position, _sourceRectangle, Color.White * _live[i]);
            }
        }

        public void DrawLight(SpriteBatch spriteBatch)
        {
            // Debug: Draw the damage box.
            // var drawRect = _hitBox.Rectangle();
            // spriteBatch.Draw(Resources.SprWhite, new Vector2(drawRect.X, drawRect.Y), new Rectangle(0, 0, (int)drawRect.Width, (int)drawRect.Height), Color.Red * 1.00f);

            // Draw the lighting effect.
            if (light_source && GameSettings.ObjectLights)
            {
                for (var i = 0; i < _points.Length; i++)
                    DrawHelper.DrawLight(spriteBatch, new Rectangle((int)_points[i].X - light_size / 2, (int)(_points[i].Y - _points[i].Z) - 2 - light_size / 2, light_size, light_size), new Color(light_red, light_grn, light_blu) * light_bright * _live[i]);
            }
        }
    }
}
