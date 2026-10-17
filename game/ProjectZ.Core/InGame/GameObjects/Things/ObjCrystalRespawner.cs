using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class CrystalRespawner : GameObject
    {
        private readonly string _spriteId;
        private readonly string _dialogPath;
        private readonly bool _isHardCrystal;
        private readonly int _color;

        private int _lastFieldTime;
        private Rectangle _objectField;

        public CrystalRespawner(Map.Map map, int posX, int posY, string spriteId, string dialogPath, bool isHardCrystal, int color) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _spriteId = spriteId;
            _dialogPath = dialogPath;
            _isHardCrystal = isHardCrystal;
            _color = color;

            _lastFieldTime = Map.GetUpdateState(EntityPosition.Position);
            _objectField = Map.GetField(new Vector2(posX + 8, posY + 8));

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // If classic camera mode is active, respawn when leaving the current field.
            if (Camera.ClassicMode)
            {
                // Respawn once a scroll settles on a field other than this one.
                if (MapManager.ObjLink.FieldTransition(_objectField))
                    SpawnCrystal();
            }
            else
            {
                var updateState = Map.GetUpdateState(EntityPosition.Position);
                if (_lastFieldTime < updateState)
                    SpawnCrystal();
            }
        }

        private void SpawnCrystal()
        {
            // Delete the respawn object and respawn the crystal object.
            Map.Objects.DeleteObjects.Add(this);
            Map.Objects.SpawnObject(new ObjCrystal(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 
                _spriteId, _color, _isHardCrystal, _dialogPath));
        }
    }
}
