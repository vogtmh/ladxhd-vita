using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    internal class BossAnglerBarrier : GameObject
    {
        CSprite _sprite;
        private int _posX;
        private int _posY;

        public int PosX => _posX;
        public int PosY => _posY;

        public BossAnglerBarrier() : base("fish_barrier") { }

        public BossAnglerBarrier(Map.Map map, int posX, int posY, bool pipe) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _posX = posX;
            _posY = posY;

            var sprite = pipe 
                ? Resources.GetSprite("fish_pipe")
                : Resources.GetSprite("fish_barrier");

            _sprite = new CSprite(sprite, EntityPosition, Vector2.Zero);

            var collisionBox = new CBox(posX, posY, 0, 16, 16, 16);

            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(collisionBox, Values.CollisionTypes.Normal));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerBottom));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }
    }
}
