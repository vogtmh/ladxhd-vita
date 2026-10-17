using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    internal class MBossBlainoGlove : GameObject
    {
        private readonly DamageFieldComponent _damageField;
        private readonly PushableComponent _pushComponent;
        private readonly HittableComponent _hitComponent;

        private readonly MBossBlaino _blaino;
        private readonly string _resetDoor;

        private int _hitDirection;

        public MBossBlainoGlove(Map.Map map, MBossBlaino blaino, Vector2 position, string resetDoor) : base(map)
        {
            EntityPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(0, 0, 11, 11);

            var damageBox = new CBox(EntityPosition, 0, 0, 0, 11, 11, 8);
            var hittableBox = new CBox(EntityPosition, 0, 0, 0, 11, 11, 8);
            var pushableBox = new CBox(EntityPosition, 0, 0, 0, 11, 11, 8);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4) { OnDamage = DamagePlayer, PushMultiplier = 2.25f });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));

            _blaino = blaino;
            _resetDoor = resetDoor;
        }

        public void SetHitDirection(int direction)
        {
            _hitDirection = direction;
        }

        public void SetPosition(Vector2 newPosition)
        {
            EntityPosition.Set(newPosition);
        }

        private bool DamagePlayer()
        {
            if (_blaino.CurrentAttack != BlainoAttack.None && MapManager.ObjLink.IsBlockingState() &&
                ((_hitDirection == -1 && MapManager.ObjLink.Direction != 0) || (_hitDirection == 1 && MapManager.ObjLink.Direction != 2)))
            {
                _blaino.GlovePush(new Vector2(-_hitDirection * 3.5f, 0));
                MapManager.ObjLink.Body.Velocity += new Vector3(_hitDirection * 3.5f, 0, 0);
                return false;
            }
            if (!_damageField.DamagePlayer())
                return false;

            switch (_blaino.CurrentAttack)
            {
                case BlainoAttack.Haymaker when _blaino.KnockoutWindow:
                    Game1.AudioManager.PlaySoundEffect("D360-11-0B");
                    MapManager.ObjLink.Knockout(new Vector2(_hitDirection * 0.75f, -1), _resetDoor);
                    break;
                case BlainoAttack.Lunge:
                    MapManager.ObjLink.Stun(MBossBlaino.StunTime, true);
                    break;
            }
            return true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_blaino.IsInvulnerable())
                return Values.HitCollision.None;

            if ((hitType & HitType.AnySword) == 0)
                return Values.HitCollision.None;

            if ((hitType & HitType.SwordSpin) != 0 || _blaino.PlayerFacingAway())
                return Values.HitCollision.None;

            _blaino.GloveParried(direction);
            return Values.HitCollision.Blocking;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            // The fist is not pushable while an attack is running.
            if (_blaino.CurrentAttack != BlainoAttack.None)
                return false;

            _blaino.OnPush(direction, type);
            return true;
        }

        public void OnDeath()
        {
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }
    }
}
