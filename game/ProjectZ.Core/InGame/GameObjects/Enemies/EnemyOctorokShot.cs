using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyOctorokShot : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly BodyComponent _body;
        private readonly CBox _damageCollider;
        private readonly CSprite _drawComponent;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushableComponent;
        private readonly ShadowBodyDrawComponent _shadowBody;

        private float _lifeCounter = 950;
        private float _despawnPercentage = 1;
        private int _direction;
        private int _despawnTime = 750;
        private bool _repelledPlayer;
        private bool _playSound;
        private bool _reflected;

        public EnemyOctorokShot(Map.Map map, float posX, float posY, Vector2 velocity, int direction) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;
            ShieldDeflect = true;

            EntityPosition = new CPosition(posX, posY, 2);
            EntitySize = new Rectangle(-5, -12, 10, 12);
            CanReset = true;
            OnReset = Reset;

            // abort spawn in a wall
            var box = Box.Empty;
            if (Map.Objects.Collision(new Box(EntityPosition.X - 4, EntityPosition.Y - 8, 0, 8, 8, 8),
                Box.Empty, Values.CollisionTypes.Normal, 0, 0, ref box))
            {
                IsDead = true;
                return;
            }

            var animator = AnimatorSaveLoad.LoadAnimator("Enemies/octorok shot");
            animator.Play("idle");

            _drawComponent = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(animator, _drawComponent, new Vector2(-5, -10));

            _body = new BodyComponent(EntityPosition, -4, -8, 8, 8, 8)
            {
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                MoveCollision = OnCollision,
                FieldRectangle = map.GetField(posX, posY),
                VelocityTarget = velocity,
                Bounciness = 0.35f,
                Drag = 0.75f,
                IgnoreHeight = true,
                IgnoresZ = true,
                IgnoreInsideCollision = false,
            };

            var stateIdle = new AiState(UpdateIdle);
            var stateDespawn = new AiState() { Init = InitDespawn };
            stateDespawn.Trigger.Add(new AiTriggerCountdown(_despawnTime, Despawn, () => Despawn(0)));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("despawn", stateDespawn);
            _aiComponent.ChangeState("idle");

            _direction = direction;
            _damageCollider = new CBox(EntityPosition, -5, -10, 0, 10, 10, 4);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_damageCollider, HitType.Projectile, 2) { OnDamage = OnDamage, Direction = _direction });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(PushableComponent.Index, _pushableComponent = new PushableComponent(_body.BodyBox, OnPush) { RepelMultiplier = 0.2f });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _drawComponent, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, _shadowBody = new ShadowBodyDrawComponent(EntityPosition));

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadows");
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public override void Reset()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        private void InitDespawn()
        {
            _pushableComponent.IsActive = false;
            _body.IgnoresZ = false;
            _damageField.IsActive = false;

            if (_playSound)
                Game1.AudioManager.PlaySoundEffect("D360-07-07");
        }

        private void UpdateIdle()
        {
            _lifeCounter -= Game1.DeltaTime;
            if (_lifeCounter < 0)
            {
                _body.IsGrounded = false;
                _body.IgnoresZ = false;
                _body.Gravity = -0.125f;
                _body.Bounciness = 0.75f;
                _body.Drag = 0.9f;
                _body.Velocity = new Vector3(_body.VelocityTarget.X, _body.VelocityTarget.Y, 0);
                _body.VelocityTarget = Vector2.Zero;
                _aiComponent.ChangeState("despawn");
            }
            // If the shot was reflected, try to hit an enemy.
            if (_reflected)
            {
                // Probably the closest parallel to player damage types is the Bow.
                var collision = Map.Objects.Hit(MapManager.ObjLink, EntityPosition.Position, _damageCollider.Box, HitType.Bow, 2, false, false);
                if ((collision & Values.HitCollision.Enemy) != 0)
                    Map.Objects.DeleteObjects.Add(this);
            }
        }

        private void Despawn(double time)
        {
            _despawnPercentage = (float)(time / (_despawnTime / 3));
            if (_despawnPercentage > 1)
                _despawnPercentage = 1;

            _drawComponent.Color = Color.White * _despawnPercentage;
            _shadowBody.Transparency = _despawnPercentage;

            if (time <= 0)
                Map.Objects.DeleteObjects.Add(this);
        }

        private bool OnDamage()
        {
            // Get whether or not the player recieved damage.
            bool damaged = _damageField.DamagePlayer();

            // If the player was not damaged, check if it should be reflected.
            if (MapManager.ObjLink.Reflected(_direction, new bool[]{!damaged, !_reflected}))
            {
                Reflect();
            }
            // Whether the player was damaged or it was blocked, destroy the shot.
            else
            {
                _aiComponent.ChangeState("despawn");
                _body.Velocity = new Vector3(-_body.VelocityTarget.X * 0.25f, -_body.VelocityTarget.Y * 0.25f, 1.5f);
                _body.VelocityTarget = Vector2.Zero;
            }
            // Return the damage state for the damage field component.
            return damaged;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (_aiComponent.CurrentStateId != "despawn")
                _aiComponent.ChangeState("despawn");
            else if (_repelledPlayer)
                return false;
            else
            {
                // it is possible that we despawn because of OnDamage in the same frame
                // we need to make sure to still repell the player
                _repelledPlayer = true;
                return _repelledPlayer;
            }

            _body.Velocity = new Vector3(direction.X * 0.25f, direction.Y * 0.25f, 1.5f);
            _body.VelocityTarget = Vector2.Zero;

            return true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            bool swordBlock = GameSettings.SwMissileBlock 
                ? (hitType & HitType.Sword) != 0 && (hitType & HitType.SwordHold) == 0 
                : false;

            if (_aiComponent.CurrentStateId != "despawn" && swordBlock)
                _aiComponent.ChangeState("despawn");
            else
                return Values.HitCollision.None;
            
            _body.Velocity = new Vector3(direction.X, direction.Y, 1.5f);
            _body.VelocityTarget = Vector2.Zero;

            return Values.HitCollision.None;
        }

        private void Reflect()
        {
            // Don't let the spear reflect more than once.
            _reflected = true;

            // It should not damage Link from this point on.
            _hitComponent.IsActive = false;
            _damageField.IsActive = false;

            // Reverse direction and reset the shots lifespan.
            var newVelocity = -_body.VelocityTarget * 1.35f;
            _lifeCounter = 950;

            // Reverse the movement of the projectile.
            _body.VelocityTarget = newVelocity;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            _playSound = true;

            if (direction == Values.BodyCollision.Floor)
                return;

            if (_aiComponent.CurrentStateId != "despawn")
                _aiComponent.ChangeState("despawn");

            _body.Velocity = new Vector3(-_body.VelocityTarget.X * 0.25f, -_body.VelocityTarget.Y * 0.25f, 1.5f);
            _body.VelocityTarget = Vector2.Zero;
        }
    }
}
