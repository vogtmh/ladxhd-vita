using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjButtonPush : GameObject
    {
        private readonly Box _collisionBox;

        private int _pushDirection;
        private string _strKey;
        private int _activeValue;

        public ObjButtonPush(Map.Map map, int posX, int posY, string strKey, int activeValue, int pushDirection, int buttonWidth, int buttonHeight) : base(map)
        {
            SprEditorImage = Resources.SprWhite;
            EditorIconSource = new Rectangle(0, 0, 16, 16);
            EditorColor = Color.Blue * 0.5f;

            _pushDirection = pushDirection;
            _strKey = strKey;
            _activeValue = activeValue;

            _collisionBox = new Box(posX, posY, 0, buttonWidth, buttonHeight, 32);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        private void Update()
        {
            // Get the current value of the activate key.
            int currentValue = 0;
            if (!string.IsNullOrEmpty(_strKey))
            {
                string value = Game1.GameManager.SaveManager.GetString(_strKey);
                int.TryParse(value, out currentValue);
            }

            // The player walked into the push button collision box.
            if (currentValue == _activeValue && 
                _collisionBox.Intersects(MapManager.ObjLink.Body.BodyBox.Box))
            {
                // Shorten the reference.
                var Link = MapManager.ObjLink;

                // Set the velocity, direction, and force walking.
                var velocity = AnimationHelper.DirectionOffset[_pushDirection];
                Link.Body.VelocityTarget = velocity;
                Link.LinkWalking(true);
                Link.DisableInput = true;
            }
        }
    }
}
