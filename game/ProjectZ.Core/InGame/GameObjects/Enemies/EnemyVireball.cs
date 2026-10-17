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
    internal class EnemyVireball : GameObject
    {
        private readonly BodyComponent _body;
        private readonly CBox _damageCollider;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private double _liveTime = 2500;
        private bool _reflected;

        public EnemyVireball(Map.Map map, Vector2 position, Vector2 velocity) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(-5, -5, 10, 10);
            CanReset = false;
            OnReset = Reset;

            var animator = AnimatorSaveLoad.LoadAnimator("Enemies/vireball");
            animator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -5, -5, 10, 10, 8)
            {
                FieldRectangle = map.GetField(position.X, position.Y),
                FieldLocked = false,
                IgnoresZ = true,
                IgnoreHoles = true,
                CollisionTypes = Values.CollisionTypes.None
            };

            _body.VelocityTarget = velocity;

            _damageCollider = new CBox(EntityPosition, -3, -3, 0, 6, 6, 4);
            var hittableBox = new CBox(EntityPosition, -4, -4, 0, 8, 8, 8);

            AddComponent(BodyComponent.Index, _body);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_damageCollider, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerTop));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public override void Reset()
        {
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
            Map.Objects.DeleteObjects.Add(this);
        }

        private void Update()
        {
            _liveTime -= Game1.DeltaTime;

            // fade out
            if (_liveTime <= 100)
                _sprite.Color = Color.White * ((float)_liveTime / 100f);

            if (_liveTime < 0)
                Delete();

            // If the shot was reflected, try to hit an enemy.
            if (_reflected)
            {
                // Probably the closest parallel to player damage types is the Bow.
                var collision = Map.Objects.Hit(MapManager.ObjLink, EntityPosition.Position, _damageCollider.Box, HitType.Bow, 2, false, false);
                if ((collision & Values.HitCollision.Enemy) != 0)
                    Map.Objects.DeleteObjects.Add(this);
            }
        }
        
        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            OnDeath(true);

            return Values.HitCollision.Enemy;
        }

        private bool OnPush(Vector2 vecDirection, PushableComponent.PushType type)
        {
            // Check if the incoming push type is from the shield.
            if (type == PushableComponent.PushType.Impact)
            {
                // We only want a single interaction so check if it's been reflected.
                if (!_reflected)
                {
                    // The direction must be an inversion of incoming damage.
                    int direction = AnimationHelper.GetDirection(vecDirection) + 2 % 4;

                    // If the shield is able to reflect the shot.
                    if (MapManager.ObjLink.Reflected(direction))
                    {
                        Reflect(vecDirection);
                        return false;
                    }
                    // Otherwise kill it.
                    else
                        OnDeath(false);
                }
            }
            // The shot was not reflected so perform the knockback.
            return true;
        }

        private void Reflect(Vector2 shieldDirection)
        {
            // Play the deflection sound.
            Game1.AudioManager.PlaySoundEffect("D360-22-16");

            // Don't let the spear reflect more than once.
            _reflected = true;

            // It should not damage Link from this point on.
            _hitComponent.IsActive = false;
            _damageField.IsActive = false;
            _pushComponent.IsActive = false;
            _liveTime = 2250;

            // Use the incoming direction and the shield reflect direction to determine new direction.
            shieldDirection.Normalize();
            var incoming = _body.VelocityTarget;
            var reflected = (incoming - 2 * Vector2.Dot(incoming, shieldDirection) * shieldDirection) * 1.75f;

            // Reverse the movement of the projectile.
            _body.VelocityTarget = reflected;
        }

        private void OnDeath(bool playSound)
        {
            if (playSound)
                Game1.AudioManager.PlaySoundEffect("D360-03-03");

            var splashAnimator = new ObjAnimator(Map, 0, 0, 0, 0, Values.LayerTop, "Particles/spawn", "run", true);
            splashAnimator.EntityPosition.Set(EntityPosition.Position - new Vector2(8, 8));
            Map.Objects.SpawnObject(splashAnimator);
            Delete();
        }

        private void Delete()
        {
            Map.Objects.DeleteObjects.Add(this);
        }
    }
}
