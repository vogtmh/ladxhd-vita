using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Map
{
    public class Camera
    {
        public Vector2 Location;
        public Vector2 MoveLocation;
        private Rectangle fieldRect;

        public float Scale = 4;
        public float ShakeOffsetX;
        public float ShakeOffsetY;
        public float CameraFollowMultiplier = 1;

        public bool CameraPixelSnapping;

        private float RoundedShakeX => MathF.Round(ShakeOffsetX);
        private float RoundedShakeY => MathF.Round(ShakeOffsetY);

        public float RoundX => CameraPixelSnapping 
            ? MathF.Round((Location.X + RoundedShakeX * Scale) / Scale) * Scale
            : (int)Math.Round(Location.X + RoundedShakeX * Scale, MidpointRounding.AwayFromZero);
        public float RoundY => CameraPixelSnapping 
            ? MathF.Round((Location.Y + RoundedShakeY * Scale) / Scale) * Scale
            : (int)Math.Round(Location.Y + RoundedShakeY * Scale, MidpointRounding.AwayFromZero);

        public Matrix TransformMatrix
        {
            get
            {
                if (_viewportWidth <= 0 || _viewportHeight <= 0 || Scale <= 0f)
                    return Matrix.Identity;

                int centerX = _viewportWidth / 2;
                int centerY = _viewportHeight / 2;

                float tx = centerX - RoundX;
                float ty = centerY - RoundY;

                return
                    Matrix.CreateScale(Scale, Scale, 1f) *
                    Matrix.CreateTranslation(tx, ty, 0f) *
                    Game1.GameManager.GetMatrix;
            }
        }
        public static bool  LockCamera;
        public static bool  SnapCamera;
        public static float SnapCameraTimer;
        public static bool  SnapCameraOnce;

        public int X => (int)Math.Round(Location.X + ShakeOffsetX * Scale);
        public int Y => (int)Math.Round(Location.Y + ShakeOffsetY * Scale);

        public int ScaleValue => (int)Scale;

        private Vector2 _cameraDistance;

        private int _viewportWidth;
        private int _viewportHeight;

        // Returns the UV offset needed to align the pixel grid with game pixels
        public Vector2 PixelGridOffset
        {
            get
            {
                int centerX = _viewportWidth / 2;
                int centerY = _viewportHeight / 2;

                // Translation applied by the transform matrix
                float tx = centerX - RoundX;
                float ty = centerY - RoundY;

                // How far the top-left of the screen is from a whole game pixel boundary
                float offsetX = ((-tx) % Scale) / Scale;
                float offsetY = ((-ty) % Scale) / Scale;

                return new Vector2(offsetX, offsetY);
            }
        }

    //----------------------------------------------------------------------------------------------------------------------
    //   LAHDMOD
    //----------------------------------------------------------------------------------------------------------------------

        // Values configurable via lahdmod.
        private float classic_transition_speed = 1.00f;
        private float classic_scroll_speed = 1.00f;
        private float classic_scroll_step = 4.00f;

        // There isn't a good entry point here so this is loaded in "MapManager".
        public void LoadLAHDModFile()
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "Camera.lahdmod");
            ModFile.Parse(modFile, this);
        }
    
    //----------------------------------------------------------------------------------------------------------------------
    //   CLASSIC CAMERA 
    //----------------------------------------------------------------------------------------------------------------------

        // Vars to force Classic Camera during boss fights.
        public static bool ForceClassic;
        public bool ClassicReset;
        private GameObject _bossField;

        // The primary variable to report back if Classic Camera should be used.
        public static bool ClassicMode =>
            ForceClassic ||                                                                         // Classic Camera is forced when "ClassicBosses" is enabled and a boss if being fought.
            GameSettings.CameraMode == 1 ||                                                         // Always use "Classic Camera" when the Camera Type is set to it.
            (GameSettings.CameraMode == 2 &&                                                        // When Camera Type is set to "Customize Camera" the type of map must match the setting to use Classic Camera.
                ((GameSettings.ClassicOverworld && MapManager.ObjLink?.Map?.IsOverworld == true) || // The current map is the overworld. Set by "ObjOverworld" map object when loaded into the game.
                (GameSettings.ClassicHouses && MapManager.ObjLink?.Map?.IsHouse == true) ||         // The current map is a house. Set by "ObjHouse" map object when loaded into the game.
                (GameSettings.ClassicCaves && MapManager.ObjLink?.Map?.IsCave == true) ||           // The current map is a cave. Set by "ObjCave" map object when loaded into the game.
                (GameSettings.ClassicDungeons && MapManager.ObjLink?.Map?.IsDungeon == true) ||     // The current map is a dungeon. Set by "ObjDungeon" map object in "GameManager.SetDungeon()".
                (GameSettings.ClassicCastle && MapManager.ObjLink?.Map?.IsCastle == true) ||        // The current map is a castle. Set by "ObjCastle" map object in "GameManager.SetCastle()".
                (GameSettings.ClassicEgg && MapManager.ObjLink?.Map?.IsEgg == true) ||              // The current map is the egg. Set by "ObjDungeonEgg" map object in "GameManager.SetDungeonEgg()".
                (GameSettings.Classic2DMaps && MapManager.ObjLink?.Map?.Is2dMap == true)));         // The current map is a 2D map. Set by "Obj2DMode" map object in "MapData.AddObject()".

        public void ForceClassicCamera(bool force)
        {
            // Forces Classic Camera on or off and starts a scale change.
            SnapCameraTimer = 50;
            ForceClassic = force;
            Game1.ScaleChanged = true;
        }

        private float GetClassicBiasOffset()
        {
            if (GameSettings.ClassicBias == 0 || _viewportHeight <= 0 || GameSettings.ClassicBorder == 2)
                return 0f;

            // Empty space between the field edge and screen edge when centered.
            float fieldHalfHeight  = (fieldRect.Height * Scale) / 2f;
            float screenHalfHeight = _viewportHeight / 2f;
            float room = screenHalfHeight - fieldHalfHeight;

            // Can't bias if field is as tall (or taller) than the screen.
            if (room <= 0f)
                return 0f;

            // Ceiling so the field reaches the edge flush.
            room = MathF.Floor(room);

            return GameSettings.ClassicBias == 1 ? -room : room;
        }

    //----------------------------------------------------------------------------------------------------------------------
    //   CLASSIC CAMERA: CLASSIC FIELD SCROLLING
    //----------------------------------------------------------------------------------------------------------------------

        
        private Vector2 _lastFieldTarget;                          // The field center the camera was last told to travel to.
        private bool _scrollingToField;                            // True while the classic camera is travelling between two field centers.
        private float _scrollStartDistance;                        // Distance of the current scroll, captured when it starts. Used for progress.
        private float _lastScrollDistance = float.MaxValue;        // Watchdog. If the remaining distance stops shrinking the scroll can never finish,
        private float _scrollStallTime;                            // so the latch is force cleared rather than freezing the world forever.

        public bool FieldScrollActive =>                           // The camera is travelling between two field centers. This is pure camera state and
            ClassicMode && _scrollingToField &&                    // can be referenced whether "Classic Scrolling" is enabled or not. 
            MapManager.ObjLink?.IsTransitioning != true;

        public bool FieldScrolling =>                              // The freeze, the push, and the constant scroll speed all key off this one. This is
            GameSettings.ClassicScrolling && FieldScrollActive;    // only true when "Classic Scrolling" is enabled and not when it's disabled.

        public float FieldScrollProgress =>                        // 0 when the scroll starts, 1 when the camera arrives. Link's push is derived from
            _scrollStartDistance <= 0f                             // this so the two can never desync regardless of speed or frame rate.
                ? 1.00f
                : MathHelper.Clamp(1f - ((MoveLocation - Location).Length() / _scrollStartDistance), 0f, 1f);

        public void QueueClassicReset(GameObject camField)
        {
            // When the field changes or a map transition happens, signals to reset camera.
            ClassicReset = true;

            // The boss camera field object is stored so it can be deleted later.
            if (camField != null)
                _bossField = camField;
        }

    //----------------------------------------------------------------------------------------------------------------------
    //   CAMERA OFFSET
    //----------------------------------------------------------------------------------------------------------------------

        private Vector2 _offsetTarget;
        private Vector2 _offsetCurrent;
        private float _offsetDuration;
        private float _offsetElapsed;
        private bool _offsetActive;

        public void StartOffset(Vector2 targetOffset, float duration)
        {
            _offsetTarget = targetOffset;
            _offsetCurrent = Vector2.Zero;
            _offsetDuration = duration;
            _offsetElapsed = 0f;
            _offsetActive = true;
        }

        public void StopOffset()
        {
            _offsetActive = false;
            _offsetCurrent = Vector2.Zero;
            _offsetTarget = Vector2.Zero;
        }

        private void UpdateOffset()
        {
            _offsetElapsed += Game1.DeltaTime;
            var t = MathHelper.Clamp(_offsetElapsed / _offsetDuration, 0f, 1f);
            t = t * t * (3f - 2f * t);
            _offsetCurrent = Vector2.Lerp(Vector2.Zero, _offsetTarget, t);
        }

    //----------------------------------------------------------------------------------------------------------------------
    //   CAMERA PANNING & POSITION
    //----------------------------------------------------------------------------------------------------------------------

        private Vector2 _panFrom;
        private Vector2 _panTo;
        private float _panDuration;
        private float _panElapsed;
        public bool IsPanning { get; private set; }

        private float CameraSpeedCurve(float x)
        {
            // Speed rises quickly then eases out.
            var y = MathF.Atan(x);

            // Linear growth to speed up for distant targets.
            if (x > 2) { y += (x - 2) / 2; }

            // Minimum speed to prevent reaching 0.
            return y + 0.1f;
        }

        public void ForceUpdate(Vector2 lockPosition)
        {
            MoveLocation = lockPosition;
            Location = lockPosition;

            // The camera was teleported, so any scroll in progress is void.
            _lastFieldTarget = lockPosition;
            _scrollStartDistance = 0f;
            _scrollingToField = false;
        }

        public void SoftUpdate(Vector2 position)
        {
            // When classic camera is enabled this will mess up transitions.
            if (ClassicMode && GameSettings.SmoothCamera)
                return;

            MoveLocation = position - _cameraDistance;
            Location = position;

            // The camera was repositioned outside of Center, so any scroll state is void.
            _lastFieldTarget = position;
            _scrollStartDistance = 0f;
            _scrollingToField = false;
        }

        public void OffsetCameraDistance(Vector2 offset)
        {
            _cameraDistance += offset;
        }

        public void StartPan(Vector2 from, Vector2 to, float duration)
        {
            _panFrom = from * Scale;
            _panTo = to * Scale;
            _panDuration = duration;
            _panElapsed = 0f;
            IsPanning = true;
            LockCamera = true;
            ForceUpdate(_panFrom);
        }

        public void StopPan()
        {
            IsPanning = false;
            LockCamera = false;
        }

        public void UpdatePan()
        {
            _panElapsed += Game1.DeltaTime;
            var t = MathHelper.Clamp(_panElapsed / _panDuration, 0f, 1f);
            t = t * t * (3f - 2f * t);

            var pos = Vector2.Lerp(_panFrom, _panTo, t);
            Location = pos;
            MoveLocation = pos;

            if (_panElapsed >= _panDuration)
                StopPan();
        }

        public void SetBounds(int viewportWidth, int viewportHeight)
        {
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;
        }

        public Rectangle GetCameraRectangle()
        {
            var rectangle = new Rectangle(
                (int)RoundX - _viewportWidth / 2,
                (int)RoundY - _viewportHeight / 2,
                _viewportWidth, _viewportHeight);
            return rectangle;
        }

        public Rectangle GetGameView()
        {
            var rectangle = new Rectangle(
                (int)(RoundX / Scale) - (int)(_viewportWidth / 2 / Scale),
                (int)(RoundY / Scale) - (int)(_viewportHeight / 2 / Scale),
                (int)(_viewportWidth / Scale), (int)(_viewportHeight / Scale));
            return rectangle;
        }

        public Vector2 GetFieldCenter()
        {
            // A camera field object takes over from Link entirely when one is present off the overworld.
            var useOverride = Game1.CameraObjects.CameraFieldCoords != Vector2.Zero && !MapManager.ObjLink.Map.IsOverworld;

            // With an override, resolve the field from the camera object's coords. Otherwise reuse the field
            // "ObjLink" already resolved this frame from "FieldScrollPoint", so the two can never disagree
            // about which field is current within a single frame.
            fieldRect = useOverride
                ? MapManager.ObjLink.Map.GetField(Game1.CameraObjects.CameraFieldCoords)
                : MapManager.ObjLink.CurrentField.Width > 0
                    ? MapManager.ObjLink.CurrentField
                    : MapManager.ObjLink.Map.GetField(MapManager.ObjLink.FieldScrollPoint);

            // Get the center point of the field.
            int rectCenterX = (fieldRect.X + fieldRect.Width / 2) * ScaleValue;
            int rectCenterY = (fieldRect.Y + fieldRect.Height / 2) * ScaleValue;
            return new Vector2(rectCenterX, rectCenterY);
        }

    //----------------------------------------------------------------------------------------------------------------------
    //   CAMERA CENTERING
    //----------------------------------------------------------------------------------------------------------------------

        public void Center(Vector2 position, bool moveX, bool moveY)
        {
            // If a reset is queued and a map transition or field change happens.
            if (ClassicReset && (MapManager.ObjLink.TransitioningIn || MapManager.ObjLink.FieldChange))
            {
                // Stop forcing classic camera.
                ForceClassic = false;
                ClassicReset = false;
                Game1.ScaleChanged = true;

                // Delete the boss camera field object
                if (_bossField != null && _bossField.Map != null)
                    _bossField.Map.Objects.DeleteObjects.Add(_bossField);
            }
            // If the camera is panning (used during the ending).
            if (IsPanning)
                UpdatePan();

            // Panning and other things may lock the camera. Do not update during lock.
            if (LockCamera)
            {
                // A pan starting mid scroll would strand the latch and freeze the world.
                _scrollingToField = false;
                return;
            }
            // If SnapCamera was enabled and a timer started.
            if (SnapCameraTimer > 0)
            {
                SnapCameraTimer -= Game1.DeltaTime;

                // Clamp at zero. Without this it drifts negative forever.
                if (SnapCameraTimer < 0)
                    SnapCameraTimer = 0;
            }
            // If it's classic camera, center the camera on the next field.
            if (ClassicMode)
            {
                CenterClassicCamera(moveX, moveY);
            }
            // If it's the modern camera, center the camera on Link.
            else
            {
                CenterModernCamera(position, moveX, moveY);
            }
        }

        private void CenterModernCamera(Vector2 position, bool moveX, bool moveY)
        {
            // If an offset is applied (used during the ending).
            if (_offsetActive)
                UpdateOffset();

            if (_offsetActive)
                position += _offsetCurrent * Scale;

            if (SnapCamera || SnapCameraTimer > 0)
            {
                Location = MapManager.GetCameraTargetLink();
                MoveLocation = MapManager.GetCameraTargetLink();
            }
            if (!GameSettings.SmoothCamera)
            {
                Location = position;
                return;
            }
            var direction = position - MoveLocation;

            if (direction != Vector2.Zero)
            {
                var distance = direction.Length() / Scale * CameraFollowMultiplier;
                var speedMult = CameraSpeedCurve(distance / 12.5f);

                direction.Normalize();
                var cameraSpeed = direction * speedMult * Scale * Game1.TimeMultiplier;

                if (moveX)
                    MoveLocation.X += cameraSpeed.X;
                if (moveY)
                    MoveLocation.Y += cameraSpeed.Y;
                if (distance <= 0.1f * Game1.TimeMultiplier)
                    MoveLocation = position;
            }
            // This is needed so the player does not wiggle around while the camera is following him.
            if (moveX)
                _cameraDistance.X = position.X - MoveLocation.X;
            if (moveY)
                _cameraDistance.Y = position.Y - MoveLocation.Y;
            Location = new Vector2((int)Math.Round(position.X), (int)Math.Round(position.Y)) - _cameraDistance;
        }

        private void CenterClassicCamera(bool moveX, bool moveY)
        {
            // Get the field rectangle and its center.
            Vector2 rectCenter = GetFieldCenter();

            // Apply vertical bias.
            rectCenter.Y -= GetClassicBiasOffset();

            // Stall watchdog. A healthy scroll shrinks the remaining distance every frame.
            // If it stops shrinking the arrival check can never fire, so clear the latch.
            if (_scrollingToField)
            {
                var remaining = (MoveLocation - Location).Length();

                if (remaining < _lastScrollDistance - 0.01f)
                    _scrollStallTime = 0f;
                else
                    _scrollStallTime += Game1.DeltaTime;

                _lastScrollDistance = remaining;

                // Roughly six frames of no progress.
                if (_scrollStallTime > 100)
                    _scrollingToField = false;
            }
            else
            {
                _scrollStallTime = 0f;
                _lastScrollDistance = float.MaxValue;
            }
            // Snap when no smoothing, when snapping is enabled, or when the snap timer is set.
            if (!GameSettings.SmoothCamera || SnapCamera || SnapCameraOnce || SnapCameraTimer > 0)
            {
                Location = rectCenter;
                MoveLocation = rectCenter;
                _lastFieldTarget = rectCenter;
                _scrollingToField = false;
                SnapCameraOnce = false;
                return;
            }
            // The target moved, so this is the start of a scroll.
            if (rectCenter != _lastFieldTarget)
            {
                var jump = (rectCenter - _lastFieldTarget).Length();
                _lastFieldTarget = rectCenter;
                _scrollStartDistance = (rectCenter - Location).Length();
                _scrollingToField = _scrollStartDistance > 0f && jump <= 160 * Scale;
            }
            if (MoveLocation != rectCenter)
                MoveLocation = rectCenter;

            // Smoothly move camera toward MoveLocation.
            var direction = MoveLocation - Location;
            if (direction != Vector2.Zero)
            {
                var remaining = direction.Length();
                var distance = remaining / Scale * CameraFollowMultiplier;

                direction.Normalize();

                // When "Classic Scrolling" is enabled, scroll the camera at a consistent speed.
                if (FieldScrolling)
                {
                    // Calculate the speed of the scroll using "pixels per frame". The original game is
                    // 4 pixels per frame, so for original speed "classic_scroll_step" should be "4".
                    var speed = classic_scroll_speed;
                    var step = classic_scroll_step * Scale * Game1.TimeMultiplier * speed;

                    // Snap to the target position when close enough to avoid oscillation.
                    if (step >= remaining)
                    {
                        Location = MoveLocation;
                        _scrollingToField = false;
                    }
                    // Consistently move the camera to the target position.
                    else
                    {
                        var cameraSpeed = direction * step;

                        if (moveX)
                            Location.X += cameraSpeed.X;
                        if (moveY)
                            Location.Y += cameraSpeed.Y;
                    }
                }
                else
                {
                    var speedMult = CameraSpeedCurve(distance / 3.5f);
                    var cameraSpeed = direction * speedMult * Scale * Game1.TimeMultiplier * classic_transition_speed;

                    if (moveX)
                        Location.X += cameraSpeed.X;
                    if (moveY)
                        Location.Y += cameraSpeed.Y;

                    // Snap if close enough.
                    if (distance <= 0.1f * Game1.TimeMultiplier)
                    {
                        Location = MoveLocation;
                        _scrollingToField = false;
                    }
                }
            }
            else
            {
                // Camera can not travel further, so prevent permanently freezing the world.
                _scrollingToField = false;
            }
        }

    //----------------------------------------------------------------------------------------------------------------------
    //   DRAWING CODE: CLASSIC CAMERA BORDER / SUPER GAME BOY BORDER
    //----------------------------------------------------------------------------------------------------------------------

        public void Draw(SpriteBatch spriteBatch)
        {
            // Create a border around the current field.
            if (ClassicMode)
            {
                // Draw the black border even if using SGB border.
                if (GameSettings.ClassicBorder > 0)
                {
                    int thickness = 4;
                    float scale = ScaleValue;

                    // Screen center
                    var viewport = spriteBatch.GraphicsDevice.Viewport;
                    var screenCenter = new Vector2(viewport.Width / 2f, viewport.Height / 2f);

                    // Compute scaled field rect
                    var fieldX = fieldRect.X * scale - thickness;
                    var fieldY = fieldRect.Y * scale - thickness;
                    var fieldW = fieldRect.Width * scale + thickness * 2;
                    var fieldH = fieldRect.Height * scale + thickness * 2;

                    // Compute the field center in world space
                    var fieldCenter = new Vector2(
                        (fieldRect.X + fieldRect.Width / 2f) * scale,
                        (fieldRect.Y + fieldRect.Height / 2f) * scale
                    );

                    // Offset so that the field rect is centered on screen (like your camera)
                    var drawOffset = screenCenter - (fieldCenter - Location);
                    drawOffset.Y += GetClassicBiasOffset();
                    var tex = Resources.SprWhite;

                    // Draw border lines (using alpha)
                    Color borderColor = Color.Black * GameSettings.ClassicAlpha;

                    // Draw Order: Top / Bottom / Left / Right
                    spriteBatch.Draw(tex, new Rectangle((int)(drawOffset.X + fieldX - Location.X), (int)(drawOffset.Y + fieldY - Location.Y), (int)fieldW, thickness), borderColor);
                    spriteBatch.Draw(tex, new Rectangle((int)(drawOffset.X + fieldX - Location.X), (int)(drawOffset.Y + fieldY - Location.Y + fieldH - thickness), (int)fieldW, thickness), borderColor);
                    spriteBatch.Draw(tex, new Rectangle((int)(drawOffset.X + fieldX - Location.X), (int)(drawOffset.Y + fieldY - Location.Y - 1), thickness, (int)fieldH + 2), borderColor);
                    spriteBatch.Draw(tex, new Rectangle((int)(drawOffset.X + fieldX - Location.X + fieldW - thickness), (int)(drawOffset.Y + fieldY - Location.Y - 1), thickness, (int)fieldH + 2), borderColor);

                    // Fill everything outside the border with black.
                    var screenW = viewport.Width + 1;
                    var screenH = viewport.Height + 1;

                    // Compute the rectangle's position on screen.
                    var rectScreenX = drawOffset.X + fieldX - Location.X;
                    var rectScreenY = drawOffset.Y + fieldY - Location.Y;

                    // Draw border fill (using alpha)
                    Color blackoutColor = Color.Black * GameSettings.ClassicAlpha;

                    int topFillH    = (int)rectScreenY;
                    int bottomFillY = (int)(rectScreenY + fieldH);

                    // Draw Order: Top / Bottom / Left / Right
                    spriteBatch.Draw(tex, new Rectangle(0, 0, screenW, Math.Max(0, topFillH)), blackoutColor);
                    spriteBatch.Draw(tex, new Rectangle(0, bottomFillY, screenW, Math.Max(0, screenH - bottomFillY)), blackoutColor);
                    spriteBatch.Draw(tex, new Rectangle(0, (int)rectScreenY - 1, (int)rectScreenX, (int)fieldH + 2), blackoutColor);
                    spriteBatch.Draw(tex, new Rectangle((int)(rectScreenX + fieldW), (int)rectScreenY - 1, (int)(screenW - (rectScreenX + fieldW)), (int)fieldH + 2), blackoutColor);

                    // If set to the Super Game Boy border.
                    if (GameSettings.ClassicBorder == 2)
                    {
                        // Border's original pixel size.
                        const int borderW = 256;
                        const int borderH = 224;

                        // Scale by the camera scale (which should be integer scale with Classic Camera enabled).
                        int scaledW = (int)(borderW * MapManager.Camera.Scale);
                        int scaledH = (int)(borderH * MapManager.Camera.Scale);

                        // Center the SGB border on the screen.
                        Vector2 pos = new Vector2((viewport.Width  - scaledW) / 2f, (viewport.Height - scaledH) / 2f);

                        // Draw centered with point sampling.
                        spriteBatch.End();
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

                        // Draw the border using nearest neighbor (point filtering) for sharp pixels).
                        spriteBatch.Draw(Resources.sgbBorder, pos, null, Color.White, 0f, Vector2.Zero, MapManager.Camera.Scale, SpriteEffects.None, 0f);
                    }
                }
            }
        }
    }
}
