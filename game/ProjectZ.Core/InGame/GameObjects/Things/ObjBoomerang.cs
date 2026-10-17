using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    public class ObjBoomerang : GameObject
    {
        private readonly List<GameObject> _itemList = new List<GameObject>();
        private readonly BodyComponent _body;
        private readonly CBox _damageBox;

        private AnimationComponent _animationComponent;
        private Animator _animator;
        private CSprite _sprite;

        private List<ObjItem> _itemsGrabbed = new List<ObjItem>();
        private ObjDungeonFairy _fairy;

        private Vector2 _startPosition;
        private Vector2 _direction;

        private bool _comingBack;
        private bool _swordBounce;

        public ObjBoomerang()
        {
            EntityPosition = new CPosition(0, 0, 4);
            EntityPosition.AddPositionListener(typeof(ObjBoomerang), UpdateItemPosition);
            EntityPosition.AddPositionListener(typeof(ObjBoomerang), UpdateFairyPosition);
            EntitySize = new Rectangle(-8, -12, 16, 16);

            _damageBox = new CBox(EntityPosition, -5, -5, 0, 10, 10, 20, true);

            _animator = GameSettings.ClassicSprites 
                ? AnimatorSaveLoad.LoadAnimator("Objects/boomerangOrig")
                : AnimatorSaveLoad.LoadAnimator("Objects/boomerang");

            _animator.Play("run");

            _body = new BodyComponent(EntityPosition, -1, -1, 2, 2, 8)
            {
                IgnoresZ = true,
                MoveCollision = OnCollision,
                CollisionTypesIgnore = Values.CollisionTypes.ThrowWeaponIgnore | Values.CollisionTypes.Switch | Values.CollisionTypes.Bush,
                IgnoreInsideCollision = false,
            };
            _sprite = new CSprite(EntityPosition);

            AddComponent(BodyComponent.Index, _body);
            AddComponent(HittableComponent.Index, new HittableComponent(_damageBox, OnHit));
            AddComponent(BaseAnimationComponent.Index, _animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-6, -6)));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));
        }

        public void ToggleAnimator(bool toggle)
        {
            // Update the boomerang animator based on the selected setting.
            _animator = toggle 
                ? AnimatorSaveLoad.LoadAnimator("Objects/boomerangOrig")
                : AnimatorSaveLoad.LoadAnimator("Objects/boomerang");

            // Start playing the animation and create a new animation component. Recyling the old one does not work.
            _animator.Play("run");
            _animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-6, -6));

            // The only thing that worked here was to remove the base component and readd it.
            RemoveComponent(BaseAnimationComponent.Index);
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
        }

        public void Start(Map.Map map, Vector3 position, Vector2 direction)
        {
            Map = map;
            EntityPosition.Set(new Vector3(position.X, position.Y, position.Z + 4));

            _startPosition = new Vector2(position.X, position.Y);
            _direction = direction;
            _body.VelocityTarget = Vector2.Zero;
            _body.CollisionTypes = Values.CollisionTypes.Normal;
            _body.Level = MapStates.GetLevel(MapManager.ObjLink.Body.CurrentFieldState);

            _comingBack = false;
            _itemsGrabbed.Clear();
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If "Sword Item Interactions" is enabled, bounce the boomerang off the sword.
            if ((hitType & HitType.Sword) != 0 && GameSettings.SwBoomerang && !_swordBounce && _comingBack)
            {
                _comingBack = false;
                _swordBounce = true;
                _startPosition = EntityPosition.Position;
                _body.CollisionTypes = Values.CollisionTypes.Normal;

                // Redirect the boomerang toward the direction Link is facing.
                _direction = MapManager.ObjLink.ForwardVector;
                _body.VelocityTarget = _direction * 3f;

                var animation = new ObjSparkingEffect(Map, 0, 0, 0, 0);
                animation.EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y - EntityPosition.Z, 0));
                Map.Objects.SpawnObject(animation);
                Game1.AudioManager.PlaySoundEffect("D360-07-07");
            }
            return Values.HitCollision.None;
        }

        private void Update()
        {
            // A null map can cause a crash so make sure it isn't null for some reason.
            if (Map == null) 
                return;

            // Set up some shortcut vars for Link.
            ObjLink Link = MapManager.ObjLink;
            Vector2 LinkPos = Link.EntityPosition.Position;

            // Play sound effect.
            Game1.AudioManager.PlaySoundEffect("D378-45-2D", false);

            // The boomerang has just been thrown out or is moving away from the player.
            if (!_comingBack)
            {
                // Update the boomerang's position.
                EntityPosition.Z = AnimationHelper.MoveToTarget(EntityPosition.Z, 4, 0.35f * Game1.TimeMultiplier);

                // Get the position and set the speed of the boomerang.
                float distance = (_startPosition - EntityPosition.Position).Length();
                float speed = 3f - (float)Math.Sin(MathHelper.Clamp(distance / 80, 0, 1) * (Math.PI / 2));
                _body.VelocityTarget = _direction * speed;

                // When Modern Camera is enabled, use the camera's current bounds to determine when object collides with screen's edge. 
                if (!Camera.ClassicMode && !MapManager.Camera.GetGameView().Contains(EntityPosition.Position))
                {
                    ComeBack(true);
                    return;
                }
                // When Classic Camera is enabled, use current field to determine when object collides with screen's edge. 
                else if (Camera.ClassicMode && !Link.CurrentField.Contains(EntityPosition.Position))
                {
                    ComeBack(true);
                    return;
                }
                // The distance from Link that the boomerang travels.
                else if (distance >= 80)
                    ComeBack();
            }
            // The boomerang is making its return trip.
            else
            {
                // Update the boomerang's position.
                EntityPosition.Z = AnimationHelper.MoveToTarget(EntityPosition.Z, 4, 1.25f * Game1.TimeMultiplier);

                // Get the direction, position, and set the speed of the boomerang.
                Vector2 direction = new Vector2(LinkPos.X, LinkPos.Y - 3) - EntityPosition.Position;
                float distance = direction.Length();
                float speed = Math.Min(3f - (float)Math.Sin(MathHelper.Clamp(distance / 80, 0, 1) * (Math.PI / 2)), distance);

                // Normalize the direction if it's not zero.
                if (direction != Vector2.Zero)
                    direction.Normalize();

                // Apply the speed to the boomerang.
                _body.VelocityTarget = direction * speed;

                // Remove the boomerang when it returns to Link.
                if ((Map.Is2dMap || Math.Abs(Link.EntityPosition.Z - EntityPosition.Z) <= 6) && distance < 2)
                    Despawn();
            }
            // Find items to collect.
            CollectItem();

            // The default damage of the Boomerang.
            var damage = 2;

            // Find enemies to hit.
            var collision = Map.Objects.Hit(this, EntityPosition.Position, _damageBox.Box, HitType.Boomerang, damage, false);

            // Return the boomerang if it collision hits something.
            if (!_comingBack && (collision & (Values.HitCollision.Blocking | Values.HitCollision.Repelling | Values.HitCollision.Enemy)) != 0)
            {
                var particle = (collision & Values.HitCollision.Repelling) != 0;
                ComeBack(particle);
            }
        }

        private void CollectItem()
        {
            // I once experienced a strange crash where "Map" was null. Not sure how... so prevent that I guess.
            if (Map == null) { return; }

            _itemList.Clear();

            // Search for objects via collision component mask.
            Map.Objects.GetComponentList(_itemList, (int)_damageBox.Box.X, (int)_damageBox.Box.Y,
                (int)_damageBox.Box.Width, (int)_damageBox.Box.Height, CollisionComponent.Mask);

            // Check if an item was found.
            foreach (var gameObject in _itemList)
            {
                // Create box reference and get object via collision component.
                var collidingBox = Box.Empty;
                var collisionObject = gameObject.Components[CollisionComponent.Index] as CollisionComponent;

                // Get objects colliding with the boomerang.
                if ((collisionObject.CollisionType & Values.CollisionTypes.Item) != 0 && 
                    collisionObject.Collision(_damageBox.Box, 0, 0, ref collidingBox))
                {
                    // Type of colliding object is an item.
                    if (collisionObject.Owner is ObjItem newItem)
                    {
                        // The item is an instrument.
                        var isInstrument  = newItem._itemName.Contains("instrument");
                        var isSwordLevel2 = newItem._itemName == "sword2";

                        // Item must be active, not collected, not flying, not an instrument, and not in the boomerang list.
                        if (newItem.IsActive && !newItem.Collected && !newItem._isFlying && !isInstrument && !_itemsGrabbed.Contains(newItem) && !isSwordLevel2)
                        {
                            _itemsGrabbed.Add(newItem);
                            newItem.InitCollection();
                        }
                    }
                    // Type of colliding object is a fairy.
                    else if (collisionObject.Owner is ObjDungeonFairy fairy)
                    {
                        _fairy = fairy;
                        _fairy.RemoveCooldown();
                    }
                }
            }
        }

        private void UpdateItemPosition(CPosition position)
        {
            // Loop through the items in the list.
            foreach (var item in _itemsGrabbed.ToList())
            {
                // If itemn wasn't collected update its postion.
                if (item != null && !item.Collected)
                    item.EntityPosition.Set(new Vector3(position.X, position.Y + 4, position.Z));

                // Otherwise remove the item from the list.
                else
                    _itemsGrabbed.Remove(item);
            }
        }

        public void Despawn()
        {
            if (Map != null)
                Map.Objects.DeleteObjects.Add(this);
        }

        private void UpdateFairyPosition(CPosition position)
        {
            // Update the position of the fairy.
            _fairy?.EntityPosition.Set(new Vector3(position.X, position.Y + 4, position.Z));
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            // Return the boomerang if it hits a wall.
            ComeBack(true);
        }

        private void ComeBack(bool particle = false)
        {
            // A null map can cause a crash so make sure it isn't null for some reason.
            if (Map == null) 
                return;

            // Draw the "sparking" effect and play the sound if set.
            if (particle)
            {
                var animation = new ObjSparkingEffect(Map, 0, 0, 0, 0);
                animation.EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y - EntityPosition.Z, 0));
                Map.Objects.SpawnObject(animation);
                Game1.AudioManager.PlaySoundEffect("D360-07-07");
            }
            // Specify that it's coming back and remove collision.
            _comingBack = true;
            _swordBounce = false;
            _body.CollisionTypes = Values.CollisionTypes.None;
        }
    }
}
