using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.Graphics;

namespace ProjectZ.InGame.Things
{
    public class GameItem
    {
        public readonly DictAtlasEntry Sprite;       // If set, the sprite shown in the game world and inventory unless "mapSprite" is set.
        public readonly DictAtlasEntry MapSprite;    // If set, "sprite" is used for inventory and "mapSprite" is used for game world. 
        public readonly Rectangle? SourceRectangle;
        public readonly bool AnimateSprite;
        public readonly string Name;
        public readonly string PickUpDialog;
        public readonly string SoundEffectName;
        public readonly int MusicName;
        public readonly bool TurnDownMusic;
        public readonly int Level;
        public readonly int Count;
        public readonly int MaxCount;
        public readonly int DrawLength;
        public readonly bool Instrument;
        public readonly bool TradeItem;
        public readonly bool Equipable;
        public readonly bool ShowEffect;
        public readonly bool SwordCollect;
        public readonly int ShowAnimation;
        public readonly int ShowTime;
        public readonly int CollectWidth;
        public readonly int CollectHeight;
        public readonly int CollectOffsetX;
        public readonly int CollectOffsetY;

        public GameItem(
            DictAtlasEntry sprite = null,
            DictAtlasEntry mapSprite = null,
            bool animateSprite = false,
            string name = null,
            string pickUpDialog = null,
            string soundEffectName = null,
            int musicName = -1,
            bool turnDownMusic = false,
            int level = 0,
            int count = 0,
            int maxCount = 0,
            int drawLength = 2,
            bool instrument = false,
            bool tradeItem = false,
            bool equipable = false,
            bool showEffect = false,
            bool swordCollect = false,
            int showAnimation = 0,
            int showTime = 250,
            int collectWidth = -1,
            int collectHeight = -1,
            int collectOffsetX = 0,
            int collectOffsetY = 0)
        {
            Sprite = sprite;
            MapSprite = mapSprite;
            AnimateSprite = animateSprite;
            Name = name;
            PickUpDialog = pickUpDialog;
            SoundEffectName = soundEffectName;
            MusicName = musicName;
            TurnDownMusic = turnDownMusic;
            Level = level;
            Count = count;
            MaxCount = maxCount;
            DrawLength = drawLength;
            Instrument = instrument;
            TradeItem = tradeItem;
            Equipable = equipable;
            ShowEffect = showEffect;
            SwordCollect = swordCollect;
            ShowAnimation = showAnimation;
            ShowTime = showTime;
            CollectWidth = collectWidth;
            CollectHeight = collectHeight;
            CollectOffsetX = collectOffsetX;
            CollectOffsetY = collectOffsetY;

            if (sprite != null)
                SourceRectangle = sprite.SourceRectangle;
        }

        public Rectangle CreateCollectRectangle(Rectangle sourceRectangle)
        {
            var width = CollectWidth > 0 ? CollectWidth : sourceRectangle.Width + 2;
            var height = CollectHeight > 0 ? CollectHeight : Math.Min(sourceRectangle.Height, 12);
            return new Rectangle(-sourceRectangle.Width / 2 + CollectOffsetX, -height + CollectOffsetY, width, height);
        }
    }

    public class GameItemCollected
    {
        public string Name;
        public string LocationBounding;
        public int Count;

        public GameItemCollected(string name)
        {
            Name = name;
        }
    }
}
