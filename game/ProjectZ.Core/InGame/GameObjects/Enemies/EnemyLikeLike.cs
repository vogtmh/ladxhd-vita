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
    internal class EnemyLikeLike : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CBox _collisionBox;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private float _collisionTimer;
        private float _moveSpeed = 0.5f;
        private int _direction;
        private bool _hasPlayerTrapped;
        private bool _stoleShield;
        private int _lives = EnemyLives.LikeLike;
        private int _dropIndex = 4;

        public EnemyLikeLike() : base("like like") { }

        public EnemyLikeLike(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntityPosition.AddPositionListener(typeof(EnemyLikeLike), UpdatePosition);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/likelike");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -7, -14, 14, 14, 8)
            {
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                Bounciness = 0.25f,
                Drag = 0.85f,
                AbsorbPercentage = 0.75f
            };

            var stateMove = new AiState(UpdateMoving);
            stateMove.Trigger.Add(new AiTriggerRandomTime(ChangeDirection, 350, 650));
            var stateTrap = new AiState(UpdateTrap);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("moving", stateMove);
            _aiComponent.States.Add("trap", stateTrap);
            new AiFallState(_aiComponent, _body, OnHoleAbsorb);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };
            _damageState.OnDeath = OnDeath;

            // Start moving without a delay to trapping the player.
            ToMoving(true);

            // Start randomly idle or walking facing a random direction.
            _direction = Game1.RandomNumber.Next(0, 4);

            var boxHittable = new CBox(EntityPosition, -8, -16, 16, 16, 8);
            _collisionBox = new CBox(EntityPosition, -5, -10, 10, 8, 2);

            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(boxHittable, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
        }

        public override void Reset()
        {
            _animator.Continue();
            _collisionTimer = 0;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("moving");
            _aiComponent.ChangeState("moving");
            _damageState.CurrentLives = EnemyLives.LikeLike;
            _animator.SpeedMultiplier = 1f;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void UpdatePosition(CPosition newPosition)
        {
            if (_hasPlayerTrapped)
                MapManager.ObjLink.SetPosition(newPosition.Position);
        }

        private void ToMoving(bool init)
        {
            ChangeDirection();
            _animator.SpeedMultiplier = 1.0f;
            _aiComponent.ChangeState("moving");
            _hasPlayerTrapped = false;
            _body.Drag = 0.85f;
            _collisionTimer = init ? 0 : 1750;
        }

        private void UpdateMoving()
        {
            if (_collisionTimer > 0)
                _collisionTimer -= Game1.DeltaTime;

            // collided with the player?
            if (_collisionTimer <= 0 && !MapManager.ObjLink.IsTrapped() &&
                _collisionBox.Box.Intersects(MapManager.ObjLink.Body.BodyBox.Box))
                ToTrap();
        }

        private void ToTrap()
        {
            MapManager.ObjLink.TrapPlayer();
            MapManager.ObjLink.SetPosition(new Vector2(EntityPosition.Position.X, EntityPosition.Y - 1));

            if (!_stoleShield)
            {
                // Steal the shield from Link if it's a level 1 shield.
                _stoleShield = MapManager.ObjLink.StealShield();

                // If the shield was stolen, set the drop table to return it and disable powerups.
                if (_stoleShield)
                {
                    _damageState.SpawnPowerups = false;
                    _damageState.DropTableIndex = 13;
                }
            }
            _animator.SpeedMultiplier = 2.0f;
            _aiComponent.ChangeState("trap");
            _body.VelocityTarget = Vector2.Zero;
            _body.Drag = 0;
            _hasPlayerTrapped = true;
        }

        private void UpdateTrap()
        {
            var linkPos = MapManager.ObjLink.Position;
            var likePos = new Vector2(EntityPosition.Position.X, EntityPosition.Y - 1);

            if (!MapManager.ObjLink.IsTrapped())
                ToMoving(false);

            else if (MapManager.ObjLink.IsTrapped() && linkPos != likePos)
                MapManager.ObjLink.SetPosition(likePos);
        }

        private void ChangeDirection()
        {
            // random new direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * _moveSpeed;
        }

        private void OnDeath(bool pieceOfPower)
        {
            _damageState.BaseOnDeath(pieceOfPower);

            // free the player
            if (_hasPlayerTrapped)
                MapManager.ObjLink.FreeTrappedPlayer();
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            if (_aiComponent.CurrentStateId != "moving")
                return;

            if ((direction & (Values.BodyCollision.Horizontal | Values.BodyCollision.Vertical)) != 0)
            {
                _direction = (_direction + 2) % 4;
                _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * _moveSpeed;
            }
        }

        private void OnHoleAbsorb()
        {
            _animator.SpeedMultiplier = 3f;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_hasPlayerTrapped && (hitType & HitType.Sword) != 0)
                return Values.HitCollision.None;

            if (_hasPlayerTrapped && (hitType == HitType.Boomerang || hitType == HitType.Bow ||
                                      hitType == HitType.Hookshot || hitType == HitType.MagicRod))
            {
                _hasPlayerTrapped = false;
                MapManager.ObjLink.FreeTrappedPlayer();
                direction = MapManager.ObjLink.ForwardVector;
            }
            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }
    }
}
