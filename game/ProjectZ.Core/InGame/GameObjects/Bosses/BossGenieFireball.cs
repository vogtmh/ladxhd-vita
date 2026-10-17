using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    internal class BossGenieFireball : GameObject
    {
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private BossGenie _owner;
        private bool _useShader;

        public BossGenieFireball(Map.Map map, BossGenie owner, Vector3 position, bool useShader) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(position);
            EntitySize = new Rectangle(-7, -64, 14, 64);

            _sprite = new CSprite("fireball", EntityPosition, new Vector2(-7, -16));
            _useShader = useShader;
            _owner = owner;

            _body = new BodyComponent(EntityPosition, -5, -10, 10, 10, 8)
            {
                IgnoresZ = true,
                DragAir = 1.0f,
                Gravity = 0.0f,
                FieldRectangle = map.GetField(position.X, position.Y),
                FieldLocked = false,
                CollisionTypes = Values.CollisionTypes.None,
                MoveCollision = OnCollision,
            };

            var damageCollider = new CBox(EntityPosition, -6, -12, 0, 12, 12, 8, true);
            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(damageCollider, HitType.Enemy, 4) { OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            Map.Objects.RegisterAlwaysAnimateObject(this);
            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        private void Update()
        {
            if (!_useShader)
                return;

            _sprite.SpriteShader = (Game1.TotalGameTime % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime) 
                ? Resources.DamageSpriteShader0 
                : null;
        }

        private void OnDamagedPlayer()
        {
            // If it deals damage transfer to the main boss.
            if (_owner != null)
                _owner.OnDamagedPlayer();
        }

        public void ThrowFireball(Vector3 velocity)
        {
            _body.IgnoresZ = false;
            _body.Velocity = velocity;
        }

        public void SetPosition(Vector3 newPosition)
        {
            EntityPosition.Set(newPosition);
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            Map.Objects.DeleteObjects.Add(this);
        }
    }
}
