using System.IO;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Effects
{
    internal class ObjFireballDeath : ObjAnimator
    {
        private bool _blink;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 240;
        private int   light_blu    = 235;
        private float light_bright = 0.75f;
        private int   light_size   = 32;
        private float light_fade   = 0.33f;

        public ObjFireballDeath(Map.Map map, int posX, int posY, int offsetX, int offsetY, bool blink)
            : base(map, posX, posY, offsetX, offsetY, Values.LayerTop, "Particles/fireballDeath", "run", deleteOnFinish: true)
        {
            // If this effect is supposed to blink.
            _blink = blink;

            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjFireballDeath.lahdmod");
            ModFile.Parse(modFile, this);

            ConfigureLight(light_source, light_red, light_grn, light_blu, light_bright, light_size, light_fade);
            _blink = blink;
        }

        protected override void Update()
        {
            base.Update();

            if (_blink && !GameSettings.EpilepsySafe)
                this.Sprite.SpriteShader = (Game1.TotalGameTime % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime) ? Resources.DamageSpriteShader0 : null;
        }
    }
}
