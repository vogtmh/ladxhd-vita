using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyRope : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private const float WalkSpeed = 0.5f;
        private const float RunSpeed = 1.0f;

        private int _animationDirection;
        private int _direction;
        private int _lives = EnemyLives.Rope;
        private int _dropIndex = 2;

        public EnemyRope() : base("rope") { }

        public EnemyRope(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/rope");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -6, -10, 12, 10, 8)
            {
                MoveCollision = OnCollision,
                AbsorbPercentage = 0.85f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.DeepWater,
                FieldRectangle = map.GetField(posX, posY),
                Drag = 0.85f,
            };

            var stateIdle = new AiState { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walk"), 250, 500));
            var stateWalk = new AiState(UpdateWalk) { Init = InitWalk };
            stateWalk.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 750, 1000));
            var stateRun = new AiState { Init = InitRun };
            stateRun.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 650, 750));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walk", stateWalk);
            _aiComponent.States.Add("run", stateRun);
            new AiFallState(_aiComponent, _body, OnHoleAbsorb, null);
            new AiDeepWaterState(_body);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            _aiComponent.ChangeState("walk");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -8, -15, 0, 16, 15, 8);
            var pushableBox = new CBox(EntityPosition, -7, -14, 0, 14, 14, 8);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite) { Height = 1.0f, Rotation = 0.1f });
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("walk");
            _aiComponent.ChangeState("walk");
            _animator.SpeedMultiplier = 1f;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void InitIdle()
        {
            _body.VelocityTarget = new Vector2(0, 0);
            _animator.Pause();
        }

        private void InitWalk()
        {
            // random new direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * WalkSpeed;

            SetAnimation(_direction);
        }

        private void SetAnimation(int moveDirection)
        {
            _animator.SpeedMultiplier = 1;

            // look to the left or to the right
            if (moveDirection == 0)
                _animationDirection = -1;
            else if (moveDirection == 2)
                _animationDirection = 1;

            _animator.Play("move_" + _animationDirection);
        }

        private void UpdateWalk()
        {
            // is the player on the same line horizontally or vertically?
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection.Length() < 64 &&
                (Math.Abs(playerDirection.X) < 4 || Math.Abs(playerDirection.Y) < 4))
            {
                if (playerDirection != Vector2.Zero)
                    playerDirection.Normalize();

                _direction = AnimationHelper.GetDirection(playerDirection);
                _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * RunSpeed;

                _aiComponent.ChangeState("run");
            }
        }

        private void InitRun()
        {
            SetAnimation(_direction);
            _animator.SpeedMultiplier = 2;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            if (_aiComponent.CurrentStateId == "walk")
                _aiComponent.ChangeState("idle");
        }

        private void OnHoleAbsorb()
        {
            _animator.SpeedMultiplier = 3f;
            _animator.Continue();
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
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
