using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyGopongaFlower : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly CBox _collisionBox;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private BoxCollisionComponent _collisionComponent;

        private readonly int _animationLength;
        private float _soundCooldown;
        private bool _blockSound = false;
        private bool _dealsDamage = true;
        private int _lives = EnemyLives.GopongaFlower;
        private int _dropIndex = 4;

        public EnemyGopongaFlower() : base("goponga flower") { }

        public EnemyGopongaFlower(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 8, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/goponga flower");
            _animator.Play("idle");

            foreach (var frame in _animator.CurrentAnimation.Frames)
                _animationLength += frame.FrameTime;

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -8));

            var body = new BodyComponent(EntityPosition, -8, -8, 16, 16, 8) 
            { 
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                IgnoresZ = true 
            };
            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", new AiState());
            _aiComponent.ChangeState("idle");
            _damageState = new AiDamageState(this, body, _aiComponent, sprite, _lives, _dropIndex) { HitMultiplierX = 0, HitMultiplierY = 0, OnBurn = OnBurn };

            var damageBox = new CBox(EntityPosition, -6, -6, 12, 12, 8);
            _collisionBox = new CBox(EntityPosition, -5, -5, 10, 10, 8);
            var hittableBox = new CBox(EntityPosition, -8, -8, 16, 16, 8);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4));
            AddComponent(CollisionComponent.Index, _collisionComponent = new BoxCollisionComponent(_collisionBox, Values.CollisionTypes.Enemy));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(body.BodyBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(body, sprite, Values.LayerPlayer) { WaterOutline = false });
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");

            if (_collisionComponent == null)
                AddComponent(CollisionComponent.Index, _collisionComponent = new BoxCollisionComponent(_collisionBox, Values.CollisionTypes.Enemy));
        }

        private void OnBurn()
        {
            _animator.Pause();
            _dealsDamage = false;
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
            RemoveComponent(CollisionComponent.Index);
            _collisionComponent = null;
        }

        private void Update()
        {
            if (_blockSound)
            {
                _soundCooldown += Game1.DeltaTime;
                if (_soundCooldown > 220) 
                {
                    _soundCooldown = 0;
                    _blockSound = false;
                }
            }
            // @HACK: this is used to sync all the animations with the same length
            // otherwise they would not be in sync if they did not get updated at the same time
            _animator.SetFrame(0);
            _animator.SetTime(Game1.TotalGameTime % _animationLength);
            _animator.Update();
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            return true;
        }

        private bool ValidateHit(HitType hitType, bool pieceOfPower)
        {
            // We can't directly compare pegausus sword hit to level 2 sword hit so just check the level.
            if ((hitType & HitType.PegasusBootsSword) != 0 && Game1.GameManager.SwordLevel == 2)
                return true;

            // What can kill these:
            // Bow-wow ; Hookshot ; Magic Rod ; Boomerang ; Sword2 + Spin Slash ; Sword2 + Piece of Power/Red Tunic
            if (hitType == HitType.BowWow || hitType == HitType.Hookshot || hitType == HitType.MagicRod ||  hitType == HitType.Boomerang ||
                ((hitType & HitType.Sword2) != 0 && (hitType & HitType.SwordSpin) != 0) || ((hitType & HitType.Sword2) != 0 && pieceOfPower))
            {
                return true;
            }
            return false;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If the hit type was validated damage can be dealt.
            if (ValidateHit(hitType, pieceOfPower))
            {
                // Determine when knockback is applied. BowWow damage and Magic Rod do not knockback the flower.
                if (hitType != HitType.BowWow && (hitType == HitType.MagicRod || damage >= _damageState.CurrentLives))
                {
                    _damageState.HitMultiplierX = 4;
                    _damageState.HitMultiplierY = 4;
                }
                // Do not register a hit on the flower if it's currently burning.
                if (_dealsDamage)
                {
                    if (_damageState.IsInDamageState())
                        _damageState.DamageTrigger.CurrentTime = 0;

                    // Register the hit.
                    var hit = _damageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);

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
                return Values.HitCollision.None;
            }
            // If damage is not dealt, play a "bump" sound.
            if (!_blockSound)
            {
                Game1.AudioManager.PlaySoundEffect("D360-09-09");
                _blockSound = true;
            }
            return Values.HitCollision.Blocking;
        }
    }
}
