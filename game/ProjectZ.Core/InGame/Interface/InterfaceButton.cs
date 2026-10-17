using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Pages;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Interface
{
    public class InterfaceButton : InterfaceElement
    {
        public InterfaceElement InsideElement;
        public InterfaceLabel InsideLabel;

        public delegate void BFunction(InterfaceElement element);
        public BFunction ClickFunction;

        public string LabelKey;
        public string GameSetting;

        // Backup colors for when buttons are disabled/enabled.
        public Color Backup_Color;
        public Color Backup_SelectionColor;

        public Color Backup_Color_Disabled;
        public Color Backup_SelectionColor_Disabled;

        // Values configurable via lahdmod.
        private int custom_button_color_red           = 40;
        private int custom_button_color_grn           = 64;
        private int custom_button_color_blu           = 128;
        private int custom_button_select_red          = 90;
        private int custom_button_select_grn          = 110;
        private int custom_button_select_blu          = 170;
        private int custom_button_color_disabled_red  = 100;
        private int custom_button_color_disabled_grn  = 100;
        private int custom_button_color_disabled_blu  = 100;
        private int custom_button_select_disabled_red = 150;
        private int custom_button_select_disabled_grn = 150;
        private int custom_button_select_disabled_blu = 150;

        public InterfaceButton()
        {
            // Try to load a lahdmod to get a custom set of colors.
            string modFile = Path.Combine(Values.PathLAHDMods, "InterfaceButton.lahdmod");
            ModFile.Parse(modFile, this);

            // Load in whatever the colors are now.
            Color = new Color(custom_button_color_red, custom_button_color_grn, custom_button_color_blu);
            SelectionColor = new Color(custom_button_select_red, custom_button_select_grn, custom_button_select_blu);

            // Backup colors for when interfaces get toggled.
            Backup_Color = Color;
            Backup_SelectionColor = SelectionColor;

            // Backup colors for when interfaces get disabled.
            Backup_Color_Disabled = new Color(custom_button_color_disabled_red, custom_button_color_disabled_grn, custom_button_color_disabled_blu);
            Backup_SelectionColor_Disabled = new Color(custom_button_select_disabled_red, custom_button_select_disabled_grn, custom_button_select_disabled_blu);

            Selectable = true;
        }

        public InterfaceButton(Point size, Point margin, InterfaceElement insideElement, BFunction clickFunction) : this()
        {
            Size = size;
            Margin = margin;
            InsideElement = insideElement;
            ClickFunction = clickFunction;
        }

        public InterfaceButton(Point size, Point margin, string text, BFunction clickFunction) : this()
        {
            Size = size;
            Margin = margin;

            // By setting this to a public field we can easily access it's internals directly.
            InsideLabel = new InterfaceLabel(text, size, Point.Zero);

            InsideElement = InsideLabel;
            ClickFunction = clickFunction;
        }

        public InterfaceButton(Point size, Point margin, string labelKey, string gameSetting, BFunction clickFunction) : this()
        {
            Size = size;
            Margin = margin;

            // The actual label will be set via override text. 
            InsideLabel = new InterfaceLabel("", size, Point.Zero);

            LabelKey = labelKey;
            InsideElement = InsideLabel;
            ClickFunction = clickFunction;
            GameSetting = gameSetting;
        }

        public override void Select(Directions direction, bool animate)
        {
            InsideElement?.Select(direction, animate);

            base.Select(direction, animate);
        }

        public override void Deselect(bool animate)
        {
            InsideElement?.Deselect(animate);

            base.Deselect(animate);
        }

        public override InputEventReturn PressedButton(CButtons pressedButton)
        {
            // HACK: Allow pressing "Start" to select a save file and only save files.
            var currentPage = Game1.UiPageManager.GetCurrentPage();
            var buttonText = InsideLabel?.Text;

            // The hack should only be applied to the main menu page so check for that.
            if (currentPage != null && currentPage.GetType() == typeof(MainMenuPage))
            {
                // Only allow Start to select the save file and not the other buttons.
                if (pressedButton == CButtons.Start && (buttonText == "Settings" || buttonText == "Quit"))
                    return InputEventReturn.Nothing;

                // All that remains is the save file selection.
                if (pressedButton != CButtons.Start && pressedButton != ControlHandler.ConfirmButton)
                    return InputEventReturn.Nothing;
            }
            // In every other case only allow the confirm button to click a button.
            else if (pressedButton != ControlHandler.ConfirmButton)
                return InputEventReturn.Nothing;

            // A function has been assigned to the button.
            if (ClickFunction != null)
            {
                // Play a sound effect in most cases, except when it's these buttons.
                if (buttonText != "Back" && buttonText != "Return to Game")
                    Game1.AudioManager.PlaySoundEffect("D360-19-13");

                // Run the click function and return input did something.
                ClickFunction(this);
                return InputEventReturn.Something;
            }
            // Since this is constantly checked in a loop, return nothing most of the time.
            return InputEventReturn.Nothing;
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency)
        {
            base.Draw(spriteBatch, drawPosition, scale, transparency);

            // draw the embedded element
            InsideElement?.Draw(spriteBatch, drawPosition, scale, transparency);
        }
    }
}
