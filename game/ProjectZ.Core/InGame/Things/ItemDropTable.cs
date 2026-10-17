using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.Things
{
    internal class ItemDropTable
    {
        public struct LootTable
        {
            public string ItemName;
            public int ChanceNormal;
            public int ChanceLowHealth;

            public LootTable(string itemName, int chanceA, int chanceB)
            {
                ItemName = itemName;
                ChanceNormal = chanceA;
                ChanceLowHealth = chanceB;
            }
        }

        public static LootTable[] ItemDrops = new LootTable[]
        {
            // Item -- Chance -- Low HP Chance -- Index
            new("none",         0,   0),  //  0
            new("ruby",        25,  50),  //  1: 25/50% Rupee Chance, Only used by "Pincer"
            new("ruby",        50,  50),  //  2: 50/50% Rupee Chance, Most Common
            new("ruby",       100, 100),  //  3: 100% Rupee Chance, UNUSED
            new("heart",       25,  50),  //  4: 25/50% Heart, Most Common 
            new("heart",       50,  50),  //  5: 50/50% Heart, Only used by Arm Mimic, Boo Buddy, and Gibdo
            new("heart",      100, 100),  //  6: 100% Heart, Only used by Goomba
            new("fairy",       25,  50),  //  7: 25/50% Fairy, Only used by Anti-Fairy, Bomber, and Bone Putter
            new("fairy",       50,  50),  //  8: 50/50% Fairy, Only used by Anti-Kirby
            new("fairy",      100, 100),  //  9: 100% Fairy, Used by Ghini, Spark, and other special cases
            new("bomb_1",     100, 100),  // 10: 100% Bomb, Only used by Bombite and Hardhat Beetle
            new("arrow_1",    100, 100),  // 11: 100% Arrow, Only used by Armos Statue
            new("powder_1",   100, 100),  // 12: 100% Powder, UNUSED
            new("shieldBack", 100, 100),  // 13: Returns shield to player, Only used by Like-Like
                                          // 14: 25/50% Random Table, Only used by Buzz Blob (see "AiDamageState.BaseOnDeath")
                                          // 15: 100% Random Table (minus fairy), Only used by Ghini
        };

        public struct RandomTable
        {
            public string ItemName;

            public RandomTable(string itemName) 
            {
                ItemName = itemName;
            }
        }

        public static RandomTable[] RandomDrops = new RandomTable[]
        {
            new("ruby"),
            new("heart"),
            new("bomb_1"),
            new("ruby"),
            new("heart"),
            new("bomb_1"),
            new("arrow_1"),
            new("fairy"),
        };

        public static string GetItemDrop(int index, bool spawnPowerups)
        {
            // Try to get a powerup drop before getting an item.
            if (spawnPowerups && GetPowerupDrop() is string powerupDrop && powerupDrop != "")
                return powerupDrop;

            // If the index is greater than 14 (Buzz Blob or Ghini) use the random table.
            if (index >= 14)
                return GetRandomDrop(index, spawnPowerups);

            // Holds the item or empty string that is returned.
            string returnItem = "";

            // Get the item by index.
            LootTable tableEntry = ItemDrops[index];

            // Roll the dice to see if an item drops.
            int diceRoll = Game1.RandomNumber.Next(0, 100);

            // Get the chance of a drop based on current health.
            int dropChance = MapManager.ObjLink.IsLowHealth 
                ? tableEntry.ChanceLowHealth 
                : tableEntry.ChanceNormal;

            // See if it falls under the current drop chance
            if (diceRoll < dropChance) 
                returnItem = tableEntry.ItemName;

            // If it's a heart and the player disabled them clear the string.
            if (GameSettings.NoHeartDrops && returnItem == "heart")
                returnItem = "";

            // If it was not a hit, return an empty string.
            return returnItem;
        }

        private static string GetRandomDrop(int index, bool spawnPowerups)
        {
            // Try to get a powerup drop before getting an item.
            if (spawnPowerups && GetPowerupDrop() is string powerupDrop && powerupDrop != "")
                return powerupDrop;

            // Holds the item or empty string that is returned.
            string returnItem = "";

            // Roll the dice to see if an item drops.
            int diceRoll = Game1.RandomNumber.Next(0, 100);

            // Index 14 is 25/50 (Buzz Blob). Index 15 is 100 (Ghini).
            int dropChance = index == 14
                ? MapManager.ObjLink.IsLowHealth ? 50 : 25
                : 100;

            // See if it falls under the current drop chance
            if (diceRoll < dropChance) 
            {
                // Return a random item from the table. No fairy on Ghini drops.
                int randomItem = index == 14
                    ? Game1.RandomNumber.Next(0,8)
                    : Game1.RandomNumber.Next(0,7);
                RandomTable tableEntry = RandomDrops[randomItem];
                returnItem = tableEntry.ItemName;
            }
            // If it's a heart and the player disabled them clear the string.
            if (GameSettings.NoHeartDrops && returnItem == "heart")
                returnItem = "";

            // If it was not a hit, return an empty string.
            return returnItem;
        }

        private static string GetPowerupDrop()
        {
            // Check for powerup active and get the kill counts.
            int acorn_killcount = Game1.GameManager.GuardianAcornCount;
            int pop_killcount = Game1.GameManager.PieceOfPowerCount;

            // Check if the player disabled powerups.
            bool _disableGuardianAcorn = MapManager.ObjLink.DisableGuardianAcorn;
            bool _disablePieceofPower  = MapManager.ObjLink.DisablePieceOfPower;

            // Guardian Acorn: static 12 kills without taking damage. 
            if (acorn_killcount >= 12 && !_disableGuardianAcorn)
            {
                Game1.GameManager.GuardianAcornCount = 0;
                if (!MapManager.ObjLink.HasPowerup)
                    return "guardianAcorn";
            }
            // The number of enemies that need to be killed for a Piece of Power spawn fluctuates depending on max hearts.
            int pop_threshold = Game1.GameManager.MaxHearts switch
            {
                <= 6  => 30,
                <= 10 => 35,
                _     => 40
            };
            // Piece of Power: The threshold for kills relative to current hearts was met.
            if (pop_killcount >= pop_threshold && !_disablePieceofPower)
            {
                Game1.GameManager.PieceOfPowerCount = 0;
                if (!MapManager.ObjLink.HasPowerup)
                    return "pieceOfPower";
            }
            // If a powerup threshold was not met, return empty string.
            return "";
        }
    }
}
