using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;
using System;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjFinalFountain : GameObject
    {
        private readonly Animator _animatorTop;
        private readonly Animator _animatorBottom;

        private Vector2 _position;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;

        private string _activationKey;
        private bool _isActive;

        private const int WobbleTime = 2500;
        private double _counter;

        private bool _moving;
        private bool _despawnStairs;

        public ObjFinalFountain() : base("final_fountain") { }

        public ObjFinalFountain(Map.Map map, int posX, int posY, string activationKey) : base(map)
        {
            _activationKey = activationKey;

            _animatorTop = AnimatorSaveLoad.LoadAnimator("Sequences/final fountain");
            _animatorTop.Play("top");
            _animatorBottom = AnimatorSaveLoad.LoadAnimator("Sequences/final fountain");
            _animatorBottom.Play("bottom");

            _startPosition = new Vector2(posX + 8, posY + 128);
            _targetPosition = new Vector2(posX + 8, posY + 8);
            _position = _startPosition;

            if (!string.IsNullOrEmpty(_activationKey))
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerBackground, new CPosition(0, 0, 0)));
        }

        private void OnKeyChange()
        {
            if (!_isActive && Game1.GameManager.SaveManager.GetString(_activationKey) == "1")
                Activate();
        }

        private void Activate()
        {
            _isActive = true;
            Game1.AudioManager.PlaySoundEffect("D378-52-34");
            if (GameSettings.ScreenShake)
                Game1.GameManager.ShakeScreen(WobbleTime, 1, 0, 6, 0);
            Map.CameraTarget = new Vector2(MapManager.ObjLink.PosX, MapManager.ObjLink.PosY);
        }

        private void Update()
        {
            if (!_isActive)
                return;

            // The Classic Camera centers on Link's field and ignores "CameraTarget", so lifting him out of
            // the field would start a field scroll and the "Classic Scrolling" push would drag him back down
            // to the bottom edge of the field above. Lock the camera for the duration of the rise.
            if (Camera.ClassicMode)
                Camera.LockCamera = true;

            // Increment the counter each update. 
            _counter += Game1.DeltaTime;

            // Start the fountain sequence part of the ending.
            if (_counter > WobbleTime)
            {
                // Despawn the platform and the final stairs.
                if (!_despawnStairs && _counter - WobbleTime > 150)
                {
                    _despawnStairs = true;
                    Game1.GameManager.SaveManager.SetString("despawn_stairs", "1");
                }
                // Move up to the player.
                if (_counter - WobbleTime < 250)
                {
                    var percentage = Math.Clamp(((float)_counter - WobbleTime) / 250, 0, 1);
                    _position = Vector2.Lerp(_startPosition, _targetPosition, percentage);
                }
                // Oscillate between up and down.
                else if (_counter - WobbleTime < 5000 - 250)
                {
                    var percentage = (float)((_counter - WobbleTime - 250) / 2000) * MathF.PI * 2;
                    _position = new Vector2(_targetPosition.X, _targetPosition.Y - MathF.Sin(percentage) * 3);
                }
                // Leave the screen.
                else
                {
                    _position.Y -= Game1.TimeMultiplier * MathHelper.Clamp((float)(_counter - WobbleTime - 250 - 4000) / 150, 0.125f, 16f);
                }
                // Release the camera lock before starting the final sequence.
                if (_counter - WobbleTime - 5000 - 250 > 1500)
                {
                    Camera.LockCamera = false;
                    Game1.GameManager.InGameOverlay.StartSequence("final");
                }
                // Move Link to the top of the fountain.
                if (_moving || _position.Y < MapManager.ObjLink.PosY + 36)
                {
                    MapManager.ObjLink.SetPosition(new Vector2(_position.X, _position.Y - 36));

                    if (!_moving)
                    {
                        _moving = true;
                        Game1.GameManager.SaveManager.SetString("final_move_background", "1");
                        Game1.GameManager.SaveManager.SetString("link_animation", "fountain");
                    }
                }
            }
            _animatorTop.Update();
            _animatorBottom.Update();
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            if (!_isActive)
                return;

            var fadePercentage = MathHelper.Clamp((float)(_counter - WobbleTime) / 100, 0, 1);

            _animatorTop.Draw(spriteBatch, _position, Color.White * fadePercentage);

            // Draw the bottom part up to the screen end.
            var cameraView = MapManager.Camera.GetGameView();
            var top = Math.Max(_position.Y, cameraView.Top);
            var bottomCount = (int)Math.Ceiling((cameraView.Bottom - top) / 16);
            for (var i = 0; i < bottomCount; i++)
            {
                var pos = new Vector2(_position.X, top + (bottomCount - i - 1) * 16);
                _animatorBottom.Draw(spriteBatch, pos, Color.White * fadePercentage);
            }
        }
    }
}
