using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;
using ProjectZ.InGame.Controls;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjJump : GameObject
    {
        private readonly PushableComponent _pushComponent;
        private readonly Vector2 _offset;

        private readonly float _inertiaTime;
        private readonly float _height;
        private readonly float _speed;
        private readonly int _direction;
        private readonly bool _ignoreCollision;
        private readonly bool _moveOnTop;

        public ObjJump() : base("editor jump")
        {
            EditorColor = Color.Pink * 0.5f;
        }

        public ObjJump(Map.Map map, int posX, int posY, int offsetX, int offsetY, int fieldWidth, int fieldHeight,
            float height, float speed, int inertiaTime, bool ignoreCollision, bool moveOnTop) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, fieldWidth, fieldHeight);

            _offset = new Vector2(offsetX, offsetY);
            _height = height;
            _speed = speed;
            _inertiaTime = inertiaTime;
            _ignoreCollision = ignoreCollision;
            _moveOnTop = moveOnTop;

            _direction = AnimationHelper.GetDirection(_offset);

            var box = new CBox(EntityPosition, 0, 0, fieldWidth, fieldHeight, 16);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(box, OnPush));
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            // Get the direction the player is pushing towards.
            var cliffDir = AnimationHelper.GetDirection(direction);

            // If the jump direction doesn't match the push direction.
            if (cliffDir != _direction || type == PushableComponent.PushType.Impact)
                return false;

            // Dashing with the Pegasus Boots bypasses both the analog threshold and the inertia wait.
            // The boots lock the run direction, so the stick is not required to be held.
            if (!MapManager.ObjLink.IsDashing())
            {
                // Get the amount the player is pushing the analog stick.
                var vecDirection = ControlHandler.GetMoveVector2();

                // Check to see if it breaches the threshold set.
                bool doJump = cliffDir switch
                {
                    0 => vecDirection.X < -0.85f,
                    1 => vecDirection.Y < -0.85f,
                    2 => vecDirection.X > 0.85f,
                    3 => vecDirection.Y > 0.85f,
                    _ => false
                };
                // If the player is not holding the analog stick full tilt then don't jump.
                if (!doJump)
                    return false;
            }
            // we do the inertia counter stuff in the object because we ignore it while the player is running at the ObjJump
            // otherwise we would collide with the object and bounce off
            // the object was pushed the last frame?
            if (_pushComponent.LastWaitTime >= Game1.TotalGameTimeLast)
            {
                _pushComponent.InertiaCounter -= Game1.DeltaTime;
                _pushComponent.LastWaitTime = Game1.TotalGameTime;
            }
            else
            {
                // reset inertia counter if pushing has just begone
                _pushComponent.InertiaCounter = _inertiaTime;
                _pushComponent.LastWaitTime = Game1.TotalGameTime;
            }

            if (_pushComponent.InertiaCounter > 0 && !MapManager.ObjLink.IsDashing())
                return false;

            // calculate the goal position based on the offset, object position and the player position
            var playerBody = MapManager.ObjLink.Body;
            var goalPosition = MapManager.ObjLink.Position;

            if (cliffDir == 0)
                goalPosition.X = EntityPosition.Position.X + EntitySize.Width + _offset.X - playerBody.Width / 2;
            else if (cliffDir == 2)
                goalPosition.X = EntityPosition.Position.X + _offset.X + playerBody.Width / 2;
            else if (cliffDir == 1)
                goalPosition.Y = EntityPosition.Position.Y + EntitySize.Height + _offset.Y;
            else if (cliffDir == 3)
                goalPosition.Y = EntityPosition.Position.Y + _offset.Y + playerBody.Height;

            if (cliffDir % 2 != 0)
                goalPosition.X += _offset.X;
            if (cliffDir % 2 == 0)
                goalPosition.Y += _offset.Y;

            var goalPositionZ = 0f;

            // do not initiate a jump if there is something in the way
            if (!_ignoreCollision || _moveOnTop)
            {
                var collidingBox = Box.Empty;
                if (Map.Objects.Collision(
                    new Box(goalPosition.X + playerBody.OffsetX, goalPosition.Y + playerBody.OffsetY, 0,
                        playerBody.Width, playerBody.Height, 8),
                    Box.Empty, Values.CollisionTypes.Normal, 0, 0, ref collidingBox))
                {
                    if (!_moveOnTop || collidingBox.Z + collidingBox.Depth > 8)
                        return true;

                    // jump on top of the colliding box
                    // this does only work if we only colliding with one box or all the boxes we are colliding with have the same height
                    goalPositionZ = collidingBox.Top;
                }
            }

            var offsetLength = _offset.Length();

            var jumpMult = 1.0f;
            if (offsetLength > 16)
                jumpMult += (offsetLength - 16) / 32;
            if (_offset.Y < -4)
                jumpMult *= 0.75f;

            var speedMult = 1.0f;
            if (offsetLength > 16)
                speedMult = 1 - (offsetLength - 16) / 80;

            MapManager.ObjLink.StartRailJump(goalPosition, jumpMult * _height, speedMult * _speed, goalPositionZ);

            return true;
        }
    }
}
