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
    internal class EnemyOctorok : GameObject, IHasVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly Vector2[] _shotOffset =
        {
            new Vector2(-8, -1),new Vector2(0, -6),
            new Vector2(8, -1),new Vector2(0, 11)
        };

        private float _walkSpeed = 0.5f;
        private float _shotCooldown = 2000;
        private int _direction;
        private int _lives = EnemyLives.Octorok;
        private int _dropIndex = 2;

        public bool IsVisible { get; internal set; }

        public EnemyOctorok() : base("octorok") { }

        public EnemyOctorok(Map.Map map, int posX, int posY) : base(map)
        {
            IsVisible = true;
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 12, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 12, 0);
            EntitySize = new Rectangle(-8, -15, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/octorok");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -15));

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8)
            {
                MoveCollision = OnCollision,
                AbsorbPercentage = 0.85f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy |
                                 Values.CollisionTypes.Player,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                Bounciness = 0.25f,
                Drag = 0.85f,
            };

            var walkingState = new AiState { Init = ToWalking };
            walkingState.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 750, 1000));
            var idleState = new AiState { Init = ToIdle };
            idleState.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walking"), 250, 500));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("walking", walkingState);
            _aiComponent.States.Add("idle", idleState);
            new AiFallState(_aiComponent, _body, OnHoleAbsorb, null);
            _aiComponent.ChangeState("walking");

            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 0, 14, 15, 8);
            var pushableBox = new CBox(EntityPosition, -7, -13, 0, 14, 13, 4);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite) { Height = 1.0f, Rotation = 0.1f });
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        private void Update()
        {
            if (_shotCooldown > 0)
                _shotCooldown -= Game1.DeltaTime;
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
            _animator.SpeedMultiplier = 1f;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        public override void Init()
        {
            base.Init();

            if (!IsActive)
            {
                IsVisible = false;
                return;
            }
            // random start position/state
            _direction = Game1.RandomNumber.Next(0, 4);
            _animator.Play("walk_" + _direction);
            _aiComponent.ChangeState(Game1.RandomNumber.Next(0, 2) == 0 ? "idle" : "walking");
        }

        private void ToIdle()
        {
            _animator.Play("stand_" + _direction);
            _body.VelocityTarget = new Vector2(0, 0);

            // shoot if the player is in the range and in the right direction
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection.Length() < 80)
            {
                if (playerDirection != Vector2.Zero)
                    playerDirection.Normalize();
                var direction = AnimationHelper.GetDirection(playerDirection);

                if (direction == _direction && _shotCooldown <= 0)
                {
                    // shoot
                    _shotCooldown = 2000;
                    var shot = new EnemyOctorokShot(Map,
                        EntityPosition.X + _shotOffset[_direction].X,
                        EntityPosition.Y + _shotOffset[_direction].Y,
                        AnimationHelper.DirectionOffset[_direction] * 2f,
                        _direction);
                    Map.Objects.SpawnObject(shot);
                }
            }
        }

        private void ToWalking()
        {
            // random new direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _animator.Play("walk_" + _direction);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * _walkSpeed;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            if (_aiComponent.CurrentStateId == "walking")
                _aiComponent.ChangeState("idle");
        }

        private void OnHoleAbsorb()
        {
            _animator.SpeedMultiplier = 3f;
            _animator.Play("walk_" + _direction);
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
