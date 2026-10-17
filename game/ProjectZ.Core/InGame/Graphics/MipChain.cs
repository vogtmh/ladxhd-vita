using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Graphics
{
    internal static class MipChain
    {
        public static bool Enabled = true;

        private static readonly Regex MipFileRegex = new Regex(@"_mip\d+\.png$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool IsMipFile(string fileName) => MipFileRegex.IsMatch(fileName);

        public static Texture2D TryLoad(string assetPath)
        {
            if (!Enabled || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return null;

            var basePath = assetPath.Substring(0, assetPath.Length - 4);

            if (!GameFS.Exists(LevelPath(basePath, 1)))
                return null;

            var level0 = Decode(assetPath, out var width, out var height);
            var texture = new Texture2D(Game1.Graphics.GraphicsDevice, width, height, true, SurfaceFormat.Color);
            texture.SetData(0, null, level0, 0, level0.Length);

            var previous = level0;
            var previousWidth = width;
            var previousHeight = height;
            var supplied = 0;

            for (var level = 1; level < texture.LevelCount; level++)
            {
                var levelWidth = Math.Max(1, width >> level);
                var levelHeight = Math.Max(1, height >> level);
                var levelPath = LevelPath(basePath, level);
                byte[] data = null;

                if (GameFS.Exists(levelPath))
                {
                    var decoded = Decode(levelPath, out var fileWidth, out var fileHeight);

                    if (fileWidth == levelWidth && fileHeight == levelHeight)
                    {
                        data = decoded;
                        supplied++;
                    }
                    else
                    {
                        Debug.WriteLine($"[MipChain] {levelPath}: is {fileWidth}x{fileHeight}, level {level} must be {levelWidth}x{levelHeight}. Generating it instead.");
                    }
                }

                data ??= Downsample(previous, previousWidth, previousHeight, levelWidth, levelHeight);
                texture.SetData(level, null, data, 0, data.Length);

                previous = data;
                previousWidth = levelWidth;
                previousHeight = levelHeight;
            }
            Debug.WriteLine($"[MipChain] {assetPath}: {texture.LevelCount} levels, {supplied} supplied, {texture.LevelCount - 1 - supplied} generated.");
            return texture;
        }

        private static string LevelPath(string basePath, int level) => basePath + "_mip" + level + ".png";

        private static byte[] Decode(string path, out int width, out int height)
        {
            byte[] pixels = null;

            using (var stream = GameFS.OpenReadAny(path))
            using (var decoded = Texture2D.FromStream(Game1.Graphics.GraphicsDevice, stream, data =>
            {
                DefaultColorProcessors.ZeroTransparentPixels(data);
                pixels = data;
            }))
            {
                width = decoded.Width;
                height = decoded.Height;
            }

            return pixels;
        }

        private static byte[] Downsample(byte[] source, int sourceWidth, int sourceHeight, int width, int height)
        {
            var result = new byte[width * height * 4];

            for (var y = 0; y < height; y++)
            {
                var y0 = Math.Min(y * 2, sourceHeight - 1);
                var y1 = Math.Min(y * 2 + 1, sourceHeight - 1);

                for (var x = 0; x < width; x++)
                {
                    var x0 = Math.Min(x * 2, sourceWidth - 1);
                    var x1 = Math.Min(x * 2 + 1, sourceWidth - 1);

                    int r = 0, g = 0, b = 0, a = 0;
                    Accumulate(source, (y0 * sourceWidth + x0) * 4, ref r, ref g, ref b, ref a);
                    Accumulate(source, (y0 * sourceWidth + x1) * 4, ref r, ref g, ref b, ref a);
                    Accumulate(source, (y1 * sourceWidth + x0) * 4, ref r, ref g, ref b, ref a);
                    Accumulate(source, (y1 * sourceWidth + x1) * 4, ref r, ref g, ref b, ref a);

                    var offset = (y * width + x) * 4;
                    if (a > 0)
                    {
                        result[offset] = (byte)((r + a / 2) / a);
                        result[offset + 1] = (byte)((g + a / 2) / a);
                        result[offset + 2] = (byte)((b + a / 2) / a);
                    }
                    result[offset + 3] = (byte)((a + 2) / 4);
                }
            }
            return result;
        }

        private static void Accumulate(byte[] data, int index, ref int r, ref int g, ref int b, ref int a)
        {
            int alpha = data[index + 3];
            r += data[index] * alpha;
            g += data[index + 1] * alpha;
            b += data[index + 2] * alpha;
            a += alpha;
        }
    }
}
