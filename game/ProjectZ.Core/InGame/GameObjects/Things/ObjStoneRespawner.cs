using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjStoneRespawner : GameObject
    {
        private readonly string _spriteId;
        private readonly string _spawnItem;
        private readonly string _pickupKey;
        private readonly string _dialogPath;
        private readonly bool _isHeavy;
        private readonly bool _potMessage;
        private readonly bool _itemRespawns;

        private int _lastFieldTime;
        private bool _respawnedStone;
        private Rectangle _objectField;

        public ObjStoneRespawner(Map.Map map, int posX, int posY, string spriteId, string spawnItem, string pickupKey, string dialogPath, bool isHeavy, bool potMessage, bool fromSpawner, bool itemsRespawn) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _spriteId = spriteId;
            _pickupKey = pickupKey;
            _dialogPath = dialogPath;
            _isHeavy = isHeavy;
            _potMessage = potMessage;
            _respawnedStone = fromSpawner;
            _itemRespawns = itemsRespawn;

            // Only the first pot should contain the fairy.
            if (spawnItem != "fairy")
                _spawnItem = spawnItem;
            else
                _spawnItem = "";

            _lastFieldTime = Map.GetUpdateState(EntityPosition.Position);
            _objectField = Map.GetField(new Vector2(posX + 8, posY + 8));

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // If the object has already been spawned from a spawner, delete this.
            if (_respawnedStone)
            {
                DeleteThis();
                return;
            }
            // If classic camera mode is active, respawn when leaving the current field.
            if (Camera.ClassicMode)
            {
                // Respawn once this object's field is no longer the one being shown.
                if (MapManager.ObjLink.FieldTransition(_objectField))
                    SpawnStone();
            }
            // If the field is outside of the update range. Link must be at least 3
            // fields away from the field the object is found on for it to respawn.
            else
            {
                // Overworld / smooth camera mode: respawn when field update time changes
                var updateState = Map.GetUpdateState(EntityPosition.Position);

                if (_lastFieldTime < updateState)
                    SpawnStone();
            }
        }

        private void DeleteThis()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        private void SpawnStone()
        {
            // Remove the respawner object.
            Map.Objects.DeleteObjects.Add(this);

            // If the item is supposed to respawn, feed it into the parameter.
            string spawnedItem = _itemRespawns ? _spawnItem : null; 

            // Respawn original stone type
            Map.Objects.SpawnObject(new ObjStone(Map, (int)EntityPosition.X, (int)EntityPosition.Y, _spriteId, spawnedItem, _pickupKey, _dialogPath, _isHeavy, _potMessage, _itemRespawns));
        }
    }
}
