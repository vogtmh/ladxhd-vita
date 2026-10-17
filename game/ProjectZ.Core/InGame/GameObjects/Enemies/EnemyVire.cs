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
    internal class EnemyVire : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private EnemyVireBat _batLeft;
        private EnemyVireBat _batRight;

        private readonly Rectangle _roomRectangle;
        private readonly Vector2 _roomCenter;

        private const float DashSpeed = 2.0f;

        private const int CircleWidth = 80;
        private const int CircleHeight = 60;
        private const int FlyHeight = 41;

        private Vector2 _targetPosition;
        private int _circleDirection;
        private int _lives = EnemyLives.Vire;
        private int _dropIndex = 2;

        public EnemyVire() : base("vire") { }

        public EnemyVire(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-12, -64, 24, 64);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/vire");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animatorComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            var fieldRectangle = map.GetField(posX, posY, 8);

            _body = new BodyComponent(EntityPosition, -8, -15, 16, 15, 8)
            {
                IgnoresZ = true,
                IgnoreHoles = true,
                Gravity = -0.125f,
                DragAir = 0.975f,
                Bounciness = 0.25f,
                FieldRectangle = Map.GetField(posX, posY),
                FieldLocked = false,
                CollisionTypes = Values.CollisionTypes.None,
                AbsorbPercentage = 0.75f
            };

            _roomRectangle = Map.GetField(posX, posY);
            _roomCenter = new Vector2(fieldRectangle.Center.X, fieldRectangle.Center.Y);

            _aiComponent = new AiComponent();

            var stateDebug = new AiState();
            var stateIdle = new AiState(UpdateIdle);
            var stateFlying = new AiState(UpdateFlying) { Init = InitFlying };
            var stateCircling = new AiState(UpdateCircling) { Init = InitCircling };
            stateCircling.Trigger.Add(new AiTriggerCountdown(2000, null, RandomAttack));
            var statePreAttack = new AiState() { Init = InitPreAttack };
            statePreAttack.Trigger.Add(new AiTriggerCountdown(100, null, () => _aiComponent.ChangeState("attack")));
            var stateAttack = new AiState(UpdateAttack) { Init = InitAttack };
            var statePreDash = new AiState() { Init = InitPreDash };
            statePreDash.Trigger.Add(new AiTriggerCountdown(500, null, () => _aiComponent.ChangeState("dash")));
            var stateDash = new AiState(UpdateDash) { Init = InitDash };
            stateDash.Trigger.Add(new AiTriggerCountdown(750, null, () => _aiComponent.ChangeState("repelled")));
            var stateRepelled = new AiState(UpdateRepelled);
            stateRepelled.Trigger.Add(new AiTriggerCountdown(750, null, () => _aiComponent.ChangeState("circling")));

            _aiComponent.States.Add("debug", stateDebug);
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("flying", stateFlying);
            _aiComponent.States.Add("circling", stateCircling);
            _aiComponent.States.Add("preAttack", statePreAttack);
            _aiComponent.States.Add("attack", stateAttack);
            _aiComponent.States.Add("preDash", statePreDash);
            _aiComponent.States.Add("dash", stateDash);
            _aiComponent.States.Add("repelled", stateRepelled);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            new AiFallState(_aiComponent, _body, null, null);
            new AiDeepWaterState(_body) { FallTime = 0, OnDrown = OnDrown };

            _aiComponent.ChangeState("idle");

            var damageBox = new CBox(EntityPosition, -8, -15, 0, 16, 15, 8, true);
            var hittableBox = new CBox(EntityPosition, -10, -15, 0, 20, 15, 8, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(damageBox, OnPush));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animatorComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer) { WaterOutline = false });
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 10, ShadowHeight = 5 });

            _batLeft = new EnemyVireBat(Map, EntityPosition.ToVector3(), new Vector2(-0.75f, 0)) { IsActive = false };
            _batRight = new EnemyVireBat(Map, EntityPosition.ToVector3(), new Vector2(0.75f, 0)) { IsActive = false };
            Map.Objects.SpawnObject(_batLeft);
            Map.Objects.SpawnObject(_batRight);

            var spriteShadow = new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            Map.Objects.RegisterAlwaysAnimateObject(this);
            Map.Objects.RegisterAlwaysAnimateObject(spriteShadow);
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;

            EntityPosition.Z = 0;
            _animator.Play("idle");
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
            _body.IgnoresZ = true;
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
            _damageState.CurrentLives = EnemyLives.Vire;

            if (_batLeft == null || _batLeft.IsDead)
                _batLeft = new EnemyVireBat(Map, EntityPosition.ToVector3(), new Vector2(-0.75f, 0)) { IsActive = false };
            if (_batRight == null || _batRight.IsDead)
                _batRight = new EnemyVireBat(Map, EntityPosition.ToVector3(), new Vector2(0.75f, 0)) { IsActive = false };
        }

        private void OnDrown()
        {
            Map.Objects.DeleteObjects.Add(_batLeft);
            Map.Objects.DeleteObjects.Add(_batRight);
        }

        private void OnBurn()
        {
            _body.IgnoresZ = false;
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
            _batLeft.IsDead = true;
            _batRight.IsDead = true;
            Map.Objects.DeleteObjects.Add(_batLeft);
            Map.Objects.DeleteObjects.Add(_batRight);
        }

        private void UpdateIdle()
        {
            // Get the distance between Link and Vire.
            var distVec = EntityPosition.Position - new Vector2(MapManager.ObjLink.PosX, MapManager.ObjLink.PosY + 16);

            // Don't starty flying if not in the same room.
            if (!_roomRectangle.Contains(MapManager.ObjLink.CenterPosition))
                return;

            // Start flying if the player gets near.
            if (distVec.Length() < 60)
                _aiComponent.ChangeState("flying");
        }

        private void RandomAttack()
        {
            if (!_roomRectangle.Contains(MapManager.ObjLink.CenterPosition))
                return;

            if (Game1.RandomNumber.Next(0, 3) == 0)
                _aiComponent.ChangeState("preDash");
            else
                _aiComponent.ChangeState("preAttack");
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_aiComponent.CurrentStateId == "idle")
                _aiComponent.ChangeState("flying");

            var hitReturn = _damageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);

            if (_damageState.CurrentLives <= 0)
                SpawnBats();

            return hitReturn;
        }

        private void SpawnBats()
        {
            var spawnPosition = new Vector3(EntityPosition.X, EntityPosition.Y, EntityPosition.Z);

            // spawn the explosion effect
            var splashAnimator = new ObjAnimator(Map, (int)spawnPosition.X - 8,
                (int)spawnPosition.Y - (int)spawnPosition.Z - 16, 0, 0, Values.LayerTop, "Particles/spawn", "run", true);
            Map.Objects.SpawnObject(splashAnimator);

            // spawn the bats
            _batLeft.IsActive = _batLeft.IsVisible = true;
            _batLeft.EntityPosition.Set(spawnPosition);
            _batRight.IsActive = _batRight.IsVisible = true;
            _batRight.EntityPosition.Set(spawnPosition);

            Map.Objects.DeleteObjects.Add(this);
        }

        private void UpdateRepelled()
        {
            // get repelled by the player
            var playerDirection = new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z) -
                MapManager.ObjLink.Position;
            if (playerDirection != Vector2.Zero && playerDirection.Length() < 64)
            {
                playerDirection.Normalize();

                // dodge to the side
                var centerDirection = _roomCenter - new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z);
                if (centerDirection != Vector2.Zero)
                    centerDirection.Normalize();
                var moveDirection = new Vector2(centerDirection.Y, -centerDirection.X) * _circleDirection;

                _body.VelocityTarget = Vector2.Lerp(_body.VelocityTarget, moveDirection * 2f + playerDirection * 1.5f, 0.025f * Game1.TimeMultiplier);
            }
            else
            {
                _body.VelocityTarget = Vector2.Lerp(_body.VelocityTarget, Vector2.Zero, 0.05f * Game1.TimeMultiplier);
            }

            // move up
            if (EntityPosition.Z + 1 * Game1.TimeMultiplier < FlyHeight)
                EntityPosition.Z += 1 * Game1.TimeMultiplier;
            else
                EntityPosition.Z = FlyHeight;
        }

        private void InitPreDash()
        {
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("attack");
        }

        private void InitDash()
        {
            _animator.Play("fly");

            var playerDirection = MapManager.ObjLink.Position - new Vector2(EntityPosition.X, EntityPosition.Y - 12);
            if (playerDirection != Vector2.Zero)
            {
                playerDirection.Normalize();
                _body.VelocityTarget = playerDirection * DashSpeed;
            }
        }

        private void UpdateDash()
        {
            // move down
            if (EntityPosition.Z - 1 * Game1.TimeMultiplier > 12)
                EntityPosition.Z -= 1 * Game1.TimeMultiplier;
            else
                EntityPosition.Z = 12;
        }

        private void InitPreAttack()
        {
            _animator.Play("attack");
        }

        private void InitAttack()
        {
            var startPosition = new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z - 8);
            var direction = MapManager.ObjLink.Position - startPosition;
            var radiant = MathF.Atan2(direction.Y, direction.X);

            var dist = 0.125f;
            var vireball0 = new EnemyVireball(Map, startPosition, new Vector2(MathF.Cos(radiant - dist), MathF.Sin(radiant - dist) * 1.5f));
            Map.Objects.SpawnObject(vireball0);
            var vireball1 = new EnemyVireball(Map, startPosition, new Vector2(MathF.Cos(radiant + dist), MathF.Sin(radiant + dist) * 1.5f));
            Map.Objects.SpawnObject(vireball1);
            Game1.AudioManager.PlaySoundEffect("D378-13-0D");
        }

        private void UpdateAttack()
        {
            if (!_animator.IsPlaying)
            {
                _animator.Play("fly");
                _aiComponent.ChangeState("circling");
            }
        }

        private void InitFlying()
        {
            var playerDirection = _roomCenter - MapManager.ObjLink.Position;
            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();

            _targetPosition = _roomCenter + new Vector2(playerDirection.X * CircleWidth, playerDirection.Y * CircleHeight + FlyHeight + 16);
        }

        private void UpdateFlying()
        {
            _animator.Play("fly");

            var reachedTarget = true;

            // move towards the target position
            var distVec = _targetPosition - EntityPosition.Position;
            if (distVec.Length() > 0.5f * Game1.TimeMultiplier)
            {
                distVec.Normalize();
                _body.VelocityTarget = distVec * 0.5f;
                reachedTarget = false;
            }
            else
            {
                _body.VelocityTarget = Vector2.Zero;
                EntityPosition.Set(_targetPosition);
            }

            // fly up
            if (EntityPosition.Z + 0.5f * Game1.TimeMultiplier < FlyHeight)
            {
                EntityPosition.Z += 0.5f * Game1.TimeMultiplier;
                reachedTarget = false;
            }
            else
            {
                EntityPosition.Z = FlyHeight;
            }

            // finished flying up and moving towards the target?
            if (reachedTarget)
            {
                _aiComponent.ChangeState("circling");
            }
        }

        private void InitCircling()
        {
            _circleDirection = Game1.RandomNumber.Next(0, 1) * 2 - 1;
        }

        private void UpdateCircling()
        {
            var playerDirection = new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z) -
                MapManager.ObjLink.Position;
            if (playerDirection.Length() < 40)
            {
                _aiComponent.ChangeState("repelled");
            }

            var centerDirection = _roomCenter - new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z);
            if (centerDirection != Vector2.Zero)
                centerDirection.Normalize();

            var moveDirection = new Vector2(centerDirection.Y, -centerDirection.X) * _circleDirection;
            var angle = MathF.Atan2(centerDirection.Y, centerDirection.X);

            // distance to the ellipse
            var e = MathF.Sqrt(1 - (float)(CircleHeight * CircleHeight) / (CircleWidth * CircleWidth));
            var distance = CircleHeight / MathF.Sqrt(1 - MathF.Pow(e * MathF.Cos(angle), 2));
            var circleVector = -centerDirection * distance;

            var targetPosition = _roomCenter + circleVector;
            var entityPosition = new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z);
            var circleDirection = targetPosition - entityPosition;

            // slow down to not overshoot while moving towards the circle
            if (circleDirection.Length() > 16)
                circleDirection.Normalize();
            else
                circleDirection /= 16;

            // move towards the circle/ around the circle
            _body.VelocityTarget = Vector2.Lerp(_body.VelocityTarget, moveDirection * 0.5f + circleDirection, 0.1f * Game1.TimeMultiplier);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }
    }
}
