using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjChest : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly ObjSprite _spriteFront;
        private readonly CSprite _spriteBack;
        private readonly GameObject _spawnObject;
        private readonly CarriableComponent _carriableComponent;

        private ObjSprite _itemSprite;
        private GameItem _item;

        private readonly string _itemName;
        private readonly string _locationBound;
        public readonly string ItemKey;
        private readonly string _dialogPath;

        // @INFO: this time is also used in the "seashell" script and should be changed there also
        private const int FadeTime = 175;
        private const int MoveTime = 300;

        private bool _opened;
        private bool _isActive = true;

        public override bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                _spriteFront.IsActive = value;

                // this is needed when the last shell in a chest is found because the chest will get deactivated
                if (_itemSprite != null)
                    _itemSprite.Sprite.Color = Color.Transparent;

                CheckOpened();
            }
        }


        public ObjChest() : base("chest") { }

        public ObjChest(Map.Map map, int posX, int posY, string itemName, string itemBounding, string itemKey, int spriteType, bool hitMode) : base(map)
        {
            FreezePersist = true;
            EntityPosition = new CPosition(posX, posY + 13, 0);
            EntitySize = new Rectangle(0, -13, 16, 16);

            _itemName = itemName;
            _locationBound = itemBounding;
            ItemKey = itemKey;

            var openingTrigger = new AiTriggerCountdown(MoveTime, OpeningTick, OpeningEnd);
            var fadingTrigger = new AiTriggerCountdown(FadeTime, FadeTick, FadeEnd);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("closed", new AiState());
            _aiComponent.States.Add("opening", new AiState { Init = InitOpen, Trigger = { openingTrigger } });
            _aiComponent.States.Add("opened", new AiState { Init = InitOpen });
            _aiComponent.States.Add("textbox", new AiState(UpdateTextBox));
            _aiComponent.States.Add("fading", new AiState { Trigger = { fadingTrigger } });
            _aiComponent.ChangeState("closed");

            CRectangle grabBox = new CRectangle(EntityPosition, new Rectangle(1, 2 - 13, 14, 13));

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(grabBox, null, null, null) { IsCollision = true });
            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(new CBox(posX, posY + 3, 0, 16, 11, 12), Values.CollisionTypes.Normal | Values.CollisionTypes.Hookshot));

            if (!hitMode)
                AddComponent(InteractComponent.Index, new InteractComponent(new CBox(posX + 4, posY + 3, 0, 8, 13, 16), Interact));
            else
                AddComponent(HittableComponent.Index, new HittableComponent(new CBox(posX + 4, posY + 3, 0, 8, 13, 16), OnHit));
            
            _spriteBack = new CSprite("chest_back", new CPosition(posX, posY + 12.9f, 0), new Vector2(0, -12.9f));
            _spriteBack.SourceRectangle.X += spriteType * 32;
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_spriteBack, Values.LayerPlayer));

            // sprite front
            _spriteFront = new ObjSprite(map, posX, posY + 13, "chest_front", Vector2.Zero, Values.LayerPlayer, null);
            _spriteFront.Sprite.SourceRectangle.X += spriteType * 32;
            Map.Objects.SpawnObject(_spriteFront);

            // check if the chest was already opened
            if (!CheckOpened())
            {
                if (_itemName == "greenZol")
                {
                    var greenZol = new EnemyGreenZol(Map, posX, posY, 8, false);
                    greenZol.SpawnDelay();
                    _spawnObject = greenZol;
                }
                else if (_itemName != null && _itemName.StartsWith("dialog:"))
                {
                    _dialogPath = _itemName.Remove(0, 7);
                }
                else
                {
                    if (_itemName == null)
                        IsDead = true;

                    _item = Game1.GameManager.ItemManager[_itemName];

                    if (_item == null)
                        IsDead = true;
                }
            }
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType type, int damage, bool pieceOfPower)
        {
            if ((type & HitType.ThrownObject) != 0)
            {
                OpenChest();
                return Values.HitCollision.Blocking;
            }

            return Values.HitCollision.None;
        }

        private bool CheckOpened()
        {
            if (!string.IsNullOrEmpty(ItemKey) && Game1.GameManager.SaveManager.GetString(ItemKey) == "1")
            {
                _aiComponent.ChangeState("opened");
                return true;
            }

            return false;
        }

        private void CreateItem()
        {
            _item = Game1.GameManager.ItemManager[_itemName];

            Rectangle itemSource;

            if (_item.MapSprite != null)
                itemSource = _item.MapSprite.ScaledRectangle;
            else if (_item.SourceRectangle.HasValue)
                itemSource = _item.SourceRectangle.Value;
            else
            {
                var baseItem = Game1.GameManager.ItemManager[_item.Name];
                itemSource = baseItem.SourceRectangle.Value;
            }
            _itemSprite = new ObjSprite(Map, 0, 0, Resources.SprItem, itemSource, new Vector2(0, -itemSource.Height + 1), Values.LayerPlayer);
            _itemSprite.EntityPosition.Set(new Vector2(EntityPosition.X + 8 - itemSource.Width / 2f, EntityPosition.Y - 0.05f));
            _itemSprite.Sprite.Color = Color.Transparent;

            Map.Objects.SpawnObject(_itemSprite);
        }

        private void InitOpen()
        {
            if (_opened)
                return;

            _opened = true;
            _spriteBack.SourceRectangle.X += 16;
            _spriteFront.Sprite.SourceRectangle.X += 16;
        }

        private void OpeningTick(double tick)
        {
            MapManager.ObjLink.FreezePlayer();
            MapManager.ObjLink.FreezeWorldForEvents = true;
            _itemSprite.EntityPosition.Z = (float)Math.Sin((float)(MoveTime - tick) / MoveTime * Math.PI / 1.55f) * 12;
        }

        private void OpeningEnd()
        {
            OpeningTick(0);
            PickUpItem();
        }

        private void PickUpItem()
        {
            _aiComponent.ChangeState("textbox");

            var collectedItem = new GameItemCollected(_itemName)
            { Count = _item.Count, LocationBounding = _locationBound };
            MapManager.ObjLink.PickUpItem(collectedItem, true);

            SetKey();
        }

        private void UpdateTextBox()
        {
            // don't fade away if the item is shown
            if (_item.ShowAnimation != 0)
                FadeEnd();
            else
                _aiComponent.ChangeState("fading");
        }

        private void FadeTick(double time)
        {
            _itemSprite.Sprite.Color = Color.White * (float)(time / FadeTime);
        }

        private void FadeEnd()
        {
            _itemSprite.Sprite.Color = Color.Transparent;
            _aiComponent.ChangeState("opened");
        }

        private void SpawnObject()
        {
            // spawn the object
            Map.Objects.SpawnObject(_spawnObject);
        }

        private void SetKey()
        {
            if (!string.IsNullOrEmpty(ItemKey))
                Game1.GameManager.SaveManager.SetString(ItemKey, "1");
        }

        private void TryEarnAchievement()
        {
            // We're gonna use this a lot.
            var saveManager = Game1.GameManager.SaveManager;

            //-------------------------------------------------------------------
            // Dream Shrine Achievement:
            // Grab both chests without taking damage.
            //-------------------------------------------------------------------
            if (!string.IsNullOrEmpty(saveManager.GetString("dream_shrine_achievement")))
            {
                // Exit if it's not the correct two chests.
                if (ItemKey != "shrine_r100" && ItemKey != "shrine_ocarina")
                    return;

                // Get the current value and add one to it.
                int dreamAchieveCount = saveManager.GetInt("dream_shrine_count", 0) + 1;

                // Increment the counter for the next check.
                saveManager.SetInt("dream_shrine_count", dreamAchieveCount);

                // If the two chests were opened without taking damage.
                if (dreamAchieveCount == 2)
                {
                    AchievementManager.Earn(45);
                    saveManager.RemoveInt("dream_shrine_count");
                    saveManager.RemoveString("dream_shrine_achievement");
                }
            }

            //-------------------------------------------------------------------
            // Tal Tal Heights 5 Chest Challenge:
            // Must open all five chests for the achievement.
            //-------------------------------------------------------------------
            if (ItemKey == "cave27_chest")
            {
                // Get the current value and add one to it.
                int caveAchieveCount = saveManager.GetInt("cave27_achievement", 0) + 1;

                // Increment the counter for the next check.
                saveManager.SetInt("cave27_achievement", caveAchieveCount);

                // If all five chests were opened earn the achievement.
                if (caveAchieveCount == 5)
                {
                    AchievementManager.Earn(85);
                    saveManager.RemoveInt("cave27_achievement");
                }
            }
        }

        private bool OpenChest()
        {
            if (_aiComponent.CurrentStateId != "closed")
                return false;

            Game1.AudioManager.PlaySoundEffect("D378-04-04");

            // spawn object
            if (_spawnObject != null)
            {
                Game1.AudioManager.PlaySoundEffect("D360-29-1D");

                _aiComponent.ChangeState("opened");
                SpawnObject();
                SetKey();
            }
            // show dialog
            else if (_dialogPath != null)
            {
                _aiComponent.ChangeState("opened");
                Game1.GameManager.StartDialogPath(_dialogPath);
                SetKey();
            }
            // spawn item
            else
            {
                CreateItem();
                _aiComponent.ChangeState("opening");
                _itemSprite.Sprite.Color = Color.White;
            }
            // Some acheivements involve chests.
            TryEarnAchievement();

            return true;
        }

        private bool Interact()
        {
            // only open if the player is facing up and the chest is closed
            if (MapManager.ObjLink.Direction != 1)
                return false;

            return OpenChest();
        }
    }
}
