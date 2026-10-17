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
    internal class EnemyRaven : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerTimer _followTimer;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;
        private readonly Box _activationBox;

        private bool _startAttack;
        private float _flapCounter;
        private float _fadeOutTime = 750;
        private double _dirRadius;
        private int _dirIndex;
        private int _lives = EnemyLives.Raven;
        private int _dropIndex = 2;

        public EnemyRaven() : base("raven") { }

        public EnemyRaven(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 12, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 12, 0);
            EntitySize = new Rectangle(-8, -32, 16, 32);
            CanReset = false;

            _activationBox = new Box(posX + 8 - 20, posY - 32, 0, 40, 90, 16);

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/raven");

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
            var stateStart = new AiState(UpdateStart);
            var stateFlying = new AiState(UpdateFlying);
            stateFlying.Trigger.Add(_followTimer = new AiTriggerTimer(1000));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("start", stateStart);
            _aiComponent.States.Add("flying", stateFlying);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, true, false);

            _aiComponent.ChangeState("idle");

            // the player can jump over the enemy...
            var damageBox   = new CBox(EntityPosition, -3, -10, 0,  6,  6, 4, true);
            var hittableBox = new CBox(EntityPosition, -8, -16, 0, 16, 16, 8, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2) { IsActive = false });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(hittableBox, OnPush));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerTop));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));

            var spriteShadow = new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            Map.Objects.RegisterAlwaysAnimateObject(this);
            Map.Objects.RegisterAlwaysAnimateObject(spriteShadow);
        }

        private void UpdateIdle()
        {
            _dirIndex = MapManager.ObjLink.PosX < EntityPosition.X ? 0 : 1;
            _animator.Play("idle_" + _dirIndex);
        }

        private void StartWaiting()
        {
            _aiComponent.ChangeState("waiting");
            _damageField.IsActive = true;
        }

        private void UpdateWaiting()
        {
            _dirIndex = MapManager.ObjLink.PosX < EntityPosition.X ? 0 : 1;

            _animator.Play("idle_" + _dirIndex);

            // activate the crow
            if (MapManager.ObjLink.Body.BodyBox.Box.Intersects(_activationBox) || _startAttack)
            {
                _aiComponent.ChangeState("start");
            }
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

            _dirIndex = velocity.X < 0 ? 0 : 1;
            _animator.Play("fly_" + _dirIndex);

            if (distance < 85)
                UpdateFlyingSound();

            if (distance > 100)
                FadeOutDelete();
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction * 2.5f, _body.Velocity.Z);

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
            if (hitType == HitType.MagicPowder)
                return Values.HitCollision.None;

            _startAttack = true;

            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }
    }
}
