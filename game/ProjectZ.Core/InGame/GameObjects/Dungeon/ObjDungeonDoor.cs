using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjDungeonDoor : GameObject
    {
        public enum DoorStates { Opening, Closing, Open, Closed }
        public DoorStates _currentState;

        private readonly BoxCollisionComponent _collisionComponent;
        private readonly CarriableComponent _carriableComponent;
        private readonly Rectangle _sourceRectangle;
        private readonly CSprite _sprite;

        private readonly string _strKey;
        private readonly string _strPushKey;
        private readonly string _pushItem;
        private readonly int _mode;

        private float _doorState;
        private bool _wasUpdated;
        private bool _closePending;

        public ObjDungeonDoor() : base("dungeon_door") { }

        public ObjDungeonDoor(Map.Map map, int posX, int posY, int mode, string strKey, int direction, string strPushKey) : base(map)
        {
            FreezePersist = true;
            _sourceRectangle = Resources.SourceRectangle("dungeon_door");
            
            _strKey = strKey;
            _strPushKey = strPushKey;
            _mode = mode;

            if (string.IsNullOrEmpty(_strKey))
            {
                IsDead = true;
                return;
            }

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _collisionComponent = new BoxCollisionComponent(new CBox(posX, posY, 0, 16, 16, 16), Values.CollisionTypes.Normal);
            _sprite = new CSprite(Resources.SprObjects, EntityPosition, Rectangle.Empty, new Vector2(8, 8));
            _sprite.Center = new Vector2(8, 8);
            _sprite.Rotation = (float)(Math.PI / 2 * (direction + 1));

            CRectangle grabBox = new CRectangle(EntityPosition, new Rectangle(1, 1, 14, 14));

            if (!string.IsNullOrEmpty(_strKey))
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(grabBox, null, null, null) { IsCollision = true });
            AddComponent(CollisionComponent.Index, _collisionComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerBottom));

            _sourceRectangle.X += mode * 16;

            if (mode == 1)
                _pushItem = "smallkey";
            else if (mode == 3)
                _pushItem = "nightmarekey";

            if (mode == 1 || mode == 3)
            {
                var pushBox = new CBox(EntityPosition, 0, 0, 16, 16, 8);
                AddComponent(PushableComponent.Index, new PushableComponent(pushBox, OnPush) { InertiaTime = 100 });
            }

            _sprite.SourceRectangle = _sourceRectangle;
        }

        private void Update()
        {
            _wasUpdated = true;

            if (_closePending)
                Close();

            if (_currentState == DoorStates.Opening)
            {
                _doorState -= Game1.TimeMultiplier * 0.05f;

                if (_doorState <= 0.5f)
                {
                    _collisionComponent.IsActive = false;
                    _carriableComponent.IsActive = false;
                }
                if (_doorState <= 0)
                {
                    _doorState = 0;
                    _currentState = DoorStates.Open;
                }
            }
            else if (_currentState == DoorStates.Closing)
            {
                _doorState += Game1.TimeMultiplier * 0.1f;
                if (_doorState >= 1)
                {
                    _doorState = 1;
                    _currentState = DoorStates.Closed;
                }
            }
            _sprite.SourceRectangle.Height = (int)Math.Round(16 * _doorState);
            _sprite.SourceRectangle.Y = _sourceRectangle.Y + 16 - _sprite.SourceRectangle.Height;
            _sprite.SpriteEffect = SpriteEffects.FlipHorizontally;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            // Don't trigger if shield is out or the door has already been opened.
            if (type == PushableComponent.PushType.Impact || _currentState != DoorStates.Closed)
                return false;

            // If it's the nightmare door, check for the key but don't consume it.
            if (_pushItem == "nightmarekey")
            {
                // If it's been collected, it will show up as "0" and not "null".
                if (Game1.GameManager.GetItem(_pushItem)?.Count == null)
                {
                    // Don't show the message if disable helper text is enabled.
                    if (GameSettings.NoHelperText)
                        return false;

                    // Start the dialog if the player doesn't have the nightmare key.
                    Game1.GameManager.StartDialogPath("door_" + _pushItem);
                    return false;
                }
            }
            // If it's a small key then try to remove one.
            else if (!Game1.GameManager.RemoveItem(_pushItem, 1))
            {
                return false;
            }
            // Only play the sound effect when the player uses a key to open the door.
            Game1.AudioManager.PlaySoundEffect("D378-04-04", false);

            // Save the status of this door being opened if door has a dictionary entry.
            if (!string.IsNullOrEmpty(_strPushKey))
                Game1.GameManager.SaveManager.SetString(_strPushKey, "1");

            return true;
        }

        private void Open()
        {
            _currentState = DoorStates.Opening;
        }

        private void Close()
        {
            // Don't slam the door on the player; wait until the doorway is clear.
            if (DoorBlockedByPlayerBody())
            {
                _closePending = true;
                return;
            }
            _closePending = false;
            _currentState = DoorStates.Closing;
            _collisionComponent.IsActive = true;
            _carriableComponent.IsActive = true;
            Game1.AudioManager.PlaySoundEffect("D378-16-10", false);
        }

        private bool DoorBlockedByPlayerBody()
        {
            var tile = new Box(EntityPosition.X, EntityPosition.Y, 0, 16, 16, 16);
            return MapManager.ObjLink.Body.BodyBox.Box.Intersects(tile);
        }

        private void KeyChanged()
        {
            // open/close the door if it is not already in the right state
            // 1: open, 0: closed
            var value = Game1.GameManager.SaveManager.GetString(_strKey);
            var openDoor = value != null && value != "0";

            if (_wasUpdated)
            {
                if (_currentState != DoorStates.Open && openDoor)
                    Open();
                else if (_currentState != DoorStates.Closed && _currentState != DoorStates.Closing && !openDoor)
                    Close();
            }
            else
            {
                // set the door to open or closed
                if (openDoor)
                {
                    _currentState = DoorStates.Open;
                    _collisionComponent.IsActive = false;
                    _carriableComponent.IsActive = false;
                    _doorState = 0;
                }
                else
                {
                    _currentState = DoorStates.Closed;
                    _collisionComponent.IsActive = true;
                    _carriableComponent.IsActive = true;
                    _doorState = 1;
                }
            }
        }
        public int GetMode()
        {
            return _mode;
        }
    }
}
