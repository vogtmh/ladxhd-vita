using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    public class ObjBowWow : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiTriggerSwitch _changeDirectionSwitch;
        private readonly AiTriggerUpdate _trackSwamp;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _drawComponent;
        private readonly BodyDrawShadowComponent _shadowComponent;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly ObjChain _chain;
        private readonly List<GameObject> _enemyList = new List<GameObject>();
        private GameObject _enemyTarget;

        private Vector2 _chainPull;
        private Vector2 _origin;
        private Vector2 _currentDirectionOffset;
        private Vector2 _treasurePosition;

        private int _chainMax;
        private int _direction;
        private float _outsideCounter;
        private bool _followMode;
        private float _attackPlayerCooldown;
        private float _idleBobTimer;

        private float _worldTeleportRiseTime;
        private float _worldTeleportRiseDuration;
        private float _worldTeleportFallDelay;

        private string _lastTreasure;

        private int _swampKillCount;
        private bool _swampTracking;
        private bool _swampAchievementEarned;

        public BodyComponent Body => _body;

        public ObjBowWow() : base("bowwow") { }

        private ObjBowWowWater _waterGraphic;

        public ObjBowWow(Map.Map map, int posX, int posY, string mode) : base(map)
        {
            EntityPosition = new CPosition(posX, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            _origin = new Vector2(posX + 8, posY + 8);

            var state = Game1.GameManager.SaveManager.GetString("bowWow");

            // cave bowwow
            if (mode == "cave")
            {
                // only spawn at the right state
                if (state == "1")
                {
                    EntityPosition.Set(new Vector2(EntityPosition.X - 8, EntityPosition.Y));
                    _followMode = false;
                }
                else
                {
                    IsDead = true;
                }
            }
            else
            {
                // bowwow is in the cave
                if (state == "1")
                    IsDead = true;
                // bowwow is following the player
                if (state == "2" || state == "3" || state == "5")
                    _followMode = true;
                // bowwow is home
                if (state == "4")
                    _followMode = false;
            }

            if (string.IsNullOrEmpty(mode) && state != "2" && state != "3" && state != "5")
                IsDead = true;

            if (IsDead)
                return;

            _body = new BodyComponent(EntityPosition, -7, -10, 14, 10, 16)
            {
                IgnoreHoles = true,
                Gravity = -0.175f,
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY)
            };

            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/BowWow");
            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            var stateIAttack = new AiState(UpdateIAttack);
            stateIAttack.Trigger.Add(new AiTriggerCountdown(400, null, ToAttack));
            var stateIdle = new AiState(UpdateIdle);
            stateIdle.Trigger.Add(new AiTriggerRandomTime(EndIdle, 500, 1500));
            var stateWalking = new AiState(UpdateWalking) { Init = InitWalking };
            stateWalking.Trigger.Add(new AiTriggerRandomTime(EndWalking, 500, 1000));
            stateWalking.Trigger.Add(_changeDirectionSwitch = new AiTriggerSwitch(250));
            var stateAttack = new AiState(UpdateAttack);
            var stateAttackPlayer = new AiState(UpdateAttackPlayer); 
            var stateTreasure = new AiState(UpdateTreasure);
            var stateWorldTeleportRise = new AiState(UpdateWorldTeleportRise);
            var stateWorldTeleportFall = new AiState(UpdateWorldTeleportFall);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("iattack", stateIAttack);
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("attack", stateAttack);
            _aiComponent.States.Add("attackPlayer", stateAttackPlayer);
            _aiComponent.States.Add("treasure", stateTreasure);
            _aiComponent.States.Add("worldTeleportRise", stateWorldTeleportRise);
            _aiComponent.States.Add("worldTeleportFall", stateWorldTeleportFall);

            // If the swamp achievement has not been earned then track it.
            _swampAchievementEarned = AchievementManager.IsEarned(25);

            if (!_swampAchievementEarned)
                _aiComponent.Trigger.Add(_trackSwamp = new AiTriggerUpdate(SwampAchievementTracking));

            if (Game1.RandomNumber.Next(0, 100) < 50)
                _aiComponent.ChangeState("iattack");
            else
                _aiComponent.ChangeState("walking");

            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, _drawComponent  = new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, _shadowComponent = new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 12, ShadowHeight = 5 });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit));
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));

            Map.Objects.SpawnObject(_chain = new ObjChain(map, _origin));
            _currentDirectionOffset = AnimationHelper.DirectionOffset[_direction];

            SetFollowMode(_followMode);
            _waterGraphic = new ObjBowWowWater(Map, posX, posY, this);

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            AchievementSaveLoadWorkaround();
        }

        private void AchievementSaveLoadWorkaround()
        {
            // Guard against null values.
            var objLink = MapManager.ObjLink;
            if (objLink?.SavePosition is not { } savePos)
                return;

            // A very specific fix that determines if the player loaded the game while outside of the dungeon 2 entrance. It is
            // possible for BowWow to immediately start eating enemies, so the key needs to be ready immediately. The key that is
            // used to start the achievement is usually set through "ObjButtonLeave" objects, which is not applicable here.
            var savePosition = new Vector2(objLink.SavePosition.X, objLink.SavePosition.Y);

            if (!_swampAchievementEarned && savePos == new Vector2(712, 298))
                Game1.GameManager?.SaveManager?.SetString("swamp_achievement", "1");
        }

        private void OnKeyChange()
        {
            // Check the states where BowWow is with the player.
            var state = Game1.GameManager.SaveManager.GetString("bowWow");
            if (state != null && (state == "2" || state == "3" || state == "5"))
                SetFollowMode(true);

            // The rest of this stuff tracks the swamp achievement.
            if (_swampAchievementEarned)
                return;

            // Check if the achievement values have been set. 
            var swampCheck = Game1.GameManager.SaveManager.GetString("swamp_achievement");

            // When entering the swamp enable tracking, and disable when leaving.
            if (!string.IsNullOrEmpty(swampCheck))
            {
                if (swampCheck == "1")
                    _swampTracking = true;
                
                if (swampCheck == "0")
                {
                    _swampTracking = false;
                    _swampKillCount = 0;
                }
            }
        }

        private void SwampAchievementTracking()
        {
            if (!_swampTracking)
                return;

            // If Link takes damage setting the value is enough to prevent the achievement.
            if (MapManager.ObjLink.InDamageState)
                _swampKillCount = 0;
            
            // Grand the achievement when all 15 monsters are eaten.
            if (_swampKillCount >= 15)
            {
                _swampTracking = false;
                _swampKillCount = 0;
                _aiComponent.Trigger.Remove(_trackSwamp);
                _swampAchievementEarned = true;
                AchievementManager.Earn(25);
            }
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            return Values.HitCollision.RepellingParticle | Values.HitCollision.SpawnFire;
        }

        public override void Init()
        {
            // set the position to the one of the player
            if (_followMode && MapManager.ObjLink.NextMapPositionEnd.HasValue)
            {
                EntityPosition.Set(MapManager.ObjLink.NextMapPositionEnd.Value);
                _chain.SetChainPosition(MapManager.ObjLink.NextMapPositionEnd.Value);
            }
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X, direction.Y, 0.25f);

            return true;
        }

        private void SetFollowMode(bool follow)
        {
            _followMode = follow;
            _hitComponent.IsActive = !follow;
            _pushComponent.IsActive = !follow;

            if (follow)
            {
                _body.CollisionTypes = Values.CollisionTypes.None;
                _body.FieldRectangle = Map.GetField(MapManager.ObjLink.CenterPosition);
                MapManager.ObjLink.SetBowWowFollower(this);
                Map.Objects.RegisterAlwaysAnimateObject(this);
                _chainMax = 46;
            }
            else
            {
                _body.CollisionTypes = Values.CollisionTypes.NPCWall;
                _chainMax = 40;
            }
        }

        private void ToIdle()
        {
            _aiComponent.ChangeState("idle");
            _body.VelocityTarget.X = 0;
            _body.VelocityTarget.Y = 0;

            // Original private countdown 1: random 32-95 frames at 60fps
            _idleBobTimer = Game1.RandomNumber.Next(530, 1580);

            if (!_followMode)
                _outsideCounter = Game1.RandomNumber.Next(80, 200);
        }

        private void UpdateIAttack()
        {
            UpdatePosition();
        }

        private void UpdateIdle()
        {
            if (_attackPlayerCooldown > 0)
                _attackPlayerCooldown -= Game1.DeltaTime;

            // Short timer fires a tiny bob in a random direction, replicating
            // the original's "state 1 light bounce" that ran constantly during idle.
            _idleBobTimer -= Game1.DeltaTime;
            if (_idleBobTimer <= 0)
            {
                _idleBobTimer = Game1.RandomNumber.Next(530, 1580);

                // Pick one of the 8 directions from the original's speed table.
                // Values are scaled down from GB units to match this engine's scale.
                var dirs = new Vector2[]
                {
                    new( 0.25f, -0.75f),
                    new( 0.50f, -0.50f),
                    new( 0.75f,  0.25f),
                    new( 0.50f,  0.50f),
                    new(-0.25f,  0.75f),
                    new(-0.50f,  0.50f),
                    new(-0.75f, -0.25f),
                    new(-0.50f, -0.50f),
                };

                var dir = dirs[Game1.RandomNumber.Next(0, 8)];
                _body.VelocityTarget = dir;
                _body.Velocity.Z = 1.0f; // small bob, not a full lunge jump
                _body.IsGrounded = false;

                _direction = AnimationHelper.GetDirection(_body.VelocityTarget);
                _animator.Play("walk_" + _direction);
            }
            if (!_followMode)
            {
                _outsideCounter -= Game1.DeltaTime;
                if (_outsideCounter <= 0)
                    EndIdle();
                return;
            }
            UpdatePosition();
        }

        private void EndIdle()
        {
            if (_followMode)
            {
                _treasurePosition = GetTreasurePosition();

                if (_treasurePosition != Vector2.Zero)
                {
                    var direction = _treasurePosition - EntityPosition.Position;
                    if (direction != Vector2.Zero)
                        direction.Normalize();
                    _body.VelocityTarget = direction * 1.5f;

                    _direction = AnimationHelper.GetDirection(_body.VelocityTarget);
                    _animator.Play("walk_" + _direction);

                    _aiComponent.ChangeState("treasure");
                    return;
                }
                if (Game1.RandomNumber.Next(0, 100) < 35)
                    _aiComponent.ChangeState("walking");
                else
                    ToAttack();
            }
            else
            {
                if (Game1.RandomNumber.Next(0, 100) < 65 || _attackPlayerCooldown > 0)
                    _aiComponent.ChangeState("walking");
                else
                    ToAttackPlayer();
            }
        }

        private void InitWalking()
        {
            float rotation = 1;
            if (_followMode)
            {
                // the farther away the enemy is from the origin the more likely it becomes that he will move towards the center position
                var origin = MapManager.ObjLink.Position - new Vector2(0, 4);
                var directionToStart = origin - EntityPosition.Position;
                var radiusToCenter = MathF.Atan2(directionToStart.Y, directionToStart.X);
                var maxDistanceX = 64.0f;
                var maxDistanceY = 64.0f;
                var distanceMultiplier = MathHelper.Clamp(
                    MathF.Min(
                        (maxDistanceX - MathF.Abs(directionToStart.X)) / maxDistanceX,
                        (maxDistanceY - MathF.Abs(directionToStart.Y)) / maxDistanceY), 0, 1);

                rotation = radiusToCenter + (MathF.PI - Game1.RandomNumber.Next(0, 628) / 100f) * distanceMultiplier;
            }
            else
            {
                rotation = Game1.RandomNumber.Next(0, 628) / 100f;
            }
            // change the direction
            SetWalkDirection(new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation)));
        }

        private void SetWalkDirection(Vector2 direction)
        {
            _body.VelocityTarget = direction * Game1.RandomNumber.Next(25, 40) / 25f;
            _direction = AnimationHelper.GetDirection(_body.VelocityTarget);
            _animator.Play("walk_" + _direction);
        }

        private void UpdateWalking()
        {
            if (_attackPlayerCooldown > 0)
                _attackPlayerCooldown -= Game1.DeltaTime;

            if (_body.IsGrounded)
                _body.Velocity.Z = 1.50f;

            UpdatePosition();
        }

        private void EndWalking()
        {
            if (_followMode)
            {
                if (Game1.RandomNumber.Next(0, 100) < 35)
                    ToIdle();
                else
                    ToAttack();
            }
            else
            {
                if (Game1.RandomNumber.Next(0, 100) < 65 || _attackPlayerCooldown > 0)
                    ToIdle();
                else
                    ToAttackPlayer();
            }
        }

        private void ToAttackPlayer()
        {
            _attackPlayerCooldown = 3000;
            var playerPos = new Vector2(MapManager.ObjLink.EntityPosition.X, MapManager.ObjLink.EntityPosition.Y - 8);
            var direction = playerPos - new Vector2(EntityPosition.X, EntityPosition.Y - 8);
            if (direction == Vector2.Zero)
            {
                ToIdle();
                return;
            }

            direction.Normalize();

            _body.VelocityTarget = direction * 4;

            // Launch into the air
            _body.Velocity.Z = 2f;
            _body.IsGrounded = false;

            _direction = AnimationHelper.GetDirection(_body.VelocityTarget);
            _animator.Play("walk_" + _direction);
            _aiComponent.ChangeState("attackPlayer");
        }

        private void UpdateAttackPlayer()
        {
            // Wait until he's back on the ground after the jump
            if (_body.IsGrounded && _body.Velocity.Z == 0 && _body.Position.Z <= 0)
            {
                _body.VelocityTarget = Vector2.Zero;
                ToIdle();
                return;
            }
            UpdatePosition();
        }

        private void ToAttack()
        {
            // Update the field rectangle when Bow Wow is a follower.
            if (_followMode)
            {
                _body.FieldRectangle = Map.GetField(MapManager.ObjLink.CenterPosition);
            }
            // Reset the target each attack.
            _enemyTarget = null;

            // The point the chain is anchored to. In follow mode this matches what
            // UpdatePosition uses; in the cave it's the fixed spawn origin.
            var reachOrigin = _followMode
                ? MapManager.ObjLink.Position - new Vector2(0, 4)
                : _origin;

            // Chain clamp plus a small buffer for the ~5px attack tolerance and the
            // body/anchor offset, so we don't reject enemies he can just barely grab.
            var reachRadius = _chainMax + 8;

            // Where he measures his lunges from (matches UpdateAttack).
            var bowWowPoint = new Vector2(EntityPosition.X, EntityPosition.Y - 8);

            // Get a list of enemies for Bow Wow to attack.
            Map.Objects.GetGameObjectsWithTag(_enemyList, Values.GameObjectTag.Enemy,
                (int)MapManager.ObjLink.CenterPosition.X - 50,
                (int)MapManager.ObjLink.CenterPosition.Y - 50, 100, 100);

            // Remove enemies that Bow Wow shouldn't eat.
            _enemyList.RemoveAll(obj => obj.BowWowImmune);

            // Make sure the enemy is currently within the field rectangle.
            if (Camera.ClassicMode)
                _enemyList.RemoveAll(obj => !_body.FieldRectangle.Contains(obj.EntityPosition.Position));

            // Drop anything inactive or beyond the chain's reach so he never lunges
            // at an enemy he can't actually get to.
            _enemyList.RemoveAll(obj =>
                !obj.IsActive ||
                (GetTargetPoint(obj) - reachOrigin).Length() > reachRadius);

            // Prefer goponga flowers, otherwise the closest remaining enemy. Within
            // either group always pick the nearest one to where he is right now.
            float bestFlower = float.MaxValue;
            float bestOther = float.MaxValue;
            GameObject closestFlower = null;
            GameObject closestOther = null;

            foreach (var obj in _enemyList)
            {
                var dist = (GetTargetPoint(obj) - bowWowPoint).LengthSquared();

                if (obj is EnemyGopongaFlower || obj is EnemyGopongaFlowerGiant)
                {
                    if (dist < bestFlower)
                    {
                        bestFlower = dist;
                        closestFlower = obj;
                    }
                }
                else if (dist < bestOther)
                {
                    bestOther = dist;
                    closestOther = obj;
                }
            }

            _enemyTarget = closestFlower ?? closestOther;

            // No reachable enemies to attack.
            if (_enemyTarget == null)
            {
                // Coming out of the initial attack state just resumes wandering.
                if (_aiComponent.CurrentStateId == "iattack")
                {
                    _animator.Play("walk_" + _direction);
                    _aiComponent.ChangeState("walking");
                    return;
                }
                ToIdle();
                return;
            }

            // If the enemy is a fish, it might be underwater.
            if (_enemyTarget is EnemyFish fish)
                fish.MakeVulerable();

            // Set the attack direction.
            var damageState = (HittableComponent)_enemyTarget.Components[HittableComponent.Index];
            var attackDir = damageState.HittableBox.Box.Center - bowWowPoint;
            if (attackDir != Vector2.Zero)
                attackDir.Normalize();
            _body.VelocityTarget = attackDir * 3;

            // Update the animation.
            _direction = AnimationHelper.GetDirection(_body.VelocityTarget);
            _animator.Play("walk_" + _direction);
            _aiComponent.ChangeState("attack");
        }

        private Vector2 GetTargetPoint(GameObject obj)
        {
            if (obj.Components[HittableComponent.Index] is HittableComponent hit)
                return hit.HittableBox.Box.Center;
            return obj.EntityPosition.Position;
        }
        
        private Vector2 GetTreasurePosition()
        {
            if (Map?.DigMap == null || Map?.HoleMap?.ArrayTileMap == null)
                return Vector2.Zero;

            var digPositionX = (int)EntityPosition.X / 16;
            var digPositionY = (int)EntityPosition.Y / 16;

            for (var y = digPositionY - 3; y < digPositionY + 3; y++)
            {
                if (y < 0 || Map.DigMap.GetLength(1) <= y)
                    continue;

                for (var x = digPositionX - 3; x < digPositionX + 3; x++)
                {
                    if (x < 0 || Map.DigMap.GetLength(0) <= x)
                        continue;

                    if (x == digPositionX && y == digPositionY)
                        continue;

                    var digCell = Map.DigMap[x, y];
                    if (Map.HoleMap.ArrayTileMap[x, y, 0] < 0 &&
                        !string.IsNullOrEmpty(digCell) &&
                        digCell.Contains(':'))
                    {
                        var split = digCell.Split(':');
                        if (split.Length >= 2 && Game1.GameManager.SaveManager.GetString(split[1], "0") != "1")
                        {
                            _lastTreasure = split[1];
                            return new Vector2(x * 16 + 8, y * 16 + 8);
                        }
                    }
                }
            }
            return Vector2.Zero;
        }

        private void UpdateAttack()
        {
            var damageState = (HittableComponent)_enemyTarget.Components[HittableComponent.Index];
            var direction = damageState.HittableBox.Box.Center - new Vector2(EntityPosition.X, EntityPosition.Y - 8);

            // Attack the enemy if we are close enough.
            if (direction.Length() < 5)
            {
                // Make sure it still exists.
                if (_enemyTarget.Map != null)
                {
                    // BowWow eats the enemy.
                    var hit = damageState?.Hit(this, _body.VelocityTarget * 0.1f, HitType.BowWow, 8, false);

                    // If trying to get the achievement count the number of kills.
                    if (_swampTracking && hit == Values.HitCollision.Enemy)
                        _swampKillCount++;
                }
                ToIdle();
            }
            if (_body.VelocityTarget == Vector2.Zero)
                ToIdle();

            UpdatePosition();
        }

        private void UpdateTreasure()
        {
            // Get the vector between points.
            var direction = _treasurePosition - EntityPosition.Position;

            // Move to the treasure.
            if (direction.Length() < 5)
            {
                // Achievement: BowWow finds all shells in a single session.
                if (_lastTreasure.Contains("shell") && !AchievementManager.IsEarned(28))
                {
                    // Map each shell treasure key to its own bit (shells are 3, 5, 9, 14).
                    int bit = _lastTreasure switch
                    {
                        "shell_3"  => 0,
                        "shell_5"  => 1,
                        "shell_9"  => 2,
                        "shell_14" => 3,
                        _          => -1,
                    };
                    // Only track shells that are part of the achievement set.
                    if (bit >= 0)
                    {
                        // If the treasure is a shell from the list.
                        var saveManager = Game1.GameManager.SaveManager;
                        int shellMask = saveManager.GetInt("bowWowShells", 0);

                        // If this shell's bit isn't recorded yet, set it.
                        if ((shellMask & (1 << bit)) == 0)
                        {
                            // Flip the bit for the corresponding shell.
                            shellMask |= (1 << bit);

                            // All four bits were flipped so all shells were found.
                            if (shellMask == 0b1111)
                            {
                                AchievementManager.Earn(28);
                                saveManager.RemoveInt("bowWowShells");
                            }
                            // There are still more shells to find so record what was found.
                            else
                            {
                                saveManager.SetInt("bowWowShells", shellMask);
                            }
                        } 
                    }
                }
                // Show the text for finding a dig spot.
                Game1.GameManager.StartDialogPath("bowWow_dig");
                ToIdle();
            }
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (_followMode)
            {
                _origin = MapManager.ObjLink.Position - new Vector2(0, 4);
                _body.FieldRectangle = Map.GetField(MapManager.ObjLink.CenterPosition);
            }
            var distance = (EntityPosition.Position +
                new Vector2(_body.VelocityTarget.X + _body.Velocity.X, _body.VelocityTarget.Y + _body.Velocity.Y) * Game1.TimeMultiplier - new Vector2(0, 4)) - _origin;
            var dist = distance.Length();

            if (dist > _chainMax)
            {
                var dir = distance;
                dir.Normalize();
                var newPosition = _origin + dir * _chainMax + new Vector2(0, 4);

                var direction = newPosition - EntityPosition.Position;
                if (direction.Length() > 0)
                    direction.Normalize();

                var mult = 0.125f + Math.Clamp((dist - _chainMax) / 4, 0, 4);
                _chainPull = direction * mult;

                if (_followMode)
                {
                    if (dist < (distance + _body.VelocityTarget).Length())
                        _body.VelocityTarget = AnimationHelper.MoveToTarget(_body.VelocityTarget, Vector2.Zero, Game1.TimeMultiplier);

                    _outsideCounter -= Game1.DeltaTime;
                    if (_outsideCounter <= 0)
                    {
                        _outsideCounter += 350;
                        SetWalkDirection(direction);
                        _body.VelocityTarget *= 1.5f;
                        _aiComponent.ChangeState("walking", true);
                    }
                }
                else
                {
                    // Hard clamp: compute actual current overshoot and snap position back to
                    // the chain boundary. Also kill all outward XY velocity so there is no
                    // rubber-band correction visible on the next frame.
                    var actualDistance = (EntityPosition.Position - new Vector2(0, 4)) - _origin;
                    var actualDist = actualDistance.Length();
                    if (actualDist > _chainMax)
                    {
                        var clampDir = actualDistance / actualDist;
                        EntityPosition.Set(new Vector2(
                            _origin.X + clampDir.X * _chainMax,
                            _origin.Y + clampDir.Y * _chainMax + 4));

                        // Kill XY velocity but preserve Z so a jump arc completes normally.
                        _body.VelocityTarget = Vector2.Zero;
                        _body.Velocity = new Vector3(0, 0, _body.Velocity.Z);
                        _chainPull = Vector2.Zero;

                        // If he was mid-lunge, end the attack cleanly.
                        if (_aiComponent.CurrentStateId == "attackPlayer")
                            ToIdle();
                    }
                }
            }
            else
            {
                _outsideCounter = 350;
                _chainPull = Vector2.Zero;
            }
            _body.AdditionalMovementVT = _chainPull;
            _chainPull *= (float)Math.Pow(0.75f, Game1.TimeMultiplier);
            UpdateChain();
        }

        private void UpdateChain()
        {
            // update the chain
            var directionOffset = AnimationHelper.DirectionOffset[_direction] * new Vector2(6, 3);
            _currentDirectionOffset = Vector2.Lerp(_currentDirectionOffset, directionOffset, Game1.TimeMultiplier * 0.25f);

            var startPosition = new Vector3(_origin.X, _origin.Y, _followMode ? MapManager.ObjLink.EntityPosition.Z : 0);
            var goalPosition = new Vector3(
                EntityPosition.Position.X - _currentDirectionOffset.X,
                EntityPosition.Position.Y - 4 - _currentDirectionOffset.Y,
                EntityPosition.Z);

            startPosition.Z = MathHelper.Clamp(startPosition.Z, 0, 12);
            _chain.UpdateChain(startPosition, goalPosition);
        }

        public void StartWorldTeleportRise(float durationMs)
        {
            _worldTeleportRiseTime = 0;
            _worldTeleportRiseDuration = durationMs;
            _body.IgnoresZ = true;
            _body.IsGrounded = false;
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
            _body.AdditionalMovementVT = Vector2.Zero;
            _chainPull = Vector2.Zero;
            EntityPosition.Z = 0;

            _chain.SetVisible(false);
            _aiComponent.ChangeState("worldTeleportRise");
        }

        private void UpdateWorldTeleportRise()
        {
            _body.VelocityTarget = Vector2.Zero;
            _body.AdditionalMovementVT = Vector2.Zero;
            _chainPull = Vector2.Zero;

            _worldTeleportRiseTime += Game1.DeltaTime;
            var percentage = MathHelper.Clamp(_worldTeleportRiseTime / _worldTeleportRiseDuration, 0, 1);
            EntityPosition.Z = percentage * 128;
        }

        public void HoldForWorldTeleportFall(float delayMs)
        {
            _worldTeleportFallDelay = delayMs;
            _body.IgnoresZ = true;
            _body.IsGrounded = false;
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
            _body.AdditionalMovementVT = Vector2.Zero;
            _chainPull = Vector2.Zero;

            _direction = 3;
            _animator.Play("walk_" + _direction);
            _aiComponent.ChangeState("worldTeleportFall");
        }

        private void UpdateWorldTeleportFall()
        {
            _body.VelocityTarget = Vector2.Zero;
            _body.AdditionalMovementVT = Vector2.Zero;
            _chainPull = Vector2.Zero;

            if (_worldTeleportFallDelay > 0)
            {
                _worldTeleportFallDelay -= Game1.DeltaTime;
                return;
            }
            _body.IgnoresZ = false;

            if (_body.IsGrounded)
            {
                _chain.SetVisible(true);
                _aiComponent.ChangeState("walking");
            }
        }

        private void OnCollision(Values.BodyCollision moveCollision)
        {
            // rotate after wall collision
            // top collision
            if ((moveCollision & Values.BodyCollision.Horizontal) != 0)
            {
                if (!_changeDirectionSwitch.State)
                    return;
                _changeDirectionSwitch.Reset();

                _body.VelocityTarget.X = -_body.VelocityTarget.X * 0.5f;
            }
            // vertical collision
            else if ((moveCollision & Values.BodyCollision.Vertical) != 0)
            {
                _body.VelocityTarget.Y = -_body.VelocityTarget.Y * 0.5f;
            }
            if ((moveCollision & (Values.BodyCollision.Vertical | Values.BodyCollision.Horizontal)) != 0)
            {
                _direction = AnimationHelper.GetDirection(_body.VelocityTarget);
                _animator.Play("walk_" + _direction);
            }
        }
    }
}
