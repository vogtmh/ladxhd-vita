using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    class BossFinalBossMoldormTail : GameObject
    {
        public readonly CSprite Sprite;
        private string _animationId;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 255;
        private int   light_blu    = 150;
        private float light_bright = 0.85f;
        private int   light_size   = 35;

        public BossFinalBossMoldormTail(Map.Map map, BossFinalBoss nightmare, string animationId, bool hittable) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "BossFinalBossMoldormTail.lahdmod");
            ModFile.Parse(modFile, this);

            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(nightmare.EntityPosition.X, nightmare.EntityPosition.Y, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);
            _animationId = animationId;

            var animator = AnimatorSaveLoad.LoadAnimator("Nightmares/nightmare");
            animator.Play(animationId);

            Sprite = new CSprite(EntityPosition);

            if (hittable)
            {
                var hittableBox = new CBox(EntityPosition, -6, -6, 12, 12, 8);
                AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, nightmare.HitTail));
            }
            AddComponent(BaseAnimationComponent.Index, new AnimationComponent(animator, Sprite, Vector2.Zero));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (_animationId == "moldorm_tail" && light_source && GameSettings.ObjectLights)
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
