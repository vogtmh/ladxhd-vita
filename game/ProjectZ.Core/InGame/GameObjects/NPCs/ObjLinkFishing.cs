using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjLinkFishing : GameObject
    {
        private readonly Animator _animator;
        private readonly CSprite _sprite;

        private readonly Rectangle _hookSource = new Rectangle(384, 210, 16, 13);
        private readonly Rectangle _hookSourceHooked = new Rectangle(389, 211, 8, 8);
        private readonly Vector2 _hookStartPosition = new Vector2(117, 40);

        private Vector2 _hookVelocity;

        private int _fishCount;

        private bool _isFishing;
        private bool _isTransitioning;
        private bool _wasInWater;
        private bool _pulledOut;

        public static ObjFish HookedFish;
        public static Vector2 HookPosition;
        public static bool HasFish;

        public ObjLinkFishing(Map.Map map, int posX, int posY) : base(map)
        {
            SprEditorImage = Resources.SprLink;
            EditorIconSource = new Rectangle(313, 133, 25, 16);

            _animator = AnimatorSaveLoad.LoadAnimator("link fishing");
            _animator.Play("idle");

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            HookedFish = null;
            HookPosition = _hookStartPosition;
            HasFish = false;

            Game1.GameManager.SaveManager.SetString("leavePond", "no");
            Game1.GameManager.SaveManager.SetString("emptyPond", "0");

            // set camera target position
            map.CameraTarget = new Vector2(80, 64);

            MapManager.ObjLink.NextMapPositionStart = map.CameraTarget;
            MapManager.ObjLink.NextMapPositionEnd = map.CameraTarget;
            MapManager.ObjLink.TransitionInWalking = false;

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-18, -16));

            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));

            var marin = MapManager.ObjLink.GetMarin();
            if (marin != null)
                marin.IsVisible = false;
        }

        private void Update()
        {
            // hide the real player
            MapManager.ObjLink.UpdatePlayer = false;
            MapManager.ObjLink.IsVisible = false;

            var direction = new Vector2(122, 38) - HookPosition;

            if (!_isFishing)
            {
                // start hook throw animation
                if (ControlHandler.ButtonPressed(ControlHandler.ConfirmButton))
                {
                    _animator.Play("throw");
                }

                // set the hook position to the animation
                if (_animator.IsPlaying && _animator.CurrentAnimation.Id == "throw" && _animator.CurrentFrameIndex <= 5)
                {
                    HookPosition = EntityPosition.Position + new Vector2(-18, -16) + new Vector2(_animator.CollisionRectangle.X, _animator.CollisionRectangle.Y);
                }

                // throw hook
                if (_animator.CurrentFrameIndex == 5)
                {
                    Game1.AudioManager.PlaySoundEffect("D360-08-08");
                    _hookVelocity = new Vector2(-2.0f, -1f);
                    _isFishing = true;
                    HasFish = false;
                }
            }

            if (_isFishing)
            {
                if (!_pulledOut)
                {
                    if (!_animator.IsPlaying)
                    {
                        _animator.Play(HookedFish == null ? "idle" : "hooked");
                    }

                    if (ControlHandler.ButtonPressed(ControlHandler.ConfirmButton) && _wasInWater)
                    {
                        _animator.Play(HookedFish == null ? "pull_right" : "hooked_pull");

                        if (!(HasFish && HookedFish == null) && direction.Length() <= 4)
                        {
                            PulledInHook();
                            return;
                        }

                        // pull in the hook
                        if (direction != Vector2.Zero)
                            direction.Normalize();
                        _hookVelocity = direction * 2.0f;
                    }
                }

                if (_pulledOut)
                {
                    if (!_animator.IsPlaying)
                    {
                        // show victory message
                        Game1.GameManager.StartDialogPath(HookedFish.DialogName);

                        ResetFishing();

                        // remove the fish from the map
                        Map.Objects.DeleteObjects.Add(HookedFish);
                        HookedFish = null;
                    }
                }
                // not fish hooked
                else if (HookedFish == null)
                {
                    var moveVec = ControlHandler.GetMoveVector2();
                    var moveDir = AnimationHelper.GetDirection(moveVec);

                    // pull the hook up
                    if (HookPosition.Y >= 40 && ControlHandler.ButtonPressed(CButtons.Right))
                    {
                        _animator.Play("pull_up");
                        _hookVelocity = new Vector2(0, -2.0f);
                    }

                    // hook is in the air?
                    if (HookPosition.Y < 32)
                    {
                        _hookVelocity *= (float)Math.Pow(0.99, Game1.TimeMultiplier);
                        _hookVelocity.Y += 0.035f * Game1.TimeMultiplier;

                        if (moveDir == 2 && _hookVelocity.X < 0)
                            _hookVelocity.X += 0.05f * Game1.TimeMultiplier;
                    }
                    else
                    {
                        _hookVelocity *= (float)Math.Pow(0.75, Game1.TimeMultiplier);
                        _hookVelocity.Y += 0.05f * Game1.TimeMultiplier;

                        if (!_wasInWater)
                        {
                            _wasInWater = true;
                            var splashAnimator = new ObjAnimator(Map, 0, 0, 0, 36, 0, "Particles/fishingSplash", "idle", true);
                            splashAnimator.EntityPosition.Set(new Vector2(HookPosition.X + 2, 0));
                            Map.Objects.SpawnObject(splashAnimator);
                        }
                    }

                    HookPosition += _hookVelocity * Game1.TimeMultiplier;

                    // floor
                    if (HookPosition.Y > 110)
                    {
                        HookPosition.Y = 110;
                        _hookVelocity.Y = 0;
                    }

                    // notify fish inside the hook range
                    var interactBox = new Box(HookPosition.X - 8, HookPosition.Y - 1, 0, 22, 6, 8);
                    Map.Objects.InteractWithObject(interactBox);
                }
                else
                {
                    // Fluctuate the strength of a button press to give the fish the appearance of having more "fight" to them.
                    float pullStrength = (HookedFish.Type == 0) 
                        ? GameMath.GetRandomFloat(0.10f, 0.20f)
                        : GameMath.GetRandomFloat(0.06f, 0.20f);

                    // I settled on 3 decimal places for a good amount of variation.
                    pullStrength = (float)Math.Round(pullStrength, 3);

                    HookedFish.Body.Velocity.X += _hookVelocity.X * pullStrength;
                    HookedFish.Body.Velocity.Y += _hookVelocity.Y * pullStrength;
                    _hookVelocity = Vector2.Zero;

                    // lost the fish?
                    if (HookedFish.EntityPosition.X <= -8)
                    {
                        HasFish = false;
                        Map.Objects.DeleteObjects.Add(HookedFish);
                        HookedFish = null;
                        LoosedFish();
                    }
                }
            }
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            _sprite.Draw(spriteBatch);

            // Draw the colored tunic.
            var texture = _sprite.SprTexture;
            var cloakColor = Game1.GameManager.CloakColor;

            _sprite.Color = cloakColor * MapManager.ObjLink.SpriteTransparency;
            _sprite.SprTexture = Resources.SprLinkCloak;
            _sprite.Draw(spriteBatch);

            _sprite.Color = Color.White * MapManager.ObjLink.SpriteTransparency;
            _sprite.SprTexture = texture;

            if (_pulledOut)
                return;

            var source = _hookSource;

            // "Hook is up/down?" was the original comment. Not sure what this means yet.
            if (_hookVelocity.Y >= 0)
                source.Y += 16;

            // "Rotate color?" was the original comment. Not sure what this means yet either.
            else if (_isFishing && HookPosition.Y >= 28)
                source.X += 16 * (Game1.TotalGameTime % 200 < 100 ? 1 : 0);

            if (HookedFish == null)
                spriteBatch.Draw(Resources.SprLink, HookPosition - new Vector2(9, 6), source, Color.White);
            else
                spriteBatch.Draw(Resources.SprLink, HookPosition - new Vector2(4, 6), _hookSourceHooked, Color.White);
        }

        private void PulledInHook()
        {
            if (HookedFish != null)
            {
                _animator.Play("pullout");

                _pulledOut = true;

                IncrementFishCount();

                HookedFish.ToJump();
            }
            else
            {
                ResetFishing();
                Game1.GameManager.StartDialogPath("fishing_empty");
            }
        }

        private void LoosedFish()
        {
            ResetFishing();

            IncrementFishCount();

            Game1.GameManager.StartDialogPath("fishing_loss");
        }

        private void ResetFishing()
        {
            _pulledOut = false;
            _wasInWater = false;
            _isFishing = false;
            HookPosition = _hookStartPosition;
            _animator.Play("idle");
        }

        private void IncrementFishCount()
        {
            _fishCount++;
            // is the pond empty
            if (_fishCount >= 5)
                Game1.GameManager.SaveManager.SetString("emptyPond", "1");
        }

        private void KeyChanged()
        {
            // spawn object
            if (_isTransitioning || Game1.GameManager.SaveManager.GetString("leavePond") != "yes") return;

            _isTransitioning = true;

            MapManager.ObjLink.MapTransitionStart = MapManager.ObjLink.Position;
            MapManager.ObjLink.MapTransitionEnd = MapManager.ObjLink.Position;

            // append a map change
            ((MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)]).AppendMapChange("overworld.map", "pond");
        }
    }
}
