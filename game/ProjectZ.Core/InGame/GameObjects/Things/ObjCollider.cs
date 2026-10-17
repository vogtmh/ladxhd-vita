using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjCollider : GameObject
    {
        private Box _singleCollisionBox;
        private Box[] CollisionBoxes { get; }

        private readonly Color _editorColor = Color.DarkRed * 0.65f;
        private readonly int _level = -1;

        public ObjCollider(Map.Map map, int posX, int posY, bool grabComponent, Color editorColor, Values.CollisionTypes type, params Rectangle[] rectangles) : base(map)
        {
            EditorIconSource = new Rectangle(0, 0, 16, 16);
            _editorColor = editorColor;

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            CollisionBoxes = new Box[rectangles.Length];
            for (var i = 0; i < rectangles.Length; i++)
            {
                CollisionBoxes[i] = new Box(posX + rectangles[i].X, posY + rectangles[i].Y, 0, rectangles[i].Width, rectangles[i].Height, 16);

                if (grabComponent)
                    AddComponent(CarriableComponent.Index, new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(rectangles[i].X + 1, rectangles[i].Y + 1, rectangles[i].Width -2 , rectangles[i].Height - 2)), null, null, null) { IsCollision = true });
            }
            AddComponent(CollisionComponent.Index, new CollisionComponent(MultiBoxCollision) { CollisionType = type });
        }

        public ObjCollider(Map.Map map, int posX, int posY, bool grabComponent, int height, Rectangle rectangle, Values.CollisionTypes type, int level) : base(map)
        {
            if (type == Values.CollisionTypes.NonWater)
                _editorColor = Color.DarkBlue * 0.65f;

            EditorIconSource = new Rectangle(0, 0, 16, 16);

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _level = level;
            _singleCollisionBox = new Box(
                posX + rectangle.X, posY + rectangle.Y, 0,
                rectangle.Width, rectangle.Height, height);

            AddComponent(CollisionComponent.Index, new CollisionComponent(SingleBoxCollision) { CollisionType = type });
            if (grabComponent)
                AddComponent(CarriableComponent.Index, new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(1, 1, 14, 14)), null, null, null) { IsCollision = true });
        }

        private bool MultiBoxCollision(Box box, int dir, int level, ref Box collidingBox)
        {
            foreach (var singleBox in CollisionBoxes)
            {
                if (singleBox.Intersects(box))
                {
                    collidingBox = singleBox;
                    return true;
                }
            }
            return false;
        }

        private bool SingleBoxCollision(Box box, int dir, int level, ref Box collidingBox)
        {
            if ((_level != -1 && _level < level) || !_singleCollisionBox.Intersects(box))
                return false;

            collidingBox = _singleCollisionBox;
            return true;
        }

        public override void DrawEditor(SpriteBatch spriteBatch, Vector2 drawPosition)
        {
            if (CollisionBoxes != null)
            {
                for (var i = 0; i < CollisionBoxes.Length; i++)
                {
                    spriteBatch.Draw(Resources.SprWhite,
                        new Rectangle(
                            (int)(drawPosition.X + CollisionBoxes[i].X),
                            (int)(drawPosition.Y + CollisionBoxes[i].Y),
                            (int)CollisionBoxes[i].Width,
                            (int)CollisionBoxes[i].Height), _editorColor);
                }
            }
            else
            {
                spriteBatch.Draw(Resources.SprWhite,
                    new Rectangle(
                        (int)(drawPosition.X + _singleCollisionBox.X),
                        (int)(drawPosition.Y + _singleCollisionBox.Y),
                        (int)_singleCollisionBox.Width,
                        (int)_singleCollisionBox.Height), _editorColor);
            }
        }
    }
}
