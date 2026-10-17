using System.IO;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    class EnemyFlameFountain : GameObject
    {
        private readonly Animator _animator;
        private int _lastFrameIndex;

        public struct LightSettings
        {
            public bool Shader;
            public bool Enabled;
            public int Red;
            public int Green;
            public int Blue;
            public float Brightness;
            public int Size;
        }
        private LightSettings _light = new LightSettings();

        // Values configurable via lahdmod.
        private bool  sprite_shader = true;
        private bool  light_source  = true;
        private int   light_red     = 255;
        private int   light_grn     = 200;
        private int   light_blu     = 200;
        private float light_bright  = 0.35f;
        private int   light_size    = 32;

        public EnemyFlameFountain() : base("flame fountain") { }

        public EnemyFlameFountain(Map.Map map, int posX, int posY) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "EnemyFlameFountain.lahdmod");
            ModFile.Parse(modFile, this);

            _light.Shader = sprite_shader;
            _light.Enabled = light_source;
            _light.Red = light_red;
            _light.Green = light_grn;
            _light.Blue = light_blu;
            _light.Brightness = light_bright;
            _light.Size = light_size;

            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);
            CanReset = false;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/flame fountain");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -8));

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(sprite, Values.LayerBottom));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // spawn a fireball
            if (_animator.CurrentFrameIndex == 1 && _lastFrameIndex == 0)
                Map.Objects.SpawnObject(new EnemyFlameFountainFireball(Map, new Vector2(EntityPosition.X, EntityPosition.Y + 8), new Vector2(0, 1), _light));
            
            _lastFrameIndex = _animator.CurrentFrameIndex;
        }
    }
}
