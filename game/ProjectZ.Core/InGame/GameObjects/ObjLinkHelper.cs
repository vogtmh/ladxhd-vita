using System;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.NPCs;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects
{
    public partial class ObjLink
    {
        //-------------------------------------------------------------------------------------------------------
        // State tracking functions: check multiple state types at once by category.
        public bool IsAttackingState() =>
            CurrentState == State.Attacking ||
            CurrentState == State.AttackBlocking ||
            CurrentState == State.AttackJumping ||
            CurrentState == State.AttackSwimming;
        public bool IsChargingState() =>
            CurrentState == State.Charging ||
            CurrentState == State.ChargeBlocking ||
            CurrentState == State.ChargeJumping ||
            CurrentState == State.ChargeSwimming;
        public bool IsBlockingState() =>
            CurrentState == State.Blocking ||
            CurrentState == State.AttackBlocking ||
            CurrentState == State.ChargeBlocking ||
            CurrentState == State.BlockPushing;
        public bool IsSwimmingState() =>
            CurrentState == State.Swimming ||
            CurrentState == State.AttackSwimming ||
            CurrentState == State.ChargeSwimming;
        public bool IsJumpingState() =>
            CurrentState == State.Jumping ||
            CurrentState == State.AttackJumping ||
            CurrentState == State.ChargeJumping;
        public bool IsPushingState() =>
            CurrentState == State.Pushing ||
            CurrentState == State.BlockPushing;
        public bool IsShowingInstrument() =>
            CurrentState == State.ShowInstrumentPart0 ||
            CurrentState == State.ShowInstrumentPart1 ||
            CurrentState == State.ShowInstrumentPart2 ||
            CurrentState == State.ShowInstrumentPart3;
        public bool IsShowingCloak() =>
            CurrentState == State.CloakShow0 ||
            CurrentState == State.CloakShow1;
        public bool IsShowingSword() =>
            CurrentState == State.SwordShow0 ||
            CurrentState == State.SwordShow1 ||
            CurrentState == State.SwordShow2 ||
            CurrentState == State.SwordShowLv2;

        //-------------------------------------------------------------------------------------------------------
        // One-line "getter" functions.
        public bool IsInWater2D()   => Is2DMode && _inWater;
        public bool IsDiving()      => CurrentState == State.Swimming && _diveCounter > 0;
        public bool IsGrounded()    => _body.IsGrounded && !_railJump && !_isFlying;
        public bool IsRailJumping() => _railJump;
        public bool IsHoleAbsorb()  => _isFallingIntoHole;
        public bool IsDashing()     => _bootsRunning;
        public bool IsStunned()     => CurrentState == State.Stunned;
        public bool IsTrapped()     => _isTrapped;
        public bool IsFlying()      => _isFlying && CurrentState == State.Carrying;
        public bool IsDying()       => CurrentState == State.Dying;
        public bool IsClimbing()    => _isClimbing;
        public bool IsCarrying()    => _carriedComponent != null;
        public bool IsUsingHookshot()    => CurrentState == State.Hookshot;
        public bool DrownedInLava()      => _drownedInLava;
        public Vector2 GetSwimVelocity() => _swimVelocity;
        public ObjMarin GetMarin()       => _objMarin;

        //-------------------------------------------------------------------------------------------------------
        // One-line "setter" functions.
        public void SetBowWowFollower(ObjBowWow bowWow) => _objBowWow = bowWow;
        public void LinkWalking(bool walk) => _isWalking = walk;
        public void ToggleBlockButton(bool toggle) => _blockButton = toggle;
        public void ToggleLowHealthBeep(bool toggle) => _enableHealthBeep = toggle;

        //-------------------------------------------------------------------------------------------------------
        // Enable/Disable game states by using a SaveManager key-value pair.
        public void DisableOptions(bool disable) => Game1.GameManager.SaveManager.SetString("disable_options", disable ? "1" : "0");
        public void DisableInventory(bool disable) => Game1.GameManager.SaveManager.SetString("disable_inventory", disable ? "1" : "0");
        public void FreezeAnimations(bool freeze)  => Game1.GameManager.SaveManager.SetString("freezeGame", freeze ? "1" : "0");

        //-------------------------------------------------------------------------------------------------------
        // Set by objects that take exclusive control of Link's position, so the field scroll
        // push does not fight them. Cleared by the same object when it is finished.
        public bool PositionControlled;

        //-------------------------------------------------------------------------------------------------------
        // Various functions to set, manipulate, or reference "Direction".
        private int ReverseDirection(int direction) => (direction + 2) % 4;
        
        public void SetWalkingDirection(int direction)
        {
            // Used in various objects to force Link's current facing.
            Direction = direction;
            UpdateAnimation();
        }

        public int ToDirection(Vector2 direction)
        {
            // Fail safe in case the impossible happens.
            if (direction == Vector2.Zero) { return Direction; }

            // If player wants old style movement.
            if (GameSettings.OldMovement)
                return ToDirectionClassic(direction);

            // Bias towards horizontal (0/2) or bias towards the vertical (1/3).
            float bias = (Direction == 0 || Direction == 2) ? 1.05f : 0.95f;

            // Prefer staying in current axis when movement is ambiguous.
            if (Math.Abs(direction.X) * bias > Math.Abs(direction.Y))
                return direction.X > 0 ? 2 : 0;
            else
                return direction.Y > 0 ? 3 : 1;
        }

        public int ToDirectionClassic(Vector2 direction)
        {
            // Get angle in degrees 0-360.
            float angle = (float)Math.Atan2(direction.Y, direction.X);
            float deg = MathHelper.ToDegrees(angle);
            if (deg < 0) { deg += 360f; }

            // 0:Left 1:Up 2:Right 3:Down
            return deg switch
            {
                180 => 0,
                270 => 1,
                0   => 2,
                90  => 3,
                _   => Direction
            };
        }

        public int GetCorrectDirection(int direction)
        {
            int dir = direction;

            if (Map.Is2dMap)
            {
                Vector2 input = ControlHandler.GetMoveVector2();
                if (input.X != 0 && input.Y != 0)
                    dir = input.X > 0 ? 2 : 0;
            }
            return dir;
        }

        //-------------------------------------------------------------------------------------------------------
        // Used in instances projectiles are blocked with the shield. This determines if the projectile should
        // be reflected if the Mirror Shield is obtained and the user has enabled "Mirror Shield Reflects". 

        public bool Reflected(int direction = -1, bool[] extraConditions = null)
        {
            // Check if there are additional conditions to meet.
            bool extra = extraConditions != null;

            // Set the size of the array of checks.
            int arrayBase = 5;
            int arraySize = extra ? arrayBase + extraConditions.Length : arrayBase;

            // Set the initial checks that must always exist.
            bool[] reflectProjectile = new bool[arraySize];
            reflectProjectile[0] = GameSettings.MirrorReflects;
            reflectProjectile[1] = Game1.GameManager.ShieldLevel == 2;
            reflectProjectile[2] = !MapManager.ObjLink.InDamageState;
            reflectProjectile[3] = IsBlockingState();

            // The direction check is a weird one that is optional. If it's not defined, then return
            // true. If it is defined, it must be coming in the opposite direction of Link's facing.
            reflectProjectile[4] = direction == -1 
                ? true 
                : Direction == ReverseDirection(direction);

            // If there are extra conditions add them to the array.
            if (extra) for (int i = arrayBase; i < arraySize; i++) 
                reflectProjectile[i] = extraConditions[i-arrayBase];

            // If any check fails then do not reflect.
            for (int i = 0; i < arraySize; i++)
            {
                if (!reflectProjectile[i]) 
                    return false;
            }
            // We passed the checks so return true.
            return true;
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by "DialogPath" and the function to prevent egg entry when a follower
        // is with Link to temporarily lock input but still update his animations.
        public void SeqLockPlayer()
        {
            UpdatePlayer = false;

            if (Map.Is2dMap)
                UpdateAnimation2D();
            else
                UpdateAnimation();
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by "ObjBat" to lock the player in place during item storage upgrade.
        private bool _isLocked;

        public void LockPlayer() => _isLocked = true;

        //-------------------------------------------------------------------------------------------------------
        // Used by "ObjMermaid" to shorten Link's dive counter.
        public void ShortenDive() => _diveCounter = 350;

        //-------------------------------------------------------------------------------------------------------
        // Used in "ObjColorJumpTile" to force Link to jump when landing on a tile.
        public void StartJump()
        {
            if (CurrentState != State.Dying && CurrentState != State.PickingUp)
                Jump(force:true);
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by "ObjDoorEgg" to freeze animations.
        public void FreezeAnimationState()
        {
            CurrentState = State.Frozen;
            Animation.Pause();
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by "EnemyBomber" to detect if the sword is within dodge range.
        public bool IsSwordThreatActive() => 
            CurrentState == State.Attacking || 
            _bootsRunning && CarrySword;

        //-------------------------------------------------------------------------------------------------------
        // Used by "EnemyBuzzBlob" to shock the player.
        public void ShockPlayer(int time)
        {
            _bootsHolding = false;
            _bootsRunning = false;
            _bootsCounter = 0;

            ReturnToIdle();

            Game1.GameManager.UseShockEffect = true;

            if (GameSettings.ScreenShake)
                Game1.GameManager.ShakeScreen(time, 1.50f, 0.00f, 10.00f, 0.00f);

            Game1.GameManager.InflictDamage(4);
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by "EnemyLikeLike" and "EnemyAntiKirby" to temporarily trap Link.
        public void FreeTrappedPlayer() => _isTrapped = false;

        public void TrapPlayer(bool disableItems = false)
        {
            _isTrapped = true;
            _trappedDisableItems = disableItems;
            _trapInteractionCount = 8;
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by "EnemyLikeLike" to steal the shield if it's equipped to a usable button.
        public bool StealShield()
        {
            for (var i = 0; i < 6; i++)
            {
                if (Game1.GameManager.Equipment[i] != null && Game1.GameManager.Equipment[i].Name == "shield")
                {
                    Game1.GameManager.RemoveItem("shield", 1);
                    Game1.GameManager.SaveManager.RemoveString("store_shield");

                    CarryShield = false;
                    return true;
                }
            }
            return false;
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by "EnemyVacuum" to rotate the player while it is active.
        public void RotatePlayer()
        {
            if (!_isRotating)
            {
                _rotateDirection = Direction;
                _isRotating = true;
            }
            _rotationCounter += Game1.DeltaTime;
            if (_rotationCounter > 133)
            {
                _rotationCounter -= 133;
                _rotateDirection = (_rotateDirection + 1) % 4;

                var shieldString = CarryShield
                    ? (Game1.GameManager.ShieldLevel == 2 ? "ms_" : "s_")
                    : "_";

                if (IsChargingState() || _bootsRunning)
                {
                    Animation.Play("stand" + shieldString + _rotateDirection);
                    PlayWeaponAnimation("stand", _rotateDirection);
                }
            }
        }
        public void StopRotating()
        {
            _wasRotating = _isRotating;
            _isRotating = false;
        }

        //-------------------------------------------------------------------------------------------------------
        // Used to slow down Link's walking speed (EnemyGel, ObjStairs).
        public void SlowDown(float speed)
        {
            if (!IsJumpingState())
                _currentWalkSpeed = speed;
        }

        //-------------------------------------------------------------------------------------------------------
        // Stun the player when ground is slammed (MBossArmosKnight, MBossStoneHinox, BossSlimeEye).
        public void GroundStun(int stunTime = 1250)
        {
            if (!IsJumpingState() && _body.IsGrounded)
                Stun(stunTime);
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by "MBossHinox" to grab and throw Link.
        private bool _isGrabbed;

        public void StartGrab() => _isGrabbed = true;

        public void EndGrab() => _isGrabbed = false;

        public void StartThrow(Vector3 direction)
        {
            _body.Velocity = direction;
            _body.IsGrounded = false;
            _body.JumpStartHeight = 0;
        }

        //-------------------------------------------------------------------------------------------------------
        // Stun the player for an amount of time (MBossHinox, MBossBlaino).
        public void Stun(int stunTime, bool particle = false)
        {
            if (IsDying())
                return;

            CurrentState = State.InitStunned;

            _stunnedParticles = particle;
            _stunnedCounter = stunTime;
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by dungeon 8 boss "MBossBlaino" to knock the player to the entrance of the dungeon. 
        public void Knockout(Vector2 direction, string resetDoor)
        {
            if (CurrentState == State.Knockout)
                return;

            if (Camera.ClassicMode)
                Camera.SnapCameraTimer = 100;

            CurrentState = State.Knockout;

            MapTransitionStart = MapManager.ObjLink.Position;
            MapTransitionEnd = MapManager.ObjLink.Position + direction * 80;
            TransitionOutWalking = false;

            var transitionSystem = ((MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)]);
            transitionSystem.AppendMapChange(Map.MapName, resetDoor, false, false, Color.White, false);
            transitionSystem.StartKnockoutTransition = true;
        }

        //-------------------------------------------------------------------------------------------------------
        // Return to idle or to rafting if that was the player was rafting before
        private void ReturnToIdle()
        {
            if (CurrentState == State.FinalStand)
                return;

            if (_isRafting)
                CurrentState = State.Rafting;
            else
                CurrentState = State.Idle;
        }

        //-------------------------------------------------------------------------------------------------------
        // Used in "FinalSequence" to start the ending.
        public void InitEnding()
        {
            CurrentState = State.Sequence;
            Animation.Play("stand_1");
        }

        //-------------------------------------------------------------------------------------------------------
        // Used to detect bombable walls or locked doors when poking with the sword.
        private bool IsDestroyableWall(Box box)
        {
            _destroyableWallList.Clear();
            Map.Objects.GetComponentList(_destroyableWallList, (int)box.X, (int)box.Y, (int)box.Width + 1, (int)box.Height + 1, CollisionComponent.Mask);

            var collidingBox = Box.Empty;
            foreach (var gameObject in _destroyableWallList)
            {
                var collisionObject = gameObject.Components[CollisionComponent.Index] as CollisionComponent;

                if (collisionObject.Collision(box, 0, 0, ref collidingBox))
                {
                    // The poked object is a bombable wall.
                    if ((collisionObject.CollisionType & Values.CollisionTypes.Destroyable) != 0)
                        return true;

                    // The poked object is a locked dungeon door.
                    if (gameObject is ObjDungeonDoor door && (door.GetMode() == 1 || door.GetMode() == 3))
                        return true;
                }
            }
            return false;
        }

        //-------------------------------------------------------------------------------------------------------
        // Used by the "Modifier Settings" page to adjust Link's overall movement speed.
        public void AlterMoveSpeed(float amount)
        {
            float walkSpeedBase = 1.0f;
            float walkSpeedPoPBase = 1.25f;
            float bootsRunningSpeed = 2.0f;
            float swimSpeed = 0.5f;
            float swimSpeedA = 1.0f;
            float bootsMaxSpeed = 2.0f;
            float climbSpeed = 0.7f;
            float maxSwimSpeed2D = 0.50f;
            float flyingSpeed = 0.50f;

            WalkSpeed = walkSpeedBase + amount;
            WalkSpeedPoP = walkSpeedPoPBase + amount;
            BootsRunningSpeed = bootsRunningSpeed + amount;
            SwimSpeed = swimSpeed + amount;
            SwimSpeedA = swimSpeedA + amount;
            _bootsMaxSpeed = bootsMaxSpeed + amount;
            ClimbSpeed = climbSpeed + amount;
            MaxSwimSpeed2D = maxSwimSpeed2D + amount;
            _flyingSpeed = flyingSpeed + amount;
        }
    }
}
