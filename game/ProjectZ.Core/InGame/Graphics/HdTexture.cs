using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.InGame.Graphics
{
    public static class HdTexture
    {
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_TexelWidth")]
        private static extern void SetTexelWidth(Texture2D texture, float value);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_TexelHeight")]
        private static extern void SetTexelHeight(Texture2D texture, float value);

        private sealed class ScaleBox { public int Value; }

        private static readonly ConditionalWeakTable<Texture2D, ScaleBox> _scales = new();

        // Flips to false if a MonoGame update ever removes the internal setters. HD textures then
        // fall back to drawing at 1x (wrong size) instead of crashing.
        private static bool _supported = true;

        public static int GetScale(Texture2D texture)
        {
            if (texture == null)
                return 1;

            return _scales.TryGetValue(texture, out var box) ? box.Value : 1;
        }
        public static int LogicalWidth(Texture2D texture) => texture.Width / GetScale(texture);

        public static int LogicalHeight(Texture2D texture) => texture.Height / GetScale(texture);

        // Works out the scale of "texture" by comparing its size against "reference", which is either the
        // texture it replaces or the original 1x version. Does nothing if the texture already has a scale,
        // or if its size is not an exact, uniform multiple of the reference (an edited or extended sheet).
        public static void InferScale(Texture2D texture, Texture2D reference, string debugName = null)
        {
            if (texture == null || reference == null || GetScale(texture) != 1)
                return;

            int baseWidth = LogicalWidth(reference);
            int baseHeight = LogicalHeight(reference);

            if (baseWidth <= 0 || baseHeight <= 0)
                return;

            if (texture.Width % baseWidth != 0 || texture.Height % baseHeight != 0)
                return;

            int scaleX = texture.Width / baseWidth;
            int scaleY = texture.Height / baseHeight;

            if (scaleX != scaleY)
            {
                Debug.WriteLine($"[HdTexture] {debugName}: uneven scale {scaleX}x{scaleY} compared to the reference texture, using 1.");
                return;
            }
            if (scaleX > 1)
                SetScale(texture, scaleX, debugName);
        }

        public static void SetScale(Texture2D texture, int scale, string debugName = null)
        {
            if (texture == null)
                return;

            if (scale < 1)
                scale = 1;

            if (texture.Width % scale != 0 || texture.Height % scale != 0)
            {
                Debug.WriteLine($"[HdTexture] {debugName}: {texture.Width}x{texture.Height} is not divisible by scale {scale}, using 1.");
                scale = 1;
            }

            if (!_supported)
            {
                if (scale > 1)
                    Debug.WriteLine($"[HdTexture] {debugName}: HD textures are unsupported on this MonoGame build, using 1.");
                return;
            }

            try
            {
                SetTexelWidth(texture, scale / (float)texture.Width);
                SetTexelHeight(texture, scale / (float)texture.Height);
            }
            catch (MissingMemberException ex)
            {
                _supported = false;
                Debug.WriteLine($"[HdTexture] Texture2D.TexelWidth/TexelHeight setters not found, HD textures disabled: {ex.Message}");
                return;
            }

            _scales.AddOrUpdate(texture, new ScaleBox { Value = scale });

            if (scale > 1)
                Debug.WriteLine($"[HdTexture] {debugName}: scale {scale}, {texture.Width}x{texture.Height} texels, {LogicalWidth(texture)}x{LogicalHeight(texture)} logical.");
        }
    }
}
