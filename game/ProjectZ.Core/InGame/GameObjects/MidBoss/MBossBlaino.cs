using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;
using ProjectZ.InGame.Map;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    internal enum BlainoAttack { None, Jab, Lunge, Haymaker }

    internal class MBossBlaino : GameObject
    {
        public const int StunTime = 2667;

        private const int AttackCooldownMin = 800;
        private const int AttackCooldownMax = 1800;

        private const int IntroDelay = 2133;

        private const int HitTime1 = 300;
        private const int HitTime2 = 400;
        private const int HitTime3 = 350;

        private const int TimeSwing0 = 2000;
        private const int TimeSwing1 = 200;
        private const int TimeSwing2 = 400;
        private const float SwingRotationDivisor = 110f;

        private const int IgnoreHitsBase = 133;
        private const int IgnoreHitsJab = 267;
        private const int IgnoreHitsLunge = 533;

        private const float AttackRange = 44f;

        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly AnimationComponent _animationComponent;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DictAtlasEntry _gloveSprite;
        private readonly DamageFieldComponent _damageFieldComponent;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;
        private readonly MBossBlainoGlove _objGlove;

        private readonly string _saveKey;
        private readonly string _resetDoor;

        private Vector2 _spawnPosition;
        private Vector2 _glovePosition;
        private Vector2 _gloveStartPosition;
        private Vector2 _gloveTargetPosition;
        private Vector2 _gloveBodyOffset;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;
        private Vector2 _lastPosition;
        private Vector2 _swingOrigin = new Vector2(0, -10);

        private float _swingStartRotation;
        private float _swingStartDistance;
        private float _lastSwingRotation;
        private int _direction = -1;
        private int _jumpFollowDelay;
        private int _openingAttacks;
        private int _lives = EnemyLives.Blaino;
        private bool _drawGlove;

        private BlainoAttack _currentAttack;
        private bool _knockoutWindow;
        private double _ignoreHitsCounter;
        private double _attackCooldown;

        public AiDamageState DamageState => _damageState;
        public AiComponent AiComponent => _aiComponent;

        public BlainoAttack CurrentAttack => _currentAttack;

        public bool KnockoutWindow => _knockoutWindow;

        public MBossBlaino() : base("blaino") { }

        public MBossBlaino(Map.Map map, int posX, int posY, string saveKey, string resetDoor) : base(map)
        {
            if (!string.IsNullOrEmpty(saveKey) &&
                Game1.GameManager.SaveManager.GetString(saveKey) == "1")
            {
                IsDead = true;
                return;
            }

            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-16, -32, 32, 32);

            _saveKey = saveKey;
            _resetDoor = resetDoor;

            _spawnPosition = EntityPosition.Position;

            _gloveSprite = Resources.GetSprite("blaino glove");

            _animator = AnimatorSaveLoad.LoadAnimator("MidBoss/blaino");
            _animator.Play("jump");

            _sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8)
            {
                SimpleMovement = false,
                Drag = 0.65f,
                DragAir = 0.75f,
                Gravity = -0.15f,
                MoveCollision = OnMoveCollision,
                FieldRectangle = map.GetField(posX, posY),
            };

            var stateWaiting = new AiState(UpdateWaiting);
            var stateJumping = new AiState(UpdateJumping) { Init = InitJump };
            var stateBox = new AiState(UpdateBox);

            var stateHit0 = new AiState(UpdateHit0) { Init = InitHit };
            var stateHit1 = new AiState();
            stateHit1.Trigger.Add(new AiTriggerCountdown(HitTime1, TickHit1, () => TickHit1(0)));
            var stateHit2 = new AiState();
            stateHit2.Trigger.Add(new AiTriggerCountdown(HitTime2, TickHit2, () => TickHit2(0)));
            var stateHit3 = new AiState() { Init = InitHit3 };
            stateHit3.Trigger.Add(new AiTriggerCountdown(HitTime3, TickHit3, () => TickHit3(0)));
            var stateHitBlocked = new AiState();
            stateHitBlocked.Trigger.Add(new AiTriggerCountdown(250, null, () => _aiComponent.ChangeState("hit3")));

            var stateSwing0 = new AiState() { Init = InitSwing0 };
            stateSwing0.Trigger.Add(new AiTriggerCountdown(TimeSwing0, TickSwing0, () => TickSwing0(0)));
            var stateSwing1 = new AiState();
            stateSwing1.Trigger.Add(new AiTriggerCountdown(TimeSwing1, null, () => _aiComponent.ChangeState("swing2")));
            var stateSwing2 = new AiState() { Init = InitSwing2 };
            stateSwing2.Trigger.Add(new AiTriggerCountdown(TimeSwing2, TickSwing2, () => TickSwing2(0)));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("jumping", stateJumping);
            _aiComponent.States.Add("box", stateBox);
            _aiComponent.States.Add("hit0", stateHit0);
            _aiComponent.States.Add("hit1", stateHit1);
            _aiComponent.States.Add("hit2", stateHit2);
            _aiComponent.States.Add("hit3", stateHit3);
            _aiComponent.States.Add("hitBlocked", stateHitBlocked);
            _aiComponent.States.Add("swing0", stateSwing0);
            _aiComponent.States.Add("swing1", stateSwing1);
            _aiComponent.States.Add("swing2", stateSwing2);

            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, 0, true, false);
            _damageState.AddBossDamageState(OnDeath);
            _damageState.ExplosionOffsetY = 8;
            _damageState.BossHitSound = true;
            _aiComponent.ChangeState("waiting");

            var hittableBox = new CBox(EntityPosition, -7, -14, 0, 14, 14, 8, true);
            var damageBox = new CBox(EntityPosition, -8, -14, 0, 16, 14, 8, true);

            AddComponent(DamageFieldComponent.Index, _damageFieldComponent = new DamageFieldComponent(damageBox, HitType.Enemy, 4));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true, MagicRodMultiplier = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(damageBox, OnPush));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, Draw, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            _objGlove = new MBossBlainoGlove(map, this, EntityPosition.Position, resetDoor);
            Map.Objects.SpawnObject(_objGlove);
        }

        private void Update()
        {
            if (_ignoreHitsCounter > 0)
                _ignoreHitsCounter -= Game1.DeltaTime;
            if (_attackCooldown > 0)
                _attackCooldown -= Game1.DeltaTime;

            // Keep the glove (and its hitbox) locked to the body even while the
            // swing/hit ticks are suspended during the damage/knockback state.
            if (_objGlove.IsActive)
                _objGlove.EntityPosition.Set(EntityPosition.Position + _gloveBodyOffset);
        }

        private void UpdateWaiting()
        {
            // Jump around the start position.
            if (_body.IsGrounded)
            {
                var targetDirection = _spawnPosition - EntityPosition.Position;
                if (targetDirection == Vector2.Zero)
                    targetDirection = new Vector2(-1, -0.25f);
                targetDirection.Normalize();

                _body.VelocityTarget = targetDirection * 0.25f;
                _body.Velocity.Z = 1;
            }

            // Player entered the room.
            if (_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
            {
                // Start the boss music.
                Game1.AudioManager.SetMusicFadeTransition(79, 2, 350);

                _attackCooldown = IntroDelay;
                _aiComponent.ChangeState("jumping");
            }
        }

        private void InitJump()
        {
            _animator.Play("jump");
        }

        private void UpdateJumping()
        {
            GloveAnimationUpdate();

            // Landed after a jump?
            if (!_body.IsGrounded)
                return;

            var playerPosition = MapManager.ObjLink.Position;

            // Face the player.
            var playerDirection = EntityPosition.Position - playerPosition;
            if (Math.Abs(playerDirection.X) > 8)
                _direction = playerDirection.X < 0 ? 1 : -1;
            _animationComponent.MirroredH = _direction == 1;

            if (TryStartAttack(Vector2.Distance(EntityPosition.Position, playerPosition)))
                return;

            // Jump in front of the player.
            var verticalDistance = Math.Abs(EntityPosition.Y - MapManager.ObjLink.EntityPosition.Y) / 2;
            var sinDist = MathF.Sin((float)Game1.TotalGameTime / 500);
            var distanceToPlayer = 26 + sinDist * 2 + verticalDistance;
            var targetPosition = new Vector2(EntityPosition.X + _direction * distanceToPlayer, EntityPosition.Y);
            var targetDirection = playerPosition - targetPosition;
            var distance = targetDirection.Length();

            if (_jumpFollowDelay <= 0)
            {
                var speedMultiplier = MathHelper.Clamp(distance / 12, 0.25f, 1.0f);
                if (targetDirection != Vector2.Zero)
                    targetDirection.Normalize();
                _body.VelocityTarget = targetDirection * speedMultiplier;
            }
            else
            {
                _jumpFollowDelay--;
            }
            _body.Velocity.Z = 1;
        }

        private bool TryStartAttack(float distanceToPlayer)
        {
            // A dazed Link is attacked immediately; the cooldown check comes after this
            // branch in the original, so it does not apply here.
            if (MapManager.ObjLink.IsStunned())
            {
                if (distanceToPlayer < AttackRange)
                {
                    BeginHaymaker();
                    return true;
                }
                return false;
            }

            if (_attackCooldown > 0 || distanceToPlayer >= AttackRange)
                return false;

            if (_openingAttacks < 5)
            {
                _openingAttacks++;
                BeginJab();
                return true;
            }

            switch (Game1.RandomNumber.Next(0, 4))
            {
                case 1:
                    BeginLunge();
                    break;
                case 2:
                    BeginHaymaker();
                    break;
                default:
                    BeginJab();
                    break;
            }
            return true;
        }

        private void EndAttack()
        {
            _currentAttack = BlainoAttack.None;
            _knockoutWindow = false;
            _drawGlove = false;
            _objGlove.IsActive = false;
            _attackCooldown = Game1.RandomNumber.Next(AttackCooldownMin, AttackCooldownMax + 1);
        }

        private void BeginJab()
        {
            _currentAttack = BlainoAttack.Jab;
            _body.VelocityTarget = Vector2.Zero;
            _objGlove.SetHitDirection(_direction);

            Game1.AudioManager.PlaySoundEffect("D378-10-0A");
            _animator.Play("box_short");
            _aiComponent.ChangeState("box");
        }

        private void UpdateBox()
        {
            GloveAnimationUpdate();

            if (_animator.IsPlaying)
                return;

            if (Game1.RandomNumber.Next(0, 4) == 0)
            {
                EndAttack();
                _aiComponent.ChangeState("jumping");
                return;
            }

            Game1.AudioManager.PlaySoundEffect("D378-10-0A");
            _animator.Play("box_short");
        }

        private void BeginLunge()
        {
            _currentAttack = BlainoAttack.Lunge;
            _objGlove.SetHitDirection(_direction);
            _aiComponent.ChangeState("hit0");
        }

        private void InitHit()
        {
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("hit0");
        }

        private void UpdateHit0()
        {
            if (!_animator.IsPlaying)
            {
                _startPosition = EntityPosition.Position;
                _targetPosition = _startPosition + new Vector2(-_direction * 6, 0);
                _lastPosition = Vector2.Zero;

                _objGlove.IsActive = true;
                _objGlove.SetHitDirection(_direction);
                _drawGlove = true;

                _animator.Play("hit1");
                _aiComponent.ChangeState("hit1");
            }
        }

        private void TickHit1(double time)
        {
            // Move 6px back.
            var percentage = 1 - (float)(time / HitTime1);

            BodyMove(percentage);
            SetGlovePosition(new Vector2(5, -14));

            if (time == 0)
            {
                _startPosition = EntityPosition.Position;
                _targetPosition = _startPosition + new Vector2(_direction * 22, 0);
                _lastPosition = Vector2.Zero;

                TickHit2(HitTime2);
                _animator.Play("hit2");
                _aiComponent.ChangeState("hit2");
            }
        }

        private void TickHit2(double time)
        {
            var percentageGlove = MathHelper.Clamp((float)(HitTime2 - time - 25) / 125, 0, 1);
            var sPercentageGlove = 1 - MathF.Cos(percentageGlove * MathF.PI / 2);

            var percentage = MathHelper.Clamp((float)(HitTime2 - time - 75) / 125, 0, 1);
            var sPercentage = 1 - MathF.Cos(percentage * MathF.PI / 2);

            // Move forward.
            BodyMove(sPercentage);

            // Update the glove position.
            SetGlovePosition(Vector2.Lerp(new Vector2(-15, -11), new Vector2(-15 - 16, -11), sPercentageGlove));

            if (time == 0)
                _aiComponent.ChangeState("hit3");
        }

        private void InitHit3()
        {
            _gloveStartPosition = _glovePosition;
            _gloveTargetPosition = new Vector2(-15, -11);

            // The lunge ends with Blaino hopping back out of sword range.
            _startPosition = EntityPosition.Position;
            _targetPosition = _startPosition + new Vector2(-_direction * 34, 0);
            _lastPosition = Vector2.Zero;
        }

        private void TickHit3(double time)
        {
            var percentage = 1 - (float)(time / HitTime3);

            BodyMove(MathF.Sin(percentage * MathF.PI / 2));
            SetGlovePosition(Vector2.Lerp(_gloveStartPosition, _gloveTargetPosition, percentage));

            if (time == 0)
            {
                EndAttack();
                _aiComponent.ChangeState("jumping");
            }
        }

        private void BeginHaymaker()
        {
            _currentAttack = BlainoAttack.Haymaker;
            _aiComponent.ChangeState("swing0");
        }

        private void InitSwing0()
        {
            _body.VelocityTarget = Vector2.Zero;
            _startPosition = EntityPosition.Position;
            _targetPosition = _startPosition + new Vector2(-_direction * 23, 0);
            _lastPosition = Vector2.Zero;

            _objGlove.IsActive = true;
            _objGlove.SetHitDirection(_direction);
            _drawGlove = true;

            _animator.Play("hit1");
            Game1.AudioManager.PlaySoundEffect("D360-51-33");
        }

        private void TickSwing0(double counter)
        {
            var percentage = (float)(TimeSwing0 - counter) / SwingRotationDivisor + MathF.PI / 2 - MathF.Asin(3 / 4f);

            var finishedSwing = false;
            if ((_lastSwingRotation % MathF.PI) > (percentage % MathF.PI) && percentage / MathF.PI >= 5)
            {
                percentage = MathF.PI;
                finishedSwing = true;
            }
            // Moving back.
            var movePercentage = MathHelper.Clamp((float)((TimeSwing0 - counter) / 750), 0, 1);
            BodyMove(MathF.Sin(movePercentage * MathF.PI / 2));

            // Update the glove position.
            var offset = new Vector2(2 - MathF.Cos(percentage) * 4 + MathF.Sin(percentage) * 2, -6 + MathF.Sin(percentage) * 8);
            SetGlovePosition(new Vector2(5, -14) + offset);

            if (counter == 0 || finishedSwing)
                _aiComponent.ChangeState("swing1");

            _lastSwingRotation = percentage;
        }

        private void InitSwing2()
        {
            _knockoutWindow = true;

            _startPosition = EntityPosition.Position;
            _targetPosition = _startPosition + new Vector2(_direction * 36, 0);
            _lastPosition = Vector2.Zero;

            var originDirection = new Vector2(_glovePosition.X + 5.5f, _glovePosition.Y + 5.5f) - _swingOrigin;
            _swingStartDistance = originDirection.Length();
            _swingStartRotation = MathF.Atan2(originDirection.Y, originDirection.X);
        }

        private void TickSwing2(double counter)
        {
            var percentage = MathHelper.Clamp((float)((TimeSwing2 - counter) / (TimeSwing2 - 75)), 0, 1);
            var newRotation = MathHelper.Lerp(_swingStartRotation, _swingStartRotation + 3.75f, percentage);

            // Moving forward.
            BodyMove(MathF.Sin(percentage * MathF.PI / 2));
            SetGlovePosition(new Vector2(_swingOrigin.X - 5.5f, _swingOrigin.Y - 5.5f) + new Vector2(MathF.Cos(newRotation), MathF.Sin(newRotation)) * _swingStartDistance);

            // Change animation frame.
            if (newRotation > _swingStartRotation + 1.85f)
                _animator.Play("hit2");

            if (counter == 0)
            {
                EndAttack();
                _aiComponent.ChangeState("jumping");
            }
        }

        private void BodyMove(float percentage)
        {
            // adjust the start/target positions if the body has moved
            if (_lastPosition != Vector2.Zero)
            {
                var positionOffset = EntityPosition.Position - _lastPosition;
                _startPosition += positionOffset;
                _targetPosition += positionOffset;
            }
            var targetPosition = Vector2.Lerp(_startPosition, _targetPosition, percentage);
            targetPosition.X = MathHelper.Clamp(targetPosition.X, _body.FieldRectangle.X + 7, _body.FieldRectangle.Right - 7);
            EntityPosition.Set(targetPosition);
            _lastPosition = targetPosition;
        }

        private void SetGlovePosition(Vector2 newPosition)
        {
            _glovePosition = newPosition;

            var world = new Vector2(
                EntityPosition.X + _glovePosition.X * -_direction - (_direction == 1 ? 11 : 0),
                EntityPosition.Y - EntityPosition.Z + _glovePosition.Y);

            _gloveBodyOffset = world - EntityPosition.Position;
            _objGlove.EntityPosition.Set(world);
        }

        private void GloveAnimationUpdate()
        {
            if (_animator.CollisionRectangle != Rectangle.Empty)
            {
                _objGlove.IsActive = true;

                var world = EntityPosition.Position +
                    new Vector2(_animator.CollisionRectangle.X * -_direction - (_direction == 1 ? 11 : 0), _animator.CollisionRectangle.Y);

                _gloveBodyOffset = world - EntityPosition.Position;
                _objGlove.EntityPosition.Set(world);
            }
            else
            {
                _objGlove.IsActive = false;
            }
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            _sprite.Draw(spriteBatch);

            if (_drawGlove)
            {
                DrawHelper.DrawNormalized(spriteBatch, _gloveSprite,
                    new Vector2(
                        EntityPosition.X + _glovePosition.X * -_direction - (_direction == 1 ? 11 : 0),
                        EntityPosition.Y - EntityPosition.Z + _glovePosition.Y), Color.White);
            }
        }

        private void OnMoveCollision(Values.BodyCollision collision)
        {
            // make sure to not continuesly jump into the wall
            if ((collision & Values.BodyCollision.Horizontal) != 0 &&
                _aiComponent.CurrentStateId == "jumping")
            {
                _direction = -_direction;
            }
        }

        public bool IsInvulnerable()
        {
            return _ignoreHitsCounter > 0 ||
                   _damageState.IsInDamageState() ||
                   _aiComponent.CurrentStateId == "damage" ||
                   _aiComponent.CurrentStateId == "dying";
        }

        public bool PlayerFacingAway()
        {
            return (_direction == -1 && MapManager.ObjLink.Direction == 2) ||
                   (_direction == 1 && MapManager.ObjLink.Direction == 0);
        }

        public void GloveParried(Vector2 direction)
        {
            Game1.AudioManager.PlaySoundEffect("D360-09-09");

            var awayFromBlaino = MapManager.ObjLink.EntityPosition.Position - EntityPosition.Position;
            if (awayFromBlaino != Vector2.Zero)
                awayFromBlaino.Normalize();
            else
                awayFromBlaino = new Vector2(_direction, 0);

            // Parrying the swing is just as bad as walking into it.
            if (_currentAttack == BlainoAttack.Haymaker && _knockoutWindow)
            {
                Game1.AudioManager.PlaySoundEffect("D360-11-0B");
                MapManager.ObjLink.Knockout(new Vector2(_direction * 0.75f, -1), _resetDoor);
                return;
            }

            float pushStrength;
            switch (_currentAttack)
            {
                case BlainoAttack.Jab:
                    _ignoreHitsCounter = IgnoreHitsJab;
                    pushStrength = 2.5f;
                    break;
                case BlainoAttack.Lunge:
                    _ignoreHitsCounter = IgnoreHitsLunge;
                    pushStrength = 2.5f;
                    break;
                default:
                    _ignoreHitsCounter = IgnoreHitsBase;
                    pushStrength = 1.5f;
                    break;
            }

            MapManager.ObjLink.Body.Velocity += new Vector3(
                awayFromBlaino.X * pushStrength, awayFromBlaino.Y * pushStrength, 0);

            // Recoils Blaino away from Link as well.
            _body.VelocityTarget = Vector2.Zero;
            _body.Velocity.X -= awayFromBlaino.X * 4;
            _body.Velocity.Y -= awayFromBlaino.Y * 4;
        }

        public bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            if (pushType == PushableComponent.PushType.Impact)
            {
                _jumpFollowDelay = 2;

                var mult = 2.25f;
                _body.Velocity = new Vector3(direction.X * mult, direction.Y * mult, _body.Velocity.Z);
                _body.VelocityTarget = direction * 0.125f;
            }

            return true;
        }

        public void NoHitKnockback(Vector2 direction)
        {
            _body.VelocityTarget = Vector2.Zero;
            _body.Velocity.X += direction.X * 4;
            _body.Velocity.Y += direction.Y * 4;
            Game1.AudioManager.PlaySoundEffect("D360-09-09");
        }

        public void GlovePush(Vector2 direction)
        {
            _objGlove.IsActive = false;
            _aiComponent.ChangeState("hitBlocked");

            _body.Velocity = new Vector3(direction.X, direction.Y, _body.Velocity.Z);
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (IsInvulnerable())
                return Values.HitCollision.None;

            if (hitType == HitType.Bomb || hitType == HitType.Bow || hitType == HitType.MagicRod)
                return Values.HitCollision.Enemy;

            if ((hitType & HitType.SwordSpin) == 0)
            {
                var hitDir = AnimationHelper.GetDirection(direction);

                if ((hitDir == 2 && _direction == -1) || (hitDir == 0 && _direction == 1))
                {
                    NoHitKnockback(direction);
                    _ignoreHitsCounter = IgnoreHitsBase;
                    return Values.HitCollision.Blocking;
                }
            }
            _body.DragAir = 0.75f;

            _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            if (_damageState.CurrentLives <= 0)
            {
                _objGlove.OnDeath();
                _damageFieldComponent.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
                _animator.IsPlaying = false;
                _body.VelocityTarget = Vector2.Zero;
                Map.Objects.DeleteObjects.Add(_objGlove);
            }
            return Values.HitCollision.Enemy;
        }

        private void OnDeath()
        {
            if (!string.IsNullOrEmpty(_saveKey))
                Game1.GameManager.SaveManager.SetString(_saveKey, "1");

            // Stop the boss music.
            Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);

            Game1.AudioManager.PlaySoundEffect("D360-27-1B");
            Map.Objects.SpawnObject(new ObjDungeonFairy(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 8));

            Map.Objects.DeleteObjects.Add(this);
        }
    }
}
