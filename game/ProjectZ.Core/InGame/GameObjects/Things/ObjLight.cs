using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjLight : GameObject
    {
        private readonly Rectangle _drawRectangle;
        private Color _lightColor;
        private Color _baseColor;

        // Values configurable via lahdmod.
        private bool light_source = true;

        public ObjLight() : base("editor light") { }

        public ObjLight(Map.Map map, int posX, int posY, int size, int colorR, int colorG, int colorB, int colorA, int layer) : base(map)
        {
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjLight.lahdmod");
            ModFile.Parse(modFile, this);

            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            EntitySize = new Rectangle(-size / 2, -size / 2, size, size);

            _drawRectangle = new Rectangle(posX + 8 - size / 2, posY + 8 - size / 2, size, size);
            _lightColor = new Color(colorR, colorG, colorB) * (colorA / 255f);
            _baseColor = new Color(colorR, colorG, colorB);

            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight) { Layer = layer });
        }

        public void SetBrightness(float bright)
        {
            _lightColor = _baseColor * bright;
        }

        public void DrawLight(SpriteBatch spriteBatch)
        {
            if (light_source && GameSettings.ObjectLights)
                spriteBatch.Draw(Resources.SprLight, _drawRectangle, _lightColor);
        }
    }
}
