using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjLeverStone : GameObject
    {
        private readonly CBox _box;

        private readonly Vector2 _startPosition;
        private readonly Vector2 _endPosition;

        public ObjLeverStone() : base("movestone_0") { }

        public ObjLeverStone(Map.Map map, int posX, int posY, int direction) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _startPosition = new Vector2(posX, posY);
            _endPosition = new Vector2(posX, posY) + AnimationHelper.DirectionOffset[direction] * 16;

            _box = new CBox(EntityPosition, 0, 0, 0, 16, 16, 32);

            // does not deal damage in the real game
            var damageBox = new CBox(EntityPosition, 1, 1, 0, 14, 14, 30);
            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(damageBox, HitType.Object, 2));
            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(_box, Values.CollisionTypes.Normal));
            AddComponent(MovingSolidComponent.Index, new MovingSolidComponent(_box));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawSpriteComponent("movestone_0", EntityPosition, Vector2.Zero, Values.LayerBottom));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            EntityPosition.Set(Vector2.Lerp(_startPosition, _endPosition, ObjPullLever.LeverState));
        }
    }
}
