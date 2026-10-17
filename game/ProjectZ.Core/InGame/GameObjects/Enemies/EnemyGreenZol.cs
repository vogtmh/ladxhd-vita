using System;
using Microsoft.Xna.Framework;
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
    class EnemyGreenZol : GameObject, IHasVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AnimationComponent _animationComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawShadowComponent _drawShadow;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private bool _fallMode;
        private bool _pushable = false;
        private bool _isSpawning;
        private int _jumpsLeft;
        private int _lives = EnemyLives.GreenZol;
        private int _dropIndex = 2;

        public bool IsVisible { get; private set; }

        public Action OnHitPlayer;
        private GameObject _owner = null;
        private ObjSpriteShadow _spriteShadow;

        public EnemyGreenZol() : base("green zol") { }

        public EnemyGreenZol(Map.Map map, int posX, int posY, int posZ, bool fallMode, GameObject owner = null) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 13, posZ);
            ResetPosition  = new CPosition(posX + 8, posY + 13, posZ);
            EntitySize = new Rectangle(-8, -24, 16, 24);
            CanReset = true;
            OnReset = Reset;

            _fallMode = fallMode;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/green zol");
            _animator.Play("walk_1");

            _sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-6, -16));

            _body = new BodyComponent(EntityPosition, -6, -10, 12, 10, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                AbsorbPercentage = 1,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.DeepWater,
                Gravity = -0.15f,
                Bounciness = 0f,
                Drag = 0.85f
            };

            var stateInit = new AiState();
            // small delay before possibility of spawning; needed for situation where the enemy is directly at the door
            stateInit.Trigger.Add(new AiTriggerCountdown(450, null, () => _aiComponent.ChangeState("notSpawned")));
            var stateHidden = new AiState();
            stateHidden.Trigger.Add(new AiTriggerCountdown(2000, null, () => _aiComponent.ChangeState("notSpawned")));
            var stateNotSpawned = new AiState(UpdateNotSpawned);
            var stateSpawning = new AiState(UpdateSpawning);
            var stateFalling = new AiState(UpdateFalling);
            var stateJumping = new AiState(UpdateJumping);
            var stateWaiting = new AiState(UpdateWaiting);
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(WaitingTrigger, 750, 1250));
            var stateShaking = new AiState();
            stateShaking.Trigger.Add(new AiTriggerCountdown(500, ShakingTick, ShakingEnd));
            var stateDespawning = new AiState(UpdateDespawning);
            var stateSpawnDelay = new AiState();
            stateSpawnDelay.Trigger.Add(new AiTriggerCountdown(450, null, EndSpawnDelay));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("init", stateInit);
            _aiComponent.States.Add("notSpawned", stateNotSpawned);
            _aiComponent.States.Add("hidden", stateHidden);
            _aiComponent.States.Add("spawning", stateSpawning);
            _aiComponent.States.Add("fall", stateFalling);
            _aiComponent.States.Add("jumping", stateJumping);
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("shaking", stateShaking);
            _aiComponent.States.Add("despawning", stateDespawning);
            _aiComponent.States.Add("spawnDelay", stateSpawnDelay);

            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            new AiFallState(_aiComponent, _body, null, null, 250);
            new AiDeepWaterState(_body) { FallTime = 50 };

            _aiComponent.ChangeState("init");

            var damageBox   = new CBox(EntityPosition, -3,  -6, 0,  6,  4, 4);
            var hittableBox = new CBox(EntityPosition, -6, -11, 0, 12, 11, 8, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2) { OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, _drawShadow = new BodyDrawShadowComponent(_body, _sprite));

            if (_fallMode)
                InitFalling();
            else
            {
                _body.IsActive = false;
                _damageState.IsActive = false;
                _damageField.IsActive = false;
                _sprite.IsVisible = IsVisible = false;
            }
            // Add a sprite shadow and a force draw method. The "spawn" animation shows the "Zol" in the air for 6 frames
            // during it's 3rd animation frame, which still has a "Z-Position" of 0, so it's not technically in the air.
            // Detect when those frames are shown and force drawing the sprite shadow so it looks "proper". 
            _spriteShadow = new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            _spriteShadow.ForceDrawCheck = () => _isSpawning && _animator.CurrentFrameIndex > 1;

            // Do the same kind of override for the dynamic shadow, except force a fake "Z-Height" to force draw the shadow.
            _drawShadow.HeightOverride = () => _isSpawning && _animator.CurrentFrameIndex > 1 ? 5f : 0f;
        }

        private void OnDamagedPlayer()
        {
            OnHitPlayer?.Invoke();
        }

        public override void Reset()
        {
            _animator.Continue();
            _pushable = false;
            _body.IsActive = false;
            _body.Velocity.Z = 0;
            _body.VelocityTarget = Vector2.Zero;
            _damageState.IsActive = false;
            _damageField.IsActive = false;
            _hitComponent.IsActive = true;
            _sprite.IsVisible = false;
            _aiComponent.ChangeState("init");
            _aiComponent.ChangeState("init");
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        public void SpawnDelay()
        {
            _body.IgnoresZ = true;
            _animator.Play("idle");
            _sprite.IsVisible = IsVisible = true;
            _aiComponent.ChangeState("spawnDelay");
        }

        private void EndSpawnDelay()
        {
            _damageState.IsActive = true;
            _damageField.IsActive = true;
            _body.IsActive = true;
            _body.IgnoresZ = false;
            _body.IsGrounded = false;

            _jumpsLeft = Game1.RandomNumber.Next(3, 5);
            ToJump();
        }

        private void InitFalling()
        {
            _body.IsGrounded = false;
            _animator.Play("jump");
            _aiComponent.ChangeState("jumping");
            _sprite.Color = Color.Transparent;
        }

        private void UpdateNotSpawned()
        {
            var distVec = EntityPosition.Position - MapManager.ObjLink.Position;

            // spawn?
            if (distVec.Length() < 32)
                ToSpawning();
        }

        private void ToSpawning()
        {
            _jumpsLeft = Game1.RandomNumber.Next(3, 5);
            _animator.Play("spawn");
            _sprite.IsVisible = IsVisible = true;
            _isSpawning = true;
            _aiComponent.ChangeState("spawning");
        }

        private void UpdateSpawning()
        {
            // fall down
            if (!_animator.IsPlaying)
                ToFalling();
        }

        private void ToFalling()
        {
            Game1.AudioManager.PlaySoundEffect("D360-36-24");

            _pushable = true;
            _body.IsActive = true;
            _damageState.IsActive = true;
            _isSpawning = false;

            // fall down
            EntityPosition.Z = 5;

            _animator.Play("idle");
            _aiComponent.ChangeState("fall");
        }

        private void UpdateFalling()
        {
            if (_body.IsGrounded)
                _aiComponent.ChangeState("shaking");
        }

        private void UpdateWaiting()
        {
            _animator.Play("idle");
        }

        private void WaitingTrigger()
        {
            if (_jumpsLeft <= 0 && !_fallMode)
                ToDespawn();
            else
                ToJump();
        }

        private void ToJump()
        {
            _jumpsLeft--;

            _aiComponent.ChangeState("jumping");
            _animator.Play("jump");

            Game1.AudioManager.PlaySoundEffect("D360-36-24");

            var vecDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (vecDirection != Vector2.Zero)
                vecDirection.Normalize();
            vecDirection *= 0.75f;

            _body.VelocityTarget = new Vector2(vecDirection.X, vecDirection.Y);
            _body.Velocity.Z = 1.5f;
        }

        private void UpdateJumping()
        {
            _sprite.Color = Color.White * MathF.Min((100 - EntityPosition.Z) / 10f, 1);

            if (_body.IsGrounded)
            {
                _body.VelocityTarget = Vector2.Zero;
                _aiComponent.ChangeState("waiting");
            }
        }

        private void ShakingTick(double time)
        {
            var shakeState = (float)Math.Sin(time / 25f);
            _animationComponent.SpriteOffset.X = -6 + shakeState;
            _animationComponent.UpdateSprite();
        }

        private void ShakingEnd()
        {
            _damageState.MoveBody = false;
            _damageState.CooldownTime = 0;
            _damageField.IsActive = true;
            _animationComponent.SpriteOffset.X = -6;
            _animationComponent.UpdateSprite();
            _aiComponent.ChangeState("waiting");
        }

        private void ToDespawn()
        {
            _aiComponent.ChangeState("despawning");
            _animator.Play("despawn");

            _pushable = false;
            _body.IsActive = false;
            _damageState.IsActive = false;
            _damageField.IsActive = false;
            _damageState.MoveBody = true;
            _damageState.CooldownTime = AiDamageState.BlinkTime * 6;
        }

        private void UpdateDespawning()
        {
            // hide
            if (_animator.IsPlaying)
                return;

            _sprite.IsVisible = IsVisible = false;
            _aiComponent.ChangeState("hidden");
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (!_pushable || type != PushableComponent.PushType.Impact)
                return false;

            _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);
            return true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If spawned during Slime Eye it can not drop powerups.
            if (_owner != null)
                _damageState.SpawnPowerups = false;
            
            // When hit with anything but the sword, they get knocked back.
            if ((hitType & HitType.AnySword) == 0)
            {
                _damageState.CooldownTime = AiDamageState.BlinkTime * 6;
                _damageState.MoveBody = true;
            }
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
