using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Interface;

class InterfaceImagePlayer : InterfaceImage
{
    private readonly Animator _player;
    private readonly Animator _sword;

    public bool ShowSword;
    public bool ShowShield;

    public InterfaceImagePlayer(Animator player, Animator sword, Texture2D texture, Rectangle source, Point size, Point margin) : base(texture, source, size, margin)
    {
        _player = player;
        _sword = sword;
    }

    public override void Draw(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency)
    {
        var color = ImageColor * transparency;
        var basePos = drawPosition + Offset * scale;

        spriteBatch.Draw(_player.SprTexture, basePos, _player.CurrentFrame.SourceRectangle, color, 0f, Vector2.Zero, scale, Effects, 0f);

        if (!ShowSword)
            return;

        var gripX = ShowShield ? 8 : 7;
        var swordOffset = basePos + new Vector2(_sword.CurrentFrame.Offset.X + gripX, _sword.CurrentFrame.Offset.Y + 13) * scale;
        spriteBatch.Draw(_sword.SprTexture, swordOffset, _sword.CurrentFrame.SourceRectangle, color, 0f, Vector2.Zero, scale, Effects, 0f);
    }
}
