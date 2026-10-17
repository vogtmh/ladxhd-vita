using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Pools;
using ProjectZ.InGame.GameObjects.Base.Systems;
using ProjectZ.InGame.GameObjects.NPCs;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Map
{
    public class ObjectManager
    {
        // The map the object manager is currently managing.
        public Map Owner;

        // The main lists of game objects. 
        public List<GameObjectItem> ObjectList = new List<GameObjectItem>();
        public List<GameObjectItem> ObjectListB = new List<GameObjectItem>();
        private List<GameObject> SpawnObjects = new List<GameObject>();
        public List<GameObject> DeleteObjects = new List<GameObject>();

        // The "always animate" lists of game objects.
        public List<GameObject> AlwaysAnimateObjectsMain { get; } = new();
        public List<GameObject> AlwaysAnimateObjectsTemp { get; } = new();

        private readonly object _alwaysAnimateLock = new object();

        // Shadow and effects texture.
        public Texture2D ShadowTexture;
        public static Effect CurrentEffect;

        // Component pools.
        private List<GameObject> _poolSpawnedObjects = new List<GameObject>();
        private ComponentPool _gameObjectPool;
        private ComponentDrawPool _drawPool;
        private ComponentDrawPool _drawPoolB;

        // Component update loops.
        private SystemBody _systemBody;
        private SystemSolids _systemSolids;
        private SystemAi _systemAi;
        private SystemAnimation _systemAnimator;

        // Listener component array.
        private List<KeyChangeListenerComponent.KeyChangeTemplate> _keyChangeListeners =
            new List<KeyChangeListenerComponent.KeyChangeTemplate>();

        // Temporary lists. Probably don't need all these, one list could do it all?
        private readonly List<GameObject> _updateGameObjects = new List<GameObject>();
        private readonly List<GameObject> _damageFieldObjects = new List<GameObject>();
        private readonly List<GameObject> _drawShadowObjects = new List<GameObject>();
        private readonly List<GameObject> _overlapObjects = new List<GameObject>();
        private readonly List<GameObject> _depthObjectList = new List<GameObject>();
        private readonly List<GameObject> _objectTypeList = new List<GameObject>();
        private readonly List<GameObject> _objectTagAllList = new List<GameObject>();
        private readonly List<GameObject> _collisionObjects = new List<GameObject>();
        private readonly List<GameObject> _collidingObjects = new List<GameObject>();
        private readonly List<GameObject> _lightObjectList = new List<GameObject>();
        private readonly List<GameObject> _carriableObjectList = new List<GameObject>();
        private readonly List<GameObject> _hittableObjectList = new List<GameObject>();
        private readonly List<GameObject> _pushableObjectList = new List<GameObject>();
        private readonly List<GameObject> _interactableObjectList = new List<GameObject>();
        private readonly List<GameObject> _scrollTileObjects = new List<GameObject>();

        // Use hash sets to prevent double adding to lists which is faster than "List.Contains()".
        private readonly HashSet<GameObject> _updateGameObjectsSet = new();
        private readonly HashSet<GameObject> _collidingObjectsSet = new();
        private readonly HashSet<GameObject> _damageFieldObjectsSet = new();
        private readonly HashSet<GameObject> _alwaysAnimateSet = new();

        // Debug lists. Only relevant for debugging.
        private readonly List<GameObject> db_damageList = new List<GameObject>();
        private readonly List<GameObject> db_bodyList = new List<GameObject>();
        private readonly List<GameObject> db_gameObjectList = new List<GameObject>();

        private List<GameObject> _freezePersistObjects = new List<GameObject>();

        private bool _keyChanged;
        private bool _finishedLoading;

        public static Rectangle UpdateField;
        public static Rectangle ActualField;

        public static int ViewportX;
        public static int ViewportY;
        public static int ViewportW;
        public static int ViewportH;

        private Rectangle _resetField;
        private bool _resetPending;
        private bool _holeResetPending;

        public ObjectManager(Map owner)
        {
            Owner = owner;

            // These are only component update loops so not sure why they specifically
            // have dedicated classes while other component update loops do not.
            _systemBody = new SystemBody(this);
            _systemSolids = new SystemSolids();
            _systemAi = new SystemAi(this);
            _systemAnimator = new SystemAnimation(this);
        }

        public List<GameObjectItem> GetMergedObjectLists()
        {
            // When game objects are needed, this combines both ObjectList and ObjectListB into a single list.
            var objectList = new List<GameObjectItem>{ };
            foreach (var obj in ObjectList)
                objectList.Add(obj);
            foreach (var obj in ObjectListB)
                objectList.Add(obj);
            return objectList;
        }

        public void LoadObjects()
        {
            // TODO_End tweak the size for best performance for the finished game
            // the size of the pools can be tweaked for faster update times
            _gameObjectPool = new ComponentPool(Owner, Owner.MapWidth, Owner.MapHeight, 32, 32);
            _drawPool = new ComponentDrawPool(Owner.MapWidth, Owner.MapHeight, 32, 32);
            _drawPoolB = new ComponentDrawPool(Owner.MapWidth, Owner.MapHeight, 32, 32);

            _systemAnimator.Pool = _gameObjectPool;
            _systemAi.Pool = _gameObjectPool;
            _systemBody.Pool = _gameObjectPool;
            _systemSolids.Pool = _gameObjectPool;

            ClearPools();

            // You would think it would be okay to just merge the lists and run a single loop
            // but NO it does NOT work and breaks the interaction between hole maps/rocks/flowers.
            foreach (var gameObj in ObjectList)
            {
                var gameObject = GetGameObject(Owner, gameObj.Index, gameObj.Parameter);
                AddObjectToMap(gameObject, false);
            }

            foreach (var gameObj in ObjectListB)
            {
                var gameObject = GetGameObject(Owner, gameObj.Index, gameObj.Parameter);
                AddObjectToMap(gameObject, true);
            }

            // done after calling the constructors before the init methode
            // stonespawner adds sprites that can be accessed in the init methode
            // we make sure to not call the init methodes to not call it twice
            AddSpawnedObjects(true);

            // okay so one problem are the dodongo snakes:
            // a chest should get spawned after killing two of them
            // but after reloading the chest should not be there
            // so the snakes will reset a key on reload
            // now the key condition setter sets the key
            // and the position dialog spawns the chest
            // so in the init method the key needs to be set correctly
            // for this to work the key condition setter will need to be updated after the snakes are created
            // for this we need to call the key listeners before calling the init method
            UpdateKeyListeners();

            foreach (var gameObject in _poolSpawnedObjects)
                gameObject.Init();

            // ensures key setters, key condition setter, etc are all set correctly after loading the objects
            UpdateKeyListeners();

            // done before and after init because ObjEnemyTrigger Init expects objects spawned in the constructor
            AddSpawnedObjects();

            _finishedLoading = true;
        }

        public void UpdateViewPort()
        {
            float scale = MapManager.Camera.Scale;
            ViewportX = (int)((MapManager.Camera.X - Game1.RenderWidth  / 2) / scale);
            ViewportY = (int)((MapManager.Camera.Y - Game1.RenderHeight / 2) / scale);
            ViewportW = (int)(Game1.RenderWidth  / scale);
            ViewportH = (int)(Game1.RenderHeight / scale);
        }

        public void Update(bool frozen)
        {
            // Link is referenced a lot so let's make the code easier to read.
            var Link = MapManager.ObjLink;

            // Freeze the world when a dialog is open.
            if (frozen)
            {
                UpdateFreezePersistObjects();
                UpdateKeyListeners();
                UpdateDeleteObjects();
                AddSpawnedObjects();
                return;
            }
            // Freezes the world around Link but continues to run his update loop. This is used to finish up some things
            // on Link before the rest of the world continues: like opening the Tail Key chest in the forest before the owl.
            if (Link.FreezeWorldAroundPlayer)
            {
                Link.FreezeWorldAroundPlayer = false;
                Link.Update();
                UpdateFreezePersistObjects();
                UpdateDeleteObjects();
                AddSpawnedObjects();
                return;
            }
            // Freeze most things except specific types when an event takes place. This variable should not be set directly,
            // but rather set via: Game1.GameManager.SaveManager.SetString("freezeGame", "1"); The listener on ObjLink will
            // detect this and set the "FreezeWorldForEvents" boolean accordingly. To unfreeze, just set it back to "0". It
            // can also be set with ObjLink function "FreezeAnimations(bool)", a shortcut to set "freezeGame" to 0 or 1.
            var worldFrozen = Link.FreezeWorldForEvents;

            // The "_system" classes need to be aware if the world is frozen and not change mid-frame so capture it now.
            _systemAnimator.GameWorldFrozen = worldFrozen;
            _systemAi.GameWorldFrozen = worldFrozen;
            _systemBody.GameWorldFrozen = worldFrozen;
            _systemSolids.GameWorldFrozen = worldFrozen;

            // HACK: Keep link playing the ocarina when everything else is frozen. This may be
            // useful for other states to keep Link active when the world should be frozen.
            if (worldFrozen && Link.CurrentState == ObjLink.State.Ocarina)
                Link.Animation.Update();

            // When classic camera is enabled, only objects within the current field are updated.
            if (Camera.ClassicMode)
            {
                // The "UpdateField" is the real size of the field. The "Actual" field has an additional tile in each direction.
                UpdateField = Owner.GetField(Link.CenterPosition);
                ActualField = new Rectangle(UpdateField.X - 16, UpdateField.Y - 16, UpdateField.Width + 32, UpdateField.Height + 32);

                // Reset the field that the player just left once scrolling has completely finished. This prevents the field from
                // visibly resetting while the transition happens which looks weird. 
                if (_resetPending && !MapManager.Camera.FieldScrollActive)
                {
                    _resetPending = false;
                    ResetSpawnPositions(_resetField);
                }
                // Reset any holes after the camera has completely shifted to the new field. 
                if (_holeResetPending && !MapManager.Camera.FieldScrollActive)
                {
                    _holeResetPending = false;
                    Owner.ResetHoleMap();
                }
                // If a field change has happened, then reset the enemies on the previous field.
                if (Link.FieldChange)
                {
                    // Do not reset enemies during a transition which causes a double reset.
                    if (!Link.PreventReset)
                    {
                        _resetField = Link.PreviousField;
                        _resetPending = true;
                    }
                    // Reset any dug holes once the camera has arrived.
                    _holeResetPending = true;
                }
            }
            // Optimization: Computer the viewport once per update loop. This was done almost 20 times per frame so just do it once.
            else
            {
                UpdateViewPort();
            }
            // Add the always animate objects from the main list to the temporary list here. The objects are copied to this 
            // list so it can serve as a "static" non-changing list that wont cause crashes due to it being updated mid-loop.
            if (AlwaysAnimateObjectsMain != null && AlwaysAnimateObjectsMain.Count != 0)
            {
                lock (_alwaysAnimateLock)
                {
                    try
                    {
                        AlwaysAnimateObjectsTemp.AddRange(AlwaysAnimateObjectsMain);
                    }
                    catch (ArgumentException ex)
                    {
                        Game1.AudioManager.PlaySoundEffect("D370-35-23");
                        System.Diagnostics.Debug.WriteLine($"AddRange failed: {ex.Message} (Count={AlwaysAnimateObjectsMain.Count})");
                        AlwaysAnimateObjectsTemp.AddRange(AlwaysAnimateObjectsMain.ToArray());
                    }
                    catch (Exception ex)
                    {
                        Game1.AudioManager.PlaySoundEffect("D378-55-37");
                        System.Diagnostics.Debug.WriteLine($"Unexpected exception in AddRange: {ex}");
                    }
                }
            }
            // Update everything: animations, listeners, bodies, etc. When "AlwaysAnimate" contains something, only those
            // types found that have been added to the type array will be updated. This is a method used to "freeze" the entire
            // game world when an event takes place. It also freezes Link so care must be taken when applying it.
            _systemAnimator.Update(false);
            UpdateGameObjects();
            _systemAi.Update();
            UpdateKeyListeners();
            _systemBody.Update();
            _systemSolids.Update();
            UpdatePlayerCollision();
            UpdateDamageFields();
            UpdateDeleteObjects();
            AddSpawnedObjects();

            // For some reason the list must be cleared here or "ghost" objects will remain that can still
            // deal damage. They also causes crashes stating that "Map is null" an issue I'll never grasp...
            AlwaysAnimateObjectsTemp.Clear();
        }

        public void RegisterAlwaysAnimateObject(GameObject obj)
        {
            lock (_alwaysAnimateLock)
                if (_alwaysAnimateSet.Add(obj))
                    AlwaysAnimateObjectsMain.Add(obj);
        }

        public void RegisterFreezePersistObject(GameObject obj)
        {
            if (!_freezePersistObjects.Contains(obj))
                _freezePersistObjects.Add(obj);
        }

        public void UpdateFreezePersistObjects()
        {
            for (int i = _freezePersistObjects.Count - 1; i >= 0; i--)
            {
                var gameObject = _freezePersistObjects[i];

                // Clean out anything that has been removed from the map.
                if (gameObject == null || gameObject.IsDead || gameObject.Map == null)
                {
                    _freezePersistObjects.RemoveAt(i);
                    continue;
                }
                if (!gameObject.IsActive)
                    continue;

                if (gameObject.Components[UpdateComponent.Index] is UpdateComponent updateComponent && updateComponent.IsActive)
                    updateComponent.UpdateFunction?.Invoke();
            }
        }

        public void UpdateAnimations()
        {
            _systemAnimator.Update(true);
        }

        private void AddSpawnedObjects(bool suppressInit = false)
        {
            // add newly spawned objects
            if (SpawnObjects.Count > 0)
            {
                // ObjChest is adding to the SpawnObjects list in it's init method
                for (var index = 0; index < SpawnObjects.Count; index++)
                {
                    if (!suppressInit)
                        SpawnObjects[index].Init();
                    AddObjectToMap(SpawnObjects[index], false);
                }
                SpawnObjects.Clear();
            }
        }

        private void ResetSpawnPositions(Rectangle field)
        {
            _updateGameObjects.Clear();

            // Get all objects on the previous field with a DrawComponent. Add 4 additional pixels to the
            // bottom of the field to get objects that potentially fall outside the range of the field.
            _gameObjectPool.GetComponentList(_updateGameObjects, field.X, field.Y, field.Width, field.Height + 4, DrawComponent.Mask);
            FilterObjectsInField(_updateGameObjects, new RectangleF(field.X, field.Y, field.Width, field.Height + 4));

            foreach (var gameObject in _updateGameObjects)
            {
                // Reset the enemy position and invoke their "OnReset" method if available.
                if (gameObject != null && gameObject.CanReset)
                {
                    gameObject.ResetToSpawn();
                    gameObject.OnReset?.Invoke();
                }
            }
        }

        public static void FilterObjectsInField(List<GameObject> gameObjectList, RectangleF actualField)
        {
            int writeIndex = 0;

            // Loop through the object list to find objects not in the current field.
            for (int readIndex = 0; readIndex < gameObjectList.Count; readIndex++)
            {
                // Get the current loop object and it's current entity position if it exists.
                var obj = gameObjectList[readIndex];
                var entityPos = obj?.EntityPosition;

                // Remove when entity position exists and object is outside current field.
                if (entityPos != null && !actualField.Contains(entityPos.Position))
                    continue;
  
                gameObjectList[writeIndex++] = obj;
            }
            if (writeIndex < gameObjectList.Count)
                gameObjectList.RemoveRange(writeIndex, gameObjectList.Count - writeIndex);
        }

        public static void FilterObjectsInFog(List<GameObject> gameObjectList)
        {
            var fog = Game1.GameManager?.MapDiscoveryFog;

            // The fog is not freezing anything right now, so leave the list alone.
            if (fog == null || !fog.FreezeActive)
                return;

            int writeIndex = 0;

            for (int readIndex = 0; readIndex < gameObjectList.Count; readIndex++)
            {
                var obj = gameObjectList[readIndex];
                var entityPos = obj?.EntityPosition;

                // Link is never frozen by the fog. He is the one revealing it.
                if (obj == MapManager.ObjLink)
                {
                    gameObjectList[writeIndex++] = obj;
                    continue;
                }
                // Objects without "EntityPosiion", are flagged "FreezePersist", or are an
                // animated tile are always animated/updated even when inside fog.
                if (entityPos != null && !obj.FreezePersist && !obj.AnimatedTile)
                {
                    var hidden = fog.IsHiddenAt(entityPos.X, entityPos.Y);

                    // The first time an object is seen in a revealed field, that field becomes its home.
                    if (obj.HomeFogNode.X == int.MinValue && !hidden)
                        obj.HomeFogNode = fog.NodeAt(entityPos.X, entityPos.Y);

                    // Only freeze objects that have never been in a revealed field.
                    if (hidden && obj.HomeFogNode.X == int.MinValue)
                        continue;
                }
                gameObjectList[writeIndex++] = obj;
            }
            if (writeIndex < gameObjectList.Count)
                gameObjectList.RemoveRange(writeIndex, gameObjectList.Count - writeIndex);
        }

        private void UpdateGameObjects()
        {
            // Clear the lists before rebuilding them.
            _updateGameObjects.Clear();
            _updateGameObjectsSet.Clear();

            // Classic Camera: Only update objects within the current field.
            if (Camera.ClassicMode && !Camera.LockCamera && GameSettings.ClassicBorder != 0 && (GameSettings.ClassicBorder != 1 || GameSettings.ClassicAlpha >= 1.0f))
            {
                _gameObjectPool.GetComponentList(_updateGameObjects, UpdateField.X, UpdateField.Y, UpdateField.Width, UpdateField.Height, UpdateComponent.Mask);
                FilterObjectsInField(_updateGameObjects, ActualField);
                _updateGameObjectsSet.UnionWith(_updateGameObjects);

                // Classic Scrolling: We want animated tiles to keep animating between scrolls. 
                if (MapManager.Camera.FieldScrolling)
                {
                    // The only place we need to worry about is the previous field. 
                    var prev = MapManager.ObjLink.PreviousField;
                    if (prev.Width > 0)
                    {
                        // Clear the scroll tile list and get all animated tiles from the previous field.
                        _scrollTileObjects.Clear();
                        _gameObjectPool.GetComponentList(_scrollTileObjects, prev.X - 16, prev.Y - 16, prev.Width + 32, prev.Height + 32, UpdateComponent.Mask);

                        // Add the tiles to the update list so they are animated between scrolling.
                        for (int i = 0; i < _scrollTileObjects.Count; i++)
                        {
                            var obj = _scrollTileObjects[i];
                            if (obj.AnimatedTile && !obj.IsDead && _updateGameObjectsSet.Add(obj))
                                _updateGameObjects.Add(obj);
                        }
                    }
                }
            }
            // Normal Camera: Update objects that are within the viewport.
            else
            {
                var updateFieldSize = new Vector2(Game1.RenderWidth, Game1.RenderHeight);
                _gameObjectPool.GetComponentList(_updateGameObjects,
                   (int)((MapManager.Camera.X - updateFieldSize.X / 2) / MapManager.Camera.Scale),
                   (int)((MapManager.Camera.Y - updateFieldSize.Y / 2) / MapManager.Camera.Scale),
                   (int)(updateFieldSize.X / MapManager.Camera.Scale),
                   (int)(updateFieldSize.Y / MapManager.Camera.Scale), UpdateComponent.Mask);
                ObjectManager.FilterObjectsInFog(_updateGameObjects);
                _updateGameObjectsSet.UnionWith(_updateGameObjects);
            }

            // Always update certain objects that are flagged as "always animate".
            for (int i = 0; i < AlwaysAnimateObjectsTemp.Count; i++)
            {
                var gameObject = AlwaysAnimateObjectsTemp[i];
                if (gameObject != null && !gameObject.IsDead && _updateGameObjectsSet.Add(gameObject))
                    _updateGameObjects.Add(gameObject);
            }
            // Update all game object update components in the list.
            for (int i = 0; i < _updateGameObjects.Count; i++)
            {
                var gameObject = _updateGameObjects[i];
                if (!gameObject.IsActive)
                    continue;

                if (gameObject.Components[UpdateComponent.Index] is UpdateComponent updateComponent && updateComponent.IsActive)
                    updateComponent.UpdateFunction?.Invoke();
            }
        }

        private void UpdateKeyListeners()
        {
            // notify all key listeners
            // repeat the process so key changes that depend on different key changes get processed in a single frame
            // max is used to avoid an infinite loop in case of a bug
            var max = 5;
            while (_keyChanged && max > 0)
            {
                max--;
                _keyChanged = false;

                // notify key listeners if a key value was changed
                foreach (var listener in _keyChangeListeners)
                    listener();
            }
        }

        private void UpdatePlayerCollision()
        {
            // Link is referenced quite a bit.
            var Link = MapManager.ObjLink;

            // Clear the lists before rebuilding them.
            _collidingObjects.Clear();
            _collidingObjectsSet.Clear();

            // Update objects that are within the player's body rectangle.
            _gameObjectPool.GetComponentList(_collidingObjects, (int)Link.BodyRectangle.X, (int)Link.BodyRectangle.Y,
                (int)Link.BodyRectangle.Width, (int)Link.BodyRectangle.Height, ObjectCollisionComponent.Mask);
            _collidingObjectsSet.UnionWith(_collidingObjects);

            // Always include certain objects that are flagged as "always animate".
            for (int i = 0; i < AlwaysAnimateObjectsTemp.Count; i++)
            {
                var gameObject = AlwaysAnimateObjectsTemp[i];
                if (gameObject != null && !gameObject.IsDead && _collidingObjectsSet.Add(gameObject))
                    _collidingObjects.Add(gameObject);
            }
            // Update all game object collision components in the list.
            for (int i = 0; i < _collidingObjects.Count; i++)
            {
                var gameObject = _collidingObjects[i];

                if (!gameObject.IsActive) { continue; }

                if (gameObject.Components[ObjectCollisionComponent.Index] is ObjectCollisionComponent component)
                {
                    bool collides = component.TriggerOnCollision
                        ? component.CollisionRectangle.Rectangle.Intersects(Link.BodyRectangle)
                        : component.CollisionRectangle.Rectangle.Contains(Link.BodyRectangle);
                    if (collides) { component.OnCollision(Link); }
                }
            }
        }

        private void UpdateDamageFields()
        {
            // Get link's damage box.
            var LinkHitBox = MapManager.ObjLink.DamageCollider.Box;

            // Clear the lists before rebuilding them.
            _damageFieldObjects.Clear();
            _damageFieldObjectsSet.Clear();

            // Classic Camera: Only update objects that collide with Link.
            _gameObjectPool.GetComponentList(_damageFieldObjects, (int)LinkHitBox.X, (int)LinkHitBox.Y, (int)LinkHitBox.Width,(int)LinkHitBox.Height, DamageFieldComponent.Mask);
            _damageFieldObjectsSet.UnionWith(_damageFieldObjects);

            // Always include certain objects that are flagged as "always animate".
            for (int i = 0; i < AlwaysAnimateObjectsTemp.Count; i++)
            {
                var gameObject = AlwaysAnimateObjectsTemp[i];
                if (gameObject != null && !gameObject.IsDead && _damageFieldObjectsSet.Add(gameObject))
                    _damageFieldObjects.Add(gameObject);
            }
            // Update all game object damage field components in the list.
            for (int i = 0; i < _damageFieldObjects.Count; i++)
            {
                var gameObject = _damageFieldObjects[i];

                if (!gameObject.IsActive) { continue; }

                if (gameObject.Components[DamageFieldComponent.Index] is DamageFieldComponent damageField)
                    if (damageField.IsActive && damageField.CollisionBox.Box.Intersects(LinkHitBox))
                        damageField.OnDamage?.Invoke();
            }
        }

        private void UpdateDeleteObjects()
        {
            // Exit early if no delete objects.
            if (DeleteObjects.Count == 0)
                return;

            // Remove the object from the always animate list.
            lock (_alwaysAnimateLock)
            {
                foreach (var gameObject in DeleteObjects)
                {
                    _alwaysAnimateSet.Remove(gameObject);
                    AlwaysAnimateObjectsMain.Remove(gameObject);
                    _freezePersistObjects.Remove(gameObject);
                }
            }

            // Remove the object from the game.
            foreach (var gameObject in DeleteObjects)
                RemoveObject(gameObject);

            DeleteObjects.Clear();
        }

        public static void SpriteBatchBegin(SpriteBatch spriteBatch, SpriteShader spriteShader)
        {
            SetSpriteShader(spriteShader);

            CurrentEffect = spriteShader?.Effect;
            spriteBatch.Begin(SpriteSortMode.Deferred, null,
                MapManager.Camera.Scale >= 1 ? SamplerState.PointClamp : SamplerState.AnisotropicWrap,
                null, null, CurrentEffect, MapManager.Camera.TransformMatrix);
        }

        public static void SetSpriteShader(SpriteShader spriteShader)
        {
            if (spriteShader == null)
                return;

            foreach (var parameter in spriteShader.FloatParameter)
                spriteShader.Effect.Parameters[parameter.Key]?.SetValue(parameter.Value);

            foreach (var parameter in spriteShader.Vector4Parameter)
                spriteShader.Effect.Parameters[parameter.Key]?.SetValue(parameter.Value);
        }

        public static void SpriteBatchBeginAnisotropic(SpriteBatch spriteBatch, Effect effect)
        {
            CurrentEffect = effect;
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.AnisotropicClamp,
                null, null, effect, MapManager.Camera.TransformMatrix);
        }

        public void DrawBottom(SpriteBatch spriteBatch)
        {
            if (!_finishedLoading)
                return;

            SpriteBatchBegin(spriteBatch, null);

            _drawPool.DrawPool(spriteBatch, ViewportX, ViewportY, ViewportW, ViewportH, 0, 1);
            spriteBatch.End();

            SpriteBatchBegin(spriteBatch, null);

            _drawPoolB.DrawPool(spriteBatch, ViewportX, ViewportY, ViewportW, ViewportH, 0, 1);
            spriteBatch.End();
        }

        public void DrawMiddle(SpriteBatch spriteBatch)
        {
            if (!_finishedLoading)
                return;

            // This is a really silly way to do this, but I can't think of how else it could be done. The HoleMap and the game objects
            // conflict in that, some game objects (pushable stones) should be ABOVE the holes, while objects like flower patches should
            // be BELOW the holes. So to solve this, "ObjectList" and "_drawpool" were split into two groups, the alternate "B" group
            // holds the pushable stone. This way we can draw game objects >> draw hole map >> draw stones to get the correct order.

            SpriteBatchBegin(spriteBatch, null);
            _drawPool.DrawPool(spriteBatch, ViewportX, ViewportY, ViewportW, ViewportH, 1, 2);
            spriteBatch.End();

            Owner.HoleMap.Draw(spriteBatch);
            
            SpriteBatchBegin(spriteBatch, null);
            _drawPoolB.DrawPool(spriteBatch, ViewportX, ViewportY, ViewportW, ViewportH, 1, 2);
            spriteBatch.End();

            if (GameSettings.EnableShadows && Owner.UseShadows && !Game1.GameManager.UseShockEffect && ShadowTexture != null)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.AnisotropicClamp);//, null, null, null, Game1.GameManager.GetMatrix);
                spriteBatch.Draw(ShadowTexture, new Rectangle(0, 0, Game1.GameManager.CurrentRenderWidth, Game1.GameManager.CurrentRenderHeight), Color.Black * 0.55f);
                spriteBatch.End();
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_finishedLoading)
                return;

            spriteBatch.Begin(SpriteSortMode.Deferred, null,
                MapManager.Camera.Scale >= 1 ? SamplerState.PointClamp : SamplerState.AnisotropicWrap,
                null, null, null, MapManager.Camera.TransformMatrix);
            _drawPool.DrawPool(spriteBatch, ViewportX, ViewportY, ViewportW, ViewportH, 2, 4);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, null,
                MapManager.Camera.Scale >= 1 ? SamplerState.PointClamp : SamplerState.AnisotropicWrap,
                null, null, null, MapManager.Camera.TransformMatrix);
            _drawPoolB.DrawPool(spriteBatch, ViewportX, ViewportY, ViewportW, ViewportH, 2, 4);
            spriteBatch.End();

            // draw the body colliders
            if (Game1.DebugMode)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, null,
                    MapManager.Camera.Scale >= 1 ? SamplerState.PointClamp : SamplerState.AnisotropicWrap,
                    null, null, null, MapManager.Camera.TransformMatrix);

                // draw entity size rectangle
                if (Game1.DebugBoxMode == 0)
                {
                    db_gameObjectList.Clear();
                    _gameObjectPool.GetObjectList(db_gameObjectList, ViewportX, ViewportY, ViewportW, ViewportH);

                    foreach (var gameObject in db_gameObjectList)
                    {
                        if (gameObject.EntityPosition != null && !(gameObject is ObjLamp))
                        {
                            var rectangle = new RectangleF(
                                gameObject.EntityPosition.X + gameObject.EntitySize.X,
                                gameObject.EntityPosition.Y + gameObject.EntitySize.Y,
                                gameObject.EntitySize.Width, gameObject.EntitySize.Height);

                            DrawRectangle(spriteBatch, rectangle, Color.LightBlue);
                        }
                    }
                }

                // draw the damage fields
                if (Game1.DebugBoxMode == 0 || Game1.DebugBoxMode == 2)
                {
                    db_damageList.Clear();
                    _gameObjectPool.GetComponentList(db_damageList, ViewportX, ViewportY, ViewportW, ViewportH, DamageFieldComponent.Mask);
                    foreach (var drawTile in db_damageList)
                    {
                        var damageComponent = drawTile.Components[DamageFieldComponent.Index] as DamageFieldComponent;
                        DrawRectangle(spriteBatch, damageComponent.CollisionBox.Box.Rectangle(), Color.Green);
                    }
                }

                // draw the hittable fields
                if (Game1.DebugBoxMode == 0 || Game1.DebugBoxMode == 3)
                {
                    db_damageList.Clear();
                    _gameObjectPool.GetComponentList(db_damageList, ViewportX, ViewportY, ViewportW, ViewportH, HittableComponent.Mask);
                    foreach (var drawTile in db_damageList)
                    {
                        var hittableComponent = drawTile.Components[HittableComponent.Index] as HittableComponent;
                        DrawRectangle(spriteBatch, hittableComponent.HittableBox.Box.Rectangle(), Color.Yellow);
                    }
                }

                // draw the push fields
                if (Game1.DebugBoxMode == 0 || Game1.DebugBoxMode == 4)
                {
                    db_damageList.Clear();
                    _gameObjectPool.GetComponentList(db_damageList, ViewportX, ViewportY, ViewportW, ViewportH, PushableComponent.Mask);
                    foreach (var drawTile in db_damageList)
                    {
                        var pushableComponent = drawTile.Components[PushableComponent.Index] as PushableComponent;
                        DrawRectangle(spriteBatch, pushableComponent.PushableBox.Box.Rectangle(), Color.Orange);
                    }
                }

                // draw the bodies
                if (Game1.DebugBoxMode == 0 || Game1.DebugBoxMode == 1)
                {
                    db_bodyList.Clear();
                    _gameObjectPool.GetComponentList(db_bodyList, ViewportX, ViewportY, ViewportW, ViewportH, BodyComponent.Mask);
                    foreach (var drawTile in db_bodyList)
                    {
                        var body = drawTile.Components[BodyComponent.Index] as BodyComponent;
                        DrawRectangle(spriteBatch, body.BodyBox.Box.Rectangle(), Color.Red);
                    }
                }

                // draw the interact rectangle
                if (Game1.DebugBoxMode == 0 || Game1.DebugBoxMode == 5)
                {
                    db_damageList.Clear();
                    _gameObjectPool.GetComponentList(db_damageList, ViewportX, ViewportY, ViewportW, ViewportH, InteractComponent.Mask);
                    foreach (var drawTile in db_damageList)
                    {
                        var pushableComponent = drawTile.Components[InteractComponent.Index] as InteractComponent;
                        DrawRectangle(spriteBatch, pushableComponent.BoxInteractable.Box.Rectangle(), Color.Aqua);
                    }
                }
                spriteBatch.End();
            }
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, null, null, MapManager.Camera.TransformMatrix);
        }

        public void DrawRectangle(SpriteBatch spriteBatch, RectangleF rectangle, Color color)
        {
            spriteBatch.Draw(Resources.SprWhite,
                new Vector2(rectangle.X, rectangle.Y),
                new Rectangle(0, 0, (int)rectangle.Width, (int)rectangle.Height), color * 0.25f);

            var thickness = 1 / (float)Game1.UiScale;
            spriteBatch.Draw(Resources.SprWhite,
                new Vector2(rectangle.X, rectangle.Y),
                new Rectangle(0, 0, 1, (int)(rectangle.Height * Game1.UiScale)),
                color * 0.75f, 0, Vector2.Zero, thickness, SpriteEffects.None, 0);
            spriteBatch.Draw(Resources.SprWhite,
                new Vector2(rectangle.X + rectangle.Width - thickness, rectangle.Y),
                new Rectangle(0, 0, 1, (int)(rectangle.Height * Game1.UiScale)),
                color * 0.75f, 0, Vector2.Zero, thickness, SpriteEffects.None, 0);
            spriteBatch.Draw(Resources.SprWhite,
                new Vector2(rectangle.X, rectangle.Y),
                new Rectangle(0, 0, (int)(rectangle.Width * Game1.UiScale), 1),
                color * 0.75f, 0, Vector2.Zero, thickness, SpriteEffects.None, 0);
            spriteBatch.Draw(Resources.SprWhite,
                new Vector2(rectangle.X, rectangle.Y + rectangle.Height - thickness),
                new Rectangle(0, 0, (int)(rectangle.Width * Game1.UiScale), 1),
                color * 0.75f, 0, Vector2.Zero, thickness, SpriteEffects.None, 0);
        }

        public void DrawShadow(SpriteBatch spriteBatch)
        {
            DrawHelper.StartShadowDrawing();

            _drawShadowObjects.Clear();
            _gameObjectPool.GetComponentList(_drawShadowObjects,
                (int)((MapManager.Camera.X - Game1.RenderWidth / 2) / MapManager.Camera.Scale),
                (int)((MapManager.Camera.Y - Game1.RenderHeight / 2) / MapManager.Camera.Scale),
                (int)(Game1.RenderWidth / MapManager.Camera.Scale),
                (int)(Game1.RenderHeight / MapManager.Camera.Scale), DrawShadowComponent.Mask);

            foreach (var gameObject in _drawShadowObjects)
            {
                if (!gameObject.IsActive)
                    continue;

                if (gameObject.Components[DrawShadowComponent.Index] is not DrawShadowComponent shadowComponent)
                    continue;

                shadowComponent.Draw(spriteBatch);
            }

            DrawHelper.EndShadowDrawing();
        }

        public void DrawLight(SpriteBatch spriteBatch)
        {
            _lightObjectList.Clear();
            _gameObjectPool.GetComponentList(_lightObjectList,
                (int)((MapManager.Camera.X - Game1.RenderWidth / 2) / MapManager.Camera.Scale),
                (int)((MapManager.Camera.Y - Game1.RenderHeight / 2) / MapManager.Camera.Scale),
                (int)(Game1.RenderWidth / MapManager.Camera.Scale),
                (int)(Game1.RenderHeight / MapManager.Camera.Scale), LightDrawComponent.Mask);

            _lightObjectList.Sort((obj0, obj1) =>
            {
                var light0 = (LightDrawComponent)obj0.Components[LightDrawComponent.Index];
                var light1 = (LightDrawComponent)obj1.Components[LightDrawComponent.Index];

                if (light0 != null && light1 != null)
                {
                    if (light0.Layer == light1.Layer &&
                        light0.Owner.EntityPosition != null && light1.Owner.EntityPosition != null)
                        return (int)light0.Owner.EntityPosition.Y - (int)light1.Owner.EntityPosition.Y;

                    return light0.Layer - light1.Layer;
                }
                return 0;
            });
            for (var i = 0; i < _lightObjectList.Count; i++)
            {
                if (_lightObjectList[i].IsActive)
                    (_lightObjectList[i].Components[LightDrawComponent.Index] as LightDrawComponent)?.Draw(spriteBatch);
            }
        }

        public void DrawBlur(SpriteBatch spriteBatch)
        {
            // draw the shadows
            _drawShadowObjects.Clear();
            _gameObjectPool.GetComponentList(_drawShadowObjects,
                (int)((MapManager.Camera.X - Game1.RenderWidth / 2) / MapManager.Camera.Scale),
                (int)((MapManager.Camera.Y - Game1.RenderHeight / 2) / MapManager.Camera.Scale),
                (int)(Game1.RenderWidth / MapManager.Camera.Scale),
                (int)(Game1.RenderHeight / MapManager.Camera.Scale), BlurDrawComponent.Mask);

            foreach (var gameObject in _drawShadowObjects)
                if (gameObject.IsActive)
                    (gameObject.Components[BlurDrawComponent.Index] as BlurDrawComponent)?.Draw(spriteBatch);
        }

        public void Clear()
        {
           lock (_alwaysAnimateLock)
                _alwaysAnimateSet.Clear();

            AlwaysAnimateObjectsMain.Clear();
            AlwaysAnimateObjectsTemp.Clear();
            ObjectList.Clear();
            ObjectListB.Clear();
        }

        private void ClearPools()
        {
            _keyChangeListeners.Clear();
            _poolSpawnedObjects.Clear();
        }

        private void AddObjectToMap(GameObject gameObject, bool isAltObject)
        {
            if (gameObject == null || gameObject.IsDead)
                return;

            _poolSpawnedObjects.Add(gameObject);

            // order is important because the draw pool does not update the last position value
            // add the object to the drawable pool
            if ((gameObject.ComponentsMask & DrawComponent.Mask) == DrawComponent.Mask)
            {
                if (isAltObject)
                    _drawPoolB.AddEntity(gameObject);
                else
                    _drawPool.AddEntity(gameObject);
            }
            // add the object to the pool
            _gameObjectPool.AddEntity(gameObject);

            // add key listeners
            if ((gameObject.ComponentsMask & KeyChangeListenerComponent.Mask) == KeyChangeListenerComponent.Mask)
            {
                var listener = (gameObject.Components[KeyChangeListenerComponent.Index] as KeyChangeListenerComponent).KeyChangeFunction;
                _keyChangeListeners.Add(listener);
            }
        }

        public void RemoveObject(GameObject gameObject)
        {
            gameObject.Map = null;

            _poolSpawnedObjects.Remove(gameObject);

            // Remove the object from the draw pool.
            if ((gameObject.ComponentsMask & DrawComponent.Mask) == DrawComponent.Mask)
            {
                _drawPool.RemoveEntity(gameObject);
                _drawPoolB.RemoveEntity(gameObject);
            }
            // Remove the object from the pool.
            _gameObjectPool.RemoveEntity(gameObject);

            // Remove key listeners.
            if ((gameObject.ComponentsMask & KeyChangeListenerComponent.Mask) == KeyChangeListenerComponent.Mask)
            {
                var listener = (gameObject.Components[KeyChangeListenerComponent.Index] as KeyChangeListenerComponent).KeyChangeFunction;
                _keyChangeListeners.Remove(listener);
            }
        }

        public void ReloadObjects()
        {
            LoadObjects();
        }

        public void TriggerKeyChange()
        {
            _keyChanged = true;
        }

        public bool SpawnObject(GameObject newObject)
        {
            if (newObject == null || newObject.IsDead)
                return false;

            SpawnObjects.Add(newObject);
            return true;
        }

        public bool SpawnObject(string objectId, object[] objectParameter)
        {
            if (objectId == null || !GameObjectTemplates.ObjectTemplates.ContainsKey(objectId))
                return false;

            return SpawnObject(GetGameObject(Owner, objectId, objectParameter));
        }

        public static GameObject GetGameObject(Map owner, string objectId, object[] objectParameter)
        {
            if (!GameObjectTemplates.ObjectSpawner.ContainsKey(objectId))
                return null;

            if (objectParameter == null)
            {
                objectParameter = MapData.GetParameter(objectId, null);
                objectParameter[1] = 0;
                objectParameter[2] = 0;
            }

            objectParameter[0] = owner;
            var constructor = GameObjectTemplates.ObjectSpawner[objectId];

            return constructor(objectParameter);
        }

        // TODO_End: be careful to not use this often
        public List<GameObject> GetObjectsOfType(Type type)
        {
            _objectTypeList.Clear();

            for (var i = 0; i < _poolSpawnedObjects.Count; i++)
                if (_poolSpawnedObjects[i].GetType() == type)
                    _objectTypeList.Add(_poolSpawnedObjects[i]);

            return _objectTypeList;
        }

        public List<GameObject> GetObjects(int left, int top, int width, int height)
        {
            // get possible candidates
            _objectTagAllList.Clear();
            _gameObjectPool.GetObjectList(_objectTagAllList, left, top, width, height);

            var outputList = new List<GameObject>();
            foreach (var gameObject in _objectTagAllList)
            {
                if (gameObject.EntityPosition != null &&
                    left <= gameObject.EntityPosition.X + gameObject.EntitySize.X &&
                    gameObject.EntityPosition.X + gameObject.EntitySize.X + gameObject.EntitySize.Width <= left + width &&
                    top <= gameObject.EntityPosition.Y + gameObject.EntitySize.Y &&
                    gameObject.EntityPosition.Y + gameObject.EntitySize.Y + gameObject.EntitySize.Height <= top + height)
                    outputList.Add(gameObject);
            }
            return outputList;
        }

        public void GetObjectsOfType(List<GameObject> outputList, Type type, int left, int top, int width, int height)
        {
            // get possible candidates
            _objectTagAllList.Clear();
            _gameObjectPool.GetObjectList(_objectTagAllList, left, top, width, height);

            foreach (var gameObject in _objectTagAllList)
            {
                if (gameObject.GetType() == type &&
                    left <= gameObject.EntityPosition.X + gameObject.EntitySize.X + gameObject.EntitySize.Width &&
                    gameObject.EntityPosition.X + gameObject.EntitySize.X <= left + width &&
                    top <= gameObject.EntityPosition.Y + gameObject.EntitySize.Y + gameObject.EntitySize.Height &&
                    gameObject.EntityPosition.Y + gameObject.EntitySize.Y <= top + height)
                    outputList.Add(gameObject);
            }
        }

        public GameObject GetObjectOfType(int left, int top, int width, int height, Type type)
        {
            return _gameObjectPool.GetObjectOfType(left, top, width, height, type);
        }

        public void GetGameObjectsWithTag(List<GameObject> outputList, Values.GameObjectTag tag, int left, int top, int width, int height)
        {
            // get possible candidates
            _objectTagAllList.Clear();
            _gameObjectPool.GetObjectList(_objectTagAllList, left, top, width, height);

            outputList.Clear();

            foreach (var gameObject in _objectTagAllList)
            {
                if ((gameObject.Tags & tag) != 0 &&
                   left <= gameObject.EntityPosition.X && gameObject.EntityPosition.X <= left + width &&
                   top <= gameObject.EntityPosition.Y && gameObject.EntityPosition.Y <= top + height)
                    outputList.Add(gameObject);
            }
        }

        public void GetComponentList(List<GameObject> gameObjectList, int recLeft, int recTop, int recWidth, int recHeight, int componentMask)
        {
            _gameObjectPool.GetComponentList(gameObjectList, recLeft, recTop, recWidth, recHeight, componentMask);
        }

        public float GetCollisionOverlapArea(Box box, Values.CollisionTypes collisionTypes, Values.CollisionTypes ignoreTypes, int dir, int level)
        {
            _overlapObjects.Clear();
            _gameObjectPool.GetComponentList(_overlapObjects, (int)box.X, (int)box.Y, (int)box.Width, (int)box.Height, CollisionComponent.Mask);

            var bodyRec = box.Rectangle();
            var bodyArea = bodyRec.Width * bodyRec.Height;
            if (bodyArea <= 0)
                return 0;

            float overlapArea = 0;

            for (int i = 0; i < _overlapObjects.Count; i++)
            {
                var gameObject = _overlapObjects[i];

                if (!gameObject.IsActive)
                    continue;

                var collisionObject = gameObject.Components[CollisionComponent.Index] as CollisionComponent;
                if ((collisionObject.CollisionType & collisionTypes) == 0 ||
                    (collisionObject.CollisionType & ignoreTypes) != 0)
                    continue;

                var collidingBox = Box.Empty;
                if (!collisionObject.Collision(box, dir, level, ref collidingBox))
                    continue;

                var intersection = collidingBox.Rectangle().GetIntersection(bodyRec);
                if (intersection.Width > 0 && intersection.Height > 0)
                    overlapArea += intersection.Width * intersection.Height;
            }

            // Overlapping colliders can double count, so never report more than the body itself.
            return Math.Min(overlapArea, bodyArea);
        }

        public bool Collision(Box box, Box oldBox, Values.CollisionTypes collisionTypes, Values.CollisionTypes ignoreTypes, int dir, int level, ref Box collidingBox)
        {
            // Get objects that are found within the collision box.
            _collisionObjects.Clear();
            _gameObjectPool.GetComponentList(_collisionObjects, (int)box.X, (int)box.Y, (int)box.Width, (int)box.Height, CollisionComponent.Mask);

            // Check each object to see if it is colliding.
            for (int i = 0; i < _collisionObjects.Count; i++)
            {
                var gameObject = _collisionObjects[i];

                if (!gameObject.IsActive)
                    continue;

                var collisionObject = gameObject.Components[CollisionComponent.Index] as CollisionComponent;
                if ((collisionObject.CollisionType & collisionTypes) != 0 &&
                    (collisionObject.CollisionType & ignoreTypes) == 0 &&
                     collisionObject.Collision(box, dir, level, ref collidingBox) &&
                    (oldBox == Box.Empty || !collisionObject.Collision(oldBox, dir, level, ref collidingBox)))
                    return true;
            }
            return false;
        }

        public bool Collision(Box box, Box oldBox, Values.CollisionTypes collisionTypes, int dir, int level, ref Box collidingBox)
        {
            return Collision(box, oldBox, collisionTypes, Values.CollisionTypes.None, dir, level, ref collidingBox);
        }

        public float GetDepth(Box box, Values.CollisionTypes collisionType, float maxDepth)
        {
            float outDepth = 0;

            // depth of holes
            var center = new Vector2((box.X + box.Width / 2) / Values.TileSize, (box.Front - 1) / Values.TileSize);
            if (Owner.HoleMap != null && Owner.HoleMap.ArrayTileMap != null &&
                0 <= center.X && center.X < Owner.HoleMap.ArrayTileMap.GetLength(0) &&
                0 <= center.Y && center.Y < Owner.HoleMap.ArrayTileMap.GetLength(1))
            {
                var distance = new Vector2((int)center.X + 0.5f, (int)center.Y + 0.5f) - center;
                outDepth = Owner.HoleMap.ArrayTileMap[(int)center.X, (int)center.Y, 0] < 0 ? 0 : -2 * Math.Clamp(1 - distance.Length() * 1.5f, 0, 1);
            }

            _depthObjectList.Clear();
            _gameObjectPool.GetComponentList(_depthObjectList, (int)box.X, (int)box.Y, (int)box.Width, (int)box.Height, CollisionComponent.Mask);

            var clampDepth = outDepth;

            foreach (var gameObject in _depthObjectList)
            {
                if (!gameObject.IsActive)
                    continue;

                var newDepth = outDepth;
                var lowestDepth = GetObjectDepth(gameObject, box, collisionType, ref newDepth);

                // floating point inaccuracy can lead to combined intersection areas greater than 100%
                // to fix this problem the maxDepth value will need to be clamped to not exceed the max depth
                if (lowestDepth < clampDepth)
                    clampDepth = lowestDepth;

                if (newDepth <= maxDepth)
                    outDepth = newDepth;
            }
            return MathF.Max(outDepth, clampDepth);
        }

        private float GetObjectDepth(GameObject gameObject, Box box, Values.CollisionTypes collisionType, ref float maxDepth)
        {
            var collisionObject = gameObject.Components[CollisionComponent.Index] as CollisionComponent;
            var collidingBox = Box.Empty;

            // add the object to the list if it is in the mask and is colliding with the provided rectangle
            if ((collisionObject.CollisionType & collisionType) != 0 &&
                collisionObject.Collision(box, 0, 0, ref collidingBox))
            {
                var bottom = collidingBox.Z + collidingBox.Depth;
                if (bottom < 0)
                {
                    // combine the depth values lower than 0 to smoothly transition to lower floors
                    var bodyRec = box.Rectangle();
                    var intersection = collidingBox.Rectangle().GetIntersection(bodyRec);
                    maxDepth += bottom * ((intersection.Width * intersection.Height) / (bodyRec.Width * bodyRec.Height));
                    return bottom;
                }
                if (bottom > maxDepth)
                {
                    maxDepth = bottom;
                    return maxDepth;
                }
            }
            return 0;
        }

        public GameObject GetCarryableObjects(RectangleF rectangle)
        {
            _carriableObjectList.Clear();
            _gameObjectPool.GetComponentList(_carriableObjectList, (int)rectangle.X, (int)rectangle.Y, (int)rectangle.Width, (int)rectangle.Height, CarriableComponent.Mask);

            foreach (var gameObject in _carriableObjectList)
            {
                if (!gameObject.IsActive)
                    continue;

                var component = gameObject.Components[CarriableComponent.Index] as CarriableComponent;

                if (component.IsActive && component.Rectangle.Rectangle.Intersects(rectangle))
                    return gameObject;
            }
            return null;
        }

        public Values.HitCollision Hit(GameObject originObject, Vector2 forceOrigin, Box hitBox, HitType type, int damage, bool doubleDamage, bool multidamage = true)
        {
            return Hit(originObject, forceOrigin, hitBox, type, damage, doubleDamage, out var direction, multidamage);
        }

        private void ReduceToClosestHittable<T>(Func<T, bool> shouldRestrict, Box hitBox, Vector2 forceOrigin) where T : GameObject
        {
            T target = null;
            float targetDistanceSq = float.MaxValue;

            foreach (var gameObject in _hittableObjectList)
            {
                if (gameObject.GetType() != typeof(T) || !gameObject.IsActive)
                    continue;

                var obj = (T)gameObject;
                if (!shouldRestrict(obj))
                    continue;

                var hittableComponent = obj.Components[HittableComponent.Index] as HittableComponent;
                if (!hittableComponent.IsActive || !hittableComponent.HittableBox.Box.Intersects(hitBox))
                    continue;

                var distanceSq = Vector2.DistanceSquared(hittableComponent.HittableBox.Box.Center, forceOrigin);
                if (target == null || distanceSq < targetDistanceSq)
                {
                    target = obj;
                    targetDistanceSq = distanceSq;
                }
            }
            if (target != null)
                _hittableObjectList.RemoveAll(go => go.GetType() == typeof(T) && !ReferenceEquals(go, target));
        }

        public Values.HitCollision Hit(GameObject originObject, Vector2 forceOrigin, Box hitBox, HitType type, int damage, bool doubleDamage, out Vector2 direction, bool multidamage = true)
        {
            // get all the near objects of the rectangle
            _hittableObjectList.Clear();
            _gameObjectPool.GetComponentList(_hittableObjectList, (int)hitBox.X, (int)hitBox.Y, (int)hitBox.Width, (int)hitBox.Height, HittableComponent.Mask);

            var hitCollision = Values.HitCollision.None;
            direction = Vector2.Zero;

            // If there are multiple bushes/crystals being poked, we only want to hit one of each kind.
            if (GameSettings.ClassicSword && (type & HitType.SwordPoke) != 0)
            {
                ReduceToClosestHittable<ObjBush>(bush => !bush.IsGrass, hitBox, forceOrigin);
                ReduceToClosestHittable<ObjCrystal>(crystal => !crystal.IsHardCrystal, hitBox, forceOrigin);
            }

            foreach (var gameObject in _hittableObjectList)
            {
                // If the game object is not active then skip it.
                if (!gameObject.IsActive)
                    continue;

                // An object can never hit itself (thrown enemies deal damage from their own body box).
                if (ReferenceEquals(gameObject, originObject))
                    continue;

                // Get the components of the game object hit.
                var bodyComponent = gameObject.Components[BodyComponent.Index] as BodyComponent;
                var damageFieldComponent = gameObject.Components[DamageFieldComponent.Index] as DamageFieldComponent;
                var hittableComponent = gameObject.Components[HittableComponent.Index] as HittableComponent;

                // If the hit component is not active or the hitbox and hittable box do not intersect.
                if (!hittableComponent.IsActive || !hittableComponent.HittableBox.Box.Intersects(hitBox))
                    continue;

                // Specialized hits that few objects make use of. Bush checks ClassicSword, while crystals check CystalSmash.
                if ((type & HitType.ClassicSword) != 0 && !hittableComponent.RespondClassicSword)
                    continue;
                if ((type & HitType.CrystalSmash) != 0 && !hittableComponent.RespondCrystalSmash)
                    continue;

                // Direction goes from the hitter towards the center of hittable box.
                direction = hittableComponent.HittableBox.Box.Center - forceOrigin;
                if (direction != Vector2.Zero)
                    direction.Normalize();

                // If the hit component contains a damage multiplier.
                if (type == HitType.Bow && hittableComponent.ArrowMultiplier ||
                    type == HitType.Bomb && hittableComponent.BombMultiplier ||
                    type == HitType.BombArrow && hittableComponent.BombMultiplier ||
                    type == HitType.MagicRod && hittableComponent.MagicRodMultiplier ||
                    type == HitType.ThrownObject && hittableComponent.ThrownMultiplier)
                    damage = 4;

                // The boomerange deals 24 damage when multiplier is in effect.
                if (type == HitType.Boomerang && hittableComponent.BoomerangMultiplier)
                    damage = 24;

                // The cheat for one hit kills.
                if (GameSettings.ChOneHitKills)
                    damage = 1000;

                hitCollision |= hittableComponent.Hit(originObject, direction, type, damage, doubleDamage);

                if (!multidamage && hitCollision != Values.HitCollision.None && hitCollision != Values.HitCollision.NoneBlocking)
                    return hitCollision;
            }
            return hitCollision;
        }

        private static Vector2 ApplyImpactOffset(Vector2 direction, Box pushBox, Box pushableBox)
        {
            // How strong the sideways nudge is relative to the main push (0 = none).
            float nudgeScale = 0.85f;

            // Primary push axis = the dominant component of the direction.
            bool primaryX = Math.Abs(direction.X) >= Math.Abs(direction.Y);
            float offset, halfExtent, primaryMag;

            if (primaryX)
            {
                offset     = pushableBox.Center.Y - pushBox.Center.Y;
                halfExtent = (pushBox.Height + pushableBox.Height) * 0.5f;
                primaryMag = Math.Abs(direction.X);
            }
            else
            {
                offset     = pushableBox.Center.X - pushBox.Center.X;
                halfExtent = (pushBox.Width + pushableBox.Width) * 0.5f;
                primaryMag = Math.Abs(direction.Y);
            }
            // Normalize, clamp, and scale relative to the main push so the angle stays consistent.
            float nudge = halfExtent > 0f ? MathHelper.Clamp(offset / halfExtent, -1f, 1f) : 0f;
            nudge *= nudgeScale * primaryMag;

            // Keep the primary component and add the perpendicular nudge.
            return primaryX
                ? new Vector2(direction.X, direction.Y + nudge)
                : new Vector2(direction.X + nudge, direction.Y);
        }

        public PushableComponent PushObject(Box box, Vector2 direction, PushableComponent.PushType type)
        {
            // get all the near objects of the rectangle
            _pushableObjectList.Clear();
            _gameObjectPool.GetComponentList(_pushableObjectList, (int)box.X, (int)box.Y, (int)box.Width, (int)box.Height, PushableComponent.Mask);

            PushableComponent outPushComponent = null;

            foreach (var gameObject in _pushableObjectList)
            {
                if (!gameObject.IsActive || gameObject.ShieldDeflect)
                    continue;

                var pushableComponent = gameObject.Components[PushableComponent.Index] as PushableComponent;

                // check for collision and if cooldown time is met
                if (pushableComponent.IsActive &&
                    pushableComponent.LastPushTime + pushableComponent.CooldownTime < Game1.TotalGameTime &&
                    box.Intersects(pushableComponent.PushableBox.Box))
                {
                    // object got inertia?
                    if (pushableComponent.InertiaTime > 0 && type == PushableComponent.PushType.Continues)
                    {
                        // the object was pushed the last frame?
                        if (pushableComponent.LastWaitTime >= Game1.TotalGameTimeLast)
                        {
                            pushableComponent.InertiaCounter -= Game1.DeltaTime;
                            if (pushableComponent.InertiaCounter <= 0 && pushableComponent.Push(direction, type))
                            {
                                pushableComponent.InertiaCounter = pushableComponent.InertiaTime;
                                pushableComponent.LastPushTime = Game1.TotalGameTime;
                                pushableComponent.LastPushDirection = direction;
                                outPushComponent = pushableComponent;
                            }
                        }
                        else
                        {
                            // reset inertia counter if pushing has just begone
                            pushableComponent.InertiaCounter = pushableComponent.InertiaTime;
                        }

                        pushableComponent.LastWaitTime = Game1.TotalGameTime;
                    }
                    else
                    {
                        var pushDirection = type == PushableComponent.PushType.Impact
                            ? ApplyImpactOffset(direction, box, pushableComponent.PushableBox.Box)
                            : direction;

                        if (pushableComponent.Push(pushDirection, type))
                        {
                            pushableComponent.LastPushTime = Game1.TotalGameTime;
                            pushableComponent.LastPushDirection = pushDirection;
                            outPushComponent = pushableComponent;
                        }
                    }
                }
            }
            return outPushComponent;
        }

        public bool InteractWithObject(Box box)
        {
            // get the interactable objects at the given position
            _interactableObjectList.Clear();
            _gameObjectPool.GetComponentList(_interactableObjectList, (int)box.X, (int)box.Y, (int)box.Width, (int)box.Height, InteractComponent.Mask);


            // go through all the interactable objects and check for collision before interacting with them
            foreach (var gameObject in _interactableObjectList)
            {
                if (!gameObject.IsActive)
                    continue;

                // Add a workaround if it's Tarin in raccoon form and powder is equipped: don't interact and instead use the powder.
                if (gameObject.GetType() == typeof(ObjRaccoon))
                {
                    int index = GameSettings.SwapButtons ? 1 : 0;
                    if (Game1.GameManager.Equipment[index] != null && Game1.GameManager.Equipment[index].Name == "powder")
                        return false;
                }
                var component = gameObject.Components[InteractComponent.Index] as InteractComponent;
                if (component.IsActive && component.BoxInteractable.Box.Intersects(box) && component.InteractFunction())
                    return true;
            }
            return false;
        }
    }
}
