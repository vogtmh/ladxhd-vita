using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyIronMask : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private float _moveSpeed = 0.5f;
        private float _moveSpeedUnprotected = 0.75f;
        private int _direction;
        private bool _isUnprotected;
        private int _lives = EnemyLives.IronMask;
        private int _dropIndex = 4;

        public EnemyIronMask() : base("iron mask") { }

        public EnemyIronMask(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/iron mask");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -15));

            _body = new BodyComponent(EntityPosition, -6, -10, 12, 10, 8)
            {
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                Bounciness = 0.25f,
                Drag = 0.75f
            };

            var stateIdle = new AiState { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walking"), 350, 750));
            var stateWalking = new AiState { Init = InitWalking };
            stateWalking.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 750, 1000));
            var stateStunned = new AiState { Init = InitStunned };
            stateStunned.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walking"), 1000, 1200));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("stunned", stateStunned);

            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            new AiFallState(_aiComponent, _body, OnHoleAbsorb);
            new AiDeepWaterState(_body);

            _aiComponent.ChangeState("idle");

            // stand in a random direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _animator.Play("walk_" + _direction);
            _animator.IsPlaying = false;

            var damageBox   = new CBox(EntityPosition, -4,  -8, 0,  8, 6, 16);
            var hittableBox = new CBox(EntityPosition, -7, -14, 0, 14, 14, 8);
            var pushableBox = new CBox(EntityPosition, -7, -12, 0, 14, 12, 8);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
        }

        public override void Reset()
        {
            _isUnprotected = false;
            _animator.Play("walk_" + _direction);
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void InitStunned()
        {
            if (!_isUnprotected)
                _animator.IsPlaying = false;
            _body.VelocityTarget = Vector2.Zero;
        }

        private void InitIdle()
        {
            if (!_isUnprotected)
                _animator.IsPlaying = false;
            _body.VelocityTarget = Vector2.Zero;
        }

        private void InitWalking()
        {
            ChangeDirection();
        }

        private void ChangeDirection()
        {
            // random new direction
            _direction = Game1.RandomNumber.Next(0, 4);

            if (!_isUnprotected)
                _animator.Play("walk_" + _direction);

            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] *
                                   (_isUnprotected ? _moveSpeedUnprotected : _moveSpeed);
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
            _animator.SpeedMultiplier = 3f;

            if (!_isUnprotected)
                _animator.Play("walk_" + _direction);
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // The enemy is wearing its mask.
            if (!_isUnprotected)
            {
                // Convert the vector into an integer direction.
                var dir = AnimationHelper.GetDirection(direction);

                // Easy reference variables to know the direction damage is coming from.
                bool isHitFront = dir == (_direction + 2) % 4;
                bool isHitBack  = dir == _direction;

                // The Hookshot can grab the mask when hitting it from the front.
                if (isHitFront && hitType == HitType.Hookshot)
                {
                    Game1.AudioManager.PlaySoundEffect("D370-01-01");
                    _isUnprotected = true;
                    _animator.Play("unprotected");
                    _damageState.SetDamageState(false);
                    return Values.HitCollision.Blocking;
                }
                // Any attack from the sword can only hit the back while it has the mask.
                else if (!isHitBack && (hitType & HitType.AnySword) != 0)
                {
                    _body.Velocity = new Vector3(direction * 0.75f, 0);
                    _aiComponent.ChangeState("stunned");
                    return Values.HitCollision.RepellingParticle;
                }
                // The Boomerang just quietly returns.
                else if (isHitFront && hitType == HitType.Boomerang)
                {
                    return Values.HitCollision.Blocking;
                }
                // Pretty much nothing can kill it from the front except bombs and thrown objects.
                else if (isHitFront && (hitType & HitType.Bomb) == 0 && (hitType & HitType.ThrownObject) == 0)
                {
                    return Values.HitCollision.RepellingParticle | Values.HitCollision.SpawnFire;
                }
            }
            // It's either unprotected or hit from the sides.
            var hit = _damageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);

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
