using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjEnemyRespawner : GameObject
    {
        private GameObject _gameObject;

        private readonly string _strSpawnObjectId;
        private readonly object[] _objParameter;

        private int _lastFieldTime;

        public ObjEnemyRespawner() : base("editor object respawner") { }

        public ObjEnemyRespawner(Map.Map map, int posX, int posY, string strSpawnObjectId, string strSpawnParameter) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            string[] parameter = null;
            if (strSpawnParameter != null)
            {
                parameter = strSpawnParameter.Split('.');

                // HACK: Some objects have strings with periods in them, so use
                // the "$" character which will be replaced with a period here.
                for (var i = 0; i < parameter.Length; i++)
                    parameter[i] = parameter[i].Replace("$", ".");
            }
            _strSpawnObjectId = strSpawnObjectId;
            _objParameter = MapData.GetParameter(strSpawnObjectId, parameter);

            // The object's position parameters must be manually assigned.
            if (_objParameter != null)
            {
                _objParameter[1] = posX;
                _objParameter[2] = posY;
            }
            _lastFieldTime = Map.GetUpdateState(EntityPosition.Position);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            SpawnObject();
        }

        private void Update()
        {
            // Track when the field is outside of the update range.
            var updateState = Map.GetUpdateState(EntityPosition.Position);

            // Do not respawn the enemy unless it no longer exists.
            if (_gameObject == null || _gameObject.Map != null || _lastFieldTime >= updateState)
            {
                _lastFieldTime = updateState;
                return;
            }
            // Respawn the enemy.
            SpawnObject();
        }

        private void SpawnObject()
        {
            // Get the game object by it's ID and pass along it's spawn parameters.
            _gameObject = ObjectManager.GetGameObject(Map, _strSpawnObjectId, _objParameter);

            // If it successfully returned an object spawn it into the map.
            if (_gameObject != null)
                Map.Objects.SpawnObject(_gameObject);
        }
    }
}
