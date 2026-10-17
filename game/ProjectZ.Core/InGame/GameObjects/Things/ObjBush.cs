using System.IO;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjBush : GameObject, IHasSpriteVisibility
    {
        private readonly BodyComponent _body;
        private readonly BoxCollisionComponent _collisionComponent;
        private readonly CarriableComponent _carriableComponent;
        private readonly HittableComponent _hitComponent;
        private readonly CBox _hittableBox;
        private readonly CBox _upperBox;
        private readonly CBox _lowerBox;
        private readonly CSprite _sprite;
        private readonly CPosition _respawnPosition;

        private readonly string _spawnItem;
        private readonly string _spriteId;
        private readonly bool _hasCollider;
        private readonly bool _drawShadow;
        private readonly bool _setGrassField;
        private readonly int _drawLayer;
        private readonly string _pickupKey;
        private readonly object[] _spawnObjectParameter;
        private readonly string _spawnObjectId;
        private readonly int _fieldPosX;
        private readonly int _fieldPosY;

        private Rectangle _field;
        private bool _isThrown;

        // Properties shared with "EnemySpinyBeetle".
        public EnemySpinyBeetle SpinyBeetle;
        public bool NoRespawn = false;
        public bool OnSpinyBeetle = false;
        public bool IsGrass = false;

        public BoxCollisionComponent CollideComponent => _collisionComponent;
        public HittableComponent HitComponent => _hitComponent;
        public CarriableComponent CarryComponent => _carriableComponent;

        // Values configurable via lahdmod.
        private bool  fall_to_ground   = false;
        private float bush_leaf_alpha  = 0.90f;
        private float grass_leaf_alpha = 0.50f;

        // Properties shared with "ObjLink".
        public CSprite Sprite => _sprite;

        public ObjBush(Map.Map map, int posX, int posY, string spawnItem, string spriteId,
            bool hasCollider, bool drawShadow, bool setGrassField, int drawLayer, string pickupKey) : base(map, spriteId)
        {
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjBush.lahdmod");
            ModFile.Parse(modFile, this);

            var sprite = Resources.GetSprite(spriteId);

            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);

            _respawnPosition = new CPosition(posX + 8, posY + 8, 0);
            _spawnItem = spawnItem;
            _spriteId = spriteId;
            _hasCollider = hasCollider;
            _drawShadow = drawShadow;
            _setGrassField = IsGrass = setGrassField;
            _drawLayer = drawLayer;
            _pickupKey = pickupKey;

            _field = Map.GetField(posX, posY);
            _fieldPosX = posX / 16;
            _fieldPosY = posY / 16;

            // {objName}:{parameter.parameter1...}
            if (!string.IsNullOrEmpty(spawnItem))
            {
                var split = spawnItem?.Split(':');
                if (split?.Length >= 1)
                {
                    _spawnObjectId = split[0];
                    string[] parameter = null;

                    if (split.Length >= 2)
                        parameter = split[1].Split('.');

                    _spawnObjectParameter = MapData.GetParameter(_spawnObjectId, parameter);
                    if (_spawnObjectParameter == null)
                        return;

                    _spawnObjectParameter[1] = posX;
                    _spawnObjectParameter[2] = posY;
                }
            }

            _upperBox = new CBox(EntityPosition, -4, -5, 0, 8, 8, 4, true);
            _lowerBox = new CBox(EntityPosition, -4, -5, 0, 8, 8, 4);
            _hittableBox = new CBox(EntityPosition, -8, -7, 0, 16, 14, 8, true);

            if (hasCollider)
            {
                _body = new BodyComponent(EntityPosition, -4, -4, 8, 8, 8)
                {
                    MoveCollision = Collision,
                    DragAir = 1.0f,
                    Gravity = -0.125f
                };
                var collisionBox = new CBox(EntityPosition, -8, -7, 0, 16, 14, 16, true);
                AddComponent(BodyComponent.Index, _body);
                AddComponent(CollisionComponent.Index, _collisionComponent = new BoxCollisionComponent(collisionBox, Values.CollisionTypes.Normal | Values.CollisionTypes.Bush));
                AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent( new CRectangle(EntityPosition, new Rectangle(-8, -6, 16, 14)), CarryInit, CarryUpdate, CarryThrow) { StartGrabbing = StartGrabbing });
                AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            }

            if (setGrassField)
                Map.SetFieldState(_fieldPosX, _fieldPosY, MapStates.FieldStates.Grass);

            _sprite = new CSprite(spriteId, EntityPosition, Vector2.Zero);
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_hittableBox, OnHit) { RespondClassicSword = true });
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, drawLayer));

            if (drawShadow)
            {
                // not sure where this is used
                if (_body == null)
                    AddComponent(DrawShadowComponent.Index, new DrawShadowSpriteComponent(_sprite, Resources.SprObjects, EntityPosition, sprite.ScaledRectangle, new Vector2(sprite.Origin.X + 1.0f, sprite.Origin.Y - 1.0f), 1.0f, 0.0f));
                else
                {
                    AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));
                    new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
                }
            }
        }

        private void Update()
        {
            if (!_isThrown)
                return;

            // When classic camera is enabled rocks, pots, etc. should smash against the edge of the field.
            var collisionType = Camera.ClassicMode
                ? Values.CollisionTypes.Normal | Values.CollisionTypes.Field
                : Values.CollisionTypes.Normal;

            // This is used because the normal collision detection looks strang when throwing directly towards a lower wall.
            var outBox = Box.Empty;
            if (!Map.Is2dMap &&
                Map.Objects.Collision(_upperBox.Box, Box.Empty, collisionType, 0, _body.Level, ref outBox) &&
                Map.Objects.Collision(_lowerBox.Box, Box.Empty, collisionType, 0, _body.Level, ref outBox))
                DestroyBush(Vector2.Zero);

            // The amount of damage dealt when hitting an enemy with a bush.
            var damage = 2;

            // Try to find an object to hit. The bush will try to hit itself so this is worked around in the "OnHit" method.
            var hitCollision = Map.Objects.Hit(this, _hittableBox.Box.Center, _hittableBox.Box, HitType.ThrownObject, damage, false);

            // When the bush hits something, destroy it.
            if (hitCollision != Values.HitCollision.None && hitCollision != Values.HitCollision.NoneBlocking)
                DestroyBush(new Vector2(_body.Velocity.X, _body.Velocity.Y));
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            _collisionComponent.IsActive = false;
            _body.IsActive = false;
            SpawnItem(Vector2.Zero);
            return new Vector3(EntityPosition.X, EntityPosition.Y + 6, EntityPosition.Z);
        }

        private bool CarryUpdate(Vector3 newPosition)
        {
            EntityPosition.X = newPosition.X;
            EntityPosition.Y = newPosition.Y - 6;
            EntityPosition.Z = newPosition.Z;
            EntityPosition.NotifyListeners();
            return true;
        }

        private void CarryThrow(Vector2 velocity)
        {
            _isThrown = true;
            _carriableComponent.Thrown = true;
            _body.IsGrounded = false;
            _body.IsActive = true;
            _body.Velocity = new Vector3(velocity.X, velocity.Y, 0) * 1.0f;
        }

        private void Collision(Values.BodyCollision direction)
        {
            DestroyBush(new Vector2(_body.Velocity.X, _body.Velocity.Y));
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If the bush is being carried by a Spiny Beetle.
            if (OnSpinyBeetle)
            {
                // For these hit types, we want to kill the bush but not the beetle.
                if ((hitType & HitType.SwordShot) != 0 || (hitType & HitType.Hookshot) != 0 || (hitType & HitType.Bow) != 0)
                {
                    Game1.AudioManager.PlaySoundEffect("D360-03-03");
                    DestroyBush(direction);
                    return Values.HitCollision.Blocking;
                }
                // Getting this to work like the original game with the bomb requires some trickery.
                if ((hitType & HitType.Bomb) != 0)
                    return Values.HitCollision.None;

                // When burning the beetle, we want the bush to remain intact while it burns.
                if ((hitType & HitType.MagicPowder) != 0 || (hitType & HitType.MagicRod) != 0)
                    return Values.HitCollision.None;
            }
            // Link must be on the same field as the bush with Classic Camera.
            if (Camera.ClassicMode && !_field.Contains(MapManager.ObjLink.CenterPosition))
                return Values.HitCollision.None;

            // If "Classic Sword" is enabled, only the tile that the bush/grass is on should "hit".
            if (MapManager.ObjLink.ClassicSword && (hitType & HitType.Sword) != 0 && !MapManager.ObjLink.IsPoking)
                return Values.HitCollision.None;

            // Prevent the bush from colliding with itself. Because it "can hit" and also "be hit"
            // the collision box is in conflict with itself and self denotates when thrown.
            if (hitType == HitType.ThrownObject && gameObject.GetType() == typeof(ObjBush))
                return Values.HitCollision.None;

            // Boomerangs and Magic Rod create a small explosion.
            if (!OnSpinyBeetle && (hitType & HitType.Boomerang) != 0 || (hitType & HitType.MagicRod) != 0)
            {
                // Don't do this to grass, only bushes.
                if (!_setGrassField)
                {
                    // Spawn in the smoke effect.
                    var animation = new ObjBushExplosion(Map, (int)EntityPosition.X, (int)EntityPosition.Y - (int)EntityPosition.Z, 0, 0);
                    Map.Objects.SpawnObject(animation);
                    Map.Objects.RegisterAlwaysAnimateObject(animation);

                    // If the bush is on a beetle or creatine with an ObjObjectRespawner don't respawn it.
                    if (!NoRespawn)
                        Map.Objects.SpawnObject(new ObjBushRespawner(Map, (int)_respawnPosition.X - 8, (int)_respawnPosition.Y - 8, _spawnItem, _spriteId, _hasCollider, _drawShadow, _setGrassField, _drawLayer, _pickupKey));
 
                    // Play a specialized sound effect and remove the bush outright.
                    Game1.AudioManager.PlaySoundEffect("D378-19-13");
                    Map.Objects.DeleteObjects.Add(this);
                    return Values.HitCollision.None;
                }
            }
 
            // Magic Powder has unique death.
            if ((hitType & HitType.MagicPowder) != 0!)
            {
                // Don't do this to grass, only bushes.
                if (!_setGrassField)
                {
                    // Create a respawner for the bush.
                    if (!NoRespawn)
                        Map.Objects.SpawnObject(new ObjBushRespawner(Map, (int)_respawnPosition.X - 8, (int)_respawnPosition.Y - 8, _spawnItem, _spriteId, _hasCollider, _drawShadow, _setGrassField, _drawLayer, _pickupKey));

                    // We just delete the bush instead of returning damage state.
                    IsDead = true;
                    Map.Objects.DeleteObjects.Add(this);

                    // Try to spawn an item.
                    SpawnItem(direction);

                    // Play the sound and show the smoke effect.
                    Game1.AudioManager.PlaySoundEffect("D360-09-09");
                    Game1.AudioManager.PlaySoundEffect("D360-47-2F");
                    var explosionAnimation = new ObjAnimator(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 8, Values.LayerTop, "Particles/spawn", "run", true);
                    Map.Objects.SpawnObject(explosionAnimation);
                }
                return Values.HitCollision.None;
            }

            // Damage types that don't destroy the bush. If it does not have a collider it is grass.
            if (IsDead ||
                (hitType & HitType.SwordHold) != 0 ||
                (hitType & HitType.SwordPoke) != 0 && !_hasCollider ||
                hitType == HitType.Bow ||
                hitType == HitType.Hookshot ||
                ((!GameSettings.SwBeamShrubs || Game1.GameManager.GetItem("sword2") == null) && hitType == HitType.SwordShot) ||
                hitType == HitType.PegasusBootsPush ||
                hitType == HitType.MagicRod && !_hasCollider ||
                hitType == HitType.Boomerang && !_hasCollider ||
                hitType == HitType.ThrownObject && !_hasCollider)
                return Values.HitCollision.None;

            // A smaller hitbox is used for sword attacks on bushes.
            if (gameObject is ObjLink player && (hitType & HitType.Sword) != 0)
            {
                var collidingRec = player.SwordDamageBox.Rectangle().GetIntersection(_hittableBox.Box.Rectangle());
                var collidingArea = collidingRec.Width * collidingRec.Height;

                if (collidingArea < 16)
                    return Values.HitCollision.None;
            }
            // Try to spawn an item and destroy the bush.
            SpawnItem(direction);
            DestroyBush(direction);
            return Values.HitCollision.NoneBlocking;
        }

        private void SpawnItem(Vector2 direction)
        {
            // set the pickup key
            if (!string.IsNullOrEmpty(_pickupKey))
                Game1.GameManager.SaveManager.SetString(_pickupKey, "1");

            // spawn the object if it exists
            bool spawnedObject = false;

            // try to spawn the object
            if (!string.IsNullOrEmpty(_spawnObjectId))
            {
                var objSpawnedObject = ObjectManager.GetGameObject(Map, _spawnObjectId, _spawnObjectParameter);
                spawnedObject = Map.Objects.SpawnObject(objSpawnedObject);
                if (spawnedObject && objSpawnedObject is ObjItem spawnedItem)
                    spawnedItem.SetVelocity(new Vector3(direction.X * 0.5f, direction.Y * 0.5f, 0.75f));

                // If the spawned object is a hole there needs to be a way to track it was created
                // from a bush so it can be set to inactive when resetting the current field.
                if (objSpawnedObject is ObjHole spawnedHole)
                    spawnedHole.BushSpawn();
            }
            // Try to spawn a rupee or a heart.
            if (!spawnedObject)
            {
                // There is a 1 in 8 chance (12.5%) an item is dropped.
                if (Game1.RandomNumber.Next(0,8) != 0)
                    return;

                // Roll a heart or a rupee. Overall it's a 6.25% chance per item to drop.
                var itemSpawn = Game1.RandomNumber.Next(0,2) == 0
                    ? "heart"
                    : "ruby";

                // If it was a heart and item drops are disabled, nullify it.
                if (GameSettings.NoHeartDrops && itemSpawn == "heart")
                    itemSpawn = "";

                // If something is supposed to drop.
                if (!string.IsNullOrEmpty(itemSpawn)) 
                {
                    var objItem = new ObjItem(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 8, "j", null, itemSpawn, null, true);
                    objItem.SetVelocity(new Vector3(direction.X * 0.5f, direction.Y * 0.5f, 0.75f));
                    Map.Objects.SpawnObject(objItem);
                }
            }
        }

        public void DestroyBush(Vector2 direction)
        {
            if (IsDead) return;
            IsDead = true;
 
            Game1.AudioManager.PlaySoundEffect("D378-05-05");
 
            if (!NoRespawn)
                Map.Objects.SpawnObject(new ObjBushRespawner(Map, (int)_respawnPosition.X - 8, (int)_respawnPosition.Y - 8, _spawnItem, _spriteId, _hasCollider, _drawShadow, _setGrassField, _drawLayer, _pickupKey));
 
            Map.Objects.DeleteObjects.Add(this);
            Map.RemoveFieldState(_fieldPosX, _fieldPosY, MapStates.FieldStates.Grass);
 
            var classic = !fall_to_ground;
            var bushAlpha = bush_leaf_alpha;
            var grassAlpha = grass_leaf_alpha;

            // Track if a bush is destroyed carried by a beetle.
            if (OnSpinyBeetle)
                SpinyBeetle.ObjectDestroyed = true;

            // Leaves spread out like the original game.
            if (classic)
            {
                bool isSwamp = DetermineSwampVariant();
                for (int i = 0; i < 4; i++)
                {
                    Map.Objects.SpawnObject(new ObjLeafClassic(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 8, _setGrassField, isSwamp, i, bushAlpha, grassAlpha));
                }
            }
            // Leaves fall to the ground slowly.
            else
            {    
                var offsets = new[] { new Point(-7, -1), new Point(1, -1), new Point(-7, 7), new Point(1, 7) };
                for (var i = 0; i < offsets.Length; i++)
                {
                    var posZ = EntityPosition.Z + 5 - Game1.RandomNumber.Next(0, 40) / 10f;
                    var newLeaf = new ObjLeaf(Map, (int)EntityPosition.X + offsets[i].X, (int)EntityPosition.Y + offsets[i].Y, posZ, _setGrassField, direction, bushAlpha, grassAlpha);
                    Map.Objects.SpawnObject(newLeaf);
                }
            }
        }
 
        private bool DetermineSwampVariant()
        {
            return false;
        }
    }
}
