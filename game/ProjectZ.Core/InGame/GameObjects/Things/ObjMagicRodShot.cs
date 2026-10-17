using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    class ObjMagicRodShot : GameObject
    {
        private readonly CSprite _sprite;
        private readonly CBox _damageBox;

        private const int SpawnTime = 25;
        private const int FadeInTime = 25;
        private const int FadeOutTime = 50;

        private float _spawnCounter;
        private bool _dead;

        // Values configurable via lahdmod.
        private int   magicrod_damage   = 1;
        private int   magicrod_duration = 600;
        private float magicrod_speed    = 3.00f;
        private bool  magicrod_cast2d   = false;
        private bool  light_source      = true;
        private int   light_red         = 255;
        private int   light_grn         = 240;
        private int   light_blu         = 220;
        private float light_bright      = 0.7f;
        private int   light_size        = 42;

        public ObjMagicRodShot(Map.Map map, CPosition linkPos, Vector2 offsetpos, int direction) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjMagicRodShot.lahdmod");
            ModFile.Parse(modFile, this);

            CanReset = true;
            OnReset = Reset;

            var spawnPosition = new Vector3(linkPos.X + offsetpos.X, linkPos.Y + offsetpos.Y, linkPos.Z);

            if (magicrod_cast2d)
                spawnPosition = new Vector3(linkPos.X + offsetpos.X, linkPos.Y + offsetpos.Y - linkPos.Z, 0);

            EntityPosition = new CPosition(spawnPosition.X, spawnPosition.Y, spawnPosition.Z);
            EntitySize = new Rectangle(-8, -8, 16, 16);

            _damageBox = new CBox(EntityPosition, -3, -3, 0, 6, 6, 8, true);

            _sprite = new CSprite("magicRodShot", EntityPosition)
            {
                Color = Color.Transparent
            };

            var body = new BodyComponent(EntityPosition, -2 + (direction == 1 ? 2 : (direction == 3 ? -2 : 0)), -2, 4, 4, 8)
            {
                VelocityTarget = AnimationHelper.DirectionOffset[direction] * (Game1.GameManager.PieceOfPowerIsActive ? magicrod_speed + 1 : magicrod_speed),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Instrument,
                CollisionTypesIgnore = Values.CollisionTypes.ThrowWeaponIgnore | Values.CollisionTypes.Switch | Values.CollisionTypes.Bush,
                MoveCollision = OnCollision,
                IgnoreHoles = true,
                IgnoresZ = true,
                IgnoreInsideCollision = false,
                Level = MapStates.GetLevel(MapManager.ObjLink.Body.CurrentFieldState)
            };
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(BodyComponent.Index, body);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
        }

        public override void Reset()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        private void Update()
        {
            _spawnCounter += Game1.DeltaTime;

            if (_spawnCounter > SpawnTime)
            {
                if (_spawnCounter > magicrod_duration)
                    _sprite.Color = Color.White * (1 - Math.Clamp((_spawnCounter - magicrod_duration) / FadeOutTime, 0, 1));
                else
                    _sprite.Color = Color.White * Math.Clamp((_spawnCounter - SpawnTime) / FadeInTime, 0, 1);

                if (_spawnCounter > magicrod_duration + FadeOutTime)
                {
                    _dead = true;
                    Map.Objects.DeleteObjects.Add(this);
                    return;
                }
            }
            // When Modern Camera is enabled, use the camera's current bounds to determine when object collides with screen's edge. 
            if (!Camera.ClassicMode && !MapManager.Camera.GetGameView().Contains(EntityPosition.Position))
            {
                OnCollision(Values.BodyCollision.None);
                return;
            }
            // When Classic Camera is enabled, use current field to determine when object collides with screen's edge. 
            else if (Camera.ClassicMode && !MapManager.ObjLink.CurrentField.Contains(EntityPosition.Position))
            {
                OnCollision(Values.BodyCollision.None);
                return;
            }
            var collision = Map.Objects.Hit(this, EntityPosition.Position, _damageBox.Box, HitType.MagicRod, magicrod_damage, false);

            // A special collision type that impacts the target but spawns passive fire.
            if ((collision & Values.HitCollision.SpawnFire) != 0)
                OnCollision(Values.BodyCollision.None);

            // Most collision types just remove the projectile.
            if ((collision & (Values.HitCollision.Blocking | Values.HitCollision.Repelling | Values.HitCollision.Enemy)) != 0)
            {
                _dead = true;
                Map.Objects.DeleteObjects.Add(this);
            }
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if (_dead)
                return;

            var animation = new ObjBurningEffect(Map, (int)EntityPosition.X, (int)EntityPosition.Y - (int)EntityPosition.Z, 0, 0, true);
            Map.Objects.SpawnObject(animation);
            Map.Objects.RegisterAlwaysAnimateObject(animation);
            Map.Objects.DeleteObjects.Add(this);
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (light_source && GameSettings.ObjectLights)
            {
                Rectangle _lightRectangle = new Rectangle((int)EntityPosition.X - light_size / 2, (int)EntityPosition.Y - light_size / 2 - (int)EntityPosition.Z, light_size, light_size);
                DrawHelper.DrawLight(spriteBatch, _lightRectangle, new Color(light_red, light_grn, light_blu) * light_bright);
            }
        }
    }
}
