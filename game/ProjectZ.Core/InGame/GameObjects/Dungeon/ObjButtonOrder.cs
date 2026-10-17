using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjButtonOrder : GameObject
    {
        private readonly CSprite _sprite;
        private readonly Rectangle _effectSourceRectangle = new Rectangle(66, 258, 12, 12);
        private readonly Box _collisionBox;

        private readonly string _strStateKey;
        private readonly string _strKey;
        private readonly int _index;

        private float _effectCounter;
        private bool _isActive;
        private bool _wasColliding;

        public ObjButtonOrder(Map.Map map, int posX, int posY, int index, string strStateKey, string strKey, bool drawSprite) : base(map, "button")
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _index = index;
            _strStateKey = strStateKey;
            _strKey = strKey;

            var animator = AnimatorSaveLoad.LoadAnimator("Particles/buttonOrder");
            animator.Play("idle");

            if (drawSprite)
            {
                _sprite = new CSprite(EntityPosition);
                var animationComponent = new AnimationComponent(animator, _sprite, new Vector2(8, 8));
                AddComponent(BaseAnimationComponent.Index, animationComponent);
            }
            _collisionBox = new Box(posX + 4, posY + 4, 0, 8, 8, 1);

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerBottom, EntityPosition));
        }

        private void Update()
        {
            // Decrement the counter if it's above 0.
            if (_effectCounter > 0)
                _effectCounter -= Game1.DeltaTime;
            else
                _effectCounter = 0;

            // Is link colliding with the button order tile.
            var isColliding = MapManager.ObjLink.Body.BodyBox.Box.Intersects(_collisionBox);

            // Check if Link is colliding with a tile.
            if (isColliding && !_wasColliding)
            {
                // The tile must be the currently active tile.
                if (_isActive)
                {
                    // The effect is played while the counter is above 0.
                    _effectCounter = 375;

                    // Each time a tile is stepped on "_strStateKey" is incremented by adding 1 to index.
                    Game1.GameManager.SaveManager.SetString(_strStateKey, (_index + 1).ToString());

                    // When the final tile is stepped on "_strKey" is set to "1".
                    if (!string.IsNullOrEmpty(_strKey))
                        Game1.GameManager.SaveManager.SetString(_strKey, "1");

                    // Play the sound effect when stepping on the correct tile.
                    Game1.AudioManager.PlaySoundEffect("D360-19-13");
                }
                // The tile is not the currently active tile.
                else
                {
                    // Reset the puzzle if the tile is not the last tile which has "_strStateKey".
                    if (!string.IsNullOrEmpty(_strStateKey))
                        Game1.GameManager.SaveManager.SetString(_strStateKey, "0");
                }
            }
            // Makes sure the tile does not instantly reactivate which breaks the order.
            _wasColliding = isColliding;
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            if (_isActive && _sprite != null)
                _sprite.Draw(spriteBatch);

            // Effect gets played after pressing the button and setting the timer.
            if (_effectCounter > 0)
            {
                var radian = _effectCounter / 300 * MathF.PI;
                var offset = new Vector2(-MathF.Sin(radian), MathF.Cos(radian));

                var pos0 = new Vector2(EntityPosition.X + 8 - 6, EntityPosition.Y + 8 - 6) + offset * 14;
                spriteBatch.Draw(Resources.SprItem, pos0, _effectSourceRectangle, Color.White);

                var pos1 = new Vector2(EntityPosition.X + 8 - 6, EntityPosition.Y + 8 - 6) - offset * 14;
                spriteBatch.Draw(Resources.SprItem, pos1, _effectSourceRectangle, Color.White);
            }
        }

        private void KeyChanged()
        {
            if (!string.IsNullOrEmpty(_strStateKey))
            {
                var state = Game1.GameManager.SaveManager.GetString(_strStateKey);
                _isActive = state == _index.ToString();
            }
        }
    }
}
