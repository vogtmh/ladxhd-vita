using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjColorJumpTile : GameObject
    {
        private readonly List<GameObject> _collidingObjects = new List<GameObject>();
        private readonly DictAtlasEntry[] _sprites = new DictAtlasEntry[3];

        private readonly CSprite _sprite;
        private readonly ObjHole _objHole;

        private bool _restoreMode;
        private float _restoreCounter;

        private int _currentState;
        private readonly int _startState;
        
        private Rectangle _collisionRectangle;
        private Rectangle _fieldRectangle;

        public ObjColorJumpTile() : base("color_tile_0") { }

        public ObjColorJumpTile(Map.Map map, int posX, int posY, int state) : base(map)
        {
            Tags = Values.GameObjectTag.None;

            _sprites[0] = Resources.GetSprite("color_tile_0");
            _sprites[1] = Resources.GetSprite("color_tile_1");
            _sprites[2] = Resources.GetSprite("color_tile_2");

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _startState = Math.Clamp(state, 0, 2);
            _currentState = _startState;
            _collisionRectangle = new Rectangle(posX, posY, Values.TileSize, Values.TileSize);

            _fieldRectangle = map.GetField(posX, posY);

            _sprite = new CSprite(_sprites[_currentState], EntityPosition, Vector2.Zero);

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            var drawComponent = new DrawCSpriteComponent(_sprite, Values.LayerBottom);
            AddComponent(DrawComponent.Index, drawComponent);

            _restoreCounter = Game1.RandomNumber.Next(500, 1500);

            // Spawn a hole under each object so that when it depletes the hole becomes active.
            _objHole = new ObjHole(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 16, 14, Rectangle.Empty, 0, 1, 0) { IsActive = false };
            Map.Objects.SpawnObject(_objHole);

            // Register as always animate object so that tiles respawn in Classic Camera.
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // Check if the player left the field.
            var fieldChange = _currentState != _startState && !_fieldRectangle.Contains(MapManager.ObjLink.CenterPosition);

            // In classic camera the tiles immediately restore when the player leaves the room.
            if (Camera.ClassicMode)
            {
                if (fieldChange)
                    OffsetState(_startState - _currentState);
            }
            // Otherwise the tiles slowly restore one state at a time.
            else
            {
                // Start "restore mode" on field change.
                if (fieldChange)
                    _restoreMode = true;

                // If restore mode is set.
                if (_restoreMode)
                {
                    // Start the counter.
                    _restoreCounter -= Game1.DeltaTime;

                    // When the counter expires.
                    if (_restoreCounter <= 0)
                    {
                        // Restore the tile by one state and reset the time by random amount.
                        _restoreCounter = Game1.RandomNumber.Next(350, 750);
                        OffsetState(-1);

                        // When the tile is restored disable restore mode.
                        if (_currentState == _startState)
                            _restoreMode = false;
                    }
                }
            }
            // If the tile is fully depleted return.
            if (_currentState == 3)
                return;
            
            // Try to find colliding objects.
            _collidingObjects.Clear();
            Map.Objects.GetComponentList(_collidingObjects, _collisionRectangle.X, _collisionRectangle.Y, _collisionRectangle.Width, _collisionRectangle.Height, BodyComponent.Mask);

            // Loop through objects found.
            foreach (var collidingObject in _collidingObjects)
            {
                // If the player is standing on the tile, force a jump.
                if (collidingObject is ObjLink link && _collisionRectangle.Contains(link.Body.BodyBox.Box.Center) && link.Body.IsGrounded)
                {
                    link.StartJump();
                    OffsetState(1);
                }
                // If the enemy is a "Bone Putter" also force it to jump.
                else if (collidingObject is EnemyBonePutter bonePutter && collidingObject.Components[BodyComponent.Index] is BodyComponent bodyComponent)
                {
                    if (bonePutter.StartJump() && _collisionRectangle.Contains(bodyComponent.BodyBox.Box.Center))
                        OffsetState(1);
                }
            }
        }

        private void OffsetState(int offset)
        {
            // Change the tile's current state by the offset.
            _currentState += offset;
            _currentState = MathHelper.Clamp(_currentState, _startState, 3);

            // Set the sprite (green > yellow > red).
            if (_currentState < 3)
                _sprite.SetSprite(_sprites[_currentState]);

            // If the tile is depleted then hide it.
            _sprite.IsVisible = _currentState != 3;

            // Activate/deactivate the hole based on the tile state.
            _objHole.IsActive = _currentState == 3;
        }
    }
}
