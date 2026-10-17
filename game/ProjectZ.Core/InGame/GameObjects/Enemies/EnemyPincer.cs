using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyPincer : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly Rectangle _tailRectangle = new Rectangle(184, 124, 8, 8);
        private readonly Vector2 _spawnPosition;
        private Vector2 _direction;
        private Vector2 _attackOffset;
        private Vector2 _retractStartPosition;

        private ObjHole _hole;
        private float _attackCounter;
        private float _waitTimer;
        private bool _powderWindow;
        private bool _overHole;
        private int _dirIndex;
        private int _lives = EnemyLives.Pincer;
        private int _dropIndex = 1;

        private int _offsetY = 4;
        private bool _isThrown;

        public EnemyPincer() : base("pincer") { }

        public EnemyPincer(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 8, 0);
            EntitySize = new Rectangle(-32, -32, 64, 64);
            CanReset = true;
            OnReset = Reset;

            _spawnPosition = new Vector2(EntityPosition.X, EntityPosition.Y);

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/pincer");
            _animator.Play("eyes");

            _sprite = new CSprite(EntityPosition) { IsVisible = false };
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -8));

            _body = new BodyComponent(EntityPosition, -6, -6, 12, 12, 8)
            {
                MoveCollision = OnMoveCollision,
                CollisionTypes = Values.CollisionTypes.Field,
                Drag = 0.75f,
                FieldRectangle = map.GetField(posX, posY),
                IgnoreHoles = true,
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f,
            };

            var stateWaiting = new AiState(UpdateWaiting);
            var stateSpawning = new AiState(null);
            stateSpawning.Trigger.Add(new AiTriggerCountdown(1000, null, ToAttack));
            var stateAttacking = new AiState(UpdateAttack);
            var stateAttackWait = new AiState(null);
            stateAttackWait.Trigger.Add(new AiTriggerCountdown(1000, null, ToRetract));
            var stateRetract = new AiState(UpdateRetract);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("spawning", stateSpawning);
            _aiComponent.States.Add("attacking", stateAttacking);
            _aiComponent.States.Add("attackWait", stateAttackWait);
            _aiComponent.States.Add("retract", stateRetract);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, false) { OnBurn = OnBurn, HitMultiplierX = 1.5f, HitMultiplierY = 1.5f };
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, animationComponent, 3300, 900) { OnStun = OnStun, OnStunRelease = OnStunRelease };
            _aiStunnedState.StunKnockbackSpeed = 0;

            _aiComponent.ChangeState("waiting");

            var damageBox   = new CBox(EntityPosition, -3, -3, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -7, 0, 14, 14, 8);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4) { IsActive = false });
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { StunPowder = true, ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(hittableBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        public override void Reset()
        {
            if (_carriableComponent.IsPickedUp)
                return; 

            _aiStunnedState.EndStun();
            _animator.Continue();
            _attackCounter = 0;
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("waiting");
            _aiComponent.ChangeState("waiting");
            _damageState.CurrentLives = EnemyLives.Pincer;
            _overHole = false;
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
            // Exit early if the enemy is stunned.
            if (_aiStunnedState.Active)
                return;

            // If we haven't found a hole yet.
            if (_hole == null)
            {
                // Find the hole that the pincer is nearest to.
                var holes = new List<GameObject>();
                Map.Objects.GetGameObjectsWithTag(holes, Values.GameObjectTag.Hole, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 8, 16, 16);

                if (holes.Count > 0)
                    _hole = (ObjHole)holes[0];
            }
            // If we have the hole, then determine if it's currently over top of it.
            else
            {
                // The hole rectangle is a bit too big, so resize it.
                var holeRect = _hole.CollisionBox.Box.Rectangle();
                var overRect = new Rectangle((int)holeRect.X + 2, (int)holeRect.Y + 2, (int)holeRect.Width - 4, (int)holeRect.Height - 4);
                _overHole = _hole.IsActive && overRect.Contains(EntityPosition.Position);
            }
            // If it's stunned and over the hole, make it fall.
            if (_overHole && _aiStunnedState.IsStunned() && !_carriableComponent.IsPickedUp)
                FakeHoleDeath();
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
            if (type == PushableComponent.PushType.Impact &&
                (_aiComponent.CurrentStateId == "attacking" ||
                 _aiComponent.CurrentStateId == "attackWait" ||
                 _aiComponent.CurrentStateId == "retract" ||
                 _aiStunnedState.IsStunned()))
            {
                if (_aiComponent.CurrentStateId == "attacking")
                    _aiComponent.ChangeState("attackWait");

                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, 0);
                return true;
            }

            return false;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
            _carriableComponent.IsActive = false;
        }

        private void TryReleaseStun()
        {
            if (!_aiStunnedState.Active && _sprite.IsVisible)
            {
                _damageField.IsActive = true;
            }
        }

        private void ToWaiting()
        {
            _aiComponent.ChangeState("waiting");
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
        }

        private void UpdateWaiting()
        {
            if (_waitTimer < 750f)
            {
                _powderWindow = true;
                _waitTimer += Game1.DeltaTime;
                return;
            }
            _direction = MapManager.ObjLink.Position - new Vector2(EntityPosition.Position.X, EntityPosition.Position.Y - 4);
            if (_direction.Length() < 36)
            {
                _aiComponent.ChangeState("spawning");

                EntityPosition.Set(_spawnPosition);

                _powderWindow = false;
                _sprite.IsVisible = true;
                _animator.Play("eyes");
            }
            TryReleaseStun();
        }

        private void ToAttack()
        {
            _waitTimer = 0;
            _damageField.IsActive = true;
            _aiComponent.ChangeState("attacking");
            GetAttackDirection();
            TryReleaseStun();
        }

        private void UpdateAttack()
        {
            _attackCounter += (Game1.TimeMultiplier * 2) / 42.0f;
            if (_attackCounter > 1)
                _attackCounter = 1;

            _attackOffset = _direction * _attackCounter * 42.0f;
            EntityPosition.Set(_spawnPosition + _attackOffset);

            if (_attackCounter >= 1)
                _aiComponent.ChangeState("attackWait");

            TryReleaseStun();
        }

        private void ToRetract()
        {
            _aiComponent.ChangeState("retract");
            _retractStartPosition = EntityPosition.Position - _spawnPosition;
            _attackCounter = 1;
            TryReleaseStun();
        }

        private void UpdateRetract()
        {
            _attackCounter -= (Game1.TimeMultiplier * 1.25f) / 42.0f;
            if (_attackCounter < 0)
                _attackCounter = 0;

            _attackOffset = Vector2.Lerp(_retractStartPosition, Vector2.Zero, 1 - _attackCounter);
            EntityPosition.Set(_spawnPosition + _attackOffset);

            if (_attackCounter <= 0)
                ToWaiting();

            TryReleaseStun();
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // draw the body
            if (_sprite.IsVisible && _aiComponent.CurrentStateId != "spawning")
            {
                for (var i = 0; i < 3; i++)
                {
                    // How far along the head->hole line this segment sits.
                    var t = 0.2f + (i / 2f) * 0.5f;

                    var rawPosition = _spawnPosition + (EntityPosition.Position - _spawnPosition) * t - new Vector2(4, 4);
                    rawPosition.Y -= EntityPosition.Z * t;

                    var position = GameSettings.PixelSnapping
                        ? new Vector2(MathF.Round(rawPosition.X), MathF.Round(rawPosition.Y))
                        : rawPosition;

                    spriteBatch.Draw(Resources.SprEnemies, position, _tailRectangle, Color.White);
                }
            }
            // draw the head
            _sprite.Draw(spriteBatch);
        }

        private void GetAttackDirection()
        {
            _direction = MapManager.ObjLink.Position - EntityPosition.Position;

            if (_direction != Vector2.Zero)
                _direction.Normalize();

            var degree = MathHelper.ToDegrees((float)Math.Atan2(-_direction.Y, -_direction.X)) + 360;

            _dirIndex = (int)((degree + 22.5f) / 45) % 8;

            _animator.Play(_dirIndex.ToString());
        }

        private void FakeHoleDeath()
        {
            // Play the "falling down hole" sound effect.
            Game1.AudioManager.PlaySoundEffect("D360-03-03");
            Game1.AudioManager.PlaySoundEffect("D360-24-18");

            // Spawn the graphics for it.
            var fallAnimation = new ObjAnimator(_aiComponent.Owner.Map, 0, 0, Values.LayerBottom, "Particles/fall", "idle", true);
            fallAnimation.EntityPosition.Set(new Vector2(
                _body.Position.X + _body.OffsetX + _body.Width / 2.0f - 5,
                _body.Position.Y + _body.OffsetY + _body.Height / 2.0f - 5));
            _aiComponent.Owner.Map.Objects.SpawnObject(fallAnimation);

            // Remove the object from the map.
            Map.Objects.DeleteObjects.Add(this);
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Simulate the enemy falling down the hole since it can't actually be absorbed by holes.
            if (hitType == HitType.MagicPowder && _powderWindow || _overHole && _damageState.CurrentLives <= 0)
                FakeHoleDeath();

            // can only attack while the enemy is attacking
            if (_aiComponent.CurrentStateId != "attacking" &&
                _aiComponent.CurrentStateId != "attackWait" &&
                _aiComponent.CurrentStateId != "retract" &&
                !_aiStunnedState.IsStunned())
                return Values.HitCollision.None;

            if (_aiComponent.CurrentStateId == "attacking")
                _aiComponent.ChangeState("attackWait");

            _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // make sure to not fly away like for other enemies
            if (pieceOfPower)
                _body.Drag = 0.75f;

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
