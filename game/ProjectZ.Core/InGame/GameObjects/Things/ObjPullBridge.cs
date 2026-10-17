using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    class ObjPullBridge : GameObject
    {
        private readonly DictAtlasEntry _spriteStart;
        private readonly DictAtlasEntry _spriteMiddle;
        private readonly DictAtlasEntry _spriteEnd;
        private readonly DictAtlasEntry _spriteHook;
        private readonly DictAtlasEntry _spriteRope;
        private readonly DictAtlasEntry _spritePullBridge;

        private HittableComponent _hitComponent;
        private UpdateComponent _updateComponent;

        private Vector2 _startPosition;
        private List<GameObject> _holeList = new List<GameObject>();

        private readonly string _strKey;
        private float _state;

        private readonly int _min = 6;
        private readonly int _max = 72;

        private bool _startedPulling;
        private bool _finishedPulling;
        private bool _up;

        public ObjPullBridge(Map.Map map, int posX, int posY, string strKey, bool up) : base(map, "pull_bridge")
        {
            EntitySize = new Rectangle(0, up ? 0 : -64, 16, 80);
            EntityPosition = new CPosition(posX, posY, 0);
            ResetPosition  = new CPosition(posX, posY, 0);
            CanReset = true;
            OnReset = Reset;

            _strKey = strKey;
            _up = up;

            _spritePullBridge = Resources.GetSprite("pull_bridge");
            _spriteStart = Resources.GetSprite("pull_bridge_start");
            _spriteMiddle = Resources.GetSprite("pull_bridge_middle");
            _spriteEnd = Resources.GetSprite("pull_bridge_end");
            _spriteHook = Resources.GetSprite("pull_bridge_hook");
            _spriteRope = Resources.GetSprite("pull_bridge_rope");

            var _hittableBox = new CBox(posX + 4, posY + 2, 0, 8, 12, 8);

            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_hittableBox, OnHit));
            AddComponent(UpdateComponent.Index, _updateComponent = new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerBottom, EntityPosition));

            // was the bridge already pulled?
            if (!string.IsNullOrEmpty(_strKey) && Game1.GameManager.SaveManager.GetString(_strKey) == "1")
            {
                FinishedPull();
                _hitComponent.IsActive = false;
                _updateComponent.IsActive = false;
            }
        }

        public override void Reset()
        {
            // Reset all the various states of the pull bridge.
            _state = 0;
            _startedPulling = false;
            _finishedPulling = false;
            _hitComponent.IsActive = true;
            _updateComponent.IsActive = true;

            // Remove the key that was set so a respawn doesn't create it already pulled.
            Game1.GameManager.SaveManager.SetString(_strKey, "0");

            // Restore the holes if available.
            if (_holeList.Count > 0)
                foreach (var hole in _holeList) 
                    hole.IsActive = true;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType type, int damage, bool pieceOfPower)
        {
            var dir = AnimationHelper.GetDirection(direction);

            if (!_startedPulling && type == HitType.Hookshot)
            {
                // attacking from the wrong direction?
                if(!(_up && dir == 1 || !_up && dir == 3))
                    return Values.HitCollision.RepellingParticle;

                MapManager.ObjLink.Hookshot.HookshotPosition.AddPositionListener(typeof(ObjPullBridge), OnPositionChange);

                _startPosition = MapManager.ObjLink.Hookshot.HookshotPosition.Position;
                _startedPulling = true;

                return Values.HitCollision.Blocking;
            }

            return Values.HitCollision.None;
        }

        private void OnPositionChange(CPosition newPosition)
        {
            var distance = _startPosition - newPosition.Position;
            _state = Math.Clamp((distance.Length() + _min) / _max, 0, 1);

            if (_state >= 1)
                FinishedPull();
        }

        private void Update()
        {
            if (_startedPulling && !_finishedPulling && !MapManager.ObjLink.Hookshot.IsMoving)
                FinishedPull();
        }

        private void FinishedPull()
        {
            _state = 1;
            _finishedPulling = true;
            MapManager.ObjLink.Hookshot.HookshotPosition.PositionChangedDict.Remove(typeof(ObjPullBridge));
            RemoveHoles();

            // Disable components after the pull.
            _hitComponent.IsActive = false;
            _updateComponent.IsActive = false;

            // Set the key so it remains pulled.
            if (!string.IsNullOrEmpty(_strKey))
                Game1.GameManager.SaveManager.SetString(_strKey, "1");
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            var state = (int)Math.Floor((_state * _max) / 8);
            var rawStart = new Vector2(EntityPosition.Position.X + 8, EntityPosition.Position.Y + (_up ? 0 : 16));
            var startPosition = GameSettings.PixelSnapping
                ? new Vector2(MathF.Round(rawStart.X), MathF.Round(rawStart.Y))
                : rawStart;
            var position = startPosition;
            var dir = _up ? 1 : -1;

            if (_state * _max <= _min)
            {
                var p = GameSettings.PixelSnapping ? new Vector2(MathF.Round(position.X), MathF.Round(position.Y + dir * 16)) : new Vector2(position.X, position.Y + dir * 16);
                spriteBatch.Draw(_spritePullBridge.Texture, p, _spritePullBridge.ScaledRectangle, Color.White,
                    _up ? MathF.PI : 0, new Vector2(_spritePullBridge.ScaledRectangle.Width / 2f, 0), Vector2.One, SpriteEffects.None, 0);
                return;
            }

            if (state >= 9)
            {
                position.Y = startPosition.Y + (6 + _state * _max + 2) * dir;
                var p = GameSettings.PixelSnapping ? new Vector2(MathF.Round(position.X), MathF.Round(position.Y)) : position;
                spriteBatch.Draw(_spriteEnd.Texture, p, _spriteEnd.ScaledRectangle, Color.White,
                    _up ? MathF.PI : 0, new Vector2(_spriteEnd.ScaledRectangle.Width / 2f, 0), Vector2.One, SpriteEffects.None, 0);
            }
            else
            {
                position.Y = startPosition.Y + (6 + _state * _max - 4) * dir;
                var p = GameSettings.PixelSnapping ? new Vector2(MathF.Round(position.X), MathF.Round(position.Y)) : position;
                spriteBatch.Draw(_spriteRope.Texture, p, _spriteRope.ScaledRectangle, Color.White,
                    _up ? MathF.PI : 0, new Vector2(_spriteRope.ScaledRectangle.Width / 2f, 0), Vector2.One, SpriteEffects.None, 0);
            }

            position = new Vector2(startPosition.X, startPosition.Y + (state * 8 + 6) * dir);
            for (var y = 0; y < state; y++)
            {
                var p = GameSettings.PixelSnapping ? new Vector2(MathF.Round(position.X), MathF.Round(position.Y)) : position;
                spriteBatch.Draw(_spriteMiddle.Texture, p, _spriteMiddle.ScaledRectangle, Color.White,
                    _up ? MathF.PI : 0, new Vector2(_spriteMiddle.ScaledRectangle.Width / 2f, 0), Vector2.One, SpriteEffects.None, 0);
                position.Y -= 8 * dir;
            }

            var startP = GameSettings.PixelSnapping ? new Vector2(MathF.Round(position.X), MathF.Round(position.Y)) : position;
            spriteBatch.Draw(_spriteStart.Texture, startP, _spriteStart.ScaledRectangle, Color.White,
                _up ? MathF.PI : 0, new Vector2(_spriteStart.ScaledRectangle.Width / 2f, 0), Vector2.One, SpriteEffects.None, 0);

            // draw the hook
            if (state < 9)
            {
                position = startPosition;
                position.Y = startPosition.Y + (6 + _state * _max + 5) * dir;
                var p = GameSettings.PixelSnapping ? new Vector2(MathF.Round(position.X), MathF.Round(position.Y)) : position;
                spriteBatch.Draw(_spriteHook.Texture, p, _spriteHook.ScaledRectangle, Color.White,
                    _up ? MathF.PI : 0, new Vector2(_spriteHook.ScaledRectangle.Width / 2f, 0), Vector2.One, SpriteEffects.None, 0);
            }
        }

        private void RemoveHoles()
        {
            // Clear holes so we don't keep adding them over again.
            _holeList.Clear();

            // Find holes along the path.
            Map.Objects.GetGameObjectsWithTag(_holeList, Values.GameObjectTag.Hole,
                (int)EntityPosition.X + EntitySize.X, (int)EntityPosition.Y + EntitySize.Y, EntitySize.Width - 1, EntitySize.Height - 1);

            // Disable the holes that are found.
            foreach (var hole in _holeList) 
                hole.IsActive = false;
        }
    }
}
