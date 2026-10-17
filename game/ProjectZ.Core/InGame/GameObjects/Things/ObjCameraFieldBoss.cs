using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    public class ObjCameraFieldBoss : GameObject
    {
        private Rectangle _fieldRectangle;
        private string _bossKey;
        private bool _initialized;
        private bool _firstLoop;
        private bool _playerInField => _fieldRectangle.Contains(MapManager.ObjLink.CenterPosition);

        public ObjCameraFieldBoss() : base("editor field") 
        { 
            EditorColor = Color.Red * 1.00f;
        }

        public ObjCameraFieldBoss(Map.Map map, int posX, int posY, string strKey) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _bossKey = strKey;
            _fieldRectangle = map.GetField(posX, posY);

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // Check if this is the very first loop iteration.
            if (!_firstLoop)
            {
                // Some bosses rely on values that are not set until a "ObjKeySetter" is loaded, and there
                // is no guarantee it will be loaded before this object. So we wait until starting the loop
                // and check on the very first iteration of whether or not to delete the object immediately.
                if (!string.IsNullOrEmpty(_bossKey) && Game1.GameManager.SaveManager.GetString(_bossKey) == "1")
                {
                    // If the boss key is null or the boss is dead, destroy this object.
                    Map.Objects.DeleteObjects.Add(this);
                    return;
                }
                // Never run this check again until the object is reloaded.
                _firstLoop = true;
            }

            // If the option isn't enabled then skip trying to set the camera.
            if (Camera.ClassicMode || GameSettings.CameraMode != 2 || !GameSettings.ClassicBosses)
                return;

            // If the player is in the field when a map transition starts.
            if (_initialized && MapManager.ObjLink.TransitioningOut)
            {
                // Force a reset of the camera during the transition.
                MapManager.Camera.QueueClassicReset(this);
                return;
            }

            // Load the current state of the boss key.
            var bossKeyValue = Game1.GameManager.SaveManager.GetString(_bossKey, "0");

            // If the boss is dead.
            if (bossKeyValue == "1")
            {
                // Queue a camera reset when either the map or field changes.
                MapManager.Camera.QueueClassicReset(this);
                return;
            }

            // If the boss is alive and the player is in the field.
            if (bossKeyValue == "0" && !_initialized && _playerInField)
            {
                // Force Classic Camera.
                _initialized = true;
                MapManager.Camera.ForceClassicCamera(true);
            }
            // The player has left the field.
            else if (_initialized && !_playerInField)
            {
                // Disable Classic Camera.
                _initialized = false;
                MapManager.Camera.ForceClassicCamera(false);
            }
        }
    }
}
