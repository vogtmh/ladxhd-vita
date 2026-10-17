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
    internal class EnemyBombiteRespawner : GameObject
    {
        private readonly bool _green;
        private RectangleF _field;
        private Rectangle _objectField;

        public EnemyBombiteRespawner(Map.Map map, int posX, int posY, RectangleF field, bool green) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _green = green;
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
                    SpawnBombite();
            }
            // Modern camera: Respawn when player leaves the field.
            else
            {
                if (!_field.Contains(MapManager.ObjLink.CenterPosition))
                    SpawnBombite();
            }
        }

        private void SpawnBombite()
        {
            Map.Objects.DeleteObjects.Add(this);

            if (!Camera.ClassicMode)
            {
                var anim = new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y, Values.LayerTop, "Particles/spawn", "run", true);
                Map.Objects.SpawnObject(anim);
                Game1.AudioManager.PlaySoundEffect("D360-47-2F");
            }
            if (_green)
                Map.Objects.SpawnObject(new EnemyBombiteGreen(Map, (int)EntityPosition.X, (int)EntityPosition.Y));
            else
                Map.Objects.SpawnObject(new EnemyBombite(Map, (int)EntityPosition.X, (int)EntityPosition.Y));
        }
    }
}
