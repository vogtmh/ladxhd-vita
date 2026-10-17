using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjLamp : GameObject
    {
        private readonly Animator _animator;
        private readonly int _animationLength;

        private readonly string _lampKey;
        private readonly bool _powderLamp;

        private float _lampState = 1.0f;
        private float _liveTime;

        private bool _lampKeyState = true;
        private bool _emitsLight = true;

        // Values configurable via lahdmod.
        private int   powder_time  = 9000;
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 200;
        private int   light_blu    = 200;
        private float light_bright = 1.00f;
        private int   light_size   = 160;

        public ObjLamp(Map.Map map, int posX, int posY, string animationName, int rotation, bool hasCollision, bool powderLamp, string lampKey, bool emitslight) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjLamp.lahdmod");
            ModFile.Parse(modFile, this);

            EntityPosition = new CPosition(posX, posY + 8, 0);

            Tags = Values.GameObjectTag.Lamp;

            EntitySize = new Rectangle(8 - light_size / 2, -8 - light_size / 2, light_size, light_size);

            _animator = AnimatorSaveLoad.LoadAnimator(animationName);
            if (_animator == null)
            {
                IsDead = true;
                return;
            }
            _animator.Play("idle");

            SprEditorImage = _animator.SprTexture;
            EditorIconSource = _animator.CurrentFrame.SourceRectangle;

            foreach (var frame in _animator.CurrentAnimation.Frames)
                _animationLength += frame.FrameTime;

            EditorIconSource = _animator.CurrentFrame.SourceRectangle;

            var sprite = new CSprite(EntityPosition)
            {
                Rotation = (float)Math.PI / 2 * rotation,
                Center = new Vector2(8, 8)
            };

            // connect animation to sprite
            new AnimationComponent(_animator, sprite, new Vector2(8, 0));

            if (hasCollision)
            {
                var collisionBox = new CBox(posX, posY, 0, 16, 16, 16);
                AddComponent(CollisionComponent.Index, new BoxCollisionComponent(collisionBox, Values.CollisionTypes.Normal | Values.CollisionTypes.ThrowWeaponIgnore));
                AddComponent(CarriableComponent.Index, new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(1, -7, 14, 15)), null, null, null) { IsCollision = true });
            }
            _emitsLight = emitslight;
            _powderLamp = powderLamp;

            if (_powderLamp)
            {
                // the collision box is a little bit smaller so that we cant light up two lamps at the same time
                var collisionBox = new CBox(posX + 1, posY + 2, 0, 14, 14, 16);
                AddComponent(HittableComponent.Index, new HittableComponent(collisionBox, OnHit));
                if (!string.IsNullOrEmpty(lampKey))
                {
                    _lampKey = lampKey;
                    Game1.GameManager.SaveManager.SetString(_lampKey, "0");
                }
            }
            else
            {
                // lamp can be turned on/off by setting the lamp key
                if (!string.IsNullOrEmpty(lampKey))
                {
                    _lampKey = lampKey;
                    AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
                }
            }
            // If it requires powder, start with it unlit.
            if (powderLamp)
                _lampState = 0;

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(sprite, hasCollision ? Values.LayerPlayer : Values.LayerBottom));

            if (emitslight)
                AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));

            // The lamps inside the egg must be always animated for classic camera.
            if (lampKey?.Contains("egg_lamps") == true)
                Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public bool IsOn()
        {
            return _liveTime > 0;
        }

        private void OnKeyChange()
        {
            var keyState = Game1.GameManager.SaveManager.GetString(_lampKey);
            var newKeyState = keyState == "1";

            if (!_lampKeyState && newKeyState)
            {
                // play sound effect
                Game1.AudioManager.PlaySoundEffect("D378-18-12");
            }

            _lampKeyState = newKeyState;
            _animator.Play(_lampKeyState ? "idle" : "dead");
        }

        private void Update()
        {
            // @HACK: this is used to sync all the animations with the same length
            // otherwise they would not be in sync if they did not get updated at the same time
            _animator.SetFrame(0);
            _animator.SetTime(Game1.TotalGameTime % _animationLength);
            _animator.Update();

            if (_powderLamp)
                UpdatePowderedLamp();
            else
                UpdateKeyLamp();
        }

        private void UpdatePowderedLamp()
        {
            // If the user set 0 it means to keep it lit forever.
            if (powder_time == 0)
            {
                _animator.Play("idle");
                return;
            }
            _liveTime -= Game1.DeltaTime;
            if (_liveTime < 0)
            {
                if (_lampKeyState)
                {
                    _lampKeyState = false;
                    if (!string.IsNullOrEmpty(_lampKey))
                        Game1.GameManager.SaveManager.SetString(_lampKey, "0");
                }

                _animator.Play("dead");
            }
            else
            {
                _animator.Play("idle");
            }

            _lampState = AnimationHelper.MoveToTarget(_lampState, _lampKeyState ? light_bright : 0, 0.1f * Game1.TimeMultiplier);
        }

        public void SetBrightness(float bright)
        {
            _lampState = bright;
        }

        private void UpdateKeyLamp()
        {
            _lampState = AnimationHelper.MoveToTarget(_lampState, _lampKeyState ? light_bright : 0, 0.075f * Game1.TimeMultiplier);
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (light_source && GameSettings.ObjectLights)
            {
                Rectangle _lightRectangle = new Rectangle((int)EntityPosition.X + 8 - light_size / 2, (int)EntityPosition.Y + 8 - light_size / 2, light_size, light_size);
                spriteBatch.Draw(Resources.SprLight, _lightRectangle, new Color(light_red, light_grn, light_blu) * _lampState);
            }
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (hitType == HitType.MagicPowder || hitType == HitType.MagicRod)
            {
                _liveTime = powder_time;

                // play sound effect
                Game1.AudioManager.PlaySoundEffect("D378-18-12");

                _lampKeyState = true;
                if (!string.IsNullOrEmpty(_lampKey))
                    Game1.GameManager.SaveManager.SetString(_lampKey, "1");

                return Values.HitCollision.Blocking;
            }

            if ((hitType & HitType.Sword) != 0)
                return Values.HitCollision.None;

            return Values.HitCollision.NoneBlocking;
        }
    }
}
