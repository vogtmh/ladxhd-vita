using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    class BossSlimeEelTail : GameObject
    {
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageFieldComponent;
        private BossSlimeEelSpawn _owner;

        public CSprite Sprite => _sprite;

        public BossSlimeEelTail() : base("slime eel") { }

        public BossSlimeEelTail(Map.Map map, BossSlimeEelSpawn owner, Vector2 position, int spriteIndex, HittableComponent.HitTemplate onHit) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            _owner = owner;

            EntityPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);

            _sprite = new CSprite("eel_tail_" + spriteIndex, EntityPosition, spriteIndex == 1 ? new Vector2(-7) : new Vector2(-8));

            var damageCollider = new CBox(EntityPosition, -6, -6, 12, 12, 2);

            AddComponent(DamageFieldComponent.Index, _damageFieldComponent = new DamageFieldComponent(damageCollider, HitType.Enemy, 6) { OnDamagedPlayer = OnDamagedPlayer });

            if (onHit != null)
            {
                var hittableCollider = new CBox(EntityPosition, -6, -6, 12, 12, 8);
                AddComponent(HittableComponent.Index, new HittableComponent(hittableCollider, onHit));
            }
        }

        private void OnDamagedPlayer()
        {
            // If it deals damage transfer to the main boss.
            _owner.DealtPlayerDamage = true;
        }

        public void SetActive(bool state)
        {
            _damageFieldComponent.IsActive = state;
        }
    }
}
