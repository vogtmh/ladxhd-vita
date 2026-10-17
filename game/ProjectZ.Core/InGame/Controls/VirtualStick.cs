using Microsoft.Xna.Framework;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Controls
{
    public class VirtualStick
    {
        public DictAtlasEntry BaseSprite;
        public DictAtlasEntry HeadSprite;

        public Vector2 Center;
        public Vector2 DefaultCenter;
        public Vector2 AnchorCenter;
        public bool EditorOnly;
        public string LayoutKey;
        public float Radius;

        public bool IsDown;
        public bool WasDown;

        public float DisplayAlpha = GameSettings.TouchOpacity * 0.01f;
        public float ShadowAlpha = GameSettings.ShadowOpacity * 0.01f;

        public int? TouchId;

        public Vector2 KnobPosition;
        public Vector2 Output;

        public VirtualStick(string sprite, Vector2 center, float radius)
        {
            BaseSprite = Resources.GetSprite("button_outer");
            HeadSprite = Resources.GetSprite(sprite);
            Center = center;
            DefaultCenter = center;
            AnchorCenter = center;
            Radius = radius;

            IsDown = false;
            WasDown = false;

            TouchId = null;

            KnobPosition = center;
            Output = Vector2.Zero;
        }

        public void BeginUpdate()
        {
            WasDown = IsDown;
            IsDown = false;
            Output = Vector2.Zero;
            KnobPosition = Center;
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
            return Vector2.Distance(Center, point.ToVector2()) <= Radius;
        }

        public bool ContainsExpanded(Point point, float padding)
        {
            return Vector2.Distance(Center, point.ToVector2()) <= Radius + padding;
        }

        public void SetTouchPosition(Vector2 position)
        {
            Vector2 offset = position - Center;

            float length = offset.Length();

            if (length > Radius && length > 0f)
                offset = offset / length * Radius;

            KnobPosition = Center + offset;

            if (Radius <= 0f)
            {
                Output = Vector2.Zero;
                return;
            }

            Vector2 rawOutput = offset / Radius;
            float rawLength = rawOutput.Length();

            float deadZone = GameSettings.DeadZone;

            if (rawLength <= deadZone)
            {
                Output = Vector2.Zero;
                return;
            }

            float scaledLength = (rawLength - deadZone) / (1f - deadZone);
            scaledLength = MathHelper.Clamp(scaledLength, 0f, 1f);

            if (rawLength > 0f)
                Output = Vector2.Normalize(rawOutput) * scaledLength;
            else
                Output = Vector2.Zero;
        }
    }
}
