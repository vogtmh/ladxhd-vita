using System.Collections.Generic;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.NPCs;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjObjectSpawner : GameObject
    {
        private readonly GameObject _spawnObject;

        private readonly string _strKey;
        private readonly string _strValue;
        private readonly string _strSpawnObjectId;
        private readonly object[] _objParameter;

        private readonly bool _canDespawn;
        private bool _isSpawned;

        private bool _fixDuplicate;

        public ObjObjectSpawner() : base("editor object spawner") { }

        public ObjObjectSpawner(Map.Map map, int posX, int posY, string strKey, string strValue, string strSpawnObjectId, string strSpawnParameter, bool canDespawn = true) : base(map)
        {
            _strKey = strKey;
            _strValue = string.IsNullOrEmpty(strValue) ? "0" : strValue;

            _strSpawnObjectId = strSpawnObjectId;
            string[] parameter = null;
            if (strSpawnParameter != null)
            {
                parameter = strSpawnParameter.Split('.');
                // @HACK: some objects have strings with dots in them...
                for (var i = 0; i < parameter.Length; i++)
                    parameter[i] = parameter[i].Replace("$", ".");
            }
            _canDespawn = canDespawn;

            _objParameter = MapData.GetParameter(strSpawnObjectId, parameter);
            if (_objParameter != null)
            {
                _objParameter[1] = posX;
                _objParameter[2] = posY;
            }

            if (_strSpawnObjectId != null)
                _spawnObject = ObjectManager.GetGameObject(map, _strSpawnObjectId, _objParameter);

            if (_spawnObject == null)
            {
                IsDead = true;
                return;
            }
            // If it's a bush, don't create a bush respawner.
            if (_spawnObject is ObjBush bush)
                bush.NoRespawn = true;

            // If it's a stone, don't create a stone respawner.
            if (_spawnObject is ObjStone stone)
                stone.NoRespawn = true;

            // If it's a move stone, don't create a move stone respawner.
            if (_spawnObject is ObjMoveStone moveStone)
                moveStone.NoRespawn = true;

            // Add key change listener.
            if (!string.IsNullOrEmpty(_strKey))
            {
                _spawnObject.IsActive = false;
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));
            }
            // Spawn object deactivated.
            Map.Objects.SpawnObject(_spawnObject);

            // Used to find duplicate spawns.
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        private void Update()
        {
            // There is a stupid bug where Tarin spawn is duplicated where he is standing next to the tree. While both duplicate spawns do not
            // appear on the map, one of the duplicates remains active (IsActive = true) and it's possible to interact with it. Because it does
            // not have a message readily available, the message box just shows "Error" in the dialog box. This hack removes the duplicate.
            if (!_fixDuplicate && _strKey == "tarin_state" && _strValue == "4")
            {
                // Get the current state of the Tarin key-value pair.
                var value = Game1.GameManager.SaveManager.GetString(_strKey, "0");

                // If the value is not a match then Tarin will have a duplicate.
                if (value != _strValue)
                {
                    // Now that we have the Tarin spawn we can use it to find the duplicate spawn.
                    var objects = new List<GameObject>();
                    Map.Objects.GetComponentList(objects, (int)_spawnObject.EntityPosition.X - 8, (int)_spawnObject.EntityPosition.Y - 8, 16, 16, BodyComponent.Mask);

                    // Loop through the objects to find the duplicate.
                    foreach (var obj in objects)
                    {
                        // The Tarin object is using "ObjPersonNew" as the base.
                        if (obj is ObjPersonNew person && !ReferenceEquals(person, _spawnObject))
                        {
                            // Remove the duplicate.
                            Map.Objects.DeleteObjects.Add(person);
                            _fixDuplicate = true;
                        }
                    }
                }
            }
            // If there is no duplicates to fix just stop the update function.
            else if (!_fixDuplicate)
                _fixDuplicate = true;

            // Remove the component after the fix.
            if (_fixDuplicate)
                RemoveComponent(UpdateComponent.Index);
        }

        private void KeyChanged()
        {
            var value = Game1.GameManager.SaveManager.GetString(_strKey, "0");

            if (!_isSpawned && value == _strValue)
            {
                // Activate the object.
                _spawnObject.IsActive = true;
                _isSpawned = true;

                // Remove the spawner if it does not despawn the object.
                if (!_canDespawn)
                    Map.Objects.DeleteObjects.Add(this);
            }
            else if (_isSpawned && value != _strValue)
            {
                // Despawn the object.
                if (_canDespawn)
                    _spawnObject.IsActive = false;
                else
                    Map.Objects.DeleteObjects.Add(_spawnObject);

                _isSpawned = false;
            }
        }
    }
}
