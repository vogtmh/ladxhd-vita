using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyRock : GameObject
    {
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly Vector2 _spawnPosition;

        private double _liveTime = 125;

        public EnemyRock(Map.Map map, Vector2 position, Rectangle field) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(position.X, position.Y, Game1.RandomNumber.Next(8, 24));
            EntitySize = new Rectangle(-8, -40, 16, 48);
            CanReset = true;
            OnReset = Reset;

            _spawnPosition = EntityPosition.Position;

            var animator = AnimatorSaveLoad.LoadAnimator("Enemies/rock");
            animator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -5, -5, 10, 10, 8)
            {
                FieldRectangle = field,
                FieldLocked = false,
                Gravity = -0.125f,
                CollisionTypes = Values.CollisionTypes.None
            };

            var damageBox   = new CBox(EntityPosition, -3, -3, 0,  6,  6, 8, true);
            var hittableBox = new CBox(EntityPosition, -7, -7, 0, 14, 14, 8, true);

            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(PushableComponent.Index, new PushableComponent(damageBox, OnPush));
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerTop));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite) { ShadowWidth = 10, ShadowHeight = 6 });

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public override void Reset()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        private void Update()
        {
            // Do not update while the Classic Camera is scrolling.
            if (MapManager.Camera.FieldScrolling)
                return;

            // Despawn the rock when it reaches its destination.
            if (EntityPosition.Y > _spawnPosition.Y + 128)
            {
                _liveTime -= Game1.DeltaTime;

                // Fade it out of existence.
                _sprite.Color = Color.White * ((float)_liveTime / 125f);
                if (_liveTime < 0)
                    Map.Objects.DeleteObjects.Add(this);
            }

            // When it hits the ground, bounce in a random direction.
            if (_body.IsGrounded)
            {
                Game1.AudioManager.PlaySoundEffect("D360-32-20", true, EntityPosition.Position);

                _body.Velocity.Z = Game1.RandomNumber.Next(150, 250) / 100f;

                var length = Game1.RandomNumber.Next(75, 150) / 150f;
                var direction = (-100 + Game1.RandomNumber.Next(0, 200)) / 100f;
                _body.VelocityTarget = new Vector2(MathF.Sin(direction), MathF.Cos(direction)) * length;
            }
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            return Values.HitCollision.Enemy | Values.HitCollision.RepellingParticle;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType hitType)
        {
            return true;
        }
    }
}
