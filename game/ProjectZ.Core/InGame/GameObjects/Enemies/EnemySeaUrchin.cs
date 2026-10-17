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
    internal class EnemySeaUrchin : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private BodyCollisionComponent _collisionComponent;

        private readonly float _moveSpeed = 0.25f;
        private readonly int _collisionDamage = 2;

        private Vector2 _lastPosition;

        private float _soundCounter;
        private bool _dealsDamage = true;
        private int _lives = EnemyLives.SeaUrchin;
        private int _dropIndex = 2;

        public EnemySeaUrchin() : base("sea urchin") { }

        public EnemySeaUrchin(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;
            BowWowImmune = true;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _body = new BodyComponent(EntityPosition, -8, -16, 16, 16, 8)
            {
                Bounciness = 0.25f,
                Drag = 0.85f,
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Player,
                AbsorbPercentage = 0.75f
            };
            var sprite = new CSprite(EntityPosition);
            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/sea urchin");
            _animator.Play("idle");

            // randomize the start frame
            _animator.SetFrame(Game1.RandomNumber.Next(0, _animator.CurrentAnimation.Frames.Length));

            var animatorComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", new AiState());
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };
            _aiComponent.ChangeState("idle");

            var hittableBox = new CBox(EntityPosition, -8, -16, 0, 16, 16, 8, true);

            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animatorComponent);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(CollisionComponent.Index, _collisionComponent = new BodyCollisionComponent(_body, Values.CollisionTypes.Enemy));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush) { CooldownTime = 0 });
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite) { Height = 1.0f, Rotation = 0.1f });
        }

        public override void Reset()
        {
            _animator.Continue();
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _dealsDamage = true;
            _lastPosition = ResetPosition.Position;
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");

            if (_collisionComponent == null)
                AddComponent(CollisionComponent.Index, _collisionComponent = new BodyCollisionComponent(_body, Values.CollisionTypes.Enemy));
            else
                _collisionComponent.IsActive = true;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
            _dealsDamage = false;

            RemoveComponent(CollisionComponent.Index);
            _collisionComponent = null;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type != PushableComponent.PushType.Continues)
                return false;

            // push the enemy away if the player is holding a shield in the push direction
            if ((MapManager.ObjLink.IsBlockingState()) &&
                AnimationHelper.GetDirection(direction) == MapManager.ObjLink.Direction)
            {
                _body.Velocity = new Vector3(direction.X, direction.Y, 0) * _moveSpeed;

                // play sound effect
                if (_lastPosition != EntityPosition.Position)
                {
                    _soundCounter -= Game1.DeltaTime;
                    if (_soundCounter < 0)
                    {
                        Game1.AudioManager.PlaySoundEffect("D360-62-3E", false);
                        _soundCounter += 75;
                    }
                }
                _lastPosition = EntityPosition.Position;

                return true;
            }
            if (_dealsDamage)
                MapManager.ObjLink.HitPlayer(-direction * 2, HitType.Enemy, _collisionDamage, false);

            return false;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                if (_collisionComponent != null)
                    _collisionComponent.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }
    }
}
