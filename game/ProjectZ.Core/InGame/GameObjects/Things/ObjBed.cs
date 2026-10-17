using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.GameObjects.Things
{
    public class ObjBed : GameObject
    {
        private readonly string _nextMap;
        private readonly string _lampKey;

        private bool _startBed;
        private bool _startTransition;

        private const int TransitionTime = 2750;
        private float _transitionCounter = TransitionTime;
        private int _lightState;

        public ObjBed() : base("editor bed") { }

        public ObjBed(Map.Map map, int posX, int posY, string nextMap, string lampKey) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 32);

            _nextMap = nextMap;
            _lampKey = lampKey;

            var boxPushable = new CBox(EntityPosition, 0, 0, 16, 32 - MapManager.ObjLink.BodyRectangle.Height, 8);

            AddComponent(PushableComponent.Index, new PushableComponent(boxPushable, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        private bool IsStateValid()
        {
            return !(MapManager.ObjLink.IsAttackingState() || 
                    MapManager.ObjLink.IsChargingState() || 
                    MapManager.ObjLink.IsBlockingState() || 
                    MapManager.ObjLink.IsJumpingState());
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            // Check if the state is valid and the transition hasn't already started.
            if (IsStateValid() & !_startBed)
            {
                // Jump into the bed and start the transition.
                _startBed = true;
                Game1.AudioManager.SetMusic(29, 1);
                MapManager.ObjLink.StartRailJump(new Vector2(EntityPosition.X + 8, EntityPosition.Y + 21), 1, 1);
                MapManager.ObjLink.StartBedTransition();
            }
            return false;
        }

        public void Update()
        {
            if (!_startBed || _startTransition)
                return;

            Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;

            _transitionCounter -= Game1.DeltaTime;

            // Shut off the lights one by one in a sequence.
            if (_lightState < 4 && _transitionCounter < TransitionTime - 1000 - _lightState * 250)
            {
                _lightState++;
                Game1.GameManager.SaveManager.SetString(_lampKey + _lightState, "0");
            }

            // When the transition counter ends fade into the dream map.
            if (_transitionCounter < 0)
            {
                _startTransition = true;

                MapManager.ObjLink.MapTransitionStart = MapManager.ObjLink.Position;
                MapManager.ObjLink.MapTransitionEnd = MapManager.ObjLink.Position;

                var transitionSystem = ((MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)]);
                transitionSystem.AppendMapChange(_nextMap, "bed");
                transitionSystem.StartDreamTransition = true;
            }
        }
    }
}
