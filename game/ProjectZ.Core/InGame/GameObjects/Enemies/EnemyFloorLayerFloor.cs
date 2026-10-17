using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Things;
using ProjectZ.InGame.GameObjects.Things;
using System.Collections.Generic;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyFloorLayerFloor : GameObject
    {
        private readonly List<GameObject> _underlyingObjects = new List<GameObject>();

        public EnemyFloorLayerFloor(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);
            CanReset = false;

            // Remove the underlying objects.
            _underlyingObjects.Clear();
            Map.Objects.GetGameObjectsWithTag(_underlyingObjects, Values.GameObjectTag.Hole | Values.GameObjectTag.Trap, posX, posY, 16, 16);
            SetHoleState(false);

            AddComponent(DrawComponent.Index, new DrawSpriteComponent("d8 floor", EntityPosition, Vector2.Zero, Values.LayerBottom));
        }

        public void SetHoleState(bool active)
        {
            foreach (var gameObject in _underlyingObjects)
            {
                if (gameObject.EntityPosition.Position != EntityPosition.Position)
                    continue;
                if (gameObject is ObjHole hole)
                    hole.IsActive = active;
                else if (gameObject is ObjLava lava)
                    lava.SetActive(active);
            }
        }
    }
}
