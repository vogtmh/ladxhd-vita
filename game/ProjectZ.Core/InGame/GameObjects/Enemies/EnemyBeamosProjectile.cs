using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyBeamosProjectile : GameObject
    {
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly CBox _damageCollider;
        private readonly DamageFieldComponent _damageField;
        private readonly PushableComponent _pushComponent;

        private EnemyBeamos _host;

        private bool _isFirstProjectile;
        private bool _reflected;

        public EnemyBeamosProjectile(Map.Map map, EnemyBeamos host, Vector2 position, Vector2 velocityTarget, bool isFirstProjectile) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(-3, -3, 6, 6);
            CanReset = false;

            _host = host;
            _isFirstProjectile = isFirstProjectile;
            _sprite = new CSprite("beamos projectile", EntityPosition, new Vector2(-2, -2));

            _body = new BodyComponent(EntityPosition, -1, -1, 2, 2, 8)
            {
                IgnoresZ = true,
                IgnoreHoles = true,
                Level = 1,
                SimpleMovement = true,
                VelocityTarget = velocityTarget,
                MoveCollision = OnCollision,
                FieldRectangle = map.GetField(position.X, position.Y),
                CollisionTypes = Values.CollisionTypes.Normal
            };
            _damageCollider = new CBox(EntityPosition, -2, -2, 0, 4, 4, 4);

            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_damageCollider, OnPush));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_damageCollider, HitType.Enemy, 4) { OnDamage = OnDamage });;
            AddComponent(BodyComponent.Index, _body);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }
        private void Update()
        {
            // If the shot was reflected, try to hit an enemy.
            if (_reflected)
            {
                var collision = Map.Objects.Hit(MapManager.ObjLink, EntityPosition.Position, _damageCollider.Box, HitType.Bomb, 4, false, false);
                if ((collision & Values.HitCollision.Enemy) != 0)
                    DeleteProjectile(false);
            }
        }
        private bool OnDamage()
        {
            // Don't show the spark if it hits Link falling down a hole.
            bool didDamage = _damageField.DamagePlayer();
            DeleteProjectile(didDamage);
            return didDamage;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            DeleteProjectile(true);
        }

        public void Neutralize()
        {
            _damageField.IsActive = false;
            _sprite.IsVisible = false;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            // Store whether or not the player has the Mirror Shield.
            bool hasMirrorShield = Game1.GameManager.ShieldLevel == 2;

            // Check if the incoming push type is from the shield.
            if (type == PushableComponent.PushType.Impact && hasMirrorShield)
            {
                // We only want a single interaction so check if it's been reflected.
                if (!_reflected)
                {
                    // If the shield is able to reflect the shot.
                    if (MapManager.ObjLink.Reflected())
                        Reflect(direction);

                    // Otherwise kill it and show the sparking effect if Mirror Shield.
                    else
                        DeleteProjectile(hasMirrorShield);
                }
            }
            // Always return false. Whether it's reflected or deflected the beam is deleted.
            return false;
        }

        private void Reflect(Vector2 shieldDirection)
        {
            // Play the deflection sound.
            Game1.AudioManager.PlaySoundEffect("D360-22-16");

            // Don't let the spear reflect more than once.
            _reflected = true;

            // It should not damage Link from this point on.
            _damageField.IsActive = false;
            _pushComponent.IsActive = false;

            // Use the incoming direction and the shield reflect direction to determine new direction.
            shieldDirection.Normalize();
            var incoming = _body.VelocityTarget;
            var reflected = (incoming - 2 * Vector2.Dot(incoming, shieldDirection) * shieldDirection) * 1.75f;

            // Reverse the movement of the projectile.
            _body.VelocityTarget = reflected;
        }

        public void DeleteProjectile(bool showParticle)
        {
            // Spawn particles unless link is falling down a hole. Only spawn it on the first projectile.
            if (_isFirstProjectile)
            {
                if (showParticle)
                {
                    var animation = new ObjAnimator(Map, 0, 0, Values.LayerTop, "Particles/despawnParticle", "idle", true);
                    animation.EntityPosition.Set(EntityPosition.Position + _body.VelocityTarget * Game1.TimeMultiplier);
                    Map.Objects.SpawnObject(animation);
                }
            }
            // Remove the projectile from the projectile list and from the game.
            _host._projectiles.Remove(this);
            Map.Objects.DeleteObjects.Add(this);
        }
    }
}
