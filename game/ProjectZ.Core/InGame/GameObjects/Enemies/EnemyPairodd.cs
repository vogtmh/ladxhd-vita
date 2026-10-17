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
    internal class EnemyPairodd : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly AiTriggerTimer _teleportCooldown;
        private readonly AiTriggerCountdown _shootCountdown;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly DrawShadowCSpriteComponent _shadowComponent;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly Rectangle _fieldRectangle;
        private readonly Vector2 _centerPosition;

        private int _lives = EnemyLives.Pairodd;
        private int _dropIndex = 4;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyPairodd() : base("pairodd") { }

        public EnemyPairodd(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            Tags = Values.GameObjectTag.Enemy;

            _fieldRectangle = map.GetField(posX, posY);
            _centerPosition = new Vector2(_fieldRectangle.Center.X, _fieldRectangle.Center.Y + 8);

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/pairodd");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8)
            {
                MoveCollision = OnMoveCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                FieldRectangle = map.GetField(posX, posY),
                AbsorbPercentage = 0.75f,
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f
            };
            var stateIdle = new AiState(UpdateIdle);
            stateIdle.Trigger.Add(_teleportCooldown = new AiTriggerTimer(300));
            var stateSpawn = new AiState(UpdateSpawn);
            var statePreDespawn = new AiState();
            statePreDespawn.Trigger.Add(new AiTriggerCountdown(200, null, ToDespawn));
            var stateDespawn = new AiState(UpdateDespawn);
            var stateHidden = new AiState();
            stateHidden.Trigger.Add(new AiTriggerCountdown(600, null, ToSpawning));

            _aiComponent = new AiComponent();
            _aiComponent.Trigger.Add(_shootCountdown = new AiTriggerCountdown(400, null, Shoot));
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("spawn", stateSpawn);
            _aiComponent.States.Add("preDespawn", statePreDespawn);
            _aiComponent.States.Add("despawn", stateDespawn);
            _aiComponent.States.Add("hidden", stateHidden);

            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, true, false) { OnBurn = OnBurn };
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, animationComponent, 3300, 900) { ShakeOffset = 1, SilentStateChange = false, ReturnState = "idle", OnStun = OnStun, OnStunRelease = OnStunRelease };
            
            new AiFallState(_aiComponent, _body, OnHoleAbsorb);
            new AiDeepWaterState(_body);

            var damageBox   = new CBox(EntityPosition, -3, -10, 0,  6, 6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -14, 0, 14, 14, 8);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, _shadowComponent = new DrawShadowCSpriteComponent(_sprite));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, StunBoomerang = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            ToIdle();
            // do not shoot directly after spawning
            _shootCountdown.Stop();
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
            _shootCountdown.Stop();
            _sprite.IsVisible = true;
            _damageState.CurrentLives = EnemyLives.Pairodd;
            _animator.SpeedMultiplier = 1f;
            _isThrown = false;
            _aiStunnedState.EndStun();
            ToIdle();
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

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void ToSpawning()
        {
            _aiComponent.ChangeState("spawn");
            _animator.Play("spawn");
            _sprite.IsVisible = true;
            _body.Velocity = Vector3.Zero;

            // set the new position to be at the opposite side of the room
            var directionToCenter = _centerPosition - EntityPosition.Position;

            // clamp the offset to not move too fare from the center
            if (directionToCenter.Length() > 48)
            {
                directionToCenter.Normalize();
                directionToCenter *= 56;
            }

            var newPosition = _centerPosition + directionToCenter;
            EntityPosition.Set(newPosition);
        }

        private void UpdateSpawn()
        {
            // finished spawn animation?
            if (!_animator.IsPlaying)
                ToIdle();
        }

        private void ToIdle()
        {
            _aiComponent.ChangeState("idle");
            _animator.Play("idle");
            _damageState.IsActive = true;
            _shadowComponent.IsActive = true;
            _body.IsActive = true;
            _damageState.IsActive = true;
            _damageField.IsActive = true;
            _shootCountdown.OnInit();
        }

        private void UpdateIdle()
        {
            if (!_teleportCooldown.State)
                return;

            var playerDistance = _body.BodyBox.Box.Center - MapManager.ObjLink.CenterPosition;

            if (playerDistance.Length() < 36)
                _aiComponent.ChangeState("preDespawn");
        }

        private void Shoot()
        {
            if (!_fieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
                return;

            var projectile = new EnemyPairoddProjectile(Map, new Vector2(EntityPosition.X, EntityPosition.Y - 8), 1.5f);
            Map.Objects.SpawnObject(projectile);
        }

        private void ToDespawn()
        {
            // do not despawn if the enemy is dead
            if (_damageState.CurrentLives <= 0 && _damageState.DamageTrigger.CurrentTime > 0)
                return;

            Game1.AudioManager.PlaySoundEffect("D360-60-3C");

            _aiComponent.ChangeState("despawn");
            _animator.Play("despawn");
            _shadowComponent.IsActive = false;
            _body.IsActive = false;
            _damageState.IsActive = false;
            _damageField.IsActive = false;
        }

        private void UpdateDespawn()
        {
            // finished spawn animation?
            if (!_animator.IsPlaying)
                ToHidden();
        }

        private void ToHidden()
        {
            _aiComponent.ChangeState("hidden");
            _sprite.IsVisible = false;
        }

        private void OnHoleAbsorb()
        {
            _animator.Play("idle");
            _animator.SpeedMultiplier = 4f;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (!_damageState.IsActive)
                return false;

            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Don't deal damage while in damage state.
            if (!_damageState.IsActive || _damageState.IsInDamageState())
                return Values.HitCollision.None;

            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // Remove components when killed.
            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
                _carriableComponent.IsActive = false;
            }
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
