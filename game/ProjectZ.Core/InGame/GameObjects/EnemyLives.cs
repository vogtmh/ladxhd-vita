using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ProjectZ.InGame.Things;
using ProjectZ.InGame.SaveLoad;

namespace ProjectZ.InGame.GameObjects
{
    internal class EnemyLives
    {
        // Holds a list of all enemy HP default values.
        private static Dictionary<string, int> _defaultValues;

        // Enemy Hit Points
        public static int AnglerFry        = 1;
        public static int AntiFairy        = 4;
        public static int AntiKirby        = 8;
        public static int ArmMimic         = 4;
        public static int Armos            = 4;
        public static int Beetle           = 1;
        public static int Bloober          = 1;
        public static int Bomber           = 3;
        public static int Bombite          = 4;
        public static int BombiteGreen     = 4;
        public static int BonePutter       = 1;
        public static int BonePutterWing   = 3;
        public static int BooBuddy         = 4;
        public static int BuzzBlob         = 4;
        public static int CamoGoblin       = 1;
        public static int CheepCheep       = 1;
        public static int Crab             = 2;
        public static int Crow             = 2;
        public static int Darknut          = 2;
        public static int DarknutSpear     = 2;
        public static int Fish             = 1;
        public static int FlyingTile       = 1;
        public static int Gel              = 1;
        public static int Ghini            = 8;
        public static int GhiniGiant       = 8;
        public static int Gibdo            = 6;
        public static int Goomba           = 1;
        public static int GopongaFlower    = 4;
        public static int GopongaGiant     = 4;
        public static int GreenZol         = 1;
        public static int HardhatBeetle    = 4;
        public static int IronMask         = 2;
        public static int Karakoro         = 2;
        public static int Keese            = 1;
        public static int Leever           = 2;
        public static int LikeLike         = 2;
        public static int MadBomber        = 4;
        public static int MaskMimic        = 2;
        public static int MiniMoldorm      = 2;
        public static int Moblin           = 2;
        public static int MoblinSword      = 2;
        public static int MoblinPig        = 2;
        public static int MoblinPigSword   = 2;
        public static int Octorok          = 1;
        public static int OctorokWinged    = 1;
        public static int Pairodd          = 2;
        public static int Peahat           = 1;
        public static int Pincer           = 2;
        public static int PiranhaPlant     = 1;
        public static int Pokey            = 2;
        public static int PokeyPart        = 1;
        public static int PolsVoice        = 4;
        public static int Raven            = 2;
        public static int RedZol           = 1;
        public static int RiverZora        = 1;
        public static int Rope             = 1;
        public static int SeaUrchin        = 1;
        public static int ShroudedStalfos  = 2;
        public static int SpikedBeetle     = 2;
        public static int SpinyBeetle      = 1;
        public static int StalfosGreen     = 2;
        public static int StalfosKnight    = 2;
        public static int StalfosOrange    = 2;
        public static int Star             = 1;
        public static int Tektite          = 2;
        public static int Vacuum           = 1;
        public static int Vire             = 3;
        public static int WaterTektite     = 1;
        public static int Wizzrobe         = 4;
        public static int Zombie           = 1;

        // Mid-Boss Hit Points
        public static int ArmosKnight      = 12;
        public static int BaCSoldier       = 8;
        public static int Blaino           = 8;
        public static int CueBall          = 8;
        public static int DesertLanmola    = 8;
        public static int DodongoSnake     = 3;
        public static int GiantBuzzBlob    = 12;
        public static int Ghoma            = 12;
        public static int GrimCreeperFly   = 1;
        public static int Hinox            = 8;
        public static int KingMoblin       = 8;
        public static int MStalfos         = 3;
        public static int MStalfosMid      = 2;
        public static int RollingBones     = 8;
        public static int Smasher          = 8;
        public static int StoneHinox       = 8;
        public static int TurtleRock       = 16;

        // Boss Hit Points
        public static int AnglerFish       = 10;
        public static int EvilEagle        = 12;
        public static int Facade           = 18;
        public static int F_GiantZol       = 3;
        public static int F_Agahnim        = 4;
        public static int F_Moldorm        = 16;
        public static int F_Ganon          = 24;
        public static int F_DethI          = 16;
        public static int Genie            = 8;
        public static int GenieBottle      = 3;
        public static int HardHitBeetle    = 1;
        public static int HotHead          = 28;
        public static int Moldorm          = 4;
        public static int SlimeEel         = 8;
        public static int SlimeEye         = 2;
        public static int SlimeEyeHalf     = 5;

        public static void Initialize()
        {
            // Load in mod file values if present.
            ParseModFile();

            // Backup the default values or values set by the mod.
            BackupDefaultHP();

            // If the user set "Extra Enemy HP" then add the value to all lives.
            if (GameSettings.EnemyBonusHP > 0)
                AddToEnemyHP(GameSettings.EnemyBonusHP);
        }

        public static void ParseModFile()
        {
            // Read advanced file first (ObjLives section), lahdmod overrides it.
            ParseLines(ReadAdvancedSection("EnemyLives"));

            string modFile = Path.Combine(Values.PathLAHDMods, "EnemyLives.lahdmod");
            if (GameFS.Exists(modFile))
                ParseLines(GameFS.ReadAllLines(modFile));
        }

        private static IEnumerable<string> ReadAdvancedSection(string sectionName)
        {
            string advancedFile = SaveManager.GetAdvancedFile();
            if (!File.Exists(advancedFile))
                yield break;

            bool inSection = false;
            foreach (string fileLine in GameFS.ReadAllLines(advancedFile))
            {
                if (fileLine.TrimStart().StartsWith("//: "))
                {
                    inSection = fileLine.TrimStart().Substring(4).Trim() == sectionName;
                    continue;
                }
                if (inSection && !string.IsNullOrWhiteSpace(fileLine) && !fileLine.TrimStart().StartsWith("//"))
                    yield return fileLine;
            }
        }

        private static void ParseLines(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                string[] splitLine = line.Split(new char[] { '=', '/' });
                if (splitLine.Length < 2)
                    continue;

                string varName = splitLine[0].Trim();
                string varValue = splitLine[1].Trim();

                FieldInfo field = typeof(EnemyLives).GetField(varName, BindingFlags.Public | BindingFlags.Static);
                if (field == null) continue;

                try
                {
                    object convertedValue = Convert.ChangeType(varValue, field.FieldType, CultureInfo.InvariantCulture);
                    field.SetValue(null, convertedValue);
                }
                catch { }
            }
        }
        public static void BackupDefaultHP()
        {
            _defaultValues = new Dictionary<string, int>();

            FieldInfo[] enemyHP = typeof(EnemyLives).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var enemy in enemyHP)
            {
                if (enemy.FieldType == typeof(int))
                    _defaultValues[enemy.Name] = (int)enemy.GetValue(null);
            }
        }
        public static void RestoreDefaultHP()
        {
            FieldInfo[] enemyHP = typeof(EnemyLives).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var enemy in enemyHP)
            {
                if (enemy.FieldType == typeof(int) && _defaultValues.TryGetValue(enemy.Name, out int value))
                    enemy.SetValue(null, value);
            }
        }

        public static void AddToEnemyHP(int amount)
        {
            FieldInfo[] enemyHP = typeof(EnemyLives).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var enemy in enemyHP)
            {
                if (enemy.FieldType == typeof(int))
                {
                    int current = (int)enemy.GetValue(null);
                    enemy.SetValue(null, current + amount);
                }
            }
        }
    }
}
