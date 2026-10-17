using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    internal class MBossDesertLanmolaHead : GameObject, IHasVisibility
    {
        private readonly Animator _animator;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageComponent;
        private readonly ShadowBodyDrawComponent _shadowComponent;
        private readonly MBossDesertLanmola _owner;

        private ObjSpriteShadow _spriteShadow;

        private int _direction;

        public CSprite Sprite => _sprite;

        public bool IsVisible { get; internal set; }

        public MBossDesertLanmolaHead(Map.Map map, MBossDesertLanmola owner, Vector2 position) : base(map)
        {
            IsVisible = true;
            EntityPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(-8, -48, 16, 48);
            CanReset = false;
            OnReset = Reset;

            _owner = owner;
            _animator = AnimatorSaveLoad.LoadAnimator("MidBoss/desertLanmola");
            _animator.Play("head_0");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(0, 0));

            var damageBox = new CBox(EntityPosition, -7, -15, 0, 14, 14, 8, true);
            AddComponent(DamageFieldComponent.Index, _damageComponent = new DamageFieldComponent(damageBox, HitType.Enemy, 4));

            var hittableBox = new CBox(EntityPosition, -7, -15, 0, 14, 14, 8, true);
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true, MagicRodMultiplier = true });
            AddComponent(AnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, _shadowComponent = new ShadowBodyDrawComponent(EntityPosition));

            _spriteShadow = new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            _spriteShadow.UpdateVisibility(false);
        }

        private new void Reset()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (!IsVisible)
                return Values.HitCollision.None;

            return _owner.OnHit(originObject, direction, hitType, damage, pieceOfPower);
        }

        public void Hide()
        {
            IsVisible = false;
            _sprite.IsVisible = false;
            _shadowComponent.IsActive = false;
            _damageComponent.IsActive = false;
        }

        public void Spawn(Vector2 direction)
        {
            IsVisible = true;
            _sprite.IsVisible = true;
            _shadowComponent.IsActive = true;
            _damageComponent.IsActive = true;

            if (Math.Abs(direction.Y) > Math.Abs(direction.X))
                _direction = direction.Y > 0 ? 3 : 1;
            else
                _direction = direction.X > 0 ? 2 : 0;

            _animator.Play("head_" + _direction);
        }

        public void SetDown()
        {
            _animator.Play("head_3");
        }

        public void Death()
        {
            _damageComponent.IsActive = false;
        }
    }
}
