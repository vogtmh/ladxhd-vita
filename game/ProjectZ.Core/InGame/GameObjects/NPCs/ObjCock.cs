using System;
using System.Linq;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    public class ObjCock : GameObjectFollower, IHasVisibility, IHasSpriteVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _drawComponent;
        private readonly BodyDrawShadowComponent _shadowCompnent;
        private readonly CarriableComponent _carriableComponent;
        private readonly CSprite _sprite;

        private ObjCockParticle _objParticle;
        private ObjSpriteShadow _spriteShadow;

        private string _saveKey;
        private const int CarryHeight = 14;
        private int _blinkTime;
        private int _direction;
        private bool _updateCarry;
        private bool _isThrown;
        private bool _slowReturn;
        private bool _freezePlayer;
        private bool _resurrected;
        private bool _highJump;
        private float _bootCooldown;
        private const int FollowDistance = 18;
        private Map.Map _map;

        private bool _worldTeleportRise;
        private float _worldTeleportRiseTime;
        private float _worldTeleportRiseDuration;
        private bool _worldTeleportFallHold;
        private float _worldTeleportFallDelay;

        private Vector2 _pausedVelocity;
        private bool _flightPaused;

        public Vector2 FlyVelocity => _flightPaused 
            ? _pausedVelocity 
            : (_body?.VelocityTarget ?? Vector2.Zero);

        public bool IsVisible { get; internal set; }

        public CSprite Sprite => _sprite;

        public ObjCock() : base("cock") { }

        public ObjCock(Map.Map map, int posX, int posY, string saveKey) : base(map)
        {
            _map = map;

            IsVisible = false;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            _saveKey = saveKey;

            // skeleton was already awakend?
            if (_saveKey != null && Game1.GameManager.SaveManager.GetString(_saveKey) == "1")
            {
                _resurrected = true;
                IsDead = true;
                return;
            }

            // TODO_CHECK: must align with the player body
            _body = new BodyComponent(EntityPosition, -4, -10, 8, 10, 8)
            {
                Bounciness = 0f,
                Gravity = -0.075f,
                Drag = 0.85f,
                IsSlider = true,
                CollisionTypes = Values.CollisionTypes.None,
            };

            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/cock");
            _animator.Play("stand_3");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            // blink for ~1000ms
            _blinkTime = (1000 / AiDamageState.BlinkTime) * AiDamageState.BlinkTime;

            var stateSkeleton = new AiState();
            var stateParticle = new AiState(UpdateParticle) { Init = InitParticle };
            var stateBlinking = new AiState();
            stateBlinking.Trigger.Add(new AiTriggerCountdown(_blinkTime, TickBlink, EndBlink));
            var statePreSpawn = new AiState();
            statePreSpawn.Trigger.Add(new AiTriggerCountdown(1100, null, ToSpawn));
            var stateSpawn = new AiState();
            stateSpawn.Trigger.Add(new AiTriggerCountdown(2500, null, StartFollowing));
            var statePreFollowing = new AiState();
            statePreFollowing.Trigger.Add(new AiTriggerCountdown(100, null, EndPreFollowing));
            var stateFollowing = new AiState(UpdateFollowing) { Init = InitWalk };
            var stateThrown = new AiState(UpdateThrown);
            var statePickedUp = new AiState(UpdatePickedUp);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("skeleton", stateSkeleton);
            _aiComponent.States.Add("particle", stateParticle);
            _aiComponent.States.Add("blinking", stateBlinking);
            _aiComponent.States.Add("preSpawn", statePreSpawn);
            _aiComponent.States.Add("spawn", stateSpawn);
            _aiComponent.States.Add("preFollowing", statePreFollowing);
            _aiComponent.States.Add("following", stateFollowing);
            _aiComponent.States.Add("thrown", stateThrown);
            _aiComponent.States.Add("pickedUp", statePickedUp);

            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6, -14, 12, 14)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, CarryHeight = CarryHeight });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(OcarinaListenerComponent.Index, new OcarinaListenerComponent(OnSongPlayed));
            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(new CBox(EntityPosition, -8, -16, 16, 16, 8), Values.CollisionTypes.Normal));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, _drawComponent = new BodyDrawComponent(_body, _sprite, Values.LayerBottom));
            AddComponent(DrawShadowComponent.Index, _shadowCompnent = new BodyDrawShadowComponent(_body, _sprite) { IsActive = false });

            // no saveKey => spawned by the player in the following state
            if (_saveKey == null)
            {
                ToActiveState();
                _aiComponent.ChangeState("following");
            }
            else
            {
                _animator.Play("skeleton");
                _aiComponent.ChangeState("skeleton");
            }
            _updateCarry = true;
            _carriableComponent.IsActive = false;
        }

        public override void SetPosition(Vector2 position)
        {
            EntityPosition.Set(position);
        }

        public void StartWorldTeleportRise(float durationMs)
        {
            _worldTeleportRise = true;
            _worldTeleportRiseTime = 0;
            _worldTeleportRiseDuration = durationMs;
            _body.IgnoresZ = true;
            _body.Velocity = Vector3.Zero;
            EntityPosition.Z = 0;
        }

        public void HoldForWorldTeleportFall(float delayMs)
        {
            _worldTeleportFallHold = true;
            _worldTeleportFallDelay = delayMs;
            _body.IgnoresZ = true;
            _body.Velocity = Vector3.Zero;

            _direction = 3;
            _animator.Play("stand_" + _direction);
        }

        public override void SetFacingDirection(int direction)
        {
            _direction = direction;
            _animator.Play("stand_" + direction);
        }

        private void SetActive(bool isActive)
        {
            IsActive = isActive;
            IsVisible = isActive;
            _drawComponent.IsActive = isActive;
            _shadowCompnent.IsActive = isActive;
            _carriableComponent.IsActive = isActive;
            if (isActive)
                ((DrawComponent)Components[DrawComponent.Index]).Layer = Values.LayerPlayer;
        }

        private void OnSongPlayed(int songIndex)
        {
            if (songIndex == 2 && _aiComponent.CurrentStateId == "skeleton")
                _aiComponent.ChangeState("particle");
        }

        private void Update()
        {
            // A null map can cause a crash so make sure it isn't null for some reason.
            if (Map == null)
                return;

            // Do not follow the player into dungeons.
            if (Map.IsDungeon && _resurrected)
            {
                SetActive(false);
            }
            if (!Map.IsDungeon && _resurrected)
                SetActive(true);

            // Freeze Link during the spawning sequence.
            if (_freezePlayer)
                MapManager.ObjLink.FreezePlayer();

            // Detect a map change.
            if (Map != _map)
            {
                // Update the map to the new map.
                _map = Map;

                // If a sprite shadow already exists remove it.
                if (_spriteShadow != null)
                    Map.Objects.DeleteObjects.Add(_spriteShadow);

                // Check if the rooster is currently alive.
                if (_aiComponent.States.Keys.ToList().IndexOf(_aiComponent.CurrentStateId) > 5)
                {
                    // Spawn a new sprite shadow on this map and always animate it.
                    _spriteShadow = new ObjSpriteShadow(Map, this, Values.LayerPlayer, "sprshadowm") { ForceDraw = true };
                    Map.Objects.RegisterAlwaysAnimateObject(_spriteShadow);
                }
            }
        }

        private void ToActiveState()
        {
            ((DrawComponent)Components[DrawComponent.Index]).Layer = Values.LayerPlayer;
            ((BodyDrawShadowComponent)Components[DrawShadowComponent.Index]).IsActive = true;
            RemoveComponent(CollisionComponent.Index);
        }

        private void InitParticle()
        {
            // Make Link face the rooster and freeze him in place.
            MapManager.ObjLink.Direction = 1;
            _freezePlayer = true;

            // Change the music to the resurrection music.
            Game1.AudioManager.SetMusic(84, 2);

            // Spawn the rooster's spirit which flies into the body.
            _objParticle = new ObjCockParticle(Map, new Vector2(EntityPosition.X, EntityPosition.Y - 8));
            Map.Objects.SpawnObject(_objParticle);
        }

        private void UpdateParticle()
        {
            // Start blinking when the spirit reaches the skeleton.
            if (!_objParticle.IsRunning())
                _aiComponent.ChangeState("blinking");
        }

        private void TickBlink(double time)
        {
            _sprite.SpriteShader = ((_blinkTime - time) % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime) ? Resources.DamageSpriteShader0 : null;
        }

        private void EndBlink()
        {
            _sprite.SpriteShader = null;
            _aiComponent.ChangeState("preSpawn");
        }

        private void ToSpawn()
        {
            // Spawn an explosion effect.
            var objAnimation = new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y - 8, Values.LayerTop, "Particles/explosionBomb", "run", true);
            Map.Objects.SpawnObject(objAnimation);

            // Play the explosion sound effect and restore the music.
            Game1.AudioManager.PlaySoundEffect("D378-12-0C");
            Game1.AudioManager.SetMusic(-1, 2);

            // Play the spawn animation, change the AI state, and spawn a sprite shadow.
            _animator.Play("spawn");
            _aiComponent.ChangeState("spawn");
            _spriteShadow = new ObjSpriteShadow(Map, this, Values.LayerPlayer, "sprshadowm") { ForceDraw = true };

            // Always animate both the rooster and the sprite shadow.
            Map.Objects.RegisterAlwaysAnimateObject(this);
            Map.Objects.RegisterAlwaysAnimateObject(_spriteShadow);
            ToActiveState();

            // Unlock the achievement.
            AchievementManager.Earn(83);

            // Add the rooster as a follower.
            var itemRooster = new GameItemCollected("rooster") { Count = 1 };
            MapManager.ObjLink.PickUpItem(itemRooster, false);
            Game1.AudioManager.PlaySoundEffect("D368-16-10");
            Game1.GameManager.SaveManager.SetString(_saveKey, "1");
        }

        private void StartFollowing()
        {
            _aiComponent.ChangeState("preFollowing");

            // Allow pickup soon after the rooster has been revived.
            if (!Map.IsDungeon)
                _carriableComponent.IsActive = true;
        }

        private void EndPreFollowing()
        {
            _freezePlayer = false;
            _animator.Play("stand_3");
            _aiComponent.ChangeState("following");
            _resurrected = true;
        }

        private void InitWalk()
        {
            SetThrowState(false);
        }

        private void UpdateFollowing()
        {
            // Raise the rooster in the air during world teleport with Link.
            if (_worldTeleportRise)
            {
                _worldTeleportRiseTime += Game1.DeltaTime;
                _body.VelocityTarget = Vector2.Zero;
                EntityPosition.Z = MathHelper.Clamp(_worldTeleportRiseTime / _worldTeleportRiseDuration, 0, 1) * 128;

                if (_worldTeleportRiseTime >= _worldTeleportRiseDuration)
                    _worldTeleportRise = false;

                return;
            }
            // Hold the rooster in the air during world teleport until Link falls first.
            else if (_worldTeleportFallHold)
            {
                _worldTeleportFallDelay -= Game1.DeltaTime;
                _body.VelocityTarget = Vector2.Zero;
                if (_worldTeleportFallDelay <= 0)
                {
                    _worldTeleportFallHold = false;
                    _body.IgnoresZ = false;
                }
                return;
            }
            // It's more efficent to store ObjLink as a local variable.
            var Link = MapManager.ObjLink;

            // On the first tick only, check if the rooster is alive and can be carried.
            if (_updateCarry && !Map.IsDungeon)
            {
                _carriableComponent.IsActive = (Game1.GameManager.SaveManager.GetString("rooster_respawned", "0") == "1");
                _updateCarry = false;
            }
            // Import properties from Link to apply to rooster.
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            var distance = playerDirection.Length();
            var playerSpeed = MapManager.ObjLink.LastMoveVector.Length();

            // Slowly transition to the full speed.
            var movementSpeed = MathHelper.Clamp((distance - FollowDistance) / 4, -2, 2);
            if (Math.Abs(distance - FollowDistance) > FollowDistance + 4)
                movementSpeed = MathHelper.Clamp(distance / (FollowDistance + 4), -2, 2);

            // Slowly walk back to the player after have been thrown.
            if (_slowReturn)
                movementSpeed = MathHelper.Clamp(movementSpeed, playerSpeed, 1);

            if (movementSpeed > 0 && !_isThrown)
            {
                if (playerDirection != Vector2.Zero)
                    playerDirection.Normalize();

                _body.Velocity.X = playerDirection.X * movementSpeed;
                _body.Velocity.Y = playerDirection.Y * movementSpeed;

                _direction = AnimationHelper.GetDirection(playerDirection);
                _animator.Play("stand_" + _direction);
            }

            // Stop slow return when we reached the player or the player is moving faster away than we are moving.
            if (!_isThrown && (distance <= FollowDistance || playerSpeed > 1))
                _slowReturn = false;

            // Fly over deep water.
            if ((_body.CurrentFieldState & MapStates.FieldStates.DeepWater) != 0)
            {
                _body.IsGrounded = false;
                _body.IgnoresZ = true;
                var targetPosZ = 7.5f + MathF.Sin(((float)Game1.TotalGameTime / 1000) * MathF.PI * 2) * 1.5f;
                EntityPosition.Z = AnimationHelper.MoveToTarget(EntityPosition.Z, targetPosZ, 1 * Game1.TimeMultiplier);
            }
            else
            {
                _body.IgnoresZ = false;
            }

            // If Link is jumping then store that jump as a high jump the next time the rooster is grounded.
            if (Link.IsJumpingState() && _body.IsGrounded && (Link.RailJumpAmount() > 0.45f || (!Link.IsRailJumping() && Link.Body.Velocity.Z < 0)))
                _highJump = true;
            
            // When running into a wall, make the chicken jump.
            if (_bootCooldown <= 0 && MapManager.ObjLink.CurrentState == ObjLink.State.BootKnockback)
            {
                _highJump = true;
                _bootCooldown = 350f;
            }
            // Limit the jump to every 350ms since it can trigger multiple times on one knockback.
            if (_bootCooldown > 0)
                _bootCooldown -= Game1.DeltaTime;

            // When the rooster hits the ground force him to jump again.
            if (_body.IsGrounded)
            {
                var jumpHeight = MathHelper.Clamp(distance / 18, 1, 2);

                // While returning from a throw do not jump high.
                if (_slowReturn)
                    jumpHeight = 1;

                // If a jump has been stored or it Link jumped on the same frame the chicken was grounded.
                else if (_highJump || Link.IsJumpingState() || Link.IsRailJumping())
                {
                    jumpHeight = 2.25f;
                    _highJump = false;
                }
                // Force the jump.
                _body.Velocity.Z = jumpHeight;
            }
        }

        public void TargetVelocity(Vector2 targetVelocity, float maxSpeed, int direction)
        {
            // Move towards the target velocity.
            var target = _body.VelocityTarget + targetVelocity * 0.05f * Game1.TimeMultiplier;
            if (target.Length() > maxSpeed)
            {
                target.Normalize();
                target *= maxSpeed;
            }
            _body.VelocityTarget = target;

            _direction = direction;
            _animator.Play("stand_" + _direction);
        }

        private void UpdatePickedUp()
        {
            if (!MapManager.ObjLink.IsFlying())
                MapManager.ObjLink.StartFlying(this);

            Game1.AudioManager.PlaySoundEffect("D378-45-2D", false);

            // move up
            var targetPosZ = 36 + MathF.Sin(((float)Game1.TotalGameTime / 450) * MathF.PI * 2) * 1.5f;
            EntityPosition.Z = AnimationHelper.MoveToTarget(EntityPosition.Z, targetPosZ, 0.5f * Game1.TimeMultiplier);

            // lift the player up
            if (EntityPosition.Z > CarryHeight)
                MapManager.ObjLink.EntityPosition.Z = EntityPosition.Z - CarryHeight;
        }

        private void UpdateThrown()
        {
            if (_body.IsGrounded)
            {
                _aiComponent.ChangeState("following");
                _body.Velocity.X = 0;
                _body.Velocity.Y = 0;
            }
        }

        private void SetThrowState(bool thrown)
        {
            _isThrown = thrown;
            _carriableComponent.Thrown = thrown;

            _body.DragAir = thrown ? 0.975f : 0.85f;
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            _body.IgnoresZ = true;
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
            _body.CollisionTypes = MapManager.ObjLink.Body.CollisionTypes;

            _animator.SpeedMultiplier = 2.0f;
            _aiComponent.ChangeState("pickedUp");
            EntityPosition.AddPositionListener(typeof(ObjCock), OnPositionChange);

            return new Vector3(EntityPosition.X, EntityPosition.Y, EntityPosition.Z);
        }

        private bool CarryUpdate(Vector3 position)
        {
            EntityPosition.Set(new Vector3(position.X, position.Y, position.Z));
            return true;
        }

        private void CarryThrow(Vector2 direction)
        {
            var lastVelocity = FlyVelocity;
            _body.Velocity = new Vector3(direction.X, direction.Y, 0);
            MapManager.ObjLink.StopFlying(lastVelocity);
        }

        public void PauseFlying(bool paused)
        {
            if (_body == null || paused == _flightPaused)
                return;

            _flightPaused = paused;

            if (paused)
            {
                _pausedVelocity = _body.VelocityTarget;
                _body.VelocityTarget = Vector2.Zero;
            }
            else
                _body.VelocityTarget = _pausedVelocity;
        }

        public void StopFlying()
        {
            _body.IgnoresZ = false;
            _body.IsGrounded = false;
            _body.VelocityTarget = Vector2.Zero;
            _body.CollisionTypes = Values.CollisionTypes.None;

            _slowReturn = true;
            SetThrowState(true);
            _animator.SpeedMultiplier = 1.0f;
            _aiComponent.ChangeState("thrown");
            EntityPosition.RemovePositionListener(typeof(ObjCock));
        }

        private void OnPositionChange(CPosition newPosition)
        {
            if (MapManager.ObjLink.IsFlying())
                MapManager.ObjLink.SetPosition(new Vector2(newPosition.X, newPosition.Y));
        }

        public void BorrowRooster()
        {
            _animator.Play("stand_3");
            Game1.AudioManager.PlaySoundEffect("D368-16-10");
            _aiComponent.ChangeState("following");
            _carriableComponent.IsActive = true;
            RemoveComponent(CollisionComponent.Index);
            ((DrawComponent)Components[DrawComponent.Index]).Layer = Values.LayerPlayer;
        }
    }
}
