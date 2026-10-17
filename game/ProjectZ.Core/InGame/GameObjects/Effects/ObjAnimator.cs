using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Effects
{
    internal class ObjAnimator : GameObject
    {
        public Animator Animator;
        public AnimationComponent AnimationComponent;
        public CSprite Sprite;

        enum DeathState { Alive, FadingLight, Dead, Deleted }

        private DeathState deathState = DeathState.Alive;

        private float fadeTimer = 0f;
        private float startBrightness;

        private bool  light_source = false;
        private int   light_red    = 255;
        private int   light_grn    = 220;
        private int   light_blu    = 100;
        private float light_bright = 0.80f;
        private int   light_size   = 32;
        private float light_fade   = 0.35f;

        public ObjAnimator(Map.Map map, int posX, int posY, int layer, string animatorName, string animationName, bool deleteOnFinish) : 
            this(map, posX, posY, 0, 0, layer, animatorName, animationName, deleteOnFinish) { }

        public ObjAnimator(Map.Map map, int posX, int posY, int layer, string animatorName, string animationName, bool deleteOnFinish, bool useLight, int red, int grn, int blu, float brightness, int lightArea, float fadeDuration = 0.35f) : 
            this(map, posX, posY, 0, 0, layer, animatorName, animationName, deleteOnFinish, useLight, red, grn, blu, brightness, lightArea, fadeDuration) { }

        public ObjAnimator(Map.Map map, int posX, int posY, int offsetX, int offsetY, int layer, string animatorName, string animationName, bool deleteOnFinish, bool useLight, int red, int grn, int blu, float brightness, int lightArea, float fadeDuration = 0.35f) : 
            this(map, posX, posY, offsetX, offsetY, layer, animatorName, animationName, deleteOnFinish)
        {
            light_source = useLight;
            light_red = red;
            light_grn = grn;
            light_blu = blu;
            light_bright = brightness;
            light_size = lightArea;
            light_fade = fadeDuration;
        }

        public ObjAnimator(Map.Map map, int posX, int posY, int offsetX, int offsetY, int layer, string animatorName, string animationName, bool deleteOnFinish) : base(map)
        {
            SprEditorImage = Resources.SprItem;
            EditorIconSource = new Rectangle(64, 168, 16, 16);
            EntityPosition = new CPosition(posX, posY, 0);
            Animator = AnimatorSaveLoad.LoadAnimator(animatorName);

            if (Animator == null)
            {
                System.Diagnostics.Debug.WriteLine("Error: could not load animation \"{0}\"", animatorName);
                IsDead = true;
                return;
            }
            Animator.Play(animationName);

            EntitySize = new Rectangle(
                offsetX + Animator.CurrentAnimation.Offset.X + Animator.CurrentAnimation.AnimationLeft,
                offsetY + Animator.CurrentAnimation.Offset.Y + Animator.CurrentAnimation.AnimationTop,
                Animator.CurrentAnimation.AnimationWidth, Animator.CurrentAnimation.AnimationHeight);

            Sprite = new CSprite(EntityPosition);
            AnimationComponent = new AnimationComponent(Animator, Sprite, new Vector2(offsetX, offsetY));

            if (deleteOnFinish)
            {
                Animator.OnAnimationFinished = () =>
                {
                    deathState = DeathState.FadingLight;
                    Sprite.IsVisible = false;
                    fadeTimer = 0f;
                    startBrightness = light_bright;
                };
            }
            AddComponent(BaseAnimationComponent.Index, AnimationComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(Sprite, layer));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        protected virtual void Update()
        {
            if (deathState == DeathState.FadingLight)
            {
                fadeTimer += Game1.DeltaTime / 1000f;
                float t = fadeTimer / light_fade;
                t = MathHelper.Clamp(t, 0f, 1f);
                light_bright = MathHelper.Lerp(startBrightness, 0f, t);
                if (t >= 1f)
                    deathState = DeathState.Dead;
            }

            if (deathState == DeathState.Dead)
            {
                deathState = DeathState.Deleted;
                Map.Map mapRef = Map ?? MapManager.ObjLink.Map;
                mapRef.Objects.DeleteObjects.Add(this);
            }
        }

        protected void ConfigureLight(bool useLight, int red, int grn, int blu, float brightness, int size, float fadeDuration)
        {
            light_source = useLight;
            light_red = red;
            light_grn = grn;
            light_blu = blu;
            light_bright = brightness;
            light_size = size;
            light_fade = fadeDuration;
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (!GameSettings.ObjectLights || !light_source || light_bright <= 0f)
                return;
            Rectangle rect = new Rectangle((int)EntityPosition.X - light_size / 2, (int)EntityPosition.Y - light_size / 2, light_size, light_size);
            DrawHelper.DrawLight(spriteBatch, rect, new Color(light_red, light_grn, light_blu) * light_bright);
        }
    }
}
