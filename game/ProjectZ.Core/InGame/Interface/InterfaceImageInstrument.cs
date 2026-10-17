using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;

class InterfaceImageInstrument : InterfaceElement
{
    private readonly DictAtlasEntry _sprite;

    public InterfaceImageInstrument(DictAtlasEntry sprite)
    {
        _sprite = sprite;
        Size = new Point(sprite.ScaledRectangle.Width, sprite.ScaledRectangle.Height);
    }

    public override void Draw(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency)
    {
        // The size of the instruments can be changed here.
        scale = scale * 0.90f;

        // Use an alternate version of ItemDrawHelper to draw the instruments.
        ItemDrawHelper.DrawInstrument(spriteBatch, _sprite, drawPosition, scale);
    }
}
