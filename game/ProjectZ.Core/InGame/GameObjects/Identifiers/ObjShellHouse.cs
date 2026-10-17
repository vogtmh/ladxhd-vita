using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Identifiers
{
    internal class ObjShellHouse : GameObject
    {
        private readonly Animator _barAnimator;
        private readonly DictAtlasEntry _barSprite;

        private bool _triggerEntryDialog;
        private bool _triggerDialog;

        private float _barHeight = 16;
        private int _shellCount;
        private int _PresentCount;
        private int _targetHeight;
        private int _SaveFileVersion;
        private bool _fillBar;

        private float _soundCounter;
        private float _particleCounter = 1250;
        private bool _particle;

        private float _spawnCounter = 300;
        private bool _spawnPresent;
        private bool _spawnSword;

        public ObjShellHouse() : base("shell_bar") { }

        public ObjShellHouse(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX, posY + 16, 0);
            EntitySize = new Rectangle(0, -16, 16, 16);

            // already collected the sword
            if (Game1.GameManager.SaveManager.GetString("hasSword2") == "1")
            {
                IsDead = true;
                return;
            }
            _barSprite = Resources.GetSprite("shell_bar");
            _barAnimator = AnimatorSaveLoad.LoadAnimator("Objects/shell_mansion_bar");

            var shellPresentString = Game1.GameManager.SaveManager.GetString("shell_presents");
            var savedVersionString = Game1.GameManager.SaveManager.GetString("save_version");

            int.TryParse(shellPresentString, out int PresentsAsInt);
            int.TryParse(savedVersionString, out int SaveFileAsInt);

            _PresentCount = PresentsAsInt;
            _SaveFileVersion = SaveFileAsInt;

            var objShells = Game1.GameManager.GetItem("shell");
            if (objShells != null)
            {
                // Prevent the bar from overflowing when hitting 20 shells.
                _shellCount = MathHelper.Min(objShells.Count, 20);
                _targetHeight = 16;

                if (GameSettings.Unmissables && _SaveFileVersion >= 1)
                {
                    if (_PresentCount == 0)
                        _targetHeight += (int)(MathHelper.Min(_shellCount, 5) / 5f * 32);
                    else if (_PresentCount < 2)
                        _targetHeight += (int)(MathHelper.Min(_shellCount, 10) / 5f * 32);
                    else
                    {
                        _targetHeight += (int)(MathHelper.Min(_shellCount, 10) / 5f * 32);
                        _targetHeight += (int)MathHelper.Max(0, (_shellCount - 10) / 10f * 32);
                    }
                }
                else
                {
                    _targetHeight += (int)(MathHelper.Min(_shellCount, 10) / 5f * 32);
                    _targetHeight += (int)MathHelper.Max(0, (_shellCount - 10) / 10f * 32);
                }
            }
            if (objShells == null || objShells.Count == 0)
            {
                _triggerDialog = true;
                _targetHeight = 0;
            }
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerBottom, EntityPosition));
        }

        private void Update()
        {
            // Check for the player hitting the trigger point and start the count.
            var playerDistance = EntityPosition.Position - MapManager.ObjLink.Position;
            if (!_triggerEntryDialog && playerDistance.X < 105)
            {
                _triggerEntryDialog = true;
                Game1.GameManager.StartDialogPath("shell_mansion_entry");
            }

            // The player walked in far enough to trigger the bar.
            if (!_triggerDialog && playerDistance.X < 66)
            {
                _fillBar = true;
                _triggerDialog = true;
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
            }

            // Start filling the bar.
            if (_fillBar)
            {
                // Disable the 2D move hack and freeze the player.
                MapManager.ObjLink.FreezePlayer();
                MapManager.ObjLink.DisableInventory(true);
                MapManager.ObjLink.DisableDirHack2D = true;

                // Play the counting up sound at random intervals.
                _soundCounter -= Game1.DeltaTime;
                if (_soundCounter < 0)
                {
                    _soundCounter += Game1.RandomNumber.Next(50, 200);;
                    Game1.AudioManager.PlaySoundEffect("D370-06-06");
                }

                // 2sec -> 16px
                // 2000 / 16 = 125ms
                var addValue = Game1.DeltaTime / 125 * 2;
                if (_targetHeight > _barHeight + addValue)
                {
                    _barHeight += addValue;
                }
                else
                {
                    _fillBar = false;
                    _barHeight = _targetHeight;

                    _particle = true;

                    if (_shellCount >= 20 && (!GameSettings.Unmissables || _SaveFileVersion < 1 || _PresentCount >= 2))
                        _barAnimator.Play("idle");

                    bool PlaySound = _shellCount == 5 || 
                                     _shellCount == 10 || 
                                     _shellCount >= 20;

                    if (GameSettings.Unmissables && _SaveFileVersion >= 1)
                         PlaySound = _shellCount >= 5 && _PresentCount == 0 || 
                                     _shellCount >= 10 && _PresentCount < 2 || 
                                     _shellCount >= 20;

                    if (PlaySound)
                        Game1.AudioManager.PlaySoundEffect("D360-02-02");
                    else
                        Game1.AudioManager.PlaySoundEffect("D360-29-1D");

                    var objParticle0 = new ObjAnimator(Map, 0, 0, 0, 0, Values.LayerPlayer, "Particles/shell_mansion_particle", "idle", true);
                    objParticle0.Animator.CurrentAnimation.LoopCount = 1;
                    objParticle0.EntityPosition.Set(new Vector2((int)EntityPosition.X - 8, (int)EntityPosition.Y - (int)_barHeight + 7));
                    Map.Objects.SpawnObject(objParticle0);

                    var objParticle1 = new ObjAnimator(Map, 0, 0, 0, 0, Values.LayerPlayer, "Particles/shell_mansion_particle", "idle", true);
                    objParticle1.Animator.CurrentAnimation.LoopCount = 1;
                    objParticle1.EntityPosition.Set(new Vector2((int)EntityPosition.X + 16 + 8, (int)EntityPosition.Y - (int)_barHeight + 7));
                    Map.Objects.SpawnObject(objParticle1);
                }
            }

            // The meter has finished counting up.
            else if (_particle)
            {
                MapManager.ObjLink.FreezePlayer();

                // Wait until timer is finished.
                if (_particleCounter > 0)
                    _particleCounter -= Game1.DeltaTime;
                else
                {
                    _particle = false;

                    bool SpawnShell = _shellCount == 5 || 
                                      _shellCount == 10;

                    if (GameSettings.Unmissables && _SaveFileVersion >= 1)
                         SpawnShell = _shellCount >= 5 && _PresentCount == 0 || 
                                      _shellCount >= 10 && _PresentCount < 2;

                    // If a shell is spawned then show an explosion effect.
                    if (SpawnShell)
                    {
                        _spawnPresent = true;

                        Game1.AudioManager.PlaySoundEffect("D378-12-0C");

                        var objExplosion = new ObjAnimator(Map, 0, 0, 0, 0, Values.LayerBottom, "Particles/explosionBomb", "run", true);
                        objExplosion.EntityPosition.Set(new Vector2((int)EntityPosition.X - 48, (int)EntityPosition.Y - 64));
                        Map.Objects.SpawnObject(objExplosion);
                    }
                    // If more than 20 shells were collected spawn the sword.
                    else if (_shellCount >= 20)
                    {
                        Game1.GameManager.StartDialogPath("shell_mansion_sword");
                        _spawnSword = true;
                    }
                    // Spawn the "not enough shells" message.
                    else
                    {
                        Game1.GameManager.StartDialogPath("shell_mansion_nothing");
                    }
                }
            }
            else
            {
                // If the sword is spawned don't disable the direction hack as "ObjSwordSpawner" will handle it.
                if (!_spawnSword)
                {
                    MapManager.ObjLink.DisableInventory(false);
                    MapManager.ObjLink.DisableDirHack2D = false;
                }
            }
            // Spawn a shell preset if enough shells were collected.
            if (_spawnPresent)
            {
                if (_spawnCounter > 0)
                    _spawnCounter -= Game1.DeltaTime;
                else
                {
                    _spawnPresent = false;
                    var objItem = new ObjItem(Map, 0, 0, null, null, "shellPresent", null);
                    objItem.EntityPosition.Set(new Vector2((int)EntityPosition.X - 48, (int)EntityPosition.Y - 56));
                    Map.Objects.SpawnObject(objItem);
                }
            }
            // Update the bar animation.
            if (_barAnimator.IsPlaying)
                _barAnimator.Update();
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // draw the animated bar
            if (_barAnimator.IsPlaying)
            {
                for (int i = 1; i < 8; i++)
                    _barAnimator.Draw(spriteBatch, new Vector2(EntityPosition.X, EntityPosition.Y - 16 * i), Color.White);
            }
            else
            {
                spriteBatch.Draw(_barSprite.Texture, new Rectangle((int)EntityPosition.X, (int)EntityPosition.Y - (int)_barHeight, 16, (int)_barHeight), _barSprite.ScaledRectangle, Color.White);
            }
        }
    }
}
