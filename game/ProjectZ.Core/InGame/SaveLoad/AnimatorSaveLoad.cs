using System;
using System.IO;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.SaveLoad
{
    public class AnimatorSaveLoad
    {
        public static Animator LoadAnimator(string animatorId, bool redux = false)
        {
            // Try to load a custom animation file before the normal one.
            var customAnimator = Path.Combine(Values.PathAnimationMods, animatorId + ".ani");
            if (GameFS.Exists(customAnimator))
                return LoadAnimatorFile(customAnimator, redux);
            // Fall back to the game's normal animation files.
            var mainAnimator = Path.Combine(Values.PathDataFolder, "Animations", animatorId + ".ani");
            return LoadAnimatorFile(mainAnimator, redux);
        }

        private static string AddReduxToFilename(string spritePath)
        {
            // Safe for names with multiple dots: "foo.bar.png" -> "foo.bar_redux.png"
            var dot = spritePath.LastIndexOf('.');
            return dot > 0
                ? spritePath.Substring(0, dot) + "_redux" + spritePath.Substring(dot)
                : spritePath + "_redux";
        }

        public static Animator LoadAnimatorFile(string filePath, bool redux = false)
        {
            if (!GameFS.Exists(GameFS.ToAssetPath(filePath)))
                return null;

            using var stream = GameFS.OpenRead(GameFS.ToAssetPath(filePath));
            using var reader = new StreamReader(stream);

            var animator = new Animator();
            var version = reader.ReadLine();       // unused
            var spritePath = reader.ReadLine();    // required

            if (string.IsNullOrWhiteSpace(spritePath))
                return null;

            // If uncensored is enabled, pull from the "_redux" version of the sprite sheet.
            if (redux)
                spritePath = AddReduxToFilename(spritePath);

            animator.SpritePath = spritePath;
            animator.SprTexture = Resources.GetTexture(animator.SpritePath);

            // If the texture couldn't be found/loaded, fail fast.
            if (animator.SprTexture == null)
                return null;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var s = line.Split(';');
                if (s.Length < 16)
                    continue;

                int pos = 0;

                var animationId = (s[pos] ?? "").ToLowerInvariant();
                if (string.IsNullOrEmpty(animationId))
                    continue;

                var animation = new Animation(animationId)
                {
                    NextAnimation = (s[++pos] ?? "").ToLowerInvariant(),
                    LoopCount     = Convert.ToInt32(s[++pos])
                };

                animation.Offset.X = Convert.ToInt32(s[++pos]);
                animation.Offset.Y = Convert.ToInt32(s[++pos]);

                int frames = Convert.ToInt32(s[++pos]);
                if (frames < 0) frames = 0;

                animation.Frames = new Frame[frames];
                animator.AddAnimation(animation);

                for (int i = 0; i < frames; i++)
                {
                    var frame = new Frame
                    {
                        FrameTime = Convert.ToInt32(s[++pos]),

                        SourceRectangle = new Rectangle(
                            Convert.ToInt32(s[++pos]),Convert.ToInt32(s[++pos]),
                            Convert.ToInt32(s[++pos]),Convert.ToInt32(s[++pos])),

                        Offset = new Point(
                            Convert.ToInt32(s[++pos]), Convert.ToInt32(s[++pos])),

                        CollisionRectangle = new Rectangle(
                            Convert.ToInt32(s[++pos]), Convert.ToInt32(s[++pos]),
                            Convert.ToInt32(s[++pos]), Convert.ToInt32(s[++pos])),

                        MirroredV = Convert.ToBoolean(s[++pos]),
                        MirroredH = Convert.ToBoolean(s[++pos]),
                    };
                    animator.SetFrameAt(animationId, i, frame);
                }
            }
            return animator;
        }
    }
}
