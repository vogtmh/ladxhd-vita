using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    class MBossStoneHinoxStone : GameObject
    {
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly MBossStoneHinox _owner;

        private readonly int _centerX;
        private readonly int _spawnY;
        
        private int _collisionCount;

        public MBossStoneHinoxStone(Map.Map map, MBossStoneHinox owner, Vector3 position, Vector3 direction, int centerX) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            _owner = owner;

            EntityPosition = new CPosition(position.X, position.Y, position.Z);
            EntitySize = new Rectangle(-8, -32, 16, 32);

            _centerX = centerX;
            _spawnY = (int)EntityPosition.Position.Y;
            _sprite = new CSprite("hinox stone", EntityPosition, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -6, -12, 12, 12, 8)
            {
                FieldRectangle = owner.Body.FieldRectangle,
                FieldLocked = false,
                MoveCollision = MoveCollision,
                CollisionTypes = Values.CollisionTypes.None,
                Gravity = -0.1f,
                DragAir = 1.0f,
                Bounciness = 0.85f
            };
            _body.Velocity = direction;

            // random start sprite effect
            _sprite.SpriteEffect = (SpriteEffects)Game1.RandomNumber.Next(0, 4);

            var hitBox = new CBox(EntityPosition, -7, -14, 0, 14, 14, 8, true);
            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(hitBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, new HittableComponent(hitBox, OnHit));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowSpriteComponent(_sprite, Resources.SprShadow, EntityPosition, new Rectangle(0, 0, 65, 66), new Vector2(-6, -6), 12, 6));

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void MoveCollision(Values.BodyCollision collisionType)
        {
            _collisionCount++;

            if ((collisionType & Values.BodyCollision.Floor) != 0)
                Game1.AudioManager.PlaySoundEffect("D360-32-20");

            if (_collisionCount > 3 || EntityPosition.Y > _spawnY + Values.FieldHeight - 32)
            {
                _owner.HinoxStones.Remove(this);
                Map.Objects.DeleteObjects.Add(this);
                return;
            }
            // set a new random direction
            _body.Velocity.X = -1 + Game1.RandomNumber.Next(0, 100) / 50f;

            // make sure that we do not get to far away from the center of the room
            if (MathF.Abs(EntityPosition.X - _centerX) > 64)
                _body.Velocity.X = MathF.Sign(_centerX - EntityPosition.X);

            _body.Velocity.Z = Game1.RandomNumber.Next(75, 125) / 50f;

            // flip the sprite
            _sprite.SpriteEffect ^= SpriteEffects.FlipHorizontally;
        }

        public void DestroyStone()
        {
            var explosionAnimation = new ObjAnimator(Map, (int)EntityPosition.X-8, (int)EntityPosition.Y-26, Values.LayerTop, "Particles/spawn", "run", true);
            Map.Objects.SpawnObject(explosionAnimation);
            Map.Objects.DeleteObjects.Add(this);
            _owner.HinoxStones.Remove(this);
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            return Values.HitCollision.RepellingParticle;
        }
    }
}
