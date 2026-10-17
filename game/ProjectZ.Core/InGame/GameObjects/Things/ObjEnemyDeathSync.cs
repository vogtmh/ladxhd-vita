using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjEnemyDeathSync : GameObject
    {
        public List<GameObject> EnemyList = new List<GameObject>();
        private readonly Rectangle _triggerField;
        private readonly string _triggerKey;

        private int _enemyCount;
        private bool _respawn;
        private bool _findEnemies;
        private bool _firstDeath;
        private float _syncWindow;
        private float _deathTimer;
        private float _recheckTimer;

        public ObjEnemyDeathSync() : base("editor enemy death sync") { }

        public ObjEnemyDeathSync(Map.Map map, int posX, int posY, string triggerKey, bool respawn, float syncWindow) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);

            Tags = Values.GameObjectTag.Utility;

            if (string.IsNullOrEmpty(triggerKey))
            {
                IsDead = true;
                return;
            }
            _triggerKey = triggerKey;
            _triggerField = map.GetField(posX, posY);
            _respawn = respawn;
            _syncWindow = syncWindow;
            _findEnemies = true;

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        private void Update()
        {
            if (_findEnemies)
            {
                EnemyList.Clear();
                Map.Objects.GetGameObjectsWithTag(EnemyList, Values.GameObjectTag.Enemy,
                    _triggerField.X, _triggerField.Y, _triggerField.Width, _triggerField.Height);

                _findEnemies = false;
                _firstDeath = false;
                _deathTimer = 0;
                _enemyCount = EnemyList.Count;

                if (_respawn && _enemyCount > 0 &&
                    _triggerField.Contains(MapManager.ObjLink.CenterPosition))
                    Game1.GameManager.SaveManager.SetString(_triggerKey, "0");
            }

            if (MapManager.ObjLink.FieldChange)
                _findEnemies = true;

            if (_enemyCount == 0)
            {
                if (!_respawn)
                {
                    Map.Objects.DeleteObjects.Add(this);
                    return;
                }

                _recheckTimer += Game1.DeltaTime;
                if (_recheckTimer > 200)
                {
                    _recheckTimer = 0;
                    _findEnemies = true;
                }
                return;
            }

            var aliveCount = 0;
            foreach (var enemy in EnemyList)
                if (enemy.Map != null)
                    aliveCount++;

            if (!_firstDeath && aliveCount < _enemyCount)
            {
                _firstDeath = true;
                _deathTimer = 0;
            }

            else if (_firstDeath)
                _deathTimer += Game1.DeltaTime;

            if (aliveCount > 0)
                return;

            if (_deathTimer <= _syncWindow)
            {
                Game1.GameManager.SaveManager.SetString(_triggerKey, "1");
                Game1.GameManager.StartDialogPath(_triggerKey);
            }
            _enemyCount = 0;
            EnemyList.Clear();

            if (!_respawn)
                Map.Objects.DeleteObjects.Add(this);
            else
                _recheckTimer = 0;
        }
    }
}
