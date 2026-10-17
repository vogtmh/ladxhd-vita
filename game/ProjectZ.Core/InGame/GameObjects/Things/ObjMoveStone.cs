using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Base.Systems;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjMoveStone : GameObject
    {
        private readonly List<GameObject> _collidingObjects = new List<GameObject>();
        private readonly List<GameObject> _groupOfMoveStone = new List<GameObject>();
        private readonly List<EnemyGhini> _attachedGhinis = new List<EnemyGhini>();

        private readonly AiComponent _aiComponent;
        private readonly BodyComponent _body;
        private readonly CBox _box;

        private static ObjMoveStone _activePushStone;

        private Vector2 _startPosition;
        private Vector2 _goalPosition;

        private readonly int _posX;
        private readonly int _posY;
        private readonly string _spriteId;
        Rectangle _collisionRect;
        int _layer;

        private readonly string _strKey;
        private readonly string _strKeyDir;
        private readonly string _strResetKey;
        private readonly int _allowedDirections;
        private readonly int _moveTime = 450;

        // Used to work around an issue in dungeon 7 floor 2 where a barrier can show through a block.
        private List<GameObject> _groupOfBarrier = new List<GameObject>();
        private List<ObjDungeonBarrier> _barrierFound = new List<ObjDungeonBarrier>();

        // Type 1 (Gravestone) sets the key directly on push and resets it on spawn.
        private int _type;
        private int _moveDirection;
        private bool _freezePlayer;
        private bool _isResetting;

        public bool NoRespawn;

        public ObjMoveStone(Map.Map map, int posX, int posY, int moveDirections, string strKey, string spriteId, Rectangle collisionRectangle, int layer, int type, bool freezePlayer, string resetKey) : base(map, spriteId)
        {
            EntityPosition = new CPosition(posX, posY + 16, 0);
            ResetPosition = new CPosition(posX, posY + 16, 0);
            EntitySize = new Rectangle(0, -16, 16, 16);

            CanReset = true;
            OnReset = Reset;

            _posX = posX;
            _posY = posY;
            _spriteId = spriteId;
            _collisionRect = collisionRectangle;
            _layer = layer;

            _allowedDirections = moveDirections;
            _strKey = strKey;
            _strKeyDir = strKey + "_dir";
            _type = type;
            _freezePlayer = freezePlayer;
            _strResetKey = resetKey;

            _body = new BodyComponent(EntityPosition, 3, -13, 10, 10, 8)
            {
                IgnoreHoles = true,
                IgnoreHeight = true
            };
            var movingTrigger = new AiTriggerCountdown(_moveTime, MoveTick, MoveEnd);
            var movedState = new AiState { Init = InitMoved };

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", new AiState());
            _aiComponent.States.Add("moving", new AiState { Trigger = { movingTrigger } });
            _aiComponent.States.Add("moved", movedState);
            new AiFallState(_aiComponent, _body, OnHoleAbsorb, null, 200);
            _aiComponent.ChangeState("idle");

            _box = new CBox(EntityPosition, collisionRectangle.X, collisionRectangle.Y, collisionRectangle.Width, collisionRectangle.Height, 16);

            var sprite = Resources.GetSprite(spriteId);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(PushableComponent.Index, new PushableComponent(_box, OnPush) { InertiaTime = 500 });
            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(_box, Values.CollisionTypes.Normal | Values.CollisionTypes.Hookshot));
            AddComponent(DrawComponent.Index, new DrawSpriteComponent(spriteId, EntityPosition, new Vector2(0, -sprite.SourceRectangle.Height), layer));

            if (!string.IsNullOrEmpty(_strResetKey))
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));

            // Set the initial key state.
            if (_type == 1 && _strKey != null)
                Game1.GameManager.SaveManager.SetString(_strKey, "0");
            if (!string.IsNullOrEmpty(_strKeyDir))
                Game1.GameManager.SaveManager.SetString(_strKeyDir, "-1");
        }

        public override void Reset()
        {
            // Reset the push stone so it doesn't get stuck.
            _activePushStone = null;

            // Set the current state to idle so it can be pushed again.
            _aiComponent.CurrentStateId = "idle";

            // Just in case a Ghini push is somehow triggered on field transition.
            foreach (var ghini in _attachedGhinis)
                ghini.PushedByGrave(false);
            _attachedGhinis.Clear();

            // Reset all the keys on the stone.
            if (_type == 1 && _strKey != null)
                Game1.GameManager.SaveManager.SetString(_strKey, "0");
            if (!string.IsNullOrEmpty(_strKeyDir))
                Game1.GameManager.SaveManager.SetString(_strKeyDir, "-1");
            if (!string.IsNullOrEmpty(_strResetKey))
                Game1.GameManager.SaveManager.SetString(_strResetKey, "0");

            // Restore the level 7 dungeon barrier.
            RestoreLevel7DungeonBarrier();
        }

        private void OnHoleAbsorb()
        {
            // Stones created through ObjObjectSpawner should not spawn
            // with ObjMoveStoneRespawner or it will create duplicates.
            if (!NoRespawn)
                Map.Objects.SpawnObject(new ObjMoveStoneRespawner(Map, _posX, _posY, _allowedDirections, _strKey, _spriteId, _collisionRect, _layer, _type, _freezePlayer, _strResetKey));

            // If this is not cleared properly, then it could prevent the
            // player from pushing stones until the game is reset.
            _activePushStone = null;
        }

        private void OnKeyChange()
        {
            // Get the stone's reset key.
            var keyState = Game1.GameManager.SaveManager.GetString(_strResetKey);

            // Check if the block should be moved back to the start position.
            if (keyState == "1" && _aiComponent.CurrentStateId == "moved")
                ResetToStart();
        }

        private void ResetToStart()
        {
            // Stones are reset when the field changes.
            if (Camera.ClassicMode)
                return;

            // If "Modern Camera" then the stone moves itself back to its start location.
            _isResetting = true;
            _goalPosition = _startPosition;
            _startPosition = EntityPosition.Position;
            _moveDirection = (_moveDirection + 2) % 4;

            // Reset the key with the direction it was pushed.
            if (!string.IsNullOrEmpty(_strKeyDir))
                Game1.GameManager.SaveManager.SetString(_strKeyDir, "-1");

            // Start moving the stone.
            ToMoving();

            // Restore the level 7 dungeon barrier.
            RestoreLevel7DungeonBarrier();
        }

        private bool IsClosestStone(Vector2 pushDirection)
        {
            const float biasStrength = 48f;

            // Get Link's body component and body box.
            var bodyComLink = MapManager.ObjLink.Components[BodyComponent.Index] as BodyComponent;
            var bodyBoxLink = bodyComLink.BodyBox.Box;

            // Get the center of Link's body and the center of the stone's body.
            Vector2 boxCenterLink = new(bodyBoxLink.Center.X, bodyBoxLink.Center.Y);
            Vector2 boxCenterRock = new(_box.Box.Center.X, _box.Box.Center.Y);
            Vector2 distBoxCenter = Vector2.Normalize(boxCenterRock - boxCenterLink);

            // Compare the distance and give it a score to compare to other stones.
            float dotLinkRock = Vector2.Dot(distBoxCenter, pushDirection);
            float distSquared = Vector2.DistanceSquared(boxCenterLink, boxCenterRock);
            float stoneScoreA = distSquared - dotLinkRock * biasStrength;

            // Find nearby objects to add to a list to find stones.
            _groupOfMoveStone.Clear();
            Map.Objects.GetComponentList(_groupOfMoveStone, (int)boxCenterLink.X - 32, (int)boxCenterLink.Y - 32, 64, 64, BodyComponent.Mask);

            // Loop through the object group.
            foreach (var obj in _groupOfMoveStone)
            {
                // If the object is not a stone then skip it.
                if (obj is not ObjMoveStone otherStone) continue;
                if (otherStone == this) continue;

                // Get the center of this stone's box.
                Vector2 boxCentOther = new(otherStone._box.Box.Center.X, otherStone._box.Box.Center.Y);
                Vector2 distBoxOther = Vector2.Normalize(boxCentOther - boxCenterLink);
                
                // Get the distance score of this stone compared to Link.
                float dotRockOther = Vector2.Dot(distBoxOther, pushDirection);
                float distSquOther = Vector2.DistanceSquared(boxCenterLink, boxCentOther);
                float stoneScoreB  = distSquOther - dotRockOther * biasStrength;

                // If it's score is not higher than the previous stone then don't push it.
                if (stoneScoreB < stoneScoreA)
                    return false;

                // In the case of a tie we need a tie breaker event.
                if (stoneScoreB == stoneScoreA)
                    if (otherStone.GetHashCode() < GetHashCode())
                        return false;
            }
            return true;
        }

        private bool BlockColorDungeonEntry()
        {
            // Check if it's the grave that opens up the color dungeon.
            if (_type == 1 && _strKey == "ow_grave_4")
            {
                // The player must have have the second instrument.
                var theInstrument = Game1.GameManager.GetItem("instrument1");
                var hasInstrument = theInstrument != null && theInstrument.Count > 0;

                // Check if the player has a follower with them.
                var hasBowWow  = Game1.GameManager.SaveManager.GetString("has_bowWow", "0") == "1";
                var hasMarin   = Game1.GameManager.SaveManager.GetString("has_marin", "0") == "1";
                var hasGhost   = Game1.GameManager.SaveManager.GetString("has_ghost", "0") == "1";
                var hasRooster = Game1.GameManager.SaveManager.GetString("has_rooster", "0") == "1";

                // The player has a follower with them.
                if (!hasInstrument || hasBowWow || hasMarin || hasGhost || hasRooster)
                {
                    // Create a list to find the grave trigger which tracks the stone order.
                    List<GameObject> graveTriggerList = new List<GameObject>();
                    Rectangle field = MapManager.ObjLink.CurrentField;

                    // Search the current field that Link is in.
                    Map.Objects.GetGameObjectsWithTag(graveTriggerList, Values.GameObjectTag.Utility, field.X, field.Y, field.Width, field.Height);

                    // The first object in the list will be what we're looking for.
                    ObjGraveTrigger graveTrigger = graveTriggerList[0] as ObjGraveTrigger;

                    // Show the message about not having enough strength.
                    if (graveTrigger.CurrentState == 4)
                    {
                        Game1.GameManager.StartDialogPath("grave_locked");
                        return true;
                    }
                }
            }
            // It's not the gravestone we're looking for.
            return false;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            // Set Link to a variable to shorten the references and get the direction the stone should move.
            var Link = MapManager.ObjLink;
            _moveDirection = AnimationHelper.GetDirection(direction);

            // Assemble the list of conditions to check if the stone should move.
            bool linkNotPushing = !Link.WasPushing && !Link.IsCarrying();                      // Link was pushing the stone last frame update.
            bool dirNotMatching = _moveDirection != Link.Direction;                            // Direction of Link matches direction of push.
            bool stateIsNotIdle = _aiComponent.CurrentStateId != "idle";                       // Current state of stone must be "Idle".
            bool pushTypeImpact = type == PushableComponent.PushType.Impact;                   // Push type must not be "Impact" type.
            bool singlePushOnly = _activePushStone != null && _activePushStone != this;        // Only allow a single stone to move at once.
            bool noColorDungeon = BlockColorDungeonEntry();                                    // The gravestone which accesses the color dungeon.

            // These conditions are combined into a single condition for readability.
            bool directionExist = _allowedDirections != -1;                                    // Stone has valid pushable directions set.
            bool directionFails = (_allowedDirections & (0x01 << _moveDirection)) == 0;        // Pushed direction must match a pushable direction.
            bool blockDirection = directionExist && directionFails;

            // If any of the conditions pass, then fail pushing the stone.
            if (linkNotPushing || dirNotMatching || stateIsNotIdle || pushTypeImpact || singlePushOnly || noColorDungeon || blockDirection)
                return false;

            // Must be the closest stone to Link. Separated out as it's the most expensive check.
            if (!IsClosestStone(direction))
                return false;

            // Only move if there is nothing blocking the way.
            var pushVector = AnimationHelper.DirectionOffset[_moveDirection];
            var collidingRectangle = Box.Empty;
            var collisionBox = new Box(EntityPosition.X + pushVector.X * 16, EntityPosition.Y + pushVector.Y * 16 - 16, 0, 16, 16, 16);

            // Collision blocks movement, but the push is valid. Return true so the push is consumed and not retried elsewhere.
            if (Map.Objects.Collision(collisionBox, Box.Empty, Values.CollisionTypes.Normal | Values.CollisionTypes.Passageway, 0, 0, ref collidingRectangle))
                return true;

            // Set the stone's starting and finishing position before being pushed.
            _startPosition = EntityPosition.Position;
            _goalPosition = _startPosition + pushVector * 16;

            // Set this stone as the active stone being pushed before moving it.
            _activePushStone = this;

            // Start moving the stone.
            ToMoving();

            // If a reset key exists then set it to zero.
            if (!string.IsNullOrEmpty(_strResetKey))
                Game1.GameManager.SaveManager.SetString(_strResetKey, "0");

            // Set the key assigned to the stone.
            if (_type == 1 && !string.IsNullOrEmpty(_strKey))
                Game1.GameManager.SaveManager.SetString(_strKey, "1");

            // Set the key that stores the direction the stone was pushed.
            if (_type == 1 && !string.IsNullOrEmpty(_strKeyDir))
                Game1.GameManager.SaveManager.SetString(_strKeyDir, _moveDirection.ToString());

            return true;
        }

        private void ToMoving()
        {
            // Play the push sound effect.
            Game1.AudioManager.PlaySoundEffect("D378-17-11");

            // Find any Ghini nearby so it travels with the stone.
            _attachedGhinis.Clear();
            var nearbyObjects = new List<GameObject>();
            Map.Objects.GetGameObjectsWithTag(nearbyObjects, Values.GameObjectTag.Enemy,
                (int)_box.Box.X - 16, (int)_box.Box.Y - 16, (int)_box.Box.Width + 32, (int)_box.Box.Height + 32);

            // Alert the Ghini to move with the stone.
            foreach (var obj in nearbyObjects)
            {
                if (obj is EnemyGhini ghini && ghini.IsSpawning)
                {
                    _attachedGhinis.Add(ghini);
                    ghini.PushedByGrave(true);
                }
            }
            // Start moving the stone.
            _aiComponent.ChangeState("moving");
        }

        private void MoveTick(double time)
        {
            var amount = (float)Math.Sin((_moveTime - time) / _moveTime * (Math.PI / 2f));
            var Link = MapManager.ObjLink;
            Move(amount);

            if (!_isResetting)
            {
                // Hack: Starts a timer that while active, forces Link into "pushing" animation
                // while he is pushing against the moving stone. This is because blocking while
                // pushing is possible, and in the original game pushing animation was forced.
                Link.NotifyPushing(_box.Box);

                // Some "event" stones freeze the player (mermaid statue, weathervane).
                if (_freezePlayer)
                    Link.FreezePlayer();
            }
        }

        private void MoveEnd()
        {
            // Finished moving.
            Move(1);

            // When the move is done, if any Ghini's were pushed then clear the push.
            foreach (var ghini in _attachedGhinis)
                ghini.PushedByGrave(false);
            _attachedGhinis.Clear();

            // Set the key.
            if (_type == 0 && !string.IsNullOrEmpty(_strKey))
                Game1.GameManager.SaveManager.SetString(_strKey, "1");

            if (_isResetting)
            {
                _isResetting = false;
                _aiComponent.ChangeState("idle");
                if (_type == 0 && !string.IsNullOrEmpty(_strKeyDir))
                    Game1.GameManager.SaveManager.SetString(_strKeyDir, "-1");
            }
            else
            {
                _aiComponent.ChangeState("moved");
                if (_type == 0 && !string.IsNullOrEmpty(_strKeyDir))
                    Game1.GameManager.SaveManager.SetString(_strKeyDir, _moveDirection.ToString());
            }
            // Works around an issue in dungeon 7 floor 2 where a barrier can show through a block.
            HideLevel7DungeonBarrier();

            // Now that the move is finished, clear the active stone.
            _activePushStone = null;

            // Can fall into holes after finishing the movement animation.
            _body.IgnoreHoles = false;
        }

        private void HideLevel7DungeonBarrier()
        {
            // Get any dungeon barriers nearby.
            Map.Objects.GetComponentList(_groupOfBarrier, (int)_body.BodyBox.Box.X, (int)_body.BodyBox.Box.Y, 4, 4, CollisionComponent.Mask);

            // Loop through the barriers found.
            foreach (var obj in _groupOfBarrier)
            {
                // We only want barriers.
                if (obj is not ObjDungeonBarrier barrier) continue;

                // Single out the barrier in Level 7 dungeon.
                Vector2 barPosition = barrier.EntityPosition.Position;
                Vector2 newPosition = new Vector2(354.5f, 229f);

                // The barrier we want to remove is the one in Level 7 floor 2 when pushing the block up over it.
                if (_moveDirection == 1 && barPosition == newPosition)
                {
                    // Add the barrier to a list so it can be restored.
                    if (!_barrierFound.Contains(barrier))
                        _barrierFound.Add(barrier);

                    // Deactivate the barrier.
                    barrier.IsActive = false;
                    break;
                }
            }
            // Clear the list of potential barriers.
            _groupOfBarrier.Clear();
        }

        private void RestoreLevel7DungeonBarrier()
        {
            // If there are barriers that were hidden.
            if (_barrierFound.Count > 0)
            {
                // Loop through the list of them.
                foreach (var barrier in _barrierFound.ToList()) 
                {
                    // Restore the barrier and remove from the list.
                    barrier.IsActive = true;
                    _barrierFound.Remove(barrier);
                }
            }
        }

        private void InitMoved()
        {
            // Fall into the water.
            if (_body.CurrentFieldState.HasFlag(MapStates.FieldStates.DeepWater))
            {
                Game1.AudioManager.PlaySoundEffect("D360-14-0E");

                // Spawn splash effect.
                var fallAnimation = new ObjAnimator(Map,
                    (int)(_body.Position.X + _body.OffsetX + _body.Width / 2.0f),
                    (int)(_body.Position.Y + _body.OffsetY + _body.Height - 2),
                    Values.LayerPlayer, "Particles/fishingSplash", "idle", true);
                Map.Objects.SpawnObject(fallAnimation);
                Map.Objects.SpawnObject(new ObjMoveStoneRespawner(Map, _posX, _posY, _allowedDirections, _strKey, _spriteId, _collisionRect, _layer, _type, _freezePlayer, _strResetKey));
                Map.Objects.DeleteObjects.Add(this);
            }
        }

        private void Move(float amount)
        {
            var lastBox = _box.Box;
            var lastPosition = EntityPosition.Position;

            // Lerp the position of the stone each frame.
            EntityPosition.Set(Vector2.Lerp(_startPosition, _goalPosition, amount));

            // If a Ghini is being pushed with the stone, move it with the stone.
            var moveDelta = EntityPosition.Position - lastPosition;
            foreach (var ghini in _attachedGhinis)
                ghini.MoveWithGrave(moveDelta);

            // Get other collding objects.
            _collidingObjects.Clear();
            Map.Objects.GetComponentList(_collidingObjects, (int)EntityPosition.Position.X, (int)EntityPosition.Position.Y - 16, 17, 17, BodyComponent.Mask);

            // Handle collding objects.
            foreach (var collidingObject in _collidingObjects)
            {
                if (collidingObject is ObjMoveStone)
                    continue;

                var body = (BodyComponent)collidingObject.Components[BodyComponent.Index];

                if (body.BodyBox.Box.Intersects(_box.Box) && !body.BodyBox.Box.Intersects(lastBox))
                {
                    var offset = Vector2.Zero;
                    if (_moveDirection == 0)
                        offset.X = _box.Box.Left - body.BodyBox.Box.Right - 0.05f;
                    else if (_moveDirection == 2)
                        offset.X = _box.Box.Right - body.BodyBox.Box.Left + 0.05f;
                    else if (_moveDirection == 1)
                        offset.Y = _box.Box.Back - body.BodyBox.Box.Front - 0.05f;
                    else if (_moveDirection == 3)
                        offset.Y = _box.Box.Front - body.BodyBox.Box.Back + 0.05f;

                    SystemBody.MoveBody(body, offset, body.CollisionTypes, false, false, false);
                    body.Position.NotifyListeners();
                }
            }
        }
    }
}
