using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    class EnemyNut : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private Vector2 _moveDirection;
        private float _moveSpeed;
        private int _collisionCount;

        private bool _isDead;
        private int _lives = 1;
        private int _dropIndex = 2;

        public EnemyNut(Map.Map map, Vector3 position, Vector3 direction) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(position.X, position.Y, position.Z);
            EntitySize = new Rectangle(-6, -48, 12, 48);
            CanReset = true;
            OnReset = Reset;

            var throwDirection = new Vector2(direction.X, direction.Y);

            _sprite = new CSprite(Resources.SprEnemies, EntityPosition, new Rectangle(306, 2, 12, 12), new Vector2(-6, -12));
            _moveSpeed = throwDirection.Length();

            if (_moveSpeed > 0)
                _moveDirection = throwDirection / _moveSpeed;
            else
                _moveSpeed = 1f;

            _body = new BodyComponent(EntityPosition, -6, -12, 12, 12, 8)
            {
                MoveCollision = MoveCollision,
                CollisionTypes = Values.CollisionTypes.Field,
                FieldRectangle = map.GetField(position.X, position.Y),
                Gravity = -0.1f,
                Bounciness = 0.75f
            };

            _body.Velocity = new Vector3(0, 0, direction.Z);
            _body.VelocityTarget = _moveDirection * _moveSpeed;

            _aiComponent = new AiComponent();
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex)
            {
                OnBurn = OnBurn,
                OnLiveZeroed = OnLivesZeroed
            };

            var movingState = new AiState(UpdateMoving);
            _aiComponent.States.Add("moving", movingState);
            _aiComponent.ChangeState("moving");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6,  4, true);
            var hittableBox = new CBox(EntityPosition, -5, -11, 0, 10, 10, 10, true);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(hittableBox, OnPush));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));

            var shadow = new DrawShadowSpriteComponent(_sprite, Resources.SprShadow, EntityPosition, new Rectangle(0, 0, 65, 66), new Vector2(-6, -6), 12, 6);
            AddComponent(DrawShadowComponent.Index, shadow);

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void Reset()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        private void UpdateMoving()
        {
            if (_isDead)
                return;

            // Update the movement of the coconut.
            _body.VelocityTarget = _moveDirection * _moveSpeed;
        }

        private void StopMovement()
        {
            // Disable velocity, stop bouncing, and update collision.
            _isDead = true;
            _body.Velocity.Z = 0;
            _body.VelocityTarget = Vector2.Zero;
            _body.Bounciness = 0f;
            _body.CollisionTypes = Values.CollisionTypes.Field | Values.CollisionTypes.Normal;
        }

        private void OnBurn()
        {
            // Disable components and cancel movement velocities and behaviors.
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
            StopMovement();
        }

        private void OnLivesZeroed()
        {
            // Cancel movement velocities and behaviors.
            StopMovement();
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void MoveCollision(Values.BodyCollision collisionType)
        {
            // If it's already dead stop updating move collision.
            if (_isDead)
                return;

            // After it bounces a few times, remove it.
            _collisionCount++;
            if (_collisionCount > 3)
            {
                Map.Objects.DeleteObjects.Add(this);
                return;
            }
            // Play bounce sound effect.
            Game1.AudioManager.PlaySoundEffect("D360-09-09");

            // Set a new random direction.
            var angle = (Game1.RandomNumber.Next(0, 100) / 100f) * (float)Math.PI * 2f;
            _moveDirection = new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle));
            _body.VelocityTarget = _moveDirection * _moveSpeed;

            // Flip the sprite.
            _sprite.SpriteEffect ^= SpriteEffects.FlipHorizontally;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }
    }
}
