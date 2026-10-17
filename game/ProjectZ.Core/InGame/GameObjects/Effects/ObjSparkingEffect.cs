using System.IO;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Effects
{
    internal class ObjSparkingEffect : ObjAnimator
    {
        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 255;
        private int   light_blu    = 200;
        private float light_bright = 0.80f;
        private int   light_size   = 26;
        private float light_fade   = 0.15f;

        public ObjSparkingEffect(Map.Map map, int posX, int posY, int offsetX, int offsetY)
            : base(map, posX, posY, offsetX, offsetY, Values.LayerTop, "Particles/swordPoke", "run", deleteOnFinish: true)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjSparkingEffect.lahdmod");
            ModFile.Parse(modFile, this);

            ConfigureLight(light_source, light_red, light_grn, light_blu, light_bright, light_size, light_fade);
        }
    }
}
