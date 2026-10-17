using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;
using ProjectZ.InGame.GameObjects.Effects;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjDungeonOneWay : GameObject
    {
        private readonly ObjAnimator _animatorTop;
        private readonly ObjAnimator _animatorBottom;

        private Vector2 _startPosition;
        private Vector2 _endPosition;

        private bool _isRotation;
        private float _revealTimer;

        public ObjDungeonOneWay() : base("dungeonOneWay") { }

        public ObjDungeonOneWay(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);

            _animatorTop = new ObjAnimator(map, posX, posY, Values.LayerBottom, "Objects/dOneWay", "idle2", false);
            Map.Objects.SpawnObject(_animatorTop);

            _animatorBottom = new ObjAnimator(map, posX, posY + 16, Values.LayerBottom, "Objects/dOneWay", "idle", false);
            Map.Objects.SpawnObject(_animatorBottom);

            var pushBox = new CBox(posX + 6, posY + 16, 0, 4, 16, 16);
            AddComponent(PushableComponent.Index, new PushableComponent(pushBox, OnPush) { InertiaTime = 50 });

            var collisionBox = new CBox(posX, posY, 0, 16, 32, 16);
            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(collisionBox, Values.CollisionTypes.Normal));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type != PushableComponent.PushType.Continues || MapManager.ObjLink.Direction != 1)
                return false;

            // If Link jumped through the door, then reset him back to the ground.
            MapManager.ObjLink.EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y, 0));
            MapManager.ObjLink.Body.Velocity = Vector3.Zero;
            MapManager.ObjLink.Body.VelocityTarget = Vector2.Zero;
            MapManager.ObjLink.Body.IsGrounded = true;

            // Start spinning the door.
            StartRotation();
            return true;
        }

        private void Update()
        {
            // Hold Link hidden briefly after the flip so he does not pop in the moment it lands.
            if (_revealTimer > 0)
            {
                MapManager.ObjLink.FreezePlayer();
                _revealTimer -= Game1.DeltaTime;
                if (_revealTimer <= 0)
                    MapManager.ObjLink.IsVisible = true;
            }

            if (!_isRotation)
                return;

            MapManager.ObjLink.CanWalk = false;

            if (_animatorBottom.Animator.CurrentFrameIndex < 4)
            {
                var frameTime = _animatorBottom.Animator.GetAnimationTime(0, _animatorBottom.Animator.CurrentFrameIndex) +
                                _animatorBottom.Animator.FrameCounter;
                var maxTime = _animatorBottom.Animator.GetAnimationTime(0, 4);

                var state = (float)frameTime / maxTime;
                var currentPosition = Vector2.Lerp(_startPosition, _endPosition, state);
                MapManager.ObjLink.SetPosition(currentPosition);
            }

            if (_animatorBottom.Animator.CurrentFrameIndex >= 4)
            {
                _isRotation = false;
                MapManager.ObjLink.SetPosition(_endPosition);
                MapManager.ObjLink.PositionControlled = false;
                _revealTimer = 400;
            }
        }

        private void StartRotation()
        {
            // hide the player
            MapManager.ObjLink.IsVisible = false;

            // This object lerps Link to the far side, so nothing else may move him.
            MapManager.ObjLink.PositionControlled = true;

            Game1.AudioManager.PlaySoundEffect("D360-12-0C");

            _isRotation = true;

            // the player will be moved between these two points while transitioning to the other side
            _startPosition = new Vector2(MapManager.ObjLink.EntityPosition.X, MapManager.ObjLink.EntityPosition.Y);
            _endPosition = new Vector2(
                EntityPosition.X + 8,
                EntityPosition.Y - MapManager.ObjLink.Body.OffsetY - MapManager.ObjLink.Body.Height);

            _animatorTop.Animator.Play("rotate2");
            _animatorBottom.Animator.Play("rotate");
        }
    }
}
