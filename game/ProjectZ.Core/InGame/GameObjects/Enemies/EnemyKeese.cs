using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyKeese : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly float _turnSpeed;

        private float _flyState;
        private int _dir;
        private int _lives = EnemyLives.Keese;
        private int _dropIndex = 2;

        public EnemyKeese() : base("keese") { }

        public EnemyKeese(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            _turnSpeed = Game1.RandomNumber.Next(20, 30) / 1000f;
            _flyState = (float)Math.PI / 2 * Game1.RandomNumber.Next(0, 4);
            _dir = Game1.RandomNumber.Next(0, 2) * 2 - 1;

            EntityPosition = new CPosition(posX + 8, posY + 24, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 24, 0);
            EntitySize = new Rectangle(-8, -24, 16, 24);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/keese");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animatorComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -21));

            _body = new BodyComponent(EntityPosition, -6, -20, 12, 8, 8)
            {
                IgnoresZ = false,
                IgnoreHoles = true,
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.Field,
                AvoidTypes     = Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.Field,
                MoveCollision = OnCollision
            };

            _aiComponent = new AiComponent();

            var stateIdle = new AiState(UpdateIdle);
            var stateCooldown = new AiState();
            stateCooldown.Trigger.Add(new AiTriggerRandomTime(StartIdle, 1000, 2000));
            var stateFlying = new AiState(UpdateFlying);
            stateFlying.Trigger.Add(new AiTriggerRandomTime(StartCooldown, 1000, 2000));

            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("cooldown", stateCooldown);
            _aiComponent.States.Add("flying", stateFlying);
            new AiFallState(_aiComponent, _body, null, null, 0);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            _aiComponent.ChangeState("cooldown");

            var damageBox   = new CBox(EntityPosition, -3, -18, 2, 6, 4, 4);
            var pushableBox = new CBox(EntityPosition, -4, -18, 0, 8, 6, 4);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animatorComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer) { WaterOutline = false });
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
        }

        public override void Reset()
        {
            _body.IgnoreHoles = true;
            _body.VelocityTarget = Vector2.Zero;
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("cooldown");
            _aiComponent.ChangeState("cooldown");
            _animator.Play("idle");
        }

        private void OnBurn()
        {
            _body.IgnoreHoles = false;
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void StartIdle()
        {
            _aiComponent.ChangeState("idle");
        }

        private void UpdateIdle()
        {
            var distVec = EntityPosition.Position - new Vector2(MapManager.ObjLink.PosX, MapManager.ObjLink.PosY + 16);

            // start flying if the player is near the keese
            if (distVec.Length() < 60)
                StartFlying();
        }

        private void StartFlying()
        {
            _aiComponent.ChangeState("flying");
            _dir = Game1.RandomNumber.Next(0, 2) * 2 - 1;
        }

        private void UpdateFlying()
        {
            _animator.Play("fly");

            _flyState += _dir * _turnSpeed * Game1.TimeMultiplier;
            var vecDirection = new Vector2((float)Math.Sin(_flyState), (float)Math.Cos(_flyState));

            _body.VelocityTarget = vecDirection * 0.75f;
        }

        private void StartCooldown()
        {
            _aiComponent.ChangeState("cooldown");
            _animator.Play("idle");
            _body.VelocityTarget = Vector2.Zero;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            _flyState += (float)Math.PI;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
            {
                _body.Velocity.X = direction.X * 2.5f;
                _body.Velocity.Y = direction.Y * 2.5f;
            }
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
