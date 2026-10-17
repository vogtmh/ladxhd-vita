using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjHole : GameObject
    {
        private readonly BoxCollisionComponent _collisionComponent;
        private readonly DrawSpriteComponent _drawComponent;
        private readonly UpdateComponent _updateComponent;
        private Rectangle _spawnField;

        public readonly Vector2 Center;
        public readonly int Color;
        public CBox CollisionBox;

        public ObjHole() : base("hole_0") { }

        public ObjHole(Map.Map map, int posX, int posY, int width, int height, Rectangle sourceRectangle, int offsetX, int offsetY, int color) : base(map)
        {
            Tags = Values.GameObjectTag.Hole;

            Center = new Vector2(posX + offsetX + width / 2, posY + offsetY + height / 2);
            Color = color;

            if (sourceRectangle == Rectangle.Empty)
            {
                EntityPosition = new CPosition(posX + offsetX, posY + offsetY, 0);
                EntitySize = new Rectangle(0, 0, width, height);
            }
            else
            {
                EntityPosition = new CPosition(posX, posY, 0);
                EntitySize = new Rectangle(0, 0, sourceRectangle.Width, sourceRectangle.Height);
            }
            float holePosX = posX + offsetX;
            float holePosY = posY + offsetY;
            float holeWidth = width;
            float holeHeight = height;

            CollisionBox = new CBox(holePosX, holePosY, 0, holeWidth, holeHeight, 16);
            AddComponent(CollisionComponent.Index, _collisionComponent = new BoxCollisionComponent(CollisionBox, Values.CollisionTypes.Hole));
            AddComponent(UpdateComponent.Index, _updateComponent = new UpdateComponent(Update) { IsActive = false }) ;

            // visible hole?
            if (sourceRectangle != Rectangle.Empty)
            {
                _drawComponent = new DrawSpriteComponent(Resources.SprObjects, EntityPosition, sourceRectangle, new Vector2(0, 0), Values.LayerBottom);
                AddComponent(DrawComponent.Index, _drawComponent);
            }
        }

        public void BushSpawn()
        {
            // Track if spawned from a bush so it can be reset.
            _spawnField = Map.GetField(Center);
            Map.Objects.RegisterAlwaysAnimateObject(this);
            _updateComponent.IsActive = true;
        }

        private void Update()
        {
            // When Classic Camera is active.
            if (Camera.ClassicMode)
            {
                // Despawn once a scroll settles on a field other than the one the hole is on.
                if (MapManager.ObjLink.FieldTransition(_spawnField))
                    Map.Objects.DeleteObjects.Add(this);
            }
        }

        public void SetActive(bool state)
        {
            if (_drawComponent != null)
                _drawComponent.IsActive = state;
            _collisionComponent.IsActive = state;
        }
    }
}
