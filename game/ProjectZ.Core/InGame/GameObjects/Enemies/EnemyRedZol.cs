using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyRedZol : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AnimationComponent _animationComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly EnemyGel _gel0;
        private readonly EnemyGel _gel1;

        private float _jumpAcceleration = 1.5f;
        private bool _spawnSmallZols = true;
        private bool _multipleHits = false;
        private int _lives = EnemyLives.RedZol;
        private int _dropIndex = 2;

        public EnemyRedZol() : base("red zol") { }

        public EnemyRedZol(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 13, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 13, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/red zol");
            _animator.Play("walk_1");

            var sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-6, -16));

            _body = new BodyComponent(EntityPosition, -6, -10, 12, 10, 8)
            {
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.DeepWater,
                FieldRectangle = map.GetField(posX, posY),
                Gravity = -0.15f,
                Bounciness = 0.25f,
                Drag = 0.85f
            };

            var stateWaiting = new AiState { Init = InitWaiting };
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(EndWaiting, 200, 200));
            var stateWalking = new AiState(StateWalking) { Init = InitWalking };
            stateWalking.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("waiting"), 132, 132));
            var stateShaking = new AiState();
            stateShaking.Trigger.Add(new AiTriggerCountdown(1000, TickShake, ShakeEnd));
            var stateJumping = new AiState { Init = InitJumping };

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("shaking", stateShaking);
            _aiComponent.States.Add("jumping", stateJumping);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnDeath = OnDeath, OnBurn = OnBurn  };

            new AiFallState(_aiComponent, _body, null, null, 100);
            new AiDeepWaterState(_body) { FallTime = 50, OnDrown = OnDrown };

            _aiComponent.ChangeState("waiting");

            var damageBox   = new CBox(EntityPosition, -3,  -6, 0,  6,  4, 4);
            var hittableBox = new CBox(EntityPosition, -6, -10, 0, 12, 10, 8);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true, MagicRodMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite));

            // Spawn gels but inactive which is required for "Enemy Triggers" inside dungeons. Enemy Triggers are 
            // used for things such as spawning chest or opening doors when all enemies in the field are defeated.
            _gel0 = new EnemyGel(Map, posX, posY) { IsActive = false };
            Map.Objects.SpawnObject(_gel0);

            _gel1 = new EnemyGel(Map, posX, posY) { IsActive = false };
            Map.Objects.SpawnObject(_gel1);

            // Setting the position here prevents needing to subtract offsets later.
            Vector2 ZolRespawnPos = new Vector2(posX, posY);

            // The gels need to be able to track the main gel and if the other is still alive.
            _gel0.SetOtherGel(_gel1, false, ZolRespawnPos);
            _gel1.SetOtherGel(_gel0, true, ZolRespawnPos);

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void Reset()
        {
            _animator.Continue();
            _spawnSmallZols = true;
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("waiting");
            _aiComponent.ChangeState("waiting");
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void OnDrown()
        {
            Map.Objects.DeleteObjects.Add(_gel0);
            Map.Objects.DeleteObjects.Add(_gel1);
        }

        private void InitWaiting()
        {
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("idle");
        }

        private void EndWaiting()
        {
            // If Link is not on the field then hold position. 
            if (!_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
                return;

            if (Game1.RandomNumber.Next(0, 10) == 0)
                _aiComponent.ChangeState("shaking");
            else
                _aiComponent.ChangeState("walking");
        }

        private void InitWalking()
        {
            _animator.Play("walk");
        }

        private void StateWalking()
        {
            // walk to the player
            MoveToPlayer(0.4f);
        }

        private void TickShake(double time)
        {
            _animationComponent.SpriteOffset.X = -6 + (float)Math.Sin(time / 25f);
            _animationComponent.UpdateSprite();
        }

        private void ShakeEnd()
        {
            _animationComponent.SpriteOffset.X = -6;
            _animationComponent.UpdateSprite();

            _aiComponent.ChangeState("jumping");
        }

        private void InitJumping()
        {
            _animator.Play("walk");

            _body.Velocity.Z = _jumpAcceleration;

            // move to the player
            MoveToPlayer(1.25f);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void OnCollision(Values.BodyCollision type)
        {
            // hit the floor after a jump
            if ((type & Values.BodyCollision.Floor) != 0)
                _aiComponent.ChangeState("waiting");
        }

        private void MoveToPlayer(float speed)
        {
            var vecDirection = new Vector2(
                MapManager.ObjLink.PosX - EntityPosition.X,
                MapManager.ObjLink.PosY - EntityPosition.Y);

            if (vecDirection == Vector2.Zero)
                return;

            vecDirection.Normalize();
            _body.VelocityTarget = vecDirection * speed;
        }

        private void OnDeath(bool pieceOfPower)
        {
            _damageState.BaseOnDeath(pieceOfPower);

            if (!_spawnSmallZols)
            {
                Map.Objects.DeleteObjects.Add(_gel0);
                Map.Objects.DeleteObjects.Add(_gel1);
                return;
            }
            // positions are set so that the gels are inside of the body to not collide with stuff
            _gel0.EntityPosition.Set(new Vector2(EntityPosition.X - 1.9f - Game1.RandomNumber.Next(0, 2), EntityPosition.Y - Game1.RandomNumber.Next(0, 2)));
            _gel0.IsActive = true;
            _gel0.InitSpawn();
            _gel1.EntityPosition.Set(new Vector2(EntityPosition.X + 2.9f + Game1.RandomNumber.Next(0, 2), EntityPosition.Y - Game1.RandomNumber.Next(0, 2)));
            _gel1.IsActive = true;
            _gel1.InitSpawn();
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Check if it was hit with the sword.
            bool swordHit = (hitType & HitType.AnySword) != 0;

            // When hit with the sword, they don't get knocked back.
            if (swordHit)
                _damageState.MoveBody = false;

            // If the damage is greater than the number of lives they have kill it outright.
            if (damage > _lives)
            {
                // When killed with the sword, they explode instantly.
                if (swordHit)
                    _damageState.CooldownTime = 0;

                // If the modifier to add additional lives is used, the Zol may take more than one hit before it dies. This can
                // cause a weird situation where hitting with sword doesn't kill it, then using boomerang does kill it. Without
                // detecting this, the Zol will just disappear with no death effects and not spawn Gels.
                if (!_multipleHits)
                    _spawnSmallZols = false;

                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            // Spawn Gels if the damage is not over the amount of HP they have.
            else
            {
                _multipleHits = true;
                _damageState.MoveBody = false;
                _damageState.NullifyDeathEffects(true);
            }
            // In the original game, it does not get knocked back at all.
            _body.VelocityTarget = Vector2.Zero;

            return _damageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);
        }

        public void AddToEnemyTriggerGroup(ObjEnemyTrigger etrigger)
        {
            // If respawned in a room with an enemy trigger, this is a means 
            // to adding the two Gels spawned with the Zol to the trigger list.
            etrigger.EnemyTriggerList.Add(_gel0);
            etrigger.EnemyTriggerList.Add(_gel1);
        }
    }
}
