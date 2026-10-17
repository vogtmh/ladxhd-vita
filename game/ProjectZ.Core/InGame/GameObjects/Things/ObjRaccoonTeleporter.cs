using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjRaccoonTeleporter : GameObject
    {
        private readonly int _posY;
        private readonly int _offsetX;
        private readonly int _offsetY;

        private float _teleportTime;
        private float _teleportCount;
        private float _fadeTime;

        private bool _isTeleporting;
        private int _direction;
        private int _mode;
        private int _extraYOffset;

        public ObjRaccoonTeleporter() : base("editor teleporter")
        {
            EditorColor = Color.Green * 0.5f;
        }

        // Mode 0: Raccoon Teleport
        // Mode 1: Dungeon 6 Teleport

        public ObjRaccoonTeleporter(Map.Map map, int posX, int posY, int offsetX, int offsetY, int width, int height, int mode) : base(map)
        {
            _posY = posY;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _mode = mode;

            AddComponent(ObjectCollisionComponent.Index, new ObjectCollisionComponent(new Rectangle(posX, posY, width, height), OnCollision));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        private void Update()
        {
            // If a teleport is not taking place then skip the update.
            if (!_isTeleporting)
                return;

            // Modern Camera: Freeze the player until the teleport finishes.
            if (!Camera.ClassicMode)
                MapManager.ObjLink.FreezePlayer();

            // Increment the teleport count. Multiply by direction to flip to negative when timer is satisfied.
            _teleportCount += Game1.DeltaTime * _direction;

            // The timer has reached its end.
            if (_teleportCount >= _teleportTime)
            {
                // Match the count to the total time and flip the direction.
                _teleportCount = _teleportTime;
                _direction = -1;

                // Teleport Link to the new position.
                var LinkPosX = MapManager.ObjLink.PosX + (_offsetX * Values.TileSize);
                var LinkPosY = MapManager.ObjLink.PosY + (_offsetY * Values.TileSize) - _extraYOffset;
                var newPosition = new Vector2(LinkPosX, LinkPosY);
                MapManager.ObjLink.SetPosition(newPosition);
            }
            // If the direction and the count flipped to negative.
            if (_direction < 0 && _teleportCount <= 0)
            {
                // End the teleport and set the count to zero for the effect.
                _isTeleporting = false;
                _teleportCount = 0;

                // Classic Camera: Give Link a slight push so the screen always scrolls.
                if (Camera.ClassicMode)
                    MapManager.ObjLink.Body.Velocity.Y += -0.85f;
            }

            // Modern Camera: Smooth out the transition effect during teleport.
            if (!Camera.ClassicMode)
            {
                var transitionSystem = (MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)];
                transitionSystem.SetColorMode(_mode == 0 ? Color.White : Color.Black, MathHelper.Clamp(_teleportCount / _fadeTime, 0, 1), false);
            }
        }

        private void OnCollision(GameObject gameObject)
        {
            // If the teleport is already taking effect then exit.
            if (_isTeleporting)
                return;

            // Used when "Classic Scrolling" is disabled. Since it triggers a field change earlier, we need to adjust
            // the point at which the camera shifts the screen. Classic Scrolling triggers about 7 pixels sooner. 
            var triggerThreshold = _posY + 21;

            // Do not scroll the camera until the threshold is exceeded.
            if (!GameSettings.ClassicScrolling || !Camera.ClassicMode)
            {
                if (MapManager.ObjLink.CenterPosition.Y > triggerThreshold)
                    return;
            }
            // Set the initial values.
            _extraYOffset = Camera.ClassicMode ? 4 : 0;
            _teleportTime = Camera.ClassicMode ? 0 : 300;
            _fadeTime = (_mode == 0) ? 200 : 250;
            _direction = 1;
            _isTeleporting = true;

            // For the raccoon teleport, play a sound effect and reset the warning message.
            if (_mode == 0)
            {
                Game1.AudioManager.PlaySoundEffect("D360-30-1E");
                Game1.GameManager.SaveManager.SetString("raccoon_warning", "0");
            }
        }
    }
}
