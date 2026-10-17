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
    internal class EnemyLeever : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _bodyDrawComponent;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly Rectangle _fieldPosition;

        private const float MoveSpeed = 0.5f;
        private int _lives = EnemyLives.Leever;
        private int _dropIndex = 4;

        public EnemyLeever() : base("leever") { }

        public EnemyLeever(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-50, -50 - 8, 100, 100);
            CanReset = true;
            OnReset = Reset;

            _fieldPosition = map.GetField(posX, posY);

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/leever");
            _animator.Play("move");

            _sprite = new CSprite(EntityPosition);
            _sprite.IsVisible = false;

            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall,
                Bounciness = 0.25f,
                Drag = 0.85f,
            };

            var stateInit = new AiState();
            stateInit.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("hidden"), 750, 1500));
            var stateHidden = new AiState();
            var stateSpawning = new AiState(UpdateSpawning);
            var stateMoving = new AiState(UpdateMoving);
            stateMoving.Trigger.Add(new AiTriggerRandomTime(ToLeaving, 2000, 3000));
            var stateLeaving = new AiState(UpdateLeaving);

            var stateWaiting = new AiState();
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(Spawn, 1000, 2000));

            var stateRespawn = new AiState();
            stateRespawn.Trigger.Add(new AiTriggerRandomTime(Spawn, 5, 10));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("init", stateInit);
            _aiComponent.States.Add("hidden", stateHidden);
            _aiComponent.States.Add("spawning", stateSpawning);
            _aiComponent.States.Add("moving", stateMoving);
            _aiComponent.States.Add("leaving", stateLeaving);
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("respawn", stateRespawn);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            _aiComponent.ChangeState("init");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4, true);
            var hittableBox = new CBox(EntityPosition, -7, -14, 0, 14, 14, 8);
            var spawnRectangle = new Rectangle(posX + 8 + EntitySize.X, posY + 16 + EntitySize.Y, EntitySize.Width, EntitySize.Height);

            AddComponent(ObjectCollisionComponent.Index, new ObjectCollisionComponent(spawnRectangle, OnEnterSpawnArea));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(DrawComponent.Index, _bodyDrawComponent = new BodyDrawComponent(_body, _sprite, Values.LayerPlayer) { IsActive = false });
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite));

            Deactivate();
        }

        public override void Reset()
        {
            Deactivate();
            _bodyDrawComponent.IsActive = false;
            _sprite.IsVisible = false;
            _animator.Continue();
            _damageField.IsActive = false;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("init");
            _aiComponent.ChangeState("init");
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void OnEnterSpawnArea(GameObject gameObject)
        {
            // spawn if the player enters the spawnRectangle
            if (_aiComponent.CurrentStateId == "hidden")
                Spawn();
        }

        private void Activate()
        {
            _damageField.IsActive = true;
            _body.IsActive = true;
        }

        private void Deactivate()
        {
            _damageState.IsActive = false;
            _damageField.IsActive = false;
            _body.IsActive = false;
        }

        private void Spawn()
        {
            // Find a position.
            var newPosition = new Vector2(
                _fieldPosition.X + Game1.RandomNumber.Next(0, 10) * 16,
                _fieldPosition.Y + Game1.RandomNumber.Next(0, 8) * 16);

            // Make sure to not spawn directly at the player.
            var playerDistance = MapManager.ObjLink.Position - new Vector2(newPosition.X + 8, newPosition.Y + 16);
            if (playerDistance.Length() < 16)
                return;

            // Check the field state is not inside water.
            var fieldState = Map.GetFieldState(newPosition);
            bool fieldStateCheck = (fieldState & (MapStates.FieldStates.Water | MapStates.FieldStates.DeepWater)) == 0;

            // Check if the new position is not colliding with with the wall, other enemies, or the player.
            var posBox = new Box(newPosition.X, newPosition.Y, 0, 16, 16, 16);
            var refBox = Box.Empty;
            var colTypes = Values.CollisionTypes.Normal | Values.CollisionTypes.NPCWall | Values.CollisionTypes.Enemy | Values.CollisionTypes.Player;
            bool collisionCheck = !Map.Objects.Collision(posBox, Box.Empty, colTypes, 0, 0, ref refBox);

            // If the position is valid, spawn the leever. 
            if (fieldStateCheck && collisionCheck)
            {
                EntityPosition.Set(newPosition + new Vector2(8, 16));
                ToSpawning();
            }
            // If it is not, rapid fire searching for a new a position.
            else
                _aiComponent.ChangeState("respawn");
        }

        private void ToSpawning()
        {
            _aiComponent.ChangeState("spawning");

            _bodyDrawComponent.IsActive = true;
            _sprite.IsVisible = true;

            _animator.Play("spawn");
        }

        private void UpdateSpawning()
        {
            if (_animator.CurrentFrameIndex > 0)
                _damageState.IsActive = true;

            if (!_animator.IsPlaying)
                ToMoving();
        }

        private void ToMoving()
        {
            _aiComponent.ChangeState("moving");

            Activate();

            _animator.Play("move");
        }

        private void UpdateMoving()
        {
            // move in the direction of the player
            var direction = MapManager.ObjLink.Position - EntityPosition.Position;
            direction.Normalize();
            _body.VelocityTarget = direction * MoveSpeed;
        }

        private void ToLeaving()
        {
            _aiComponent.ChangeState("leaving");

            Deactivate();

            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("leave");
        }

        private void UpdateLeaving()
        {
            if (_animator.CurrentFrameIndex > 1)
                _damageState.IsActive = false;

            if (!_animator.IsPlaying)
                ToWaiting();
        }

        private void ToWaiting()
        {
            _aiComponent.ChangeState("waiting");

            _bodyDrawComponent.IsActive = false;
            _sprite.IsVisible = false;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (!_body.IsActive)
                return false;

            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
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
