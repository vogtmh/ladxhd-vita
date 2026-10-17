using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyWaterTektite : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerRandomTime _movementTimer;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private float _currentSpeed;
        private int _currentDir;
        private int _lives = EnemyLives.WaterTektite;
        private int _dropIndex = 2;

        public static Vector2[] Directions =
        {
            new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1)
        };

        public EnemyWaterTektite() : base("water tektite") { }

        public EnemyWaterTektite(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/water tektite");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -7, -14, 14, 12, 8)
            {
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy |
                                 Values.CollisionTypes.Player,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NonWater |
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                Bounciness = 0.25f,
                AbsorbPercentage = 0.75f,
                Drag = 0.85f,
                IgnoreHeight = true,
            };

            var damageBox   = new CBox(EntityPosition, -3, -10, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 0, 14, 14, 8);

            var stateMoving = new AiState(UpdateMoving);
            stateMoving.Trigger.Add(_movementTimer = new AiTriggerRandomTime(ToStop, 400, 750));
            var stateWaiting = new AiState();
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(ToMoving, 300, 500));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("moving", stateMoving);
            _aiComponent.States.Add("waiting", stateWaiting);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer) { WaterOutline = false });
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));

            ToMoving();
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            ToMoving();
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            ToStop();
        }

        private void ToMoving()
        {
            _aiComponent.ChangeState("moving");

            // TODO: should not be a constant velocity
            _currentDir = Game1.RandomNumber.Next(0, 4);
        }

        private void UpdateMoving()
        {
            // speed up or slow down
            if (_movementTimer.CurrentTime > 100)
            {
                _currentSpeed += 0.05f * Game1.TimeMultiplier;
                if (_currentSpeed > 0.75f)
                    _currentSpeed = 0.75f;
            }
            else
            {
                _currentSpeed -= 0.05f * Game1.TimeMultiplier;
                if (_currentSpeed < 0)
                    _currentSpeed = 0;
            }

            _body.VelocityTarget = Directions[_currentDir] * _currentSpeed;
        }

        private void ToStop()
        {
            _currentSpeed = 0;
            _body.VelocityTarget = Vector2.Zero;

            if (_aiComponent.CurrentStateId == "moving")
                _aiComponent.ChangeState("waiting");
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
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
