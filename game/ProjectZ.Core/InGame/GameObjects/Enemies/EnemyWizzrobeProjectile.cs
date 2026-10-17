using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyWizzrobeProjectile : GameObject
    {
        private readonly BodyComponent _body;
        private readonly CBox _damageCollider;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly DrawCSpriteComponent _drawComponent;

        private int _direction;
        private bool _reflected;

        public EnemyWizzrobeProjectile(Map.Map map, Vector2 position, int direction, float speed) : base(map)
        {
            EntityPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(-6, -6, 12, 12);
            CanReset = true;
            OnReset = Reset;
            _direction = direction;

            _sprite = new CSprite("wizzrobe shot", EntityPosition, Vector2.Zero);
            _sprite.Center = new Vector2(6, 6);
            _sprite.Rotation = MathF.PI / 2f * _direction;

            _body = new BodyComponent(EntityPosition, -2, -2, 4, 4, 8)
            {
                FieldRectangle = map.GetField(position.X, position.Y),
                IgnoresZ = true,
                IgnoreHoles = true,
                CollisionTypes = Values.CollisionTypes.Normal,
                MoveCollision = OnCollision
            };
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * speed;

            _damageCollider = new CBox(EntityPosition, -3, -3, 0, 6, 6, 4);
            
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_damageCollider, HitType.Enemy, 4) { OnDamage = OnDamage, Direction = direction });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, _drawComponent = new DrawCSpriteComponent(_sprite, Values.LayerTop));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public override void Reset()
        {
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
            Map.Objects.DeleteObjects.Add(this);
        }

        private void Update()
        {
            // blink
            var blinkTime = 66.667f;
            _sprite.SpriteShader = (Game1.TotalGameTime % (blinkTime * 2) < blinkTime) ? Resources.DamageSpriteShader0 : null;

            // If the shot was reflected, try to hit an enemy.
            if (_reflected)
            {
                // Probably the closest parallel to player damage types is the Bow.
                var collision = Map.Objects.Hit(MapManager.ObjLink, EntityPosition.Position, _damageCollider.Box, HitType.Bomb, 4, false, false);
                if ((collision & Values.HitCollision.Enemy) != 0)
                    Map.Objects.DeleteObjects.Add(this);
            }
        }

        private bool OnDamage()
        {
            // Get whether or not the player recieved damage.
            bool damaged = _damageField.DamagePlayer();

            // If the player was not damaged, check if it should be reflected.
            if (MapManager.ObjLink.Reflected(_direction, new bool[]{!damaged, !_reflected}))
            {
                Reflect();
            }
            // Whether the player was damaged or it was blocked, destroy the shot.
            else
            {
                Despawn();
                _body.Velocity = new Vector3(-_body.VelocityTarget.X * 0.25f, -_body.VelocityTarget.Y * 0.25f, 1.5f);
                _body.VelocityTarget = Vector2.Zero;
            }
            // Return the damage state for the damage field component.
            return damaged;
        }

        private void Reflect()
        {
            // Play the reflected sound effect.
            Game1.AudioManager.PlaySoundEffect("D360-22-16");

            // Reverse the direction of the projectile.
            var newDirection = _direction + 2 % 4;
            _drawComponent.Sprite.Rotation = MathF.PI / 2f * newDirection;

            // Don't let the spear reflect more than once.
            _reflected = true;

            // It should not damage Link from this point on.
            _damageField.IsActive = false;

            // Reverse direction and reset the shots lifespan.
            var newVelocity = -_body.VelocityTarget * 1.75f;

            // Reverse the movement of the projectile.
            _body.VelocityTarget = newVelocity;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            // Don't detect a shield hit when shield is Mirror Shield and reflection is enabled.
            if (MapManager.ObjLink.Reflected(_direction))
                return false;

            // Under normal circumstances block and knock the player back.
            Despawn();
            return true;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            // When it hits the wall despawn.
            Despawn();
        }

        private void Despawn()
        {
            // Show the sparking effect whenever the object is destroyed.
            var animation = new ObjSparkingEffect(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 0, 0);
            Map.Objects.SpawnObject(animation);
            Map.Objects.DeleteObjects.Add(this);
        }
    }
}
