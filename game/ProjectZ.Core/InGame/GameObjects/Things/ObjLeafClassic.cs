using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;
 
namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjLeafClassic : GameObject
    {
        private static readonly (int dx, int dy, SpriteEffects fx)[,] FrameData = new (int, int, SpriteEffects)[8, 4]
        {
            // frame 0
            {
                ( -4,  2, SpriteEffects.None),
                (  4, -5, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically),
                (  6,  5, SpriteEffects.None),
                ( 10,  1, SpriteEffects.FlipHorizontally),
            },
            // frame 1
            {
                ( -1,  1, SpriteEffects.None),
                (  4, -7, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically),
                (  6,  8, SpriteEffects.None),
                (  7,  2, SpriteEffects.FlipHorizontally),
            },
            // frame 2
            {
                (  0,  0, SpriteEffects.FlipHorizontally),
                (  2, -8, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically),
                (  4,  4, SpriteEffects.FlipHorizontally),
                (  7, 10, SpriteEffects.FlipHorizontally),
            },
            // frame 3
            {
                (  1, -2, SpriteEffects.FlipHorizontally),
                (  1,  4, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically),
                (  5,  4, SpriteEffects.FlipHorizontally),
                (  7, 12, SpriteEffects.FlipHorizontally),
            },
            // frame 4
            {
                (  0, -3, SpriteEffects.FlipHorizontally),
                ( -2,  4, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically),
                (  8,  8, SpriteEffects.FlipHorizontally),
                (  9, 14, SpriteEffects.FlipHorizontally),
            },
            // frame 5
            {
                ( -1, -4, SpriteEffects.None),
                ( -6,  4, SpriteEffects.FlipVertically),
                (  9,  8, SpriteEffects.FlipHorizontally),
                ( 10, 15, SpriteEffects.None),
            },
            // frame 6
            {
                ( -2, -5, SpriteEffects.None),
                ( -7,  3, SpriteEffects.FlipVertically),
                ( 12,  8, SpriteEffects.None),
                ( 11, 17, SpriteEffects.None),
            },
            // frame 7  (last)
            {
                ( -3, -6, SpriteEffects.None),
                ( -9,  1, SpriteEffects.FlipVertically),
                ( 13,  9, SpriteEffects.None),
                ( 12, 15, SpriteEffects.None),
            },
        };
 
        private static readonly Color SwampTint = new Color(139, 100, 60);
 
        private const int    TotalFrames   = 8;
        private const double AnimDuration  = TotalFrames * 34.0;
        private const double FadeDuration  = 120.0;

        private readonly Vector2[]       _waypoints;
        private readonly SpriteEffects[] _fxFrames;
 
        private readonly AiComponent _aiComponent;
        private readonly CSprite     _sprite;
        private readonly bool        _isSwamp;
 
        private double _animTimer;
        private double _fadeTimer;

        public ObjLeafClassic(Map.Map map, int posX, int posY, bool isGrass, bool isSwamp, int leafIndex, float bushAlpha, float grassAlpha)
            : base(map)
        {
            _isSwamp = isSwamp;
            leafIndex = Math.Clamp(leafIndex, 0, 3);
 
            // Extract this leaf's waypoints and flip states from the shared table
            _waypoints = new Vector2[TotalFrames];
            _fxFrames  = new SpriteEffects[TotalFrames];
            for (int f = 0; f < TotalFrames; f++)
            {
                var (dx, dy, fx) = FrameData[f, leafIndex];
                _waypoints[f] = new Vector2(dx, dy);
                _fxFrames[f]  = fx;
            }
 
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize     = new Rectangle(-10, -12, 28, 28);
 
            _aiComponent = new AiComponent();
            _aiComponent.States.Add("animating", new AiState(StateAnimating));
            _aiComponent.States.Add("fading",    new AiState(StateFading));
            _aiComponent.ChangeState("animating");
 
            var sourceRectangle = Resources.SourceRectangle("leaf");
            _sprite = new CSprite(Resources.SprObjects, EntityPosition, sourceRectangle, Vector2.Zero);
            _sprite.Color = _isSwamp
                ? SwampTint
                : isGrass
                    ? _sprite.Color = Color.White * grassAlpha
                    : _sprite.Color = Color.White * bushAlpha;
 
            AddComponent(AiComponent.Index,   _aiComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
 
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void StateAnimating()
        {
            _animTimer += Game1.DeltaTime;

            float t = (float)Math.Min(_animTimer / AnimDuration, 1.0);
 
            // Map t onto the waypoint array: scaledT in [0, TotalFrames-1]
            float scaledT  = t * (TotalFrames - 1);
            int   seg      = Math.Min((int)scaledT, TotalFrames - 2);
            float localT   = scaledT - seg;
 
            // Catmull-Rom control points — clamp endpoints rather than extrapolate
            Vector2 p0 = _waypoints[Math.Max(seg - 1, 0)];
            Vector2 p1 = _waypoints[seg];
            Vector2 p2 = _waypoints[seg + 1];
            Vector2 p3 = _waypoints[Math.Min(seg + 2, TotalFrames - 1)];
 
            _sprite.DrawOffset   = CatmullRom(p0, p1, p2, p3, localT);
            _sprite.SpriteEffect = _fxFrames[seg];
 
            if (_animTimer >= AnimDuration)
                _aiComponent.ChangeState("fading");
        }
 
        private void StateFading()
        {
            _fadeTimer += Game1.DeltaTime;
 
            float alpha = 1f - (float)(_fadeTimer / FadeDuration);
            var baseColor = _isSwamp ? SwampTint : Color.White;
            _sprite.Color = baseColor * Math.Max(alpha, 0f);
 
            if (_fadeTimer >= FadeDuration)
                Map.Objects.DeleteObjects.Add(this);
        }

        // Catmull-Rom spline interpolation: Smoothly interpolates between p1 and p2 using
        // p0 and p3 as tangent guides. localT is in [0, 1] between the two inner points.
        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
 
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
