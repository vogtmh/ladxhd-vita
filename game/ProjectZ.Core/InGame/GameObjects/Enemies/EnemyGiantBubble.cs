using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyGiantBubble : GameObject
    {
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;

        private bool _flashState;
        private double _lastFlashTime;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 255;
        private int   light_blu    = 255;
        private float light_bright = 0.50f;
        private int   light_size   = 64;

        public EnemyGiantBubble() : base("giant bubble") { }

        public EnemyGiantBubble(Map.Map map, int posX, int posY) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "EnemyGiantBubble.lahdmod");
            ModFile.Parse(modFile, this);

            // maybe create a new tag for enemies that should be ignored by the enemy trigger
            Tags = Values.GameObjectTag.Damage;

            EntityPosition = new CPosition(posX + 16, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 16, posY + 16, 0);
            EntitySize = new Rectangle(-32, -32, 64, 64);
            CanReset = true;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/giant bubble");
            _animator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -10, -10, 20, 20, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                MoveCollision = OnCollision,
                IgnoresZ = true,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NPCWall
            };

            // start with a random direction
            _body.VelocityTarget = new Vector2(Game1.RandomNumber.Next(0, 2) * 2 - 1, Game1.RandomNumber.Next(0, 2) * 2 - 1) * 0.7f;

            var damageBox = new CBox(EntityPosition, -9, -9, 0, 18, 18, 8);

            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer) { WaterOutline = false });
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
        }

        private void OnCollision(Values.BodyCollision collider)
        {
            if ((collider & Values.BodyCollision.Horizontal) != 0)
                _body.VelocityTarget.X = -_body.VelocityTarget.X;
            if ((collider & Values.BodyCollision.Vertical) != 0)
                _body.VelocityTarget.Y = -_body.VelocityTarget.Y;
        }

        private void Update()
        {
            var animationFramePercentage = _animator.FrameCounter / _animator.CurrentFrame.FrameTime;
            bool state = animationFramePercentage % 0.5 < 0.25;

            if (GameSettings.EpilepsySafe)
            {
                double totalSeconds = Game1.TotalGameTime * 0.001;
                if (totalSeconds - _lastFlashTime >= 0.50)
                {
                    _lastFlashTime = totalSeconds;
                    _flashState = !_flashState;
                }
                state = _flashState;
            }
            _sprite.SpriteShader = state ? Resources.DamageSpriteShader0 : null;
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (light_source && GameSettings.ObjectLights)
                if (_sprite.SpriteShader != null)
                    DrawHelper.DrawLight(spriteBatch, new Rectangle((int)EntityPosition.X - light_size / 2, (int)EntityPosition.Y - light_size / 2, light_size, light_size), new Color(light_red, light_grn, light_blu) * light_bright);
        }
    }
}
