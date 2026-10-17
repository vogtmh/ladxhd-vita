using System.IO;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Effects
{
    internal class ObjBurningEffect : ObjAnimator
    {
        private readonly bool _rod;
        private int _posX;
        private int _posY;
        private Box _burnBox;
        private float _burnIteration;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 230;
        private int   light_blu    = 230;
        private float light_bright = 0.70f;
        private int   light_size   = 120;
        private float light_fade   = 0.35f;

        public ObjBurningEffect(Map.Map map, int posX, int posY, int offsetX, int offsetY, bool rod)
            : base(map, posX, posY, offsetX, offsetY, Values.LayerTop, "Particles/flame", rod ? "rod" : "idle", deleteOnFinish: true)
        {
            // Track that this came from the Magic Rod.
            _rod = rod;

            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjBurningEffect.lahdmod");
            ModFile.Parse(modFile, this);

            ConfigureLight(light_source, light_red, light_grn, light_blu, light_bright, light_size, light_fade);

            // If it's a passive Magic Rod shot.
            if (_rod)
            {
                // Store the positions to be used for the burn box and set the burn box.
                _posX = posX;
                _posY = posY;
                _burnBox = new Box(posX - 6, posY - 6, 0, 12, 12, 8);

                // Hit the moment it spawns, the update function will then burn over time.
                Map.Objects.Hit(this, new Vector2(_posX, _posY), _burnBox, HitType.MagicRod, 1, false, false);
            }
        }

        protected override void Update()
        {
            base.Update();

            // Only burn while the flame is actually still showing.
            if (!_rod || !Sprite.IsVisible)
                return;

            // Add the delta time to the burn rate.
            _burnIteration += Game1.DeltaTime;

            // Burn enemies at a rate of 10 times a second.
            if (_burnIteration >= 100)
            {
                _burnIteration = 0;
                Map.Objects.Hit(this, new Vector2(_posX, _posY), _burnBox, HitType.MagicRod, 1, false, false);
            }
        }
    }
}
