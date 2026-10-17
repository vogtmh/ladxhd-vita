using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
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
    class MBossMasterStalfos : GameObject, IHasVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly AiTriggerSwitch _damageCooldown;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;
        private readonly ShadowBodyDrawComponent _shadowComponent;

        private AiTriggerCountdown _damagedCountdown;
        private AiTriggerCountdown _wobbleCountdown;
        private AiTriggerCountdown _standUpCountdown;
        private CPosition[] _positions = new CPosition[5];
        private CSprite[] _sprites = new CSprite[5];
        private SheetAnimator _animator;

        private readonly string _saveKey;
        private readonly int _encounterNumber;

        private const float _swordShotStunTime = 150f;
        private const float _moveSpeed = 0.5f;
        private const int _wobbleTime = 750;
        private const int _fleeTime = 400;

        private float[] _damageTime = { 0, 175, 75, 0 };
        private float[] _damageCounters = new float[4];
        private float[] _partVelocity = new float[4];
        private float[] _partOffset = { 0, 0, 3, 3 };
        private float[] _standCounters = new float[4];

        private int[] _drawOrder = { 0, 1, 2, 4, 3 };

        private string _encounterState;
        private float _partGravity = 0.15f;
        private float _transparency;
        private bool _shownIntroText;
        private bool _damageState;
        private bool _flee;
        private bool _attackSound;
        private bool _swordShotHit;
        private bool _vulnerable;
        private int _direction;

        // Lives are how many bombs it takes to kill. This is a separate pool from "real lives" that is decremented whenever
        // damage from a bomb, bomb arrow, or sword shot is taken. Lives for fights 1-4 are 3-2-2-3 lives.
        private int _lives = EnemyLives.MStalfos;
        private int _livesMid = EnemyLives.MStalfosMid;

        // "Real" lives are simulated lives to replicate the behavior of the original game when using sword shots or bomb
        // arrows. When real lives reach 0, the boss will experience a "real death". Real hits also trigger "bomb hits" in
        // the original game, so use a separate count "_realHits" that triggers the flee. 
        private int _realLives;
        private int _realHits;

        public bool IsVisible { get; internal set; }

        public MBossMasterStalfos() : base("ms_shield") { }

        public MBossMasterStalfos(Map.Map map, int posX, int posY, string saveKey, int encounterNumber) : base(map)
        {
            IsVisible = false;
            EntityPosition = new CPosition(posX + 16, posY + 32, 100);
            EntitySize = new Rectangle(-24, -38, 48, 38);

            _saveKey = saveKey;
            _encounterNumber = encounterNumber;

            _animator = new SheetAnimator();

            var aniStandLeft = new SheetAnimation("stand-1", 0,
                new AFrame(16, new ASprite(-15, -16), new ASprite(-14, -32, false, true), new ASprite(-24, -24), new ASprite(8, -37), null));
            var aniStandRight = new SheetAnimation("stand1", 0,
                new AFrame(16, new ASprite(-15, -16), new ASprite(-13, -32), new ASprite(-16, -24), new ASprite(8, -40), null));
            var aniPreJumpLeft = new SheetAnimation("preJump-1", 0,
                new AFrame(16, new ASprite(-15, -16), new ASprite(-14, -30, false, true), new ASprite(-24, -22), new ASprite(8, -35), null));
            var aniPreJumpRight = new SheetAnimation("preJump1", 0,
                new AFrame(16, new ASprite(-15, -16), new ASprite(-13, -30), new ASprite(-16, -22), new ASprite(8, -38), null));
            var aniWalkLeft = new SheetAnimation("walk-1", -1,
                new AFrame(16, new ASprite(-15, -16), new ASprite(-14, -32, false, true), new ASprite(-24, -24), new ASprite(8, -37), null),
                new AFrame(16, new ASprite(-15, -16, false, true), new ASprite(-14, -32, false, true), new ASprite(-24, -24), new ASprite(8, -37), null));
            var aniWalkRight = new SheetAnimation("walk1", -1,
                new AFrame(16, new ASprite(-15, -16), new ASprite(-13, -32), new ASprite(-16, -24), new ASprite(8, -40), null),
                new AFrame(16, new ASprite(-15, -16, false, true), new ASprite(-13, -32), new ASprite(-16, -24), new ASprite(8, -40), null));
            var aniHitLeft = new SheetAnimation("hit-1", 0,
                new AFrame(24, new ASprite(-15, -16, false, true), new ASprite(-14, -32, false, true), new ASprite(-24, -24), new ASprite(-16, -48), null),
                new AFrame(8, new ASprite(-15, -16, false, true), new ASprite(-14, -32), new ASprite(-24, -32), null, new ASprite(-24, -24)),
                new AFrame(8, new ASprite(-15, -16, false, true), new ASprite(-14, -32), new ASprite(-24, -36), new ASprite(8, -8, true), new ASprite(-24, -24)),
                new AFrame(40, new ASprite(-15, -16, false, true), new ASprite(-14, -32), new ASprite(-24, -36), new ASprite(8, -8, true), null));
            var aniHitRight = new SheetAnimation("hit1", 0,
                new AFrame(24, new ASprite(-15, -16), new ASprite(-13, -32), new ASprite(-16, -24), new ASprite(1, -56, false, true), null),
                new AFrame(8, new ASprite(-15, -16), new ASprite(-14, -32, false, true), new ASprite(-24, -28), null, new ASprite(-8, -24, false, true)),
                new AFrame(8, new ASprite(-15, -16), new ASprite(-14, -32, false, true), new ASprite(-24, -32), new ASprite(-15, -8, true, true), new ASprite(-8, -24, false, true)),
                new AFrame(40, new ASprite(-15, -16), new ASprite(-14, -32, false, true), new ASprite(-24, -32), new ASprite(-15, -8, true, true), null));

            _animator.Animations.Add(aniStandLeft);
            _animator.Animations.Add(aniStandRight);
            _animator.Animations.Add(aniPreJumpLeft);
            _animator.Animations.Add(aniPreJumpRight);
            _animator.Animations.Add(aniWalkLeft);
            _animator.Animations.Add(aniWalkRight);
            _animator.Animations.Add(aniHitLeft);
            _animator.Animations.Add(aniHitRight);

            _animator.Play("standLeft");

            for (var i = 0; i < _positions.Length; i++)
                _positions[i] = new CPosition(0, 0, 0);

            // get all the sprites for the boss
            _sprites[0] = new CSprite("ms_feet", _positions[0], Vector2.Zero);
            _sprites[1] = new CSprite("ms_head", _positions[1], Vector2.Zero);
            _sprites[2] = new CSprite("ms_shield", _positions[2], Vector2.Zero);
            _sprites[3] = new CSprite("ms_sword", _positions[3], Vector2.Zero);
            _sprites[4] = new CSprite("ms_swing", _positions[4], Vector2.Zero);

            var animationComponent = new AnimationSheetComponent(_animator);

            _body = new BodyComponent(EntityPosition, -14, -16, 28, 16, 8)
            {
                FieldRectangle = Map.GetField(posX, posY),
                FieldLocked = false,
                IgnoreHoles = true,
                Gravity = -0.125f,
                IsActive = false
            };

            _aiComponent = new AiComponent();
            _aiComponent.Trigger.Add(_damageCooldown = new AiTriggerSwitch(500));

            var stateHidden = new AiState(UpdateHidden);
            var statePreFall = new AiState();
            statePreFall.Trigger.Add(new AiTriggerCountdown(500, null, () => _aiComponent.ChangeState("fall")));
            var stateFall = new AiState(UpdateFall) { Init = InitFall };
            var statePostFall = new AiState();
            statePostFall.Trigger.Add(new AiTriggerCountdown(500, null, () => _aiComponent.ChangeState("idle")));
            var stateIdle = new AiState { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerCountdown(350, null, EndIdle));
            var stateWalk = new AiState(UpdateWalking) { Init = InitWalk };
            stateWalk.Trigger.Add(new AiTriggerCountdown(1000, null, EndWalking));
            var statePreDamaged = new AiState(UpdatePreDamaged) { Init = InitPreDamageState };
            var stateDamaged = new AiState(UpdateDamaged) { Init = InitDamageState };
            stateDamaged.Trigger.Add(_damagedCountdown = new AiTriggerCountdown(3000, null, () => _aiComponent.ChangeState("wobble")));
            var stateWobble = new AiState();
            stateWobble.Trigger.Add(_wobbleCountdown = new AiTriggerCountdown(_wobbleTime, WobbleTick, WobbleEnd));
            var stateStandingUp = new AiState { Init = InitStandingUp };
            stateStandingUp.Trigger.Add(_standUpCountdown = new AiTriggerCountdown(750, StandUpTick, StandUpEnd));
            var statePreJump = new AiState { Init = InitPreJump };
            statePreJump.Trigger.Add(new AiTriggerCountdown(500, null, () => _aiComponent.ChangeState("jump")));
            var stateJump = new AiState(UpdateJump) { Init = InitJump };
            var statePostJump = new AiState { Init = InitPostJump };
            statePostJump.Trigger.Add(new AiTriggerCountdown(300, null, () => _aiComponent.ChangeState("idle")));
            var stateAttack = new AiState(UpdateAttack) { Init = InitAttack };
            var statePreFlee = new AiState { Init = InitPreJump };
            statePreFlee.Trigger.Add(new AiTriggerCountdown(250, null, () => _aiComponent.ChangeState("flee")));
            var stateFlee = new AiState { Init = InitFlee };
            stateFlee.Trigger.Add(new AiTriggerCountdown(_fleeTime, FleeTick, FleeEnd));

            _aiComponent.States.Add("hidden", stateHidden);
            _aiComponent.States.Add("preFall", statePreFall);
            _aiComponent.States.Add("fall", stateFall);
            _aiComponent.States.Add("postFall", statePostFall);
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walk", stateWalk);
            _aiComponent.States.Add("preDamaged", statePreDamaged);
            _aiComponent.States.Add("damaged", stateDamaged);
            _aiComponent.States.Add("wobble", stateWobble);
            _aiComponent.States.Add("standUp", stateStandingUp);
            _aiComponent.States.Add("preJump", statePreJump);
            _aiComponent.States.Add("jump", stateJump);
            _aiComponent.States.Add("postJump", statePostJump);
            _aiComponent.States.Add("attack", stateAttack);
            _aiComponent.States.Add("preFlee", statePreFlee);
            _aiComponent.States.Add("flee", stateFlee);

            // dummy sprite
            _sprite = new CSprite(EntityPosition);

            // Four encounters: less lives on middle encounters, more lives on first and last encounters.
            int lives = (_encounterNumber == 1 || _encounterNumber == 2) ? _livesMid : _lives;

            // For the final encounter the sword shots taken are slightly lower.
            // This replicates a bug in the original game where sword shot can deal damage.
            _realLives = (_encounterNumber == 3) ? 192 : 256;

            _aiDamageState = new AiDamageState(this, _body, _aiComponent, _sprite, lives, 0, false, false)
            { BossHitSound = true, HitMultiplierX = 3, HitMultiplierY = 3 };

            // Do normal "OnDeath" unless it's the final fight.
            if (_encounterNumber != 3)
                _aiDamageState.OnDeath = OnDeath;
            else
                _aiDamageState.AddBossDamageState(OnBossDeath);

            // If it's the second encounter, start tracking whether or not damage was taken.
            if (_encounterNumber == 1)
                Game1.GameManager.SaveManager.SetString("mstalfos_achievement","1");

            _aiDamageState.ExplosionOffsetY = 8;

            _aiComponent.ChangeState("hidden");

            var damageCollider = new CBox(EntityPosition, -14, -24, 0, 28, 24, 8);
            var hittableBox = new CBox(EntityPosition, -12, -26, 0, 24, 24, 8);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 4));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(damageCollider, OnPush));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, _shadowComponent = new ShadowBodyDrawComponent(EntityPosition) { IsActive = false, ShadowWidth = 22, ShadowHeight = 6 });

            if (!string.IsNullOrEmpty(_saveKey))
            {
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));

                // spawn the hookshot if the player did not collect it after 
                var encounterState = Game1.GameManager.SaveManager.GetString(_saveKey);
                if (encounterState == "4" && _encounterNumber == 3)
                    SpawnHookshot();
            }
            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowl");
        }

        private void OnKeyChange()
        {
            _encounterState = Game1.GameManager.SaveManager.GetString(_saveKey);
            if (_encounterState == null)
                _encounterState = "0";
        }

        private void InitFlee()
        {
            Game1.AudioManager.PlaySoundEffect("D378-62-3F");

            _body.IsActive = false;
            _damageField.IsActive = false;
            _animator.Play("stand" + _direction);

            // stop the music
            Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);
        }

        private void FleeTick(double counter)
        {
            var state = (float)(_fleeTime - counter) / _fleeTime;
            EntityPosition.Z = MathF.Sin(state * MathF.PI * 0.4f) * 80;
            _transparency = MathF.Min((1 - state) / 0.25f, 1);
            _shadowComponent.Transparency = 1 - state;
        }

        private void FleeEnd()
        {
            if (!string.IsNullOrEmpty(_saveKey))
                Game1.GameManager.SaveManager.SetString(_saveKey, (_encounterNumber + 1).ToString());

            Map.Objects.DeleteObjects.Add(this);
            IsVisible = false;
        }

        private void UpdateHidden()
        {
            // player entered the room? => fall down
            if (_encounterState == _encounterNumber.ToString() &&
                _body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
            {
                Game1.AudioManager.SetMusicFadeTransition(79, 2, 350);
                _aiComponent.ChangeState("preFall");
            }
        }

        private void InitFall()
        {
            Game1.AudioManager.PlaySoundEffect("D360-08-08");

            _animator.Play("stand1");
            _shadowComponent.IsActive = true;
            _body.IsActive = true;
            IsVisible = true;
        }

        private void UpdateFall()
        {
            _transparency = MathF.Min((100 - EntityPosition.Z) / 10f, 1);

            if (_body.IsGrounded)
            {
                _animator.Play("preJump1");
                _aiComponent.ChangeState("postFall");
            }
        }

        private void InitAttack()
        {
            _attackSound = false;
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("hit" + _direction);
        }

        private void UpdateAttack()
        {
            if (_animator.CurrentFrameIndex == 1 || _animator.CurrentFrameIndex == 2)
            {
                if (!_attackSound)
                {
                    _attackSound = true;
                    Game1.AudioManager.PlaySoundEffect("D378-39-27");
                }

                // attack the player
                var damageBox = new Box(EntityPosition.X - 16 + (_direction == -1 ? -6 : 6), EntityPosition.Y - 22, 0, 32, 38, 8);
                if (damageBox.Intersects(MapManager.ObjLink.Body.BodyBox.Box))
                {
                    var direction = MapManager.ObjLink.Position - EntityPosition.Position;
                    if (direction != Vector2.Zero)
                        direction.Normalize();

                    MapManager.ObjLink.HitPlayer(direction * 2.5f, HitType.Boss, 4, false);
                }
            }

            // finished attacking
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("idle");
        }

        private void InitIdle()
        {
            _body.VelocityTarget = Vector2.Zero;
            // look at the player
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            _direction = playerDirection.X < 0 ? -1 : 1;
            _animator.Play("stand" + _direction);
            _vulnerable = true;
        }

        private void EndIdle()
        {
            // When he stands up, real hits should be able to end the fight if the number of hits is
            // greater than lives remaining. Simulates a "shared pool" between the two lives systems.
            if (_aiDamageState.CurrentLives - _realHits <= 0 && _encounterNumber != 3)
                OnDeath(false);

            // Whether it was bomb death or if it was triggered above, boss will flee.
            if (_flee)
            {
                Game1.GameManager.StartDialogPath("master_stalfos_0");
                _aiComponent.ChangeState("preFlee");
                return;
            }

            if (_encounterNumber > 0 && !_shownIntroText)
            {
                _shownIntroText = true;
                Game1.GameManager.StartDialogPath(_encounterNumber == 3 ? "master_stalfos_2" : "master_stalfos_1");
            }

            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;

            // player is standing in front of the boss? => attack
            if (Math.Abs(playerDirection.X) < 24 && 0 < playerDirection.Y && playerDirection.Y < 18)
                _aiComponent.ChangeState("attack");
            else if (Math.Abs(playerDirection.X) < 28 && 0 < playerDirection.Y && playerDirection.Y < 48)
                _aiComponent.ChangeState("walk");
            else
                _aiComponent.ChangeState("preJump");

            _vulnerable = false;
        }

        private void InitPreJump()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            _direction = playerDirection.X < 0 ? -1 : 1;
            _animator.Play("preJump" + _direction);
        }

        private void InitJump()
        {
            Game1.AudioManager.PlaySoundEffect("D360-36-24");

            _animator.Play("stand" + _direction);

            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection != Vector2.Zero)
            {
                // try to jump at a spot where the player can be easily hit
                if (playerDirection.Y < 0)
                    playerDirection.Y *= 2;

                playerDirection.Normalize();
                _body.VelocityTarget = playerDirection * 1.5f;
                _body.Velocity.Z = 3;
            }
        }

        private void UpdateJump()
        {
            // reached the ground
            if (_body.IsGrounded && _body.Velocity.Z <= 0)
            {
                _aiComponent.ChangeState("postJump");
                _body.VelocityTarget = Vector2.Zero;
            }
        }

        private void InitPostJump()
        {
            _animator.Play("preJump" + _direction);
        }

        private void InitWalk()
        {
            // look at the player while moving
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            _direction = playerDirection.X < 0 ? -1 : 1;
            _animator.Play("walk" + _direction);

            // move towards the player
            if (playerDirection != Vector2.Zero)
            {
                playerDirection.Normalize();
                _body.VelocityTarget = playerDirection * _moveSpeed;
            }
        }

        private void UpdateWalking()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;

            // attack if we are close enough
            if (Math.Abs(playerDirection.X) < 24 && 0 < playerDirection.Y && playerDirection.Y < 18)
                _aiComponent.ChangeState("attack");
        }

        private void EndWalking()
        {
            _aiComponent.ChangeState("idle");
        }

        private void InitPreDamageState()
        {
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
        }

        private void UpdatePreDamaged()
        {
            if (_body.IsGrounded && !_aiDamageState.IsInDamageState() && _body.Velocity.Length() < 0.1f)
                _aiComponent.ChangeState("damaged");
        }

        private void InitDamageState()
        {
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;

            _damageField.IsActive = false;

            if (!_swordShotHit)
                Game1.AudioManager.PlaySoundEffect("D360-40-28");

            for (var i = 0; i < _partVelocity.Length; i++)
            {
                _damageCounters[i] = _damageTime[i];
                _partVelocity[i] = 0;
            }
        }

        private void UpdateDamaged()
        {
            _damageState = true;
            _vulnerable = false;

            for (var i = 1; i < _partVelocity.Length; i++)
            {
                _damageCounters[i] -= Game1.DeltaTime;

                // fall onto the ground
                if (_damageCounters[i] < 0)
                {
                    _partVelocity[i] += _partGravity * Game1.TimeMultiplier;
                    var velocityOffset = _partVelocity[i] * Game1.TimeMultiplier;

                    if (_positions[i].Z - velocityOffset > _sprites[i].SourceRectangle.Height - _partOffset[i])
                        _positions[i].Z -= velocityOffset;
                    else
                        _positions[i].Z = _sprites[i].SourceRectangle.Height - _partOffset[i];
                }
            }
        }

        private void WobbleTick(double counter)
        {
            // move the upper body up and down
            // 4 frames to go up/down
            _positions[1].Z = _sprites[1].SourceRectangle.Height + 1 -
                              MathF.Sin(MathF.PI / 2 + MathF.PI * ((_wobbleTime - (float)counter) / 1000 * (60 / 4f)));
        }

        private void WobbleEnd()
        {
            _aiComponent.ChangeState("standUp");
        }

        private void InitStandingUp()
        {
            for (var i = 0; i < _partVelocity.Length; i++)
            {
                _standCounters[i] = _damageTime[i];
            }
        }

        private void StandUpTick(double counter)
        {
            for (var i = 0; i < _partVelocity.Length; i++)
            {
                _standCounters[i] -= Game1.DeltaTime;

                if (_standCounters[i] < 0)
                {
                    var speed = 0.5f * Game1.TimeMultiplier;
                    if (_positions[i].Z + speed < -_animator.CurrentFrame.Sprites[i].Offset.Y)
                        _positions[i].Z += speed;
                    else
                        _positions[i].Z = -_animator.CurrentFrame.Sprites[i].Offset.Y;
                }
            }
        }

        private void StandUpEnd()
        {
            if (_aiDamageState.CurrentLives > 0)
                _damageField.IsActive = true;

            _swordShotHit = false;
            _aiDamageState.MoveBody = true;
            _damageCooldown.Reset();
            _damageState = false;
            _aiComponent.ChangeState("idle");
        }

        private void OnDeath(bool pieceOfPower)
        {
            _flee = true;
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void OnBossDeath()
        {
            TryEarnAchievement();

            if (!string.IsNullOrEmpty(_saveKey))
                Game1.GameManager.SaveManager.SetString(_saveKey, (_encounterNumber + 1).ToString());

            // stop the music
            Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);

            if (_encounterNumber == 3)
                SpawnHookshot();
            else
                SpawnSmallKey();

            Map.Objects.DeleteObjects.Add(this);
        }

        private void TryEarnAchievement()
        {
            // If it's the third encounter, 
            if (_encounterNumber == 3)
            {
                if (Game1.GameManager.SaveManager.GetString("mstalfos_achievement","0") == "1")
                    AchievementManager.Earn(66);
                Game1.GameManager.SaveManager.RemoveString("mstalfos_achievement");
            }
        }
        
        private void SpawnHookshot()
        {
            var objItem = new ObjItem(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 16, "j", "hookshot_collected", "hookshot", null);
            Map.Objects.SpawnObject(objItem);
        }

        private void SpawnSmallKey()
        {
            var saveKey =  _saveKey + "_key_" + _encounterNumber;
            var objItem = new ObjItem(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 16, "j", saveKey, "smallkey", "five");
            Map.Objects.SpawnObject(objItem);
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // change the draw effect
            if (_sprite.SpriteShader != null)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, _sprite.SpriteShader);
            }

            // do not draw the legs if the upper part is laying on the floor
            _sprites[0].IsVisible = _sprites[1].SourceRectangle.Height + 2 < _positions[1].Z;

            for (var i = 0; i < _positions.Length; i++)
            {
                var spriteIndex = _drawOrder[i];

                if (_animator.CurrentFrame.Sprites[spriteIndex] == null)
                    continue;

                _positions[spriteIndex].X = EntityPosition.X + _animator.CurrentFrame.Sprites[spriteIndex].Offset.X;
                _positions[spriteIndex].Y = EntityPosition.Y;

                // only update the animation if the boss is not in the damaged state
                if (!_damageState)
                {
                    _positions[spriteIndex].Z = EntityPosition.Z - _animator.CurrentFrame.Sprites[spriteIndex].Offset.Y;

                    _sprites[spriteIndex].SpriteEffect = (_animator.CurrentFrame.Sprites[spriteIndex].MirroredV ? SpriteEffects.FlipVertically : 0) |
                                                         (_animator.CurrentFrame.Sprites[spriteIndex].MirroredH ? SpriteEffects.FlipHorizontally : 0);
                }
                _sprites[spriteIndex].Color = Color.White * _transparency;
                _sprites[spriteIndex].Draw(spriteBatch);
            }

            // change the draw effect
            if (_sprite.SpriteShader != null)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, null);
            }
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (!_damageField.IsActive)
                return false;

            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X, direction.Y, _body.Velocity.Z);

            return true;
        }

        private void PauseDownedState(float pauseTime)
        {
            switch (_aiComponent.CurrentStateId)
            {
                case "damaged":
                {
                    _damagedCountdown.CurrentTime += pauseTime;
                    break;
                }
                case "wobble":
                {
                    _wobbleCountdown.CurrentTime = Math.Min(_wobbleCountdown.CurrentTime + pauseTime, _wobbleTime);
                    break;
                }
                case "standUp":
                {
                    _standUpCountdown.CurrentTime += pauseTime;

                    for (var i = 0; i < _standCounters.Length; i++)
                        _standCounters[i] += pauseTime;
                    break;
                }
            }
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Is the master stalfos currently in a damage state.
            var inDamageState = _aiDamageState.IsInDamageState();
            bool isBombArrow = (hitType & HitType.BombArrow) == HitType.BombArrow;

            // Simulates a "real" hit like in the original game.
            Values.HitCollision SimulateHitBug()
            {
                _realHits++;
                _aiDamageState.OnHit(gameObject, direction, hitType, 0, pieceOfPower);
                _aiDamageState.SetDamageState(true);
                return Values.HitCollision.Blocking;
            }

            // Simulates a "real" death like in the original game.
            Values.HitCollision SimulateDeathBug()
            {
                // Add the "boss death" type with the explosions.
                if (!_aiComponent.States.ContainsKey("deathBoss"))
                    _aiDamageState.AddBossDamageState(OnBossDeath);

                // Perform a big hit on the master stalfos that takes away all HP.
                var result = _aiDamageState.OnHit(gameObject, direction, hitType, 500, pieceOfPower);
                if (_aiDamageState.CurrentLives <= 0)
                {
                    _damageField.IsActive = false;
                    _hitComponent.IsActive = false;
                    _pushComponent.IsActive = false;
                }
                return result;
            }

            // To simulate master stalfos swordshot damage draw from a separate "HP" pool.
            if (hitType == HitType.SwordShot && (_aiComponent.CurrentStateId == "damaged" || _aiComponent.CurrentStateId == "wobble"))
            {
                // If the simulated HP pool still has HP left in it.
                if (_realLives > 0)
                {
                    // The sound is skipped while damage state is active so force play it.
                    Game1.AudioManager.PlaySoundEffect("D370-07-07");

                    // Simulate a "hit" by reducing the "shot pool" by 2 damage.
                    _realLives -= 1;
                    _shownIntroText = true;
                    _swordShotHit = true;
                    _aiDamageState.MoveBody = false;

                    // Delay the downed-state timers so spamming shots keeps him stun locked.
                    PauseDownedState(_swordShotStunTime);

                    // When out of HP do the "real" death.
                    return _realLives <= 0 ? SimulateDeathBug() : SimulateHitBug();
                }
                // When out of HP do the "real" death.
                return SimulateDeathBug();
            }
            // Sword Shot should not deal damage unless he's on the ground.
            else if (hitType == HitType.SwordShot)
                return Values.HitCollision.RepellingParticle;
            
            // Bomb Arrows drain the simulated HP pool like sword shots — only while down.
            if (isBombArrow)
            {
                // Boss is currently a pile of bones.
                if (_aiComponent.CurrentStateId == "damaged" || _aiComponent.CurrentStateId == "wobble")
                {
                    // If he stil has more "real lives" then we can deal damage.
                    if (_realLives > 0)
                    {
                        // Fail the "no damage or bomb arrows between fights 2-4" achievement if using bomb arrows.
                        if (_encounterNumber > 0)
                            Game1.GameManager.SaveManager.RemoveString("mstalfos_achievement");

                        Game1.AudioManager.PlaySoundEffect("D370-07-07");
                        _realLives -= 64;
                        _shownIntroText = true;

                        // We do this here too so it hits the moment real lives hit zero.
                        return _realLives <= 0 ? SimulateDeathBug() : SimulateHitBug();
                    }
                    // If real lives are reduced to zero then kill the boss.
                    return SimulateDeathBug();
                }
                // Standing: bomb arrows can't knock him down.
                return Values.HitCollision.RepellingParticle;
            }
            // Don't deal more damage during flicker.
            if (_aiDamageState.CurrentLives <= 0 || inDamageState)
                return Values.HitCollision.None;

            // Fall into a pile of bones. If hit before the dialog, don't show it anymore. 
            if (_vulnerable && _aiComponent.CurrentStateId != "preDamaged" || (_damageCooldown.State && (hitType & HitType.AnySword) != 0 &&
                _aiComponent.CurrentStateId != "preDamaged" && _aiComponent.CurrentStateId != "damaged" &&
                _aiComponent.CurrentStateId != "attack" && _aiComponent.CurrentStateId != "wobble" && _aiComponent.CurrentStateId != "standUp"))
            {
                _realLives -= 1;
                _shownIntroText = true;
                _aiComponent.ChangeState("preDamaged");
                _aiDamageState.SetDamageState(false);
                Game1.AudioManager.PlaySoundEffect("D370-07-07");
                return Values.HitCollision.Repelling;
            }

            // Knockback when hit.
            if (_body.Velocity.Length() < 0.01f)
            {
                _body.Velocity.X = direction.X;
                _body.Velocity.Y = direction.Y;
            }

            // Can be damaged on the floor with bombs.
            if ((_aiComponent.CurrentStateId == "damaged" || _aiComponent.CurrentStateId == "wobble") && (hitType & HitType.Bomb) != 0)
            {
                // Subtract 4 real HP from normal bomb hits.
                _realLives -= 4;

                // Reduces 1 HP from the bomb count lives on a bomb hit.
                _aiDamageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

                // If the number of "real hits" would reduce lives below zero when a bomb
                // is used, then call the normal "OnDeath" so he can not take further hits. 
                if (_aiDamageState.CurrentLives - _realHits <= 0)
                    OnDeath(false);
            }

            // When out of HP do the "real" death.
            if (_realLives <= 0)
                SimulateDeathBug();

            // If it's an attack that has no effect.
            return Values.HitCollision.RepellingParticle;
        }
    }
}
