using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Components.AI
{
    class AiDamageState
    {
        public delegate void OnDeleteTemplate(bool pieceOfPower);
        public OnDeleteTemplate OnDeath;

        public delegate void OnLiveZero();
        public OnLiveZero OnLiveZeroed;

        public delegate void OnBurnDelegate();
        public OnBurnDelegate OnBurn;

        public int ExplosionOffsetY;
        public Point FlameOffset;

        public float HitMultiplierX = 5;
        public float HitMultiplierY = 5;

        public bool IgnoreZeroDamage;
        public bool IsActive = true;
        public bool MoveBody = true;
        public bool UpdateLastStateFire;

        public bool DeathAnimation = true;
        public bool PlayHitSound = true;
        public bool PlayDeathSound = true;
        public bool PlayDeathExplosions = true;

        public int DropTableIndex = 0;
        public bool SpawnPowerups = true;

        private readonly GameObject _gameObject;
        private readonly BodyComponent _body;
        private readonly AiComponent _aiComponent;
        private readonly CSprite _sprite;

        public SpriteShader DamageSpriteShader;
        private SpriteShader _normalShader;

        public AiTriggerCountdown DamageTrigger;
        private AiTriggerCountdown _deathCountdown;
        private AiTriggerCountdown _knockbackCountdown;

        private bool _pieceOfPower;
        private float _bodyDrag;
        private float _bodyDragAir;

        private double _pieceOfPowerCounter;
        private int _pieceOfPowerDeathCount;

        public int CurrentLives;

        private bool _damageBlink;
        private bool _returnState;
        private readonly bool _hasBurnState;
        private bool _isDead;

        public const int BlinkTime = 66;
        public const int StaticCooldown = BlinkTime * 6;

        private int _cooldownTime;
        public int CooldownTime
        {
            get { return _cooldownTime; }
            set
            {
                _cooldownTime = value;
                DamageTrigger.StartTime = value;
                _deathCountdown.StartTime = value;
                _knockbackCountdown.StartTime = value;
            }
        }
        public int ExplosionWidth = 32;
        public int ExplosionHeight = 32;

        public bool HasDamageState;
        public bool BossHitSound;

        private float _deathCount = -1000;

        public bool _disableGuardianAcorn => MapManager.ObjLink.DisableGuardianAcorn;
        public bool _disablePieceofPower  => MapManager.ObjLink.DisablePieceOfPower;

        public AiDamageState(GameObject gameObject, BodyComponent body, AiComponent aiComponent, CSprite sprite, int lives, int dropIndex, bool hasDamageState = true, bool hasBurnState = true, int cooldownTime = StaticCooldown)
        {
            _gameObject = gameObject;
            _body = body;
            _aiComponent = aiComponent;
            _sprite = sprite;
            _normalShader = sprite.SpriteShader;

            CurrentLives = lives;
            DropTableIndex = dropIndex;

            HasDamageState = hasDamageState;
            _hasBurnState = hasBurnState;

            _cooldownTime = cooldownTime;

            OnDeath = BaseOnDeath;

            DamageSpriteShader = Resources.DamageSpriteShader0;

            _aiComponent.Trigger.Add(DamageTrigger = new AiTriggerCountdown(cooldownTime, DamageTick, FinishDamage));
            if (hasDamageState)
                _aiComponent.States.Add("damage", new AiState());

            var stateKnockBack = new AiState();
            stateKnockBack.Trigger.Add(_knockbackCountdown = new AiTriggerCountdown(cooldownTime, null, FinishKnockback));

            var statePieceOfPower = new AiState(UpdatePieceOfPower) { Init = InitPieceOfPower };
            var stateBurning = new AiState(UpdateBurning) { Init = InitBurning };
            var stateDamageDeath = new AiState { Init = () => OnDeath(false) };

            _aiComponent.Trigger.Add(_deathCountdown = new AiTriggerCountdown(cooldownTime, DeathTick, () => DeathTick(0)));

            _aiComponent.States.Add("knockBack", stateKnockBack);
            _aiComponent.States.Add("pieceOfPower", statePieceOfPower);
            if (hasBurnState)
                _aiComponent.States.Add("burning", stateBurning);
            _aiComponent.States.Add("damageDeath", stateDamageDeath);
        }

        public void AddBossDamageState(AiTriggerCountdown.TriggerEndFunction deathAnimationEnd)
        {
            OnDeath = OnDeathBoss;

            var stateDeath = new AiState(UpdateDeath);
            stateDeath.Trigger.Add(new AiTriggerCountdown(3000 / BlinkTime * BlinkTime, UpdateBlink, deathAnimationEnd));

            _aiComponent.States.Add("deathBoss", stateDeath);
        }

        public bool IsInDamageState()
        {
            return DamageTrigger.CurrentTime > 0 ||
                _aiComponent.CurrentStateId == "knockBack" ||
                _aiComponent.CurrentStateId == "burning" ||
                _aiComponent.CurrentStateId == "pieceOfPower";
        }

        public Values.HitCollision HitKnockBack(GameObject gameObject, Vector2 direction, HitType hitType, bool pieceOfPower, bool blink = true)
        {
            if (!IsActive || IsInDamageState())
                return Values.HitCollision.None;

            // NULL-BODY: without a body there is nothing to launch, so the piece
            // of power state (which depends on body collision flags) is skipped.
            if (_body == null)
                pieceOfPower = false;

            _aiComponent.ChangeState(pieceOfPower ? "pieceOfPower" : "knockBack");

            _damageBlink = blink;
            DamageTrigger.OnInit();

            // NULL-BODY: skip all velocity changes without a body.
            if (_body != null)
            {
                if (pieceOfPower && !GameSettings.NoDamageLaunch)
                {
                    _body.Velocity.X = direction.X * 3;
                    _body.Velocity.Y = direction.Y * 3;
                }
                else
                {
                    _body.Velocity.X = direction.X * HitMultiplierX;
                    _body.Velocity.Y = direction.Y * HitMultiplierY;
                }
            }
            _returnState = true;

            return Values.HitCollision.Enemy;
        }

        private bool IsStunHit(HitType hitType)
        {
            var hittable = _gameObject.Components[HittableComponent.Index] as HittableComponent;
            if (hittable == null)
                return false;

            return (hitType == HitType.Hookshot     && hittable.StunHookshot)  ||
                   (hitType == HitType.Boomerang    && hittable.StunBoomerang) ||
                   (hitType == HitType.ThrownObject && hittable.StunThrown)    ||
                   (hitType == HitType.MagicPowder  && hittable.StunPowder);
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If one hit kills is enabled make sure it kills in one hit.
            if (GameSettings.ChOneHitKills)
                damage = 1000;

            // Don't register a hit if object is not active.
            if (!IsActive || IsInDamageState())
                return Values.HitCollision.None;

            // NULL-BODY: the piece of power launch state requires a body to move and to
            // detect wall collisions; without one, treat the hit as a normal hit instead.
            if (_body == null)
                pieceOfPower = false;

            // Directly delete the GameObject if the attack comes from Bow Wow.
            if (hitType == HitType.BowWow)
            {
                // Bow Wow has a custom sound for attacking so disable the normal sound and play this one.
                Game1.AudioManager.PlaySoundEffect("D360-03-03");
                PlayDeathSound = false;
                PlayDeathExplosions = false;
                DeathAnimation = false;
                OnDeath(false);
                return Values.HitCollision.Enemy;
            }
            // If the enemy can detect hits even when health is 0 (or lower).
            if (damage <= 0 && IgnoreZeroDamage || DamageTrigger.CurrentTime > 0)
                return Values.HitCollision.Enemy;

            // Stun-type hits stun without dealing damage.
            if (_aiComponent.StunnedState != null && IsStunHit(hitType))
            {
                var damageField = _gameObject.Components[DamageFieldComponent.Index] as DamageFieldComponent;
                SetDamageState(false);
                return _aiComponent.StunnedState.HitStun(_body, damageField, direction);
            }
            // Reduce the enemy's health by the amount of damage recieved.
            CurrentLives -= damage;

            // Burn on Magic Powder or Magic Rod impact.
            if (_hasBurnState && (hitType == HitType.MagicPowder || hitType == HitType.MagicRod))
            {
                if (_aiComponent.CurrentStateId != "burning")
                {
                    _aiComponent.ChangeState("burning");
                    var speedMultiply = (hitType == HitType.MagicPowder ? 0.125f : 0.5f);

                    // NULL-BODY: skip the burn knockback without a body.
                    if (MoveBody && _body != null)
                    {
                        _body.Velocity.X = direction.X * HitMultiplierX * speedMultiply;
                        _body.Velocity.Y = direction.Y * HitMultiplierY * speedMultiply;
                    }
                    // Burning with Magic Powder is a combination of two sound effects.
                    Game1.AudioManager.PlaySoundEffect("D360-03-03");
                    Game1.AudioManager.PlaySoundEffect("D378-18-12");

                    return Values.HitCollision.Enemy;
                }
            }
            // Don't register a hit if object is burning.
            if (_aiComponent.CurrentStateId == "burning")
                return Values.HitCollision.None;

            // Play the appropriate "hit" sound effect.
            if (PlayHitSound && !BossHitSound)
            {
                if (pieceOfPower)
                    Game1.AudioManager.PlaySoundEffect("D370-17-11");
                else if (damage > 0)
                    Game1.AudioManager.PlaySoundEffect("D360-03-03");
                else
                    Game1.AudioManager.PlaySoundEffect("D360-09-09");
            }
            else if (BossHitSound)
            {
                if (CurrentLives <= 0)
                    Game1.AudioManager.PlaySoundEffect("D378-19-13");
                else if (damage > 0)
                    Game1.AudioManager.PlaySoundEffect("D370-07-07");
                else
                    Game1.AudioManager.PlaySoundEffect("D360-09-09");
            }
            // If the player reduced the damage launch effect.
            if (pieceOfPower && !GameSettings.NoDamageLaunch)
                _aiComponent.ChangeState("pieceOfPower");
            else
            {
                if (HasDamageState)
                {
                    _returnState = true;
                    _aiComponent.ChangeState("damage");
                }
            }
            DamageTrigger.OnInit();

            _damageBlink = damage > 0;

            // NULL-BODY: skip the hit knockback without a body.
            if (MoveBody && _body != null)
            {
                if (pieceOfPower && !GameSettings.NoDamageLaunch)
                {
                    _body.Velocity.X = direction.X * 3;
                    _body.Velocity.Y = direction.Y * 3;
                }
                else
                {
                    _body.Velocity.X = direction.X * HitMultiplierX;
                    _body.Velocity.Y = direction.Y * HitMultiplierY;
                }
            }
            // Trigger death when the enemy health is depleted.
            if (CurrentLives <= 0)
            {
                OnLiveZeroed?.Invoke();
                _deathCountdown.OnInit();
                gameObject.Map.Objects.RegisterAlwaysAnimateObject(gameObject);
            }
            return Values.HitCollision.Enemy;
        }

        public void SetDamageState(bool blink = true)
        {
            _damageBlink = blink;
            DamageTrigger.OnInit();
        }

        private void InitPieceOfPower()
        {
            _pieceOfPower = true;

            // NULL-BODY: this state should be unreachable without a body
            // (OnHit/HitKnockBack force pieceOfPower to false), but guard anyway.
            if (_body != null)
            {
                _bodyDrag = _body.Drag;
                _bodyDragAir = _body.DragAir;

                _body.Drag = 1.0f;
                _body.DragAir = 1.0f;
            }
            _pieceOfPowerDeathCount = 0;
        }

        private void UpdatePieceOfPower()
        {
            if (!HasDamageState)
                _aiComponent.States[_aiComponent.LastStateId].Update?.Invoke();

            // Draw a trail of smoke as the enemy travels.
            if (_pieceOfPowerCounter <= 0)
            {
                _pieceOfPowerCounter = 80;
                var animation = new ObjAnimator(_gameObject.Map, 0, 0, 0, 0, Values.LayerPlayer, "Particles/pieceOfPowerTrail", "run", true);

                // NULL-BODY: fall back to the entity position for the trail position.
                if (_body != null)
                {
                    var aniOffset = new Vector3(_body.OffsetX + _body.Width / 2f, _body.OffsetY + _body.Height / 2f, 0);
                    animation.EntityPosition.Set(_body.Position.ToVector3() + aniOffset);
                }
                else
                {
                    animation.EntityPosition.Set(_gameObject.EntityPosition.ToVector3());
                }
                Game1.GameManager.MapManager.CurrentMap.Objects.SpawnObject(animation);
                _pieceOfPowerDeathCount++;
            }
            _pieceOfPowerCounter -= Game1.DeltaTime;

            // NULL-BODY: without a body there are no velocities or wall collisions to
            // evaluate; let the death counter end the state after 5 iterations below.
            var blockedX = false;
            var blockedY = false;
            var collision = false;

            if (_body != null)
            {
                // Filter out any extremely small velocities.
                float epsilon = 0.001f;

                // Store the current velocities so they can be referenced.
                var bodyVelX = _body.Velocity.X;
                var bodyVelY = _body.Velocity.Y;
                var lastCollision = _body.LastVelocityCollision;

                // Test for collision along the X axis.
                bool collideL = (bodyVelX < -epsilon) && (lastCollision & Values.BodyCollision.Left) != 0;
                bool collideR = (bodyVelX > epsilon) && (lastCollision & Values.BodyCollision.Right) != 0;
                blockedX = collideL || collideR;

                // Test for collision along the Y axis.
                bool collideT = (bodyVelY < -epsilon) && (lastCollision & Values.BodyCollision.Top) != 0;
                bool collideB = (bodyVelY > epsilon) && (lastCollision & Values.BodyCollision.Bottom) != 0;
                blockedY = collideT || collideB;

                // If a collision took place then cancel the corresponding velocity.
                if (blockedX)
                    _body.Velocity.X = 0;
                if (blockedY)
                    _body.Velocity.Y = 0;

                // Glide on the wall depending on the angle the body moved towards the wall.
                bool collisionX = blockedX && MathF.Abs(bodyVelX) > MathF.Abs(bodyVelY);
                bool collisionY = blockedY && MathF.Abs(bodyVelY) > MathF.Abs(bodyVelX);
                collision = collisionX || collisionY;
            }

            // If both collisions happened or the counter has reached 5 iterations.
            if ((collision && _pieceOfPowerDeathCount > 1) || (_pieceOfPowerDeathCount > 5))
            {
                // The hit killed the enemy.
                if (CurrentLives <= 0)
                {
                    // NULL-BODY: no velocities to clear without a body.
                    if (_body != null)
                    {
                        _body.Velocity.X = 0;
                        _body.Velocity.Y = 0;
                    }
                    _deathCountdown.Stop();
                    OnDeath(true);
                    return;
                }
                // The enemy survived the hit.
                _pieceOfPower = false;

                // NULL-BODY: no drag values to restore without a body.
                if (_body != null)
                {
                    _body.Drag = _bodyDrag;
                    _body.DragAir = _bodyDragAir;
                }
                _aiComponent.ChangeState(_aiComponent.LastStateId, true);
            }
        }

        private void InitBurning()
        {
            OnBurn?.Invoke();

            // NULL-BODY: no movement to stop without a body.
            if (_body != null)
                _body.VelocityTarget = Vector2.Zero;

            // Spawn the burning effect.
            var burnAnimator = new ObjBurningEffect(_gameObject.Map, 0, 0, 0, 0, false);
            burnAnimator.EntityPosition.Set(_gameObject.EntityPosition.Position);

            // NULL-BODY: without a body, anchor the flame to the entity position
            // plus the flame offset instead of the body box.
            var flameAnchor = _body != null
                ? new Vector2((int)(_body.OffsetX + _body.Width / 2) + FlameOffset.X, (int)(_body.OffsetY + _body.Height) - 8 + FlameOffset.Y)
                : new Vector2(FlameOffset.X, -8 + FlameOffset.Y);

            // Move the animation with the game object.
            burnAnimator.EntityPosition.SetParent(_gameObject.EntityPosition, flameAnchor);

            // Remove the burning sprite if the ai state changes (e.g. by falling down a hole).
            var prevFrameChange = burnAnimator.Animator.OnFrameChange;
            burnAnimator.Animator.OnFrameChange = () =>
            {
                prevFrameChange?.Invoke();
                burnAnimator.AnimationComponent.UpdateSprite();
                if (_aiComponent.Owner.Map == null || _aiComponent.CurrentStateId != "burning")
                    burnAnimator.Map.Objects.DeleteObjects.Add(burnAnimator);
            };
            // Remove the burning sprite if the animation has finished.
            var previousOnFinished = burnAnimator.Animator.OnAnimationFinished;
            burnAnimator.Animator.OnAnimationFinished = () =>
            {
                previousOnFinished?.Invoke();
                FinishBurning();
            };
            // Spawn the burn sprite and register it as an always animate object.
            Game1.GameManager.MapManager.CurrentMap.Objects.SpawnObject(burnAnimator);
            Game1.GameManager.MapManager.CurrentMap.Objects.RegisterAlwaysAnimateObject(burnAnimator);
        }

        private void UpdateBurning()
        {
            if (UpdateLastStateFire)
                _aiComponent.States[_aiComponent.LastStateId].Update?.Invoke();
        }

        private void FinishBurning()
        {
            OnDeath(false);
        }

        private void DamageTick(double time)
        {
            if (_damageBlink)
                _sprite.SpriteShader = (_cooldownTime - time) % (BlinkTime * 2) < BlinkTime ? DamageSpriteShader : _normalShader;
        }

        private void FinishDamage()
        {
            _sprite.SpriteShader = _normalShader;

            if (CurrentLives > 0 &&
                _aiComponent.CurrentStateId != "pieceOfPower" &&
                _aiComponent.LastStateId != "pieceOfPower" &&
                _aiComponent.LastStateId != "knockBack")
            {
                // Go back to the previous state without calling the init methods.
                if (HasDamageState && _returnState)
                {
                    _returnState = false;
                    _aiComponent.ChangeState(_aiComponent.LastStateId, true);
                }
            }
        }

        private void FinishKnockback()
        {
            _sprite.SpriteShader = _normalShader;

            // Go back to the previous state without calling the init methods.
            _aiComponent.ChangeState(_aiComponent.LastStateId, true);
        }

        private void DeathTick(double time)
        {
            // Destroy the enemy when the timer runs out or the velocity is reduced to the threshold.
            // NULL-BODY: without a body there is no velocity to test, so the
            // countdown always runs its full duration before invoking OnDeath.
            if (time <= 0 || (_body != null && time < _cooldownTime - 175 && _body.Velocity.Length() < 0.5f && HitMultiplierX > 0 && HitMultiplierY > 0))
            {
                if (_pieceOfPower && _body != null)
                {
                    _body.Drag = _bodyDrag;
                    _body.DragAir = _bodyDragAir;
                }
                _deathCountdown.Stop();
                OnDeath(false);
            }
        }

        private void UpdateBlink(double time)
        {
            var blinkTime = BlinkTime;
            _sprite.SpriteShader = time % (blinkTime * 2) >= blinkTime ? DamageSpriteShader : _normalShader;
        }

        private void UpdateDeath()
        {
            _deathCount += Game1.DeltaTime;
            if (_deathCount < 100)
                return;
            _deathCount -= 100;

            if (PlayDeathExplosions)
                Game1.AudioManager.PlaySoundEffect("D378-19-13");

            var posX = (int)_gameObject.EntityPosition.X - ExplosionWidth / 2 + Game1.RandomNumber.Next(0, ExplosionWidth) - 8;
            var posY = (int)_gameObject.EntityPosition.Y - (int)_gameObject.EntityPosition.Z + ExplosionOffsetY - ExplosionHeight + Game1.RandomNumber.Next(0, ExplosionHeight) - 8;

            // Spawn the particle effect just before the explosion.
            var explosionAnimation = new ObjAnimator(_gameObject.Map, posX, posY, Values.LayerTop, "Particles/spawn", "run", true);
            _gameObject.Map.Objects.SpawnObject(explosionAnimation);
            _gameObject.Map.Objects.RegisterAlwaysAnimateObject(explosionAnimation);
        }

        public void OnDeathBoss(bool pieceOfPower)
        {
            if (PlayDeathSound)
                Game1.AudioManager.PlaySoundEffect("D370-16-10");

            IsActive = false;

            // Start the death animation.
            _aiComponent.ChangeState("deathBoss");
        }

        private Vector2 GetBodyCenter()
        {
            // If the object has a body return the box's center.
            if (_body != null)
                return _body.BodyBox.Box.Center;

            // If it doesn't have a body fall back to EntityPosition.
            return _gameObject.EntityPosition.Position;
        }

        private float GetBodyZ()
        {
            // If the object has a body, get the body Z-height. If it does not have a body, use EntityPosition as the reference.
            var posZ = _body != null ? _body.Position.Z : _gameObject.EntityPosition.Z;

            // Bodies in the water are at negative Z height, so items that spawned from them either don't jump (shallow water) 
            // or are instantly submerged (deep water). To prevent this, don't let the returned Z value fall below 0.
            return Math.Max(posZ, 0f);
        }

        public void NullifyDeathEffects(bool noItemDrops = false)
        {
            // Disabling item drops entirely is toggleable.
            if (noItemDrops)
                DropTableIndex = 0;

            // Whenever nullifying effects powerups are always disabled.
            SpawnPowerups = false;
            PlayDeathSound = false;
            DeathAnimation = false;
        }

        public void BaseOnDeath(bool pieceOfPower)
        {
            // If the object is already gone then return.
            if (_isDead || _gameObject.Map == null)
                return;

            // Prevent stacking deaths.
            _isDead = true;

            // Delete the object.
            _gameObject.Map.Objects.DeleteObjects.Add(_gameObject);

            // Play explosion death sound.
            if (PlayDeathSound)
            {
                // The explosion sound effect.
                Game1.AudioManager.PlaySoundEffect("D378-19-13");

                // The piece of power explosion is played with the normal sound effect.
                if (_pieceOfPower)
                    Game1.AudioManager.PlaySoundEffect("D370-18-12");
            }
            // Spawn the explosion effect.
            // NULL-BODY: uses the body box center when available, entity position otherwise.
            var bodyCenter = GetBodyCenter();
            bodyCenter.Y += ExplosionOffsetY;

            // Piece of power has an alternate effect. The paramater being fed to "BaseOnDeath" does not work so use the global field.
            if (DeathAnimation)
            {
                if (!_pieceOfPower)
                {
                    var posX = (int)bodyCenter.X;
                    var posY = (int)(bodyCenter.Y);
                    var explosionAnimation = new ObjDeathExplodeEffect(_gameObject.Map, posX, posY, -12, -12, false);
                    Game1.GameManager.MapManager.CurrentMap.Objects.SpawnObject(explosionAnimation);
                    Game1.GameManager.MapManager.CurrentMap.Objects.RegisterAlwaysAnimateObject(explosionAnimation);
                }
                else
                {
                    var posX = (int)bodyCenter.X;
                    var posY = (int)bodyCenter.Y;
                    var explosionAnimation = new ObjDeathExplodeEffect(_gameObject.Map, posX, posY, 0, 0, true);
                    Game1.GameManager.MapManager.CurrentMap.Objects.SpawnObject(explosionAnimation);
                    Game1.GameManager.MapManager.CurrentMap.Objects.RegisterAlwaysAnimateObject(explosionAnimation);
                }
            }
            // Normal kill count should always be incremented.
            Game1.GameManager.KillCount++;

            // Add up the kill counts.
            Game1.GameManager.GuardianAcornCount++;
            Game1.GameManager.PieceOfPowerCount++;

            // Get the item or powerup that is dropped.
            string itemDrop = ItemDropTable.GetItemDrop(DropTableIndex, SpawnPowerups);

            // If an item was returned, drop the item.
            if (!string.IsNullOrEmpty(itemDrop))
            {
                // The fairy is not classified as an "item" so it needs its own spawn.
                GameObject itemObject = itemDrop == "fairy"
                    ? new ObjDungeonFairy(_gameObject.Map, (int)bodyCenter.X, (int)bodyCenter.Y, (int)GetBodyZ())
                    : new ObjItem(_gameObject.Map, (int)bodyCenter.X, (int)bodyCenter.Y, "j", null, itemDrop, null, true);

                // Just in case something weird goes wrong.
                if (itemObject != null)
                {
                    // Spawn the object and set it to the position of the enemy.
                    _gameObject?.Map?.Objects?.SpawnObject(itemObject);
                    itemObject?.EntityPosition?.Set(new Vector3(bodyCenter.X, bodyCenter.Y, GetBodyZ()));
                }
            }
        }
    }
}
