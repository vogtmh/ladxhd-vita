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
    internal class EnemyWizzrobe : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly AiTriggerTimer _hiddenTimer;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly Rectangle _fieldRectangle;

        private const int BlinkTime = 600;
        private int _direction;
        private int _lives = EnemyLives.Wizzrobe;
        private int _dropIndex = 0;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyWizzrobe() : base("wizzrobe") { }

        public EnemyWizzrobe(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            Tags = Values.GameObjectTag.Enemy;

            _fieldRectangle = map.GetField(posX, posY);

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/wizzrobe");
            _animator.Play("head");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, 0));

            _body = new BodyComponent(EntityPosition, -6, -12, 12, 12, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                MoveCollision = OnMoveCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.Field,
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f
            };
            var stateHidden = new AiState(UpdateHidden) { Init = InitHidden };
            // will be hidden for at lease x time
            stateHidden.Trigger.Add(_hiddenTimer = new AiTriggerTimer(1000));
            var stateSpawn = new AiState { Init = InitSpawn };
            stateSpawn.Trigger.Add(new AiTriggerCountdown(BlinkTime, BlinkTick, () => _aiComponent.ChangeState("head")));
            var stateHead = new AiState { Init = InitHead };
            stateHead.Trigger.Add(new AiTriggerCountdown(400, null, () => _aiComponent.ChangeState("stand")));
            var stateStand = new AiState { Init = InitStand };
            stateStand.Trigger.Add(new AiTriggerCountdown(300, null, Shoot));
            stateStand.Trigger.Add(new AiTriggerCountdown(1000, null, () => _aiComponent.ChangeState("despawnHead")));
            var stateDespawnHead = new AiState { Init = InitHead };
            stateDespawnHead.Trigger.Add(new AiTriggerCountdown(400, null, () => _aiComponent.ChangeState("despawn")));
            var stateDespawn = new AiState();
            stateDespawn.Trigger.Add(new AiTriggerCountdown(BlinkTime, BlinkTick, () => _aiComponent.ChangeState("hidden")));

            _aiComponent = new AiComponent();

            _aiComponent.States.Add("hidden", stateHidden);
            _aiComponent.States.Add("spawn", stateSpawn);
            _aiComponent.States.Add("head", stateHead);
            _aiComponent.States.Add("stand", stateStand);
            _aiComponent.States.Add("despawn", stateDespawn);
            _aiComponent.States.Add("despawnHead", stateDespawnHead);
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, animationComponent, 3300, 900) { OnStun = OnStun, OnStunRelease = OnStunRelease };
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, false, false);

            new AiFallState(_aiComponent, _body, null, null, 100);
            new AiDeepWaterState(_body);

            _aiComponent.ChangeState("hidden");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 0, 14, 15, 8);
            var pushableBox = new CBox(EntityPosition, -6, -14, 0, 12, 14, 8);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4) { IsActive = false });
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { StunHookshot = true, StunPowder = true, StunBoomerang = true, BombMultiplier = true, ThrownMultiplier = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush) { IsActive = false });
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        public override void Reset()
        {
            if (_carriableComponent.IsPickedUp)
                return; 

            _aiStunnedState.EndStun();
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
            _pushComponent.IsActive = false;
            _carriableComponent.IsActive = false;
            _aiComponent.ChangeState("hidden");
            _aiComponent.ChangeState("hidden");
            _damageState.CurrentLives = EnemyLives.Wizzrobe;
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

        private bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            if (pushType == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void InitSpawn()
        {
            _animator.Play("head");
        }

        private void BlinkTick(double timer)
        {
            var sinState = (float)((BlinkTime - timer) / BlinkTime);
            var state = MathF.Sin(sinState * 9f * MathF.PI * 2);

            // blink
            _sprite.IsVisible = state >= 0;
        }

        private void InitHead()
        {
            _animator.Play("head");
            _sprite.IsVisible = true;

            _damageField.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void InitHidden()
        {
            _sprite.IsVisible = false;
        }

        private void UpdateHidden()
        {
            // start spawning
            if (_hiddenTimer.State && _fieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
                _aiComponent.ChangeState("spawn");
        }

        private void InitStand()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;

            _direction = AnimationHelper.GetDirection(playerDirection);

            _damageField.IsActive = true;
            _pushComponent.IsActive = true;

            // look towards the player
            _animator.Play("stand_" + _direction);
        }

        private void Shoot()
        {
            var projectile = new EnemyWizzrobeProjectile(Map, new Vector2(EntityPosition.X, EntityPosition.Y - 7), _direction, 2.0f);
            Map.Objects.SpawnObject(projectile);
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Can not hit the enemy while he is spawning or hidden.
            if (_damageState.CurrentLives <= 0 || _damageState.IsInDamageState() || (_aiComponent.CurrentStateId != "stand" && !_aiStunnedState.IsStunned()))
                return Values.HitCollision.None;

            // Sword doesn't deal damage.
            if ((hitType & HitType.AnySword) != 0)
                damage = 0;

            // Powder does not knockback when stunning.
            if (hitType == HitType.MagicPowder)
                _aiStunnedState.StunKnockbackSpeed = 0;
            else if (hitType == HitType.Hookshot || hitType == HitType.Boomerang)
                _aiStunnedState.StunKnockbackSpeed = 4.0f;

            // Register the hit.
            var hit = _damageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);

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
