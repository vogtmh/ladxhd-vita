using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyBonePutter : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly AnimationComponent _animatorComponent;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;

        private readonly Vector2 _roomCenter;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;
        private float _flyCounter;
        private float _flyTime;
        private int _bombThrowCounter;

        private const float JumpSpeed = 0.25f;
        private bool _hasWings;
        private int _lives = EnemyLives.BonePutter;
        private int _livesWings = EnemyLives.BonePutterWing;
        private int _dropIndex = 2;

        public EnemyBonePutter() : base("bone putter") { }

        public EnemyBonePutter(Map.Map map, int posX, int posY, bool hasWings) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 16);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 16);
            EntitySize = new Rectangle(-8, -32, 16, 32);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/bone putter");

            var sprite = new CSprite(EntityPosition);
            _animatorComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -4, -8, 8, 8, 8)
            {
                MoveCollision = OnCollision,
                HoleOnPull = OnHolePull,
                IgnoresZ = true,
                Gravity = -0.075f,
                DragAir = 0.875f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY)
            };
            _hasWings = hasWings;

            var roomRectangle = map.GetField(posX, posY);
            _roomCenter = new Vector2(roomRectangle.Center.X, roomRectangle.Center.Y + 8);

            _aiComponent = new AiComponent();

            var stateFlying = new AiState(UpdateFlying) { Init = InitFlying };
            var stateJumping = new AiState(UpdateJumping) { Init = InitJumping };
            var stateHole = new AiState();

            _aiComponent.States.Add("flying", stateFlying);
            _aiComponent.States.Add("jumping", stateJumping);
            _aiComponent.States.Add("holePull", stateHole);
            _aiComponent.ChangeState(_hasWings ? "flying" : "jumping");
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _hasWings ? _livesWings : _lives, _dropIndex, false);

            new AiFallState(_aiComponent, _body, null, null, 200);

            var damageBox   = new CBox(EntityPosition, -3,  -8, 2,  6,  6, 4, true);
            var hittableBox = new CBox(EntityPosition, -6, -15, 2, 12, 14, 8, true);
            var pushableBox = new CBox(EntityPosition, -6, -15, 2, 12, 14, 4, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2) { IsActive = !_hasWings });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BoomerangMultiplier = true });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, _animatorComponent);
            AddComponent(PushableComponent.Index, new PushableComponent(pushableBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite));

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void Reset()
        {
            _aiComponent.ChangeState(_hasWings ? "flying" : "jumping");
            _aiComponent.ChangeState(_hasWings ? "flying" : "jumping");
            _hitComponent.ArrowMultiplier = _hasWings ? true : false;
        }

        public bool StartJump()
        {
            return _aiComponent.CurrentStateId == "jumping" && _body.IsGrounded && EntityPosition.Z <= 0;
        }

        private void InitFlying()
        {
            _animator.Play("fly");
            _startPosition = EntityPosition.Position;
            _targetPosition = _startPosition;
            _flyCounter = 0;
        }

        private void UpdateFlying()
        {
            _flyCounter -= Game1.DeltaTime;

            if (_flyCounter > 0)
            {
                var lerpPercentage = 0.5f - MathF.Sin(_flyCounter / _flyTime * MathF.PI - MathF.PI / 2) * 0.5f;
                var newPosition = Vector2.Lerp(_startPosition, _targetPosition, lerpPercentage);
                EntityPosition.Set(newPosition);
            }
            else
            {
                ThrowBomb();

                EntityPosition.Set(_targetPosition);
                _startPosition = _targetPosition;

                // the target direction will be in the direction of the center if we are farther away from the center
                var centerDirection = EntityPosition.Position - _roomCenter;
                var centerDistance = centerDirection.Length();
                if (centerDirection != Vector2.Zero)
                    centerDirection.Normalize();
                var centerRadian = MathF.Atan2(centerDirection.Y, centerDirection.X);

                // set a new target position
                var centerOffset = Math.Clamp((50 - centerDistance) / 25, 0, 1);
                var randomRotation = centerRadian - (MathF.PI + Game1.RandomNumber.Next(0, 628) / 100f) * centerOffset;
                var randomDistance = Game1.RandomNumber.Next(12, 20);
                _targetPosition.X = _startPosition.X - MathF.Cos(randomRotation) * randomDistance;
                _targetPosition.Y = _startPosition.Y - MathF.Sin(randomRotation) * randomDistance;

                _animatorComponent.MirroredH = _targetPosition.X > _startPosition.X;
                _animatorComponent.UpdateSprite();

                // random fly time
                _flyTime = Game1.RandomNumber.Next(600, 800);
                _flyCounter = _flyTime;
            }
        }

        private void ThrowBomb()
        {
            _bombThrowCounter--;
            if (_bombThrowCounter > 0)
                return;

            var playerDistance = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDistance.Length() > 64)
                return;

            _bombThrowCounter = Game1.RandomNumber.Next(2, 6);

            // spawn a bomb
            var bomb = new ObjBomb(Map, 0, 0, false, true);
            bomb.EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y + 1, 18));
            bomb.Body.Velocity = new Vector3(0, 0, 0.25f);
            bomb.Body.Gravity = -0.1f;
            bomb.Body.Bounciness = 0.4f;
            Map.Objects.SpawnObject(bomb);
        }

        private void InitJumping()
        {
            _animator.Play("jump");
            _body.IgnoresZ = false;
            _body.IsGrounded = false;
            _body.JumpStartHeight = 0;
        }

        private void UpdateJumping()
        {
            if (_body.IsGrounded)
                Jump();
        }

        private void Jump()
        {
            // jump into a random direction
            var randomRotation = Game1.RandomNumber.Next(0, 628) / 100f;
            _body.VelocityTarget.X = -MathF.Cos(randomRotation) * JumpSpeed;
            _body.VelocityTarget.Y = -MathF.Sin(randomRotation) * JumpSpeed;
            _body.Velocity.Z = 1.5f;

            _animatorComponent.MirroredH = _body.VelocityTarget.X > 0;
            _animatorComponent.UpdateSprite();
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // They are basically an entirely different enemy when flying.
            if (_aiComponent.CurrentStateId == "flying")
            {
                // Immune to Bombs, Bomb Arrows, and the Hookshot.
                if (hitType == HitType.Bomb || hitType == HitType.BombArrow || hitType == HitType.Hookshot)
                {
                    return Values.HitCollision.Blocking;
                }
                // If hit with the sword or its beam remove it's wings.
                if (hitType == HitType.SwordShot || (hitType & HitType.AnySword) != 0)
                {
                    _aiComponent.ChangeState("jumping");
                    _hitComponent.ArrowMultiplier = false;
                    _damageField.IsActive = true;
                 }
                // Arrows kill it instantly and sometimes drops a fairy.
                else if (hitType == HitType.Bow)
                {
                    _damageState.DropTableIndex = 7;
                    _damageState.ExplosionOffsetY = -16;
                }
                // Magic Rod removes their drop table completely.
                else if (hitType == HitType.MagicRod)
                {
                    _aiComponent.ChangeState("jumping");
                    _damageState.DropTableIndex = 0;
                }
                // Boomerang & Magic Powder have a unique death.
                else if ((hitType == HitType.Boomerang || hitType == HitType.MagicPowder))
                {
                    // Cancel out the normal death effects and run BaseOnDeath for item drops.
                    _damageState.NullifyDeathEffects();
                    _damageState.DropTableIndex = 9;
                    _damageState.BaseOnDeath(pieceOfPower);

                    // Play the crunch sound and show the smoke effect.
                    Game1.AudioManager.PlaySoundEffect("D360-03-03");
                    var explosionAnimation = new ObjAnimator(Map, (int)EntityPosition.X-8, (int)EntityPosition.Y-26, Values.LayerTop, "Particles/spawn", "run", true);
                    Map.Objects.SpawnObject(explosionAnimation);
                    return Values.HitCollision.Blocking;
                }
            }
            // Register the hit.
            var hit = _damageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
            }
            // Return the hit.
            return hit;

        }

        private void OnHolePull(Vector2 direction, float percentage)
        {
            if (percentage < 0.5f)
                return;

            _aiComponent.ChangeState("holePull");
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if ((collision & Values.BodyCollision.Horizontal) != 0)
                _body.Velocity.X = -_body.Velocity.X * 0.5f;
            if ((collision & Values.BodyCollision.Vertical) != 0)
                _body.Velocity.Y = -_body.Velocity.Y * 0.5f;
        }
    }
}
