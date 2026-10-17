using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    class BossHardhitBeetle : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly AiTriggerCountdown _hitCooldown;
        private readonly AiTriggerCountdown _colorCountdown;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private EnemyStalfosGreen[] _stalfos = new EnemyStalfosGreen[2];

        private readonly Color[] _colors = {
            new Color(42, 41, 254),
            new Color(0, 149, 114),
            new Color(34, 212, 16),
            new Color(141, 206, 9),
            new Color(254, 198, 1),
            new Color(253, 131, 0),
            new Color(255, 66, 1),
            new Color(253, 0, 0)
        };
        private readonly string _saveKey;

        // small delay before starting to walk
        private float _idleDelayCounter = 250;

        private const int CooldownTime = 250;
        private const float MoveSpeed = 0.375f;

        private int _colorIndex;
        private int _lastcolorIndex;
        private bool _negativeColorChange;
        private double _swordHitValue;

        private int _lives = EnemyLives.HardHitBeetle;
        private bool _isDead;

        private float _stalfosCounter;
        private bool _spawnedStalfos;

        public bool DealtPlayerDamage;

        public BossHardhitBeetle() : base("hardhit beetle") { }

        public BossHardhitBeetle(Map.Map map, int posX, int posY, string saveKey) : base(map)
        {
            EntityPosition = new CPosition(posX + 16, posY + 32, 0);
            EntitySize = new Rectangle(-16, -40, 32, 40);

            _saveKey = saveKey;

            // was already killed?
            if (!string.IsNullOrEmpty(_saveKey) &&
                Game1.GameManager.SaveManager.GetString(_saveKey) == "1")
            {
                IsDead = true;
                return;
            }

            _animator = AnimatorSaveLoad.LoadAnimator("Nightmares/hardhit beetle");
            _animator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -14, -26, 28, 26, 8)
            {
                Gravity = -0.1f,
                FieldRectangle = Map.GetField(posX, posY, 16)
            };

            var stateIdle = new AiState(UpdateIdle);
            var stateIdleDelay = new AiState(UpdateIdleDelay);
            var stateWalk = new AiState(UpdateWalk) { Init = InitWalk };
            stateWalk.Trigger.Add(new AiTriggerRandomTime(EndWalk, 500, 1000));

            _aiComponent = new AiComponent();
            _aiComponent.Trigger.Add(new AiTriggerRandomTime(Shoot, 500, 2500));
            _aiComponent.Trigger.Add(_colorCountdown = new AiTriggerCountdown(2000, null, OnColorReset));
            _aiComponent.Trigger.Add(_hitCooldown = new AiTriggerCountdown(CooldownTime, TickCooldown, null));

            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("idleDelay", stateIdleDelay);
            _aiComponent.States.Add("walk", stateWalk);
            _aiDamageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, 0, true, false)
            {
                HitMultiplierX = 0,
                HitMultiplierY = 0,
                BossHitSound = true
            };
            _aiDamageState.DamageSpriteShader = Resources.DamageSpriteShader1;
            _aiDamageState.AddBossDamageState(OnDeathAnimationEnd);

            _aiComponent.ChangeState("idle");

            var damageBox = new CBox(EntityPosition, -14, -24, 0, 28, 24, 8);
            var hittableBox = new CBox(EntityPosition, -13, -34, 0, 26, 30, 8);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4) { OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));
        }

        public void OnDamagedPlayer()
        {
            DealtPlayerDamage = true;
        }

        private void UpdateIdle()
        {
            // player enters the room?
            if (_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
            {
                Game1.GameManager.StartDialogPath("hardhit_beetle_enter");
                _aiComponent.ChangeState("idleDelay");
            }
        }

        private void UpdateIdleDelay()
        {
            if (Game1.GameManager.DialogIsRunning())
                return;

            _idleDelayCounter -= Game1.DeltaTime;
            if (0 < _idleDelayCounter)
                return;

            _aiComponent.ChangeState("walk");
        }

        private void TickCooldown(double counter)
        {
            _sprite.SpriteShader = (CooldownTime - counter) <= 4200 / 60f ? Resources.DamageSpriteShader0 : null;
        }

        private void OffsetColor(int offset)
        {
            // Offset the color index with the amount provided by offset parameter.
            _colorIndex = MathHelper.Clamp(_colorIndex + offset, 0, _colors.Length - 1);

            // Tracks when a color change happens for level 1 sword which only deals half a damage.
            if (offset < 0)
                _negativeColorChange = true;

            // If the index falls to 0 (blue) reset the stalfos and show the reset dialog.
            if (_colorIndex == 0 && _colorIndex != _lastcolorIndex)
            {
                _spawnedStalfos = false;
                _swordHitValue = 0;
                Game1.GameManager.StartDialogPath("hardhit_beetle_1");
            }

            // When the integer hits 6 (red-orange) spawn 2 stalfos and show the dialog.
            if (!_spawnedStalfos && offset > 0 && _colorIndex >= 6)
            {
                _spawnedStalfos = true;
                _stalfosCounter = 250;
                Game1.GameManager.StartDialogPath("hardhit_beetle_2");
            }
            // Remember the last index used so we don't repeat lines above.
            _lastcolorIndex = _colorIndex;
        }

        private void OnColorReset()
        {
            OffsetColor(-1);
            _colorCountdown.OnInit();
        }

        private void InitWalk()
        {
            var direction = Game1.RandomNumber.Next(0, 8) / 4f * MathF.PI;
            _body.VelocityTarget = new Vector2(MathF.Sin(direction), MathF.Cos(direction)) * MoveSpeed;
        }

        private void UpdateWalk()
        {
            if (!_spawnedStalfos || _stalfosCounter <= 0)
                return;

            _stalfosCounter -= Game1.DeltaTime;

            if (_stalfosCounter <= 0)
            {
                for (var i = 0; i < _stalfos.Length; i++)
                {
                    if (_stalfos[i] != null && _stalfos[i].Map != null)
                        continue;

                    var randomOffsetX = (int)MapManager.ObjLink.EntityPosition.X - 8 + (Game1.RandomNumber.Next(0, 13) - 6);
                    var randomOffsetY = (int)MapManager.ObjLink.EntityPosition.Y - 15 + (Game1.RandomNumber.Next(0, 8) - 4);

                    _stalfos[i] = new EnemyStalfosGreen(Map, randomOffsetX, randomOffsetY, this);
                    _stalfos[i].SetAirPosition(32);
                    Map.Objects.SpawnObject(_stalfos[i]);
                }
            }
        }

        private void RemoveStalfos()
        {
            // The moment the boss dies, the Stalfos should be removed.
            for (var i = 0; i < _stalfos.Length; i++)
            {
                // Make sure they still exist before trying to remove them to avoid crashes.
                if (_stalfos[i] != null && _stalfos[i].Map != null)
                {
                    Map.Objects.DeleteObjects.Add(_stalfos[i]);
                    var explosionAnimation = new ObjAnimator(Map, (int)_stalfos[i].EntityPosition.X - 8, (int)_stalfos[i].EntityPosition.Y - 16, Values.LayerTop, "Particles/spawn", "run", true);
                    Map.Objects.SpawnObject(explosionAnimation);
                }
            }
        }

        private void Shoot()
        {
            if (_aiComponent.CurrentStateId != "walk")
                return;

            var objShot = new BossHardhitBeetleShot(Map, this, new Vector2(EntityPosition.X, EntityPosition.Y - 16), 1, _body.FieldRectangle);
            Map.Objects.SpawnObject(objShot);
        }

        private void EndWalk()
        {
            _aiComponent.ChangeState("walk");
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            _sprite.Draw(spriteBatch);

            var sourceRectangle = _sprite.SourceRectangle;

            _sprite.SourceRectangle.X += sourceRectangle.Width + (int)(_sprite.Scale * 2);

            _sprite.Color = _colors[_colorIndex];
            _sprite.Draw(spriteBatch);

            _sprite.SourceRectangle = sourceRectangle;
            _sprite.Color = Color.White;
        }

        private void OnDeathAnimationEnd()
        {
            if (!string.IsNullOrEmpty(_saveKey))
                Game1.GameManager.SaveManager.SetString(_saveKey, "1");

            // Stop boss music and play sound effect.
            Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);
            Game1.AudioManager.PlaySoundEffect("D378-26-1A");

            // Spawns a fairy.
            Game1.AudioManager.PlaySoundEffect("D360-27-1B");
            Map.Objects.SpawnObject(new ObjDungeonFairy(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 8));

            // Achievement: If the player didn't take damage, unlock the achievement.
            if (!DealtPlayerDamage)
                AchievementManager.Earn(42);

            // Remove the boss.
            Map.Objects.DeleteObjects.Add(this);
        }
        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            return true;
        }

        private int GetSwordLevel1Hit()
        {
            // Tracks if +1 to the counter is returned or not.
            var returnValue = 0;

            // Track that a hit was dealt since last color change.
            _swordHitValue += 0.5;

            // Deal a hit when blue, when color change went negative, or _swordHitValue reaches 1.
            if (_colorIndex == 0 || _negativeColorChange || _swordHitValue >= 1)
            {
                // Reset the sword hit value to 0 and add a point.
                _swordHitValue = 0;
                returnValue = 1;
            }
            // Reset this so the next negative color change can be tracked.
            _negativeColorChange = false;

            // Return 0 so the color is not incremented.
            return returnValue;
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Ignore Magic Powder hits, damage cooldown, boss is already dead, or it's "idle" (fight hasn't started yet). 
            if (hitType == HitType.MagicPowder || _hitCooldown.CurrentTime > 0 || _isDead || _aiComponent.CurrentStateId == "idle")
                return Values.HitCollision.None;

            // Damage hit shader simulation.
            _hitCooldown.OnInit();

            // Reset the timer when going from 0 > 1 so it can't immediately fall back to 0.
            if (_colorIndex == 0)
                _colorCountdown.OnInit();

            // The amount of change to the color index and the type of hit collision.
            var colorOffset = 0;
            var collisionType = Values.HitCollision.Repelling | Values.HitCollision.Repelling0;

            // A sword spin or boots dash basically adds the level of the sword.
            if ((hitType & HitType.SwordSpin) != 0 || (hitType & HitType.PegasusBootsSword) != 0)
                colorOffset = Game1.GameManager.SwordLevel;

            // The level 1 sword is unique in that it only deals 0.5 damage. This game does not handle
            // damage as decimals, so we need to perform some trickery to simulate a half of a hit.
            else if ((hitType & HitType.Sword1) != 0)
                colorOffset = GetSwordLevel1Hit();

            // These damage types add 1 point.
            else if ((hitType & HitType.Sword2) != 0 || hitType == HitType.Boomerang || hitType == HitType.Hookshot || hitType == HitType.SwordShot)
                colorOffset = 1;

            // These damage types add 2 points.
            else if (hitType == HitType.Bow ||hitType == HitType.Bomb || hitType == HitType.BombArrow || hitType == HitType.MagicRod)
                colorOffset = 2;

            // Change the hit type so the hit is absorbed.
            if ((hitType & HitType.SwordHold) != 0 || hitType == HitType.Hookshot || hitType == HitType.SwordShot || hitType == HitType.Boomerang || hitType == HitType.Bow)
                collisionType = Values.HitCollision.Enemy;

            // If the index 6 + 2 (exceeds 7) or is 7 (Red) and the offset is positive, kill the boss.
            if (colorOffset > 0 && (_colorIndex == 6 && colorOffset >= 2 || _colorIndex == 7))
                _aiDamageState.OnHit(gameObject, direction, hitType, damage, false);

            // When below 7, do a small knockback on the boss and play the hit sound effect.
            else
            {
                _body.Velocity.X = direction.X;
                _body.Velocity.Y = direction.Y;
                Game1.AudioManager.PlaySoundEffect("D370-07-07");
            }
            // If the boss took a hit that reduced it's lives below 0, then it is defeated.
            if (_aiDamageState.CurrentLives <= 0)
            {
                _isDead = true;
                _animator.Pause();
                _damageField.IsActive = false;
                _pushComponent.IsActive = false;
                _hitComponent.IsActive = false;
                _body.VelocityTarget = Vector2.Zero;
                RemoveStalfos();
            }
            // If it was any type of hit other than level 1 sword, reset
            // the count up value so it tracks correctly with a new color.
            if ((hitType & HitType.Sword1) == 0)
                _swordHitValue = 0;

            // Apply the offset to the color.
            OffsetColor(colorOffset);

            return collisionType;
        }
    }
}
