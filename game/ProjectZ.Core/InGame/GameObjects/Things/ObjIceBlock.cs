using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjIceBlock : GameObject
    {
        private readonly BoxCollisionComponent _collisionComponent;
        private readonly Animator _animator;
        private readonly CSprite _sprite;
        private readonly Rectangle _field;
        private readonly int _animationLength;

        private const int RespawnTime = 1000;
        private float _respawnCounter;
        private bool _isActive = true;

        int _posX;
        int _posY;

        public ObjIceBlock() : base("ice block") { }

        public ObjIceBlock(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);

            _field = map.GetField(posX, posY);

            _posX = posX;
            _posY = posY;

            _animator = AnimatorSaveLoad.LoadAnimator("Objects/ice block");
            _animator.Play("idle");

            _animationLength = _animator.GetAnimationTime(0, _animator.CurrentAnimation.Frames.Length);

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -8));

            var hittableBox = new CBox(EntityPosition, -7, -7, 0, 14, 14, 8, true);
            var collisionBox = new CBox(EntityPosition, -8, -8, 0, 16, 16, 16, true);

            AddComponent(CollisionComponent.Index, _collisionComponent =
                new BoxCollisionComponent(collisionBox, Values.CollisionTypes.Normal | Values.CollisionTypes.ThrowWeaponIgnore));
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, new PushableComponent(collisionBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerBottom));
        }

        private void Update()
        {
            // @HACK: this is used to sync all the animations with the same length
            // otherwise they would not be in sync if they did not get updated at the same time
            _animator.SetFrame(0);
            _animator.SetTime(Game1.TotalGameTime % _animationLength);
            _animator.Update();

            // respawn when the player leaves the room
            if (!_isActive && !_field.Contains(MapManager.ObjLink.CenterPosition))
            {
                _respawnCounter -= Game1.DeltaTime;

                if (_respawnCounter <= 0)
                {
                    // spawn explosion effect
                    Map.Objects.SpawnObject(new ObjAnimator(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 7, Values.LayerTop, "Particles/spawn", "run", true));

                    SetActive(true);
                }
            }
        }

        private void SetActive(bool state)
        {
            _isActive = state;

            _collisionComponent.IsActive = state;
            _sprite.IsVisible = state;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            if (!_isActive)
                return false;

            // Don't show the "Brrrr... this is a solid block of ice! it's very cold!" text if disabled.
            if (!GameSettings.NoHelperText)
                Game1.GameManager.StartDialogPath("ice_block");

            return false;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Don't let passive fire melt the ice blocks.
            if (gameObject.GetType() == typeof(ObjBurningEffect))
                return Values.HitCollision.None;

            if (!_isActive || (hitType & (HitType.Sword | HitType.PegasusBootsSword)) != 0)
                return Values.HitCollision.None;
            if (hitType != HitType.MagicRod)
                return Values.HitCollision.Repelling;

            Game1.AudioManager.PlaySoundEffect("D378-19-13");

            Map.Objects.SpawnObject(new ObjIceBlockRespawner(Map, _posX, _posY, false));
            var animation = new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 0, 0, Values.LayerPlayer, "Particles/ice block despawn", "run", true);
            Map.Objects.SpawnObject(animation);

            _respawnCounter = RespawnTime;

            SetActive(false);

            return Values.HitCollision.None;
        }
    }
}
