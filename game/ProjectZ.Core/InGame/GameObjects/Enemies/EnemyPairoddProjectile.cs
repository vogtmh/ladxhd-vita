using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyPairoddProjectile : GameObject
    {
        private CSprite _sprite;
        private DamageFieldComponent _damageField;

        public EnemyPairoddProjectile(Map.Map map, Vector2 position, float speed) : base(map)
        {
            Tags = Values.GameObjectTag.Damage;

            EntityPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(-7, -7, 14, 14);
            CanReset = true;
            OnReset = Reset;

            var animator = AnimatorSaveLoad.LoadAnimator("Enemies/pairodd projectile");
            animator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(animator, _sprite, Vector2.Zero);

            var body = new BodyComponent(EntityPosition, -2, -2, 4, 4, 8)
            {
                IgnoresZ = true,
                IgnoreHoles = true,
                FieldRectangle = map.GetField(position.X, position.Y),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                MoveCollision = OnCollision
            };

            var velocity = MapManager.ObjLink.Position - EntityPosition.Position;
            if (velocity != Vector2.Zero)
                velocity.Normalize();
            body.VelocityTarget = velocity * speed;

            var damageCollider = new CBox(EntityPosition, -3, -3, 0, 6, 6, 4);

            AddComponent(PushableComponent.Index, new PushableComponent(body.BodyBox, OnPush));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 4) { OnDamagedPlayer = DamagedPlayer });
            AddComponent(BodyComponent.Index, body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
        }

        public override void Reset()
        {
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
            Map.Objects.DeleteObjects.Add(this);
        }

        private void DamagedPlayer()
        {
            Despawn();
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            Despawn();

            return true;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            Despawn();
        }

        private void Despawn()
        {
            // spawn despawn effect
            var animation = new ObjSparkingEffect(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 0, 0);
            Map.Objects.SpawnObject(animation);
            Map.Objects.DeleteObjects.Add(this);
        }
    }
}
