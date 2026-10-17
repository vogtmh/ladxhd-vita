﻿using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    class MBossGohma : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly AiTriggerCountdown _attackAbortTrigger;
        private readonly AnimationComponent _animationComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private const float AttackSpeed = 1.5f;
        private const float AttackReturnSpeed = 1f;
        private const float WalkSpeed = 1.0f;
        private const float RunSpeed = 1.5f;
        private const int ShakeTime = 1500;
        private const int BodyWidth = 28;

        private Vector2 _attackStartPosition;
        private Vector2 _attackTargetPosition;

        // 0: both parts are alive
        // 1: one of them is dead
        // 2: both parts are dead
        private int _bossState;
        private int _lives = EnemyLives.Ghoma;
        private bool _isDying;
        private bool _usedOcarina;
        private bool _initialized;
        private float _resetTimer;
        private string _saveKey;

        private Rectangle _fieldRectangle;
        private bool _playerInField => _fieldRectangle.Contains(MapManager.ObjLink.CenterPosition);

        public MBossGohma(Map.Map map, int posX, int posY, string saveKey, bool onTop) : base(map, "gohma")
        {
            EntityPosition = new CPosition(posX + 16, posY + 16, 0);
            ResetPosition = new CPosition(posX + 16, posY + 16, 0);
            EntitySize = new Rectangle(-16, -16, 32, 16);
            CanReset = false;

            // Get the field the object is in.
            if (map != null)
                _fieldRectangle = map.GetField(posX, posY);

            _saveKey = saveKey;

            // there is no door and this is strange because in the original you can kill only one of them and just reenter the room
            if (!string.IsNullOrEmpty(_saveKey))
            {
                // check if the boss was already killed
                var bossState = Game1.GameManager.SaveManager.GetInt(_saveKey, 0);
                if (bossState == 2)
                {
                    IsDead = true;
                    return;
                }
                else
                {
                    // Reset from a previous fight so we can try again.
                    Game1.GameManager.SaveManager.SetInt(_saveKey, 0);
                    Game1.GameManager.SaveManager.SetString(_saveKey + "fail", "0");
                }
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
            }
            _animator = AnimatorSaveLoad.LoadAnimator("MidBoss/gohma");
            _sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(0, -16));

            _body = new BodyComponent(EntityPosition, -BodyWidth / 2, -14, BodyWidth, 14, 8)
            {
                IgnoreHoles = true,
                MoveCollision = OnCollision,
                FieldRectangle = Map.GetField(posX, posY)
            };

            _aiComponent = new AiComponent();

            var stateIdle = new AiState(UpdateIdle);
            var stateWalk = new AiState { Init = InitWalk };
            stateWalk.Trigger.Add(new AiTriggerRandomTime(ChangeState, 1500, 3000));
            var stateRun = new AiState { Init = InitRun };
            stateRun.Trigger.Add(new AiTriggerRandomTime(ChangeState, 1500, 2500));
            var stateShake = new AiState { Init = InitShake };
            stateShake.Trigger.Add(new AiTriggerCountdown(ShakeTime, ShakeTick, ShakeEnd));
            var stateAttack = new AiState(UpdateAttack) { Init = InitAttack };
            // this trigger is used to abort the attack with a little delay so to not directly return
            stateAttack.Trigger.Add(_attackAbortTrigger = new AiTriggerCountdown(65, null, () => _aiComponent.ChangeState("attackReturn"), false));
            var stateAttackReturn = new AiState(UpdateAttackRevert) { Init = InitAttackReturn };
            var stateWait = new AiState();

            var stateEye0 = new AiState();
            stateEye0.Trigger.Add(new AiTriggerCountdown(1000, null, ToEye1));
            var stateEye1 = new AiState();
            stateEye1.Trigger.Add(new AiTriggerCountdown(400, null, ToEye2));
            var stateEye2 = new AiState();
            stateEye2.Trigger.Add(new AiTriggerCountdown(350, null, ToEye3));
            var stateEye3 = new AiState();
            stateEye3.Trigger.Add(new AiTriggerCountdown(1000, null, () => _aiComponent.ChangeState("walk")));

            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walk", stateWalk);
            _aiComponent.States.Add("run", stateRun);
            _aiComponent.States.Add("attackShake", stateShake);
            _aiComponent.States.Add("attack", stateAttack);
            _aiComponent.States.Add("attackReturn", stateAttackReturn);
            _aiComponent.States.Add("wait", stateWait);
            _aiComponent.States.Add("eye0", stateEye0);
            _aiComponent.States.Add("eye1", stateEye1);
            _aiComponent.States.Add("eye2", stateEye2);
            _aiComponent.States.Add("eye3", stateEye3);

            _aiDamageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, 0, false, false)
            {
                BossHitSound = true,
                HitMultiplierX = 0,
                HitMultiplierY = 0,
                ExplosionOffsetY = 8
            };
            _aiDamageState.AddBossDamageState(OnDeath);
            _aiComponent.ChangeState("idle");

            var damageCollider = new CBox(EntityPosition, -14, -14, 0, 28, 14, 8);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 4) { OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite));
            AddComponent(OcarinaListenerComponent.Index, new OcarinaListenerComponent(OnSongPlayed));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void OnKeyChange()
        {
            _bossState = Game1.GameManager.SaveManager.GetInt(_saveKey, 0);
        }

        private void OnSongPlayed(int songIndex)
        {
            // Played the Ocarina to force the eye open.
            if (songIndex != 1 && !_isDying)
            {
                // Force the Gohma's eye open.
                ToEye0();

                // As long as the Gohma heard the ocarina it passes.
                _usedOcarina = true;
            }
        }

        private void OnDamagedPlayer()
        {
            Game1.GameManager.SaveManager.SetString(_saveKey + "fail", "1");
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

                _animator.Play("stand");
                _animator.SpeedMultiplier = 1.0f;
                _aiComponent.ChangeState("idle");
                _aiDamageState.CurrentLives = _lives;
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

        private void ChangeState()
        {
            _animator.SpeedMultiplier = 1.0f;

            // 25% chance to start walking
            var changeState = Game1.RandomNumber.Next(0, 4) < 3 &&
                              MapManager.ObjLink.Position.Y < EntityPosition.Position.Y + 40;

            if (changeState)
            {
                // player is standing above the boss
                if (Game1.RandomNumber.Next(0, 2) == 0)
                    _aiComponent.ChangeState("attackShake");
                else
                    ToEye0();
            }
            else
            {
                _aiComponent.ChangeState("walk");
            }
        }

        private void UpdateIdle()
        {
            // start walking
            if (_playerInField)
            {
                _aiComponent.ChangeState("walk");
            }
        }

        private void ToEye0()
        {
            // stop walking
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("stand");
            _aiComponent.ChangeState("eye0");
        }

        private void ToEye1()
        {
            _animator.Play("eye");

            _aiComponent.ChangeState("eye1");
        }

        private void ToEye2()
        {
            // spawn a fireball
            var fireball = new EnemyFireball(Map, (int)EntityPosition.X, (int)EntityPosition.Y - 8, 1.25f, true);
            Map.Objects.SpawnObject(fireball);

            fireball.OnHitPlayer = () => Game1.GameManager.SaveManager.SetString(_saveKey + "fail", "1");

            _aiComponent.ChangeState("eye2");
        }

        private void ToEye3()
        {
            _animator.Play("stand");
            _aiComponent.ChangeState("eye3");
        }

        private void InitWalk()
        {
            var direction = -1 + Game1.RandomNumber.Next(0, 2) * 2;
            _body.VelocityTarget = new Vector2(direction, 0) * WalkSpeed;
            _animator.Play("walk");
        }

        private void InitRun()
        {
            var direction = -1 + Game1.RandomNumber.Next(0, 2) * 2;
            _body.VelocityTarget = new Vector2(direction, 0) * RunSpeed;
            _animator.Play("walk");
            _animator.SpeedMultiplier = 1.5f;
        }

        private void InitShake()
        {
            _body.VelocityTarget = Vector2.Zero;
        }

        private void ShakeTick(double counter)
        {
            // 5 frames to go left/right
            _animationComponent.SpriteOffset.X = MathF.Sin(MathF.PI * ((ShakeTime - (float)counter) / 1000 * (60 / 5f)));
            _animationComponent.UpdateSprite();
        }

        private void ShakeEnd()
        {
            _animationComponent.SpriteOffset.X = 0;

            // attack or start running depending on if the player is standing above the boss
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection.Y < 0)
                _aiComponent.ChangeState("run");
            else
                _aiComponent.ChangeState("attack");
        }

        private void InitAttack()
        {
            _attackStartPosition = EntityPosition.Position;
            // 45 if the top is the last one alive
            _attackTargetPosition = EntityPosition.Position + new Vector2(0, _bossState == 1 ? 45 : 25);

            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;

            var offset = 44;
            // make sure to not leave the room
            if (playerDirection.X < -22 && _body.FieldRectangle.Left <= EntityPosition.Position.X - BodyWidth / 2 - offset)
                _attackTargetPosition.X -= offset;
            if (playerDirection.X > 22 && EntityPosition.Position.X + BodyWidth / 2 + offset <= _body.FieldRectangle.Right)
                _attackTargetPosition.X += offset;
        }

        private void UpdateAttack()
        {
            var targetDirection = _attackTargetPosition - EntityPosition.Position;
            var offset = AttackSpeed * Game1.TimeMultiplier;

            if (targetDirection.Length() <= offset)
            {
                EntityPosition.Set(_attackTargetPosition);
                _aiComponent.ChangeState("attackReturn");
                _attackStartPosition.X = _attackTargetPosition.X;
            }
            else
            {
                // move towards the target position
                targetDirection.Normalize();
                EntityPosition.Move(targetDirection * AttackSpeed);
            }
        }

        private void InitAttackReturn()
        {
            Game1.AudioManager.PlaySoundEffect("D370-22-16");
        }

        private void UpdateAttackRevert()
        {
            var targetDirection = _attackStartPosition - EntityPosition.Position;
            var offset = AttackReturnSpeed * Game1.TimeMultiplier;

            if (targetDirection.Length() <= offset)
            {
                EntityPosition.Set(_attackStartPosition);
                _aiComponent.ChangeState("walk");
            }
            else
            {
                // move towards the target position
                targetDirection.Normalize();
                EntityPosition.Move(targetDirection * AttackReturnSpeed);
            }
        }

        private void OnDeath()
        {
            // Spawn a heart if not disabled.
            if (!GameSettings.NoHeartDrops)
                Map.Objects.SpawnObject(new ObjItem(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 16, "j", null, "heart", null));

            // Store that this Gohma was defeated.
            Game1.GameManager.SaveManager.SetInt(_saveKey, _bossState + 1);

            // Both Gohma were defeated.
            if (_bossState == 1)
            {
                // Store the death key and go back to dungeon music.
                Game1.GameManager.SaveManager.SetString(_saveKey, "1");
                Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);

                // See if the player failed the achievement.
                var failedAchievement = Game1.GameManager.SaveManager.GetString(_saveKey + "fail", "0") == "1";

                // Player must have not taken damage, used the bow, and played the ocarina at least once.
                if (!failedAchievement && _usedOcarina)
                    AchievementManager.Earn(67);

                // Remove the "failed" save key completely.
                Game1.GameManager.SaveManager.RemoveString(_saveKey + "fail");
            }
            // Remove this Gohma after death.
            Map.Objects.DeleteObjects.Add(this);
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            // change the direction if we collide with a wall
            _body.VelocityTarget.X = -_body.VelocityTarget.X;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            // abort the attack
            if (_aiComponent.CurrentStateId == "attack" && !_attackAbortTrigger.IsRunning())
            {
                _attackAbortTrigger.OnInit();
                _attackAbortTrigger.Start();
            }

            return true;
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // can only hit the boss with the hookshot or an arrow
            if ((hitType & (HitType.Hookshot | HitType.Bow | HitType.MagicRod | HitType.Boomerang)) == 0 ||
                (_aiComponent.CurrentStateId != "eye1" && _aiComponent.CurrentStateId != "eye2") ||
                _aiDamageState.IsInDamageState())
            {
                return Values.HitCollision.RepellingParticle;
            }
            _aiDamageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            if (hitType != HitType.Bow)
                Game1.GameManager.SaveManager.SetString(_saveKey + "fail", "1");

            if (_aiDamageState.CurrentLives <= 0)
            {
                _isDying = true;
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            return Values.HitCollision.Enemy;
        }
    }
}
