using System;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyBuzzBlob : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiStunnedState _aiStunnedState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly float _moveSpeed = 0.33f;
        private const int ShockTime = 550;
        private bool _isCukeman;
        private int _lives = EnemyLives.BuzzBlob;
        private int _dropIndex = 14;

        private int _offsetY = 1;
        private bool _isThrown;

        public EnemyBuzzBlob() : base("buzz blob") { }

        public EnemyBuzzBlob(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-10, -16, 20, 20);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/buzzblob");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-6, -16));

            _body = new BodyComponent(EntityPosition, -4, -10, 8, 10, 8)
            {
                MoveCollision  = OnMoveCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes     = Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                IgnoreInsideCollision = false,
                InsideCollisionEscape = 0.5f,
            };

            var stateWalking = new AiState() { Init = InitWalking };
            stateWalking.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walking"), 500, 1000));
            var stateShocking = new AiState(UpdateShocking);
            stateShocking.Trigger.Add(new AiTriggerCountdown(ShockTime, null, () => _aiComponent.ChangeState("postShock")));
            var statePostShock = new AiState() { Init = InitPostShock };
            statePostShock.Trigger.Add(new AiTriggerCountdown(350, null, () => _aiComponent.ChangeState("walking")));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("shocking", stateShocking);
            _aiComponent.States.Add("postShock", statePostShock);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex, false) { OnDeath = OnDeath, OnBurn = OnBurn };
            _aiStunnedState = new AiStunnedState(_body, _aiComponent, animationComponent, 3300, 900) { ShakeOffset = 1, SilentStateChange = false, ReturnState = "walking", OnStun = OnStun, OnStunRelease = OnStunRelease };

            new AiFallState(_aiComponent, _body, OnHolePull, OnHoleDeath, 400);
            new AiDeepWaterState(_body) { FallTime = 50 };

            var interactionBox = new CBox(EntityPosition, -10, -16, 0, 20, 20, 8);
            var hittableBox    = new CBox(EntityPosition,  -6, -14, 0, 12, 14, 8);
            var damageBox      = new CBox(EntityPosition,  -2, -10, 0,  4,  8, 4);
            var pushableBox    = new CBox(EntityPosition,  -4, -11, 0,  8, 11, 4);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6,-12,12,12)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { StunHookshot = true, ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true, ThrownMultiplier = true });
            AddComponent(InteractComponent.Index, new InteractComponent(interactionBox, OnInteract));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            _aiComponent.ChangeState("walking");
        }

        public override void Reset()
        {
            if (_carriableComponent.IsPickedUp)
                return; 

            _isCukeman = false;
            _animator.Play("walk");
            _aiComponent.ChangeState("walking");
            _aiComponent.ChangeState("walking");
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _damageState.CurrentLives = EnemyLives.BuzzBlob;
            Game1.GameManager.UseShockEffect = false;
            _animator.SpeedMultiplier = 1f;
            _isThrown = false;
            _aiStunnedState.EndStun();
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

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void OnDeath(bool pieceOfPower)
        {
            Game1.GameManager.UseShockEffect = false;
            _damageState.BaseOnDeath(pieceOfPower);
        }

        private void UpdateShocking()
        {
            MapManager.ObjLink.CanWalk = false;
        }

        private void InitPostShock()
        {
            Game1.GameManager.UseShockEffect = false;
        }

        private void InitWalking()
        {
            _animator.Play(_isCukeman ? "cukeman" : "walk");

            if (!_aiStunnedState.IsStunned())
            {
                _damageField.IsActive = true;
            }
            // new random direction
            var directionIndex = Game1.RandomNumber.Next(0, 8);
            var radius = directionIndex / 4.0 * Math.PI;
            _body.VelocityTarget = new Vector2((float)Math.Sin(radius), (float)Math.Cos(radius)) * _moveSpeed;
        }

        private void OnHolePull()
        {
            _animator.Play("walk");
            _animator.SpeedMultiplier = 2.0f;
        }

        private void OnHoleDeath()
        {
            Game1.GameManager.UseShockEffect = false;
        }

        private bool OnInteract()
        {
            if (!_isCukeman)
                return false;

            Game1.GameManager.StartDialogPath("cukeman");

            return true;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void StartShock()
        {
            if (_aiComponent.CurrentStateId == "shocking")
                return;

            MapManager.ObjLink.ShockPlayer(ShockTime);
            Game1.AudioManager.PlaySoundEffect("D378-28-1C");

            _body.VelocityTarget = Vector2.Zero;
            _aiComponent.ChangeState("shocking");
            _animator.Play("shock");
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_damageState.IsInDamageState())
                return Values.HitCollision.None;

            // Hookshot stuns the enemy.
            if (hitType == HitType.Hookshot)
            {
                return _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);
            }
            // Magic Powder turns the enemy into Cukeman.
            else if (hitType == HitType.MagicPowder)
            {
                _isCukeman = true;
                _animator.Play("cukeman");
                Game1.AudioManager.PlaySoundEffect("D360-03-03");

                // Spawn explosion effect.
                ObjAnimator animator;
                Map.Objects.SpawnObject(animator = new ObjAnimator(Map, 0, 0, Values.LayerBottom, "Particles/spawn", "run", true));
                animator.EntityPosition.Set(new Vector2(EntityPosition.X - 8, EntityPosition.Y - 16));

                return Values.HitCollision.Enemy;
            }
            // Damage if stunned and shock the player if not stunned.
            else if (!_aiStunnedState.IsStunned() && ((hitType & HitType.Sword) != 0 || hitType == HitType.PegasusBootsSword))
            {
                if (_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
                    StartShock();

                return Values.HitCollision.Enemy;
            }
            return _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);
        }

        private void OnMoveCollision(Values.BodyCollision direction)
        {
            if (_isThrown && (direction & Values.BodyCollision.Floor) != 0)
            {
                _isThrown = false;
                _carriableComponent.Thrown = false;
                _aiStunnedState.ResetBodyState();
            }
        }
    }
}
