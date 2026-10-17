using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Map
{
    public class MapManager
    {
        public static Camera Camera;
        public static ObjLink ObjLink;
        public static BlendState LightBlendState = new BlendState();
        public static Vector2 CameraOffset;

        public Map CurrentMap;
        public Map NextMap;

        public bool UpdateCameraX;
        public bool UpdateCameraY;

        private Matrix blurMatrix;

        public MapManager()
        {
            CurrentMap = new Map();
            NextMap = new Map();
            Camera = new Camera();

            // Try to load a lahdmod file after the camera has been initialized.
            Camera.LoadLAHDModFile();

            LightBlendState = new BlendState
            {
                ColorBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.One,
                ColorDestinationBlend = Blend.InverseSourceAlpha,

                AlphaBlendFunction = Game1.GraphicsCapabilities.SupportsBlendFunctionMax ? BlendFunction.Max : BlendFunction.Add,
                AlphaSourceBlend = Blend.One,
                AlphaDestinationBlend = Blend.One,
            };
        }

        public void Load()
        {
            ObjLink = new ObjLink();
        }

        public void Update(bool frozen)
        {
            // Update the objects on the map.
            CurrentMap.Objects.Update(frozen);
            CurrentMap.UpdateMapUpdateState();
            UpdateCamera();
        }

        public void UpdateAnimation()
        {
            CurrentMap.Objects.UpdateAnimations();
        }

        public Vector2 GetCameraTarget()
        {
            if (CurrentMap.CameraTarget.HasValue)
            {
                return CurrentMap.CameraTarget.Value * Camera.Scale;
            }
            return GetCameraTargetLink();
        }

        static public Vector2 GetCameraTargetLink()
        {
            return (new Vector2 (ObjLink.PosX, ObjLink.PosY - 4) + CameraOffset) * Camera.Scale;
        }

        public void UpdateCamera()
        {
            UpdateOffsetCoords();

            if (UpdateCameraX || UpdateCameraY)
                Camera.Center(GetCameraTarget(), UpdateCameraX, UpdateCameraY);

            // Classic Camera: Push Link as the camera is scrolling.
            ObjLink.UpdateFieldScrollPush();

            UpdateCameraX = true;
            UpdateCameraY = true;

            if (ControlHandler.ButtonPressed(CButtons.RS))
                ResetOffset();
        }

        public void UpdateCameraOnly()
        {
            // Update only the camera position, with no input handling or object side effects.
            // Used while the game is paused so a scale change can still reposition the camera.
            if (UpdateCameraX || UpdateCameraY)
                Camera.Center(GetCameraTarget(), UpdateCameraX, UpdateCameraY);

            UpdateCameraX = true;
            UpdateCameraY = true;
        }

        private void UpdateOffsetCoords()
        {
            if (GameSettings.CameraLock || Camera.ClassicMode)
            {
                CameraOffset = Vector2.Zero;
                return;
            }
            var direction = ControlHandler.GetCameraVector2();
            float cameraSpeed = 0.60f;
            CameraOffset += direction * cameraSpeed * Game1.DeltaTime;
        }

        public static void ResetOffset()
        {
            CameraOffset = Vector2.Zero;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Update the viewport values before drawing.
            CurrentMap.Objects.UpdateViewPort();

            // draw the objects under the tilemap
            CurrentMap.Objects.DrawBottom(spriteBatch);

            // draw the tile map
            CurrentMap.TileMap.Draw(spriteBatch);

            // draw the objects draw over the tileset
            CurrentMap.Objects.DrawMiddle(spriteBatch);

            // draw the blured part of the tile map
            DrawBlur(spriteBatch, Game1.GameManager.TempRT2, Game1.GameManager.TempRT0, Game1.GameManager.TempRT1);

            // draw the objects
            CurrentMap.Objects.Draw(spriteBatch);

            spriteBatch.End();
        }

        public void DrawBegin(SpriteBatch spriteBatch, Effect spriteEffect)
        {
            if (!Game1.GameManager.UseShockEffect)
                spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, null, spriteEffect, blurMatrix);
            else
                ObjectManager.SpriteBatchBegin(spriteBatch, null);
        }

        private void DrawBlur(SpriteBatch spriteBatch, RenderTarget2D blurRT0, RenderTarget2D blurRT1, RenderTarget2D blurRT2)
        {
            // Defensive: require all RTs to be valid
            if (blurRT0 == null || blurRT1 == null || blurRT2 == null)
                return;

            if (blurRT0.Width <= 0 || blurRT0.Height <= 0 ||
                blurRT1.Width <= 0 || blurRT1.Height <= 0 ||
                blurRT2.Width <= 0 || blurRT2.Height <= 0)
                return;

            try
            {
                var matrixPosition = Vector2.Zero;

                if (!Game1.GameManager.UseShockEffect)
                {
                    Game1.Graphics.GraphicsDevice.SetRenderTarget(blurRT0);
                    Game1.Graphics.GraphicsDevice.Clear(Color.Transparent);

                    var cameraPosition = new Vector2(Camera.RoundX / Camera.Scale, Camera.RoundY / Camera.Scale);
                    matrixPosition = new Vector2((int)cameraPosition.X - 1, (int)cameraPosition.Y - 1);

                    // Build blurMatrix - clamp values and avoid NaN
                    var translateX = -matrixPosition.X;
                    var translateY = -matrixPosition.Y;
                    if (float.IsNaN(translateX) || float.IsNaN(translateY))
                    {
                        translateX = 0; translateY = 0;
                    }

                    blurMatrix = Matrix.CreateScale(1f) *
                                 Matrix.CreateTranslation(new Vector3(translateX, translateY, 0f)) *
                                 Matrix.CreateTranslation(new Vector3(
                                    (int)(Game1.GameManager.SideBlurRenderTargetWidth * 0.5f),
                                    (int)(Game1.GameManager.SideBlurRenderTargetHeight * 0.5f), 0f)) *
                                 Matrix.CreateScale(new Vector3(
                                    (float)blurRT0.Width / Game1.GameManager.SideBlurRenderTargetWidth,
                                    (float)blurRT0.Height / Game1.GameManager.SideBlurRenderTargetHeight, 1f));
                }

                // Begin drawing to blurRT0 (or main RT) - ensure DrawBegin uses blurMatrix; supply null safely if needed
                DrawBegin(spriteBatch, null);

                // draw object blur stuff
                CurrentMap.Objects.DrawBlur(spriteBatch);

                // blur tile maps
                CurrentMap.TileMap.DrawBlurLayer(spriteBatch);

                // Close the batch we opened in DrawBegin
                spriteBatch.End();

                if (Game1.GameManager.UseShockEffect)
                    return;

                // safety: ensure blurRT1/2 still valid before using them
                if (blurRT1 == null || blurRT2 == null || blurRT1.Width <= 0 || blurRT1.Height <= 0 || blurRT2.Width <= 0 || blurRT2.Height <= 0)
                    return;

                // prepare blur shaders - safe access of widths
                Resources.BBlurEffectH.Parameters["pixelX"].SetValue(1.0f / blurRT1.Width);
                Resources.BBlurEffectV.Parameters["pixelY"].SetValue(1.0f / blurRT1.Height);

                // v blur pass
                Game1.Graphics.GraphicsDevice.SetRenderTarget(blurRT1);
                Game1.Graphics.GraphicsDevice.Clear(Color.Transparent);

                var blurX = (-(matrixPosition.X % 2) / 2.0f) + (blurRT1.Width % 2 * 0.5f);
                var blurY = (-(matrixPosition.Y % 2) / 2.0f) + (blurRT1.Height % 2 * 0.5f);

                spriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, Resources.BBlurEffectV, null);
                spriteBatch.Draw(blurRT0, new Vector2(blurX, blurY),
                    new Rectangle(0, 0, blurRT0.Width, blurRT0.Height), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
                spriteBatch.End();

                // h blur pass
                Game1.Graphics.GraphicsDevice.SetRenderTarget(blurRT2);
                Game1.Graphics.GraphicsDevice.Clear(Color.Transparent);

                spriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, Resources.BBlurEffectH, null);
                spriteBatch.Draw(blurRT1, Vector2.Zero, Color.White);
                spriteBatch.End();

                // Restore the active render target to your main RT
                Game1.GameManager.SetActiveRenderTarget();

                // Draw the blurred texture into world using camera transform - ensure Camera.TransformMatrix is valid
                var safeTransform = Camera.TransformMatrix;

                // optional check for NaN components here, replace with Matrix.Identity if found
                if (float.IsNaN(safeTransform.M11) || float.IsNaN(safeTransform.M41))
                    safeTransform = Matrix.Identity;

                spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.AnisotropicClamp, null, null, null, safeTransform);

                var scale = new Vector2((float)Game1.GameManager.BlurRenderTargetWidth / blurRT1.Width * 2f,
                                        (float)Game1.GameManager.BlurRenderTargetHeight / blurRT1.Height * 2f);

                var drawPos = new Vector2(
                    matrixPosition.X + matrixPosition.X % 2 - (int)(Game1.GameManager.SideBlurRenderTargetWidth * 0.5f) - (int)(Game1.RenderWidth / (Camera.Scale * 2)) % 2,
                    matrixPosition.Y + matrixPosition.Y % 2 - (int)(Game1.GameManager.SideBlurRenderTargetHeight * 0.5f) - (int)(Game1.RenderHeight / (Camera.Scale * 2)) % 2);

                spriteBatch.Draw(blurRT2, drawPos,
                    new Rectangle(0, 0, blurRT2.Width, blurRT2.Height), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                spriteBatch.End();
            }
            catch (Exception ex)
            {
                // Log and attempt to recover: restore to default render target
                System.Diagnostics.Debug.WriteLine($"DrawBlur exception: {ex}");
                try
                {
                    // ensure no render target left set
                    Game1.Graphics.GraphicsDevice.SetRenderTarget(null);
                }
                catch { }
            }
        }

        public void DrawLight(SpriteBatch spriteBatch)
        {
            Color lighting = GameSettings.GlobalLights ? CurrentMap.LightColor : new Color(255, 255, 255);
            Game1.Graphics.GraphicsDevice.Clear(lighting);

            var sampler = Game1.GraphicsCapabilities.UseAnisotropicFiltering
                ? SamplerState.AnisotropicClamp
                : SamplerState.LinearClamp;

            spriteBatch.Begin(SpriteSortMode.Deferred, LightBlendState, sampler, null, null, null, Camera.TransformMatrix);
            CurrentMap.Objects.DrawLight(spriteBatch);
            spriteBatch.End();
        }

        public void ReloadMap()
        {
            // @Hack
            var tempTm = CurrentMap;
            CurrentMap = NextMap;
            NextMap = tempTm;
            NextMap.Objects.ReloadObjects();
            NextMap.Objects.SpawnObject(ObjLink);

            // reset the hole map
            SaveLoadMap.CreateEmptyHoleMap(NextMap.HoleMap, NextMap.MapWidth, NextMap.MapHeight);

            ObjLink.MapInit();

            tempTm = CurrentMap;
            CurrentMap = NextMap;
            NextMap = tempTm;

            CurrentMap.Objects.TriggerKeyChange();
        }

        public void FinishLoadingMap(Map map)
        {
            ObjLink.Map.Objects.RemoveObject(ObjLink);
            ObjLink.FinishLoadingMap(map);
            map.Objects.SpawnObject(ObjLink);
            map.Objects.TriggerKeyChange();
        }
    }
}
