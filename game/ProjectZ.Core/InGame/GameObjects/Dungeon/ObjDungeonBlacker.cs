using System;
using System.IO;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjDungeonBlacker : GameObject
    {
        private Color _baseColor;

        public ObjDungeonBlacker() : base("editor dungeon blacker")
        {
            EditorColor = Color.DarkRed * 0.75f;
        }

        public ObjDungeonBlacker(Map.Map map, int posX, int posY, int colorR, int colorG, int colorB, int colorA) : base(map)
        {
            // Check for the existence of "ObjDungeonBlacker.lahdmod" in folder "Data\Mods".
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjDungeonBlacker.lahdmod");

            Tags = Values.GameObjectTag.Utility;

            // If the file exists attempt to load in custom values.
            if (GameFS.Exists(modFile))
            {
                // If the map name was in the file then load its values.
                string[] values = ParseModFile(modFile, map.MapName);

                if (values != null) 
                {
                    colorR = Convert.ToInt32(values[1]);
                    colorG = Convert.ToInt32(values[2]);
                    colorB = Convert.ToInt32(values[3]);
                    colorA = Convert.ToInt32(values[4]);
                }
            }
            map.UseLight = true;
            map.LightColor = new Color(colorR, colorG, colorB) * (colorA / 255f);
            _baseColor = map.LightColor;
            IsDead = true;
        }

        public void SetBrightness(float bright)
        {
            Map.LightColor = _baseColor * bright;
        }

        private string[] ParseModFile(string modFile, string mapName)
        {
            // Loop through each line of the text file.
            foreach (string line in GameFS.ReadAllLines(modFile))
            {
                // Ignore empty lines and comment lines.
                if (string.IsNullOrEmpty(line) || line.Substring(0,2) == "//") 
                    continue;

                // Split string on semicolons and same line comments.
                string[] splitLine = line.Split(new char[] {';','/'});

                // If the line contains the map name.
                if (splitLine[0] == mapName)
                {
                    // Load the values into the array.
                    string[] values = new string[5];

                    for (int i = 0; i < 5; i++)
                        values[i] = splitLine[i].Trim();

                    return values;
                }
            }
            // If the map wasn't found just return null.
            return null;
        }
    }
}
