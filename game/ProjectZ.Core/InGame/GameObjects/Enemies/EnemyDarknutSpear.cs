using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyDarknutSpear : GameObject
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
            new Vector2(-8, -3), new Vector2(0, -3),
            new Vector2(8, -3), new Vector2(0, 2)
        };
        private float _spearCooldown;
        private float _moveSpeed = 0.5f;
        private int _direction;
        private int _lives = EnemyLives.DarknutSpear;
        private int _dropIndex = 4;

        public EnemyDarknutSpear() : base("darknut spear") { }

        public EnemyDarknutSpear(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/darknut spear");
            _animator.Play("walk_1");
            _spearCooldown = 2000;

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -7, -10, 14, 10, 8)
            {
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                Bounciness = 0.25f,
                Drag = 0.85f
            };

            var walkingState = new AiState { Init = InitWalking };
            walkingState.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 550, 850));
            var idleState = new AiState { Init = InitIdle };
            idleState.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walking"), 300, 500));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("walking", walkingState);
            _aiComponent.States.Add("idle", idleState);
            new AiFallState(_aiComponent, _body, OnHoleAbsorb);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            // start randomly idle or walking facing a random direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _aiComponent.ChangeState(Game1.RandomNumber.Next(0, 2) == 0 ? "walking" : "idle");

            var damageBox = new CBox(EntityPosition, -4, -10, 0, 8, 8, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 14, 15, 8);
            var pushableBox = new CBox(EntityPosition, -7, -11, 0, 14, 11, 4);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _direction = Game1.RandomNumber.Next(0, 4);
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
            _animator.Play("idle");
            _damageState.CurrentLives = EnemyLives.DarknutSpear;
            _animator.SpeedMultiplier = 1f;
        }

        private void Update()
        {
            if (_spearCooldown > 0)
                _spearCooldown -= Game1.DeltaTime;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void InitIdle()
        {
            _animator.Play("stand_" + _direction);
            _body.VelocityTarget = Vector2.Zero;

            ThrowSpear();
        }

        private void InitWalking()
        {
            ChangeDirection();
        }

        private void ChangeDirection()
        {
            // random new direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _animator.Play("walk_" + _direction);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * _moveSpeed;
        }

        private void ThrowSpear()
        {
            // Don't let them throw spears one after the other.
            if (_spearCooldown > 0)
                return;

            // It's a 50% chance to throw a spear if Link is in the field.
            if (Game1.RandomNumber.Next(0, 2) == 0 || !_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
                return;

            // Calculate distance between the Stalfos and Link.
            var playerDistance = MapManager.ObjLink.Position - EntityPosition.Position;

            // Throw a spear if the player is in range and in the facing direction.
            if (playerDistance.Length() < 160)
            {
                // Get the direction the player is in.
                if (playerDistance != Vector2.Zero)
                    playerDistance.Normalize();
                var direction = AnimationHelper.GetDirection(playerDistance);

                // If the directions are the same.
                if (direction == _direction)
                {
                    // Reference box and comparison box.
                    var boxX = EntityPosition.X + _shotOffset[_direction].X - 4;
                    var boxY = EntityPosition.Y + _shotOffset[_direction].Y - 4;
                    var colBox = new Box(boxX, boxY, 0, 8, 8, 8);
                    var refBox = Box.Empty;

                    // Check if the box collides with anything.
                    if (!Map.Objects.Collision(colBox, Box.Empty, Values.CollisionTypes.Normal, 0, _body.Level, ref refBox))
                    {
                        // The position and velocity. 
                        var x = EntityPosition.X + _shotOffset[_direction].X;
                        var y = EntityPosition.Y + _shotOffset[_direction].Y;
                        var v = AnimationHelper.DirectionOffset[_direction] * 2f;

                        // Throw a spear towards the direction with the velocity.
                        var position = new Vector3(x, y, 3);
                        var shot = new EnemySpear(Map, position, v, _direction);
                        Map.Objects.SpawnObject(shot);

                        // Don't allow throwing another spear for 2 seconds.
                        _spearCooldown = 2000;
                    }
                }
            }
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
            _animator.Play("walk_" + _direction);
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If we got here, it was probably a hit from the back or another weapon than sword.
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
