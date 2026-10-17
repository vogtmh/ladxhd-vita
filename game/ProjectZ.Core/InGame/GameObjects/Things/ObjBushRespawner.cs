using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjBushRespawner : GameObject
    {
        private readonly string _spawnItem;
        private readonly string _spriteId;
        private readonly bool _hasCollider;
        private readonly bool _drawShadow;
        private readonly bool _setGrassField;
        private readonly int _drawLayer;
        private readonly string _pickupKey;
        private int _lastFieldTime;
        private Rectangle _objectField;

        public ObjBushRespawner(Map.Map map, int posX, int posY, string spawnItem, string spriteId,
            bool hasCollider, bool drawShadow, bool setGrassField, int drawLayer, string pickupKey) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _spawnItem = spawnItem;
            _spriteId = spriteId;
            _hasCollider = hasCollider;
            _drawShadow = drawShadow;
            _setGrassField = setGrassField;
            _drawLayer = drawLayer;
            _pickupKey = pickupKey;
            _objectField = Map.GetField(new Vector2(posX + 8, posY + 8));

            _lastFieldTime = Map.GetUpdateState(EntityPosition.Position);

            // add key change listener
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // If classic camera mode is active, respawn when leaving the current field.
            if (Camera.ClassicMode)
            {
                // Respawn once a scroll settles on a field other than this one.
                if (MapManager.ObjLink.FieldTransition(_objectField))
                    SpawnObject();
            }
            // If the field is outside of the update range. Link must be at least 3
            // fields away from the field the object is found on for it to respawn.
            else
            {
                var updateState = Map.GetUpdateState(EntityPosition.Position);
                if (_lastFieldTime < updateState)
                    SpawnObject();
            }
        }

        private void SpawnObject()
        {
            // Delete the respawn object and respawn the bush or grass object.
            Map.Objects.DeleteObjects.Add(this);
            Map.Objects.SpawnObject(new ObjBush(Map, (int)EntityPosition.X, (int)EntityPosition.Y,
                _spawnItem, _spriteId, _hasCollider, _drawShadow, _setGrassField, _drawLayer, _pickupKey));
        }
    }
}
