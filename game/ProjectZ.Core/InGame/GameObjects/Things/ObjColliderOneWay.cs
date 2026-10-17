using System;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjColliderOneWay : GameObject
    {
        private readonly Box _collisionBox;
        private readonly Box _solidBox;
        private readonly int _direction;
        private readonly bool _isPusher;

        // Matches the depth of the "stairs" on the corners of 2D collision. This opening on
        // the collision is used to push Link upward when landing inside it if he is jumping.
        private const float ClipDepth = 4f;

        // The clip is only allowed if Link jumped into the air, or the surface is above
        // the ground he left. Walking off a ledge onto an equally high ledge doesn't count.
        private bool ClipAllowed(ObjLink link) => link.JumpedFromGround2D || _collisionBox.Y < link.LastGroundY2D - 0.1f;

        public ObjColliderOneWay(Map.Map map, int posX, int posY, Rectangle collisionRectangle, Values.CollisionTypes type, int direction, bool isPusher) : base(map)
        {
            SprEditorImage = Resources.SprWhite;
            EditorIconSource = new Rectangle(0, 0, collisionRectangle.Width, collisionRectangle.Height);
            EditorColor = Color.DeepPink;

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = collisionRectangle;

            _collisionBox = new Box(posX + collisionRectangle.X, posY + collisionRectangle.Y, 0, collisionRectangle.Width, collisionRectangle.Height, 3.25f);
            _solidBox = new Box(posX, posY, 0, 16, 16, 3.25f);
            _direction = direction;
            _isPusher = isPusher;

            AddComponent(CollisionComponent.Index, new CollisionComponent(CollisionCheck) { CollisionType = type });
        }

        private bool CollisionCheck(Box box, int dir, int level, ref Box collidingBox)
        {
            // Store some of Link's properties.
            var Link = MapManager.ObjLink;
            var Body = Link.Body;

            // We can't compare the box parameter directly with Link's body box, so compare the dimensions.
            if (box.Width != Body.BodyBox.Box.Width || box.Height != Body.BodyBox.Box.Height)
                return false;

            // A battery of checks in 2D to determine if Link hits the corner of a collider. From there he will either be teleported 
            // upward to the top of it if jumping, or the tile will act as full collision if walking off the ledge. This runs on the 
            // horizontal pass so it resolves before the stair-stepped collider underneath can catch him and set "IsGrounded".
            bool horizontalDir   = dir % 2 == 0;
            bool belowSurface    = Link.EntityPosition.Y > _collisionBox.Y;
            bool withinClipDepth = Link.EntityPosition.Y - _collisionBox.Y <= ClipDepth;
            bool isLinkFalling   = Body.Velocity.Y > 0;
            bool boxesIntersect  = _solidBox.Intersects(box);

            if (_isPusher && Link.Is2DMode && horizontalDir && belowSurface && withinClipDepth && isLinkFalling && boxesIntersect)
            {
                // He walked off a ledge rather than launching himself, so block the entry.
                if (!ClipAllowed(Link))
                {
                    collidingBox = _solidBox;
                    return true;
                }
                // The clip is allowed, so lift him to the surface and let the move complete.
                Link.SetPosition(new Vector2(Link.EntityPosition.X, _collisionBox.Y - 1));

                // Force the walking animation now that we're on solid ground.
                if (Link.CurrentState == ObjLink.State.Jumping)
                    Link.Animation.Play("walk_" + Link.Direction);

                return false;
            }
            // The direction of incoming collision must match the parameter set in the constructor and the boxes must be colliding.
            if (dir != _direction || !_collisionBox.Intersects(box))
                return false;

            // The collider type is a "pusher" set by the parameter in the constructor.
            if (_isPusher)
            {
                // When used on 3D maps, it acts as collision in a single direction.
                if (!Link.Is2DMode || Link.EntityPosition.Y <= _collisionBox.Y || Body.Velocity.Y <= 0)
                {
                    collidingBox = _collisionBox;
                    return true;
                }
                // The corner clip failed, so let him pass through instead of teleporting up.
                if (!ClipAllowed(Link))
                    return false;

                // On 2D Maps, when Link is below the top, "teleport" him to the top of the collision.
                if (Math.Abs(Link.EntityPosition.Y - _collisionBox.Y) > 0.1f)
                    Link.SetPosition(new Vector2(Link.EntityPosition.X, _collisionBox.Y - 1));

                // Force Link into the walking state from the jumping state after the teleport.
                if (Link.CurrentState == ObjLink.State.Jumping)
                    Link.Animation.Play("walk_" + Link.Direction);
            }
            collidingBox = _collisionBox;
            return true;
        }
    }
}
