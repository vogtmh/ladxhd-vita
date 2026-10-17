using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.InGame.Graphics
{
    public class DictAtlasEntry
    {
        public readonly Texture2D Texture;

        // Logical game pixel values. These are what the draw code should use.
        public readonly Rectangle SourceRectangle;
        public readonly Vector2 Origin;

        // The real scale of the texture. Informational only since the texture itself is registered
        // at its logical size (see HdTexture), so draw code is never multiplies by this.
        public readonly int TexelScale;

        // Legacy fields from the old "apply the scale at every draw call" approach (editor icons,
        // ItemDrawHelper). We keep these so that every single draw call does not need to be updated.
        public readonly Rectangle ScaledRectangle;
        public readonly Vector2 ScaledOrigin;
        public readonly int TextureScale = 1;
        public readonly float Scale = 1.0f;

        public DictAtlasEntry(Texture2D texture, Rectangle sourceRectangle, Vector2 origin, int texelScale)
        {
            Texture = texture;

            SourceRectangle = sourceRectangle;
            Origin = origin;
            TexelScale = texelScale < 1 ? 1 : texelScale;

            ScaledRectangle = sourceRectangle;
            ScaledOrigin = origin;
        }
    }
}
