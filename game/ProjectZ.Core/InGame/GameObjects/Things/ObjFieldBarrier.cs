using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    public class ObjFieldBarrier : GameObject
    {
        public Box CollisionBox;
        public Rectangle Rect;
        public Point Position;
        public int Width;
        public int Height;

        public ObjFieldBarrier(Map.Map map, int posX, int posY, Values.CollisionTypes type, Rectangle rectangle) : base(map)
        {
            Position.X = posX;
            Position.Y = posY;

            Width  = rectangle.Width;
            Height = rectangle.Height;

            EditorIconSource = new Rectangle(0, 0, Width, Height);

            EntityPosition = new CPosition(Position.X, Position.Y, 0);
            EntitySize = new Rectangle(0, 0, Width, Height);

            Rect = rectangle;
            CollisionBox = new Box(Position.X + rectangle.X, Position.Y + rectangle.Y, 0, Width, Height, 16);

            AddComponent(CollisionComponent.Index, new CollisionComponent(DetectCollision) { CollisionType = type });
        }

        public void SetPosition(int posX, int posY)
        {
            Position.X = posX;
            Position.Y = posY;
            EntityPosition.Set(new Vector2(Position.X, Position.Y));
            CollisionBox = new Box(Position.X, Position.Y, 0, Width, Height, 16);
        }

        private bool DetectCollision(Box box, int dir, int level, ref Box collidingBox)
        {
            if (!CollisionBox.Intersects(box))
                return false;

            collidingBox = CollisionBox;
            return true;
        }
    }
}
