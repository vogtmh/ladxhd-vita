using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjTracy : GameObject
    {
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;

        private float _lookUpdateCounter;

        public ObjTracy() : base("tracy") { }

        public ObjTracy(Map.Map map, int posX, int posY) : base(map)
        {
            // Tracy will retain her "in-between" state between messages even when leaving the
            // building and after reloading a save file, so clear upon entering the building.
            Game1.GameManager.SaveManager.SetString("npc_tracy", "0");

            // Even kills > 28 rupees
            // Odd kills  > 42 rupees
            var price = Game1.GameManager.KillCount % 2 == 0 ? 0 : 1;

            // Bought 8 potions > 7 rupees
            if (Game1.GameManager.SaveManager.GetInt("npc_tracy", 0) == 8)
                price = 2;

            // The actual price is set in "scripts.zScript".
            Game1.GameManager.SaveManager.SetString("npc_tracy_price", price.ToString());

            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/npc_tracy");
            _animator.Play("idle");

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 34);

            _sprite = new CSprite(EntityPosition);
            _body = new BodyComponent(EntityPosition, -8, -11, 15, 11, 8);

            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);
            var interactBox = new CBox(EntityPosition, -8, -11, 15, 29, 8);

            AddComponent(BodyComponent.Index, _body);
            AddComponent(CollisionComponent.Index, new BodyCollisionComponent(_body, Values.CollisionTypes.Normal | Values.CollisionTypes.NPC));
            AddComponent(InteractComponent.Index, new InteractComponent(interactBox, Interact));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite));
        }

        private void Update()
        {
            // Don't update the fake tracy.
            if (!_sprite.IsVisible) 
                return;

            _lookUpdateCounter += Game1.DeltaTime;
            if (_lookUpdateCounter > 250)
            {
                _lookUpdateCounter = 0;

                // look at the player
                var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
                if (Math.Abs(playerDirection.X) > Math.Abs(playerDirection.Y))
                {
                    if (playerDirection.X < 0)
                        _animator.Play("left");
                    else
                        _animator.Play("right");
                }
                else
                {
                    _animator.Play("idle");
                }
            }
        }

        private bool Interact()
        {
            Game1.GameManager.StartDialogPath("npc_tracy");
            return true;
        }
    }
}
