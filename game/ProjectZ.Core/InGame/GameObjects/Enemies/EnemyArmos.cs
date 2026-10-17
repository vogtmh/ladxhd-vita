using System;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyArmos : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly AiTriggerRandomTime _walkCounter;
        private readonly Animator _animator;
        private readonly AnimationComponent _animationComponent;
        private readonly BodyComponent _body;
        private readonly BodyCollisionComponent _bodyCollision;
        private readonly CarriableComponent _carriableComponent;
        private readonly DamageFieldComponent _damageField;

        private readonly string _animationPrefix;

        private float _moveSpeed = 0.5f;
        private float _counter;
        private bool _collided;
        private int _direction;

        private int _lives = EnemyLives.Armos;
        private int _dropIndex = 11;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyArmos() : base("armos") { }

        public EnemyArmos(Map.Map map, int posX, int posY, bool darkArmos) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 17);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/armos");
            _animationPrefix = darkArmos ? "_dark" : "";

            var sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8)
            {
                MoveCollision = OnMoveCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Enemy |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                IgnoreInsideCollision = true,
                InsideCollisionEscape = 0.5f,
                Bounciness = 0.25f,
                Drag = 0.8f
            };

            var stateIdle = new AiState { Init = InitIdle };
            var stateAwaking = new AiState(UpdateAwaking) { Init = InitAwaking };
            var stateWalking = new AiState { Init = InitWalking };
            stateWalking.Trigger.Add(_walkCounter = new AiTriggerRandomTime(ChangeDirection, 1000, 1500));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("awaking", stateAwaking);
            _aiComponent.States.Add("walking", stateWalking);

            new AiFallState(_aiComponent, _body, null, null);
            new AiDeepWaterState(_body);

            _aiStunnedState = new AiStunnedState(_body, _aiComponent, _animationComponent, 3300, 900) { ShakeOffset = 1, SilentStateChange = false, ReturnState = "walking",  OnStun = OnStun, OnStunRelease = OnStunRelease };
            _aiDamageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex, true, false) { SpawnPowerups = false };

            _aiComponent.ChangeState("idle");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 0, 14, 15, 8);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(CollisionComponent.Index, _bodyCollision = new BodyCollisionComponent(_body, Values.CollisionTypes.Enemy));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 8) { IsActive = false });
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit) { StunHookshot = true, StunThrown = true, BombMultiplier = true, ArrowMultiplier = true });
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        public override void Reset()
        {
            if (_carriableComponent.IsPickedUp)
                return;

            _aiStunnedState.EndStun();
            _damageField.IsActive = false;
            _bodyCollision.IsActive = true;
            _carriableComponent.IsActive = false;
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
            _isThrown = false;
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
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
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

        private void InitIdle()
        {
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("idle" + _animationPrefix);
        }

        private void InitAwaking()
        {
            _animator.Play("awaking" + _animationPrefix);
        }

        private void UpdateAwaking()
        {
            // wobble
            _counter += Game1.DeltaTime;
            _animationComponent.SpriteOffset.X = -8 + 1 * MathF.Sin(MathF.PI * ((_counter / 1000) * (60 / 4f)));

            if (!_animator.IsPlaying)
            {
                _animationComponent.SpriteOffset.X = -8;
                _aiComponent.ChangeState("walking");
            }

            _animationComponent.UpdateSprite();
        }

        private void InitWalking()
        {
            ChangeDirection();
            _animator.Play("walking" + _animationPrefix);
            _damageField.IsActive = true;
            _bodyCollision.IsActive = false;
            _collided = false;
        }

        private void ChangeDirection()
        {
            // random new direction
            _direction = Game1.RandomNumber.Next(0, 8);
            var radius = (float)Math.PI * (_direction / 4f);
            _body.VelocityTarget = new Vector2((float)Math.Sin(radius), (float)Math.Cos(radius)) * _moveSpeed;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);
            else if (_aiComponent.CurrentStateId == "idle")
                _aiComponent.ChangeState("awaking");

            return true;
        }
        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_aiDamageState.IsInDamageState())
                return Values.HitCollision.None;

            if (_aiComponent.CurrentStateId == "idle" || _aiComponent.CurrentStateId == "awaking")
                return Values.HitCollision.RepellingParticle;

            if (hitType == HitType.MagicRod || hitType == HitType.MagicPowder)
                return Values.HitCollision.Blocking;

            if (hitType == HitType.Bow || hitType == HitType.Bomb || hitType == HitType.Boomerang || hitType == HitType.Hookshot || hitType == HitType.ThrownObject)
                return _aiDamageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            _aiDamageState.HitKnockBack(gameObject, direction, hitType, pieceOfPower, false);

            Game1.AudioManager.PlaySoundEffect("D360-09-09");

            if (pieceOfPower)
                Game1.AudioManager.PlaySoundEffect("D370-17-11");

            return Values.HitCollision.Blocking;
        }

        private void OnMoveCollision(Values.BodyCollision direction)
        {
            // cut the time we walk into the wall
            if (!_collided)
            {
                _walkCounter.CurrentTime /= 2;
                _collided = true;
            }

            if (_isThrown && (direction & Values.BodyCollision.Floor) != 0)
            {
                _isThrown = false;
                _carriableComponent.Thrown = false;
                _aiStunnedState.ResetBodyState();
                _body.VelocityTarget = Vector2.Zero;
                _aiStunnedState.ResetStun();
            }
        }
    }
}
