using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyMoblinPigSword : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _drawComponent;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly EnemyMoblinPigSwordSword _sword;

        private const float MoveSpeed = 0.5f;
        private const float AttackMoveSpeed = 0.55f;
        private int _attackRange = 50;
        private int _followRange = 65;
        private float _statsResetTimer;

        private Rectangle _fieldRectangle;
        private bool _isActive = true;
        private int _direction;
        private int _lives = EnemyLives.MoblinPigSword;
        private int _dropIndex = 4;

        public BodyComponent Body => _body;
        public int Direction => _direction;
        public string AiState { get => _aiComponent.CurrentStateId; }
        public CBox HittableBox
        {
            get => _hitComponent.HittableBox;
            set => _hitComponent.HittableBox = value;
        }
        public override bool IsActive
        {
            set
            {
                _isActive = value;
                _sword.IsActive = value;
            }
            get => _isActive;
        }

        public EnemyMoblinPigSword() : base("moblinPigSword") { }

        public EnemyMoblinPigSword(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/moblinPig");
            _animator.Play("walk_1");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -16));

            _fieldRectangle = map.GetField(posX, posY);

            _body = new BodyComponent(EntityPosition, -7, -14, 14, 14, 8)
            {
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = _fieldRectangle,
                Bounciness = 0.25f,
                AbsorbPercentage = 0.75f,
                Drag = 0.85f
            };

            var stateIdle = new AiState { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerRandomTime(EndIdle, 300, 500));
            var stateWalk = new AiState { Init = InitWalking };
            stateWalk.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 550, 850));
            var stateAttack = new AiState(UpdateAttack);

            _aiComponent = new AiComponent();
            _aiComponent.Trigger.Add(new AiTriggerUpdate(UpdateDamageTick));
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walking", stateWalk);
            _aiComponent.States.Add("attack", stateAttack);
            new AiFallState(_aiComponent, _body, OnHoleAbsorb, OnAbsorbDeath);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex) { OnDeath = OnDeath, OnBurn = OnBurn };

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -3, -12, 0, 6, 8, 8);
            var pushableBox = new CBox(EntityPosition, -7, -11, 0, 14, 11, 4);

            _drawComponent = new BodyDrawComponent(_body, _sprite, Values.LayerPlayer);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            _sword = new EnemyMoblinPigSwordSword(Map, this);
        }

        public override void Reset()
        {
            _sword.Animator.Continue();
            _sword.DamageField.IsActive = true;
            _sword.HitComponent.IsActive = true;
            _sword.PushComponent.IsActive = true;

            _attackRange = 50;
            _followRange = 65;

            InitIdle();
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
            _damageState.CurrentLives = _lives;
        }

        private void Update()
        {
            // If Link pokes, alert the enemy of his presence.
            if (MapManager.ObjLink.IsPoking)
            {
                _attackRange = 200;
                _followRange = 200;
                _aiComponent.ChangeState("attack");
                _statsResetTimer = 2000;
            }

            // Reset the inflated attack and follow range after a time.
            if (_statsResetTimer > 0)
            {
                _statsResetTimer -= Game1.DeltaTime;
                if (_statsResetTimer <= 0)
                {
                    _statsResetTimer = 0;
                    _attackRange = 50;
                    _followRange = 65;
                }
            }
        }

        private void OnBurn()
        {
            _animator.Pause();
            _sword.Animator.Pause();
            _sword.DamageField.IsActive = false;
            _sword.HitComponent.IsActive = false;
            _sword.PushComponent.IsActive = false;
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        public override void Init()
        {
            // add the sword to the map
            Map.Objects.SpawnObject(_sword);

            // start randomly idle or walking facing a random direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _aiComponent.ChangeState(Game1.RandomNumber.Next(0, 2) == 0 ? "walking" : "idle");
        }

        private void UpdateDamageTick()
        {
            _sword.Sprite.SpriteShader = _sprite.SpriteShader;
        }

        private void OnDeath(bool pieceOfPower)
        {
            _damageState.BaseOnDeath(pieceOfPower);

            Map.Objects.DeleteObjects.Add(_sword);
        }

        private void InitIdle()
        {
            _body.VelocityTarget = Vector2.Zero;

            _animator.Play("stand_" + _direction);
            _sword.Animator.Play("stand_" + _direction);
        }

        private void EndIdle()
        {
            var distance = EntityPosition.Position - MapManager.ObjLink.Position;

            if (_fieldRectangle.Contains(MapManager.ObjLink.CenterPosition) && distance.Length() < _attackRange)
                _aiComponent.ChangeState("attack");
            else
                _aiComponent.ChangeState("walking");
        }

        private void InitWalking()
        {
            ChangeDirection();
        }

        private void ChangeDirection()
        {
            _animator.SpeedMultiplier = 0.5f;
            _sword.Animator.SpeedMultiplier = 0.5f;

            // random new direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _animator.Play("walk_" + _direction);
            _sword.Animator.Play("walk_" + _direction);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * MoveSpeed;
        }

        private void UpdateAttack()
        {
            var playerDirection = (MapManager.ObjLink.Position + AnimationHelper.DirectionOffset[_direction] * 3) - EntityPosition.Position;

            if (!_fieldRectangle.Contains(MapManager.ObjLink.CenterPosition) || playerDirection.Length() > _followRange)
            {
                _attackRange = 50;
                _followRange = 65;
                _aiComponent.ChangeState("idle");
                return;
            }

            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();

            _body.VelocityTarget = playerDirection * AttackMoveSpeed;

            UpdateDirection(playerDirection);

            _animator.SpeedMultiplier = 1f;
            _sword.Animator.SpeedMultiplier = 1f;
        }

        private void UpdateDirection(Vector2 direction)
        {
            _direction = AnimationHelper.GetDirection(direction);

            _animator.Play("walk_" + _direction);
            _sword.Animator.Play("walk_" + _direction);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            if (_aiComponent.CurrentStateId != "walking")
                return;

            // stop walking
            _aiComponent.ChangeState("idle");
        }

        private void OnHoleAbsorb()
        {
            _animator.Play("walk_" + _direction);
            _sword.Animator.Play("walk_" + _direction);

            _animator.SpeedMultiplier = 3f;
            _sword.Animator.SpeedMultiplier = 3f;
        }

        private void OnAbsorbDeath()
        {
            Map.Objects.DeleteObjects.Add(_sword);
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            if (_direction == 1)
                ((DrawComponent)_sword.Components[DrawComponent.Index]).Draw(spriteBatch);

            _drawComponent.Draw(spriteBatch);

            if (_direction != 1)
                ((DrawComponent)_sword.Components[DrawComponent.Index]).Draw(spriteBatch);
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
