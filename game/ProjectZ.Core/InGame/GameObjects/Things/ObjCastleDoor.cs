using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjCastleDoor : GameObject
    {
        public ObjCastleDoor() : base("castle_door") { }

        public ObjCastleDoor(Map.Map map, int posX, int posY, string saveKey) : base(map)
        {
            var sprite = Resources.GetSprite("castle_door");

            EntityPosition = new CPosition(posX + sprite.Origin.X, posY + sprite.Origin.Y, 0);
            EntitySize = new Rectangle(-(int)sprite.Origin.X, -(int)sprite.Origin.Y, sprite.SourceRectangle.Width, sprite.SourceRectangle.Height);

            // Don't spawn the door if the player pushed the button inside to open the gate.
            if (saveKey != null && Game1.GameManager.SaveManager.GetString(saveKey) == "1")
            {
                IsDead = true;
                return;
            }
            var cSprite = new CSprite(sprite, EntityPosition);
            var collisionRect = new Rectangle(-(int)sprite.Origin.X, -(int)sprite.Origin.Y + 16, 48, 16);
            var carriableRect = new Rectangle(collisionRect.X + 1, collisionRect.Y + 1, collisionRect.Width - 2, collisionRect.Height - 2);
            var collisionBox = new CBox(EntityPosition, collisionRect.X, collisionRect.Y, 0, collisionRect.Width, collisionRect.Height, 16);

            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(collisionBox, Values.CollisionTypes.Normal));
            AddComponent(CarriableComponent.Index, new CarriableComponent(new CRectangle(EntityPosition, carriableRect), null, null, null) { IsCollision = true });
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(cSprite, Values.LayerPlayer));
        }
    }
}
