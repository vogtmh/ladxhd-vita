using Microsoft.Xna.Framework;
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
    internal class EnemyGoomba : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;
        
        private const float WalkSpeed = 0.5f;
        private float _directionCounter;
        private int _fadeTime = 75;
        private int _lives = EnemyLives.Goomba;
        private int _dropIndex = 2;

        public EnemyGoomba() : base("goomba") { }

        public EnemyGoomba(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/goomba");
            _animator.Play("walk");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, Map.Is2dMap ? -14 : -16));

            _body = new BodyComponent(EntityPosition, -6, -12, 12, 12, 8)
            {
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy |
                                 Values.CollisionTypes.NPCWall,
                AvoidTypes =     Values.CollisionTypes.Hole,
                FieldRectangle = map.GetField(posX, posY),
                Drag = 0.85f,
                DragAir = 0.85f,
                Gravity2D = 0.15f,
            };
            var dir = Game1.RandomNumber.Next(0, 2) * 2 - 1;
            _body.VelocityTarget.X = dir * WalkSpeed;

            var stateWalking = new AiState(UpdateWalking) { Init = InitWalking };
            var stateDead = new AiState();
            stateDead.Trigger.Add(new AiTriggerCountdown(1000 - _fadeTime, null, () => _aiComponent.ChangeState("fade")));
            var stateFade = new AiState() { Init = InitFade };
            stateFade.Trigger.Add(new AiTriggerCountdown(_fadeTime, DespawnTick, RemoveEntity));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("dead", stateDead);
            _aiComponent.States.Add("fade", stateFade);

            new AiFallState(_aiComponent, _body, OnHoleAbsorb);

            _aiComponent.ChangeState("walking");
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex) { OnBurn = OnBurn, HitMultiplierY = 1.0f };

            CBox damageCollider = Map.Is2dMap 
                ? new CBox(EntityPosition, -3, -8, 0, 6, 6, 4) 
                : new CBox(EntityPosition, -3, -10, 0, 6, 6, 4);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));

            if (Map.Is2dMap)
                Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _animator.Play("walk");
            _aiComponent.ChangeState("walking");
            _aiComponent.ChangeState("walking");
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void InitWalking()
        {
            int randomDirection;

            // 2D maps: randomly walk left or right. 3D maps: pick a random direction.
            if (Map.Is2dMap)
                randomDirection = Game1.RandomNumber.Next(0, 2) * 2;
            else
                randomDirection = Game1.RandomNumber.Next(0, 4);

            // Apply the velocity and start walking in the randomized direction.
            _body.VelocityTarget = AnimationHelper.DirectionOffset[randomDirection] * WalkSpeed;

            // Counter to change direction is only needed on 3D maps.
            if (!Map.Is2dMap)
                _directionCounter = Game1.RandomNumber.Next(750, 1500);
        }

        private void UpdateWalking()
        {
            // Link's body box and the Goomba box have collided.
            bool intersect = _body.BodyBox.Box.Intersects(MapManager.ObjLink.Body.BodyBox.Box);

            // On 2D maps, the player must be above the Goomba while falling when body boxes meet.
            bool squash2D = Map.Is2dMap &&
                MapManager.ObjLink.Body.Velocity.Y > 0 &&
                MapManager.ObjLink.EntityPosition.Y < EntityPosition.Y - 10;

            // On 3D maps, simply check if the player is falling. 
            bool squash3D = !Map.Is2dMap && 
                MapManager.ObjLink.Body.Velocity.Z < 0 &&
                MapManager.ObjLink.EntityPosition.Z > EntityPosition.Z + 4;

            // If the condition passes squash the Goomba.
            if (intersect && (squash2D || squash3D))
                JumpDeath();

            // If on a 2D map exit now.
            if (Map.Is2dMap)
                return;

            // On 3D maps the Goomba randomly changes direction.
            _directionCounter -= Game1.DeltaTime;

            // When the counter expires, restart the walking state which sets a direction.
            if (_directionCounter < 0)
                _aiComponent.ChangeState("walking");
        }

        private void InitFade()
        {
            var animation = new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y - 4, 0, 0, Values.LayerTop, "Particles/despawnParticle", "orange", true);
            Map.Objects.SpawnObject(animation);
        }

        private void DespawnTick(double time)
        {
            if (time <= _fadeTime)
                _sprite.Color = Color.White * (float)(time / _fadeTime);
        }

        private void RemoveEntity()
        {
            _damageState.NullifyDeathEffects();
            _damageState.DropTableIndex = 6;
            _damageState.BaseOnDeath(false);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);
            return true;
        }

        private void JumpDeath()
        {
            // Player jumped on the Goomba.
            Game1.AudioManager.PlaySoundEffect("D370-14-0E");

            // Make the player bounce slightly.
            if (Map.Is2dMap)
                MapManager.ObjLink.Body.Velocity.Y = -1.0f;
            else
                MapManager.ObjLink.Body.Velocity.Z = 1.0f;

            // Show it's squashed animation before removing.
            _animator.Play("dead");
            _aiComponent.ChangeState("dead");
            _body.VelocityTarget = Vector2.Zero;
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
        }

        private void OnHoleAbsorb()
        {
            _animator.SpeedMultiplier = 3f;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            // We don't care about collision unless it's walking.
            if (_aiComponent.CurrentStateId != "walking")
                return;

            // Reverse the direction when collision happens.
            if (Map.Is2dMap && (direction & Values.BodyCollision.Horizontal) != 0)
                _body.VelocityTarget.X = -_body.VelocityTarget.X;

            // Stop walking into the wall.
            if (!Map.Is2dMap && (direction & (Values.BodyCollision.Horizontal | Values.BodyCollision.Vertical)) != 0)
                _aiComponent.ChangeState("walking");
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Stop the goomba from moving when it's burned.
            if (hitType == HitType.MagicPowder || hitType == HitType.MagicRod)
                _body.VelocityTarget = Vector2.Zero;

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
