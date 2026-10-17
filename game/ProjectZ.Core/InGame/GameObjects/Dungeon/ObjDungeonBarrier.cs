using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjDungeonBarrier : GameObject
    {
        private readonly DrawComponent _drawComponent;
        private readonly List<GameObject> _collidingObjects = new List<GameObject>();

        private readonly DictAtlasEntry _dictBarrier;
        private readonly DictAtlasEntry _dictBarrierBack;

        private readonly CBox _bodyBox;
        private ObjCollider _horseBoxHack;

        private readonly string _key;
        private readonly bool _negate;

        private const int StateTimer = 200;
        private float _stateCounter;
        private float _transitionPercentage;
        private float _transitionState;

        private bool _isUp;
        private ObjLink LinkHack;

        public ObjDungeonBarrier(Map.Map map, int posX, int posY, string strKey, bool negate, int type, bool horseBox) : base(map)
        {
            type = MathHelper.Clamp(type, 0, 3);

            _dictBarrier = Resources.GetSprite("barrier_" + type);
            _dictBarrierBack = Resources.GetSprite("barrier_bottom_" + type);

            SprEditorImage = _dictBarrier.Texture;
            EditorIconSource = _dictBarrier.ScaledRectangle;
            EditorIconScale = _dictBarrier.Scale;

            EntityPosition = new CPosition(posX + 2.5f, posY + 5, 0);
            EntitySize = new Rectangle(0, -5, 11, 14);

            _key = strKey;
            _negate = negate;

            // A stupid hack to prevent throwing horse heads over the colliders. 
            if (horseBox)
            {
                // Spawns in a collision box only the horse heads can not pass. Deactivated when the barrier is lowered.
                _horseBoxHack = new ObjCollider(map, posX + 3, posY + 6, false, 16, new Rectangle(0, 0, 8, 8), Values.CollisionTypes.Barrier, -1);
                map.Objects.SpawnObject(_horseBoxHack);
            }

            var collisionComponent = new BoxCollisionComponent(_bodyBox = new CBox(EntityPosition, 0, -1, 11, 10, 4), Values.CollisionTypes.Normal | Values.CollisionTypes.ThrowWeaponIgnore);

            AddComponent(CollisionComponent.Index, collisionComponent);
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawShadowComponent.Index, new DrawShadowComponent(DrawShadow));
            AddComponent(DrawComponent.Index, _drawComponent = new DrawComponent(Draw, Values.LayerBottom, EntityPosition));
            KeyChanged();

            if (_isUp)
                _stateCounter = StateTimer;
            
            // If the barriers are down then disable the horse box.
            if (_horseBoxHack != null && !_isUp)
                _horseBoxHack.IsActive = false;

            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void KeyChanged()
        {
            if (!string.IsNullOrWhiteSpace(_key))
            {
                _isUp = _negate != (Game1.GameManager.SaveManager.GetString(_key, "0") == "1");
                if (_isUp)
                    _drawComponent.Layer = Values.LayerPlayer;
            }
        }

        private void Update()
        {
            if (!_isUp && _stateCounter > 0)
            {
                _stateCounter -= Game1.DeltaTime;

                if (_stateCounter < 0)
                {
                    _stateCounter = 0;
                    _drawComponent.Layer = Values.LayerBottom;
                }
                if (_horseBoxHack != null)
                    _horseBoxHack.IsActive = false;
            }
            else if (_isUp && _stateCounter < StateTimer)
            {
                _stateCounter += Game1.DeltaTime;

                if (_stateCounter > StateTimer)
                    _stateCounter = StateTimer;

                if (_horseBoxHack != null)
                    _horseBoxHack.IsActive = true;
            }

            _transitionPercentage = MathF.Sin((_stateCounter / StateTimer) * MathF.PI - MathF.PI / 2) * 0.5f + 0.5f;
            _transitionState = _transitionPercentage * 4;

            if (EntityPosition.Z != _transitionState - 4)
            {
                var lastBox = _bodyBox.Box;

                EntityPosition.Z = _transitionState - 4;
                EntityPosition.NotifyListeners();

                // Check for game objects (enemy units, Link) that are currently standing over the barrier.
                _collidingObjects.Clear();
                Map.Objects.GetComponentList(
                    _collidingObjects, (int)EntityPosition.Position.X, (int)EntityPosition.Position.Y - 1, 11, 8, BodyComponent.Mask);

                foreach (var collidingObject in _collidingObjects)
                {
                    BodyComponent collisionBody = collidingObject.Components[BodyComponent.Index] as BodyComponent;
                    Box collisionBodyBox = collisionBody.BodyBox.Box;

                    // Lift the game object up with the barrier.
                    if (collisionBodyBox.Intersects(_bodyBox.Box))
                    {
                        if (!collisionBodyBox.Intersects(lastBox))
                        {
                            collisionBody.Position.Z = EntityPosition.Z + _bodyBox.Box.Depth;
                            collisionBody.Position.NotifyListeners();
                        }
                    }
                    // If Link is jumping when the transition starts, it will bug out his height so wait until he lands to fix his Z-Position.
                    if (collidingObject.GetType() == typeof(ObjLink) && LinkHack == null)
                    {
                        LinkHack = collidingObject as ObjLink;

                        // He must be both jumping and his starting jump height must be less than 4.00.
                        if (!LinkHack.IsJumpingState() && LinkHack._jumpStartZPos >= 4.00)
                            LinkHack = null;
                    }
                }
            }
            // If Link is still over the boxes then fix his Z-Position.
            if (LinkHack != null && LinkHack.Body.IsGrounded && 
                LinkHack.Body.BodyBox.Box.Intersects(_bodyBox.Box))
            {
                LinkHack.Body.Position.Z = 4;
            }
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // draw the bottom part
            DrawHelper.DrawNormalized(spriteBatch, _dictBarrierBack, new Vector2(EntityPosition.X, EntityPosition.Y - 1), Color.White);

            // draw the barrier
            if (_transitionState != 0)
            {
                var rectangle = _dictBarrier.ScaledRectangle;
                rectangle.Height = (int)((_dictBarrier.SourceRectangle.Height - 4 + _transitionState) / _dictBarrier.Scale);
                DrawHelper.DrawNormalized(spriteBatch, _dictBarrier.Texture,
                    new Vector2(EntityPosition.X, EntityPosition.Y - 1 - _transitionState), rectangle, Color.White);
            }
        }

        private void DrawShadow(SpriteBatch spriteBatch)
        {
            DrawHelper.DrawShadow(_dictBarrier.Texture, new Vector2(EntityPosition.X, EntityPosition.Y - 6),
                _dictBarrier.ScaledRectangle, _dictBarrier.SourceRectangle.Width, _dictBarrier.SourceRectangle.Height, false,
                Map.ShadowHeight, Map.ShadowRotation, Color.White * _transitionPercentage);
        }
    }
}
