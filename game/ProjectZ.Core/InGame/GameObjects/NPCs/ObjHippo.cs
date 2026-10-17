using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    class ObjHippo : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly Animator _animator;

        private int _direction = -1;

        public ObjHippo() : base("hippo") { }

        public ObjHippo(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-12, -24, 24, 32);

            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/npc_hippo", GameSettings.Uncensored);
            _animator.Play("stand_" + _direction);

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            var body = new BodyComponent(EntityPosition, -9, -13, 18, 12, 8);

            _aiComponent = new AiComponent();
            var stateStand = new AiState(UpdateStanding);
            var stateEmbarassed = new AiState(UpdateEmbarassed);

            _aiComponent.States.Add("stand", stateStand);
            _aiComponent.States.Add("embarassed", stateEmbarassed);
            _aiComponent.ChangeState("stand");

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, body);
            AddComponent(CollisionComponent.Index, new BodyCollisionComponent(body, Values.CollisionTypes.Normal | Values.CollisionTypes.PushIgnore | Values.CollisionTypes.NPC));
            AddComponent(InteractComponent.Index, new InteractComponent(new CBox(EntityPosition, -9, -5, 18, 5, 8), Interact));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(body, sprite));
        }

        private void UpdateStanding()
        {
            var box_bashful = new RectangleF(EntityPosition.X - 16, EntityPosition.Y + 12, 64, 16);
            if (MapManager.ObjLink.BodyRectangle.Intersects(box_bashful))
                _aiComponent.ChangeState("embarassed");
        }

        private void UpdateEmbarassed()
        {
            var box_turnAround = new RectangleF(EntityPosition.X + _direction * 14 - 4, EntityPosition.Y - 14, 8, 18);
            if (MapManager.ObjLink.BodyRectangle.Intersects(box_turnAround))
                _direction = -_direction;

            _animator.Play("idle_" + _direction);
        }

        private bool Interact()
        {
            Game1.GameManager.StartDialogPath("npc_hippo_line");
            return true;
        }
    }
}
