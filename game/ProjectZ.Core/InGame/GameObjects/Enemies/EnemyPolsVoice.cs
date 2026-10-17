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
    internal class EnemyPolsVoice : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly DamageFieldComponent _damageField;

        private float _jumpVelocity = 1.0f;
        private int _lives = EnemyLives.PolsVoice;
        private int _dropIndex = 0;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyPolsVoice() : base("pols voice") { }

        public EnemyPolsVoice(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/pols voice");
            _animator.Play("jump");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -6, -10, 12, 10, 8)
            {
                MoveCollision  = OnMoveCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes     = Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f,
                MaxJumpHeight = 8f,
                Gravity = -0.05f,
                Drag = 0.75f,
                DragAir = 0.8f
            };

            var stateWaiting = new AiState { Init = InitWaiting };
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(EndWaiting, 500, 750));
            var stateJumping = new AiState(UpdateJumping) { Init = InitJump };

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("jumping", stateJumping);
            _aiComponent.ChangeState("jumping");

            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex, true, false);
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, animationComponent, 3300, 900) { ShakeOffset = 1, SilentStateChange = false, ReturnState = "waiting", OnStun = OnStun, OnStunRelease = OnStunRelease };

            new AiFallState(_aiComponent, _body, null, null, 100);
            new AiDeepWaterState(_body);

            var damageBox   = new CBox(EntityPosition, -3, -8, 0, 6, 6, 16);
            var hittableBox = new CBox(EntityPosition, -6, -12, 0, 12, 12, 8, true);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 10 });
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit) { StunHookshot = true, StunPowder = true, StunBoomerang = true, BombMultiplier = true, ThrownMultiplier = true });
            AddComponent(OcarinaListenerComponent.Index, new OcarinaListenerComponent(OnSongPlayed));
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");

            // Reset the achievement tracking.
            if (Game1.GameManager.SaveManager.ContainsValue("pols_voice_achievement"))
                Game1.GameManager.SaveManager.RemoveInt("pols_voice_achievement");
        }

        public override void Reset()
        {
            if (_carriableComponent.IsPickedUp)
                return;

            _aiComponent.ChangeState("waiting");
            _aiComponent.ChangeState("waiting");
            _carriableComponent.IsActive = false;
            _damageState.CurrentLives = EnemyLives.PolsVoice;
            _isThrown = false;
            _body.Gravity = -0.05f;
            _body.Drag = 0.75f;
            _body.DragAir = 0.8f;
            _body.Bounciness = 0.65f;
            _aiStunnedState.EndStun();
        }

        private void OnSongPlayed(int songIndex)
        {
            if (songIndex == 0)
                _damageState.BaseOnDeath(false);

            // The achievement in dungeon 6 is to kill all 3 Pols Voice with the ocarina.
            if (songIndex == 0 && Map.MapName == "dungeon6.map")
            {
                // Get how many have been killed thus far.
                int deathCount = Game1.GameManager.SaveManager.GetInt("pols_voice_achievement", 0) + 1;
                Game1.GameManager.SaveManager.SetInt("pols_voice_achievement", deathCount);

                // When all three are killed give the achievement.
                if (deathCount >= 3)
                {
                    AchievementManager.Earn(78);
                    Game1.GameManager.SaveManager.RemoveInt("pols_voice_achievement");
                }
            }
        }

        private void OnStun()
        {
            _carriableComponent.IsActive = true;
            _damageField.IsActive = false;
        }

        private void OnStunRelease()
        {
            _carriableComponent.IsActive = false;
            _damageField.IsActive = true;
        }

        private void Update()
        {
            // Check if the enemy was thrown.
            if (_isThrown)
            {
                // Deal a hit to whatever it comes in contact with.
                var pos  = new Vector3(EntityPosition.X - 7, EntityPosition.Y - 14, EntityPosition.Z);
                var size = new Vector3(14, 14, 8);
                var throwBox = new Box(pos, size);

                // Find objects to hit when thrown.
                if (Map.Objects.Hit(this, throwBox.Center, throwBox, HitType.ThrownObject, 2, false) != 0)
                {
                    // Bounce off the object when hit.
                    _body.Velocity.X = -_body.Velocity.X * 0.5f;
                    _body.Velocity.Y = -_body.Velocity.Y * 0.5f;
                }
            }
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            _body.IsActive = false;
            _body.BodyBox = new CBox(EntityPosition, -4, -8 + _offsetY, 8, 8, 12);
            return new Vector3(EntityPosition.X, EntityPosition.Y - _offsetY, EntityPosition.Z);
        }

        private bool CarryUpdate(Vector3 newPosition)
        {
            // Reset the stun state as it's being carried.
            _aiStunnedState.ResetStun();

            EntityPosition.X = newPosition.X;
            EntityPosition.Y = newPosition.Y - _offsetY;
            EntityPosition.Z = newPosition.Z;

            EntityPosition.NotifyListeners();
            return true;
        }

        private void CarryThrow(Vector2 velocity)
        {
            _isThrown = true;
            _carriableComponent.Thrown = true;
            _body.IsGrounded = false;
            _body.IsActive = true;
            _body.Velocity = new Vector3(velocity.X, velocity.Y, 0) * 2.0f;
            _body.Level = MapStates.GetLevel(MapManager.ObjLink.Body.CurrentFieldState);
            _body.Gravity = -0.25f;
            _body.Drag = 0.8f;
            _body.DragAir = 0.9f;
            _body.Bounciness = 0f;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
            {
                _body.VelocityTarget = Vector2.Zero;
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);
            }

            return true;
        }

        private void TryReleaseStun()
        {
            if (!_aiStunnedState.Active)
                _damageField.IsActive = true;
        }

        private void InitWaiting()
        {
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("stand");
            _damageField.IsActive = true;
            TryReleaseStun();
        }

        private void EndWaiting()
        {
            if (_body.FieldRectangle.Intersects(MapManager.ObjLink.BodyRectangle))
                _aiComponent.ChangeState("jumping");
            TryReleaseStun();
        }

        private void InitJump()
        {
            _animator.Play("jump");

            // start jumping
            _body.Velocity.Z = _jumpVelocity;
            _body.Bounciness = 0f;

            var jumpDirection = Vector2.Zero;

            if (Game1.RandomNumber.Next(0, 3) == 0)
            {
                // jump towards the player
                var direction = new Vector2(
                    MapManager.ObjLink.PosX - EntityPosition.X,
                    MapManager.ObjLink.PosY - EntityPosition.Y);

                if (direction != Vector2.Zero)
                {
                    direction.Normalize();
                    jumpDirection = direction;
                }
            }
            else
            {
                var randomDirection = Game1.RandomNumber.Next(0, 100) / 100f * Math.PI * 2;
                jumpDirection = new Vector2((float)Math.Sin(randomDirection), (float)Math.Cos(randomDirection));
            }
            _body.VelocityTarget = jumpDirection * 0.75f;
        }

        private void UpdateJumping()
        {
            if (_body.IsGrounded)
            {
                _animator.Play("stand");
                _aiComponent.ChangeState("waiting");
            }
            TryReleaseStun();
        }

        private void StartStun()
        {
            if (_body.Velocity.Z > 0)
                _body.Velocity.Z = 0;
            _body.VelocityTarget = Vector2.Zero;
            _body.Bounciness = 0.65f;
            _aiStunnedState.StartStun();
            _animator.Play("jump");
            _damageField.IsActive = false;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Prevent multiple concurrent hits.
            if (_damageState.IsInDamageState())
                return Values.HitCollision.None;

            // Reset these no matter the hit.
            _damageState.HitMultiplierX = 5;
            _damageState.HitMultiplierY = 5;
            _aiStunnedState.StunKnockbackSpeed = 4.0f;

            // Bow and Magic rod can hit it, thrown object and bombs are 1 shots.
            if (hitType == HitType.MagicRod || hitType == HitType.ThrownObject || hitType == HitType.Bow || hitType == HitType.Bomb || hitType == HitType.BombArrow)
                return _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // Magic Powder, Hookshot, and Boomerang stuns the Pols Voice.
            if (hitType == HitType.MagicPowder || hitType == HitType.Hookshot || hitType == HitType.Boomerang)
            {
                // Powder does not knockback when stunning.
                if (hitType == HitType.MagicPowder)
                {
                    _damageState.HitMultiplierX = 0;
                    _damageState.HitMultiplierY = 0;
                    _aiStunnedState.StunKnockbackSpeed = 0;
                }
                direction *= 0.25f;
                StartStun();
                var hitState = _damageState.HitKnockBack(gameObject, direction, hitType, pieceOfPower, false);
                Game1.AudioManager.PlaySoundEffect("D360-03-03");
                return hitState;
            }
            _damageState.HitKnockBack(gameObject, direction, hitType, pieceOfPower, false);

            // Play the "bump" sound or the "piece of power" hit.
            if (pieceOfPower)
                Game1.AudioManager.PlaySoundEffect("D370-17-11");
            else
                Game1.AudioManager.PlaySoundEffect("D360-09-09");

            // Don't deal damage and consume a poking attack.
            return Values.HitCollision.Blocking;
        }

        private void OnMoveCollision(Values.BodyCollision direction)
        {
            if (_isThrown && (direction & Values.BodyCollision.Floor) != 0)
            {
                _isThrown = false;
                _carriableComponent.Thrown = false;
                _body.Gravity = -0.05f;
                _body.Drag = 0.75f;
                _body.DragAir = 0.8f;
                _body.Bounciness = 0.65f;
                _aiStunnedState.ResetBodyState();
            }
        }
    }
}
