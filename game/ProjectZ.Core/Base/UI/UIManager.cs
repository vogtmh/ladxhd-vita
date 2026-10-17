using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Base.UI
{
    public class UiManager
    {
        public static bool HideOverlay = false;

        public string CurrentScreen
        {
            get => _currentScreen;
            set => _currentScreen = value.ToUpper();
        }

        private readonly List<UiElement> _elementList = new List<UiElement>();
        private readonly List<UiElement> _elementListNoHide = new List<UiElement>();

        // native file dialogs block the game loop inside their own modal event loop,
        // opening one in the same frame as the initiating click leaves the dialog
        // deaf to mouse input on X11, so such actions are queued until release
        private readonly Queue<Action> _deferredActions = new Queue<Action>();

        private string _currentScreen;

        private IEnumerable<UiElement> AllElements => _elementList.Concat(_elementListNoHide);

        private static void RemoveMarked(List<UiElement> list) =>
            list.RemoveAll(e => e.Remove);

        public void EnqueueDeferredAction(Action action) => _deferredActions.Enqueue(action);

        private void RunDeferredActions()
        {
            // run once the pointer click that triggered the action was released
            while (_deferredActions.Count > 0 && !InputHandler.MouseLeftDown())
            {
                var action = _deferredActions.Dequeue();

                InputHandler.ResetInputState();
                action();
                InputHandler.ResetInputState();
            }
        }

        private void UpdateElements(List<UiElement> list)
        {
            foreach (var element in list.ToList())
                if (element.Screens.Contains(_currentScreen))
                    element.Update();
        }

        public void Update()
        {
            RunDeferredActions();

            RemoveMarked(_elementList);
            RemoveMarked(_elementListNoHide);
            UpdateElements(_elementList);
            UpdateElements(_elementListNoHide);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // The overlay can be hidden with the tilde "~" key.
            if (HideOverlay)
            {
                // At least draw the dialog box.
                Game1.GameManager.InGameOverlay.TextboxOverlay.TextboxBackground.Draw(spriteBatch);

                // And draw dialog choices.
                foreach (var element in Game1.GameManager.InGameOverlay.TextboxOverlay.TextboxBackgroundChoice.ToList())
                    if (element.Screens.Contains(_currentScreen) && element.IsVisible)
                        element.Draw(spriteBatch);
                return;
            } 
            // Draw the UI elements.
            foreach (var element in AllElements.ToList())
                if (element.Screens.Contains(_currentScreen) && element.IsVisible)
                    element.Draw(spriteBatch);
        }

        public void DrawBlur(SpriteBatch spriteBatch)
        {
            foreach (var element in _elementListNoHide.ToList())
                if (element.Screens.Contains(_currentScreen) && element.IsVisible)
                    element.DrawBlur(spriteBatch);

            // The overlay can be hidden with the tilde "~" key.
            if (HideOverlay)
            {
                // Backgrounds are drawn in this pass now, so mirror Draw's dialog exception.
                Game1.GameManager.InGameOverlay.TextboxOverlay.TextboxBackground.DrawBlur(spriteBatch);

                // It's unlikely it matters since dialog + blurred background aren't supposed to co-exist, mostly just a safety fallback.
                foreach (var element in Game1.GameManager.InGameOverlay.TextboxOverlay.TextboxBackgroundChoice.ToList())
                    if (element.Screens.Contains(_currentScreen) && element.IsVisible)
                        element.DrawBlur(spriteBatch);
                return;
            }
            // Draw the UI elements.
            foreach (var element in _elementList.ToList())
                if (element.Screens.Contains(_currentScreen) && element.IsVisible)
                    element.DrawBlur(spriteBatch);
        }

        public void OnResize()
        {
            foreach (var uiElement in AllElements.ToList())
                uiElement.SizeUpdate?.Invoke(uiElement);
        }

        public UiElement AddElement(UiElement element, bool alwaysDraw = false)
        {
            if (element == null) return null;

            if (alwaysDraw)
                _elementListNoHide.Add(element);
            else
                _elementList.Add(element);

            return element;
        }

        public UiElement GetElement(string elementId)
        {
            return AllElements.FirstOrDefault(e => e.ElementId == elementId);
        }

        public void RemoveElement(string elementId, string screenId)
        {
            foreach (var element in AllElements.ToList())
                if (element.ElementId.Contains(elementId) && element.Screens.Contains(screenId))
                    element.Remove = true;
        }
    }
}
