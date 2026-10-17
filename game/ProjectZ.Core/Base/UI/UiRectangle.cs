using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.Base.UI
{
    public class UiRectangle : UiElement
    {
        public Color BlurColor;
        public float Radius = 0;
        public bool IsHudElement;

        // Base HUD color and opacity for the opaque background mode
        public static readonly Color OpaqueHudColor = new Color(230, 230, 170) * 0.90f;

        public UiRectangle(Rectangle rectangle, string elementId, string screen, Color color, Color blurColor, UiFunction update) : base(elementId, screen)
        {
            BlurColor = blurColor;
            Rectangle = rectangle;
            UpdateFunction = update;
            BackgroundColor = color;
        }

        // The background is now drawn in DrawBlur, which now runs in both modes.
        public override void Draw(SpriteBatch spriteBatch) { }

        public override void DrawBlur(SpriteBatch spriteBatch)
        {
            // If the opacity is 0 there is nothing to do here.
            if (BackgroundColor.A <= 0)
                return;

            // When blur is disabled, use the color provided above. When it's
            // enabled, use the background color that was inherited in init.  
            var drawColor = (GameSettings.DisableUiBlur && IsHudElement) 
                ? OpaqueHudColor 
                : BackgroundColor;

            // Calculate the radius of the rounded corners.
            var radiusPx = Radius * Game1.UiScale;

            // Set up the shader parameters.
            Resources.RoundedCornerBlurEffect.Parameters["blurColor"].SetValue(BlurColor.ToVector4());
            Resources.RoundedCornerBlurEffect.Parameters["width"]?.SetValue(Rectangle.Width);
            Resources.RoundedCornerBlurEffect.Parameters["height"]?.SetValue(Rectangle.Height);
            Resources.RoundedCornerBlurEffect.Parameters["scale"]?.SetValue(1f);
            Resources.RoundedCornerBlurEffect.Parameters["radius"]?.SetValue(radiusPx);

            // Draw the UI box. 
            spriteBatch.Draw(Resources.SprWhite, Rectangle, drawColor);
        }
    }
}
