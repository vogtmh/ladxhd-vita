using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyBloober : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;

        private readonly Vector2 _startPosition;

        private int _lives = EnemyLives.Bloober;
        private int _dropIndex = 2;

        public EnemyBloober() : base("bloober") { }

        public EnemyBloober(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _startPosition = EntityPosition.Position;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/bloober");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -6, -13, 12, 10, 8)
            {
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.NPCWall,
                Gravity2DWater = 0.035f,
                FieldRectangle = map.GetField(posX, posY),
                DeepWaterOffset = -9
            };

            var stateUp = new AiState(UpdateUp);
            stateUp.Trigger.Add(new AiTriggerRandomTime(ToMoveDown, 650, 750));
            var stateDown = new AiState(UpdateDown);
            stateDown.Trigger.Add(new AiTriggerRandomTime(ToMoveUp, 550, 650));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("moveUp", stateUp);
            _aiComponent.States.Add("moveDown", stateDown);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { HitMultiplierX = 2.0f, HitMultiplierY = 2.0f, FlameOffset = new Point(0, 3), OnBurn = OnBurn };

            ToMoveUp();

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -14, 0, 14, 12, 8);
            
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, _damageState.OnHit) { BoomerangMultiplier = true });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
        }

        private void ToMoveUp()
        {
            var playerDistance = MapManager.ObjLink.Position - EntityPosition.Position;

            // move towards the start position or the player depending on the distance to the player
            if (playerDistance.Length() < 80)
                MoveTowardPosition(MapManager.ObjLink.Position, 0.45f);
            else
                MoveTowardPosition(_startPosition, 0.25f);
        }

        private void MoveTowardPosition(Vector2 position, float speed)
        {
            // do not change to move up state if the player is below the enemy
            if (EntityPosition.Y - 6 < position.Y &&
                (_body.LastVelocityCollision & Values.BodyCollision.Bottom) == 0)
            {
                _aiComponent.ChangeState("moveDown");
                return;
            }

            _aiComponent.ChangeState("moveUp");
            _animator.Play("up");

            // move towards the player
            var dir = position.X < EntityPosition.X ? -1 : 1;
            _body.VelocityTarget.X = dir * speed;
        }

        private void UpdateUp()
        {
            _body.DisableVelocityTargetMultiplier = true;

            // swim up if in deep water
            if (_body.CurrentFieldState.HasFlag(MapStates.FieldStates.DeepWater))
                _body.Velocity.Y = -0.65f;
            else
            {
                ToMoveDown();
            }
        }

        private void ToMoveDown()
        {
            _aiComponent.ChangeState("moveDown");
            _animator.Play("down");
            _body.Velocity.X = _body.VelocityTarget.X;
            _body.VelocityTarget.X = 0;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
        }

        private void UpdateDown()
        {

        }

        private void OnCollision(Values.BodyCollision collision)
        {

        }
    }
}
