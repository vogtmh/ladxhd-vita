using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    class ObjGraveTrigger : GameObject
    {
        private readonly int[] _correctDirection = { 3, 0, 1, 2, 1 };
        private readonly string _triggerKey;

        public int CurrentState;

        public ObjGraveTrigger() : base("editor grave trigger") { }

        public ObjGraveTrigger(Map.Map map, int posX, int posY, string triggerKey) : base(map)
        {
            Tags = Values.GameObjectTag.Utility;

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _triggerKey = triggerKey;

            if (string.IsNullOrEmpty(_triggerKey))
            {
                IsDead = true;
                return;
            }

            Game1.GameManager.SaveManager.SetString(_triggerKey, "0");

            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
        }

        private void OnKeyChange()
        {
            var reset = true;

            // Loop through each gravestone.
            for (var i = 0; i < 5; i++)
            {
                // Get the current state for each gravestone push direction.
                var strKey = Game1.GameManager.SaveManager.GetString("ow_grave_" + i + "_dir");

                // Check if the direction has been set.
                if (!string.IsNullOrEmpty(strKey) && strKey != "-1")
                {
                    // The player must have have the second instrument.
                    var theInstrument = Game1.GameManager.GetItem("instrument1");
                    var hasInstrument = theInstrument != null && theInstrument.Count > 0;

                    // Check if the player has a follower with them.
                    var hasBowWow  = Game1.GameManager.SaveManager.GetString("has_bowWow", "0") == "1";
                    var hasMarin   = Game1.GameManager.SaveManager.GetString("has_marin", "0") == "1";
                    var hasGhost   = Game1.GameManager.SaveManager.GetString("has_ghost", "0") == "1";
                    var hasRooster = Game1.GameManager.SaveManager.GetString("has_rooster", "0") == "1";
                    var hasFollower = hasBowWow || hasMarin || hasGhost || hasRooster;

                    // Disable reset.
                    reset = false;

                    // Player moved the next gravestone in the correct direction.
                    if (_correctDirection[i].ToString() == strKey)
                    {
                        // If the current state is equal to the gravestone pushed.
                        if (CurrentState == i)
                        {
                            // Increment the current state.
                            CurrentState++;

                            // If it was the final gravestone that was pushed and the player does not have a follower.
                            if (CurrentState == 5 && !hasFollower && hasInstrument)
                            {
                                // Spawn the entrance to the Color dungeon.
                                Game1.GameManager.SaveManager.SetString(_triggerKey, "1");

                                // remove the object
                                Map.Objects.DeleteObjects.Add(this);
                            }
                        }
                    }
                    // Not the correct gravestone moved or in the wrong direction.
                    else
                        CurrentState = 5;
                }
            }
            // If reset is still true then set state back to zero.
            if (reset)
                CurrentState = 0;
        }
    }
}
