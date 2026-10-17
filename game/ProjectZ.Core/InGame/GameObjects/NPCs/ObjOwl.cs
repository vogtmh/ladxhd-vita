using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjOwl : GameObject, IHasVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly BodyDrawComponent _drawComponent;
        private readonly BodyDrawShadowComponent _drawShadowComponent;
        private readonly CSprite _sprite;

        private Animator _animator;
        private CPosition _owlPosition;
        private ObjSpriteShadow _spriteShadow;

        // The curvature of the owl's flight paths.
        // https://cubic-bezier.com/#.17,.67,.83,.67
        private readonly CubicBezier _landingCurve = new CubicBezier(100, new Vector2(0.35f, 1f), new Vector2(0.8f, 1));
        private readonly CubicBezier _leavingCurve = new CubicBezier(100, new Vector2(0.25f, 0.04f), new Vector2(0.35f, 0.11f));

        private Vector3 _startPosition;
        private Vector3 _landPosition;
        private Vector3 _leavePosition;

        private readonly string _strKey;
        private readonly string _keyCondition;

        private float _airCount;
        private float _flySoundCount;
        private int _enterTime = 2000;

        private int originX;
        private int originY;

        private bool _isAlive;
        private bool _wasTriggered;
        private bool _triggerCollided;

        private bool _noIntro;
        private double _hoverCounter;

        // 0: Leave after talking (standard).
        // 1: Stay after talking (after landing on egg).
        // 2: Spawn from the top (not sure where/if this is used).
        private int _mode; 
        private bool _sitMode;
        private bool _hoverMode;

        public bool IsVisible { get; internal set; }

        public ObjOwl() : base("owl") { }

        public ObjOwl(Map.Map map, int posX, int posY, string keyCondition, Rectangle triggerRectangle, bool hoverMode, string strKey, int mode, bool noIntro = false) : base(map)
        {
            FreezePersist = true;
            IsVisible = false;

            _strKey = strKey;
            _keyCondition = keyCondition;
            _hoverMode = hoverMode;
            _mode = mode;
            _noIntro = noIntro;

            originX = posX + 8;
            originY = posY + 16;

            _owlPosition = new CPosition(_startPosition.X, _startPosition.Y, _startPosition.Z);

            var body = new BodyComponent(_owlPosition, -6, -8, 12, 8, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                FieldLocked = false,
                IgnoresZ = true
            };

            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/owl");
            _sprite = new CSprite(_owlPosition);

            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            var stateWait = new AiState(UpdateWait);
            var stateEnter = new AiState(UpdateEnter) { Init = InitEnter };
            var stateTalk = new AiState(UpdateTalk) { Init = InitTalking };
            var stateTalked = new AiState() { Init = InitTalked };
            var stateLeave = new AiState(UpdateLeave) { Init = InitLeave };
            var stateSit = new AiState(UpdateSit) { };
            var stateFadeout = new AiState(UpdateFadeout) { Init = InitFadeout };

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("wait", stateWait);
            _aiComponent.States.Add("enter", stateEnter);
            _aiComponent.States.Add("talk", stateTalk);
            _aiComponent.States.Add("talked", stateTalked);
            _aiComponent.States.Add("leave", stateLeave);
            _aiComponent.States.Add("sit", stateSit);
            _aiComponent.States.Add("fadeout", stateFadeout);
            _aiComponent.ChangeState("wait");

            AddComponent(BodyComponent.Index, body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, _drawComponent = new BodyDrawComponent(body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, _drawShadowComponent = new BodyDrawShadowComponent(body, _sprite) { OffsetY = 0 });
            var enterRectangle = new Rectangle(posX + 8 + triggerRectangle.X, posY + 8 + triggerRectangle.Y, triggerRectangle.Width, triggerRectangle.Height);
            AddComponent(ObjectCollisionComponent.Index, new ObjectCollisionComponent(enterRectangle, OnCollision));
            AddComponent(UpdateComponent.Index, new UpdateComponent(UpdateSpriteShadow));

            Map.Objects.RegisterAlwaysAnimateObject(this);

            _sprite.Color = Color.Transparent;
            _drawComponent.IsActive = false;
            _drawShadowComponent.IsActive = false;

            // Sitting infront of the egg.
            if (mode == 2)
            {
                if (Game1.GameManager.SaveManager.GetString(_keyCondition, "0") == "1")
                {
                    IsDead = true;
                    return;
                }
                _isAlive = true;
                _sitMode = true;
            }
            else
            {
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));

                // add key change listener to activate/deactivate the owl
                if (!string.IsNullOrEmpty(_keyCondition))
                    KeyChanged();
            }
        }

        private void PlaceOwlWaiting()
        {
            // Update the positions of the owl. 
            _landPosition = new Vector3(originX, originY, 0);
            _leavePosition = _hoverMode || _mode == 2
                ? new Vector3(originX, originY - 64, 64)
                : new Vector3(originX + 64, originY - 64, 64);

            // Place the owl at it's resting position and play idle. 
            _owlPosition.Set(_landPosition);
            _animator.Play("idle");

            // Make the owl visible.
            _sprite.Color = Color.White;
            _drawComponent.IsActive = true;
            IsVisible = true;
        }

        private void KeyChanged()
        {
            var value = Game1.GameManager.SaveManager.GetString(_strKey, "0");
            _isAlive = value == _keyCondition;

            // Spawn in the owl if it's not supposed to fly in. 
            if (_isAlive && _noIntro)
                PlaceOwlWaiting();
        }

        private void OnCollision(GameObject gameObject)
        {
            if (!_wasTriggered && _isAlive)
                _triggerCollided = true;
        }

        private void UpdateSpriteShadow()
        {
            if (_spriteShadow == null)
                _spriteShadow = new ObjSpriteShadow(Map, _owlPosition.Position.X-8, _owlPosition.Position.Y-14, Values.LayerPlayer, "sprshadowm");

            _spriteShadow.UpdateVisibility(!GameSettings.EnableShadows && IsVisible);

            if (!GameSettings.EnableShadows && _spriteShadow != null)
            {
                _spriteShadow.EntityPosition.Set(new CPosition(_owlPosition.Position.X-8, _owlPosition.Position.Y-14, 0));
            }
        }

        private void UpdateSit()
        {
            var playerDirection = MapManager.ObjLink.Position - _owlPosition.Position;
            if (playerDirection.Length() < 64 && MapManager.ObjLink.IsGrounded())
            {
                // start playing owl music
                Game1.AudioManager.SetMusic(33, 2);

                _leavePosition = new Vector3(originX, originY - 90, 90);
                _aiComponent.ChangeState("talk");
            }
        }

        private void UpdateWait()
        {
            if (_triggerCollided && !MapManager.ObjLink.IsRailJumping() && !MapManager.ObjLink.IsTransitioning)
                _aiComponent.ChangeState("enter");
        }

        private void InitEnter()
        {
            IsVisible = true;

            // We only need to trigger an encounter once.
            if (_wasTriggered)
                return;

            // Adjust fly pattern and animation based on the modes set.
            var offsetX = (_mode == 2 || _hoverMode) ? 0 : 64;
            var animation = _hoverMode ? "hover" : "fly";

            // Play the animation set.
            _animator.Play(animation);

            // Set the owls start, landing, and leave positions.
            _startPosition = new Vector3(originX - offsetX, originY - 64, 64);
            _landPosition = new Vector3(originX, originY, 0);
            _leavePosition = new Vector3(originX + offsetX, originY - 64, 64);

            // Reset the tracking variables.
            _airCount = 0;
            _flySoundCount = 0;

            // Start playing owl music unless we're in the ending sequence.
            if (Game1.AudioManager.GetCurrentMusic() != 88)
                Game1.AudioManager.SetMusic(33, 2);

            // Force Link back into an "idle" state when encountering the owl.
            ObjLink Link = MapManager.ObjLink;

            // Release the rooster if holding onto it.
            if (Link.IsFlying())
                Link.ReleaseCarriedObject();

            // Play Link's stand animation and force Link into Idle state unless it's the ending.
            if (Link.CurrentState != ObjLink.State.FinalStand)
            {
                Link.Animation.Play("stand_" + Link.Direction);
                Link.CurrentState = ObjLink.State.Idle;
            }
            // Force Link jump position to zero.
            Link.EntityPosition.Z = 0;

            // Freeze the game as the owl enters the map, except for when the key conditions below
            // are set. Prevents softlocking the game when opening the egg or the ending sequence.
            if (_keyCondition != "9_0" && _keyCondition != "final")
            {
                Link.FreezeAnimations(true);
                Link.DisableInventory(true);
            }
            // Always freeze Link.
            Link.FreezePlayer();

            // Track the sequence was triggered and draw th eowl.
            _wasTriggered = true;
            _drawComponent.IsActive = true;

            // Do not draw the shadow during the ending sequence.
            if (!_hoverMode)
                _drawShadowComponent.IsActive = true;

            // The owl is already waiting in position, so skip the
            // intro flight and spawn in immediately.
            if (_isAlive && _noIntro)
            {
                _owlPosition.Set(_landPosition);
                _animator.Play(_hoverMode ? "hover" : "idle");
                _sprite.Color = Color.White;

                _aiComponent.ChangeState("talk");
                return;
            }
        }

        private void UpdateEnter()
        {
            MapManager.ObjLink.FreezePlayer();

            _airCount += Game1.DeltaTime;

            _flySoundCount -= Game1.DeltaTime;
            if (_flySoundCount < 0)
            {
                _flySoundCount = 500;
                Game1.AudioManager.PlaySoundEffect("D378-45-2D", false);
            }

            if (_airCount > _enterTime)
                _airCount = _enterTime;

            var time = _airCount / _enterTime;
            if (_airCount < _enterTime)
            {
                var currentPosition = Vector3.Lerp(_startPosition, _landPosition, _landingCurve.EvaluateX(time));
                _owlPosition.Set(currentPosition);

                if (!_hoverMode && time > 0.9)
                    _animator.Play("idle");
            }
            else
            {
                if (!_hoverMode)
                    _animator.Play("idle");

                _owlPosition.Set(_landPosition);

                _aiComponent.ChangeState("talk");
            }

            // player looks at the owl
            if (_airCount > _enterTime - 1000)
            {
                var playerDir = _owlPosition.Position - MapManager.ObjLink.Position;
                MapManager.ObjLink.Direction = AnimationHelper.GetDirection(playerDir);
            }

            // fade in
            if (time <= 0.1f)
                _sprite.Color = Color.White * (time / 0.1f);
            else
                _sprite.Color = Color.White;

            Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;
        }

        private void InitTalking()
        {
            _hoverCounter = 0;

            if (_sitMode)
                Game1.GameManager.StartDialogPath(_keyCondition);
            else
                Game1.GameManager.StartDialogPath(_strKey);

            Game1.GameManager.InGameOverlay.TextboxOverlay.OwlMode = true;
        }

        private void UpdateTalk()
        {
            MapManager.ObjLink.FreezePlayer();

            // Hover up/down in-place.
            if (_hoverMode)
            {
                _hoverCounter += Game1.DeltaTime;

                var hoverPosition = _landPosition;
                hoverPosition.Y -= MathF.Sin((GetHoverState((float)_hoverCounter / 1000f, 0.0f) - 0.0f) * MathF.PI * 2 - MathF.PI / 2) * 3 + 3;
                _owlPosition.Set(hoverPosition);
            }

            // The final action taken is started when the textbox is closed.
            if (!Game1.GameManager.InGameOverlay.TextboxOverlay.IsOpen)
            {
                // During the ending the owl fades out. 
                if (_hoverMode)
                    _aiComponent.ChangeState("fadeout");

                // Normally the owl just flies away.
                else
                    _aiComponent.ChangeState((_mode == 0 || _mode == 2) ? "leave" : "talked");
            }
        }

        private void InitFadeout()
        {
            _airCount = 0;

            // Stop playing music.
            Game1.AudioManager.SetMusic(-1, 2);

            // Unfreeze the game as the owl disappears.
            MapManager.ObjLink.FreezeAnimations(false);
            MapManager.ObjLink.DisableInventory(false);

            // Play the fade out sound effect.
            Game1.AudioManager.PlaySoundEffect("D360-38-26");
        }

        private void UpdateFadeout()
        {
            _airCount += Game1.DeltaTime;

            // Continue hovering during the fadeout.
            _hoverCounter += Game1.DeltaTime;
            var hoverPosition = _landPosition;
            hoverPosition.Y -= MathF.Sin((GetHoverState((float)_hoverCounter / 1000f, 0.0f) - 0.0f) * MathF.PI * 2 - MathF.PI / 2) * 3 + 3;
            _owlPosition.Set(hoverPosition);

            // Total fade duration in milliseconds.
            var fadeTime = 2000f;
            var time = _airCount / fadeTime;

            if (time >= 1f)
            {
                IsVisible = false;
                if (_spriteShadow != null)
                    _spriteShadow.Destroy();
                Map.Objects.DeleteObjects.Add(this);
            }
            else
            {
                _sprite.Color = Color.White * (1 - time);
            }
        }

        private float GetHoverState(float _hoverCounter, float startPercentage)
        {
            // gradient up to 0.5f
            var gradient = 0.9f;
            // time needed to reach 0.5f
            var timeX = 0.5f * gradient;

            _hoverCounter += startPercentage * gradient;
            _hoverCounter %= 1;

            if (_hoverCounter < timeX)
                return _hoverCounter / gradient;
            else
                return timeX / gradient + (1 - timeX / gradient) * (_hoverCounter - timeX) / (1 - timeX);
        }

        private void InitLeave()
        {
            _airCount = 0;
            _landPosition.X = _owlPosition.X;
            _landPosition.Y = _owlPosition.Y;

            if (!_hoverMode)
            {
                _animator.Play("fly");
                _animator.SpeedMultiplier = 1.5f;
            }
            // Stop playing music.
            Game1.AudioManager.SetMusic(-1, 2);

            // Unfreeze the game as the owl starts leaving.
            MapManager.ObjLink.FreezeAnimations(false);
            MapManager.ObjLink.DisableInventory(false);
        }

        private void UpdateLeave()
        {
            _flySoundCount -= Game1.DeltaTime;
            if (_flySoundCount < 0)
            {
                _flySoundCount = 175;
                Game1.AudioManager.PlaySoundEffect("D378-05-05");
            }
            _airCount += Game1.DeltaTime;

            if (_airCount > _enterTime)
                _airCount = _enterTime;

            var time = _airCount / (float)_enterTime;
            if (_airCount < _enterTime)
            {
                var currentPosition = Vector3.Lerp(_landPosition, _leavePosition, _leavingCurve.EvaluateX(time));
                _owlPosition.Set(currentPosition);
            }
            else
            {
                Map.Objects.DeleteObjects.Add(this);
            }
            // fade out
            if (time >= 0.9f)
            {
                IsVisible = false;
                _spriteShadow.Destroy();
                _sprite.Color = Color.White * ((1 - time) / 0.1f);
            }
            else
                _sprite.Color = Color.White;
        }

        private void InitTalked()
        {
            // stop playing music
            Game1.AudioManager.SetMusic(-1, 2);
        }
    }
}
