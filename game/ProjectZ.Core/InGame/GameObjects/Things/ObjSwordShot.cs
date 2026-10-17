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
    class ObjSwordShot : GameObject
    {
        private readonly CSprite _sprite;
        private readonly BodyComponent _body;
        private readonly CBox _damageBox;

        private readonly Vector3 _spawnPosition;

        private int _damage = 2;
        private float _spawnCounter;

        private const int SpawnTime = 10;
        private const int FadeInTime = 15;
        private const int FadeOutTime = 25;

        private float _lightState;
        private bool _collided;
        private bool _dealtHit;

        // Values configurable via lahdmod.
        private int   swordbeam_damage   = 0;
        private int   swordbeam_duration = 600;
        private float swordbeam_speed    = 4;
        private bool  swordbeam_cast2d   = false;
        private bool  light_source       = true;
        private int   light_red          = 255;
        private int   light_grn          = 255;
        private int   light_blu          = 255;
        private float light_bright       = 1.00f;
        private int   light_size         = 22;

        public ObjSwordShot(Map.Map map, CPosition linkPos, Vector2 offsetpos, int damage, int direction) : base(map)
        {
            CanReset = true;
            OnReset = Reset;

            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjSwordShot.lahdmod");
            ModFile.Parse(modFile, this);

            var spawnPosition = new Vector3(linkPos.X + offsetpos.X, linkPos.Y + offsetpos.Y, linkPos.Z);

            if (swordbeam_cast2d)
                spawnPosition = new Vector3(linkPos.X + offsetpos.X, linkPos.Y + offsetpos.Y - linkPos.Z, 0);

            EntityPosition = new CPosition(spawnPosition);
            EntitySize = new Rectangle(-8, -8, 16, 16);

            _spawnPosition = new Vector3(spawnPosition.X, spawnPosition.Y, spawnPosition.Z);

            _damage = (swordbeam_damage == 0) ? damage : swordbeam_damage;
            _damageBox = new CBox(EntityPosition, -3, -3, 0, 6, 6, 8, true);

            _sprite = new CSprite("swordShot", EntityPosition)
            {
                Color = Color.Transparent,
                Rotation = (direction - 1) * MathF.PI * 0.5f,
                DrawOffset = -AnimationHelper.DirectionOffset[direction] * 4
            };

            // offset the body to not collide with the wall if the player is standing next to one
            var directionOffset = AnimationHelper.DirectionOffset[(direction + 1) % 4];

            _body = new BodyComponent(EntityPosition, -1 + (int)directionOffset.X * 2, -1 + (int)directionOffset.Y * 2, 2, 2, 8)
            {
                VelocityTarget = AnimationHelper.DirectionOffset[direction] * swordbeam_speed,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Instrument,
                CollisionTypesIgnore = Values.CollisionTypes.Switch | Values.CollisionTypes.ThrowWeaponIgnore,
                MoveCollision = OnCollision,
                IgnoreHoles = true,
                IgnoresZ = true,
                IgnoreInsideCollision = false,
                Level = MapStates.GetLevel(MapManager.ObjLink.Body.CurrentFieldState)
            };
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));

            if (GameSettings.SwBeamShrubs)
                _body.CollisionTypesIgnore = Values.CollisionTypes.Bush | Values.CollisionTypes.Switch | Values.CollisionTypes.ThrowWeaponIgnore;
        }

        public override void Reset()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        public override void Init()
        {
            Game1.AudioManager.PlaySoundEffect("D360-59-3B");
        }

        public void Update()
        {
            _spawnCounter += Game1.DeltaTime;
            _lightState = (int)(Math.Sin(_spawnCounter / 10f) + 1.5);

            // only start showing the sprite after the spawn time
            if (_spawnCounter > SpawnTime)
            {
                if (_spawnCounter > swordbeam_duration)
                    _sprite.Color = Color.White * (1 - Math.Clamp((_spawnCounter - swordbeam_duration) / FadeOutTime, 0, 1));
                else
                    _sprite.Color = Color.White * Math.Clamp((_spawnCounter - SpawnTime) / FadeInTime, 0, 1);

                if (_spawnCounter > swordbeam_duration + FadeOutTime)
                {
                    Map.Objects.DeleteObjects.Add(this);
                    return;
                }
            }
            // When Modern Camera is enabled, use the camera's current bounds to determine when object collides with screen's edge. 
            if (!Camera.ClassicMode && !MapManager.Camera.GetGameView().Contains(EntityPosition.Position))
            {
                OnCollision(Values.BodyCollision.None);
                CreateSparkingEffect();
                return;
            }
            // When Classic Camera is enabled, use current field to determine when object collides with screen's edge. 
            else if (Camera.ClassicMode && !MapManager.ObjLink.CurrentField.Contains(EntityPosition.Position))
            {
                OnCollision(Values.BodyCollision.None);
                CreateSparkingEffect();
                return;
            }
            DealDamage();
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // draw the trail
            var spawnPosVec2 = new Vector2(_spawnPosition.X, _spawnPosition.Y);
            var spawnDistance = (new Vector2(EntityPosition.X, EntityPosition.Y) - spawnPosVec2).Length();
            var trailCount = 3;
            var distMult = 1.5f;
            for (int i = 0; i < trailCount; i++)
            {
                // If sprite snapping is enabled line up trails to the grid.
                var rawDrawPosition = new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z) + _sprite.DrawOffset - new Vector2(_body.VelocityTarget.X, _body.VelocityTarget.Y) * (trailCount - i) * distMult;
                var drawPosition = GameSettings.PixelSnapping
                    ? new Vector2(MathF.Round(rawDrawPosition.X), MathF.Round(rawDrawPosition.Y))
                    : rawDrawPosition;

                // Make sure to not show the tail behind the actual spawn position.
                if (spawnDistance > ((trailCount - i) * swordbeam_speed * distMult))
                    spriteBatch.Draw(_sprite.SprTexture, drawPosition, _sprite.SourceRectangle, _sprite.Color * (0.20f + 0.30f * ((i + 1) / (float)trailCount)),
                        _sprite.Rotation, _sprite.Center * _sprite.Scale, new Vector2(_sprite.Scale), SpriteEffects.None, 0);
            }
            var changeColor = Game1.TotalGameTime % (8 / 0.06) >= 4 / 0.06 && ObjectManager.CurrentEffect != Resources.DamageSpriteShader0.Effect;

            if (changeColor)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, Resources.DamageSpriteShader0);
            }
            // Draw the actual sword shot sprite.
            _sprite.Draw(spriteBatch);

            if (changeColor)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, null);
            }
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if (_collided)
                return;
            _collided = true;

            DealDamage();
            CreateSparkingEffect();
        }

        private void DealDamage()
        {
            // Don't let the sword shot hit more than once.
            if (_dealtHit)
                return;

            // When the sword shot collides with something remove it from the game.
            var collision = Map.Objects.Hit(this, EntityPosition.Position, _damageBox.Box, HitType.SwordShot, _damage, false);

            // When hitting an enemy or blocking collision just delete the shot.
            if ((collision & (Values.HitCollision.Blocking | Values.HitCollision.Enemy)) != 0)
            {
                _dealtHit = true;
                Map.Objects.DeleteObjects.Add(this);
            }
            // When hitting something repelling show the sparking effect.
            else if ((collision & (Values.HitCollision.Repelling | Values.HitCollision.RepellingParticle)) != 0)
            {
                _dealtHit = true;
                CreateSparkingEffect();
            }
        }

        private void CreateSparkingEffect()
        {
            var animation = new ObjSwordShotSpark(Map, (int)EntityPosition.X, (int)EntityPosition.Y - (int)EntityPosition.Z, 0, 0);
            Map.Objects.SpawnObject(animation);
            Map.Objects.DeleteObjects.Add(this);
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (light_source && GameSettings.ObjectLights)
            {
                Rectangle _lightRectangle = new Rectangle((int)EntityPosition.X - light_size / 2, (int)EntityPosition.Y - light_size / 2 - (int)EntityPosition.Z, light_size, light_size);
                DrawHelper.DrawLight(spriteBatch, _lightRectangle, new Color(light_red, light_grn, light_blu) * (0.125f + _lightState * light_bright));
            }
        }
    }
}
