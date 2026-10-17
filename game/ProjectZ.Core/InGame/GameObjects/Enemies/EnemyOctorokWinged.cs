using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyOctorokWinged : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerSwitch _damageSwitch;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _bodyDrawComponent;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly Rectangle _wingRectangle = new Rectangle(160, 67, 8, 18);

        private readonly Vector2[] _shotOffset =
        {
            new Vector2(-8, -1),new Vector2(0, -6),
            new Vector2(8, -1),new Vector2(0, 11)
        };

        private readonly Rectangle _fieldRectangle;

        private float _walkSpeed = 0.5f;
        private float _shotCooldown = 2000;
        private float _flyCounter;
        private int _direction;
        private int _lives = EnemyLives.OctorokWinged;
        private int _dropIndex = 2;

        public EnemyOctorokWinged() : base("winged octorok") { }

        public EnemyOctorokWinged(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 15, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 15, 0);
            EntitySize = new Rectangle(-8, -15 - 16, 16, 32);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/octorok");
            _fieldRectangle = map.GetField(posX, posY);

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -15));

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8)
            {
                MoveCollision = OnCollision,
                AbsorbPercentage = 0.85f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = _fieldRectangle,
                Bounciness = 0.25f,
                Drag = 0.85f,
                Gravity = -0.04f,
            };

            var stateIdle = new AiState { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walking"), 250, 500));
            var stateWalking = new AiState { Init = InitWalking };
            stateWalking.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 750, 1000));
            var stateFlying = new AiState(UpdateFlying) { Init = InitFlying };

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("flying", stateFlying);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };
            _aiComponent.Trigger.Add(_damageSwitch = new AiTriggerSwitch(350));
            new AiFallState(_aiComponent, _body, OnHoleAbsorb);

            // random start position/state
            _direction = Game1.RandomNumber.Next(0, 4);
            _animator.Play("walk_" + _direction);
            _aiComponent.ChangeState(Game1.RandomNumber.Next(0, 2) == 0 ? "idle" : "walking");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 0, 14, 15, 8, true);
            var pushableBox = new CBox(EntityPosition, -7, -13, 0, 14, 13, 4, true);

            _bodyDrawComponent = new BodyDrawComponent(_body, sprite, Values.LayerPlayer);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { Height = 1.0f, Rotation = 0.1f, ShadowWidth = 10, ShadowHeight = 5 });
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            var spriteShadow = new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            Map.Objects.RegisterAlwaysAnimateObject(spriteShadow);
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

        private void InitIdle()
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

        private void InitWalking()
        {
            // random new direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _animator.Play("walk_" + _direction);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * _walkSpeed;
        }

        private void InitFlying()
        {
            // fly towards the player
            var vecDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            vecDirection.Normalize();

            _pushComponent.IsActive = false;
            _body.VelocityTarget = vecDirection;
            _body.Velocity.Z = 1.25f;
            _body.AvoidTypes = Values.CollisionTypes.NPCWall;
            _body.FieldRectangle = RectangleF.Empty;
        }

        private void UpdateFlying()
        {
            _flyCounter += Game1.DeltaTime;

            // face the player
            var vecDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            _direction = AnimationHelper.GetDirection(vecDirection);
            _animator.Play("walk_" + _direction);

            if (_body.IsGrounded && _body.Velocity.Z <= 0)
            {
                _pushComponent.IsActive = true;
                _shotCooldown = 0;
                _flyCounter = 0;
                _aiComponent.ChangeState("idle");
                _damageSwitch.Reset();
                _body.AvoidTypes = Values.CollisionTypes.Hole |
                                   Values.CollisionTypes.NPCWall;
                _body.FieldRectangle = _fieldRectangle;
            }
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // Snap pixels to grid if enabled.
            var baseX = GameSettings.PixelSnapping
                ? MathF.Round(EntityPosition.X)
                : EntityPosition.X;
            var baseY = GameSettings.PixelSnapping
                ? MathF.Round(EntityPosition.Y - 8 - EntityPosition.Z)
                : EntityPosition.Y - 8 - EntityPosition.Z;

            // Draw the wings.
            spriteBatch.Draw(Resources.SprEnemies, new Vector2(baseX - _wingRectangle.Width - 5, baseY),
                _wingRectangle, Color.White, 0, new Vector2(0, 9), Vector2.One,
                (int)_flyCounter % 132 < 66 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
            spriteBatch.Draw(Resources.SprEnemies, new Vector2(baseX + 5, baseY),
                _wingRectangle, Color.White, 0, new Vector2(0, 9), Vector2.One,
                ((int)_flyCounter % 132 < 66 ? SpriteEffects.FlipVertically : SpriteEffects.None) | SpriteEffects.FlipHorizontally, 0);

            _bodyDrawComponent.Draw(spriteBatch);
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
            // Sword spin ignores the jumping logic.
            if ((hitType & HitType.SwordSpin) != 0 || (hitType & HitType.BowWow) != 0)
                return _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // Sword can't deal damage while flying.
            if ((hitType & HitType.Sword) != 0 && !_body.IsGrounded)
                return Values.HitCollision.None;

            // Fly over the player if the octorok is facing Link.
            var playerDirection = AnimationHelper.GetDirection(
                MapManager.ObjLink.Position - EntityPosition.Position);

            if (_direction != (playerDirection + 2) % 4 && _damageSwitch.State &&
                (_aiComponent.CurrentStateId == "walking" || _aiComponent.CurrentStateId == "idle") &&
                hitType != HitType.PegasusBootsSword &&
                hitType != HitType.Bow &&
                hitType != HitType.Hookshot &&
                hitType != HitType.MagicRod &&
                hitType != HitType.MagicPowder &&
                hitType != HitType.Boomerang)
            {
                _aiComponent.ChangeState("flying");
                return Values.HitCollision.None;
            }
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
