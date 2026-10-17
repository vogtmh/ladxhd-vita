using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;
using System;

namespace ProjectZ.InGame.GameObjects.Things
{
    class ObjCompassSound : GameObject
    {
        private Rectangle _roomRectangle;
        private Vector2 _position;
        private string _key;
        private bool _isTriggered;

        public ObjCompassSound() : base("editor compass sound") { }

        public ObjCompassSound(Map.Map map, int posX, int posY, string key) : base(map)
        {
            _roomRectangle = map.GetField(posX, posY);
            var center = _roomRectangle.Center;
            _position = new Vector2(center.X, center.Y);
            _key = key;

            if (string.IsNullOrEmpty(key) ||
                Game1.GameManager.SaveManager.GetString(_key, "0") == "1")
            {
                IsDead = true;
                return;
            }
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));
        }

        private void Update()
        {
            // If the sound effect has not yet been triggered.
            if (!_isTriggered)
            {
                // If the player walked into the room.
                if (_roomRectangle.Contains(MapManager.ObjLink.CenterPosition))
                {
                    _isTriggered = true;

                    // Play the sound effect if the player has the compass.
                    var hasCompass = Game1.GameManager.GetItem("compass") != null;
                    if (hasCompass)
                        Game1.AudioManager.PlaySoundEffect("D370-27-1B");
                }
            }
            // Reset so the sound effect can play again.
            else
            {
                // Classic Camera: Reset when leaving the field.
                if (Camera.ClassicMode)
                {
                    if (MapManager.ObjLink.FieldTransition(_roomRectangle))
                        _isTriggered = false;
                }
                // Modern Camera: Reset when the player is far enough away.
                else
                {
                    var distance = _position - MapManager.ObjLink.Position;
                    if (MathF.Abs(distance.X) > Values.FieldWidth * 0.8f ||
                        MathF.Abs(distance.Y) > 128 * 0.8f)
                        _isTriggered = false;
                }
            }
        }

        private void KeyChanged()
        {
            // Delete the object if the item was collected.
            var keyState = Game1.GameManager.SaveManager.GetString(_key, "0");
            if (keyState == "1")
                Map.Objects.DeleteObjects.Add(this);
        }
    }
}
