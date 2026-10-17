using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    internal class MBossSmasherBall : GameObject
    {
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly CBox _damageBox;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;

        private MBossSmasher _owner;
        private Rectangle _fieldRectangle;

        private float _resetTimer;
        private bool _isPickedUp;
        private bool _hitEnemies;
        private bool _isThrown;
        private bool _initialized;

        private bool _playerInField => _fieldRectangle.Contains(MapManager.ObjLink.CenterPosition);

        public MBossSmasherBall(Map.Map map, Vector2 position, MBossSmasher owner) : base(map)
        {
            EntityPosition = new CPosition(position.X, position.Y, 0);
            ResetPosition = new CPosition(position.X, position.Y, 0);
            EntitySize = new Rectangle(-8, -32, 16, 32);
            CanReset = false;

            _owner = owner;

            // this is the same size as the player so that it can not get thrown into the wall
            _body = new BodyComponent(EntityPosition, -4, -10, 8, 10, 14)
            {
                CollisionTypes = Values.CollisionTypes.Normal | Values.CollisionTypes.NPCWall,
                MoveCollision = Collision,
                IgnoreInsideCollision = false,
                DragAir = 1.0f,
                Gravity = -0.125f,
                FieldRectangle = map.GetField((int)position.X, (int)position.Y)
            };
            // Get the field the object is in.
            if (map != null)
                _fieldRectangle = map.GetField((int)position.X, (int)position.Y);

            _sprite = new CSprite("smasher_ball", EntityPosition, new Vector2(-8, -15));

            var bodyBox = new CBox(EntityPosition, -7, -12, 14, 11, 14);
            _damageBox = new CBox(EntityPosition, -7, -14, 0, 14, 14, 14, true);

            AddComponent(BodyComponent.Index, _body);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-7, -14, 14, 14)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing });
            AddComponent(PushableComponent.Index, new PushableComponent(bodyBox, OnPush));
            AddComponent(HittableComponent.Index, new HittableComponent(bodyBox, OnHit));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_damageBox, HitType.ThrownObject, 4) { IsActive = false });
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite) { ShadowWidth = 12, ShadowHeight = 6 });

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            // Try to hit something.
            if (_hitEnemies)
            {
                var collision = Map.Objects.Hit(this, EntityPosition.Position, _damageBox.Box, HitType.ThrownObject, 2, false);
                if (collision != Values.HitCollision.None)
                {
                    _body.Velocity.X = -_body.Velocity.X * 0.45f;
                    _body.Velocity.Y = -_body.Velocity.Y * 0.45f;
                }
            }
            // Stop updating if the boss is currently dying.
            if (_owner.IsDying)
                return;

            // Start music when player enters room. Room boolean is used to not reset aiComponent state every loop iteration.
            if (!_initialized && _playerInField)
            {
                _initialized = true;
            }
            // Stop the music when the player leaves the room.
            else if (_initialized && !_playerInField)
            {
                // If Link is carrying the ball force him to drop it.
                MapManager.ObjLink.ReleaseCarriedObject();

                // Stop any movement from the ball.
                _body.VelocityTarget = Vector2.Zero;
                _initialized = false;

                // Create an effect at the monster's position if Modern Camera.
                if (!Camera.ClassicMode)
                {
                    var anim = new ObjAnimator(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - (int)EntityPosition.Z - 16, Values.LayerTop, "Particles/spawn", "run", true);
                    Map.Objects.SpawnObject(anim);
                    anim.Animator.SpeedMultiplier = 1.75f;
                    _resetTimer = 200;
                    _sprite.IsVisible = false;
                }
                // Classic Camera just reset its position.
                else
                    EntityPosition.Set(ResetPosition);
            }
            // If the player leaves the field, reset the monster's position.
            if (_resetTimer > 0)
            {
                _resetTimer -= Game1.DeltaTime;
                if (_resetTimer <= 0) 
                {
                    _resetTimer = 0;
                    _sprite.IsVisible = true;
                    EntityPosition.Set(ResetPosition);
                }
            }
        }

        public void Destroy()
        {
            // spawn explosion
            var animation = new ObjDeathExplodeEffect(Map, 0, 0, 0, 0);
            animation.EntityPosition.Set(new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z - 8));
            Map.Objects.SpawnObject(animation);
            Map.Objects.SpawnObject(new ObjDungeonFairy(Map, (int)EntityPosition.X, (int)EntityPosition.Y - 8, 0));
            Map.Objects.DeleteObjects.Add(this);
        }

        public bool IsAvailable()
        {
            // Returns if the ball can be picket up by the boss. This is the case if it is laying on the ground and the player is not holding it.
            return !_isPickedUp && _body.IsGrounded;
        }

        public bool InitPickup()
        {
            // Init Pickup by the boss.
            if (_isPickedUp)
                return false;

            _carriableComponent.IsActive = false;
            _damageField.IsActive = true;
            _body.IgnoresZ = true;
            return true;
        }

        public void EndPickup()
        {
            _body.IgnoresZ = false;
            _body.Velocity = Vector3.Zero;

            _carriableComponent.IsActive = true;
        }

        public void Throw(Vector3 direction)
        {
            // make sure to not get over walls
            _body.IsGrounded = false;
            _body.JumpStartHeight = 0;
            _body.IgnoresZ = false;
            _body.Velocity = direction;

            _carriableComponent.IsActive = false;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Don't let the object hit itself.
            if (originObject == this)
                return Values.HitCollision.None;

            if (hitType == HitType.MagicRod || hitType == HitType.Boomerang)
                return Values.HitCollision.Blocking | Values.HitCollision.SpawnFire;

            return Values.HitCollision.RepellingParticle;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            if (pushType == PushableComponent.PushType.Impact)
                return true;

            return false;
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            // the ball was picked up
            _isPickedUp = true;
            _body.IsActive = false;

            return new Vector3(EntityPosition.X, EntityPosition.Y, EntityPosition.Z);
        }

        private bool CarryUpdate(Vector3 newPosition)
        {
            EntityPosition.Set(new Vector3(newPosition.X, newPosition.Y, newPosition.Z));
            return true;
        }

        private void CarryThrow(Vector2 velocity)
        {
            Release();
            _body.Velocity = new Vector3(velocity.X, velocity.Y, 0) * 1.0f;
            _hitEnemies = true;
            _isThrown = true;
            _carriableComponent.Thrown = true;
        }

        private void Release()
        {
            _isPickedUp = false;
            // @HACK: we need to make sure that the boss is not walking into walls
            _body.JumpStartHeight = 0;
            _body.IsGrounded = false;
            _body.IsActive = true;
        }

        public void DisableDamageField()
        {
            _damageField.IsActive = false;
        }

        private void Collision(Values.BodyCollision direction)
        {
            if ((direction & Values.BodyCollision.Floor) != 0)
            {
                Game1.AudioManager.PlaySoundEffect("D360-09-09");

                // stop hitting the player/boss when the ball touches the ground
                _damageField.IsActive = false;
                _hitEnemies = false;
                _isThrown = false;
                _carriableComponent.IsActive = true;
                _carriableComponent.Thrown = false;
            }
            if ((direction & Values.BodyCollision.Horizontal) != 0)
                _body.Velocity.X = -_body.Velocity.X * 0.65f;
            if ((direction & Values.BodyCollision.Vertical) != 0)
                _body.Velocity.Y = -_body.Velocity.Y * 0.65f;
        }
    }
}
