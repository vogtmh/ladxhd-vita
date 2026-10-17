using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjTrendy : GameObject
    {
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly InteractComponent _interactComponent;

        private bool _grabbed;
        private bool _fallen;
        private bool _endDialog;

        public ObjTrendy() : base("person") { }

        public ObjTrendy(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/npc_trendy");
            _animator.Play("stand_0");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -4, -8, 8, 8, 8)
            {
                RestAdditionalMovement = false,
                Gravity = -0.15f,
                Bounciness = 0.5f,
                MoveCollision = OnMoveCollision
            };
            var box = new CBox(EntityPosition, -7, -14, 14, 14, 8);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(box, Values.CollisionTypes.Enemy | Values.CollisionTypes.PushIgnore | Values.CollisionTypes.NPC));
            AddComponent(InteractComponent.Index, _interactComponent = new InteractComponent(box, Interact));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer) { WaterOutline = false });
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));

            // Reset this value when entering the building so NPC doesn't get stuck.
            Game1.GameManager.SaveManager.SetString("trendy_npc", "0");
        }

        private void Update()
        {
            // go grabbed?
            if (!_grabbed && _body.IgnoresZ)
            {
                _grabbed = true;
                _animator.Play("grabbed");
            }
            // let go by the grabber
            if (_grabbed && !_body.IgnoresZ)
            {
                _fallen = true;
                _animator.Play("fall");
            }
            // ending dialog?
            if (_fallen && !_endDialog && _body.AdditionalMovementVT == Vector2.Zero && EntityPosition.Z == 0 && _body.Velocity.Z == 0)
            {
                _endDialog = true;
                Game1.GameManager.StartDialogPath("trendy_marin_end");
            }
            if (_grabbed)
                MapManager.ObjLink.FreezePlayer();
        }

        private void OnKeyChange()
        {
            // Disable the interact component when the game starts.
            bool npcActive = Game1.GameManager.SaveManager.GetString("trendy_npc", "0") == "0";
            _interactComponent.IsActive = npcActive;

            // Remove the string when no longer needed.
            if (npcActive)
                Game1.GameManager.SaveManager.RemoveString("trendy_npc");
        }

        private void OnMoveCollision(Values.BodyCollision collision)
        {
            if ((collision & Values.BodyCollision.Floor) != 0)
                Game1.AudioManager.PlaySoundEffect("D360-09-09");
        }

        private bool Interact()
        {
            Game1.GameManager.StartDialogPath("npc_trendy");
            return true;
        }
    }
}
