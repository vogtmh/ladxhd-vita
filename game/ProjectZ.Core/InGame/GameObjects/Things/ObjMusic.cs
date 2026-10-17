using System.Collections.Generic;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.SaveLoad;

namespace ProjectZ.InGame.GameObjects.Things
{
    class ObjMusic : GameObject
    {
        private string _title;
        private float _counter;

        public ObjMusic() : base("editor music") { }

        public ObjMusic(Map.Map map, int posX, int posY, string title) : base(map)
        {
            // Store the currently playing title.
            _title = title;

            // Sometimes "special cases" overwrite the track that was proposed.
            if (int.TryParse(_title, out var songNr))
                Map.MapMusic[0] = GetProperMusicTrack(songNr);

            // Richard's Villa has an achievement for listening for Totaka's song.
            if (_title == "63" && map.MapName == "house7.map" && !AchievementManager.IsEarned(33))
                AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            else
                IsDead = true;
        }

        private static readonly Dictionary<int, int> SongToDungeon = new()
        {
            // This matches the music track with the level { track, level }
            { 19, 1 }, { 20, 2 }, { 21, 3 }, { 22, 4 }, 
            { 74, 5 }, { 87, 6 }, { 90, 7 }, { 89, 8 }
        };

        private int GetProperMusicTrack(int songNr)
        {
            // Hack to play the intro music.
            if (Game1.GameManager.SaveManager.GetString("introMusic", "0") == "1")
                return 28;

            // Hack to play the proper music in moblin cave during BowWow rescue.
            if (songNr == 61)
            {
                if (Game1.GameManager.SaveManager.GetString("mc_enemies", "0") == "1")
                    return songNr;
                else
                    return 37;
            }
            // Hack to play the proper dungeon music.
            int dungeonClear = 23;
            if (SongToDungeon.TryGetValue(songNr, out int dungeonIndex))
            {
                string heartKey = $"d{dungeonIndex}_nHeart";
                string instrumentKey = $"instrument{dungeonIndex - 1}";

                if (Game1.GameManager.SaveManager.GetString(heartKey) == "1" &&
                    (Game1.GameManager.GetItem(instrumentKey)?.Count ?? 0) < 1)
                {
                    return dungeonClear;
                }
            }
            // Hack to stop any music in slot 2 when walking into a house.
            Game1.AudioManager.SetMusic(-1, 2);

            // Return the actual song assigned to "ObjMusic".
            return songNr;
        }

        private void Update()
        {
            // If the track changed or the achievement was earned.
            if (Game1.AudioManager.GetCurrentMusic() != 63 || AchievementManager.IsEarned(33))
            {
                IsDead = true;
                RemoveComponent(UpdateComponent.Index);
                return;
            }
            // Not 100% accurate but close enough. We will use game time to know when to unlock it.
            _counter += Game1.DeltaTime;
            if (_counter >= 155000)
            {
                AchievementManager.Earn(33);
            }
        }
    }
}
