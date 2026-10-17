using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Base.Systems;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjItem : GameObject, IHasVisibility
    {
        public bool IsJumping;
        public bool Collectable;
        public bool Collected;

        public string SaveKey;

        private GameItem _item;
        public string _itemName;
        private string _locationBound;

        private AiComponent _aiComponent;
        private DrawShadowSpriteComponent _shadowComponent;
        private BodyComponent _body;
        private AiTriggerCountdown _delayCountdown;
        private BodyDrawComponent _bodyDrawComponent;
        private BoxCollisionComponent _collisionComponent;

        private CBox _collectionBox;
        private CRectangle _collectionRect;

        private Rectangle _sourceRectangle;
        private Rectangle _sourceRectangleWing = new Rectangle(2, 250, 8, 15);
        private Rectangle _shadowSourceRectangle = new Rectangle(0, 0, 65, 66);
        private Color _color = Color.White;

        private float _fadeOffset;
        private double _deepWaterCounter;
        private float _despawnCount;
        private int _despawnTime = 350;
        private int _fadeStart = 250;
        private int _moveStopTime = 250;
        private int _lastFieldTime;

        public bool _isFlying;
        public bool _isSwimming;
        private bool _swordCollected;
        private bool _despawn;

        public bool IsVisible { get; internal set; }
        public BodyComponent Body => _body;

        // Items that reset when the field changes in Classic Camera.
        private string[] _fieldChangeReset = GameSettings.ClassicItemDrop 
            ? new string[] { "shell", "dkey2" }
            : new string[] { "shell", "dkey2", "ruby", "heart", "pieceOfPower", "guardianAcorn", "bomb_1", "arrow_1", "powder_1" };

        // Items that use the small shadow.
        private string[] _shadowListSmall  = { "heart", "arrow_1" };

        // Items that use the normal sized shadow.
        private string[] _shadowListMedium = { "ruby", "bomb_1", "pieceOfPower", "guardianAcorn", "powderTrendy", "heart_1", "ruby30", "shield0" };

        public ObjSpriteShadow SpriteShadow;

        public ObjItem() : base("item") { }

        public ObjItem(Map.Map map, int posX, int posY, string strType, string saveKey, string itemName, string locationBound, bool despawn = false) : base(map)
        {
            // Start by getting basic properties of the item.
            _item = Game1.GameManager.ItemManager[itemName];
            _itemName = itemName;
            _locationBound = locationBound;
            _despawn = despawn;

            Tags = Values.GameObjectTag.Item;
            IsVisible = true;

            // The seashell can be exploited when found in bushes and Classic Camera is enabled.
            CanReset = _fieldChangeReset.Contains(_itemName);
            OnReset = Reset;

            // Checks if item is valid and has not been collected while also setting the save key.
            if (!ItemAndSaveKeyValid(_item, saveKey))
                return;

            var baseItem = _item.SourceRectangle.HasValue ? _item : Game1.GameManager.ItemManager[_item.Name];

            if (baseItem.MapSprite != null)
                _sourceRectangle = baseItem.MapSprite.SourceRectangle;
            else
                _sourceRectangle = baseItem.SourceRectangle.Value;

            EntityPosition = new CPosition(posX + 8, posY + 8 + 3, 0);
            EntitySize = new Rectangle(-9, -16, 18, 18);

            // The heart container piece needs additional offset on the Y axis.
            if (_itemName == "heartMeter" || _itemName == "heartMeterFull")
                EntityPosition.Y += 5;
            if (_item.TradeItem)
                EntityPosition.Y += 3;

            // Add sound for the bounces.
            _body = new BodyComponent(EntityPosition, -4, -8, 8, 8, 8)
            {
                RestAdditionalMovement = false,
                Gravity = -0.1f,
                Bounciness = 0.7f,
                IgnoreHeight = true,
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.LadderTop,
                HoleAbsorb = OnHoleAbsorb,
                MoveCollision = OnMoveCollision
            };

            // Attempt to apply attributes if the item type is set.
            TrySetItemType(strType);

            var stateIdle = new AiState(UpdateIdle);

            // Despawn after 15 seconds, but only if it was jumping or fall from the sky.
            if (string.IsNullOrEmpty(saveKey) && !_isFlying && !Collectable)
                stateIdle.Trigger.Add(new AiTriggerCountdown(15000, null, ToFading));

            var stateDelay = new AiState();
            var stateHoleFall = new AiState();
            stateHoleFall.Trigger.Add(new AiTriggerCountdown(125, null, HoleDespawn));
            stateDelay.Trigger.Add(_delayCountdown = new AiTriggerCountdown(0, null, () =>
            {
                IsVisible = true;
                _aiComponent.ChangeState("idle");
                _body.Velocity.Z = Map.Is2dMap ? -1f : 1f;
                EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y, 0));
            }));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("boomerang", new AiState());
            _aiComponent.States.Add("fading", new AiState(UpdateFading));
            _aiComponent.States.Add("delay", stateDelay);
            _aiComponent.States.Add("holeFall", stateHoleFall);
            _aiComponent.ChangeState("idle");

            Rectangle collectRect = _item.CreateCollectRectangle(_sourceRectangle);

            _collectionBox = new CBox(EntityPosition, -_sourceRectangle.Width / 2, collectRect.Y, _sourceRectangle.Width, collectRect.Height, 16);
            _collectionRect = new CRectangle(EntityPosition, collectRect);

            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(ObjectCollisionComponent.Index, new ObjectCollisionComponent(_collectionRect, OnCollision));
            AddComponent(CollisionComponent.Index, _collisionComponent = new BoxCollisionComponent(_collectionBox, Values.CollisionTypes.Item));
            AddComponent(HittableComponent.Index, new HittableComponent(_collectionBox, OnHit));

            if (_item.Instrument)
                _collisionComponent.CollisionType = Values.CollisionTypes.Item | Values.CollisionTypes.Instrument;

            // Create the body draw component but don't add it yet.
            _bodyDrawComponent = new BodyDrawComponent(_body, Draw, Values.LayerPlayer);

            // Create the shadow component but don't add it yet.
            var offsetX = -_sourceRectangle.Width / 2 - 1;
            var offsetY = -_sourceRectangle.Width / 4 - 2;
            var drawOffset = new Vector2(offsetX, offsetY);
            _shadowComponent = new DrawShadowSpriteComponent(null, Resources.SprShadow, EntityPosition, _shadowSourceRectangle, drawOffset, 1.0f, 0.0f) { Width = _sourceRectangle.Width + 2, Height = _sourceRectangle.Width / 2 + 2 };

            // Add the body and shadow components if it's not underwater.
            if (!_isSwimming)
            {
                AddComponent(DrawComponent.Index, _bodyDrawComponent);
                AddComponent(DrawShadowComponent.Index, _shadowComponent);
            }

            // If it's a shell present don't draw a shadow and draw on the bottom layer.
            if (_itemName == "shellPresent")
            {
                _shadowComponent.IsActive = false;
                _bodyDrawComponent.Layer = Values.LayerBottom;
            }
            if (_itemName == "sword2")
                _bodyDrawComponent.Layer = Values.LayerBottom;

            // Create the sprite shadows.
            if (strType == "d" || strType == "w")
                SpriteShadow = new ObjSpriteShadow(Map, this, Values.LayerPlayer, "sprshadowm");
            else if (_shadowListSmall.Contains(itemName))
                SpriteShadow = new ObjSpriteShadow(Map, this, Values.LayerPlayer, "sprshadows");
            else if (_shadowListMedium.Contains(itemName))
                SpriteShadow = new ObjSpriteShadow(Map, this, Values.LayerPlayer, "sprshadowm");

            // If a shadow was created, always animate it.
            if (SpriteShadow != null)
                Map.Objects.RegisterAlwaysAnimateObject(SpriteShadow);
        }

        public override void Init()
        {
            // Several spawn sites spawn the item at (0,0) and set the real position after,
            // so the field cannot be resolved until the object is actually spawned.
            _body.FieldRectangle = Map.GetField(EntityPosition.Position);

            _lastFieldTime = Map.GetUpdateState(EntityPosition.Position);
        }

        private new void Reset()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        public MapStates.FieldStates GetBodyFieldState()
        {
            return SystemBody.GetFieldState(_body);
        }

        private bool ItemAndSaveKeyValid(GameItem item, string saveKey)
        {
            // Default to "true" which creates the item.
            bool valid = true;

            // If the item is null then return false.
            if (item == null)
                valid = false;

            // Check if a save key has been defined.
            if (valid && !string.IsNullOrEmpty(saveKey))
            {
                // Store the save key name.
                SaveKey = saveKey;

                // Check if the item has already been collected.
                if (Game1.GameManager.SaveManager.GetString(SaveKey) == "1")
                    valid = false;
            }
            // If the item is null or it's already been collected set that it is dead.
            if (!valid)
                IsDead = true;

            // Return whatever the result is.
            return valid;
        }

        private void TrySetItemType(string strType)
        {
            // Check if the type of item has been set.
            if (!string.IsNullOrEmpty(strType))
            {
                // Jumping Item
                if (strType == "j")
                {
                    IsJumping = true;
                    if (!Map.Is2dMap)
                        _body.Velocity.Z = 1f;
                    else
                    {
                        Collectable = true;
                        _body.Velocity.Y = -1f;
                    }
                    _body.IsGrounded = false;
                }
                // Falling from the Sky
                else if (strType == "d")
                {
                    IsJumping = true;
                    EntityPosition.Z = 60;
                    _body.IsGrounded = false;
                    _body.RestAdditionalMovement = true;

                }
                // Flying Item
                else if (strType == "w")
                {
                    _body.IsActive = false;
                    EntityPosition.Z = 10;
                    Collectable = true;
                    _isFlying = true;
                }
                // Underwater Item
                else if (strType == "s")
                    _isSwimming = true;
            }
            // If the item type has not been set just make it "Collectable".
            else
                Collectable = true;
        }

        public void SpawnBoatSequence()
        {
            // shrink the collection rectangle
            _collectionRect.OffsetSize.X = (int)(_collectionRect.OffsetSize.X * 0.25f);
            _collectionRect.OffsetSize.Width = (int)(_collectionRect.OffsetSize.Width * 0.25f);
            _bodyDrawComponent.Layer = Values.LayerTop;

            _body.Velocity = new Vector3(1, -2.25f, 0);
            _body.DragAir = 1.0f;
        }

        public void SetVelocity(Vector3 velocity)
        {
            _body.Velocity = velocity;
        }

        public void InitCollection()
        {
            _body.IgnoresZ = true;
            Collectable = true;
            _aiComponent.ChangeState("boomerang");
        }

        public void SetSpawnDelay(int delay)
        {
            IsVisible = false;
            _delayCountdown.StartTime = delay;
            _aiComponent.ChangeState("delay");
        }

        private void UpdateIdle()
        {
            // Add "Field" collision with classic camera so items bounce off the sides of walls.
            _body.CollisionTypes = Camera.ClassicMode
                ? Values.CollisionTypes.Normal | Values.CollisionTypes.Field | Values.CollisionTypes.LadderTop
                : Values.CollisionTypes.Normal | Values.CollisionTypes.LadderTop;

            // If item is in the air and not able to be collected, make it collectable on the ground.
            if (_body.IsGrounded)
                Collectable = true;

            // Field went out of the update range?
            var updateState = Map.GetUpdateState(EntityPosition.Position);
            if (_lastFieldTime < updateState && _despawn)
                ToFading();

            if (!_body.IsActive)
                EntityPosition.Z = 20 - _sourceRectangle.Height / 2 + (float)Math.Sin((Game1.TotalGameTime / 1050) * Math.PI * 2) * 1.5f;

            // Fall into the water.
            if (!_isSwimming && !Map.Is2dMap)
            {
                if (_body.IsGrounded && _body.CurrentFieldState.HasFlag(MapStates.FieldStates.DeepWater))
                {
                    _deepWaterCounter -= Game1.DeltaTime;

                    if (_deepWaterCounter <= 0)
                    {
                        // Spawn splash effect.
                        var fallAnimation = new ObjAnimator(Map,
                            (int)(_body.Position.X + _body.OffsetX + _body.Width / 2.0f),
                            (int)(_body.Position.Y + _body.OffsetY + _body.Height / 2.0f),
                            Values.LayerPlayer, "Particles/fishingSplash", "idle", true);

                        Map.Objects.SpawnObject(fallAnimation);
                        Map.Objects.DeleteObjects.Add(this);
                        RemoveSpriteShadow();
                    }
                }
                else
                {
                    _deepWaterCounter = 125;
                }
            }

            if (!Map.Is2dMap)
                _shadowComponent.Color = Color.White * ((128 + EntityPosition.Z) / 128f);
            else
                _shadowComponent.Color = _body.IsGrounded ? Color.White : Color.Transparent;
        }

        private void ToFading()
        {
            if (SpriteShadow != null)
                SpriteShadow.ForceDraw = true;
            _body.IgnoresZ = true;
            _aiComponent.ChangeState("fading");
        }

        private void UpdateFading()
        {
            _despawnCount += Game1.DeltaTime;

            // move item up if it was collected
            if (Collected && _despawnCount < _moveStopTime)
                _fadeOffset = -(float)Math.Sin(_despawnCount / _moveStopTime * Math.PI / 1.5f) * 10;

            // fade the item after fadestart
            if (_fadeStart <= _despawnCount)
                _color = Color.White * (1 - ((_despawnCount - _fadeStart) / (_despawnTime - _fadeStart)));

            _shadowComponent.Color = _color;

            // remove the object
            if (_despawnCount > _despawnTime)
            {
                Map.Objects.DeleteObjects.Add(this);
                RemoveSpriteShadow();
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!IsVisible)
                return;

            ItemDrawHelper.DrawItem(spriteBatch, _item,
                new Vector2(EntityPosition.X - _sourceRectangle.Width / 2.0f, EntityPosition.Y - EntityPosition.Z - _sourceRectangle.Height + _fadeOffset), _color, 1, true);

            if (!_isFlying)
                return;

            var wingFlap = (Game1.TotalGameTime % (16 / 60f * 1000)) < (8 / 60f * 1000) ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // Draw the wings and snap pixels if enabled.
            var baseX = GameSettings.PixelSnapping
                ? MathF.Round(EntityPosition.X)
                : EntityPosition.X;
            var baseY = GameSettings.PixelSnapping
                ? MathF.Round(EntityPosition.Y - EntityPosition.Z - _sourceRectangle.Height / 2 - 10 + _fadeOffset)
                : EntityPosition.Y - EntityPosition.Z - _sourceRectangle.Height / 2 - 10 + _fadeOffset;

            spriteBatch.Draw(Resources.SprItem, new Vector2(baseX - _sourceRectangleWing.Width - 4f, baseY), _sourceRectangleWing, _color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None | wingFlap, 0);
            spriteBatch.Draw(Resources.SprItem, new Vector2(baseX + 4f, baseY), _sourceRectangleWing, _color, 0, Vector2.Zero, Vector2.One, SpriteEffects.FlipHorizontally | wingFlap, 0);
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If it's an instrument collide with items.
            if (_item.Instrument)
            {
                if ((hitType & HitType.Sword) != 0)
                {
                    if (_item.SwordCollect)
                        Collect();

                    return Values.HitCollision.None;
                }
                if ((hitType & HitType.Hookshot) != 0 || (hitType & HitType.Boomerang) != 0)
                    return Values.HitCollision.RepellingParticle;

                return Values.HitCollision.None;
            }
            // Item can be collected with the sword.
            if ((hitType & HitType.Sword) != 0 && _item.SwordCollect)
            {
                _swordCollected = true;
                Collect();
            }
            return Values.HitCollision.None;
        }

        private void OnMoveCollision(Values.BodyCollision collision)
        {
            // sound should play but only for the trendy game? maybe add a extra item type?
            if ((collision & Values.BodyCollision.Floor) != 0 && _body.Velocity.Z > 0.55f ||
                ((collision & Values.BodyCollision.Bottom) != 0 && _body.Velocity.Y < 0f && Map.Is2dMap))
            {
                // metalic bounce sound
                if (_item.Name == "smallkey" || _item.Name == "sword2")
                    Game1.AudioManager.PlaySoundEffect("D378-23-17");
                else
                    Game1.AudioManager.PlaySoundEffect("D360-09-09");
            }
        }

        private void OnHoleAbsorb()
        {
            if (Collected)
                return;

            _body.IsActive = false;
            if (_aiComponent.CurrentStateId != "holeFall")
                _aiComponent.ChangeState("holeFall");
        }

        private void HoleDespawn()
        {
            // play sound effect
            Game1.AudioManager.PlaySoundEffect("D360-24-18");

            var fallAnimation = new ObjAnimator(Map, (int)EntityPosition.X - 5, (int)EntityPosition.Y - 8, Values.LayerBottom, "Particles/fall", "idle", true);
            Map.Objects.SpawnObject(fallAnimation);

            Map.Objects.DeleteObjects.Add(this);
            RemoveSpriteShadow();
        }

        private void OnCollision(GameObject gameObject)
        {
            // Do not collect items if Link is falling down a hole. This prevents a bug in Yarna Desert when 
            // collecting the Angler Key while falling where the key is collected but it is not added to the inventory. 
            if (MapManager.ObjLink.CurrentState == ObjLink.State.Falling)
                return;

            // Only collect the item when the player is near it in the Z dimension.
            if (Math.Abs(EntityPosition.Z - MapManager.ObjLink.EntityPosition.Z) < 8 && (!_isSwimming || MapManager.ObjLink.IsDiving()))
                Collect();
        }

        private void RemoveSpriteShadow()
        {
            if (SpriteShadow != null)
                Map.Objects.DeleteObjects.Add(SpriteShadow);
        }

        private void Collect()
        {
            if (!Collectable || Collected)
                return;

            if (_isFlying && MapManager.ObjLink.EntityPosition.Z < 7)
                return;

            // Do not collect the item while the player is jumping unless it was collected with the sword.
            if (_item.ShowAnimation != 0 && !_swordCollected &&
                ((!Map.Is2dMap && !MapManager.ObjLink.IsGrounded()) || (Map.Is2dMap && !MapManager.ObjLink.IsGrounded() && !MapManager.ObjLink.IsInWater2D())))
            {
                return;
            }
            Collected = true;
            _body.IsActive = false;
            _bodyDrawComponent.WaterOutline = false;

            if (Map.Is2dMap)
                _body.Velocity.Y = 0f;
            else
                _body.Velocity.Z = 0f;

            // gets picked up
            var cItem = new GameItemCollected(_itemName)
            {
                Count = _item.Count,
                LocationBounding = _locationBound
            };
            MapManager.ObjLink.PickUpItem(cItem, true);

            // do not fade away the item if it the player shows it
            if (_item.ShowAnimation != 0)
            {
                Map.Objects.DeleteObjects.Add(this);
                RemoveSpriteShadow();
            }
            else
                ToFading();

            if (SaveKey != null)
            {
                Game1.GameManager.SaveManager.SetString(SaveKey, "1");

                // If it's the first Master Stalfos key give the achievement.
                if (SaveKey == "d5_ms_key_0")
                    AchievementManager.Earn(65);
            }
        }
    }
}
