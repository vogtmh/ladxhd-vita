using System;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameSystems
{
    internal class CheatSystem
    {
        public static void RefreshItemCheat(bool cheatEnabled, string upgrade, int countLow, int countHigh, string itemName)
        {
            // If the cheat is disabled don't do anything.
            if (!cheatEnabled)
                return;

            // Remove the toadstool if it's in the inventory.
            if (cheatEnabled && itemName == "powder")
                Game1.GameManager.RemoveItem("toadstool", 1);

            // Check if the player upgraded the item count, and get the max count and the item.
            var hasUpgrade = Game1.GameManager.SaveManager.GetString(upgrade, "0") == "1";
            var count = hasUpgrade ? countHigh : countLow;
            var item = Game1.GameManager.GetItem(itemName);

            // Add the item to the player's inventory.
            if (item == null)
                Game1.GameManager.CollectItem(new GameItemCollected(itemName) { Count = count }, -1, false, true);
            else
                item.Count = count;

            // Rupees need the UI updated with the new value.
            if (itemName == "ruby")
                ItemDrawHelper.InstantRupeeCollection(999);
        }

        public static void ToggleNoClipping()
        {
            // Link's body component.
            var body = MapManager.ObjLink.Body;

            // Set the state of no clipping based on the state of the cheat.
            if (MapManager.ObjLink.Map != null)
                body.CollisionTypes = GameSettings.ChNoClipping && !MapManager.ObjLink.Map.Is2dMap
                    ? Values.CollisionTypes.Enemy | Values.CollisionTypes.PlayerItem | Values.CollisionTypes.LadderTop
                    : Values.CollisionTypes.Normal | Values.CollisionTypes.Enemy | Values.CollisionTypes.PlayerItem | Values.CollisionTypes.LadderTop;
        }

        private static int SlotOf(params string[] itemNames)
        {
            // A shorter version of GameManager.
            var gm = Game1.GameManager;

            // Loop through the equipment and return the slot number.
            for (var i = 0; i < gm.Equipment.Length; i++)
            {
                if (gm.Equipment[i] != null && Array.IndexOf(itemNames, gm.Equipment[i].Name) >= 0)
                    return i;
            }
            // If we can't find it, return -1 to notify item was not found.
            return -1;
        }

        public static void EnableGiveAllItems()
        {
            // Instead of typing this over and over again...
            var gm = Game1.GameManager;

            // Powder will be given to the player so remove the toadstool.
            Game1.GameManager.RemoveItem("toadstool", 1);

            // Replaces the item while attempting to preserve the slot number.
            void Replace(string giveItemName, int count, params string[] removeItemNames)
            {
                // Get the slot number of the item if possible.
                var getSlot = SlotOf(removeItemNames);
                var useSlot = getSlot < 0 ? 0 : getSlot;

                // Remove any equipment the player has.
                foreach (var itemName in removeItemNames)
                    gm.RemoveEquipment(itemName);

                // Add the item to the player's inventory attempting to preserve slot. Set to "0" if not available.
                gm.CollectItem(new GameItemCollected(giveItemName) { Count = count }, useSlot, false, true);
            }
            // Remove any items the player has and give them all items.
            Replace("sword2",       1, "sword1", "sword2");
            Replace("mirrorShield", 1, "shield", "mirrorShield");
            Replace("stonelifter2", 1, "stonelifter", "stonelifter2");
            Replace("pegasusBoots", 1, "pegasusBoots");
            Replace("powder",      20, "powder");
            Replace("bomb",        30, "bomb");
            Replace("bow",         30, "bow");
            Replace("ocarina",      1, "ocarina");
            Replace("flippers",     1, "flippers");

            // Some items can be traded for the boomerang.
            var tradedItem = Game1.GameManager.SaveManager.GetString("tradded_item", "0");

            // Do not add the item if it was traded away.
            if (tradedItem != "shovel")
                Replace("shovel", 1, "shovel");
            if (tradedItem != "feather")
                Replace("feather", 1, "feather");
            if (tradedItem != "magicRod")
                Replace("magicRod", 1, "magicRod");
            if (tradedItem != "hookshot")
                Replace("hookshot", 1, "hookshot");
        }

        public static void DisableGiveAllItems()
        {
            // Save myself from arthritis.
            var gm = Game1.GameManager;

            // Gets the count of the item the player currently owns.
            int CountOf(params string[] itemNames)
            {
                // Loop through the item names.
                foreach (var name in itemNames)
                {
                    // Get the item by it's name.
                    var itemCollected = gm.GetItem(name);

                    // If the player has the item it will have a count.
                    if (itemCollected != null)
                        return itemCollected.Count;
                }
                // Default the count to 1. Won't be used if player doesn't have item stored.
                return 1;
            }

            // Try to restore items the player collected through normal gameplay. Higher tiers take priority over lower tiers.
            void Restore(params string[] restoreItemNames)
            {
                // Get the slot number of the item if possible.
                var getSlot = SlotOf(restoreItemNames);
                var useSlot = getSlot < 0 ? 0 : getSlot;

                // Get the item count so it can be restored.
                var itemCount = CountOf(restoreItemNames);

                // Stores the name of the item to restore.
                var restoreItem = "";

                // Remove all possible items the player collected.
                foreach (var itemName in restoreItemNames)
                    gm.RemoveEquipment(itemName);

                // Loop through the items once more.
                foreach (var itemName in restoreItemNames)
                {
                    // If the player collected the item legitimately set the name.
                    if (gm.SaveManager.GetString("store_" + itemName) == "1")
                        restoreItem = itemName;
                }
                // If the name was set then restore the item.
                if (!string.IsNullOrEmpty(restoreItem))
                    gm.CollectItem(new GameItemCollected(restoreItem) { Count = itemCount }, useSlot, false, true);
            }
            // Restore items that have the "store_itemname" key-value pair set to "1".
            Restore("sword1", "sword2");
            Restore("shield", "mirrorShield");
            Restore("feather");
            Restore("stonelifter", "stonelifter2");
            Restore("pegasusBoots");
            Restore("shovel");
            Restore("magicRod");
            Restore("hookshot");
            Restore("boomerang");
            Restore("powder");
            Restore("bomb");
            Restore("bow");
            Restore("ocarina");
            Restore("flippers");
        }

        public static void RestoreMissingTrackedItems()
        {
            // We've been through this already.
            var gm = Game1.GameManager;

            // If a legitimately owned item was not written to the save add it to the inventory.
            void AddMissing(params string[] itemNames)
            {
                // If there is more than one tiered item get the highest tier that is owned.
                string owned = null;
                foreach (var name in itemNames)
                {
                    if (gm.SaveManager.GetString("store_" + name) == "1")
                        owned = name;
                }
                // If the current item is not owned then do not add to the inventory.
                if (owned == null || gm.GetItem(owned) != null)
                    return;

                // Collect the item and give it to the player.
                gm.CollectItem(new GameItemCollected(owned) { Count = 1 }, 0, false, true);
            }
            // The list of items that potentially need restored.
            AddMissing("sword1", "sword2");
            AddMissing("shield", "mirrorShield");
            AddMissing("feather");
            AddMissing("stonelifter", "stonelifter2");
            AddMissing("pegasusBoots");
            AddMissing("shovel");
            AddMissing("magicRod");
            AddMissing("hookshot");
            AddMissing("boomerang");
            AddMissing("ocarina");
            AddMissing("flippers");
        }
    }
}
