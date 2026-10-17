using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemySpear : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _bodyDrawComponent;
        private readonly CBox _damageCollider;
        private readonly CSprite _drawComponent;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly ShadowBodyDrawComponent _shadowBody;

        private Vector2 _startPosition;

        private float _despawnPercentage = 1;
        private int _despawnTime = 500;
        private int _dir;
        private int _direction;
        private bool _playSound = true;
        private bool _reflected;

        private Point[] _collisionBoxSize = { new Point(12, 4), new Point(4, 12), new Point(12, 4), new Point(4, 12) };

        public EnemySpear(Map.Map map, Vector3 position, Vector2 velocity, int direction) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;
            ShieldDeflect = true;

            EntityPosition = new CPosition(position);
            EntitySize = new Rectangle(-8, -8, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _startPosition = EntityPosition.Position;

            _dir = AnimationHelper.GetDirection(velocity);
            _animator = AnimatorSaveLoad.LoadAnimator("Objects/spear");
            _animator.Play(_dir.ToString());

            _drawComponent = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _drawComponent, Vector2.Zero);

            _body = new BodyComponent(EntityPosition,
                -_collisionBoxSize[_dir].X / 2, -_collisionBoxSize[_dir].Y / 2,
                _collisionBoxSize[_dir].X, _collisionBoxSize[_dir].Y, 8)
            {
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                FieldRectangle = map.GetField(position.X, position.Y),
                MoveCollision = OnCollision,
                VelocityTarget = velocity,
                Bounciness = 0.35f,
                Drag = 0.75f,
                IgnoreHeight = true,
                IgnoresZ = true,
            };
            _damageCollider = new CBox(EntityPosition, -5, -5, 0, 10, 10, 4, true);
            var stateDespawn = new AiState() { Init = InitDespawn };
            stateDespawn.Trigger.Add(new AiTriggerCountdown(_despawnTime, TickDespawn, () => TickDespawn(0)));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", new AiState(UpdateIdle));
            _aiComponent.States.Add("despawn", stateDespawn);
            _aiComponent.ChangeState("idle");

            _direction = direction;

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_damageCollider, HitType.Projectile, 2) { OnDamage = OnDamage, Direction = _direction });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush) { RepelMultiplier = 0.2f });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, _bodyDrawComponent = new BodyDrawComponent(_body, _drawComponent, Values.LayerPlayer) { Grass = false });
            AddComponent(DrawShadowComponent.Index, _shadowBody = new ShadowBodyDrawComponent(EntityPosition));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public override void Reset()
        {
            _damageField.IsActive = false;
            _drawComponent.IsVisible = false;
            Map.Objects.DeleteObjects.Add(this);
        }

        public override void Init()
        {
            Game1.AudioManager.PlaySoundEffect("D378-10-0A");
        }

        private void UpdateIdle()
        {
            // With classic camera, simulate field barrier to end shots earlier.
            if (Camera.ClassicMode)
            {
                var curField = MapManager.ObjLink.CurrentField;
                var barrier = new Rectangle(curField.X - 16, curField.Y - 16, 192, 160);

                if (!curField.Contains(EntityPosition.Position) &&  
                    barrier.Contains(EntityPosition.Position))
                {
                    OnCollision(Values.BodyCollision.None);
                    return;
                }
            }
            // After travelling a certain distance, begin the spear's descent.
            var distance = _startPosition - EntityPosition.Position;
            if (MathF.Abs(distance.X) > 112 || Math.Abs(distance.Y) > 96)
                _body.IgnoresZ = false;

            // If the shot was reflected, try to hit an enemy.
            if (_reflected)
            {
                // Probably the closest parallel to player damage types is the Bow.
                var collision = Map.Objects.Hit(MapManager.ObjLink, EntityPosition.Position, _damageCollider.Box, HitType.Bow, 2, false, false);
                if ((collision & Values.HitCollision.Enemy) != 0)
                    Map.Objects.DeleteObjects.Add(this);
            }
        }

        private void InitDespawn()
        {
            _body.IgnoresZ = false;
            _damageField.IsActive = false;
            _bodyDrawComponent.Grass = true;

            _animator.Play("rotate");
            _animator.SetFrame((_dir + 1) % 4);

            if (_playSound)
                Game1.AudioManager.PlaySoundEffect("D360-07-07");
        }

        private void TickDespawn(double time)
        {
            _despawnPercentage = (float)(time / (_despawnTime / 2));
            if (_despawnPercentage > 1)
                _despawnPercentage = 1;

            _drawComponent.Color = Color.White * _despawnPercentage;
            _shadowBody.Transparency = _despawnPercentage;

            if (time <= 0)
                Map.Objects.DeleteObjects.Add(this);
        }

        private bool OnDamage()
        {
            // Don't play the "tink" sound if it hit Link or his shield.
            _playSound = false;

            // Get whether or not the player recieved damage.
            bool damaged = _damageField.DamagePlayer();

            // If player was damaged, delete the object.
            if (damaged)
                Map.Objects.DeleteObjects.Add(this);

            // If player was not damaged, it was blocked.
            else
            {
                // Mirror shield reflects the shot while normal shield just collides.
                if (MapManager.ObjLink.Reflected(_direction, new bool[]{!_reflected}))
                    Reflect();
                else
                    OnCollision(Values.BodyCollision.None);
            }
            // Return the damage state for the damage field component.
            return damaged;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (_despawnPercentage < 1)
                return false;

            if (_aiComponent.CurrentStateId != "despawn")
                _aiComponent.ChangeState("despawn");

            _body.Velocity = new Vector3(direction.X * 0.25f, direction.Y * 0.25f, 1.5f);
            _body.VelocityTarget = Vector2.Zero;

            return true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            bool swordBlock = GameSettings.SwMissileBlock 
                ? (hitType & HitType.Sword) == 0 && (hitType & HitType.SwordHold) != 0 
                : true;

            if (swordBlock)
            {
                return Values.HitCollision.None;
            }
            if (_despawnPercentage < 1)
                return Values.HitCollision.None;

            if (_aiComponent.CurrentStateId != "despawn")
                _aiComponent.ChangeState("despawn");

            _body.Velocity = new Vector3(direction.X, direction.Y, 1.5f);
            _body.VelocityTarget = Vector2.Zero;

            return Values.HitCollision.Enemy;
        }

        private void Reflect()
        {
            // Don't let the spear reflect more than once.
            _reflected = true;

            // It should not damage Link from this point on.
            _hitComponent.IsActive = false;
            _damageField.IsActive = false;

            // Reverse direction and set the start position to Link's position.
            var newVelocity = -_body.VelocityTarget * 1.35f;
            _startPosition = MapManager.ObjLink.Position;

            // Update travel direction + animation.
            _dir = AnimationHelper.GetDirection(newVelocity);
            _animator.Play(_dir.ToString());

            // Reverse the movement of the projectile.
            _body.VelocityTarget = newVelocity;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            // Return if it's already despawning.
            if (_aiComponent.CurrentStateId == "despawn")
                return;

            // Despawn the spear on impact.
            _aiComponent.ChangeState("despawn");
            
            // Play the deflected animation.
            if ((direction & Values.BodyCollision.Floor) != 0)
                _body.Velocity = new Vector3(_body.VelocityTarget.X * 0.75f, _body.VelocityTarget.Y * 0.75f, 1.5f);
            else
                _body.Velocity = new Vector3(-_body.VelocityTarget.X * 0.25f, -_body.VelocityTarget.Y * 0.25f, 1.5f);

            // Stop future velocity completely.
            _body.VelocityTarget = Vector2.Zero;
        }
    }
}
