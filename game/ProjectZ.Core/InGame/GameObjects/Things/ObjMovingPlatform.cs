using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Systems;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    class ObjMovingPlatform : GameObject
    {
        private readonly DrawSpriteComponent _spriteComponent;
        private readonly List<GameObject> _collidingObjects = new List<GameObject>();

        private readonly DictAtlasEntry _sprite;

        private readonly CBox _moveBox;
        private Box _lastBox;

        private readonly CBox _collisionBox;
        private Box _lastCollisionBox;

        private readonly Vector2 _startPosition;
        private readonly Vector2 _endPosition;

        private Vector2 _newPosition;

        private float _waitCounter;
        private float _state;
        private float _dir;

        private readonly float _startState;
        private readonly float _startDir;

        private readonly int _time;
        private readonly int _mode;

        private bool _isStandingOnTop;
        private bool _wasStandingOnTop;
        private bool _wasSinking;

        private float _returnCounter;

        private const float SinkSpeed = 0.5f;
        private const float ReturnDelay = 10000f;

        public ObjMovingPlatform() : base("moving_platform") { }

        public ObjMovingPlatform(Map.Map map, int posX, int posY, int offsetX, int offsetY, float state, int time, int mode) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            ResetPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 32, 16);

            CanReset = true;
            OnReset = Reset;

            _sprite = Resources.GetSprite("moving_platform");

            _startPosition = new Vector2(posX, posY);
            _endPosition = _startPosition + new Vector2(offsetX, offsetY);

            _state = _startState = (state / 2) * time;
            _dir = _startDir = state > time ? -1 : 1;
            _time = time;

            _mode = mode;

            _newPosition = _startPosition;
            _returnCounter = ReturnDelay;

            _moveBox = new CBox(EntityPosition, 0, -8, 0, 32, 16, 16);
            _collisionBox = new CBox(EntityPosition, 0, 0, 32, 16, 16);

            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(_collisionBox, Values.CollisionTypes.Normal | Values.CollisionTypes.MovingPlatform));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, _spriteComponent = new DrawSpriteComponent(
                Resources.SprObjects, EntityPosition, _sprite.SourceRectangle, Vector2.Zero, Values.LayerBottom));
        }

        public override void Reset()
        {
            _state = _startState;
            _dir = _startDir;
            _waitCounter = 0;
            _returnCounter = ReturnDelay;

            _isStandingOnTop = false;
            _wasStandingOnTop = false;
            _wasSinking = false;

            // Clear the face on the mode 2 platform.
            _spriteComponent.Sprite.SourceRectangle.Y = _sprite.SourceRectangle.Y;

            var percentage = (float)(-Math.Cos((_state / _time) * Math.PI) + 1) * 0.5f;
            _newPosition = _mode == 0
                ? Vector2.Lerp(_startPosition, _endPosition, percentage)
                : Vector2.Lerp(_startPosition, _endPosition, _state / _time);

            EntityPosition.Set(_newPosition);

            // The boxes are derived from EntityPosition, so make sure the first
            // update after the reset does not compare against pre-reset geometry.
            _lastBox = _moveBox.Box;
            _lastCollisionBox = _collisionBox.Box;
        }

        private void Update()
        {
            _lastBox = _moveBox.Box;
            _lastCollisionBox = _collisionBox.Box;

            if (_mode == 0)
                UpdateMove0();
            else if (_mode == 1)
                UpdateMode1();
            else if (_mode == 2)
                UpdateMode2();

            var bodyDirection = _newPosition - EntityPosition.Position;

            EntityPosition.Set(_newPosition);

            MoveBodies(bodyDirection);
        }

        // Normal moving platform.
        private void UpdateMove0()
        {
            // Update the move or the wait counter.
            if (_waitCounter <= 0)
            {
                _state += (Game1.DeltaTime - _waitCounter) * _dir;
                _waitCounter = 0;
            }
            else
                _waitCounter -= Game1.DeltaTime;

            // Finished moving to the start/end.
            if (_state < 0)
            {
                _dir = -_dir;
                _state = 0;
                _waitCounter = 175;
            }
            else if (_state > _time)
            {
                _dir = -_dir;
                _state = _time;
                _waitCounter = 175;
            }

            var percentage = (float)(-Math.Cos((_state / _time) * Math.PI) + 1) * 0.5f;
            _newPosition = Vector2.Lerp(_startPosition, _endPosition, percentage);
        }

        private void UpdateReturn(bool isOccupied)
        {
            if (Camera.ClassicMode)
                return;

            // Standing on the platform restarts the timer, whether or not it is sinking.
            if (isOccupied)
            {
                _returnCounter = ReturnDelay;
                return;
            }

            if (_state <= 0)
                return;

            if (_returnCounter > 0)
                _returnCounter -= Game1.DeltaTime;
            else
                _state = Math.Max(_state - Game1.DeltaTime * SinkSpeed, 0);
        }

        private bool Intersection()
        {
            return (MapManager.ObjLink.Body.BodyBox.Box.Intersects(_moveBox.Box) && MapManager.ObjLink.Body.IsGrounded);
        }

        private bool IsStandingOnTop()
        {
            if (!Intersection())
                return false;

            return (_state >= _time) ||
                   (MapManager.ObjLink.Body.BodyBox.Box.Left >= _moveBox.Box.Left &&
                    MapManager.ObjLink.Body.BodyBox.Box.Right < _moveBox.Box.Right);
        }

        private void UpdateMode1()
        {
            // Check if the player is standing on the platform.
            _isStandingOnTop = IsStandingOnTop();

            if (_isStandingOnTop && _state < _time)
            {
                // Sound effect when the platform starts to sink.
                if (!_wasStandingOnTop)
                    Game1.AudioManager.PlaySoundEffect("D378-17-11");

                // The platform never rises again, it only holds when the player steps off.
                _state = Math.Min(_state + Game1.DeltaTime * SinkSpeed, _time);
            }
            UpdateReturn(_isStandingOnTop);

            // Lower the platform at a constant speed.
            _newPosition = Vector2.Lerp(_startPosition, _endPosition, _state / _time);
            _wasStandingOnTop = _isStandingOnTop;
        }

        private void UpdateMode2()
        {
            // Check if the player is standing on the platform.
            var intersection = Intersection();
            _isStandingOnTop = IsStandingOnTop();

            // Show the face struggling to hold Link up.
            _spriteComponent.Sprite.SourceRectangle.Y = _sprite.SourceRectangle.Y + (intersection ? 16 : 0);

            // The platform only sinks while both conditions hold; the moment the player
            // steps off or drops what they are carrying it stops where it is and stays there.
            var isSinking = _isStandingOnTop && MapManager.ObjLink.CurrentState == ObjLink.State.Carrying;

            if (isSinking && _state < _time)
            {
                // Sound effect when the platform starts to sink.
                if (!_wasSinking)
                    Game1.AudioManager.PlaySoundEffect("D378-17-11");

                // The platform never rises again, it only holds when the player steps off.
                _state = Math.Min(_state + Game1.DeltaTime * SinkSpeed, _time);
            }
            UpdateReturn(_isStandingOnTop);

            // Lower the platform at a constant speed.
            _newPosition = Vector2.Lerp(_startPosition, _endPosition, _state / _time);
            _wasSinking = isSinking;
        }

        private void MoveBodies(Vector2 direction)
        {
            // Check for colliding bodies and push them forward.
            _collidingObjects.Clear();
            Map.Objects.GetComponentList(_collidingObjects,
                (int)_lastBox.Left, (int)_lastBox.Back - 8, (int)_lastBox.Width, (int)_lastBox.Height, BodyComponent.Mask);

            foreach (var collidingObject in _collidingObjects)
            {
                var body = (BodyComponent)collidingObject.Components[BodyComponent.Index];

                if (body.BodyBox.Box.Front <= _lastCollisionBox.Back && body.BodyBox.Box.Intersects(_lastBox))
                {
                    var offset = Vector2.Zero;

                    // Body standing on the platform.
                    if (body.IsGrounded)
                    {
                        var add = Vector2.Zero;

                        // Align the body with the platform so that the body is not wobbling around.
                        if (Math.Abs(body.VelocityTarget.X) < 0.1f && Math.Abs(body.Velocity.X) < 0.1f)
                        {
                            var distance = (body.Position.X + direction.X) - EntityPosition.X;
                            var distanceNormal = (int)Math.Round(distance * MapManager.Camera.Scale, MidpointRounding.AwayFromZero) / MapManager.Camera.Scale;

                            var dir = distanceNormal - distance;
                            if (Math.Abs(dir) > 0.005)
                                add.X += dir;
                        }

                        offset = direction + add;

                        // Put the body on top of the platform.
                        if (direction.Y != 0)
                            offset.Y = _collisionBox.Box.Back - body.BodyBox.Box.Front;
                    }
                    // Did the platform already move into the body?
                    else if (body.BodyBox.Box.Intersects(_collisionBox.Box))
                    {
                        // Move the body up/down.
                        if (direction.Y < 0)
                            offset.Y = _collisionBox.Box.Back - body.BodyBox.Box.Front - 0.05f;
                        else if (direction.Y > 0)
                            offset.Y = _collisionBox.Box.Back - body.BodyBox.Box.Front + 0.05f;
                    }

                    if (offset != Vector2.Zero)
                    {
                        SystemBody.MoveBody(body, offset, body.CollisionTypes, false, false, false);
                        body.Position.NotifyListeners();
                    }
                }
            }
        }
    }
}
