using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    public class ObjCameraFieldMStalfos : GameObject
    {
        private Rectangle _fieldRectangle;
        private string _bossKey;
        private int _encounterNumber;
        private bool _initialized;
        private bool _playerInField => _fieldRectangle.Contains(MapManager.ObjLink.CenterPosition);

        public ObjCameraFieldMStalfos() : base("editor field") 
        { 
            EditorColor = Color.Blue * 1.00f;
        }

        public ObjCameraFieldMStalfos(Map.Map map, int posX, int posY, string strKey, int encounterNumber) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _bossKey = strKey;
            _fieldRectangle = map.GetField(posX, posY);
            _encounterNumber = encounterNumber;

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // If the option isn't enabled then skip trying to set the camera.
            if (GameSettings.CameraMode != 2 || !GameSettings.ClassicBosses)
                return;

            // Load the current state of the boss key.
            var bossKeyValue = Game1.GameManager.SaveManager.GetString(_bossKey, "0");
            int.TryParse(bossKeyValue, out var bossKeyInteger);

            // If the encounter doesn't or no longer matches.
            if (bossKeyInteger > _encounterNumber)
            {
                // Queue a camera reset when either the map or field changes.
                MapManager.Camera.QueueClassicReset(this);
                return;
            }

            // If the boss is alive and the player is in the field.
            if (bossKeyInteger == _encounterNumber && !_initialized && _playerInField)
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
