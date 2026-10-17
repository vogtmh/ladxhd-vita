using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjEnemyTrigger : GameObject
    {
        public List<GameObject> EnemyTriggerList = new List<GameObject>();
        private readonly Rectangle _triggerField;
        private readonly string _triggerKey;

        private int _posX;
        private int _posY;
        private bool _enemiesAlive;
        private bool _findEnemies;
        private bool _respawn;

        private float _recheckTimer;

        public ObjEnemyTrigger() : base("editor enemy trigger") { }

        public ObjEnemyTrigger(Map.Map map, int posX, int posY, string triggerKey, bool respawn) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);

            Tags = Values.GameObjectTag.Utility;

            if (string.IsNullOrEmpty(triggerKey))
            {
                IsDead = true;
                return;
            }
            _posX = posX;
            _posY = posY;
            _triggerKey = triggerKey;
            _triggerField = map.GetField(posX, posY);
            _respawn = respawn;
            _findEnemies = true;

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        private void Update()
        {
            // If respawn is set we need a way to check for new enemies.
            if (_respawn)
            {
                // A timer is used to recheck for enemies so it's not constantly rechecking.
                _recheckTimer += Game1.DeltaTime;
                if (_recheckTimer > 200)
                {
                    _findEnemies = true;
                    _recheckTimer = 0;
                }
            }
            // Find any enemies in the current field.
            if (_findEnemies)
            {
                Map.Objects.GetGameObjectsWithTag(EnemyTriggerList, Values.GameObjectTag.Enemy,
                    _triggerField.X, _triggerField.Y, _triggerField.Width, _triggerField.Height);
                _findEnemies = false;
            }
            // Assume all enemies are defeated.
            _enemiesAlive = false;

            // Check if no more enemies remain in the field.
            foreach (var gameObject in EnemyTriggerList)
                if (gameObject.Map != null)
                    _enemiesAlive = true;

            // If there are enemies that are alive.
            if (_enemiesAlive)
            {
                // Clear the key as soon as enemies are known to be alive.
                if (_respawn && _triggerField.Contains(MapManager.ObjLink.CenterPosition))
                    Game1.GameManager.SaveManager.SetString(_triggerKey, "0");
                return;
            }
            // All enemies were defeated so set the key.
            Game1.GameManager.SaveManager.SetString(_triggerKey, "1");

            // Remove the enemy trigger from the map.
            Map.Objects.DeleteObjects.Add(this);

            // If it's a persistent enemy trigger respawn a new one.
            if (_respawn)
                Map.Objects.SpawnObject(new ObjEnemyTrigger(Map, _posX, _posY, _triggerKey, _respawn));
        }
    }
}
