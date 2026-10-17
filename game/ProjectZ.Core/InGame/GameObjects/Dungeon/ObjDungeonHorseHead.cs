using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjDungeonHorseHead : GameObject, IHasSpriteVisibility
    {
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly CSprite _sprite;

        // field that the horse head can not leave
        private readonly Rectangle _fieldRectangle;

        private readonly DictAtlasEntry _spriteHeadUp;
        private readonly DictAtlasEntry _spriteHeadDown;

        private readonly CBox _upperBox;
        private readonly CBox _lowerBox;

        private readonly string _strKey;
        private bool _dropFix;

        private int _throwDirection;
        private int _bounceCount;

        private int _direction;
        private bool _isUp;
        private bool _wasUp;
        private bool _chessBounces;
        private bool _reversal;
        private bool _isThrown;
        private bool _wasThrown;

        public CSprite Sprite => _sprite;

        public ObjDungeonHorseHead() : base("horse_head_up") { }

        public ObjDungeonHorseHead(Map.Map map, int posX, int posY, string strKey, int direction) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 13, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 13, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;

            _strKey = strKey;
            _direction = direction;

            _upperBox = new CBox(EntityPosition, -4, -8 + 3, 0, 8, 8, 4, true);
            _lowerBox = new CBox(EntityPosition, -4, -8 + 3, 0, 8, 8, 4);

            _fieldRectangle = map.GetField(posX, posY, 15);

            // this is the same size as the player so that it can not get thrown into the wall
            _body = new BodyComponent(EntityPosition, -4, -8, 8, 8, 14)
            {
                CollisionTypes = Values.CollisionTypes.Normal | Values.CollisionTypes.NPCWall | Values.CollisionTypes.Barrier,
                MoveCollision = Collision,
                IgnoreInsideCollision = false,
                DragAir = 0.95f,
                Gravity = -0.125f,
                FieldRectangle = map.GetField(posX, posY, 16)
            };

            _spriteHeadUp = Resources.GetSprite("horse_head_up");
            _spriteHeadDown = Resources.GetSprite("horse_head_down");

            _sprite = new CSprite(_spriteHeadDown, EntityPosition, new Vector2(-8, -13));

            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-7, -14, 14, 14)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing } );
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(HittableComponent.Index, new HittableComponent(_body.BodyBox, OnHit));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));

            DecrementUpState();
            UpdateSprite();

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        private void IncrementUpState()
        {
            if (string.IsNullOrEmpty(_strKey))
                return;

            // _strKey will get set to 1 after two horse heads stand up
            var currentState = Game1.GameManager.SaveManager.GetString(_strKey);
            if (currentState != "x")
                Game1.GameManager.SaveManager.SetString(_strKey, "x");
            else
                Game1.GameManager.SaveManager.SetString(_strKey, "1");
        }

        private void DecrementUpState()
        {
            if (string.IsNullOrEmpty(_strKey))
                return;

            var currentState = Game1.GameManager.SaveManager.GetString(_strKey);
            if (currentState == "x")
                Game1.GameManager.SaveManager.SetString(_strKey, "0");
        }

        private void UpdateSprite()
        {
            var newSprite = _wasThrown || _isUp ? _spriteHeadUp : _spriteHeadDown;
            _sprite.SetSprite(newSprite);
            _sprite.DrawOffset.X = -newSprite.SourceRectangle.Width / 2;

            _sprite.SpriteEffect = SpriteEffects.None;

            if (_direction == 1 || _direction == 2)
                _sprite.SpriteEffect = SpriteEffects.FlipVertically;
            if (_direction >= 2)
                _sprite.SpriteEffect |= SpriteEffects.FlipHorizontally;
        }

        private void Update()
        {
            UpdateSprite();

            // If trying to carry it out of the room it's dropped and propelled away.
            if (_dropFix)
            {
                _dropFix = false;
                _body.IgnoreInsideCollision = true;
                _body.IsActive = true;
                _body.Velocity = new Vector3(0, -1.5f, 0) * 1.0f;
                _isThrown = false;
                _carriableComponent.Thrown = false;
            }

            if (!_wasThrown || _reversal)
                return;

            // Prvents the horse head from being able to be thrown through a wall.
            var outBox = Box.Empty;
            if (!Map.Is2dMap &&
                Map.Objects.Collision(_upperBox.Box, Box.Empty, Values.CollisionTypes.Normal, 0, _body.Level, ref outBox) &&
                Map.Objects.Collision(_lowerBox.Box, Box.Empty, Values.CollisionTypes.Normal, 0, _body.Level, ref outBox))
            {
                switch (_throwDirection)
                {
                    case 0: case 2: _body.Velocity.X = -_body.Velocity.X; break;
                    case 1: case 3: _body.Velocity.Y = -_body.Velocity.Y; break;
                }
                _reversal = true;
                _throwDirection = AnimationHelper.OffsetDirection(_throwDirection, 2);
            }
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            _body.IsActive = false;
            _body.IgnoreInsideCollision = false;
            return new Vector3(EntityPosition.X, EntityPosition.Y, EntityPosition.Z);
        }

        private bool CarryUpdate(Vector3 newPosition)
        {
            // Drop the horse head if trying to leave the room with it. A small velocity is applied on Update.
            if (!_fieldRectangle.Contains(new Vector2(newPosition.X, newPosition.Y)))
            {
                _dropFix = true;
                return false;
            }
            EntityPosition.Set(new Vector3(newPosition.X, newPosition.Y, newPosition.Z + 3));
            return true;
        }

        private void CarryThrow(Vector2 velocity)
        {
            _isThrown = true;
            _carriableComponent.Thrown = true;

            _throwDirection = AnimationHelper.GetDirection(velocity);

            if (velocity.Length() > 0)
            {
                _wasThrown = true;
                _chessBounces = true;
                _direction = Game1.RandomNumber.Next(1, 3);
                _isUp = false;
            }
            _body.Velocity = new Vector3(velocity.X, velocity.Y, 0) * 1.0f;
            Release();
        }

        private void Release()
        {
            _bounceCount = 0;
            _body.JumpStartHeight = 0;
            _body.IsGrounded = false;
            _body.IsActive = true;

            // we change the bounciness to make sure that we only bounce 2 times
            _body.Bounciness = _chessBounces ? 0.725f : 0.0f;
        }

        private void Collision(Values.BodyCollision direction)
        {
            if ((direction & Values.BodyCollision.Floor) != 0 && _chessBounces)
            {
                _reversal = false;
                _wasThrown = false;
                _bounceCount++;
                _direction = AnimationHelper.OffsetDirection(_direction, _throwDirection > 2 ? 1 : -1);

                // jump to the left or right after the second bounce
                var velocityDirection = _throwDirection;
                if (_bounceCount == 3)
                {
                    _chessBounces = false;
                    _body.Bounciness = 0;
                    velocityDirection = AnimationHelper.OffsetDirection(velocityDirection, Game1.RandomNumber.Next(0, 2) * 2 - 1);

                    // 50% chance that the horse head will stand up after the throw.
                    if (Game1.RandomNumber.Next(0, 4) <= 1 || _wasUp)
                    {
                        _body.Velocity = Vector3.Zero;

                        // Play a sound when correct (this is not in the original game).
                        Game1.AudioManager.PlaySoundEffect("D370-14-0E");

                        if (!_wasUp)
                            IncrementUpState();

                        _wasUp = true;
                        _isUp = true;

                        // Make sure the head is standing up.
                        if (_direction == 1)
                            _direction = 0;
                        if (_direction == 2)
                            _direction = 3;
                    }
                    else
                    {
                        Game1.AudioManager.PlaySoundEffect("D360-29-1D");
                    }
                    _isThrown = false;
                    _carriableComponent.Thrown = false;
                }

                if (_bounceCount <= 3)
                {
                    var velocity = AnimationHelper.DirectionOffset[velocityDirection];
                    _body.Velocity.X = velocity.X * 1.25f;
                    _body.Velocity.Y = velocity.Y * 1.25f;
                }
            }

            // make sure that the throw direction gets changed so the next bounce will not go towards the wall
            if ((direction & (Values.BodyCollision.Horizontal | Values.BodyCollision.Vertical)) != 0)
                _throwDirection = AnimationHelper.OffsetDirection(_throwDirection, 2);

            if ((direction & Values.BodyCollision.Horizontal) != 0)
                _body.Velocity.X = -_body.Velocity.X * 0.65f;
            if ((direction & Values.BodyCollision.Vertical) != 0)
                _body.Velocity.Y = -_body.Velocity.Y * 0.65f;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_body.Velocity.Length() < 0.1f)
            {
                _body.Velocity.X = direction.X * 0.25f;
                _body.Velocity.Y = direction.Y * 0.25f;
            }

            return Values.HitCollision.RepellingParticle;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            if (pushType == PushableComponent.PushType.Impact)
            {
                _body.Velocity.X = direction.X * 0.25f;
                _body.Velocity.Y = direction.Y * 0.25f;
                return true;
            }
            return false;
        }
    }
}
