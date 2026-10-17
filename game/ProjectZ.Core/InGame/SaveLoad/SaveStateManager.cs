using System.IO;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.SaveLoad
{
    internal class SaveStateManager
    {
        public class SaveState
        {
            public string Name;
            public int MaxHearts;
            public int CurrentHealth;
            public int CurrentRupees;
            public int CurrentShells;
            public int Deaths;
            public int CloakType;
            public float TotalPlaytime;
            public bool SwordLevel2;
            public bool MirrorShield;
            public bool Thief;
            public bool GameCleared;
            public bool[] Instruments = new bool[8];
        }

        public static SaveState[] SaveStates = new SaveState[SaveCount];

        public const int SaveCount = 4;

        public static void LoadSaveData()
        {
            for (var i = 0; i < SaveCount; i++)
            {
                var saveManager = new SaveManager();

                if (saveManager.LoadFile(Path.Combine(Values.PathSaveFolder, SaveGameSaveLoad.SaveFileName + i)))
                {
                    SaveStates[i] = new SaveState();
                    SaveStates[i].Name = saveManager.GetString("savename");
                    SaveStates[i].CurrentHealth = saveManager.GetInt("currentHealth");
                    SaveStates[i].MaxHearts = saveManager.GetInt("maxHearts");
                    SaveStates[i].CurrentRupees = saveManager.GetInt("rubyCount", 0);
                    SaveStates[i].CurrentShells = saveManager.ShellCount;
                    SaveStates[i].Deaths = saveManager.GetInt("deathCount", 0);
                    SaveStates[i].CloakType = saveManager.GetInt("cloak", 0);
                    SaveStates[i].TotalPlaytime = saveManager.GetFloat("totalPlaytime", 0.0f);
                    SaveStates[i].SwordLevel2 = saveManager.HasSwordLevel2;
                    SaveStates[i].MirrorShield = saveManager.HasMirrorShield;
                    SaveStates[i].Thief = saveManager.GetBool("ThiefState", false);
                    SaveStates[i].GameCleared = saveManager.GetBool("cleared", false);

                    for (var j = 0; j < 8; j++)
                        SaveStates[i].Instruments[j] = saveManager.HasInstrument(j);
                }
                else
                {
                    SaveStates[i] = null;
                }
            }
        }
    }
}
