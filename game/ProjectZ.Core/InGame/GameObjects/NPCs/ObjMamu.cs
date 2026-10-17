using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjMamu : GameObject
    {
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _bodyDrawComponent;

        private readonly ObjPersonNew _leftFrog;
        private readonly ObjPersonNew _rightFrog;

        private ObjDungeonBlacker _blacker;

        private List<ObjLamp> _lamps = new List<ObjLamp>();
        private List<ObjLight> _spotLights = new List<ObjLight>();

        private Rectangle _interactRectangle;
        private bool _wasColliding;

        private readonly string _saveKey;
        private bool _drawDarkness;
        private float _darkOpacity;
        private bool _fadingOut;
        private float _startDelay;
        private float _songCounter;
        private int _songIndex;
        private bool _isPlaying;
        private bool _startedPlaying;

        struct AnimationKeyframe
        {
            public float Time;
            public string Left;
            public string Right;
            public string Middle;

            public AnimationKeyframe(float time, string left, string right, string middle)
            {
                Time = time;
                Left = left;
                Right = right;
                Middle = middle;
            }
        }

        private AnimationKeyframe[] _songKeyframes = new AnimationKeyframe[]
        {
            new AnimationKeyframe(0f   ,  "idle", "idle", "right"),
            new AnimationKeyframe(0.9f ,  "right", "idle", "right"),
            new AnimationKeyframe(1.85f,  "right", "right", "right"),
            new AnimationKeyframe(3.2f ,  "right", "right", "idleleft"),
            new AnimationKeyframe(3.75f,  "idle", "idle", "left"),
            new AnimationKeyframe(4.2f ,  "idle", "idle", "idleright"),
            new AnimationKeyframe(4.7f ,  "left", "idle", "right"),
            new AnimationKeyframe(5.15f,  "idle", "idle", "idleleft"),
            new AnimationKeyframe(5.65f,  "right", "left", "left"),
            new AnimationKeyframe(6.1f ,  "idle", "idle", "idleright"),
            new AnimationKeyframe(6.55f,  "left", "right", "right"),
            new AnimationKeyframe(7f   ,  "idle", "idle", "idleleft"),
            new AnimationKeyframe(7.5f ,  "right", "left", "left"),
            new AnimationKeyframe(8f   ,  "idle", "idle", "idleright"),
            new AnimationKeyframe(8.45f,  "left", "right", "right"),
            new AnimationKeyframe(8.9f ,  "idle", "idle", "idleleft"),
            new AnimationKeyframe(9.4f ,  "right", "left", "left"),
            new AnimationKeyframe(9.85f,  "idle", "idle", "idleright"),
            new AnimationKeyframe(10.3f,  "left", "right", "right"),
            new AnimationKeyframe(10.8f,  "idle", "idle", "idleleft"),
            new AnimationKeyframe(11.25f, "right", "left", "left"),
            new AnimationKeyframe(11.7f , "idle", "idle", "idleright"),
            new AnimationKeyframe(12.2f , "left", "right", "right"),
            new AnimationKeyframe(12.65f, "idle", "idle", "right"),
            new AnimationKeyframe(13.1f , "right", "left", "right"),
            new AnimationKeyframe(13.6f , "right", "idle", "right"),
            new AnimationKeyframe(14.1f , "right", "right", "right"),
            new AnimationKeyframe(14.1f , "right", "right", "right"),
            new AnimationKeyframe(14.5f , "right", "right", "idleright"),
            new AnimationKeyframe(15f   , "idle", "idle", "right"),
            new AnimationKeyframe(15.45f, "idle", "idle", "idleleft"),
            new AnimationKeyframe(15.95f, "right", "idle", "left"),
            new AnimationKeyframe(16.4f , "idle", "idle", "idleright"),
            new AnimationKeyframe(16.85f, "left", "right", "right"),
            new AnimationKeyframe(17.35f, "idle", "idle", "idleleft"),
            new AnimationKeyframe(17.8f , "right", "left", "left"),
            new AnimationKeyframe(18.3f , "idle", "idle", "idleright"),
            new AnimationKeyframe(18.75f, "left", "right", "right"),
            new AnimationKeyframe(19.3f , "idle", "idle", "idleleft"),
            new AnimationKeyframe(19.7f , "right", "left", "left"),
            new AnimationKeyframe(20.15f, "idle", "idle", "idleright"),
            new AnimationKeyframe(20.6f , "left", "right", "right"),
            new AnimationKeyframe(21.1f , "idle", "idle", "idleleft"),
            new AnimationKeyframe(21.55f, "right", "left", "left"),
            new AnimationKeyframe(22f   , "idle", "idle", "idleright"),
            new AnimationKeyframe(22.5f , "left", "right", "right"),
            new AnimationKeyframe(22.95f, "idle", "idle", "idleleft"),
            new AnimationKeyframe(23.4f , "right", "left", "left"),
            new AnimationKeyframe(23.9f , "idle", "idle", "left"),
            new AnimationKeyframe(24.4f , "left", "right", "left"),
            new AnimationKeyframe(24.85f, "left", "idle", "left"),
            new AnimationKeyframe(25.3f,  "left", "left", "left"),
            new AnimationKeyframe(26.3f,  "idle", "idle", "idle")
        };

        public ObjMamu() : base("mamu") { }

        public ObjMamu(Map.Map map, int posX, int posY, string saveKey) : base(map)
        {
            _saveKey = saveKey;
            if (!string.IsNullOrEmpty(_saveKey) &&
                Game1.GameManager.SaveManager.GetString(_saveKey) == "1")
            {
                IsDead = true;
                return;
            }

            EntityPosition = new CPosition(posX + 16, posY + 32, 0);
            EntitySize = new Rectangle(-16, -32, 32, 32);

            _interactRectangle = new Rectangle(posX + 16 - 12, posY + 16, 24, 32);

            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/mamu");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -16, -32, 32, 32, 8);
            _bodyDrawComponent = new BodyDrawComponent(_body, sprite, 1);

            var interactBox = new CBox(posX + 2, posY + 16, 0, 28, 16, 8);
            AddComponent(InteractComponent.Index, new InteractComponent(interactBox, OnInteract));

            AddComponent(BodyComponent.Index, _body);
            AddComponent(CollisionComponent.Index, new BodyCollisionComponent(_body, Values.CollisionTypes.Normal | Values.CollisionTypes.NPC));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));

            // Create the two frogs.
            _leftFrog = new ObjPersonNew(map, posX - 32, posY + 42, null, "singing frog", null, "idle", new Rectangle(0, 0, 14, 12));
            map.Objects.SpawnObject(_leftFrog);

            _rightFrog = new ObjPersonNew(map, posX + 48, posY + 42, null, "singing frog", null, "idle", new Rectangle(0, 0, 14, 12));
            map.Objects.SpawnObject(_rightFrog);

            // Create a "dungeon blacker" object to control lighting.
            _blacker = new ObjDungeonBlacker(Map, 0, 0, 255, 230, 200, 125) { IsDead = false };
            Map.Objects.SpawnObject(_blacker);

            // Get the lamps that are already on the game field.
            var gameObjects = new List<GameObject>();
            Map.Objects.GetGameObjectsWithTag(gameObjects, Values.GameObjectTag.Lamp, (int)EntityPosition.X - 64, (int)EntityPosition.Y - 64, 256, 256);
            foreach (var obj in gameObjects)
            {
                if (obj is ObjLamp lamp) 
                    _lamps.Add(lamp);
            }
            // Spawn spotlights on the frogs that are initially off.
            _spotLights.Add(new ObjLight(map, posX + 8, posY + 6, 54, 255, 255, 240, 0, Values.LayerPlayer));
            _spotLights.Add(new ObjLight(map, posX - 32, posY + 40, 36, 255, 255, 240, 0, Values.LayerPlayer));
            _spotLights.Add(new ObjLight(map, posX + 48, posY + 40, 36, 255, 255, 240, 0, Values.LayerPlayer));

            foreach (var spotLight in _spotLights)
                Map.Objects.SpawnObject(spotLight);
        }

        private bool OnInteract()
        {
            Game1.GameManager.StartDialogPath("mamu");

            return true;
        }

        private void OnKeyChange()
        {
            var startSing = Game1.GameManager.SaveManager.GetString("mamu_sing");
            if (!_startedPlaying && startSing == "1")
            {
                _startDelay = 2500;
                Game1.GameManager.SaveManager.RemoveString("mamu_sing");
            }
        }

        private void StartSong()
        {
            Game1.AudioManager.SetMusic(52, 2);
            _isPlaying = true;
            _startedPlaying = true;
            _drawDarkness = true;
        }

        private void Update()
        {
            // When Link gets close to Mamu start the dialog.
            if (!_startedPlaying && MapManager.ObjLink.IsGrounded())
            {
                var colliding = MapManager.ObjLink.BodyRectangle.Intersects(_interactRectangle);
                if (!_wasColliding && colliding)
                {
                    Game1.GameManager.StartDialogPath("mamu");
                }
                _wasColliding = colliding;
            }

            // When the delay is set count down.
            if (_startDelay != 0)
            {
                _startDelay -= Game1.DeltaTime;

                // When the delay hits zero start the song.
                if (_startDelay <= 0)
                {
                    _startDelay = 0;
                    _darkOpacity = 1f;
                    StartSong();
                }
                // Fade in darkness layer over 2.5 seconds.
                _darkOpacity = 1f - (_startDelay / 2500f);

                MapManager.ObjLink.FreezePlayer();
                Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;
            }

            // Fade out darkness layer over 1.5 seconds.
            if (_fadingOut)
            {
                _darkOpacity -= Game1.DeltaTime / 1500f;
                if (_darkOpacity <= 0)
                {
                    _darkOpacity = 0;
                    _fadingOut = false;
                }
            }
            // Apply lighting effects when "GlobalLights" is enabled.
            if (GameSettings.GlobalLights && _darkOpacity > 0)
            {
                // Fade out/fade in the brightness of the lamps.
                var lightOpacityA = 1f - (_darkOpacity * 0.95f);
                foreach (var lamp in _lamps)
                    lamp.SetBrightness(lightOpacityA);

                // Fade out/fade in the "dungeon blacker" object.
                var lightOpacityB = 1f - (_darkOpacity * 0.45f);
                _blacker.SetBrightness(lightOpacityB);

                // Fade in/fade out the spotlights.
                foreach (var spotLight in _spotLights)
                    spotLight.SetBrightness(_darkOpacity);
            }
            // Return early if not playing the song.
            if (!_isPlaying)
                return;

            // Freeze Link during the song and disable the inventory.
            MapManager.ObjLink.FreezePlayer();
            Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;

            // Keep track of how long the song played.
            _songCounter += Game1.DeltaTime;

            // Start a new key frame.
            if (_songCounter >= _songKeyframes[_songIndex].Time * 1000)
            {
                // Set the animations.
                _leftFrog.Animator.Play(_songKeyframes[_songIndex].Left);
                _animator.Play(_songKeyframes[_songIndex].Middle);
                _rightFrog.Animator.Play(_songKeyframes[_songIndex].Right);
                _songIndex++;

                // Song has finished. Stop song, fade out darkness layer, start ending dialog.
                if (_songIndex >= _songKeyframes.Length)
                {
                    _isPlaying = false;
                    _drawDarkness = false;
                    _fadingOut = true;
                    Game1.GameManager.StartDialogPath("mamu_finished");
                }
            }
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // Draw Mamu's body sprite.
            _bodyDrawComponent.Draw(spriteBatch);

            // We only do the sprite mask if global lighting is disabled.
            if (GameSettings.GlobalLights)
                return;

            // Draw the darkness texture from the original game.
            if (_drawDarkness || _darkOpacity > 0)
            {
                var color = Color.White * (_drawDarkness ? 1f : _darkOpacity);
                spriteBatch.Draw(Resources.SprMamuLight, new Vector2(16, 16), color);
            }
        }
    }
}
