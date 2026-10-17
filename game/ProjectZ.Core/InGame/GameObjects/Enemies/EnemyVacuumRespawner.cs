using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyVacuumRespawner : GameObject
    {
        private readonly string _roomName;
        private readonly string _entryId;
        private readonly bool _isPusher;
        private RectangleF _field;
        private Rectangle _objectField;

        public EnemyVacuumRespawner(Map.Map map, int posX, int posY, string roomName, string entryId, bool isPusher, RectangleF field) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _roomName = roomName;
            _entryId = entryId;
            _isPusher = isPusher;
            _field = field;
            _objectField = Map.GetField(new Vector2(posX + 8, posY + 8));

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // Classic Camera: respawn after field transition
            if (Camera.ClassicMode)
            {
                // Respawn once this object's field is no longer the one being shown.
                if (MapManager.ObjLink.FieldTransition(_objectField))
                    SpawnVacuum();
            }
            // Modern camera: Respawn when player leaves the field.
            else
            {
                if (!_field.Contains(MapManager.ObjLink.CenterPosition))
                    SpawnVacuum();
            }
        }

        private void SpawnVacuum()
        {
            Map.Objects.DeleteObjects.Add(this);

            if (!Camera.ClassicMode)
            {
                var anim = new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y, Values.LayerTop, "Particles/spawn", "run", true);
                Map.Objects.SpawnObject(anim);
                Game1.AudioManager.PlaySoundEffect("D360-47-2F");
            }
            Map.Objects.SpawnObject(new EnemyVacuum(Map, (int)EntityPosition.X, (int)EntityPosition.Y, _roomName, _entryId, _isPusher));
        }
    }
}
