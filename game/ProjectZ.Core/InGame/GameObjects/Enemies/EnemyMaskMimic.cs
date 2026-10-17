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
    internal class EnemyMaskMimic : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly Animator _animator;
        private readonly AnimationComponent _animatorComponent;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private Vector2 _lastPosition;
        private int _direction;
        private bool _wasColliding;
        private int _lives = EnemyLives.MaskMimic;
        private int _dropIndex = 2;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyMaskMimic() : base("mask mimic") { }

        public EnemyMaskMimic(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/mask mimic");
            _animator.Play("walk");

            var sprite = new CSprite(EntityPosition);
            _animatorComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8)
            {
                MoveCollision = OnMoveCollision,
                Gravity = -0.075f,
                DragAir = 1.0f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                IsSlider = true,
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f,
                MaxSlideDistance = 4.0f
            };

            _aiComponent = new AiComponent();

            var stateUpdate = new AiState();

            _aiComponent.States.Add("idle", stateUpdate);
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, _animatorComponent, 3300, 900) { OnStun = OnStun, OnStunRelease = OnStunRelease };
            _aiDamageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            new AiFallState(_aiComponent, _body, null, null, 300);
            new AiDeepWaterState(_body);

            _aiComponent.ChangeState("idle");

            var damageBox   = new CBox(EntityPosition, -4, -10, 2,  8,  8, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 2, 14, 15, 8);
            var pushableBox = new CBox(EntityPosition, -7, -14, 2, 14, 14, 8);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, _animatorComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { StunHookshot = true, StunBoomerang = true, BombMultiplier = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
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
            _aiDamageState.CurrentLives = EnemyLives.MaskMimic;
            _body.VelocityTarget = Vector2.Zero;
            _body.Gravity = -0.075f;
            _body.DragAir = 1.0f;
            _body.IsSlider = true;
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
        }

        private void OnStun()
        {
            _carriableComponent.IsActive = true;
            _damageField.IsActive = false;
            _body.Gravity = -0.25f;
            _body.DragAir = 0.9f;
            _body.IsSlider = false;
        }

        private void OnStunRelease()
        {
            _carriableComponent.IsActive = false;
            _damageField.IsActive = true;
            _body.Gravity = -0.075f;
            _body.DragAir = 1.0f;
            _body.IsSlider = true;
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

            // Stunning can disable damage field so reactivate it.
            if (!_aiStunnedState.Active)
                _damageField.IsActive = true;

            // Move when Link is in the same field as the Mask Mimic.
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

                    _body.VelocityTarget = moveVelocity * 0.75f;

                    if (moveVelocity.Length() > 0.01f)
                    {
                        moved = true;

                        if (!MapManager.ObjLink.IsChargingState())
                        {
                            // deadzone to not have a fixed point where the direction gets changed
                            if (Math.Abs(moveVelocity.X) * ((_direction % 2 == 0) ? 1.1f : 1f) >
                                Math.Abs(moveVelocity.Y) * ((_direction % 2 != 0) ? 1.1f : 1f))
                                _direction = moveVelocity.X < 0 ? 0 : 2;
                            else
                                _direction = moveVelocity.Y < 0 ? 1 : 3;
                        }
                        var playAnimation = "walk_" + _direction;

                        if (_animator.CurrentAnimation.Id != playAnimation)
                            _animator.Play(playAnimation);
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
            if (hitType == HitType.MagicPowder)
                return Values.HitCollision.None;

            // Can only be hit if the damage source is coming from the back.
            var dir = AnimationHelper.GetDirection(direction);
            if (dir == _direction ||
                hitType == HitType.Bomb ||
                hitType == HitType.Bow ||
                hitType == HitType.SwordShot ||
                hitType == HitType.MagicRod)
            {
                return _aiDamageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);
            }
            // Hookshot and Boomerang stun the enemy.
            if (hitType == HitType.Hookshot || hitType == HitType.Boomerang)
                return _aiDamageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);
            
            return Values.HitCollision.RepellingParticle | Values.HitCollision.Repelling1;
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
