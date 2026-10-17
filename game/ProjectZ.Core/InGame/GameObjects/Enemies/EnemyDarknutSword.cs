using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyDarknutSword : GameObject
    {
        private readonly Animator _animator;
        private readonly CBox _damageBox;
        private readonly CBox _collisionBox;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly EnemyDarknut _owner;

        private double _lastHitTime;
        private int _direction;

        public Animator Animator => _animator;
        public CSprite Sprite => _sprite;
        public DamageFieldComponent DamageField => _damageField;
        public HittableComponent HitComponent => _hitComponent;
        public PushableComponent PushComponent => _pushComponent;

        public EnemyDarknutSword(Map.Map map, EnemyDarknut owner) : base(map)
        {
            _owner = owner;
            _owner.EntityPosition.AddPositionListener(typeof(EnemyDarknutSword), PositionChange);

            _direction = _owner.Direction;

            EntityPosition = new CPosition(owner.EntityPosition.X, owner.EntityPosition.Y - 1, owner.EntityPosition.Z);
            EntitySize = new Rectangle(-22, -8 - 24, 44, 48);
            CanReset = false;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/darknut sword");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(Animator, Sprite, new Vector2(-8, -15));

            _damageBox = new CBox(0, 0, 0, 0, 0, 4);
            _collisionBox = new CBox(0, 0, 0, 0, 0, 4);

            UpdateBoxes();

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_collisionBox, OnHit));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_damageBox, OnPush) { RepelParticle = true });
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(Sprite, Values.LayerPlayer));
        }

        private void PositionChange(CPosition position)
        {
            EntityPosition.Set(new Vector2(position.X, position.Y - position.Z - 1));
        }

        private void Update()
        {
            // Get the facing direction of the Darknut.
            _direction = _owner.Direction;

            // Update the damage and collision boxes.
            UpdateBoxes();
        }

        private void UpdateBoxes()
        {
            // Update damage boxes to match sprite position.
            _damageBox.Box.X = EntityPosition.X - 8 + Animator.CollisionRectangle.X;
            _damageBox.Box.Y = EntityPosition.Y - 15 + Animator.CollisionRectangle.Y;
            _damageBox.Box.Width = Animator.CollisionRectangle.Width;
            _damageBox.Box.Height = Animator.CollisionRectangle.Height;

            // Set the initial collision sizes.
            int collisionX = (int)_damageBox.Box.X;
            int collisionY = (int)_damageBox.Box.Y;
            int collisionW = (int)_damageBox.Box.Width;
            int collisionH = (int)_damageBox.Box.Height;

            // Calculate collision based on facing direction.
            switch (_direction)
            {
                case 0: { collisionW = (int)_damageBox.Box.Width * 4 / 6; collisionX += (int)_damageBox.Box.Width - collisionW; break; }
                case 1: { collisionH = (int)_damageBox.Box.Height * 4 / 6; collisionY += (int)_damageBox.Box.Height - collisionH; break; }
                case 2: { collisionW = (int)_damageBox.Box.Width * 4 / 6; break; }
                case 3: { collisionH = (int)_damageBox.Box.Height * 4 / 6; break; }
            }
            // Apply the collision to the collision box.
            _collisionBox.Box.X = collisionX;
            _collisionBox.Box.Y = collisionY;
            _collisionBox.Box.Width = collisionW;
            _collisionBox.Box.Height = collisionH;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
            {
                _owner.Body.Velocity.X = direction.X * 2.5f;
                _owner.Body.Velocity.Y = direction.Y * 2.5f;
            }
            return true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (hitType == HitType.MagicRod || hitType == HitType.MagicPowder || hitType == HitType.Bow || hitType == HitType.Hookshot || hitType == HitType.Boomerang ||
                (_lastHitTime != 0 && Game1.TotalGameTime - _lastHitTime < 250))
                return Values.HitCollision.None;

            _lastHitTime = Game1.TotalGameTime;

            _owner.Body.Velocity.X = direction.X * 1.5f;
            _owner.Body.Velocity.Y = direction.Y * 1.5f;

            return Values.HitCollision.RepellingParticle | Values.HitCollision.Repelling2;
        }
    }
}
