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
    internal class EnemyBeetle : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiFallState _aiFallState;
        private readonly AiTriggerRandomTime _directionChangeCounter;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private float _walkSpeed = 0.5f;
        private int _direction;
        private int _lives = EnemyLives.Beetle;
        private int _dropIndex = 2;

        private bool _finishedSpawning;

        public EnemyBeetle() : base("beetle") { }

        public EnemyBeetle(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 14, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 14, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/beetle");
            _animator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -11));

            _body = new BodyComponent(EntityPosition, -5, -8, 10, 8, 8)
            {
                AbsorbPercentage = 0.9f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Enemy |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Player,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall,
                HoleOnPull = OnHolePull,
                AbsorbStop = 0,
                FieldRectangle = map.GetField(posX, posY),
                Bounciness = 0.25f,
                Drag = 0.85f,
                MoveCollision = OnMoveCollision
            };

            var stateMoving = new AiState();
            stateMoving.Trigger.Add(_directionChangeCounter = new AiTriggerRandomTime(ChangeDirection, 500, 750));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("moving", stateMoving);

            // AiFallState sets the HoleAbsorb function
            _aiFallState = new AiFallState(_aiComponent, _body, OnHoleAbsorb, null);
            _body.HoleAbsorb = OnHoleAbsorb;

            _damageState  = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex) { OnBurn = OnBurn };
            
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            playerDirection.Normalize();
            _body.VelocityTarget = playerDirection * _walkSpeed;

            _aiComponent.ChangeState("moving");

            var damageBox = new CBox(EntityPosition, -2, -6, 0, 4, 4, 4);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite) { Height = 1.0f, Rotation = 0.1f });
        }

        public override void Reset()
        {
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
            Map.Objects.DeleteObjects.Add(this);
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void OnMoveCollision(Values.BodyCollision collision)
        {
            _directionChangeCounter.CurrentTime *= 0.5f;
            _body.VelocityTarget = Vector2.Zero;
        }

        private void OnHolePull(Vector2 direction, float percentage)
        {
            if (!_finishedSpawning)
            {
                _body.HoleAbsorption *= 0.25f;
                _body.SpeedMultiply = 1.0f;

                if (percentage == 0)
                {
                    _finishedSpawning = true;
                    ChangeDirection();
                }
            }
        }

        private void OnHoleAbsorb()
        {
            if (_finishedSpawning)
            {
                _animator.SpeedMultiplier = 2f;
                _aiFallState.OnHoleAbsorb();
            }
            else
            {
                _body.HoleAbsorption *= 0.25f;
                _body.SpeedMultiply = 1.0f;
            }
        }

        private void ChangeDirection()
        {
            // random start position/state
            _direction = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * _walkSpeed;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            _finishedSpawning = true;

            return true;
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // make sure to fall into the hole after getting burned while coming out
            _finishedSpawning = true;

            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }
    }
}
