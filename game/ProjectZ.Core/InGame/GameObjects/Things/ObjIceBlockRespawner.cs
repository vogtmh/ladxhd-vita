using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjIceBlockRespawner : GameObject
    {
        private int _lastFieldTime;
        private readonly bool _respawnedFromSpawner;
        private Rectangle _objectField;

        public ObjIceBlockRespawner(Map.Map map, int posX, int posY, bool fromSpawner) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _respawnedFromSpawner = fromSpawner;
            _lastFieldTime = Map.GetUpdateState(EntityPosition.Position);
            _objectField = Map.GetField(new Vector2(posX + 8, posY + 8));

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // If this respawner already spawned the block this frame, delete it
            if (_respawnedFromSpawner)
            {
                DeleteThis();
                return;
            }
            // If classic camera mode is active, respawn when leaving the current field.
            if (Camera.ClassicMode)
            {
                // Respawn once this object's field is no longer the one being shown.
                if (MapManager.ObjLink.FieldTransition(_objectField))
                    SpawnIceBlock();
            }
            // If the field is outside of the update range. Link must be at least 3
            // fields away from the field the object is found on for it to respawn.
            else
            {
                var updateState = Map.GetUpdateState(EntityPosition.Position);
                if (_lastFieldTime < updateState)
                    SpawnIceBlock();
            }
        }

        private void DeleteThis()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        private void SpawnIceBlock()
        {
            // Remove respawner
            Map.Objects.DeleteObjects.Add(this);

            // Respawn Ice Block
            Map.Objects.SpawnObject(new ObjIceBlock(Map,
                (int)EntityPosition.X,
                (int)EntityPosition.Y));
        }
    }
}
