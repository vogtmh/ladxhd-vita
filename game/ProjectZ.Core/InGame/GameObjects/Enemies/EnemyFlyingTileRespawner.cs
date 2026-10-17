using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class EnemyFlyingTileRespawner : GameObject
    {
        private readonly string _strKey;
        private readonly int _index;
        private readonly int _mode;
        private readonly int _lastFieldTime;
        private readonly Rectangle _fieldRect;

        public EnemyFlyingTileRespawner(Map.Map map, int posX, int posY, string strKey, int index, int mode) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _strKey = strKey;
            _index  = index;
            _mode   = mode;

            // Use the center of the tile so a tile sitting on a field border resolves to its own field.
            _fieldRect = Map.GetField(new Vector2(posX + 8, posY + 8));
            _lastFieldTime = Map.GetUpdateState(new Vector2(posX + 8, posY + 8));

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // Classic Camera: respawn after field transition
            if (Camera.ClassicMode)
            {
                // Respawn once this object's field is no longer the one being shown.
                if (MapManager.ObjLink.FieldTransition(_fieldRect))
                    SpawnTile();
            }
            // Modern Camera: respawn when a few fields away.
            else
            {
                // Track when the field is outside of the update range.
                var updateState = Map.GetUpdateState(EntityPosition.Position);

                // Respawn the tile now.
                if (_lastFieldTime < updateState)
                    SpawnTile();
            }
        }

        private void SpawnTile()
        {
            List<GameObject> enemyTriggers = new List<GameObject>();
            EnemyFlyingTile flyingTile = new EnemyFlyingTile(Map, (int)EntityPosition.X, (int)EntityPosition.Y, _strKey, _index, _mode);

            // Remove respawner
            Map.Objects.DeleteObjects.Add(this);

            // Spawn new flying tile
            Map.Objects.SpawnObject(flyingTile);

            // If there is utility objects in the room find them.
            Map.Objects.GetGameObjectsWithTag(enemyTriggers, Values.GameObjectTag.Utility,
                _fieldRect.X, _fieldRect.Y, _fieldRect.Width, _fieldRect.Height);

            // Loop through the list of utility objects.
            foreach (var trigger in enemyTriggers)
            {
                // If it's an enemy trigger add the flying tile back to it.
                if (trigger is ObjEnemyTrigger etrig)
                {
                    etrig.EnemyTriggerList.Add(flyingTile);
                }
            }
        }
    }
}
