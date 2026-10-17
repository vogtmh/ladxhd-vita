using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    internal class MBossSmasher : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly CubicBezier _pickupCurveX;
        private readonly CubicBezier _pickupCurveY;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly string _saveKey;

        private MBossSmasherBall _ball;
        private Rectangle _fieldRectangle;
        private Vector2 _moveDirection;
        private Vector2 _jumpDirection;
        private Vector2 _pickupStart;

        private const int PickupTime = 500;
        private const float PickupReachX = 12f;
        private const float PickupReachY = 12f;

        private const float CarrySpeed = 0.25f;
        private const float FetchSpeed = 1.00f;
        private const float RetreatSpeed = 0.50f;
        private const float WanderSpeedX = 0.75f;
        private const float WanderSpeedY = 0.25f;

        private float _resetTimer;
        private bool _initialized;
        private bool _isDying;
        private int _direction;
        private int _jumpCount;
        private int _lives = EnemyLives.Smasher;

        // Detects when Smasher is outside of the normal boundaries. Used in dungeon 6 where a bombable wall exists as the entrance.
        private bool _inAlcove => EntityPosition.Y > _fieldRectangle.Bottom - 16 || EntityPosition.Y < _fieldRectangle.Top + 16 ||
                                  EntityPosition.X < _fieldRectangle.Left + 16   || EntityPosition.X > _fieldRectangle.Right - 16;

        // Detects when the player is in the current field.
        private bool _playerInField => _fieldRectangle.Contains(MapManager.ObjLink.CenterPosition);

        public bool IsDying => _isDying;

        private ObjLink _objLink => MapManager.ObjLink;

        public MBossSmasher(Map.Map map, int posX, int posY, string saveKey) : base(map, "smasher")
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-7, -12, 14, 12);
            CanReset = false;

            _saveKey = saveKey;

            if (!string.IsNullOrEmpty(_saveKey) &&
                Game1.GameManager.SaveManager.GetString(_saveKey) == "1")
            {
                IsDead = true;
                return;
            }
            // Get the field the object is in.
            if (map != null)
                _fieldRectangle = map.GetField(posX, posY);

            EntityPosition.AddPositionListener(typeof(MBossSmasher), OnPositionChange);

            _pickupCurveX = new CubicBezier(100, new Vector2(0.6f, 0.8f), new Vector2(0.7f, 1));
            _pickupCurveY = new CubicBezier(100, new Vector2(0.15f, 0.55f), new Vector2(0.15f, 1));

            _animator = AnimatorSaveLoad.LoadAnimator("MidBoss/smasher");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(0, 0));

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8)
            {
                MoveCollision = OnCollision,
                Drag = 0.65f,
                DragAir = 0.75f,
                Gravity = -0.125f,
                FieldRectangle = map.GetField(posX, posY)
            };
            var stateWaiting = new AiState() { Init = InitWaiting };
            var stateWalk = new AiState(UpdateWalk) { Init = InitWalk };
            var statePickup = new AiState { Init = InitPickup };
            statePickup.Trigger.Add(new AiTriggerCountdown(PickupTime, TickPickup, PickupEnd));
            var stateCarry = new AiState(UpdateCarry) { Init = InitCarrying };
            var statePostThrow = new AiState();
            statePostThrow.Trigger.Add(new AiTriggerCountdown(550, null, () => _aiComponent.ChangeState("walk")));

            _aiComponent = new AiComponent();
            _aiComponent.Trigger.Add(new AiTriggerUpdate(Update));

            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("walk", stateWalk);
            _aiComponent.States.Add("pickup", statePickup);
            _aiComponent.States.Add("carry", stateCarry);
            _aiComponent.States.Add("postThrow", statePostThrow);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, 0) { BossHitSound = true, ExplosionOffsetY = 6, OnLiveZeroed = OnLiveZeroed };
            _damageState.AddBossDamageState(RemoveObject);

            _aiComponent.ChangeState("waiting");

            var damageCollider = new CBox(EntityPosition, -7, -12, 0, 14, 12, 14, true);
            var hittableBox = new CBox(EntityPosition, -7, -12, 0, 14, 12, 14, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 4));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, MagicRodMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite) { ShadowWidth = 18, ShadowHeight = 6 });
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            _moveDirection = new Vector2(-1.2f, 0);
            _animator.Play("idle_0");

            _ball = new MBossSmasherBall(map, new Vector2(EntityPosition.X + 56, EntityPosition.Y + 16), this);
            map.Objects.SpawnObject(_ball);

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowl");
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // Stop updating if the boss is currently dying.
            if (_isDying)
                return;

            // Start music when player enters room. Room boolean is used to not reset aiComponent state every loop iteration.
            if (!_initialized && _playerInField)
            {
                if (Game1.AudioManager.GetCurrentMusic() != 79)
                    Game1.AudioManager.SetMusicFadeTransition(79, 2, 350);

                _aiComponent.ChangeState("walk");
                _hitComponent.IsActive = true;
                _initialized = true;
            }
            // Stop the music when the player leaves the room.
            else if (_initialized && !_playerInField)
            {
                Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);

                _animator.Play("idle_0");
                _animator.SpeedMultiplier = 1.0f;
                _aiComponent.ChangeState("waiting");
                _damageState.CurrentLives = _lives;
                _body.VelocityTarget = Vector2.Zero;
                _hitComponent.IsActive = false;
                _initialized = false;

                // Create an effect at the monster's position if Modern Camera.
                if (!Camera.ClassicMode)
                {
                    var anim = new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y - 8, Values.LayerTop, "Particles/pieceOfPowerExplosion", "run", true);
                    Map.Objects.SpawnObject(anim);
                    anim.Animator.SpeedMultiplier = 1.75f;
                    Game1.AudioManager.PlaySoundEffect("D360-47-2F");
                    _resetTimer = 200;
                    _sprite.IsVisible = false;
                }
                // Classic Camera just reset its position.
                else
                    EntityPosition.Set(ResetPosition);
            }
            // If the player leaves the field, reset the monster's position.
            if (_resetTimer > 0)
            {
                _resetTimer -= Game1.DeltaTime;
                if (_resetTimer <= 0) 
                {
                    _resetTimer = 0;
                    _sprite.IsVisible = true;
                    EntityPosition.Set(ResetPosition);
                }
            }
        }

        private void InitWaiting()
        {
            _body.VelocityTarget = Vector2.Zero;
        }

        private void InitPickup()
        {
            if (!_ball.InitPickup())
            {
                _aiComponent.ChangeState("walk");
                return;
            }
            Game1.AudioManager.PlaySoundEffect("D370-28-1C");

            _pickupStart = new Vector2(_ball.EntityPosition.Position.X, EntityPosition.Y - _ball.EntityPosition.Position.Y);
            _direction = _ball.EntityPosition.Position.X < EntityPosition.X ? 0 : 1;
            _animator.Play("up_" + _direction);
        }

        private void TickPickup(double countdownState)
        {
            // The ball's position above the Smasher's head.
            var ballTargetPosition = new Vector2(EntityPosition.X, 15);

            // Pick up the ball and apply a curve to the trajectory.
            var percentage = (float)((PickupTime - countdownState) / PickupTime);
            var percentageX = _pickupCurveX.EvaluateX(percentage);
            var percentageY = _pickupCurveY.EvaluateX(percentage);
            var newBallPosition = new Vector2(
                MathHelper.Lerp(_pickupStart.X, ballTargetPosition.X, percentageX),
                MathHelper.Lerp(_pickupStart.Y, ballTargetPosition.Y, percentageY));

            // Continue lifting the ball to above the Smasher's head.
            _ball.EntityPosition.Set(new Vector3(newBallPosition.X, EntityPosition.Y + 1, newBallPosition.Y));

            // Get Link's current state and potential carried object.
            var _linkState = _objLink.CurrentState;
            var _carriedObject = _objLink.CarriedObject;

            // If Link is in the process of picking up the ball or carrying something.
            if (_linkState == ObjLink.State.Pulling || _linkState == ObjLink.State.PreCarrying || _linkState == ObjLink.State.Carrying)
            {
                // If the carried object is the Smasher's ball then return.
                if (_carriedObject == null || _carriedObject.GetType() != typeof(MBossSmasherBall))
                    return;

                // Link picking up the ball may have interrupted the Smasher pickup.
                _ball.DisableDamageField();
                _aiComponent.ChangeState("walk");
                _ball.EndPickup();
            }
        }

        private void PickupEnd()
        {
            _ball.EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y + 1, 15));
            _aiComponent.ChangeState("carry");
        }

        private void InitWalk()
        {
            _jumpCount = 0;
        }

        private void UpdateWalk()
        {
            if (_body.Velocity.Z < 0)
                _animator.Play("idle_" + _direction);

            if (_body.IsGrounded)
            {
                // Jump towards the ball if the player is not already carrying it.
                if (_ball.IsAvailable())
                    JumpTowardsBall();
                else
                    JumpRandom();
            }
        }

        private void JumpRandom()
        {
            if (_saveKey == "d6_smasher")
                _body.CollisionTypes = Values.CollisionTypes.Normal;

            // Get a random direction to jump towards.
            if (_jumpCount <= 0)
            {
                _jumpCount = Game1.RandomNumber.Next(2, 5);
                var dirX = Game1.RandomNumber.Next(0, 2) * 2 - 1;
                var dirY = Game1.RandomNumber.Next(0, 2) * 2 - 1;
                _jumpDirection = new Vector2(dirX * WanderSpeedX, dirY * WanderSpeedY);
            }
            // Jump playing the directional animation.
            Jump(_jumpDirection, "up_");
            _jumpCount--;
        }

        private void JumpTowardsBall()
        {
            if (_saveKey == "d6_smasher")
                _body.CollisionTypes = Values.CollisionTypes.NPCWall;

            // Jump towards the ball or pick it up if we are close enough.
            var targetPosition = new Vector2(_ball.EntityPosition.X, _ball.EntityPosition.Y - 2);
            var ballDirection = targetPosition - EntityPosition.Position;

            // The ROM checks each axis against a box rather than using a radius. So instead
            // use a box so Smasher will not overshoot the ball and jump between two points.
            if (Math.Abs(ballDirection.X) < PickupReachX &&
                Math.Abs(ballDirection.Y) < PickupReachY)
            {
                _aiComponent.ChangeState("pickup");
                _body.VelocityTarget = Vector2.Zero;
                return;
            }
            ballDirection.Normalize();
            Jump(ballDirection * FetchSpeed, "idle_");
        }

        private void Jump(Vector2 velocity, string animationName)
        {
            _direction = velocity.X < 0 ? 0 : 1;
            _animator.Play(animationName + _direction);

            _body.VelocityTarget = velocity;
            _body.Velocity = new Vector3(0, 0, 0.8f);
        }

        private void OnPositionChange(CPosition newPosition)
        {
            if (_aiComponent.CurrentStateId != "carry")
                return;

            // Set the position of the ball if it is carried.
            _ball.EntityPosition.Set(new Vector3(newPosition.X, newPosition.Y, newPosition.Z + 15));
        }

        private void InitCarrying()
        {
            _jumpCount = 0;
        }

        private Vector2 AlcoveExitDirection()
        {
            var exit = Vector2.Zero;

            if (EntityPosition.Y > _fieldRectangle.Bottom - 16) exit.Y = -1;
            else if (EntityPosition.Y < _fieldRectangle.Top + 16) exit.Y = 1;

            if (EntityPosition.X < _fieldRectangle.Left + 16) exit.X = 1;
            else if (EntityPosition.X > _fieldRectangle.Right - 16) exit.X = -1;

            return exit;
        }

        private void UpdateCarry()
        {
            // Start throwing, but not while tucked into the hole in the wall.
            if (_jumpCount > 2 && _body.Velocity.Z < 0 && !_inAlcove)
            {
                ThrowBall();
                return;
            }

            if (!_body.IsGrounded)
                return;

            _jumpCount++;

            // Clearing the alcove comes before chasing.
            if (_inAlcove)
            {
                var exitDirection = AlcoveExitDirection();
                if (exitDirection != Vector2.Zero)
                {
                    exitDirection.Normalize();

                    // Keep facing the player to keep the illusion of chasing.
                    _direction = _objLink.Position.X < EntityPosition.X ? 0 : 1;
                    _animator.Play("up_" + _direction);

                    _body.VelocityTarget = exitDirection * RetreatSpeed;
                    _body.Velocity = new Vector3(0, 0, 0.8f);
                    return;
                }
            }

            // Jump towards the player.
            var ballDirection = _objLink.Position - EntityPosition.Position;

            if (ballDirection.Length() > 5)
            {
                ballDirection.Normalize();
                _direction = ballDirection.X < 0 ? 0 : 1;
                _animator.Play("up_" + _direction);

                _body.VelocityTarget = ballDirection * CarrySpeed;
                _body.Velocity = new Vector3(0, 0, 0.8f);
            }
        }

        private void ThrowBall()
        {
            _aiComponent.ChangeState("postThrow");

            Game1.AudioManager.PlaySoundEffect("D360-08-08");

            _animator.Play("idle_" + _direction);
            _body.Velocity = new Vector3(0, 0, 1.75f);

            float throwZVelocity = 1.5f;
            float gravity = 0.125f;

            // Ball launches from carry height (bossZ + 15). Time for it to fall
            var launchHeight = _ball.EntityPosition.Z;
            var airtime = (throwZVelocity + MathF.Sqrt(throwZVelocity * throwZVelocity + 2f * gravity * launchHeight)) / gravity;

            // Horizontal speed that covers the distance to the player in exactly that time.
            var playerDirection = new Vector2(_objLink.Position.X, _objLink.Position.Y - 2) - EntityPosition.Position;
            var playerDistance = playerDirection.Length();
            if (playerDistance > 0)
                playerDirection.Normalize();

            var horizontalSpeed = airtime > 0 ? playerDistance / airtime : 0;
            playerDirection *= Math.Clamp(horizontalSpeed, 0, 4f);

            var throwDirection = new Vector3(playerDirection, throwZVelocity);
            _ball.Throw(throwDirection);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X, direction.Y, _body.Velocity.Z);

            return true;
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (hitType == HitType.Boomerang)
                return Values.HitCollision.Blocking;

            if (_damageState.IsInDamageState())
                return Values.HitCollision.None;

            // The boss was hit with the ball.
            if ((hitType & HitType.ThrownObject) != 0)
            {
                _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);
                _body.VelocityTarget = Vector2.Zero;
            }
            // Remove damage box on death.
            if (_damageState.CurrentLives <= 0)
            {
                _isDying = true;
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            return Values.HitCollision.RepellingParticle | Values.HitCollision.SpawnFire;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            if (_aiComponent.CurrentStateId == "jumping" && (direction & Values.BodyCollision.Horizontal) != 0 &&
                Math.Sign(_body.Velocity.X) == Math.Sign(_moveDirection.X))
            {
                _aiComponent.ChangeState("pushing");
                _moveDirection.X = -_moveDirection.X;
                _body.Velocity.X = -_body.Velocity.X * 0.125f;
                _animator.Play("idle_" + (_moveDirection.X < 0 ? 0 : 1));
            }

            // Landed after a jump?
            if ((direction & Values.BodyCollision.Floor) != 0)
            {
                if (_aiComponent.CurrentStateId == "jumping")
                    _aiComponent.ChangeState("idle");
            }
        }

        private void OnLiveZeroed()
        {
            // Destroy the ball and spawn a fairy.
            if (_ball != null)
            {
                _ball.Destroy();
                _ball = null;
            }
        }

        private void RemoveObject()
        {
            if (!string.IsNullOrEmpty(_saveKey))
                Game1.GameManager.SaveManager.SetString(_saveKey, "1");

            // Stop the boss music.
            Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);

            // Spawn another fairy from the boss.
            Game1.AudioManager.PlaySoundEffect("D360-27-1B");
            Map.Objects.SpawnObject(new ObjDungeonFairy(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 8));

            Map.Objects.DeleteObjects.Add(this);
        }
    }
}
