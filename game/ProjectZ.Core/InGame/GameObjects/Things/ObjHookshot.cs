using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    public class ObjHookshot : GameObject
    {
        public CPosition HookshotPosition => _hookshotPosition;

        public bool IsMoving;

        private readonly List<GameObject> _itemList = new List<GameObject>();
        private readonly CPosition _hookshotPosition;
        private ObjItem _item;
        private ObjDungeonFairy _fairy;

        private readonly DictAtlasEntry _spriteChain;
        private readonly DictAtlasEntry _spriteHook;

        private readonly BodyComponent _body;
        private readonly CBox _damageBox;

        private Vector2 _direction;
        private Vector2 _startPositionOffset;

        private const float Speed = 3;

        private bool _comingBack;
        private bool _pullingPlayer;
        private bool _pokeParticleSpawned;

        private float _soundCounter;
        private int _damage = 2;

        public ObjHookshot()
        {
            _hookshotPosition = new CPosition(0, 0, 0);
            _hookshotPosition.AddPositionListener(typeof(ObjHookshot), UpdateItemPosition);
            _hookshotPosition.AddPositionListener(typeof(ObjHookshot), UpdateFairyPosition);

            EntitySize = new Rectangle(-10, -10, 20, 20);

            _damageBox = new CBox(_hookshotPosition, -3, -3, 0, 6, 6, 8, true);

            _body = new BodyComponent(_hookshotPosition, -2, -2, 4, 4, 8)
            {
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Instrument,
                CollisionTypesIgnore = Values.CollisionTypes.Switch,
                IgnoresZ = true,
                IgnoreHoles = true,
                IgnoreInsideCollision = false,
                MoveCollision = OnCollision,
            };

            _spriteChain = Resources.GetSprite("hookshot_chain");
            _spriteHook = Resources.GetSprite("hookshot_hook");

            AddComponent(BodyComponent.Index, _body);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, _hookshotPosition));
        }

        public override void Reset()
        {
            IsMoving = false;
        }

        public void Start(Map.Map map, Vector3 position, Vector2 direction)
        {
            Map = map;

            _hookshotPosition.Set(position);
            _startPositionOffset = new Vector2(position.X, position.Y) - MapManager.ObjLink.Position;

            _body.VelocityTarget = direction * Speed;
            _body.CollisionTypes = Values.CollisionTypes.Normal | Values.CollisionTypes.Hookshot;
            _body.Level = MapStates.GetLevel(MapManager.ObjLink.Body.CurrentFieldState);

            _direction = direction;

            _comingBack = false;
            _pullingPlayer = false;
            _item = null;

            IsMoving = true;
            _pokeParticleSpawned = false;
        }

        private void Update()
        {
            _soundCounter += Game1.DeltaTime;
            if (_soundCounter > 65)
            {
                _soundCounter -= 65;
                Game1.AudioManager.PlaySoundEffect("D378-11-0B", true);
            }

            if (_pullingPlayer)
            {
                if (!MapManager.ObjLink.UpdateHookshotPull())
                    Despawn();

                return;
            }

            var direction = MapManager.ObjLink.Position + _startPositionOffset - _hookshotPosition.Position;
            var distance = direction.Length();

            _hookshotPosition.Z = MapManager.ObjLink.EntityPosition.Z;

            if (!_comingBack)
            {
                // When Modern Camera is enabled, use the camera's current bounds to determine when object collides with screen's edge. 
                if (!Camera.ClassicMode && !MapManager.Camera.GetGameView().Contains(_hookshotPosition.Position))
                {
                    Repell();
                    ComeBack();
                    return;
                }
                // When Classic Camera is enabled, use current field to determine when object collides with screen's edge.
                if (Camera.ClassicMode && !MapManager.ObjLink.CurrentField.Contains(_hookshotPosition.Position))
                {
                    Repell();
                    ComeBack();
                    return;
                }
                // Make it come back if it goes too far.
                if (distance > 120)
                    ComeBack();
            }
            else
            {
                if (direction != Vector2.Zero)
                    direction.Normalize();

                _body.VelocityTarget = direction * Speed;

                if (distance < 2)
                    Despawn();
            }
            // Try to collect an item if it came in contact with one.
            CollectItem();

            // Do not hit anything on the return trip.
            if (!_comingBack)
            {
                // Check if the hookshot hit something.
                var collision = Map.Objects.Hit(this, _hookshotPosition.Position, _damageBox.Box, HitType.Hookshot, _damage, false, false);

                // If the hookshot made contact with something.
                if ((collision & (Values.HitCollision.Enemy | Values.HitCollision.Blocking | Values.HitCollision.Repelling | Values.HitCollision.RepellingParticle)) != 0)
                {
                    // If it hit something that repels or spawns a particle show the spark.
                    if ((collision & Values.HitCollision.RepellingParticle) != 0 && !_pokeParticleSpawned)
                        Repell();

                    // The hookshot starts coming back to the player.
                    ComeBack();
                }
            }
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // draw the chain
            var handPosition = MapManager.ObjLink.EntityPosition.ToVector3();
            handPosition.X += _startPositionOffset.X;
            handPosition.Y += _startPositionOffset.Y;
            var direction = _hookshotPosition.ToVector3() - handPosition;
            for (var i = 0; i < 3; i++)
            {
                var rawPosition = new Vector2(handPosition.X - 2, handPosition.Y - 2 - MapManager.ObjLink.EntityPosition.Z) +
                                  new Vector2(direction.X, direction.Y) * ((i + 0.75f) / 4f) +
                                  new Vector2(0, -direction.Z * ((i + 0.75f) / 4f));
                var chainPosition = GameSettings.PixelSnapping
                    ? new Vector2(MathF.Round(rawPosition.X), MathF.Round(rawPosition.Y))
                    : rawPosition;
                spriteBatch.Draw(_spriteChain.Texture, chainPosition, _spriteChain.SourceRectangle, Color.White * 0.50f);
            }
            // draw the hook
            var rawHookPosition = new Vector2(_hookshotPosition.X - 7, _hookshotPosition.Y - 7 - _hookshotPosition.Z);
            var hookPosition = GameSettings.PixelSnapping
                ? new Vector2(MathF.Round(rawHookPosition.X), MathF.Round(rawHookPosition.Y))
                : rawHookPosition;
            spriteBatch.Draw(_spriteHook.Texture, hookPosition, _spriteHook.SourceRectangle, Color.White);
        }

        public void Despawn()
        {
            IsMoving = false;

            if (Map != null)
                Map.Objects.DeleteObjects.Add(this);
        }

        private void CollectItem()
        {
            // I once experienced a strange crash where "Map" was null. Not sure how... so prevent that I guess.
            if (Map == null) { return; }

            if (_item != null && !_item.Collected)
                return;

            _item = null;
            _itemList.Clear();

            Map.Objects.GetComponentList(_itemList, (int)_damageBox.Box.X, (int)_damageBox.Box.Y, (int)_damageBox.Box.Width, (int)_damageBox.Box.Height, CollisionComponent.Mask);

            // check if an item was found
            foreach (var gameObject in _itemList)
            {
                var collidingBox = Box.Empty;
                var collisionObject = gameObject.Components[CollisionComponent.Index] as CollisionComponent;
                if ((collisionObject.CollisionType & Values.CollisionTypes.Item) != 0 && collisionObject.Collision(_damageBox.Box, 0, 0, ref collidingBox))
                {
                    // Hookshot comes in contact with item.
                    if (collisionObject.Owner is ObjItem newItem)
                    {
                        // The item is an instrument.
                        var isInstrument = newItem._itemName.Contains("instrument");
                        var isSwordLevel2 = newItem._itemName == "sword2";

                        // Item must be active, not collected, not flying, not an instrument, and not in the boomerang list.
                        if (newItem.IsActive && !newItem.Collected && !newItem._isFlying && !isInstrument && !isSwordLevel2)
                        {
                            _item = newItem;
                            _item.InitCollection();
                            ComeBack();
                        }
                    }
                    // Hookshot comes in contact with a fairy.
                    else if (collisionObject.Owner.GetType() == (typeof(ObjDungeonFairy)))
                    {
                        _fairy = collisionObject.Owner as ObjDungeonFairy;
                        _fairy.RemoveCooldown();
                        ComeBack();
                    }
                }
            }
        }

        private void UpdateItemPosition(CPosition position)
        {
            _item?.EntityPosition.Set(new Vector3(position.X, position.Y + 4, position.Z + 0.10f));
        }

        private void UpdateFairyPosition(CPosition position)
        {
            _fairy?.EntityPosition.Set(new Vector3(position.X, position.Y + 4, position.Z));
        }

        private void ComeBack()
        {
            _comingBack = true;
            _body.CollisionTypes = Values.CollisionTypes.None;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            var collidingBox = Box.Empty;
            var box = _body.BodyBox.Box;
            box.X += _direction.X;
            box.Y += _direction.Y;

            if (Map.Objects.Collision(box, Box.Empty, Values.CollisionTypes.Hookshot, 0, _body.Level, ref collidingBox))
            {
                // gets pulled towards the colliding box
                _body.VelocityTarget = Vector2.Zero;
                _pullingPlayer = true;
                MapManager.ObjLink.StartHookshotPull();
            }
            else
            {
                Repell();
                ComeBack();
            }
        }

        private void Repell()
        {
            _pokeParticleSpawned = true;

            // hookshot repels from the colliding box
            Game1.AudioManager.PlaySoundEffect("D360-07-07");
            var animation = new ObjSparkingEffect(Map, 0, 0, 0, 0);
            animation.EntityPosition.Set(_hookshotPosition.Position);
            Map.Objects.SpawnObject(animation);
        }

        public void ForceComeback()
        {
            ComeBack();
        }
    }
}
