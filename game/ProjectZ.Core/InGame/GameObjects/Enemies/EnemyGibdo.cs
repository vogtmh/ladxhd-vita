using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyGibdo : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly DamageFieldComponent _damageField;
        private readonly EnemyStalfosOrange _stalfos;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private const float MoveSpeed = 0.5f;

        private int _direction;
        private int _lives = EnemyLives.Gibdo;
        private int _dropIndex = 5;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyGibdo() : base("gibdo") { }

        public EnemyGibdo(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/gibdo");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -6, -10, 12, 10, 8)
            {
                MoveCollision = OnMoveCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f,
                Bounciness = 0.25f,
                Drag = 0.85f
            };

            var stateWalking = new AiState { Init = InitWalking };
            stateWalking.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walk"), 550, 850));
            _aiComponent = new AiComponent();
            _aiComponent.States.Add("walk", stateWalking);

            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex, false) { HitMultiplierX = 1.0f, HitMultiplierY = 1.0f, OnDeath = OnDeath, OnBurn = OnBurn };
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, animationComponent, 3300, 900) { ShakeOffset = 1, SilentStateChange = false, ReturnState = "walk", OnStun = OnStun, OnStunRelease = OnStunRelease };

            new AiFallState(_aiComponent, _body, OnHoleAbsorb);
            new AiDeepWaterState(_body);

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0, 6,  6,  4);
            var pushableBox = new CBox(EntityPosition, -6, -13, 0, 12, 13, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 0, 14, 15, 8);

            _stalfos = new EnemyStalfosOrange(Map, posX, posY, true) { IsActive = false, WasSpawned = true };
            Map.Objects.SpawnObject(_stalfos);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BombMultiplier = true, StunHookshot = true, });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            _aiComponent.ChangeState("walk");
        }

        public override void Reset()
        {
            if (_carriableComponent.IsPickedUp)
                return; 

            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _carriableComponent.IsActive = false;
            _aiComponent.ChangeState("walk");
            _aiComponent.ChangeState("walk");
            _animator.SpeedMultiplier = 1f;
            _isThrown = false;
            _aiStunnedState.EndStun();
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }
        private void OnStun()
        {
            _carriableComponent.IsActive = true;
            _damageField.IsActive = false;
        }

        private void OnStunRelease()
        {
            _carriableComponent.IsActive = false;
            _damageField.IsActive = true;
        }

        private void Update()
        {
            // Check if the enemy was thrown.
            if (_isThrown)
            {
                // Deal a hit to whatever it comes in contact with.
                var pos  = new Vector3(EntityPosition.X - 7, EntityPosition.Y - 14, EntityPosition.Z);
                var size = new Vector3(14, 14, 8);
                var throwBox = new Box(pos, size);

                // Find objects to hit when thrown.
                if (Map.Objects.Hit(this, throwBox.Center, throwBox, HitType.ThrownObject, 2, false) != 0)
                {
                    // Bounce off the object when hit.
                    _body.Velocity.X = -_body.Velocity.X * 0.5f;
                    _body.Velocity.Y = -_body.Velocity.Y * 0.5f;
                }
            }
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            _body.IsActive = false;
            _body.BodyBox = new CBox(EntityPosition, -4, -8 + _offsetY, 8, 8, 12);
            return new Vector3(EntityPosition.X, EntityPosition.Y - _offsetY, EntityPosition.Z);
        }

        private bool CarryUpdate(Vector3 newPosition)
        {
            // Reset the stun state as it's being carried.
            _aiStunnedState.ResetStun();

            EntityPosition.X = newPosition.X;
            EntityPosition.Y = newPosition.Y - _offsetY;
            EntityPosition.Z = newPosition.Z;

            EntityPosition.NotifyListeners();
            return true;
        }

        private void CarryThrow(Vector2 velocity)
        {
            _isThrown = true;
            _carriableComponent.Thrown = true;
            _body.IsGrounded = false;
            _body.IsActive = true;
            _body.Velocity = new Vector3(velocity.X, velocity.Y, 0) * 2.0f;
            _body.Level = MapStates.GetLevel(MapManager.ObjLink.Body.CurrentFieldState);
        }

        private void OnDeath(bool pieceOfPower)
        {
            if (Map == null)
                return;

            if (_aiComponent.CurrentStateId == "burning")
            {
                Map.Objects.DeleteObjects.Add(this);

                // Spawn the Stalfos.
                _stalfos.EntityPosition.Set(new Vector2(EntityPosition.X, (int)EntityPosition.Y));
                _stalfos.IsActive = true;
                return;
            }
            _damageState.BaseOnDeath(pieceOfPower);
            Map.Objects.DeleteObjects.Add(_stalfos);
        }

        private void InitWalking()
        {
            _animator.Play("idle");
            _damageField.IsActive = true;
            // walk into a random direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * MoveSpeed;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            return true;
        }

        private void OnHoleAbsorb()
        {
            _animator.SpeedMultiplier = 3f;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (hitType == HitType.MagicPowder)
            {
                return Values.HitCollision.None;
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

        public void AddToEnemyTriggerGroup(ObjEnemyTrigger etrigger)
        {
            // If respawned in a room with an enemy trigger, this is a means 
            // to adding the Stalfos spawned with the Gibdo to the trigger list.
            etrigger.EnemyTriggerList.Add(_stalfos);
        }

        private void OnMoveCollision(Values.BodyCollision direction)
        {
            if ((direction & Values.BodyCollision.Horizontal) != 0)
                _body.VelocityTarget.X = -_body.VelocityTarget.X;
            if ((direction & Values.BodyCollision.Vertical) != 0)
                _body.VelocityTarget.Y = -_body.VelocityTarget.Y;

            if (_isThrown && (direction & Values.BodyCollision.Floor) != 0)
            {
                _isThrown = false;
                _carriableComponent.Thrown = false;
                _aiStunnedState.ResetBodyState();
            }
        }
    }
}
