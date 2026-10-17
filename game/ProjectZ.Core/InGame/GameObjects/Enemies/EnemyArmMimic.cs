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
    internal class EnemyArmMimic : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly AiTriggerTimer _repelTimer;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private Vector2 _lastPosition;
        private int _direction;
        private bool _wasColliding;
        private int _lives = EnemyLives.ArmMimic;
        private int _dropIndex = 5;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyArmMimic() : base("armMimic") { }

        public EnemyArmMimic(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/arm mimic");

            var sprite = new CSprite(EntityPosition);
            var animatorComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -7, -14, 14, 14, 4)
            {
                MoveCollision = OnMoveCollision,
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Enemy |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                IsSlider = true,
                AbsorbPercentage = 0.75f,
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f,
                MaxSlideDistance = 4.0f
            };
            var stateUpdate = new AiState();

            _aiComponent = new AiComponent();
            _aiComponent.Trigger.Add(_repelTimer = new AiTriggerTimer(500));

            _aiComponent.States.Add("idle", stateUpdate);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, animatorComponent, 3300, 900) { OnStun = OnStun, OnStunRelease = OnStunRelease };

            new AiFallState(_aiComponent, _body, null, null, 300);
            new AiDeepWaterState(_body);

            _aiComponent.ChangeState("idle");

            var damageBox   = new CBox(EntityPosition, -3, -10, 2,  7,  8, 4);
            var hittableBox = new CBox(EntityPosition, -6, -12, 0, 12, 12, 4);
            var pushableBox = new CBox(EntityPosition, -5, -10, 0, 10, 10, 4);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animatorComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 12));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { StunHookshot = true, StunBoomerang = true, StunThrown = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        public override void Reset()
        {
            if (_carriableComponent.IsPickedUp)
                return; 

            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _carriableComponent.IsActive = false;
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
            _damageState.CurrentLives = EnemyLives.ArmMimic;
            _body.VelocityTarget = Vector2.Zero;
            _isThrown = false;
            _wasColliding = true;
            _aiStunnedState.EndStun();
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
            _carriableComponent.IsActive = false;
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
            // Exit early if the enemy is stunned.
            if (_aiStunnedState.Active)
                return;

            // Tracks if they moved for playing animation.
            var moved = false;

            // Move when Link is in the same field as the Arm Mimic.
            if (_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
            {
                if (_wasColliding)
                {
                    var moveVelocity = -MapManager.ObjLink.LastMoveVector;
                    var diff = (MapManager.ObjLink.Position - _lastPosition) / Game1.TimeMultiplier;

                    // Stops the enemy if the player runs into an obstacle.
                    moveVelocity = new Vector2(
                        Math.Min(Math.Abs(moveVelocity.X), Math.Abs(diff.X)) * Math.Sign(moveVelocity.X),
                        Math.Min(Math.Abs(moveVelocity.Y), Math.Abs(diff.Y)) * Math.Sign(moveVelocity.Y));

                    _body.VelocityTarget = moveVelocity;

                    if (moveVelocity.Length() > 0.01f)
                    {
                        moved = true;

                        // Use the direction from ObjLink instead of AnimationHelper since it
                        // has "bias" built into the four directions (fixes diagonal movement).
                        if (!MapManager.ObjLink.IsChargingState())
                            _direction = MapManager.ObjLink.ToDirection(moveVelocity);

                        if (_animator.CurrentAnimation.Id != "walk_" + _direction)
                            _animator.Play("walk_" + _direction);
                        else
                            _animator.Continue();
                    }
                }
                _wasColliding = true;
                _lastPosition = MapManager.ObjLink.Position;
            }
            else
            {
                _wasColliding = false;
                _body.VelocityTarget = Vector2.Zero;
            }
            if (!moved)
                _animator.Pause();
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

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // None of these weapons do anything.
            if (hitType == HitType.Bow || hitType == HitType.Bomb || hitType == HitType.MagicPowder || hitType == HitType.MagicRod)
                return Values.HitCollision.Blocking;

            if (!_repelTimer.State)
                return Values.HitCollision.None;
            _repelTimer.Reset();

            // If damage is only 1 then deal no damage.
            damage = damage <= 1 ? 0 : 4;

            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
                _carriableComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
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
