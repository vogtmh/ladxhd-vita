using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjObjectRespawner : GameObject
    {
        private GameObject _spawnedObject;

        private Box _spawnBox;

        private readonly string _strDisableKey;
        private readonly string _strSpawnObjectId;
        private readonly object[] _objParameter;

        private const int SpawnTime = 350;
        private float _spawnCounter;
        private bool _isActive = true;
        private bool _respawnStart;
        private float _respawnTimer;
        private Rectangle _objectField;

        public ObjObjectRespawner() : base("editor object respawner")
        {
            EditorColor = Color.Red * 0.65f;
        }

        public ObjObjectRespawner(Map.Map map, int posX, int posY, string strDisableKey, string strSpawnObjectId, string strSpawnParameter) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _spawnBox = new Box(posX, posY, 0, 16, 16, 8);
            _objectField = Map.GetField(new Vector2(posX + 8, posY + 8));
            _strDisableKey = strDisableKey;
            _strSpawnObjectId = strSpawnObjectId;

            // Parse the object to spawn's create parameters.
            string[] parameter = null;
            if (strSpawnParameter != null)
            {
                parameter = strSpawnParameter.Split('.');
                for (var i = 0; i < parameter.Length; i++)
                    parameter[i] = parameter[i].Replace("$", ".");
            }
            // Store the object's spawning parameters from the respawner parameters.
            _objParameter = MapData.GetParameter(strSpawnObjectId, parameter);
            if (_objParameter != null)
            {
                _objParameter[1] = posX;
                _objParameter[2] = posY;
            }
            // If the object type to respawn is null destroy the respawner.
            if (_strSpawnObjectId == null)
            {
                IsDead = true;
                return;
            }
            // Add key change listener to detect when to stop respawning.
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            if (!string.IsNullOrEmpty(_strDisableKey))
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));

            // Get the key state and try to spawn the object on map init.
            OnKeyChange();
            SpawnObject();

            // Register as an always animate object so the classic camera respawn can happen.
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void OnKeyChange()
        {
            // If there is no matching key then simply return.
            if (string.IsNullOrEmpty(_strDisableKey))
                return;

            // Get the key and set the object's active state based on whether or not the key's value was satisfied.
            var state = Game1.GameManager.SaveManager.GetString(_strDisableKey, "0");

            // When the key's value is "1" the object no longer respawns.
            _isActive = state != "1";
        }

        private void Update()
        {
            // Modern Camera respawns the object immediately.
            if (!Camera.ClassicMode)
            {
                // When the spawner is inactive or the object exists, reset the respawn delay.
                if (!_isActive || (_spawnedObject != null && _spawnedObject.Map != null))
                {
                    _spawnCounter = SpawnTime;
                    return;
                }
                // Don't respawn the object until the respawn delay expires.
                _spawnCounter -= Game1.DeltaTime;
                if (_spawnCounter > 0)
                    return;

                // Do not spawn the object if it would collide with something there.
                var outBox = Box.Empty;
                if (Map.Objects.Collision(_spawnBox, Box.Empty, Values.CollisionTypes.Normal | Values.CollisionTypes.Player, 0, 0, ref outBox))
                {
                    _spawnCounter = SpawnTime * 0.25f;
                    return;
                }
                // Spawn the object, play a sound effect, and spawn the explosion effect.
                SpawnObject();
                Game1.AudioManager.PlaySoundEffect("D360-15-0F");
                Map.Objects.SpawnObject(new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y, Values.LayerTop, "Particles/spawn", "run", true));
            }
            // Classic Camera respawns the object on a field change and does not show a special effect.
            else
            {
                // Do not spawn the object if it would collide with something there.
                var outBox = Box.Empty;
                if (Map.Objects.Collision(_spawnBox, Box.Empty, Values.CollisionTypes.Normal | Values.CollisionTypes.Player, 0, 0, ref outBox))
                    return;

                // Respawn once a scroll settles on a field other than this one.
                if (MapManager.ObjLink.FieldTransition(_objectField))
                    _respawnStart = true;

                if (_respawnStart)
                {
                    _respawnTimer += Game1.DeltaTime;
                    if (_respawnTimer >= 250)
                    {
                        SpawnObject();
                        _respawnStart = false;
                        _respawnTimer = 0;
                    }
                }
            }
        }

        private void SpawnObject()
        {
            // Create the spawned object.
            _spawnedObject = ObjectManager.GetGameObject(Map, _strSpawnObjectId, _objParameter);

            // If the spawned type is "ObjStone" then track that it was created from a spawner on the object.
            if (_spawnedObject is ObjStone spawnStone)
                spawnStone.FromObjSpawner = true;

            // Spawn the object into the map.
            Map.Objects.SpawnObject(_spawnedObject);
        }
    }
}
