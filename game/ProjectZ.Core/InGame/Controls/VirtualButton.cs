using Microsoft.Xna.Framework;
using ProjectZ.InGame.Things;
using ProjectZ.InGame.Graphics;

namespace ProjectZ.InGame.Controls
{
    public class VirtualButton
    {
        public CButtons Button;
        public Rectangle Bounds;
        public Rectangle DefaultBounds;
        public Rectangle LayoutAnchor;
        public bool EditorOnly;
        public string LayoutKey;
        public DictAtlasEntry Sprite;
        public string SpriteName;

        public bool IsDown;
        public bool WasDown;

        public float DisplayAlpha = GameSettings.TouchOpacity * 0.01f;
        public float ShadowAlpha = GameSettings.ShadowOpacity * 0.01f;

        public int? TouchId;

        public VirtualButton(string sprite, CButtons button, Rectangle bounds)
        {
            Sprite = Resources.GetSprite(sprite);
            SpriteName = sprite;
            Button = button;
            Bounds = bounds;
            DefaultBounds = bounds;
            LayoutAnchor = bounds;
            LayoutKey = VirtualController.GetLayoutKey(button);
            IsDown = false;
            WasDown = false;
            TouchId = null;
        }

        public void BeginUpdate()
        {
            WasDown = IsDown;
            IsDown = false;
        }

        public bool Pressed()
        {
            return IsDown && !WasDown;
        }

        public bool Released()
        {
            return !IsDown && WasDown;
        }

        public bool Contains(Point point)
        {
            return Bounds.Contains(point);
        }

        public bool ContainsExpanded(Point point, int padding)
        {
            Rectangle expanded = new Rectangle(
                Bounds.X - padding,
                Bounds.Y - padding,
                Bounds.Width + padding * 2,
                Bounds.Height + padding * 2);

            return expanded.Contains(point);
        }
    }
}
