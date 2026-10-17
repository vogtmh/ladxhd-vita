using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    class MBossStoneHinox : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly AnimationComponent _animationComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly PushableComponent _pushComponent;
        private readonly HittableComponent _hitComponent;

        private readonly Vector2 _spawnPosition;
        private readonly string _saveKey;

        private Vector2[] _walkOffset = { new Vector2(-24, 0), new Vector2(24, 0), new Vector2(0, 24) };
        private Vector2 _targetPosition;

        private int _lives = EnemyLives.StoneHinox;
        private int _lastWalkFrame;
        private int _jumpCount = 0;
        private bool _wasHit;

        public BodyComponent Body => _body;

        public List<MBossStoneHinoxStone> HinoxStones = new List<MBossStoneHinoxStone>();

        public MBossStoneHinox() : base("stone hinox") { }

        public MBossStoneHinox(Map.Map map, int posX, int posY, string saveKey) : base(map)
        {
            EntityPosition = new CPosition(posX + 16, posY + 32, 0);
            EntitySize = new Rectangle(-16, -32, 32, 32);

            _saveKey = saveKey;

            // was already killed?
            if (!string.IsNullOrEmpty(_saveKey) &&
                Game1.GameManager.SaveManager.GetString(_saveKey) == "1")
            {
                IsDead = true;
                return;
            }

            _spawnPosition = EntityPosition.Position;

            _animator = AnimatorSaveLoad.LoadAnimator("MidBoss/stone hinox");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -14, -20, 28, 20, 8)
            {
                Gravity = -0.1f,
                FieldRectangle = Map.GetField(posX, posY)
            };

            _aiComponent = new AiComponent();

            var stateIdle = new AiState(UpdateIdle);
            var stateIdleDelay = new AiState(UpdateIdleDelay);
            var stateWalk = new AiState(UpdateWalk) { Init = InitWalking };
            var stateJump = new AiState(UpdateJump) { Init = InitJump };
            var stateHitFloor = new AiState { Init = InitHitFloor };
            stateHitFloor.Trigger.Add(new AiTriggerCountdown(500, null, EndHitFloor));
            var statePostAttack = new AiState { Init = InitPostAttack };
            statePostAttack.Trigger.Add(new AiTriggerCountdown(400, null, EndPostAttack));
            var statePostHit = new AiState { Init = InitPostHit };
            statePostHit.Trigger.Add(new AiTriggerCountdown(300, null, EndPostHit));

            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("idleDelay", stateIdleDelay);
            _aiComponent.States.Add("walk", stateWalk);
            _aiComponent.States.Add("jump", stateJump);
            _aiComponent.States.Add("attack", stateHitFloor);
            _aiComponent.States.Add("postAttack", statePostAttack);
            _aiComponent.States.Add("postHit", statePostHit);
            _aiDamageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, 0, false, false)
            {
                HitMultiplierX = 0,
                HitMultiplierY = 0,
                BossHitSound = true
            };
            _aiDamageState.DamageSpriteShader = Resources.DamageSpriteShader1;
            _aiDamageState.AddBossDamageState(OnDeathAnimationEnd);

            _aiComponent.ChangeState("idle");

            var damageCollider = new CBox(EntityPosition, -14, -24, 0, 28, 24, 8);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 4));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit) { IsActive = false, ArrowMultiplier = true, BombMultiplier = true, MagicRodMultiplier = true });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 16, ShadowHeight = 6 });

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowl");
        }

        private void UpdateIdle()
        {
            if (_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
            {
                Game1.GameManager.StartDialogPath("stone_hinox");
                _aiComponent.ChangeState("idleDelay");
                _hitComponent.IsActive = true;
            }
        }

        private void UpdateIdleDelay()
        {
            if (!Game1.GameManager.DialogIsRunning())
                _aiComponent.ChangeState("jump");
        }

        private void InitJump()
        {
            _body.IsGrounded = false;
            _body.Velocity.Z = 2;
            _animator.Pause();
        }

        private void UpdateJump()
        {
            // jump towards the spawn position
            var spawnDirection = _spawnPosition - EntityPosition.Position;
            if (spawnDirection.Length() > 0.75f * Game1.TimeMultiplier)
            {
                spawnDirection.Normalize();
                _body.VelocityTarget = 0.75f * spawnDirection;
            }
            else
            {
                _body.VelocityTarget = Vector2.Zero;
                EntityPosition.Set(_spawnPosition);
            }

            // end jump
            if (_body.IsGrounded)
            {
                _body.VelocityTarget = Vector2.Zero;
                EntityPosition.Set(_spawnPosition);

                _aiComponent.ChangeState("attack");

                Game1.AudioManager.PlaySoundEffect("D360-11-0B");
                // shake the screen
                if (GameSettings.ScreenShake)
                    Game1.GameManager.ShakeScreen(800, 0.00f, 2.00f, 0.00f, 9.5f);

                MapManager.ObjLink.GroundStun(800);

                // spawn stones
                SpawnStone();
                SpawnStone();
            }
        }

        private void InitHitFloor()
        {
            _animationComponent.MirroredH = _animator.CurrentFrameIndex == 0;
            _animator.Play("attack");
        }

        private void EndHitFloor()
        {
            SpawnStone();
            _aiComponent.ChangeState("postAttack");
        }

        private void SpawnStone()
        {
            // Don't spawn new stones if the boss is already dead.
            if (_aiDamageState.CurrentLives <= 0)
                return;

            // Spawn a stone at a random position.
            var position = new Vector3(_spawnPosition.X + Game1.RandomNumber.Next(0, 120) - 60, _spawnPosition.Y - 8 - Game1.RandomNumber.Next(0, 8), 16);
            var direction = new Vector3(0, 1, 1);
            var centerX = (int)_spawnPosition.X;
            var objStone = new MBossStoneHinoxStone(Map, this, position, direction, centerX);
            Map.Objects.SpawnObject(objStone);
            HinoxStones.Add(objStone);
        }

        private void DestroyStones()
        {
            // Loop through the collection of stones and destroy them all.
            foreach (var stone in HinoxStones.ToArray())
                stone.DestroyStone();
        }

        private void InitPostAttack()
        {
            _animator.Play("fast");
            _animator.SetFrame(_animationComponent.MirroredH ? 1 : 0);
            _animationComponent.MirroredH = false;
        }

        private void EndPostAttack()
        {
            if (_wasHit)
            {
                _wasHit = false;
                _aiComponent.ChangeState("jump");
            }
            else
            {
                var newState = Game1.RandomNumber.Next(0, 2);

                var state = (newState != 0 && _jumpCount < 2) 
                    ? (_jumpCount++, "jump") 
                    : (_jumpCount = 0, "walk");

                _aiComponent.ChangeState(state.Item2);
            }
        }

        private void InitPostHit()
        {
            _animator.Pause();
        }

        private void EndPostHit()
        {
            _aiComponent.ChangeState("postAttack");
        }

        private void InitWalking()
        {
            var frameIndex = _animator.CurrentFrameIndex;
            _animator.Play("idle");
            _animator.SetFrame(frameIndex);
            _lastWalkFrame = frameIndex;

            // walk into a random direction if we are at the spawn position
            if (EntityPosition.Position == _spawnPosition)
            {
                var randomDirection = Game1.RandomNumber.Next(0, 3);
                _targetPosition = _spawnPosition + _walkOffset[randomDirection];
            }
            else
            {
                _targetPosition = _spawnPosition;
            }
        }

        private void UpdateWalk()
        {
            // only move when a new frame from the animation is shown
            if (_lastWalkFrame == _animator.CurrentFrameIndex)
                return;
            _lastWalkFrame = _animator.CurrentFrameIndex;

            // finished walking?
            if (EntityPosition.Position == _targetPosition)
            {
                var newState = Game1.RandomNumber.Next(0, 2);
                _aiComponent.ChangeState(newState == 0 ? "walk" : "jump");
            }

            // walk towards the target position
            var newPosition = EntityPosition.Position;
            if (EntityPosition.X != _targetPosition.X)
                newPosition.X += 4 * Math.Sign(_targetPosition.X - EntityPosition.X);
            if (EntityPosition.Y != _targetPosition.Y)
                newPosition.Y += 4 * Math.Sign(_targetPosition.Y - EntityPosition.Y);
            EntityPosition.Set(newPosition);
        }

        private void OnDeathAnimationEnd()
        {
            if (!string.IsNullOrEmpty(_saveKey))
                Game1.GameManager.SaveManager.SetString(_saveKey, "1");

            // stop boss music
            Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);

            // spawns a fairy
            Game1.AudioManager.PlaySoundEffect("D360-27-1B");
            Map.Objects.SpawnObject(new ObjDungeonFairy(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 8));

            Map.Objects.DeleteObjects.Add(this);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            return true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_aiComponent.CurrentStateId == "idle")
                return Values.HitCollision.None;

            if (_aiComponent.CurrentStateId == "walk" && _aiDamageState.CurrentLives > damage)
            {
                _aiComponent.ChangeState("postHit");
                _animator.Pause();
                _wasHit = true;
            }
            var hitCollision = _aiDamageState.OnHit(gameObject, direction, hitType, damage, false);

            if (_aiDamageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _pushComponent.IsActive = false;
                _hitComponent.IsActive = false;
                _body.VelocityTarget = Vector2.Zero;
                _animator.Pause();
                DestroyStones();
            }

            if (hitCollision != Values.HitCollision.None)
                return hitCollision | Values.HitCollision.Repelling | Values.HitCollision.Repelling0;

            return hitCollision;
        }
    }
}
