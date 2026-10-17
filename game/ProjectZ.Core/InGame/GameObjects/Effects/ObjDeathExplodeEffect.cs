using System.IO;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Effects
{
    internal class ObjDeathExplodeEffect : ObjAnimator
    {
        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 240;
        private int   light_blu    = 235;
        private float light_bright = 0.75f;
        private int   light_size   = 42;
        private float light_fade   = 0.33f;

        public ObjDeathExplodeEffect(Map.Map map, int posX, int posY, int offsetX, int offsetY, bool pieceofpower = false)
            : base(map, posX, posY, offsetX, offsetY, Values.LayerTop, pieceofpower ? "Particles/pieceOfPowerExplosion" : "Particles/explosion0", "run", deleteOnFinish: true)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjDeathExplodeEffect.lahdmod");
            ModFile.Parse(modFile, this);

            ConfigureLight(light_source, light_red, light_grn, light_blu, light_bright, light_size, light_fade);
        }
    }
}
