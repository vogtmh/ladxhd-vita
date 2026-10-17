using System;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyHardhatBeetle : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly DamageFieldComponent _damageField;

        private Vector2 _vecDirection;

        private float _maxSpeed;
        private float speedChange;
        private bool _isFollowing;
        private bool _wasFollowing;
        private int _lives = EnemyLives.HardhatBeetle;
        private int _dropIndex = 10;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyHardhatBeetle() : base("hardHatBeetle") { }

        public EnemyHardhatBeetle(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/hardhat beetle");
            _animator.Play("walk");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            var fieldRectangle = map.GetField(posX, posY);

            _body = new BodyComponent(EntityPosition, -6, -10, 12, 9, 8)
            {
                MoveCollision = OnMoveCollision,
                Drag = 0.875f,
                CollisionTypes = Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.ThrowWeaponIgnore |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.DeepWater,
                FieldRectangle = fieldRectangle,
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f
            };

            _aiComponent = new AiComponent();

            var stateWaiting = new AiState { Init = InitWaiting };
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(UpdateWaiting, 75, 100));
            var stateMoving = new AiState(UpdateMoving);

            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("moving", stateMoving);
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, animationComponent, 3300, 900) { SilentStateChange = false, ReturnState = "waiting", OnStun = OnStun, OnStunRelease = OnStunRelease };
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { SpawnPowerups = false };

            new AiFallState(_aiComponent, _body, OnHoleAbsorb, OnHoleDeath);
            new AiDeepWaterState(_body);

            _aiComponent.ChangeState("waiting");
            _maxSpeed = GameMath.GetRandomFloat(0.25f, 0.55f);

            var damageBox   = new CBox(EntityPosition, -4, -8, 0, 8, 6, 4);
            var hittableBox = new CBox(EntityPosition, -8, -14, 16, 14, 8);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4) { PushMultiplier = 2.00f });
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit) { StunHookshot = true, StunBoomerang = true, BombMultiplier = true });
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush) { RepelMultiplier = 2.05f });
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        public override void Reset()
        {
            if (_carriableComponent.IsPickedUp)
                return;

            _isFollowing = false;
            _wasFollowing = false;
            _aiComponent.ChangeState("waiting");
            _aiComponent.ChangeState("waiting");
            _animator.SpeedMultiplier = 1f;
            _carriableComponent.IsActive = false;
            _damageField.IsActive = true;
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

        private void InitWaiting()
        {
            _animator.Play("walk");
            _animator.SpeedMultiplier = 1.0f;
        }

        private void UpdateWaiting()
        {
            if (_body.FieldRectangle.Intersects(MapManager.ObjLink.BodyRectangle))
                _aiComponent.ChangeState("moving");
        }

        private void UpdateMoving()
        {
            // Give them a random speed that fluctuates every 3/4 second to prevent them from stacking on
            // top of each other. This also more closely matches their behavior from the original games.
            if ((speedChange += Game1.DeltaTime) > 750)
            {
                _maxSpeed = GameMath.GetRandomFloat(0.25f, 0.55f);
                speedChange = 0;
            }
            if (_vecDirection != Vector2.Zero)
            {
                var oldPercentage = (float)Math.Pow(0.9f, Game1.TimeMultiplier);
                var newDirection = _body.VelocityTarget * oldPercentage +
                                   _vecDirection * (1 - oldPercentage);
                newDirection.Normalize();

                _body.VelocityTarget = newDirection * _maxSpeed;
            }
            else
                _body.VelocityTarget = Vector2.Zero;

            _isFollowing = MapManager.ObjLink.BodyRectangle.Intersects(_body.FieldRectangle);

            if (_isFollowing)
                _vecDirection = new Vector2(MapManager.ObjLink.EntityPosition.X - EntityPosition.X, MapManager.ObjLink.EntityPosition.Y - EntityPosition.Y);
            else
                _vecDirection = new Vector2(ResetPosition.X - EntityPosition.X, ResetPosition.Y - EntityPosition.Y);

            if (!_isFollowing && (int)EntityPosition.X == (int)ResetPosition.X && (int)EntityPosition.Y == (int)ResetPosition.Y)
            {
                _body.VelocityTarget = Vector2.Zero;
                _aiComponent.ChangeState("waiting");
            }

            if (_vecDirection != Vector2.Zero)
                _vecDirection.Normalize();

            _damageField.IsActive = true;
            _wasFollowing = _isFollowing | !Camera.ClassicMode;
            _isFollowing = false;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void OnHoleAbsorb()
        {
            _animator.SpeedMultiplier = 2.0f;
            _animator.Play("walk");
        }

        private void OnHoleDeath()
        {
            Map.Objects.SpawnObject(new EnemyHardhatBeetleRespawner(Map, (int)ResetPosition.X - 8, (int)ResetPosition.Y - 16, _body.FieldRectangle));
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_damageState.IsInDamageState())
                return Values.HitCollision.None;

            // Thrown objects do nothing to this enemy.
            if (hitType == HitType.ThrownObject)
                return Values.HitCollision.Blocking;

            // Damage types that have no effect.
            if (hitType == HitType.SwordShot || hitType == HitType.Bow || hitType == HitType.MagicRod || hitType == HitType.MagicPowder)
            {
                _damageState.PlayHitSound = false;
                _damageState.HitMultiplierX = 0;
                _damageState.HitMultiplierY = 0;
                damage = 0;
                return Values.HitCollision.Blocking;
            }
            // Restore normal values for other damage types.
            else
            {
                _damageState.PlayHitSound = true;
                _damageState.HitMultiplierX = 5;
                _damageState.HitMultiplierY = 5;
            }
            // Bombs and BowWow cause it to drop a bomb.
            if ((hitType & HitType.Bomb) != 0 || hitType == HitType.BowWow)
            {
                return _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);
            }
            // Boomerang and Hookshot stun it.
            if (hitType == HitType.Boomerang || hitType == HitType.Hookshot)
            {
                _body.VelocityTarget = Vector2.Zero;
                _animator.Play("stunned");
                _aiStunnedState.StartStun();
                _damageField.IsActive = false;
            }
            // Allows knockback effect from piece of power or red tunic.
            if (pieceOfPower)
                return _damageState.OnHit(gameObject, direction, hitType, 0, pieceOfPower);

            _damageState.SetDamageState(false);
            _body.Velocity.X = direction.X * 3.65f;
            _body.Velocity.Y = direction.Y * 3.65f;
            Game1.AudioManager.PlaySoundEffect("D360-09-09");
            return Values.HitCollision.Enemy;
        }

        private void OnMoveCollision(Values.BodyCollision direction)
        {
            // When thrown and it hits the floor.
            if (_isThrown && (direction & Values.BodyCollision.Floor) != 0)
            {
                _isThrown = false;
                _carriableComponent.Thrown = false;
                _aiStunnedState.ResetBodyState();
            }
            // Preserves the speed when following alongside a wall.
            if (_wasFollowing)
            {
                if ((direction & Values.BodyCollision.Horizontal) != 0)
                {
                    var ratio = Math.Abs(_vecDirection.X) / Math.Abs(_vecDirection.Y);
                    if (1 < ratio && ratio < 25)
                    {
                        _vecDirection.X = 0;
                        _vecDirection.Y *= ratio;
                    }
                }
                else if ((direction & Values.BodyCollision.Vertical) != 0)
                {
                    var ratio = Math.Abs(_vecDirection.Y) / Math.Abs(_vecDirection.X);
                    if (1 < ratio && ratio < 25)
                    {
                        _vecDirection.X *= ratio;
                        _vecDirection.Y = 0;
                    }
                }
                return;
            }
            _body.VelocityTarget = Vector2.Zero;

            // Collide with a wall.
            if ((direction & Values.BodyCollision.Horizontal) != 0)
                _vecDirection.X = -_vecDirection.X;
            else if ((direction & Values.BodyCollision.Vertical) != 0)
                _vecDirection.Y = -_vecDirection.Y;
        }
    }
}
