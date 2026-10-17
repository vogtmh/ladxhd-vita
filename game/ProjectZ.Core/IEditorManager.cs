using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Screens;

namespace ProjectZ
{
    public interface IEditorManager
    {
        void SetUpEditorUi();
        void EditorUpdate(GameTime gameTime);
        void OffsetObjects(Map map, int offsetX, int offsetY);
        void RegisterEditorScreens(List<Screen> screens);
        void PopulateEditorObjectTemplates(Dictionary<string, GameObjectTemplate> templates, Func<object[], Map, int, int, object[]> addPositionFunc);
    }
}
