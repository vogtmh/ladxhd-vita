using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjDungeonSwitch : GameObject
    {
        private readonly CSprite _sprite;
        private readonly string _key;

        private const float ColorChangeTime = (8 / 60f) * 1000;
        private float _colorCounter;
        private float _hitCooldown;

        public ObjDungeonSwitch() : base("dungeon_switch") { }

        public ObjDungeonSwitch(Map.Map map, int posX, int posY, string key) : base(map)
        {
            EntityPosition = new CPosition(posX, posY + 16, 0);
            EntitySize = new Rectangle(0, -16, 16, 16);

            _key = key;

            var hittableBox  = new CBox(posX, posY, 0, 16, 16, 16);
            var collisionBox = new CBox(posX + 1, posY + 4, 0, 14, 12, 16);

            _sprite = new CSprite("dungeon_switch", EntityPosition, new Vector2(0, -16));

            if (!string.IsNullOrEmpty(_key))
                AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit));
            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(collisionBox, Values.CollisionTypes.Normal | Values.CollisionTypes.Switch));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
        }

        private void Update()
        {
            if (_hitCooldown > 0)
                _hitCooldown -= Game1.DeltaTime;

            // switch to an orange color after a hit
            if (_colorCounter > 0)
            {
                _colorCounter -= Game1.DeltaTime;
                _sprite.SpriteShader = Resources.DamageSpriteShader0;
            }
            else
                _sprite.SpriteShader = null;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_hitCooldown > 0)
                return Values.HitCollision.Blocking;

            _hitCooldown = 250;
            _colorCounter = ColorChangeTime;
            
            Game1.AudioManager.PlaySoundEffect("D360-03-03");
            Game1.AudioManager.PlaySoundEffect("D370-14-0E");

            // toggle the key
            var lastState = Game1.GameManager.SaveManager.GetString(_key, "0");
            Game1.GameManager.SaveManager.SetString(_key, (lastState == "0" ? "1" : "0"));

            return Values.HitCollision.Blocking;
        }
    }
}
