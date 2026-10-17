using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjCactus : GameObject
    {
        private BoxCollisionComponent collisionComponent;

        private CBox _collisionBox;
        private CBox _collisionBoxBlocking;

        public ObjCactus() : base("cactus") { }

        public ObjCactus(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            var sprite = new CSprite("cactus", EntityPosition);

            var pushableBox  = new CBox(posX, posY, 0, 16, 16, 8);
            var damageBox    = new CBox(posX + 2, posY + 2, 0, 12, 14, 8);

            // Collision box adjusts whether shield is out or not.
            _collisionBox = new CBox(posX + 3, posY + 3, 0, 10, 12, 32);
            _collisionBoxBlocking = new CBox(posX, posY + 1, 0, 16, 15, 32);

            // Matches the size of the "normal" collision box. When switching to "Blocking" box, prevents Link from passing through during damage blink.
            map.Objects.SpawnObject(new ObjCollider(map, posX, posY, false, 32, new Rectangle(3, 3, 10, 12), Values.CollisionTypes.Normal, -1));

            AddComponent(CollisionComponent.Index, collisionComponent = new BoxCollisionComponent(_collisionBox, Values.CollisionTypes.Normal));
            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(damageBox, HitType.Enemy, 2) 
            {
                PushMultiplier = 0.85f,
                DashPushMultiplier = 2.30f,
                SingleAxisPush = true,
                OnDamagedPlayer = OnDamagedPlayer,
                OverrideCooldown = ObjLink.CooldownTime / 2
            });
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        private void Update()
        {
            // Get the player character.
            ObjLink Link = MapManager.ObjLink;

            // Swap the collision box size whether Link is blocking or not. If he is, the box is "bigger" preventing him
            // from getting close enough to take damage when dropping the shield. The permanent "ObjCollider" prevents
            // breaching the "small" collision when "big" collision is in effect and Link is in damage flash.
            collisionComponent.CollisionBox = Link.IsBlockingState() || (Link.BootsRunning && Link.CarryShield)
                ? _collisionBoxBlocking
                : _collisionBox;
        }

        private void OnDamagedPlayer()
        {
            // Get the player character.
            ObjLink Link = MapManager.ObjLink;

            // Adjust the knockback height based on whether or not boots are used.
            var height = Link.BootsRunning ? 1.25f : 1.00f;

            // Apply a slight "jump" to the knockback.
            Link.Body.Velocity = new Vector3(Link.Body.Velocity.X, Link.Body.Velocity.Y, height);
        }
    }
}
