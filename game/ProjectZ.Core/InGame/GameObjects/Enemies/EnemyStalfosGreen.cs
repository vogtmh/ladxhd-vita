using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Bosses;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyStalfosGreen : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AnimationComponent _animatorComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly Rectangle _fieldRectangle;

        private float _walkSpeed = 0.5f;
        private float _changeDirCount;
        private float _jumpDelay;

        private int _dir;
        private int _lives = EnemyLives.StalfosGreen;
        private int _dropIndex = 4;

        private GameObject _owner;

        public EnemyStalfosGreen() : base("stalfos green") { }

        public EnemyStalfosGreen(Map.Map map, int posX, int posY, GameObject owner = null) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -40, 16, 40);
            CanReset = true;
            OnReset = Reset;

            _owner = owner;
            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/stalfos green");
            _animator.Play("walk");

            var sprite = new CSprite(EntityPosition);
            _animatorComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _fieldRectangle = map.GetField(posX, posY);

            _body = new BodyComponent(EntityPosition, -6, -10, 11, 10, 8)
            {
                MoveCollision = OnCollision,
                Gravity = -0.075f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = _fieldRectangle
            };

            _aiComponent = new AiComponent();

            var stateWalking = new AiState(UpdateWalking);
            var stateMoveUp = new AiState(UpdateMoveUp);
            var stateWait = new AiState();
            stateWait.Trigger.Add(new AiTriggerCountdown(250, null, ToMoveDown));
            var stateMoveDown = new AiState(UpdateMoveDown);
            var stateWaitFloor = new AiState();
            stateWaitFloor.Trigger.Add(new AiTriggerCountdown(250, null, ToWalk));

            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("moveUp", stateMoveUp);
            _aiComponent.States.Add("wait", stateWait);
            _aiComponent.States.Add("moveDown", stateMoveDown);
            _aiComponent.States.Add("waitFloor", stateWaitFloor);
            new AiFallState(_aiComponent, _body, null, null, 300);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };
            _aiComponent.ChangeState("walking");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 2, 13, 15, 8, true);
            var pushableBox = new CBox(EntityPosition, -6, -14, 2, 12, 14, 4);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2) { OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, _animatorComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 10 });

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        private void OnDamagedPlayer()
        {
            // If it deals damage transfer to the main boss.
            if (_owner != null && _owner is BossHardhitBeetle owner)
                owner.OnDamagedPlayer();
        }

        public override void Reset()
        {
            _animator.Continue();
            _aiComponent.ChangeState("walking");
            _aiComponent.ChangeState("walking");
            _damageState.CurrentLives = EnemyLives.StalfosGreen;
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
        }

        private void OnBurn()
        {
            _body.Velocity = Vector3.Zero;
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        public void SetAirPosition(int posZ)
        {
            // Used in "BossHardhitBeetle" to set position of spawned stalfos.
            EntityPosition.SetZ(posZ);
            _animator.Play("jump");
            ToMoveDown();

            // Randomize the walk speed so that when two are spawned at
            // the same position they will not stay at the same position.
            _walkSpeed = Game1.RandomNumber.Next(45, 55) / 100f;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
            {
                _body.Velocity.X = direction.X * 2.25f;
                _body.Velocity.Y = direction.Y * 2.25f;
            }
            return true;
        }

        private void ToWalk()
        {
            _aiComponent.ChangeState("walking");
        }

        private void UpdateWalking()
        {
            _animator.Play("walk");

            var direction = MapManager.ObjLink.Position - EntityPosition.Position;
            var distance = direction.Length();

            if (_fieldRectangle.Contains(MapManager.ObjLink.CenterPosition) && distance < 56)
            {
                // Jump delay keeps from jumping again immediately and appear to slide.
                if (distance < 24 && _jumpDelay <= 0)
                    ToJumping();

                else if (distance < 56)
                {
                    // Move towards the player.
                    direction.Normalize();
                    _body.VelocityTarget = direction * _walkSpeed;
                }
            }
            else
            {
                _changeDirCount -= Game1.DeltaTime;

                // Change direction.
                if (_changeDirCount <= 0)
                    ChangeDirection();
            }
            // Subtract the jump delay.
            if (_jumpDelay > 0)
                _jumpDelay -= Game1.DeltaTime;
        }

        private void ToJumping()
        {
            _aiComponent.ChangeState("moveUp");
            Game1.AudioManager.PlaySoundEffect("D360-36-24");
            _animator.Play("jump");
            _body.Velocity.Z = 2;
        }

        private void UpdateMoveUp()
        {
            // start waiting in the air
            if (EntityPosition.Z > 26 || _body.Velocity.Z <= 0)
            {
                ToWait();
                return;
            }
            var vecDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            vecDirection.Normalize();
            _body.VelocityTarget = vecDirection * _walkSpeed * 2;
        }

        private void ToWait()
        {
            _aiComponent.ChangeState("wait");

            _body.VelocityTarget = Vector2.Zero;
            _body.IgnoresZ = true;
        }

        private void ToMoveDown()
        {
            _aiComponent.ChangeState("moveDown");

            _body.Velocity.Z = -3.5f;
            _body.IgnoresZ = false;
        }

        private void UpdateMoveDown()
        {
            if (_body.IsGrounded)
                ToWaitFloor();
            _body.VelocityTarget = Vector2.Zero;
        }

        private void ToWaitFloor()
        {
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
            _aiComponent.ChangeState("waitFloor");

            Game1.AudioManager.PlaySoundEffect("D360-07-07");

            // Show a green sparking effect.
            var animation = new ObjSwordShotSpark(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 0, 4);
            Map.Objects.SpawnObject(animation);

            // Add a slight delay to the next jump.
            _jumpDelay = 50;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if ((collision & Values.BodyCollision.Floor) != 0 && _aiComponent.CurrentStateId == "moveDown")
                ToWaitFloor();

            // Do not try to change direction when landing from a jump.
            if (_aiComponent.CurrentStateId != "waitFloor")
                if ((collision & (Values.BodyCollision.Horizontal | Values.BodyCollision.Vertical)) != 0)
                    ChangeDirection();
        }

        private void ChangeDirection()
        {
            _changeDirCount = Game1.RandomNumber.Next(200, 600);
            _dir = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_dir] * _walkSpeed;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If spawned during Hardhit Beetle it can not drop powerups.
            if (_owner != null)
                _damageState.SpawnPowerups = false;
            
            if (hitType == HitType.MagicRod || hitType == HitType.MagicPowder)
            {
                return Values.HitCollision.Blocking;
            }
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
