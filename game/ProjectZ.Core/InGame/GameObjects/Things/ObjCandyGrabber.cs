using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    class ObjCandyGrabber : GameObject
    {
        private readonly Rectangle _recTop;
        private readonly Rectangle _recGrabberLeft;
        private readonly Rectangle _recGrabberRight;
        private readonly Rectangle _recGrabberLeftClosed;
        private readonly Rectangle _recGrabberRightClosed;
        private readonly Rectangle _recLine;
        private readonly Rectangle _fieldRectangle;

        private readonly List<GameObject> _collidingObjects = new List<GameObject>();

        private Box _grabberRectangle;
        private Vector2 _vecStart;

        private const float MoveSpeed = 0.25f;
        private const float MoveSpeedGrab = 0.25f;
        private const float MoveSpeedBack = 0.5f;

        private float _blinkCount;
        private float _grabState;
        private float _grab2Count = 5000;
        private float _waitCounter;

        private bool _marinGame;
        private bool _alwaysAnimate;

        private BodyComponent _grabbedBody;

        enum State
        {
            Idle, MoveX, IdleY, WaitY, MoveY,
            Grab0, Grab1, Grab2, Grab3,
            BackY, BackX, BackWait, ResetClaw 
        }
        private State _currentState = State.Idle;

        public ObjCandyGrabber() : base("candy_grabber") { }

        public ObjCandyGrabber(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX, posY + 38, 0);

            _recTop = Resources.SourceRectangle("candy_grabber_top");
            _recGrabberLeft = Resources.SourceRectangle("candy_grabber_left");
            _recGrabberRight = Resources.SourceRectangle("candy_grabber_right");
            _recGrabberLeftClosed = Resources.SourceRectangle("candy_grabber_left_closed");
            _recGrabberRightClosed = Resources.SourceRectangle("candy_grabber_right_closed");
            _recLine = Resources.SourceRectangle("candy_grabber_line");
            _vecStart = new Vector2(posX, posY + 38);
            _fieldRectangle = Map.GetField(posX, posY);

            var shadowSourceRectangle = new Rectangle(0, 0, 65, 66);
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, new DrawShadowSpriteComponent(null, Resources.SprShadow, EntityPosition, shadowSourceRectangle, new Vector2(0, -5), 1.0f, 0.0f) { Width = 16, Height = 8 });

            new ObjSpriteShadow(Map, this, 0, -14, Values.LayerPlayer, "sprshadowl") { ForceDraw = true };
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void AddAlwaysAnimateToObjects()
        {
            // Get all objects with the "Item" and "Utility" game tags.
            var objectList = new List<GameObject>();
            Map.Objects.GetGameObjectsWithTag(objectList, Values.GameObjectTag.Item | Values.GameObjectTag.Utility,
                _fieldRectangle.X, _fieldRectangle.Y, _fieldRectangle.Width, _fieldRectangle.Height);

            // Loop through the list of objects.
            foreach (var obj in objectList) 
            {
                // Always animate both the items and roll bands.
                if (obj is ObjItem or ObjRollBand)
                    Map.Objects.RegisterAlwaysAnimateObject(obj);
            }
        }

        private void Update()
        {
            // Always animate items and roll bands and add shadows to items.
            if (!_alwaysAnimate)
            {
                AddAlwaysAnimateToObjects();
                _alwaysAnimate = true;
            }
            _blinkCount = (_blinkCount + Game1.DeltaTime) % 500;
            _grabberRectangle = new Box(EntityPosition.X + 6, EntityPosition.Y - 3, 0, 4, 4, 2);

            _currentState = _currentState switch
            { 
                State.Idle      => HandleIdle(),
                State.MoveX     => HandleMoveX(),
                State.IdleY     => HandleIdleY(),
                State.WaitY     => HandleWaitY(),
                State.MoveY     => HandleMoveY(),
                State.Grab0     => HandleGrab0(),
                State.Grab1     => HandleGrab1(),
                State.Grab2     => HandleGrab2(),
                State.Grab3     => HandleGrab3(),
                State.BackY     => HandleBackY(),
                State.BackX     => HandleBackX(),
                State.BackWait  => HandleBackWait(),
                State.ResetClaw => HandleResetClaw(),
                _               => _currentState
            };

            if (_currentState != State.Idle && (int)_currentState < 5)
                MapManager.ObjLink.FreezePlayer();

            UpdateItemPos();

            State HandleIdle()
            {
                if (ControlHandler.TrendyButtonDown(ControlHandler.CancelButton))
                    StartGrabbing();
                return _currentState;
            }

            State HandleMoveX()
            {
                if ((_marinGame || ControlHandler.TrendyButtonDown(ControlHandler.CancelButton)) &&
                    EntityPosition.X < _vecStart.X + 112)
                {
                    Game1.AudioManager.PlaySoundEffect("D378-32-20", false);
                    EntityPosition.Move(new Vector2(MoveSpeed, 0));

                    if (EntityPosition.X > _vecStart.X + 112)
                        EntityPosition.Set(new Vector2(_vecStart.X + 112, EntityPosition.Y));

                    return _currentState;
                }
                Game1.AudioManager.StopSoundEffect("D378-32-20");
                Game1.GameManager.SaveManager.SetString("trendy_button_1", "0");
                Game1.GameManager.SaveManager.SetString("trendy_button_2", "1");
                return _marinGame ? State.WaitY : State.IdleY;
            }

            State HandleIdleY()
            {
                return ControlHandler.TrendyButtonDown(ControlHandler.ConfirmButton) ? State.MoveY : _currentState;
            }

            State HandleWaitY()
            {
                _waitCounter += Game1.DeltaTime;
                return _waitCounter > 1000 ? State.MoveY : _currentState;
            }

            State HandleMoveY()
            {
                if ((_marinGame || ControlHandler.TrendyButtonDown(ControlHandler.ConfirmButton)) &&
                    EntityPosition.Y < _vecStart.Y + 64)
                {
                    Game1.AudioManager.PlaySoundEffect("D378-32-20", false);
                    EntityPosition.Move(new Vector2(0, MoveSpeed));

                    if (EntityPosition.Y > _vecStart.Y + 64)
                        EntityPosition.Set(new Vector2(EntityPosition.X, _vecStart.Y + 64));

                    return _currentState;
                }

                Game1.AudioManager.StopSoundEffect("D378-32-20");
                Game1.GameManager.SaveManager.SetString("trendy_button_2", "0");
                _waitCounter = 0;
                return State.Grab0;
            }

            State HandleGrab0()
            {
                _waitCounter += Game1.DeltaTime;
                if (_waitCounter > 1200)
                {
                    _waitCounter = 0;
                    _grab2Count = 0; // open grabber
                    return State.Grab1;
                }
                return _currentState;
            }

            State HandleGrab1()
            {
                _waitCounter += Game1.DeltaTime;
                return _waitCounter > 1200 ? State.Grab2 : _currentState;
            }

            State HandleGrab2()
            {
                _grabState += MoveSpeedGrab * Game1.TimeMultiplier;
                if (_grabState > 15)
                {
                    _grabState = 15;
                    return State.Grab3;
                }
                return _currentState;
            }

            State HandleGrab3()
            {
                _grab2Count += Game1.DeltaTime;

                if (_grab2Count > 500)
                    _grabState = 16;
                if (_grab2Count > 2000)
                    Grab();
                if (_grab2Count > 3000)
                    return State.BackY;
                return _currentState;
            }

            State HandleBackY()
            {
                Game1.AudioManager.PlaySoundEffect("D378-32-20", false);
                _grabState -= MoveSpeedGrab * Game1.TimeMultiplier;
                if (_grabState < 0)
                {
                    _grabState = 0;
                    return State.BackX;
                }
                return _currentState;
            }

            State HandleBackX()
            {
                Game1.AudioManager.PlaySoundEffect("D378-32-20", false);

                var vecBack = _vecStart - EntityPosition.Position;
                vecBack.Normalize();
                EntityPosition.Move(vecBack * MoveSpeedBack);

                if (EntityPosition.X <= _vecStart.X && EntityPosition.Y <= _vecStart.Y)
                {
                    Game1.AudioManager.StopSoundEffect("D378-32-20");
                    _waitCounter = 0;
                    EntityPosition.Set(_vecStart);
                    return State.BackWait;
                }
                return _currentState;
            }

            State HandleBackWait()
            {
                _waitCounter += Game1.DeltaTime;
                if (_waitCounter > 1500)
                {
                    _grab2Count = 0;
                    EndGrabbing();
                    _waitCounter = 0;
                    return State.ResetClaw;
                }
                return _currentState;
            }

            State HandleResetClaw()
            {
                _waitCounter += Game1.DeltaTime;
                if (_waitCounter > 1500)
                {
                    ResetClaw();
                    _waitCounter = 0;
                    return State.Idle;
                }
                return _currentState;
            }
        }

        private void OnKeyChange()
        {
            var strMarinGame = "trendy_marin_game";
            if (!_marinGame && Game1.GameManager.SaveManager.GetString(strMarinGame, "0") == "1")
            {
                Game1.GameManager.SaveManager.RemoveString(strMarinGame);

                // Achievement: Marin plays the trendy game.
                AchievementManager.Earn(51);

                _marinGame = true;
                _currentState = State.MoveX;
            }
        }

        private void StartGrabbing()
        {
            if (!Game1.GameManager.SaveManager.GetBool("trendy_ready", false))
                return;

            MapManager.ObjLink.Direction = 1;
            MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
            MapManager.ObjLink.FreezePlayer();

            Game1.GameManager.SaveManager.SetString("can_play", "0");
            _currentState = State.MoveX;
        }

        private void Grab()
        {
            if (_grabbedBody != null)
                return;

            _collidingObjects.Clear();
            Map.Objects.GetComponentList(_collidingObjects,
                (int)_grabberRectangle.Left, (int)_grabberRectangle.Back, (int)_grabberRectangle.Width, (int)_grabberRectangle.Height, BodyComponent.Mask);

            foreach (var gameObject in _collidingObjects)
            {
                var objBody = ((BodyComponent)gameObject.Components[BodyComponent.Index]);
                if ((gameObject is ObjItem || _marinGame) && objBody.BodyBox.Box.Intersects(_grabberRectangle))
                {
                    // When grabbing an item, set the item's save key so the game knows when to end. The 
                    // exception is when the item is the Yoshi Doll. If setting the key and leaving it would 
                    // disappear forever so we also use an alternate key "trendy_5" which is set when grabbed.
                    if (gameObject is ObjItem objitem)
                    {
                        if (objitem._itemName == "trade0")
                            Game1.GameManager.SaveManager.SetString("trendy_5", "1");
                        else
                            Game1.GameManager.SaveManager.SetString(objitem.SaveKey, "1");
                    }
                    StartGrabbing(objBody);
                    return;
                }
            }
        }

        private void StartGrabbing(BodyComponent body)
        {
            _grabbedBody = body;
            _grabbedBody.AdditionalMovementVT = Vector2.Zero;
            _grabbedBody.IgnoresZ = true;

            Game1.AudioManager.PlaySoundEffect("D360-25-19", false);
        }

        private void UpdateItemPos()
        {
            _grabbedBody?.Position.Set(new Vector3(
                EntityPosition.X + 8, EntityPosition.Y + 0.001f, 13 - _grabState));
        }

        private void EndGrabbing()
        {
            if (_grabbedBody == null)
                return;

            _grabbedBody.AdditionalMovementVT = new Vector2(0, 1 / 6.0f);
            _grabbedBody.IgnoresZ = false;
            _grabbedBody = null;
        }

        private void ResetClaw()
        {
            _grabState = 0;
            _grab2Count = float.MaxValue;

            // When the crane returns and has finished resetting enable the NPC interaction box.
            Game1.GameManager.SaveManager.SetString("trendy_npc", "0");
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(Resources.SprObjects, new Vector2(EntityPosition.Position.X, EntityPosition.Position.Y - 38),
                new Rectangle(_recTop.X, _recTop.Y + (_blinkCount >= 250 ? _recTop.Height : 0), _recTop.Width, _recTop.Height), Color.White);
            spriteBatch.Draw(Resources.SprObjects, new Vector2(
                EntityPosition.X + 6, EntityPosition.Y - 38 + _recTop.Height),
                new Rectangle(_recLine.X, _recLine.Y, _recLine.Width, (int)Math.Ceiling(_grabState)), Color.White);
            spriteBatch.Draw(Resources.SprObjects, new Vector2(
                EntityPosition.X, EntityPosition.Y - 38 + _recTop.Height + _grabState),
                _grab2Count > 1000 ? _recGrabberLeftClosed : _recGrabberLeft, Color.White);
            spriteBatch.Draw(Resources.SprObjects, new Vector2(
                EntityPosition.X + _recGrabberLeft.Width,
                EntityPosition.Y - 38 + _recTop.Height + _grabState),
                _grab2Count > 2000 ? _recGrabberRightClosed : _recGrabberRight, Color.White);
        }
    }
}
