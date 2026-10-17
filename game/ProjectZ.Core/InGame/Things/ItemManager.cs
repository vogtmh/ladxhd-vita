using System.Collections.Generic;
using ProjectZ.InGame.Graphics;

namespace ProjectZ.InGame.Things
{
    public class ItemManager
    {
        public Dictionary<string, GameItem> Items => _items;

        public GameItem this[string key] => key != null && _items.ContainsKey(key) ? _items[key] : null;

        private readonly Dictionary<string, GameItem> _items = new Dictionary<string, GameItem>();

        public void Load()
        {
            // Clear the list before rebuilding as this may have been reset 
            // when the user toggled the option "Disable Helper Messages".
            _items.Clear();

            // dungeon
            // same keys but with different sounds and one does show the description
            _items.Add("smallkey", new GameItem(
                sprite: Resources.GetSprite("smallkey"),
                name: "smallkey",
                count: 1,
                maxCount: 9,
                drawLength: 1,
                soundEffectName: "D370-01-01",
                swordCollect: GameSettings.SwGrabSmallKey,
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            _items.Add("smallkeyChest", new GameItem(
                sprite: Resources.GetSprite("smallkey"),
                name: "smallkey",
                pickUpDialog: "smallkey",
                count: 1,
                drawLength: 1,
                soundEffectName: "D360-01-01",
                turnDownMusic: true,
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            _items.Add("nightmarekey", new GameItem(
                sprite: Resources.GetSprite("nightmarekey"),
                name: "nightmarekey",
                maxCount: 1,
                pickUpDialog: "nightmarekey",
                soundEffectName: "D360-01-01",
                turnDownMusic: true,
                level: -1
            ));
            _items.Add("compass", new GameItem(
                sprite: Resources.GetSprite("compass"),
                name: "compass",
                count: 1,
                maxCount: 1,
                pickUpDialog: "compass",
                soundEffectName: "D360-01-01",
                turnDownMusic: true
            ));
            _items.Add("dmap", new GameItem(
                sprite: Resources.GetSprite("dmap"),
                name: "dmap",
                count: 1,
                maxCount: 1,
                pickUpDialog: "dmap",
                soundEffectName: "D360-01-01",
                turnDownMusic: true
            ));
            _items.Add("stonebeak", new GameItem(
                sprite: Resources.GetSprite("stonebeak"),
                name: "stonebeak",
                count: 1,
                maxCount: 1,
                pickUpDialog: "stonebeak",
                soundEffectName: "D360-01-01",
                turnDownMusic: true
            ));

            _items.Add("potion", new GameItem(
                sprite: Resources.GetSprite("potion"),
                name: "potion",
                count: 1,
                maxCount: 1,
                pickUpDialog: "potion",
                soundEffectName: "D360-01-01",
                turnDownMusic: true
            ));
            _items.Add("potion_show", new GameItem(
                name: "potion",
                count: 1,
                showAnimation: 1,
                soundEffectName: "D360-01-01",
                turnDownMusic: true
            ));
            _items.Add("shell", new GameItem(
                sprite: Resources.GetSprite("shell"),
                mapSprite: Resources.GetSprite("shellMap"),
                name: "shell",
                pickUpDialog: "seashell",
                count: 1,
                soundEffectName: "D370-01-01",
                swordCollect: GameSettings.SwGrabNormal,
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("shellChest", new GameItem(
                sprite: Resources.GetSprite("shell"),
                mapSprite: Resources.GetSprite("shellMap"),
                name: "shell",
                pickUpDialog: "seashell",
                count: 1,
                soundEffectName: "D360-01-01",
                turnDownMusic: true
            ));
            _items.Add("shellPresent", new GameItem(
                sprite: Resources.GetSprite("shell_present"),
                name: "shell",
                pickUpDialog: "seashell",
                count: 1,
                showAnimation: 3,
                soundEffectName: "D370-01-01",
                collectWidth: 10,
                collectHeight: 10,
                collectOffsetX: 3,
                collectOffsetY: -2
            ));
            
            _items.Add("goldLeaf", new GameItem(
                sprite: Resources.GetSprite("goldLeafMenu"),
                mapSprite: Resources.GetSprite("goldLeaf"),
                name: "goldLeaf",
                pickUpDialog: "goldLeaf",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                showAnimation: 1,
                showTime: 1500,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 8,
                collectHeight: 15
            ));

            // instruments
            _items.Add("instrument0", new GameItem(
                sprite: Resources.GetSprite("instrument0"),
                name: "instrument0",
                pickUpDialog: "instrument0",
                count: 1,
                maxCount: 1,
                instrument: true,
                showAnimation: 1,
                showEffect: true,
                showTime: 1500,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 16,
                collectHeight: 16
            ));
            _items.Add("instrument1", new GameItem(
                sprite: Resources.GetSprite("instrument1"),
                name: "instrument1",
                pickUpDialog: "instrument1",
                count: 1,
                maxCount: 1,
                instrument: true,
                showAnimation: 1,
                showEffect: true,
                showTime: 1500,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 16,
                collectHeight: 16
            ));
            _items.Add("instrument2", new GameItem(
                sprite: Resources.GetSprite("instrument2"),
                name: "instrument2",
                pickUpDialog: "instrument2",
                count: 1,
                maxCount: 1,
                instrument: true,
                showAnimation: 1,
                showEffect: true,
                showTime: 1500,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 16,
                collectHeight: 16
            ));
            _items.Add("instrument3", new GameItem(
                sprite: Resources.GetSprite("instrument3"),
                name: "instrument3",
                pickUpDialog: "instrument3",
                count: 1,
                maxCount: 1,
                instrument: true,
                showAnimation: 1,
                showEffect: true,
                showTime: 1500,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 16,
                collectHeight: 16
            ));
            _items.Add("instrument4", new GameItem(
                sprite: Resources.GetSprite("instrument4"),
                name: "instrument4",
                pickUpDialog: "instrument4",
                count: 1,
                maxCount: 1,
                instrument: true,
                showAnimation: 1,
                showEffect: true,
                showTime: 1500,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 16,
                collectHeight: 16
            ));
            _items.Add("instrument5", new GameItem(
                sprite: Resources.GetSprite("instrument5"),
                name: "instrument5",
                pickUpDialog: "instrument5",
                count: 1,
                maxCount: 1,
                instrument: true,
                showAnimation: 1,
                showEffect: true,
                showTime: 1500,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 16,
                collectHeight: 16
            ));
            _items.Add("instrument6", new GameItem(
                sprite: Resources.GetSprite("instrument6"),
                name: "instrument6",
                pickUpDialog: "instrument6",
                count: 1,
                maxCount: 1,
                instrument: true,
                showAnimation: 1,
                showEffect: true,
                showTime: 1500,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 16,
                collectHeight: 16
            ));
            _items.Add("instrument7", new GameItem(
                sprite: Resources.GetSprite("instrument7"),
                name: "instrument7",
                pickUpDialog: "instrument7",
                count: 1,
                maxCount: 1,
                instrument: true,
                showAnimation: 1,
                showEffect: true,
                showTime: 1500,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 16,
                collectHeight: 16
            ));

            // TRADE 0: Yoshi Doll
            _items.Add("trade0", new GameItem(
                sprite: Resources.GetSprite("trade0"),
                name: "trade0",
                tradeItem: true,
                pickUpDialog: "yoshiPickup",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 1: Ribbon
            _items.Add("trade1", new GameItem(
                sprite: Resources.GetSprite("trade1"),
                name: "trade1",
                tradeItem: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 2: Dog Food
            _items.Add("trade2", new GameItem(
                sprite: Resources.GetSprite("trade2"),
                name: "trade2",
                tradeItem: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 3: Bananas
            _items.Add("trade3", new GameItem(
                sprite: Resources.GetSprite("trade3"),
                name: "trade3",
                tradeItem: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 4: Stick
            _items.Add("trade4", new GameItem(
                sprite: Resources.GetSprite("trade4"),
                name: "trade4",
                tradeItem: true,
                pickUpDialog: "trade4",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 5: Honeycomb
            _items.Add("trade5", new GameItem(
                sprite: Resources.GetSprite("trade5"),
                mapSprite: Resources.GetSprite("trade5Map"),
                name: "trade5",
                tradeItem: true,
                pickUpDialog: "trade5",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 6: Pineapple
            _items.Add("trade6", new GameItem(
                sprite: Resources.GetSprite("trade6"),
                name: "trade6",
                tradeItem: true,
                pickUpDialog: "trade6Collected",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 7: Hibiscus
            _items.Add("trade7", new GameItem(
                sprite: Resources.GetSprite("trade7"),
                name: "trade7",
                tradeItem: true,
                pickUpDialog: "trade7",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 8: Goat's Letter
            _items.Add("trade8", new GameItem(
                sprite: Resources.GetSprite("trade8"),
                name: "trade8",
                tradeItem: true,
                pickUpDialog: "trade8",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 9: Broom
            _items.Add("trade9", new GameItem(
                // shown icon is browner
                sprite: Resources.GetSprite("trade9"),
                name: "trade9",
                tradeItem: true,
                pickUpDialog: "trade9",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 10: Fishing Hook
            _items.Add("trade10", new GameItem(
                sprite: Resources.GetSprite("trade10"),
                name: "trade10",
                tradeItem: true,
                pickUpDialog: "trade10",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 11: Mermaid's Necklace / Pink Bra
            _items.Add("trade11", new GameItem(
                sprite: Resources.GetSprite("trade11"),
                name: "trade11",
                tradeItem: true,
                pickUpDialog: "trade11",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 12: Mermaid's Scale
            _items.Add("trade12", new GameItem(
                sprite: Resources.GetSprite("trade12"),
                name: "trade12",
                tradeItem: true,
                pickUpDialog: "trade12",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));
            // TRADE 13: Magnifying Lens
            _items.Add("trade13", new GameItem(
                sprite: Resources.GetSprite("trade13"),
                name: "trade13",
                tradeItem: true,
                pickUpDialog: "trade13",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetY: -1
            ));

            _items.Add("marin", new GameItem(
                sprite: Resources.GetSprite("marin_item"),
                name: "marin",
                pickUpDialog: "maria_collected",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                showAnimation: 1
            ));
            _items.Add("rooster", new GameItem(
                sprite: Resources.GetSprite("marin_item"),
                name: "rooster",
                pickUpDialog: "rooster",
                count: 1,
                maxCount: 1
            ));
            _items.Add("ghost", new GameItem(
                sprite: Resources.GetSprite("marin_item"),
                name: "ghost",
                count: 1,
                maxCount: 1
            ));

            // overworld
            // TODO: look into the colors
            _items.Add("ruby", new GameItem(
                sprite: Resources.GetSprite("rubyBlue"),
                name: "ruby",
                count: 1,
                maxCount: 999,
                soundEffectName: "D370-05-05",
                swordCollect: GameSettings.SwGrabNormal,
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            _items.Add("rubyGreen", new GameItem(
                sprite: Resources.GetSprite("rubyGreen"),
                animateSprite: true,
                name: "ruby",
                count: 5,
                soundEffectName: "D370-05-05",
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            // TODO: shouldnt red be 30?
            _items.Add("ruby5", new GameItem(
                sprite: Resources.GetSprite("rubyRed"),
                name: "ruby",
                count: 5,
                soundEffectName: "D370-05-05",
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            _items.Add("ruby10", new GameItem(
                sprite: Resources.GetSprite("rubyBlue"),
                name: "ruby",
                count: 10,
                soundEffectName: "D370-05-05",
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            _items.Add("ruby20", new GameItem(
                name: "ruby",
                pickUpDialog: "ruby20",
                count: 20,
                soundEffectName: "D360-01-01",
                turnDownMusic: true,
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            // trendy game ruby
            _items.Add("ruby30", new GameItem(
                sprite: Resources.GetSprite("rubyRed"),
                name: "ruby",
                pickUpDialog: "ruby30",
                count: 30,
                soundEffectName: "D370-05-05",
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            _items.Add("ruby50", new GameItem(
                name: "ruby",
                pickUpDialog: "ruby50",
                count: 50,
                soundEffectName: "D360-01-01",
                turnDownMusic: true,
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            _items.Add("ruby100", new GameItem(
                name: "ruby",
                pickUpDialog: "ruby100",
                count: 100,
                soundEffectName: "D360-01-01",
                turnDownMusic: true,
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));
            _items.Add("ruby200", new GameItem(
                sprite: Resources.GetSprite("rubyBlue"),
                name: "ruby",
                pickUpDialog: "ruby200",
                count: 200,
                soundEffectName: "D360-01-01",
                turnDownMusic: true,
                collectWidth: 8,
                collectHeight: 14,
                collectOffsetX: -1
            ));

            _items.Add("heart", new GameItem(
                sprite: Resources.GetSprite("heart"),
                name: "heart",
                count: 1,
                maxCount: 999,
                soundEffectName: "D360-20-14",
                swordCollect: GameSettings.SwGrabNormal,
                collectWidth: 8,
                collectHeight: 7,
                collectOffsetX: -1
            ));
            _items.Add("heart_1", new GameItem(
                sprite: Resources.GetSprite("heart"),
                name: "heart",
                pickUpDialog: "heart",
                count: 1,
                maxCount: 999,
                soundEffectName: "D360-20-14",
                collectWidth: 8,
                collectHeight: 7,
                collectOffsetX: -1
            ));
            _items.Add("heart_3", new GameItem(
                sprite: Resources.GetSprite("heart"),
                name: "heart",
                count: 3,
                maxCount: 999,
                soundEffectName: "D370-01-01",
                swordCollect: GameSettings.SwGrabNormal,
                collectWidth: 8,
                collectHeight: 7,
                collectOffsetX: -1
            ));

            _items.Add("heartMeter", new GameItem(
                sprite: Resources.GetSprite("heartMeter"),
                name: "heartMeter",
                pickUpDialog: "heartMeter",
                count: 1,
                maxCount: 99,
                showAnimation: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetX: 2,
                collectOffsetY: -2
            ));
            _items.Add("heartMeterSilent", new GameItem(
                name: "heartMeter",
                count: 1,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetX: 2,
                collectOffsetY: -2
            ));
            _items.Add("heartMeterFull", new GameItem(
                sprite: Resources.GetSprite("heartMeterFull"),
                name: "heartMeterFull",
                count: 4,
                showAnimation: 1,
                showTime: 1750,
                pickUpDialog: "heartMeterFull",
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 12,
                collectHeight: 12,
                collectOffsetX: 1
            ));

            // Level 1: Tail Key
            _items.Add("dkey1", new GameItem(
                sprite: Resources.GetSprite("dkey1"),
                name: "dkey1",
                pickUpDialog: "dkey1",
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 8,
                collectHeight: 14
            ));
            // Lvel 3: Slime Key
            _items.Add("dkey2", new GameItem(
                sprite: Resources.GetSprite("dkey2"),
                name: "dkey2",
                pickUpDialog: "dkey2",
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 8,
                collectHeight: 14
            ));
            // Level 4: Angler Key
            _items.Add("dkey3", new GameItem(
                sprite: Resources.GetSprite("dkey3"),
                name: "dkey3",
                pickUpDialog: "dkey3",
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 8,
                collectHeight: 14
            ));
            // Level 6: Face Key
            _items.Add("dkey4", new GameItem(
                sprite: Resources.GetSprite("dkey4"),
                name: "dkey4",
                pickUpDialog: "dkey4",
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 8,
                collectHeight: 14
            ));
            // Level 7: Bird Key
            _items.Add("dkey5", new GameItem(
                sprite: Resources.GetSprite("dkey5"),
                name: "dkey5",
                pickUpDialog: "dkey5",
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 8,
                collectHeight: 14
            ));

            _items.Add("guardianAcorn", new GameItem(
                sprite: Resources.GetSprite("guardianAcorn"),
                name: "guardianAcorn",
                pickUpDialog: "guardianAcorn",
                showAnimation: 2,
                soundEffectName: "D360-23-17",
                swordCollect: GameSettings.SwGrabNormal,
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("pieceOfPower", new GameItem(
                sprite: Resources.GetSprite("pieceOfPower"),
                name: "pieceOfPower",
                pickUpDialog: "pieceOfPower",
                showAnimation: 2,
                soundEffectName: "D360-23-17",
                swordCollect: GameSettings.SwGrabNormal,
                collectWidth: 12,
                collectHeight: 12
            ));
            _items.Add("sword1PoP", new GameItem(
                sprite: Resources.GetSprite("pop_sword1"),
                name: "sword1PoP",
                pickUpDialog: "pieceOfPower",
                showAnimation: 2,
                soundEffectName: "D360-23-17"
            ));
            _items.Add("sword2PoP", new GameItem(
                sprite: Resources.GetSprite("pop_sword2"),
                name: "sword2PoP",
                pickUpDialog: "pieceOfPower",
                showAnimation: 2,
                soundEffectName: "D360-23-17"
            ));

            // level: = 0   => item as count
            // level > 0    => item has level
            // else         => item has nothing
            // accessories
            _items.Add("sword1", new GameItem(
                sprite: Resources.GetSprite("sword1"),
                name: "sword1",
                pickUpDialog: "sword1Collected",
                count: 1,
                maxCount: 1,
                level: 1,
                showAnimation: 1,
                equipable: true,
                showEffect: true,
                showTime: 4000,
                collectWidth: 8,
                collectHeight: 16
            ));
            _items.Add("sword2", new GameItem(
                sprite: Resources.GetSprite("sword2"),
                mapSprite: Resources.GetSprite("swordSpawn"),
                name: "sword2",
                count: 1,
                maxCount: 1,
                level: 2,
                showAnimation: 2,
                equipable: true
            ));
            _items.Add("shield", new GameItem(
                sprite: Resources.GetSprite("shield"),
                name: "shield",
                pickUpDialog: "shield_intro",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                level: 1,
                showAnimation: 1,
                equipable: true,
                collectWidth: 8,
                collectHeight: 10
            ));
            _items.Add("shield0", new GameItem(
                sprite: Resources.GetSprite("shield"),
                name: "shield",
                pickUpDialog: "shield",
                soundEffectName: "D370-01-01",
                count: 1,
                maxCount: 1,
                level: 1,
                equipable: true,
                collectWidth: 8,
                collectHeight: 10
            ));
            _items.Add("shieldBack", new GameItem(
                sprite: Resources.GetSprite("shield"),
                name: "shield",
                soundEffectName: "D370-01-01",
                count: 1,
                maxCount: 1,
                level: 1,
                equipable: true,
                swordCollect: GameSettings.SwGrabNormal,
                collectWidth: 8,
                collectHeight: 10
            ));
            _items.Add("mirrorShield", new GameItem(
                sprite: Resources.GetSprite("mirror shield"),
                name: "mirrorShield",
                pickUpDialog: "mirrorShield",
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                count: 1,
                maxCount: 1,
                level: 2,
                showAnimation: 1,
                equipable: true
            ));
            _items.Add("toadstool", new GameItem(
                sprite: Resources.GetSprite("toadstool"),
                mapSprite: Resources.GetSprite("toadstoolMap"),
                name: "toadstool",
                pickUpDialog: "toadstool",
                count: 1,
                maxCount: 1,
                level: -1,
                showAnimation: 1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true,
                swordCollect: GameSettings.SwGrabWorldItem,
                collectWidth: 10,
                collectHeight: 11
            ));
            _items.Add("feather", new GameItem(
                sprite: Resources.GetSprite("feather"),
                name: "feather",
                pickUpDialog: "feather",
                count: 1,
                maxCount: 1,
                level: -1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("stonelifter", new GameItem(
                sprite: Resources.GetSprite("stonelifter0"),
                mapSprite: GameSettings.ClassicSprites ? Resources.GetSprite("stonelifter") : Resources.GetSprite("stonelifter0"),
                name: "stonelifter",
                pickUpDialog: "bracelet0",
                count: 1,
                maxCount: 1,
                level: 1,
                showAnimation: 1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("stonelifter2", new GameItem(
                sprite: Resources.GetSprite("stonelifter1"),
                // base is not supported for different sprites
                name: "stonelifter2",
                pickUpDialog: "bracelet1",
                count: 1,
                maxCount: 1,
                level: 2,
                showAnimation: 1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("pegasusBoots", new GameItem(
                sprite: Resources.GetSprite("pegasusBoots"),
                name: "pegasusBoots",
                pickUpDialog: "pegasusBoots",
                count: 1,
                maxCount: 1,
                level: -1,
                //showAnimation: 1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("shovel", new GameItem(
                sprite: Resources.GetSprite("shovel"),
                name: "shovel",
                pickUpDialog: "shovel",
                count: 1,
                maxCount: 1,
                level: -1,
                showAnimation: 1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("flippers", new GameItem(
                sprite: Resources.GetSprite("flippers"),
                name: "flippers",
                pickUpDialog: "flippers",
                count: 1,
                maxCount: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("magicRod", new GameItem(
                sprite: Resources.GetSprite("magicRod"),
                mapSprite: GameSettings.ClassicSprites ? Resources.GetSprite("magicRodmap") : Resources.GetSprite("magicRod"),
                name: "magicRod",
                pickUpDialog: "magicRod",
                count: 1,
                maxCount: 1,
                level: -1,
                showAnimation: 1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("ocarina", new GameItem(
                sprite: Resources.GetSprite("ocarina"),
                name: "ocarina",
                pickUpDialog: "ocarina",
                count: 1,
                maxCount: 1,
                level: -1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("ocarina_frog", new GameItem(
                sprite: Resources.GetSprite("ocarina"),
                name: "ocarina_frog",
                pickUpDialog: "ocarina_frog_collected",
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("ocarina_maria", new GameItem(
                sprite: Resources.GetSprite("ocarina"),
                name: "ocarina_maria",
                pickUpDialog: "ocarina_maria_collected",
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("ocarina_manbo", new GameItem(
                sprite: Resources.GetSprite("ocarina"),
                name: "ocarina_manbo",
                pickUpDialog: "ocarina_manbo_collected",
                count: 1,
                maxCount: 1,
                showAnimation: 1,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("hookshot", new GameItem(
                sprite: Resources.GetSprite("hookshot"),
                mapSprite: GameSettings.ClassicSprites ? Resources.GetSprite("hookshotmap") : Resources.GetSprite("hookshot"),
                name: "hookshot",
                pickUpDialog: "hookshot",
                count: 1,
                maxCount: 1,
                level: -1,
                showAnimation: 1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));
            _items.Add("boomerang", new GameItem(
                sprite: Resources.GetSprite("boomerang"),
                name: "boomerang",
                pickUpDialog: "boomerang",
                count: 1,
                maxCount: 1,
                level: -1,
                showAnimation: 1,
                equipable: true,
                soundEffectName: "D368-16-10",
                turnDownMusic: true
            ));

            _items.Add("powder", new GameItem(
                sprite: Resources.GetSprite("powder"),
                name: "powder",
                count: 20,
                maxCount: 20,
                level: 0,
                equipable: true,
                soundEffectName: "D370-01-01",
                turnDownMusic: true,
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("powderTrendy", new GameItem(
                sprite: Resources.GetSprite("powder"),
                soundEffectName: "D370-01-01",
                name: "powder",
                pickUpDialog: "powder",
                count: 10,
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("powder_1", new GameItem(
                name: "powder",
                count: 1,
                soundEffectName: "D370-01-01",
                swordCollect: GameSettings.SwGrabNormal,
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("powder_10", new GameItem(
                name: "powder",
                count: 10,
                soundEffectName: "D370-01-01",
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("powderPD", new GameItem(
                name: "powder",
                pickUpDialog: "powder",
                count: 20,
                showAnimation: 2,
                soundEffectName: "D360-01-01",
                turnDownMusic: true,
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("bomb", new GameItem(
                sprite: Resources.GetSprite("bomb"),
                name: "bomb",
                count: 10,
                maxCount: 30,
                level: 0,
                soundEffectName: "D370-01-01",
                equipable: true,
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("bombChest", new GameItem(
                name: "bomb",
                pickUpDialog: "bomb",
                count: 1,
                soundEffectName: "D360-01-01",
                turnDownMusic: true
            ));
            _items.Add("bomb_1", new GameItem(
                name: "bomb",
                count: 1,
                soundEffectName: "D370-01-01",
                swordCollect: GameSettings.SwGrabNormal,
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("bomb_10", new GameItem(
                name: "bomb",
                count: 10,
                soundEffectName: "D370-01-01",
                collectWidth: 8,
                collectHeight: 14
            ));
            _items.Add("bow", new GameItem(
                sprite: Resources.GetSprite("bow"),
                name: "bow",
                count: 10,
                maxCount: 30,
                level: 0,
                equipable: true
            ));
            _items.Add("arrow", new GameItem(
                sprite: Resources.GetSprite("arrow"),
                name: "arrow",
                count: 10,
                soundEffectName: "D370-01-01"
            ));
            _items.Add("arrow_1", new GameItem(
                sprite: Resources.GetSprite("arrow"),
                name: "arrow",
                count: 1,
                soundEffectName: "D370-01-01",
                swordCollect: GameSettings.SwGrabNormal
            ));

            _items.Add("cloakRed", new GameItem(
                sprite: Resources.GetSprite("cloak"),
                name: "cloakRed",
                pickUpDialog: "cloak_red",
                count: 1,
                maxCount: 1,
                level: -1,
                showAnimation: 1,
                soundEffectName: "D360-01-01",
                turnDownMusic: true
            ));
            _items.Add("cloakBlue", new GameItem(
                sprite: Resources.GetSprite("cloak"),
                name: "cloakBlue",
                pickUpDialog: "cloak_blue",
                count: 1,
                maxCount: 1,
                level: -1,
                showAnimation: 1,
                soundEffectName: "D360-01-01",
                turnDownMusic: true
            ));
        }
    }
}
