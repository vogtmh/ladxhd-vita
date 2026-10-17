using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjFisherman : GameObject
    {
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly string _personId = "npc_fisherman";

        private float _talkCount;
        private bool _isTransitioning;

        public ObjFisherman() : base("fisherman") { }

        public ObjFisherman(Map.Map map, int posX, int posY) : base(map)
        {
            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/" + _personId);
            _animator.Play("stand");

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(0, 0));

            Game1.GameManager.SaveManager.SetString("enterPond", "no");

            _body = new BodyComponent(EntityPosition, -8, -11, 15, 11, 8);

            AddComponent(BodyComponent.Index, _body);
            AddComponent(CollisionComponent.Index, new BodyCollisionComponent(_body, Values.CollisionTypes.Normal | Values.CollisionTypes.NPC));
            AddComponent(InteractComponent.Index, new InteractComponent(_body.BodyBox, Interact));
            AddComponent(AnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));
        }

        private void Update()
        {
            if (_talkCount > 0)
                _talkCount -= Game1.DeltaTime;
            else
                _animator.Play("stand");
        }

        private bool Interact()
        {
            _animator.Play("talk");
            _talkCount = 250;

            Game1.GameManager.StartDialogPath(_personId);
            return true;
        }

        private void KeyChanged()
        {
            var Link = MapManager.ObjLink;

            if (_isTransitioning || Game1.GameManager.SaveManager.GetString("enterPond") != "yes") return;

            _isTransitioning = true;

            Link.MapTransitionStart = Link.Position;
            Link.MapTransitionEnd = Link.Position;
            Link.TransitionOutWalking = false;

            // append a map change
            ((MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)]).AppendMapChange(
                "pond.map", "entry", true, false, Values.MapTransitionColor, false);
        }
    }
}
