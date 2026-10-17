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

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    class MBossArmosKnight : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly AiTriggerSwitch _knockbackSwitch;
        private readonly AnimationComponent _animationComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly PushableComponent _pushComponent;
        private readonly HittableComponent _hitComponent;

        private string _saveKey;
        private int _jumpCount;
        private bool _hitRepelling = true;
        private int _lives = EnemyLives.ArmosKnight;

        private const int ShakeTime = 500;
        private const float WalkSpeed = 0.6f;
        private const int AttackUpTime = 400;

        public MBossArmosKnight() : base("armos knight") { }

        public MBossArmosKnight(Map.Map map, int posX, int posY, string saveKey) : base(map)
        {
            EntityPosition = new CPosition(posX + 16, posY + 32, 0);
            EntitySize = new Rectangle(-16, -32, 32, 32);

            // check if the boss was already killed
            _saveKey = saveKey;
            if (!string.IsNullOrEmpty(_saveKey) && Game1.GameManager.SaveManager.GetString(_saveKey) == "1")
            {
                // we need to make sure to spawn the key because the player could walk out after killing the boss without collecting the key
                SpawnKey();
                IsDead = true;
                return;
            }

            _animator = AnimatorSaveLoad.LoadAnimator("MidBoss/armosKnight");

            var sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_animator, sprite, new Vector2(0, -32));

            _body = new BodyComponent(EntityPosition, -14, -20, 28, 20, 8)
            {
                IgnoreHoles = true,
                Gravity = -0.15f,
                DragAir = 0.875f,
                MaxJumpHeight = 8
            };

            _aiComponent = new AiComponent();
            _aiComponent.Trigger.Add(_knockbackSwitch = new AiTriggerSwitch(400));

            var stateIdle = new AiState(UpdateIdle) { Init = InitIdle };
            var stateAwake = new AiState(UpdateAwake) { Init = InitAwake };
            var stateShake = new AiState();
            stateShake.Trigger.Add(new AiTriggerCountdown(ShakeTime, ShakeTick, ShakeEnd));
            var stateWalk = new AiState(UpdateWalk) { Init = InitWalk };
            var stateJump = new AiState();
            var stateAttackUp = new AiState { Init = InitAttackUp };
            stateAttackUp.Trigger.Add(new AiTriggerCountdown(AttackUpTime, AttackUpTick, AttackUpEnd));
            var stateAttackWait = new AiState();
            stateAttackWait.Trigger.Add(new AiTriggerCountdown(200, null, () => _aiComponent.ChangeState("attack")));
            var stateAttack = new AiState(UpdateAttack) { Init = InitAttack };
            var stateAttackFinished = new AiState();
            stateAttackFinished.Trigger.Add(new AiTriggerCountdown(850, null, () => _aiComponent.ChangeState("walk")));

            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("awake", stateAwake);
            _aiComponent.States.Add("shake", stateShake);
            _aiComponent.States.Add("walk", stateWalk);
            _aiComponent.States.Add("jump", stateJump);
            _aiComponent.States.Add("attackUp", stateAttackUp);
            _aiComponent.States.Add("attackWait", stateAttackWait);
            _aiComponent.States.Add("attack", stateAttack);
            _aiComponent.States.Add("attackFinished", stateAttackFinished);
            _aiDamageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, 0, false) { BossHitSound = true };
            _aiDamageState.AddBossDamageState(RemoveObject);

            _aiComponent.ChangeState("idle");

            var damageCollider = new CBox(EntityPosition, -14, -24, 0, 28, 24, 8);
            var hittableBox = new CBox(EntityPosition, -13, -24, 0, 26, 22, 8);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 6));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 24, ShadowHeight = 6 });

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowl");
        }

        private void InitIdle()
        {
            _animator.Play("idle");
        }

        private void UpdateIdle()
        {
            // awake if the player is close enough
            var distance = MapManager.ObjLink.Position - EntityPosition.Position;
            if (distance.Length() < 42)
                _aiComponent.ChangeState("awake");
        }

        private void InitAwake()
        {
            _animator.Play("red");
            Game1.AudioManager.SetMusicFadeTransition(79, 2, 350);
        }

        private void UpdateAwake()
        {
            if (!_animator.IsPlaying)
            {
                _aiComponent.ChangeState("shake");
                _hitRepelling = false;
            }
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

            _aiComponent.ChangeState("walk");
        }

        private void InitWalk()
        {
            _jumpCount = 0;
        }

        private void UpdateWalk()
        {
            if (_body.IsGrounded && _aiDamageState.CurrentLives > 0)
            {
                var distance = MapManager.ObjLink.Position - EntityPosition.Position;

                if (_jumpCount >= 4)
                {
                    _aiComponent.ChangeState("attackUp");
                }
                else
                {
                    if (distance != Vector2.Zero)
                    {
                        _jumpCount++;
                        distance.Normalize();

                        _body.Velocity.Z += 1.125f;
                        _body.VelocityTarget = distance * WalkSpeed;

                        Game1.AudioManager.PlaySoundEffect("D360-32-20");
                    }
                }
            }
        }

        private void InitAttackUp()
        {
            _body.IsGrounded = false;
            _body.IgnoresZ = true;
            _body.VelocityTarget = Vector2.Zero;

            Game1.AudioManager.PlaySoundEffect("D360-36-24");

            var distance = MapManager.ObjLink.Position - EntityPosition.Position;
            if (distance != Vector2.Zero)
            {
                distance.Normalize();
                _body.Velocity = new Vector3(distance * 1f, 0);
            }
        }

        private void AttackUpTick(double time)
        {
            EntityPosition.Z = MathF.Sin((float)((AttackUpTime - time) / AttackUpTime) * MathF.PI * 0.5f) * 38;
        }

        private void AttackUpEnd()
        {
            _aiComponent.ChangeState("attackWait");
        }

        private void InitAttack()
        {
            // start falling down
            _body.IgnoresZ = false;
            _body.JumpStartHeight = 0;
        }

        private void UpdateAttack()
        {
            if (_body.IsGrounded)
            {
                MapManager.ObjLink.GroundStun(1000);
                if (GameSettings.ScreenShake)
                    Game1.GameManager.ShakeScreen(500, 0.00f, 2.50f, 0.00f, 10.00f);
                Game1.AudioManager.PlaySoundEffect("D360-11-0B");

                _aiComponent.ChangeState("attackFinished");
            }
        }

        private void InitAngry()
        {
            if (_animator.CurrentAnimation.Id == "angry")
                return;

            Game1.AudioManager.PlaySoundEffect("D378-41-29");
            SpawnStones();

            _animator.Play("angry");
        }

        private void InitBroke()
        {
            if (_animator.CurrentAnimation.Id == "broken")
                return;

            Game1.AudioManager.PlaySoundEffect("D378-41-29");
            SpawnStones();

            _animator.Play("broken");
        }

        private void SpawnStones()
        {
            var randomOffset0 = Game1.RandomNumber.Next(90, 110) / 100f;
            var randomOffset1 = Game1.RandomNumber.Next(90, 110) / 100f;
            var randomOffset2 = Game1.RandomNumber.Next(90, 110) / 100f;
            var randomOffset3 = Game1.RandomNumber.Next(90, 110) / 100f;

            var stone0 = new ObjSmallStone(Map, (int)EntityPosition.X - 3, (int)EntityPosition.Y, (int)EntityPosition.Z + 26, new Vector3(-0.25f, 0.25f * 1, 0.85f) * randomOffset0, true);
            var stone1 = new ObjSmallStone(Map, (int)EntityPosition.X - 4, (int)EntityPosition.Y + 8, (int)EntityPosition.Z + 26, new Vector3(-0.35f, 0.25f * 1, 0.85f) * randomOffset1, true);
            var stone2 = new ObjSmallStone(Map, (int)EntityPosition.X + 3, (int)EntityPosition.Y, (int)EntityPosition.Z + 26, new Vector3(0.25f, 0.25f * 1, 0.85f) * randomOffset2, true);
            var stone3 = new ObjSmallStone(Map, (int)EntityPosition.X + 4, (int)EntityPosition.Y + 8, (int)EntityPosition.Z + 26, new Vector3(0.35f, 0.25f * 1, 0.85f) * randomOffset3, true);

            Map.Objects.SpawnObject(stone0);
            Map.Objects.SpawnObject(stone1);
            Map.Objects.SpawnObject(stone2);
            Map.Objects.SpawnObject(stone3);
        }

        private void RemoveObject()
        {
            if (!string.IsNullOrEmpty(_saveKey))
                Game1.GameManager.SaveManager.SetString(_saveKey, "1");

            SpawnKey();

            // stop boss music
            Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);

            Map.Objects.DeleteObjects.Add(this);
        }

        private void SpawnKey()
        {
            var objItem = new ObjItem(Map, 0, 0, "j", "dkey4Collected", "dkey4", null);
            if (!objItem.IsDead)
            {
                objItem.EntityPosition.Set(new Vector2(EntityPosition.X, EntityPosition.Y - 2));
                Map.Objects.SpawnObject(objItem);
            }
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (!_hitRepelling && type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X, direction.Y, _body.Velocity.Z);

            return true;
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_aiDamageState.CurrentLives <= 0 || _aiDamageState.IsInDamageState())
                return Values.HitCollision.None;

            // Knock the boss back.
            if (!_hitRepelling && _knockbackSwitch.State)
            {
                _knockbackSwitch.Reset();
                _body.VelocityTarget = Vector2.Zero;
                _body.Velocity.X = direction.X * 3.0f;
                _body.Velocity.Y = direction.Y * 3.0f;
            }
            if (_hitRepelling)
                return Values.HitCollision.RepellingParticle;

            // In the original game, he could be damaged by the bow, a sword spin, or when dashing with pegasus boots.
            if ((hitType & HitType.Sword2) != 0 || (hitType & HitType.SwordSpin) != 0 || (hitType & HitType.Bow) != 0 || (hitType & HitType.PegasusBootsSword) != 0 || (hitType & HitType.MagicRod) != 0)
            {
                // Boss should start jumping directly after getting hit.
                _jumpCount = 0;

                // Apply the hit now so that the lives checks below match.
                var result = _aiDamageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

                // Now CurrentLives reflects this hit. Update the animation to match.
                if (_aiDamageState.CurrentLives <= 0)
                {
                    _body.Velocity = Vector3.Zero;
                    _body.VelocityTarget = Vector2.Zero;
                    _damageField.IsActive = false;
                    _hitComponent.IsActive = false;
                    _pushComponent.IsActive = false;
                }
                // Change the animation to reflect the health of the boss.
                if (_aiDamageState.CurrentLives <= 3)
                    InitBroke();
                else if (_aiDamageState.CurrentLives <= 7)
                    InitAngry();

                // Repelling for these weapons would make it "ting" so use blocking.
                var hitCollision = hitType == HitType.Bow || hitType == HitType.MagicRod
                    ? Values.HitCollision.Blocking
                    : Values.HitCollision.Repelling;

                return result | hitCollision;
            }
            return Values.HitCollision.None;
        }
    }
}
