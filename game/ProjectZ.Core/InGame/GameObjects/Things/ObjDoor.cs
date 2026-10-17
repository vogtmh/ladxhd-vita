using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    public class ObjDoor : GameObject
    {
        private Rectangle _collisionRectangle;

        public string _entryId;
        public string _nextMap;
        private string _exitId;

        private int _direction;
        private int _mode;
        private int _positionOffset;

        private bool _isColliding;
        private bool _wasColliding;
        public bool _savePosition;
        public bool _backdoorLevel8;
        private bool _isTransitioning;

        //----------------------------------------
        //  Door Type Modes:
        //----------------------------------------
        //  0: Normal Door
        //  1: Stairs
        //  2: No walk in Transition + Falling
        //  3: Swim in Transition
        //  4: No walk in Transition
        //  5: Final Stairs Transition
        //  6: Falling Rotate
        //  7: Falling
        //----------------------------------------

        public ObjDoor() : base("editor door")
        {
            EditorColor = Color.Yellow * 0.65f;
        }

        public ObjDoor(Map.Map map, int posX, int posY, int width, int height,
            string entryId, string nextMapId, string exitId, int direction, int mode, bool savePosition) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, width, height);

            // stairs have a smaller entry
            if (Map.Is2dMap || mode != 1)
                _collisionRectangle = new Rectangle(posX, posY, width, height);
            else
            {
                _collisionRectangle = new Rectangle(posX + 6, posY + 6, width - 12, height - 12);
                _positionOffset = 4;
            }
            if (mode == 4)
            {
                _collisionRectangle.Height = 10;
            }
            _entryId = entryId;
            _direction = direction;
            _mode = mode;
            _savePosition = savePosition;

            // Get the door that the player entered through. If the player entered
            // level 8 through a backdoor, activate the backdoor hack.
            if (_entryId != null && MapManager.ObjLink.NextMapPositionId == _entryId)
            {
                if (map.MapName == "dungeon8.map" && (entryId == "d8_left" || entryId == "d8_right"))
                    _backdoorLevel8 = true;

                PlacePlayer();
            }
            _nextMap = nextMapId;
            _exitId = exitId;

            var collisionBox = new CBox(EntityPosition, 0, 0, 0, width, height, 4, true);

            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(collisionBox, Values.CollisionTypes.Passageway) { });

            if (!string.IsNullOrEmpty(_nextMap) && !string.IsNullOrEmpty(_exitId))
            {
                AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
                AddComponent(ObjectCollisionComponent.Index, new ObjectCollisionComponent(_collisionRectangle, OnCollision));
            }
        }

        private void Update()
        {
            _wasColliding = _isColliding;
            _isColliding = false;
        }

        private void OnCollision(GameObject gameObject)
        {
            // can jump over stair entries
            if (_mode == 1 && !MapManager.ObjLink.IsGrounded() && !Map.Is2dMap)
                return;

            if (_mode == 3 && !MapManager.ObjLink.IsDiving() && !Map.Is2dMap)
                return;

            if (MapManager.ObjLink.IsRailJumping())
                return;

            _isColliding = true;

            // first step on the door?
            if (MapManager.ObjLink.WasHoleReset || MapManager.ObjLink.IsDying() || _wasColliding || _isTransitioning)
                return;

            _isTransitioning = true;

            var transitionEnd = new Vector2(
                _collisionRectangle.X + _collisionRectangle.Width / 2f,
                _collisionRectangle.Y + _collisionRectangle.Height / 2f + MapManager.ObjLink.Body.Height / 2f);
            var color = Values.MapTransitionColor;
            var colorMode = false;

            if (_mode == 0)
            {
                if (_direction == 1)
                    transitionEnd.Y = _collisionRectangle.Y + 8;
                else if (_direction == 3)
                    transitionEnd.Y = _collisionRectangle.Y + 16;

                if (!Map.Is2dMap)
                    MapManager.ObjLink.Direction = (_direction + 2) % 4;

                // walk on the ground
                if (Map.Is2dMap && (_direction % 2) == 0)
                    transitionEnd.Y = _collisionRectangle.Bottom;
            }
            else if (_mode == 3)
            {
                if (Map.Is2dMap)
                {
                    if (_direction == 0)
                        transitionEnd = MapManager.ObjLink.Position + new Vector2(8, 0);
                    else if (_direction == 2)
                        transitionEnd = MapManager.ObjLink.Position + new Vector2(-8, 0);
                    else if (_direction == 3)
                        transitionEnd = MapManager.ObjLink.Position + new Vector2(MapManager.ObjLink.GetSwimVelocity().X * 8, -8);

                    // look at the camera
                    MapManager.ObjLink.Direction = 3;
                }
                else
                    // do not move while transitioning out
                    transitionEnd = MapManager.ObjLink.Position;
            }
            else if (_mode == 5)
            {
                transitionEnd = MapManager.ObjLink.Position + MapManager.ObjLink.Body.VelocityTarget * 60 * (MapTransitionSystem.ChangeMapTime / 1000f);
                color = Color.White;
                colorMode = true;
            }
            // Play the stairs sound effect.
            Game1.AudioManager.PlaySoundEffect("D378-06-06");

            MapManager.ObjLink.MapTransitionStart = MapManager.ObjLink.Position;
            MapManager.ObjLink.MapTransitionEnd = transitionEnd;
            MapManager.ObjLink.TransitionOutWalking = MapManager.ObjLink.Position != transitionEnd;

            // Append a map change.
            var transitionSystem = (MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)];
            transitionSystem.AppendMapChange(_nextMap, _exitId, false, false, color, colorMode);
        }

        private void PlacePlayer()
        {
            _isColliding = true;
            _wasColliding = true;

            var transitionStart = new Vector2(
                _collisionRectangle.X + _collisionRectangle.Width / 2f,
                _collisionRectangle.Y + _collisionRectangle.Height / 2f + MapManager.ObjLink.Body.Height / 2f);
            var transitionEnd = transitionStart;

            if (_mode == 0 || _mode == 1)
            {
                if (MapManager.ObjLink.CurrentState != ObjLink.State.OcarinaTeleport)
                {
                    if (_direction == 0)
                        transitionEnd.X = _collisionRectangle.X - MathF.Ceiling(MapManager.ObjLink.Body.Width / 2f) - _positionOffset;
                    else if (_direction == 1)
                        transitionEnd.Y = _collisionRectangle.Y - _positionOffset;
                    else if (_direction == 2)
                        transitionEnd.X = _collisionRectangle.X + _collisionRectangle.Width + MathF.Ceiling(MapManager.ObjLink.Body.Width / 2f) + _positionOffset;
                    else if (_direction == 3)
                        transitionEnd.Y = _collisionRectangle.Y + _collisionRectangle.Height + MapManager.ObjLink.Body.Height + _positionOffset;
                }
                // walk on the ground
                if (Map.Is2dMap)
                {
                    if ((_direction % 2) == 0)
                    {
                        transitionStart.Y = _collisionRectangle.Bottom;
                        transitionEnd.Y = _collisionRectangle.Bottom;
                    }
                    // Increase offsets of 2D ladder entry to prevent accidental exits.
                    if (_direction == 1)
                        transitionEnd.Y -= 4;
                    if (_direction == 3)
                        transitionEnd.Y += 4;
                }
            }
            else if (_mode == 2)
                MapManager.ObjLink.NextMapFallStart = true;

            else if (_mode == 3)
            {
                if (_direction == 0)
                    transitionEnd.X = _collisionRectangle.X - MathF.Ceiling(MapManager.ObjLink.Body.Width / 2f) - _positionOffset;
                else if (_direction == 1)
                    transitionEnd.Y = _collisionRectangle.Y - _positionOffset;
                else if (_direction == 2)
                    transitionEnd.X = _collisionRectangle.X + _collisionRectangle.Width + MathF.Ceiling(MapManager.ObjLink.Body.Width / 2f) + _positionOffset;
                else if (_direction == 3)
                    transitionEnd.Y = _collisionRectangle.Y + _collisionRectangle.Height + MapManager.ObjLink.Body.Height + _positionOffset;
            }
            else if (_mode == 4)
            {
                // why was this here?
                //transitionEnd.Y = _collisionRectangle.Y + _collisionRectangle.Height + MapManager.ObjLink._body.Height - _positionOffset;
            }
            else if (_mode == 5)
                transitionEnd = transitionStart + new Vector2(0, -0.5f) * 60 * (MapTransitionSystem.ChangeMapTime / 1000f);

            else if (_mode == 6)
                MapManager.ObjLink.NextMapFallRotateStart = true;

            // If it's an autosave door then store current map stuff.
            if (_savePosition)
            {
                MapManager.ObjLink.SaveMap = Map.MapName;
                MapManager.ObjLink.SavePosition = transitionEnd;

                // If using the ocarina to teleport to Manbo's pond overwrite the direction.
                MapManager.ObjLink.SaveDirection = (_entryId == "ocarina_entry") ? 3 : _direction;

                // If autosave is enabled, then save the game now.
                if (GameSettings.Autosave)
                {
                    SettingsSaveLoad.SaveSettings();
                    SaveGameSaveLoad.SaveGame(Game1.GameManager, true);
                    AchievementManager.Save();
                }
            }
            // If it's a Level 8 backdoor then force front entrance info.
            if (_backdoorLevel8)
            {
                MapManager.ObjLink.SaveMap = "dungeon8.map";
                MapManager.ObjLink.SavePosition = new Vector2(576, 1028);
                MapManager.ObjLink.SaveDirection = 1;
            }
            MapManager.ObjLink.NextMapPositionStart = transitionStart;
            MapManager.ObjLink.NextMapPositionEnd = transitionEnd;
            MapManager.ObjLink.TransitionInWalking = transitionStart != transitionEnd;
            MapManager.ObjLink.DirectionEntry = _direction;

            // no transition (eg. fall into a 2d room)
            if (_mode == 7)
            {
                MapManager.ObjLink.Fall2DEntry = true;
                MapManager.ObjLink.NextMapPositionEnd = null;
            }
        }
    }
}
