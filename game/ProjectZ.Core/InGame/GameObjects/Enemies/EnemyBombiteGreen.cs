using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyBombiteGreen : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly AiTriggerSwitch _damageCooldown;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly CarriableComponent _carriableComponent;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private const float WalkSpeed = 0.5f;
        private RectangleF _fieldRect;

        private bool _startedAnimation;
        private bool _follow;
        private int _direction;
        private int _lives = EnemyLives.BombiteGreen;
        private int _dropIndex = 10;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyBombiteGreen() : base("bombiteGreen") { }

        public EnemyBombiteGreen(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/bombiteGreen");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-7, -16));

            _body = new BodyComponent(EntityPosition, -6, -12, 12, 11, 8)
            {
                MoveCollision = OnMoveCollision,
                AbsorbPercentage = 0.9f,
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall,
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f,
                Bounciness = 0.25f,
                Drag = 0.85f,
            };
            _fieldRect = map.GetField(posX, posY);

            var stateIdle = new AiState(UpdateIdle) { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerRandomTime(ChangeDirection, 250, 500));
            var stateFollow = new AiState(UpdateFollow);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("follow", stateFollow);
            new AiFallState(_aiComponent, _body, OnHoleAbsorb, null);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, false) { SpawnPowerups = false };
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, animationComponent, 3300, 900) { SilentStateChange = false, OnStun = OnStun, OnStunRelease = OnStunRelease };

            _aiComponent.Trigger.Add(_damageCooldown = new AiTriggerSwitch(250));
            _aiComponent.ChangeState("idle");
            ChangeDirection();

            var damageBox   = new CBox(EntityPosition, -3, -8, 0, 6, 6, 4);
            var hittableBox = new CBox(EntityPosition, -6, -12, 0, 12, 12, 8);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite) { Height = 1.0f, Rotation = 0.1f });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { StunHookshot = true, StunBoomerang = true, BombMultiplier = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(hittableBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        public override void Reset()
        {
            if (_carriableComponent.IsPickedUp)
                return; 

            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _carriableComponent.IsActive = false;
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
            _animator.SpeedMultiplier = 1f;
            _isThrown = false;
            _aiStunnedState.EndStun();
        }

        private void OnStun()
        {
            _carriableComponent.IsActive = true;
            _damageField.IsActive = false;
        }

        private void OnStunRelease()
        {
            _carriableComponent.IsActive = false;
            _damageField.IsActive = true;
        }

        private void Update()
        {
            // Check if the enemy was thrown.
            if (_isThrown)
            {
                // Deal a hit to whatever it comes in contact with.
                var pos  = new Vector3(EntityPosition.X - 7, EntityPosition.Y - 14, EntityPosition.Z);
                var size = new Vector3(14, 14, 8);
                var throwBox = new Box(pos, size);

                // Find objects to hit when thrown.
                if (Map.Objects.Hit(this, throwBox.Center, throwBox, HitType.ThrownObject, 2, false) != 0)
                {
                    // Bounce off the object when hit.
                    _body.Velocity.X = -_body.Velocity.X * 0.5f;
                    _body.Velocity.Y = -_body.Velocity.Y * 0.5f;
                }
            }
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            _body.IsActive = false;
            _body.BodyBox = new CBox(EntityPosition, -4, -8 + _offsetY, 8, 8, 12);
            return new Vector3(EntityPosition.X, EntityPosition.Y - _offsetY, EntityPosition.Z);
        }

        private bool CarryUpdate(Vector3 newPosition)
        {
            // Reset the stun state as it's being carried.
            _aiStunnedState.ResetStun();

            EntityPosition.X = newPosition.X;
            EntityPosition.Y = newPosition.Y - _offsetY;
            EntityPosition.Z = newPosition.Z;

            EntityPosition.NotifyListeners();
            return true;
        }

        private void CarryThrow(Vector2 velocity)
        {
            _isThrown = true;
            _carriableComponent.Thrown = true;
            _body.IsGrounded = false;
            _body.IsActive = true;
            _body.Velocity = new Vector3(velocity.X, velocity.Y, 0) * 2.0f;
            _body.Level = MapStates.GetLevel(MapManager.ObjLink.Body.CurrentFieldState);
        }

        private void TryReleaseStun()
        {
            if (!_aiStunnedState.Active)
            {
                _damageField.IsActive = true;
            }
        }

        private void InitIdle()
        {
            _animator.Play("idle");
        }

        private void UpdateIdle()
        {
            if (_follow && !_damageState.IsInDamageState())
            {
                _aiComponent.ChangeState("follow");
                _damageField.IsActive = false;
            }
            TryReleaseStun();
        }

        private void UpdateFollow()
        {
            // start animation when slowed down enough
            if (!_startedAnimation && _body.Velocity.Length() < 0.1f)
            {
                _startedAnimation = true;
                _animator.Play("timer");
            }

            if (_startedAnimation)
            {
                if (!_animator.IsPlaying)
                    Explode();
                else if (_animator.CurrentFrameIndex > 2)
                {
                    // blink
                    _sprite.SpriteShader = Game1.TotalGameTime % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime ? Resources.DamageSpriteShader0 : null;
                }

                // move towards the player
                var direction = MapManager.ObjLink.Position - EntityPosition.Position;
                var distance = direction.Length();
                if (direction != Vector2.Zero)
                    direction.Normalize();

                if (distance > 20)
                    _body.VelocityTarget = direction;
                else
                    _body.VelocityTarget = Vector2.Zero;
            }
            TryReleaseStun();
        }

        private void ChangeDirection()
        {
            _direction = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * WalkSpeed;
        }

        private void Explode()
        {
            // spawn explosion effect
            var objExplosion = new ObjBomb(Map, EntityPosition.X, EntityPosition.Y, false, false);
            objExplosion.Explode();
            Map.Objects.SpawnObject(objExplosion);
            Map.Objects.SpawnObject(new EnemyBombiteRespawner(Map, (int)ResetPosition.X - 8, (int)ResetPosition.Y - 16, _fieldRect, true));
            Map.Objects.DeleteObjects.Add(this);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
            {
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);
                _aiComponent.ChangeState("follow");
                _damageField.IsActive = false;
            }
            return true;
        }

        private void OnHoleAbsorb()
        {
            _animator.SpeedMultiplier = 3f;
            _animator.Play("idle");
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Don't deal damage in damage state.
            if (_damageState.IsInDamageState())
                return Values.HitCollision.None;

            // Thrown objects do nothing to this enemy.
            if (hitType == HitType.ThrownObject)
                return Values.HitCollision.Blocking;

            // Bombs deal permadeath and spawn a bomb.
            if ((hitType & HitType.Bomb) != 0 && !(gameObject is EnemyBombite))
            {
                return _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);
            }
            // These weapon types are absorbed and have no effect.
            if (hitType == HitType.SwordShot || hitType == HitType.Bow || hitType == HitType.MagicPowder || hitType == HitType.MagicRod)
                return Values.HitCollision.Blocking;

            // Hookshot or Boomerang stuns the enemy.
            if (hitType == HitType.Hookshot || hitType == HitType.Boomerang)
                return _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // Play the correct hit sound effect.
            if (pieceOfPower)
                Game1.AudioManager.PlaySoundEffect("D370-17-11");
            else
                Game1.AudioManager.PlaySoundEffect("D360-03-03");

            // Piece of power should knock back the bomb.
            if (pieceOfPower)
                _damageState.HitKnockBack(gameObject, direction, hitType, pieceOfPower, false);
            else
            {
                _body.Velocity.X += direction.X * 5.0f;
                _body.Velocity.Y += direction.Y * 5.0f;
                _damageState.SetDamageState(false);
            }
            // If not stunned, start following the player.
            if (!_aiStunnedState.IsStunned())
                _follow = true;

            return Values.HitCollision.Enemy;
        }

        private void OnMoveCollision(Values.BodyCollision direction)
        {
            if (_isThrown && (direction & Values.BodyCollision.Floor) != 0)
            {
                _isThrown = false;
                _carriableComponent.Thrown = false;
                _aiStunnedState.ResetBodyState();
            }
        }
    }
}
