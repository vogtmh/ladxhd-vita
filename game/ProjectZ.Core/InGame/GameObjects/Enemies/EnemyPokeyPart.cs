using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyPokeyPart : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;

        private int _collisionCount;
        private int _lives = EnemyLives.PokeyPart;
        private int _dropIndex = 2;

        public EnemyPokeyPart(Map.Map map, float posX, float posY, Vector2 velocityTarget, Vector3 velocity) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(-7, -14, 14, 14);
            CanReset = false;

            var sprite = new CSprite(Resources.SprEnemies, EntityPosition, new Rectangle(18, 369, 14, 14), new Vector2(-7, -14));

            _body = new BodyComponent(EntityPosition, -7, -14, 14, 14, 8)
            {
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                MoveCollision = OnCollision,
                Velocity = velocity,
                FieldRectangle = map.GetField(posX, posY),
                VelocityTarget = velocityTarget,
                Drag = 0.8f,
                DragAir = 0.8f,
                IgnoreHeight = true,
                IgnoresZ = true,
            };

            var stateSpawning = new AiState();
            stateSpawning.Trigger.Add(new AiTriggerCountdown(500, null, ToMoving));
            var stateMoving = new AiState();
            stateMoving.Trigger.Add(new AiTriggerCountdown(403, null, EnableDamageField));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("spawning", stateSpawning);
            _aiComponent.States.Add("moving", stateMoving);
            _aiComponent.ChangeState("spawning");
            _aiDamageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { IsActive = false };

            var damageBox = new CBox(EntityPosition, -3, -8, 0, 6, 6, 16);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2) { OnDamage = OnDamage, IsActive = false });
            AddComponent(HittableComponent.Index, new HittableComponent(_body.BodyBox, _aiDamageState.OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush) { RepelMultiplier = 0.2f });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new ShadowBodyDrawComponent(EntityPosition));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void ToMoving()
        {
            _aiComponent.ChangeState("moving");
            _aiDamageState.IsActive = true;
        }

        private void EnableDamageField()
        {
            _damageField.IsActive = true;
        }

        private bool OnDamage()
        {
            Despawn();
            return _damageField.DamagePlayer();
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            Despawn();

            return true;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            if ((direction & Values.BodyCollision.Horizontal) != 0)
            {
                _body.VelocityTarget.X = -_body.VelocityTarget.X;
                _body.Velocity = Vector3.Zero;
                _collisionCount++;
            }
            else if ((direction & Values.BodyCollision.Vertical) != 0)
            {
                _body.VelocityTarget.Y = -_body.VelocityTarget.Y;
                _body.Velocity = Vector3.Zero;
                _collisionCount++;
            }

            // despawn
            if (_collisionCount > 2)
            {
                Despawn();
            }
        }

        private void Despawn()
        {
            Map.Objects.DeleteObjects.Add(this);
            Map.Objects.SpawnObject(new ObjAnimator(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 16, Values.LayerPlayer, "Particles/spawn", "run", true));
            Game1.AudioManager.PlaySoundEffect("D360-47-2F");
        }
    }
}
