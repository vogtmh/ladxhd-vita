using System;
using ProjectZ.Base;
using Microsoft.Xna.Framework;

namespace ProjectZ.InGame.Things
{
    public static class GameMath
    {
        private static Random _rand = new Random();

        public static int GetRandomInt(int min, int max)
        {
            return _rand.Next(min, max + 1);
        }

        public static float GetRandomFloat(float min, float max)
        {
            return (float)(min + (max - min) * Game1.RandomNumber.NextDouble());
        }

        public static RectangleF RectToRectF(Rectangle rect)
        {
            return new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);
        }

        public static Rectangle RectFToRect(RectangleF rect)
        {
            return new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        }

        public static bool HasSameSign(this Vector2 a, Vector2 b)
        {
            return SameSign(a.X, b.X) && SameSign(a.Y, b.Y);
        }

        private static bool SameSign(float x, float y)
        {
            // Treat 0 and -0 as equivalent and positive
            if (x == 0f && y == 0f)
                return true;

            return Math.Sign(x) == Math.Sign(y);
        }

        public static bool HasInvertedSigns(this Vector2 a, Vector2 b)
        {
            return OppositeSign(a.X, b.X) && OppositeSign(a.Y, b.Y);
        }

        private static bool OppositeSign(float x, float y)
        {
            // Zero check: if both are zero or one is zero, not considered opposite
            if (x == 0f || y == 0f)
                return false;

            // True only if one is positive and the other is negative
            return Math.Sign(x) != Math.Sign(y);
        }
    }
}
