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
    internal class EnemyCrow : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerTimer _followTimer;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CBox _damageCollider;
        private readonly CBox _hittableBoxFly;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;
        private readonly Box _activationBox;

        private float _flapCounter;
        private float _fadeOutTime = 750;
        private double _dirRadius;
        private int _dirIndex;
        private bool _goldLeaf;
        private bool _startAttack;
        private int _lives = EnemyLives.Crow;
        private int _dropIndex = 2;

        private const string _leafSaveKey = "ow_goldLeafCrow";

        public EnemyCrow() : base("crow") { }

        public EnemyCrow(Map.Map map, int posX, int posY, bool goldLeaf) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 12, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 12, 0);
            CanReset = false;

            _goldLeaf = goldLeaf;

            if (_goldLeaf)
                EntitySize = new Rectangle(-8, -32, 16, 48);
            else
                EntitySize = new Rectangle(-8, -32, 16, 36);

            // abort spawn if the player already has the leaf
            if (_goldLeaf && Game1.GameManager.SaveManager.GetString(_leafSaveKey) == "1")
            {
                IsDead = true;
                return;
            }

            _activationBox = new Box(posX - 10, posY - 32, 0, 36, 80, 16);

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/crow");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-7, -16));

            _body = new BodyComponent(EntityPosition, -6, -14, 12, 14, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                FieldLocked = false,
                CollisionTypes = Values.CollisionTypes.None,
                IgnoreHoles = true,
                IgnoresZ = true
            };

            var stateIdle = new AiState(UpdateIdle);
            stateIdle.Trigger.Add(new AiTriggerCountdown(1000, null, StartWaiting));
            var stateWaiting = new AiState(UpdateWaiting);
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(UpdateLookDirection, 250, 750));
            var stateStart = new AiState(UpdateStart) { Init = InitStart };
            var stateFlying = new AiState(UpdateFlying);
            stateFlying.Trigger.Add(_followTimer = new AiTriggerTimer(1000));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("start", stateStart);
            _aiComponent.States.Add("flying", stateFlying);
            _aiComponent.ChangeState("waiting");
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, true, false);
            _damageCollider = new CBox(EntityPosition, -3, -10, 0,  6,  6, 4, true);
            var hittableBox = new CBox(EntityPosition, -8, -32, 0, 16, _goldLeaf ? 48 : 36, 8);
            _hittableBoxFly = new CBox(EntityPosition, -8, -16, 0, 16, 16, 8, true);

            if (_goldLeaf)
            {
                _damageState.OnDeath = OnDeath;
                _damageState.DropTableIndex = 0;
                _damageState.SpawnPowerups = false;
                _damageState.IsActive = false;
                AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            }
            else
            {
                AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_hittableBoxFly, OnHit) { BoomerangMultiplier = true });
            }
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_damageCollider, HitType.Enemy, 4));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_damageCollider, OnPush));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerTop));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));

            var spriteShadow = new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            Map.Objects.RegisterAlwaysAnimateObject(this);
            Map.Objects.RegisterAlwaysAnimateObject(spriteShadow);
        }

        private void InitStart()
        {
            if (_goldLeaf)
                _hitComponent.HittableBox = _hittableBoxFly;
        }

        private void UpdateIdle()
        {
            _dirIndex = MapManager.ObjLink.PosX < EntityPosition.X ? 0 : 1;
            _animator.Play("idle_" + _dirIndex);
        }

        private void StartWaiting()
        {
            _aiComponent.ChangeState("waiting");
        }

        private void UpdateWaiting()
        {
            // activate the crow
            if (!_goldLeaf && (MapManager.ObjLink.Body.BodyBox.Box.Intersects(_activationBox) || _startAttack))
                _aiComponent.ChangeState("start");
        }

        private void UpdateFlyingSound()
        {
            _flapCounter += Game1.DeltaTime;

            if (_lives > 0 && _flapCounter > 430)
            {
                Game1.AudioManager.PlaySoundEffect("D378-45-2D");
                _flapCounter = 0;
            }
        }

        private void UpdateStart()
        {
            _animator.Play("fly_" + _dirIndex);

            EntityPosition.Set(new Vector3(
                EntityPosition.X,
                EntityPosition.Y,
                EntityPosition.Z + 0.5f * Game1.TimeMultiplier));

            if (EntityPosition.Z >= 15)
            {
                EntityPosition.Z = 15;
                _aiComponent.ChangeState("flying");
                _damageState.IsActive = true;
                _dirRadius = Math.Atan2(MapManager.ObjLink.PosY - EntityPosition.Y, MapManager.ObjLink.PosX - EntityPosition.X);
            }
            UpdateFlyingSound();
        }

        private void UpdateFlying()
        {
            var direction = MapManager.ObjLink.Position - new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z);
            var directionRadius = Math.Atan2(direction.Y, direction.X);
            var distance = direction.Length();

            if (distance < 80)
            {
                var followSpeed = 0.02f;
                if (directionRadius < _dirRadius - followSpeed || _followTimer.State)
                    _dirRadius -= followSpeed * Game1.TimeMultiplier;
                else if (directionRadius > _dirRadius + followSpeed)
                    _dirRadius += followSpeed * Game1.TimeMultiplier;
            }

            var velocity = new Vector2((float)Math.Cos(_dirRadius), (float)Math.Sin(_dirRadius));
            _body.VelocityTarget = velocity * 1.25f;

            _dirIndex = velocity.X < 0 ? -1 : 1;
            _animator.Play("fly_" + _dirIndex);

            if (distance < 85)
                UpdateFlyingSound();

            if (distance > 100)
                FadeOutDelete();
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (_aiComponent.CurrentStateId == "waiting")
                return false;

            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void FadeOutDelete()
        {
            _fadeOutTime -= Game1.DeltaTime;

            if (_fadeOutTime < 0)
                Map.Objects.DeleteObjects.Add(this);
            else
                _sprite.Color = Color.White * MathHelper.Clamp(_fadeOutTime / 100, 0, 1);
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Remove gold leaf crow damage knockback when final hit is with sword.
            if (_goldLeaf && (damage >= 2 || _damageState.CurrentLives <= 1) && (hitType & HitType.Sword) != 0)
                _damageState.MoveBody = false;

            if (hitType == HitType.MagicPowder)
                return Values.HitCollision.None;

            // start attacking?
            if (_aiComponent.CurrentStateId == "waiting" && (hitType == HitType.Bomb || hitType == HitType.ThrownObject))
            {
                _aiComponent.ChangeState("start");
                return Values.HitCollision.None;
            }
            _startAttack = true;

            // If we got here, it was probably a hit from the back or another weapon than sword.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                // Stop the gold leaf crow dead in its tracks when it dies.
                if (_goldLeaf)
                {
                    _body.Velocity = Vector3.Zero;
                    _body.VelocityTarget = Vector2.Zero;
                }
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }

        private void OnDeath(bool pieceofpower)
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();
            playerDirection *= 1.25f;

            // spawn the golden leaf jumping towards the player
            var objLeaf = new ObjItem(Map, 0, 0, null, _leafSaveKey, "goldLeaf", null, true);
            objLeaf.EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y, EntityPosition.Z));
            objLeaf.SetVelocity(new Vector3(playerDirection.X, playerDirection.Y, 1.5f));
            objLeaf.Collectable = false;
            Map.Objects.SpawnObject(objLeaf);

            _damageState.BaseOnDeath(pieceofpower);
        }

        private void UpdateLookDirection()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection.Length() > 96)
                return;

            _dirIndex = MapManager.ObjLink.PosX < EntityPosition.X - _dirIndex * 4 ? -1 : 1;
            _animator.Play("idle_" + _dirIndex);
        }
    }
}
