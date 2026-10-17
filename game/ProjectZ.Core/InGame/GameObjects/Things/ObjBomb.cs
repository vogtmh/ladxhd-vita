using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    public class ObjBomb : GameObject, IHasSpriteVisibility
    {
        public BodyComponent Body;
        public bool DamageEnemies;

        private readonly Animator _animator;
        private readonly BodyDrawShadowComponent _bodyShadow;
        private readonly CarriableComponent _carriableComponent;
        private readonly BodyDrawComponent _drawComponent;
        private readonly CSprite _sprite = null;

        private readonly bool _playerBomb;
        private readonly bool _floorExplode;

        enum DeathState { Alive, FadingLight, Dead }
        private DeathState _deathState = DeathState.Alive;

        private float _fadeTimer = 0f;
        private float _startBrightness;

        public const int BlinkTime = 1000 / 60 * 4;

        private double _bombCounter;
        private double _explosionTime;
        private double _lastHitTime;
        private double _deepWaterCounter;
        private float _knockback;
        private bool _playerHit;

        private Map.Map _map; 

        private bool _exploded;
        private bool _arrowMode;
        private bool _carried;
        private bool _isThrown;

        ObjSpriteShadow _spriteShadow;

        // Public values for ObjLink to read LAHDMod values.
        public bool AutoPickup => auto_pickup;
        public bool RemoteBombs => remote_bombs;

        public CSprite Sprite => _sprite;

        // Values configurable via lahdmod.
        private int   fuse_timer     = 1500;
        private bool  item_interact  = false;
        private bool  enemy_interact = false;
        private bool  fire_detonates = false;
        private bool  arrow_pickup   = false;
        private bool  auto_pickup    = false;
        private bool  self_damage    = false;
        private bool  remote_bombs   = false;
        private bool  light_source   = true;
        private int   light_red      = 255;
        private int   light_grn      = 255;
        private int   light_blu      = 255;
        private float light_bright   = 1.0f;
        private int   light_size     = 160;
        private float light_fade     = 0.60f;

        public ObjBomb(Map.Map map, float posX, float posY, bool playerBomb, bool floorExplode, int explosionTime = 1500, float knockback = 1.75f) : base(map)
        {
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjBomb.lahdmod");
            ModFile.Parse(modFile, this);

            CanReset = true;
            OnReset = Reset;

            // For some reason, getting the map from the parameter avoids a crash that *sometimes* happens when shooting a bomb arrow into the 
            // mouth of a Dodongo Snake. This was originally coded to use "Map" directly, but for reasons unknown it could end up being null!
            _map = map;

            if (!_map.Is2dMap)
                EntityPosition = new CPosition(posX, posY, 5);
            else
                EntityPosition = new CPosition(posX, posY - 5, 0);

            EntitySize = new Rectangle(-8, -16, 16, 20);

            Body = new BodyComponent(EntityPosition, -4, -8, 8, 8, 4)
            {
                Bounciness = 0.5f,
                Bounciness2D = 0.5f,
                Drag = 0.85f,
                DragAir = 1.0f,
                DragWater = 0.985f,
                Gravity = -0.15f,
                HoleAbsorb = FallDeath,
                MoveCollision = OnCollision,
                IgnoreInsideCollision = false,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
            };

            if (_map.Is2dMap)
            {
                Body.OffsetY = -1;
                Body.Height = 1;
            }
            _playerBomb = playerBomb;
            _floorExplode = floorExplode;

            _animator = AnimatorSaveLoad.LoadAnimator("Objects/bomb");
            _animator.OnAnimationFinished = FinishedAnimation;
            _animator.Play("idle");

            if (fuse_timer != 1500)
                _explosionTime = fuse_timer;
            else
                _explosionTime = explosionTime;

            _bombCounter = _explosionTime;
            _knockback = knockback;

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);
            _drawComponent = new BodyDrawComponent(Body, sprite, Values.LayerPlayer);

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(HittableComponent.Index, new HittableComponent(new CBox(EntityPosition, -4, -10, 8, 10, 8), OnHit));
            AddComponent(BodyComponent.Index, Body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);

            // can not push away the bombs from enemies; would probably be fun
            if (playerBomb)
            {
                AddComponent(PushableComponent.Index, new PushableComponent(Body.BodyBox, OnPush) { RepelMultiplier = 0.5f });
                AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-4, -8, 8, 8)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing });
            }
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, _bodyShadow = new BodyDrawShadowComponent(Body, sprite));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));

            if (_playerBomb)
            {
                Map.Objects.RegisterAlwaysAnimateObject(this);
                MapManager.ObjLink.BombList.Add(this);
            }
            _spriteShadow = new ObjSpriteShadow(Map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void Reset()
        {
            if (!_carried)
                RemoveBomb();
        }

        private void SetCarriableActive(bool active)
        {
            if (_carriableComponent != null)
                _carriableComponent.IsActive = active;
        }

        private void Update()
        {
            if (_exploded)
            {
                // If it's an enemy bomb or the player enabled self damage.
                if (self_damage || !_playerBomb)
                {
                    // Use the animator's collision rectangle to detect objects.
                    var collisionRect = _animator.CollisionRectangle;
                    if (collisionRect != Rectangle.Empty)
                    {
                        // Convert the rectangle into a box.
                        var collisionBox = new Box(
                            EntityPosition.X + collisionRect.X,
                            EntityPosition.Y + collisionRect.Y, 0,
                            collisionRect.Width, collisionRect.Height, 16);

                        // Check if Link's body box intersects the collision box.
                        if (collisionBox.Intersects(MapManager.ObjLink.Body.BodyBox.Box) && !_playerHit)
                        {
                            // Determine direction from bomb center and Link's center.
                            var bombCenter = new Vector2(EntityPosition.X, EntityPosition.Y);
                            var dir = MapManager.ObjLink.BodyRectangle.Center - bombCenter;

                            // Calculate direction if there is at least some distance between the centers.
                            if (dir.LengthSquared() > 0.000001f)
                                dir.Normalize();
                            // Dead-center: shove opposite facing
                            else
                                dir = -MapManager.ObjLink.ForwardVector;

                            MapManager.ObjLink.HitPlayer(dir * _knockback, HitType.Bomb, 4, false);
                            _playerHit = true;
                        }
                    }
                }
                // Remove bomb if the animation is finished.
                if (!_animator.IsPlaying && _deathState == DeathState.Alive)
                    StartLightFade();
                
            }
            else
            {
                // blink
                if (_bombCounter < 500)
                {
                    if (_bombCounter % (BlinkTime * 2) < BlinkTime)
                        _animator.Play("blink");
                    else
                        _animator.Play("idle");
                }

                _bombCounter -= Game1.DeltaTime;
                if (_bombCounter <= 0)
                    Explode();
            }
            // fall into the water
            if (!_map.Is2dMap && Body.IsGrounded && Body.CurrentFieldState.HasFlag(MapStates.FieldStates.DeepWater))
            {
                _deepWaterCounter -= Game1.DeltaTime;

                if (_deepWaterCounter <= 0)
                {
                    // spawn splash effect
                    var fallAnimation = new ObjAnimator(_map, (int)(Body.Position.X + Body.OffsetX + Body.Width / 2.0f), (int)(Body.Position.Y + Body.OffsetY + Body.Height / 2.0f), Values.LayerPlayer, "Particles/fishingSplash", "idle", true);
                    _map.Objects.SpawnObject(fallAnimation);
                    RemoveBomb();
                }
            }
            else if (Body.IsGrounded)
            {
                if (Body.CurrentFieldState.HasFlag(MapStates.FieldStates.Water))
                    Body.Velocity = Vector3.Zero;

                _deepWaterCounter = 75;
            }

            // Fade out the light created from explosion.
            if (_deathState == DeathState.FadingLight)
            {
                _fadeTimer += Game1.DeltaTime / 1000f;
                float t = _fadeTimer / light_fade;
                t = MathHelper.Clamp(t, 0f, 1f);
                light_bright = MathHelper.Lerp(_startBrightness, 0f, t);
                if (t >= 1f)
                    _deathState = DeathState.Dead;
            }

            // Remove the bomb completely.
            if (_deathState == DeathState.Dead)
                RemoveBomb();
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            _drawComponent.Draw(spriteBatch);
        }

        private void FallDeath()
        {
            // play sound effect
            Game1.AudioManager.PlaySoundEffect("D360-24-18");

            var fallAnimation = new ObjAnimator(Map, 0, 0, Values.LayerBottom, "Particles/fall", "idle", true);
            fallAnimation.EntityPosition.Set(new Vector2(
                Body.Position.X + Body.OffsetX + Body.Width / 2.0f - 5,
                Body.Position.Y + Body.OffsetY + Body.Height / 2.0f - 5));
            _map.Objects.SpawnObject(fallAnimation);
            RemoveBomb();
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            _animator.Play("idle");
            _carried = true;

            // the bomb was picked up
            Body.IsActive = false;

            return EntityPosition.ToVector3();
        }

        private bool CarryUpdate(Vector3 newPosition)
        {
            _bombCounter = fuse_timer;

            EntityPosition.X = newPosition.X;

            if (!_map.Is2dMap)
            {
                EntityPosition.Y = newPosition.Y;
                EntityPosition.Z = newPosition.Z;
            }
            else
            {
                EntityPosition.Y = newPosition.Y - newPosition.Z;
                EntityPosition.Z = 0;
            }
            EntityPosition.NotifyListeners();
            return true;
        }

        private void CarryThrow(Vector2 velocity)
        {
            Body.Drag = 0.75f;
            Body.DragAir = 1.0f;
            Body.IsGrounded = false;
            Body.IsActive = true;
            if (_playerBomb)
                Body.Level = MapStates.GetLevel(MapManager.ObjLink.Body.CurrentFieldState);

            // Do not throw the bomb up when the player lets it fall down (e.g. by walking into a door).
            if (velocity == Vector2.Zero)
                Body.Velocity = Vector3.Zero;
            else
                Body.Velocity = new Vector3(velocity.X * 0.45f, velocity.Y * 0.45f, 1.25f);

            if (_map.Is2dMap)
                Body.Velocity.Y = -0.75f;

            Body.CollisionTypesIgnore = Values.CollisionTypes.ThrowWeaponIgnore;

            _isThrown = true;
            _carried = false;
            if (_carriableComponent != null)
                _carriableComponent.Thrown = true;
            SetCarriableActive(false);
        }

        public void Explode()
        {
            _exploded = true;
            Body.Velocity = Vector3.Zero;
            Body.IsActive = false;
            _bodyShadow.IsActive = false;
            SetCarriableActive(false);

            // Default damage of bombs.
            var damage = 1;

            // Deals damage to the player or to the enemies.
            if (_playerBomb || DamageEnemies)
                _map.Objects.Hit(this, new Vector2(EntityPosition.X, EntityPosition.Y),
                    new Box(EntityPosition.X - 20, EntityPosition.Y - 20 - 5, 0, 40, 40, 16), HitType.Bomb, damage, false);

            // Play explosion sound effect.
            Game1.AudioManager.PlaySoundEffect("D378-12-0C");

            _animator.Play("explode");
            _animator.SetFrame(1);

            // Shake the screen.
            if (GameSettings.ExScreenShake)
                Game1.GameManager.ShakeScreen(200, 2.00f, 1.00f, 50.00f, 25.50f);

            // Remove the bomb from the bomb list.
            MapManager.ObjLink.BombList.Remove(this);

            // Remove the sprite shadow.
            _map.Objects.DeleteObjects.Add(_spriteShadow);
        }

        private void FinishedAnimation()
        {
            // explode after the idle animation is finished
            if (_animator.CurrentAnimation.Id == "idle")
                Explode();
        }

        private void RemoveBomb()
        {
            // If it's a bomb from Link remove it from the bomb list.
            if (_playerBomb)
                MapManager.ObjLink.BombList.Remove(this);

            // Remove the bomb completely.
            _map.Objects.DeleteObjects.Add(this);
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if (_map.Is2dMap)
            {
                if ((collision & Values.BodyCollision.Horizontal) != 0)
                {
                    Body.Velocity.X = -Body.Velocity.X * 0.25f;
                    Game1.AudioManager.PlaySoundEffect("D360-09-09");
                }
                if ((collision & Values.BodyCollision.Bottom) != 0 && Body.Velocity.Y < -0.075f)
                {
                    Body.DragAir *= 0.975f;
                    SetCarriableActive(true);
                    Game1.AudioManager.PlaySoundEffect("D360-09-09");
                }
            }

            if ((collision & Values.BodyCollision.Floor) != 0)
            {
                if (Body.Velocity.Z > 0.5f)
                    Game1.AudioManager.PlaySoundEffect("D360-09-09");

                //Body.Level = 0;
                Body.Drag *= 0.8f;
                _isThrown = false;
                if (_carriableComponent != null)
                    _carriableComponent.Thrown = false;
                SetCarriableActive(true);

                if (_floorExplode && Body.Velocity.Z <= 0)
                    Explode();
            }
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (_exploded)
                return false;

            // push the bomb away
            if (type == PushableComponent.PushType.Impact && GameSettings.SwSmackBombs && _playerBomb)
            {
                Body.Drag = 0.85f;
                Body.Velocity = new Vector3(direction.X * 1.5f, direction.Y * 1.5f, Body.Velocity.Z);
                return true;
            }
            return false;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Allow exploding bombs with the magic rod or magic powder.
            if (fire_detonates && (hitType == HitType.MagicPowder || hitType == HitType.MagicRod))
            {
                _bombCounter = 0;
                return Values.HitCollision.Blocking;
            }
            // Block the sword from smacking it around unless the player enabled it.
            if ((hitType & HitType.Sword) != 0 || (hitType & HitType.SwordSpin) != 0 || (hitType & HitType.SwordHold) != 0 || (hitType & HitType.SwordShot) != 0 || (hitType & HitType.PegasusBootsSword) != 0)
                if ((!enemy_interact && !_playerBomb) || !GameSettings.SwSmackBombs)
                    return Values.HitCollision.None;

            // Block other item interactions unless the player enabled it.
            if (hitType == HitType.Boomerang || hitType == HitType.Bomb || hitType == HitType.MagicRod || hitType == HitType.Hookshot || hitType == HitType.MagicPowder || hitType == HitType.ThrownObject)
                if ((!enemy_interact && !_playerBomb) || !item_interact)
                    return Values.HitCollision.None;

            // Combine with arrows to create a bomb-arrow.
            if (_playerBomb && !_exploded && (_bombCounter + 175 > _explosionTime || arrow_pickup) && gameObject is ObjArrow objArrow)
            {
                _arrowMode = true;
                _bombCounter = fuse_timer;
                Body.IgnoresZ = true;
                Body.IgnoreHoles = true;
                Body.Velocity = Vector3.Zero;
                EntityPosition.Z = 0;
                objArrow.InitBombMode(this);
            }
            // Don't hit the arrow part if it's a bomb-arrow.
            if (_arrowMode)
            {
                return Values.HitCollision.None;
            }
            if (_exploded || (_lastHitTime != 0 && Game1.TotalGameTime - _lastHitTime < 250) || hitType == HitType.Bow)
            {
                return Values.HitCollision.None;
            }
            _lastHitTime = Game1.TotalGameTime;

            Body.Drag = 0.85f;
            Body.DragAir = 0.85f;
            Body.Velocity.X += direction.X * 4;
            Body.Velocity.Y += direction.Y * 4;

            if (_map.Is2dMap)
            {
                Body.DragAir = 0.925f;
                Body.Velocity.Y = direction.Y * 2f;
            }
            return Values.HitCollision.Blocking;
        }

        private void StartLightFade()
        {
            _deathState = DeathState.FadingLight;

            _fadeTimer = 0f;
            _startBrightness = light_bright;

            // Hide visuals / physics, but keep light alive
            _drawComponent.IsActive = false;
            _bodyShadow.IsActive = false;
            Body.IsActive = false;
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (!GameSettings.ObjectLights || !light_source || !_exploded || _deathState == DeathState.Dead || light_bright <= 0f)
                return;

            Rectangle rect = new Rectangle((int)EntityPosition.X - light_size / 2, (int)EntityPosition.Y - light_size / 2 - (int)EntityPosition.Z, light_size, light_size);
            DrawHelper.DrawLight(spriteBatch, rect, new Color(light_red, light_grn, light_blu) * light_bright);
        }
    }
}
