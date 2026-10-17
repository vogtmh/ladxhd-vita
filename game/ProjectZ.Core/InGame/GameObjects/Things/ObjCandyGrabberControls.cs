using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    class ObjCandyGrabberControls : GameObject
    {
        private readonly Rectangle _sourceTop;
        private readonly Rectangle _sourceButton0;
        private readonly Rectangle _sourceButton1;

        private readonly Box _collisionBox;

        private int _buttonFrame;
        private int _animationSpeed = 100;

        public static string ControlsTexture 
        {
            get
            {
                bool swap = ControlHandler.ReverseTrendyLabels ^ GameSettings.SwapButtons;

                return (GameSettings.Controller, swap) switch
                {
                    ("XBox",  false)       => "candy_grabber_controls_top",
                    ("XBox",  true)        => "candy_grabber_controls_top_rev",

                    ("Nintendo", false)    => "candy_grabber_controls_top_rev",
                    ("Nintendo", true)     => "candy_grabber_controls_top",

                    ("Playstation", false) => "candy_grabber_controls_psx_rev",
                    ("Playstation", true)  => "candy_grabber_controls_psx",

                    _                      => "candy_grabber_controls_top"
                };
            }
        }

        public ObjCandyGrabberControls() : base(ControlsTexture) { }

        public ObjCandyGrabberControls(Map.Map map, int posX, int posY) : base(map)
        {
            _sourceTop = Resources.SourceRectangle(ControlsTexture);
            _sourceButton0 = Resources.SourceRectangle("candy_grabber_controls_button_0");
            _sourceButton1 = Resources.SourceRectangle("candy_grabber_controls_button_1");

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _collisionBox = new Box(posX + 6, posY + 32, 0, 4, 10, 8);

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerBottom, EntityPosition));

            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public void Update()
        {
            _buttonFrame = ((int)Game1.TotalGameTime % (2 * _animationSpeed)) / _animationSpeed;

            // Check if the player has paid and is standing in the correct location.
            var allowedToStartGame = MapManager.ObjLink.Body.BodyBox.Box.Intersects(_collisionBox) && Game1.GameManager.SaveManager.GetString("can_play") == "1";
            Game1.GameManager.SaveManager.SetBool("trendy_ready", allowedToStartGame);

            // When the crane is used, disable the NPC's interaction box.
            if (allowedToStartGame) 
                Game1.GameManager.SaveManager.SetString("trendy_npc", "1");   
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // draw the top
            spriteBatch.Draw(Resources.SprObjects, EntityPosition.Position + new Vector2(1, 1), _sourceTop, Color.White);

            // left button
            var pressFirstButton = Game1.GameManager.SaveManager.GetString("trendy_button_1") == "1";
            spriteBatch.Draw(Resources.SprObjects, EntityPosition.Position + new Vector2(1, 17),
                pressFirstButton && _buttonFrame == 1 ? _sourceButton1 : _sourceButton0, Color.White);

            // right button
            var pressSecondButton = Game1.GameManager.SaveManager.GetString("trendy_button_2") == "1";
            spriteBatch.Draw(Resources.SprObjects, EntityPosition.Position + new Vector2(9, 17),
                pressSecondButton && _buttonFrame == 1 ? _sourceButton1 : _sourceButton0, Color.White);
        }
    }
}
