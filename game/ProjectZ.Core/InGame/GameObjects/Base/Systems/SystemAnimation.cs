using System;
using System.Collections.Generic;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Pools;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Systems
{
    class SystemAnimation
    {
        public ComponentPool Pool;
        private readonly List<GameObject> _objectList = new List<GameObject>();
        private readonly HashSet<GameObject> _objectListSet = new();
        private readonly ObjectManager _objectManager;

        public bool GameWorldFrozen;

        public SystemAnimation(ObjectManager objectManager)
        {
            _objectManager = objectManager;
        }

        public void Update(bool dialogOpen)
        {
            // Clear the lists before rebuilding them.
            _objectList.Clear();
            _objectListSet.Clear();

            // Classic Camera: Only update objects within the current field.
            if (Camera.ClassicMode && !Camera.LockCamera && GameSettings.ClassicBorder != 0 && (GameSettings.ClassicBorder != 1 || GameSettings.ClassicAlpha >= 1.0f))
            {
                Pool.GetComponentList(_objectList, ObjectManager.UpdateField.X, ObjectManager.UpdateField.Y, ObjectManager.UpdateField.Width, ObjectManager.UpdateField.Height, BaseAnimationComponent.Mask);
                ObjectManager.FilterObjectsInField(_objectList, ObjectManager.ActualField);
                _objectListSet.UnionWith(_objectList);
            }
            // Normal Camera: Update objects that are within the viewport.
            else
            {
                Pool.GetComponentList(_objectList, ObjectManager.ViewportX, ObjectManager.ViewportY, ObjectManager.ViewportW, ObjectManager.ViewportH, BaseAnimationComponent.Mask);
                _objectListSet.UnionWith(_objectList);
            }
            // Always include certain objects that are flagged as "always animate".
            for (int i = 0; i < _objectManager.AlwaysAnimateObjectsTemp.Count; i++)
            {
                var gameObject = _objectManager.AlwaysAnimateObjectsTemp[i];
                if (gameObject != null && !gameObject.IsDead && _objectListSet.Add(gameObject))
                    _objectList.Add(gameObject);
            }
            // Update all game object animation components in the list.
            for (int i = 0; i < _objectList.Count; i++)
            {
                var gameObject = _objectList[i];

                // During a freeze, only objects flagged FreezePersist keep animating.
                if (!gameObject.IsActive || (GameWorldFrozen && !gameObject.FreezePersist))
                    continue;

                if (gameObject.Components[BaseAnimationComponent.Index] is BaseAnimationComponent animationComponent)
                    if (!dialogOpen || animationComponent.UpdateWithOpenDialog)
                        animationComponent.UpdateAnimation();
            }
        }
    }
}
