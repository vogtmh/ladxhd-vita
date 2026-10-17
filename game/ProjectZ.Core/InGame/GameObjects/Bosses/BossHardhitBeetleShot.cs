using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    internal class BossHardhitBeetleShot : GameObject
    {
        private readonly CSprite _sprite;
        private BodyComponent _body;
        private double _liveTime = 2000;
        private RectangleF _bossField;
        private BossHardhitBeetle _owner;

        public BossHardhitBeetleShot(Map.Map map, BossHardhitBeetle owner, Vector2 position, float speed, RectangleF bossField) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);

            _owner = owner;
            _bossField = bossField;

            var animator = AnimatorSaveLoad.LoadAnimator("Nightmares/hardhit beetle shot");
            animator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -5, -5, 10, 10, 8)
            {
                FieldRectangle = map.GetField(position.X, position.Y),
                FieldLocked = false,
                IgnoresZ = true,
                IgnoreHoles = true,
                CollisionTypes = Values.CollisionTypes.None
            };

            // move towards the player
            var velocity = MapManager.ObjLink.Position - EntityPosition.Position;
            if (velocity != Vector2.Zero)
                velocity.Normalize();
            _body.VelocityTarget = velocity * speed;

            var hittableBox = new CBox(EntityPosition, -6, -6, 0, 12, 12, 8);
            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(hittableBox, HitType.Enemy, 2) { OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerBottom));
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            _liveTime -= Game1.DeltaTime;

            if (_liveTime <= 125)
                _sprite.Color = Color.White * ((float)_liveTime / 125f);

            if (!_bossField.Contains(_body.BodyBox.Box.Rectangle()) || _liveTime <= 0)
                Map.Objects.DeleteObjects.Add(this);
        }

        private void OnDamagedPlayer()
        {
            // If it deals damage transfer to the main boss.
            if (_owner != null)
                _owner.OnDamagedPlayer();
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (hitType == HitType.MagicRod)
                return Values.HitCollision.None | Values.HitCollision.SpawnFire;

            return Values.HitCollision.RepellingParticle | Values.HitCollision.Repelling0;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            return true;
        }
    }
}
