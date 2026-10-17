using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    class BossFinalBossDethITail : GameObject
    {
        private readonly BossFinalBoss _owner;
        private readonly CSprite _sprite;
        private readonly DrawComponent _drawComponent;
        private readonly DamageFieldComponent _damageFieldComponent;

        public string _spriteId;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 100;
        private int   light_blu    = 100;
        private float light_bright = 0.95f;
        private int   light_size   = 28;

        public BossFinalBossDethITail(Map.Map map, BossFinalBoss owner, string spriteId, Vector2 position) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "BossFinalBossDethITail.lahdmod");
            ModFile.Parse(modFile, this);

            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);
            _spriteId = spriteId;
            _owner = owner;

            _sprite = new CSprite(spriteId, EntityPosition);

            var damageCollider = new CBox(EntityPosition, -3, -3, 6, 6, 3);
            AddComponent(DamageFieldComponent.Index, _damageFieldComponent = new DamageFieldComponent(damageCollider, HitType.Enemy, 4) { OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(DrawComponent.Index, _drawComponent = new DrawComponent(Draw, Values.LayerBottom, EntityPosition));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
            SetActive(false);
        }

        private void OnDamagedPlayer()
        {
            // If it deals damage transfer to the main boss.
            _owner.DealtPlayerDamage = true;
        }

        public void DeactivateDamageField()
        {
            _damageFieldComponent.IsActive = false;
        }

        public void SetActive(bool state)
        {
            _damageFieldComponent.IsActive = state;
            _drawComponent.IsActive = state;
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            if (!_drawComponent.IsActive)
                return;

            _sprite.SpriteShader = _owner.Sprite.SpriteShader;
            _sprite.Draw(spriteBatch);
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (_drawComponent.IsActive && _spriteId == "final_part2" && light_source && GameSettings.ObjectLights)
            {
                var _lightColor = new Color(light_red, light_grn, light_blu);

                var _lightRectangle = new Rectangle(
                    (int)EntityPosition.X - light_size / 2, 
                    (int)EntityPosition.Y - light_size / 2, 
                    light_size, light_size);

                spriteBatch.Draw(Resources.SprLight, _lightRectangle, _lightColor * light_bright);
            }
        }
    }
}
