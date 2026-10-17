using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Controls
{
    public class ControlHandler
    {
        public static Dictionary<CButtons, ButtonMapper> ButtonDictionary = new Dictionary<CButtons, ButtonMapper>();

        public static CButtons DebugButtons;
        public static CButtons ConfirmButton = CButtons.A;
        public static CButtons CancelButton  = CButtons.B;

        public static bool LastKeyboardDown;
        public static bool LastDirectionDPad;

        private const int ScrollStartTime = 500;
        private const int ScrollTime = 125;

        private static float _scrollCounter;
        private static bool _initDirection;

        public static string[] ControllerNames;
        public static string[,] ControllerLabels;
        public static int ControllerIndex = 0;

        // Values configurable via lahdmod.
        private static bool reverse_trendy_labels  = false;
        private static bool reverse_teleport_labels = false;
        private static bool reverse_settings_labels = false;

        // Global references to lahdmod values.
        public static bool ReverseTrendyLabels => reverse_trendy_labels;
        public static bool ReverseTeleportLabels => reverse_teleport_labels;
        public static bool ReverseSettingsLabels => reverse_settings_labels;

        public static void Initialize()
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ControlHandler.lahdmod");
            ModFile.ParseStatic(modFile, typeof(ControlHandler));

            // Set up the initial keyboard controller style.
            SetControlStyle1();

            // A simple array to reference the names by index.
            ControllerNames = new string[]{ "XBox", "Nintendo", "Playstation" };

            // A rectangular 2D array is used to easily reference the button labels.
            ControllerLabels = new string[,] 
            { 
                { "A", "B", "X", "Y", "LB", "RB", "LT", "RT",   "Back",   "Start" },  // XBox
                { "B", "A", "Y", "X",  "L",  "R", "ZL", "ZR", "Select",   "Start" },  // Nintendo
                { "Χ", "Ο", "Γ", "Δ", "L1", "R1", "L2", "R2",  "Share", "Options" }   // Playstation
            }; 
        }

        public static void SetControllerIndex()
        {
            // Controller index is set when loading a save file or when switching a controller.
            ControllerIndex = Array.IndexOf(ControllerNames, GameSettings.Controller);
        }

        private static readonly Dictionary<string, int> ButtonIndexMap = new()
        {
            // This serves as a lookup table to translate the MonoGame button name to the 
            // selected controller. The value references the button position in the 2D array.
            ["A"]             = 0,
            ["B"]             = 1,
            ["X"]             = 2,
            ["Y"]             = 3,
            ["LeftShoulder"]  = 4,
            ["RightShoulder"] = 5,
            ["LeftTrigger"]   = 6,
            ["RightTrigger"]  = 7,
            ["Back"]          = 8,
            ["Start"]         = 9
        };
        
        public static string GetButtonName(Buttons button)
        {
            // This method should be used anywhere a button name is displayed in-game.
            string buttonName = button.ToString();

            return ButtonIndexMap.TryGetValue(buttonName, out int index)
                ? ControllerLabels[ControllerIndex, index]
                : buttonName;
        }

        public static void SetControlStyle1()
        {
            ButtonDictionary.Clear();
            ButtonDictionary.Add(CButtons.Up, new ButtonMapper(new[] { Keys.Up }, new[] { Buttons.DPadUp }));
            ButtonDictionary.Add(CButtons.Down, new ButtonMapper(new[] { Keys.Down }, new[] { Buttons.DPadDown }));
            ButtonDictionary.Add(CButtons.Left, new ButtonMapper(new[] { Keys.Left }, new[] { Buttons.DPadLeft }));
            ButtonDictionary.Add(CButtons.Right, new ButtonMapper(new[] { Keys.Right }, new[] { Buttons.DPadRight }));
            ButtonDictionary.Add(CButtons.A, new ButtonMapper(new[] { Keys.X }, new[] { Buttons.A }));
            ButtonDictionary.Add(CButtons.B, new ButtonMapper(new[] { Keys.C }, new[] { Buttons.B }));
            ButtonDictionary.Add(CButtons.X, new ButtonMapper(new[] { Keys.Z }, new[] { Buttons.X }));
            ButtonDictionary.Add(CButtons.Y, new ButtonMapper(new[] { Keys.S }, new[] { Buttons.Y }));
            ButtonDictionary.Add(CButtons.LB, new ButtonMapper(new[] { Keys.A }, new[] { Buttons.LeftShoulder }));
            ButtonDictionary.Add(CButtons.RB, new ButtonMapper(new[] { Keys.D }, new[] { Buttons.RightShoulder }));
            ButtonDictionary.Add(CButtons.LT, new ButtonMapper(new[] { Keys.Q }, new[] { Buttons.LeftTrigger }));
            ButtonDictionary.Add(CButtons.RT, new ButtonMapper(new[] { Keys.W }, new[] { Buttons.RightTrigger }));
            ButtonDictionary.Add(CButtons.Select, new ButtonMapper(new[] { Keys.Space }, new[] { Buttons.Back }));
            ButtonDictionary.Add(CButtons.Start, new ButtonMapper(new[] { Keys.Enter }, new[] { Buttons.Start }));
            ButtonDictionary.Add(CButtons.LS, new ButtonMapper(new[] { Keys.E }, new[] { Buttons.LeftStick }));
            ButtonDictionary.Add(CButtons.RS, new ButtonMapper(new[] { Keys.R }, new[] { Buttons.RightStick }));
            SetConfirmCancelButtons();
        }

        public static void SetControlStyle2()
        {
            ButtonDictionary.Clear();
            ButtonDictionary.Add(CButtons.Up, new ButtonMapper(new[] { Keys.W }, new[] { Buttons.DPadUp }));
            ButtonDictionary.Add(CButtons.Down, new ButtonMapper(new[] { Keys.S }, new[] { Buttons.DPadDown }));
            ButtonDictionary.Add(CButtons.Left, new ButtonMapper(new[] { Keys.A }, new[] { Buttons.DPadLeft }));
            ButtonDictionary.Add(CButtons.Right, new ButtonMapper(new[] { Keys.D }, new[] { Buttons.DPadRight }));
            ButtonDictionary.Add(CButtons.A, new ButtonMapper(new[] { Keys.NumPad1 }, new[] { Buttons.A }));
            ButtonDictionary.Add(CButtons.B, new ButtonMapper(new[] { Keys.NumPad2 }, new[] { Buttons.B }));
            ButtonDictionary.Add(CButtons.X, new ButtonMapper(new[] { Keys.NumPad4 }, new[] { Buttons.X }));
            ButtonDictionary.Add(CButtons.Y, new ButtonMapper(new[] { Keys.NumPad5 }, new[] { Buttons.Y }));
            ButtonDictionary.Add(CButtons.LB, new ButtonMapper(new[] { Keys.NumPad7 }, new[] { Buttons.LeftShoulder }));
            ButtonDictionary.Add(CButtons.RB, new ButtonMapper(new[] { Keys.NumPad8 }, new[] { Buttons.RightShoulder }));
            ButtonDictionary.Add(CButtons.LT, new ButtonMapper(new[] { Keys.NumPad9 }, new[] { Buttons.LeftTrigger }));
            ButtonDictionary.Add(CButtons.RT, new ButtonMapper(new[] { Keys.NumPad6 }, new[] { Buttons.RightTrigger }));
            ButtonDictionary.Add(CButtons.Select, new ButtonMapper(new[] { Keys.Space }, new[] { Buttons.Back }));
            ButtonDictionary.Add(CButtons.Start, new ButtonMapper(new[] { Keys.Enter }, new[] { Buttons.Start }));
            ButtonDictionary.Add(CButtons.LS, new ButtonMapper(new[] { Keys.NumPad0 }, new[] { Buttons.LeftStick }));
            ButtonDictionary.Add(CButtons.RS, new ButtonMapper(new[] { Keys.Decimal }, new[] { Buttons.RightStick }));
            SetConfirmCancelButtons();
        }

        public static void SaveButtonMaps(SaveManager saveManager)
        {
            foreach (var buttonMap in ButtonDictionary)
            {
                for (var i = 0; i < buttonMap.Value.Keys.Length; i++)
                    saveManager.SetInt("control" + buttonMap.Key + "key" + i, (int)buttonMap.Value.Keys[i]);

                for (var i = 0; i < buttonMap.Value.Buttons.Length; i++)
                    saveManager.SetInt("control" + buttonMap.Key + "button" + i, (int)buttonMap.Value.Buttons[i]);
            }
        }

        public static void LoadButtonMap(SaveManager saveManager)
        {
            foreach (var buttonMap in ButtonDictionary)
            {
                var index = 0;
                int key;
                var keys = new List<Keys>();
                while ((key = saveManager.GetInt("control" + buttonMap.Key + "key" + index, -1)) >= 0)
                {
                    keys.Add((Keys)key);
                    index++;
                }
                if (keys.Count > 0)
                    buttonMap.Value.Keys = keys.ToArray();

                index = 0;
                int button;
                var gamepadButtons = new List<Buttons>();
                while ((button = saveManager.GetInt("control" + buttonMap.Key + "button" + index, -1)) >= 0)
                {
                    gamepadButtons.Add((Buttons)button);
                    index++;
                }
                if (gamepadButtons.Count > 0)
                    buttonMap.Value.Buttons = gamepadButtons.ToArray();
            }
            SetConfirmCancelButtons();
        }

        public static void SetConfirmCancelButtons()
        {
            if (GameSettings.SwapButtons)
            {
                ConfirmButton = CButtons.B;
                CancelButton  = CButtons.A;
            }
            else
            {
                ConfirmButton = CButtons.A;
                CancelButton  = CButtons.B;
            }
        }

        public static void Update()
        {
            if (_scrollCounter < 0)
                _scrollCounter += ScrollTime;

            _initDirection = _scrollCounter == ScrollStartTime;

            var direction = GetMoveVector2();

            if (direction.Length() != 0)
                _scrollCounter -= Game1.DeltaTime;
            else
                _scrollCounter = ScrollStartTime;

            foreach (var button in ButtonDictionary)
            {
                for (var i = 0; i < button.Value.Keys.Length; i++)
                {
                    if (InputHandler.LastKeyDown(button.Value.Keys[i]))
                        LastKeyboardDown = true;
                }
            }

            foreach (var button in ButtonDictionary)
            {
                for (var i = 0; i < button.Value.Buttons.Length; i++)
                {
                    if (InputHandler.LastGamePadDown(button.Value.Buttons[i]))
                        LastKeyboardDown = false;
                }
            }
            DebugButtons = CButtons.None;
        }

        private static Vector2 Digitalize(Vector2 vec)
        {
            float ax = Math.Abs(vec.X);
            float ay = Math.Abs(vec.Y);
            float threshold = 0.35f;

            if (ax > ay)
            {
                if (ay / ax >= threshold)
                    return new Vector2(Math.Sign(vec.X), Math.Sign(vec.Y));

                return new Vector2(Math.Sign(vec.X), 0);
            }
            else
            {
                if (ax / ay >= threshold)
                    return new Vector2(Math.Sign(vec.X), Math.Sign(vec.Y));

                return new Vector2(0, Math.Sign(vec.Y));
            }
        }

        public static Vector2 GetMoveVector2()
        {
            Vector2 vec = GetAnalogDirection();

            if (vec != Vector2.Zero && GameSettings.DigitalAnalog)
                vec = Digitalize(vec);

            if (vec == Vector2.Zero)
            {
                if (ButtonDown(CButtons.Left)) vec.X = -1;
                else if (ButtonDown(CButtons.Right)) vec.X = 1;

                if (ButtonDown(CButtons.Up)) vec.Y = -1;
                else if (ButtonDown(CButtons.Down)) vec.Y = 1;
            }

            return vec;
        }

        public static Vector2 GetCameraVector2()
        {
            var st = InputHandler.GamePadState;
            Vector2 vec = new Vector2(st.ThumbSticks.Right.X, -st.ThumbSticks.Right.Y);
            if (Math.Abs(vec.X) > GameSettings.DeadZone || Math.Abs(vec.Y) > GameSettings.DeadZone)
                return vec;

            Vector2 keyEventStick = Game1.PlatformInput.RightStick;
            if (Math.Abs(keyEventStick.X) > GameSettings.DeadZone || Math.Abs(keyEventStick.Y) > GameSettings.DeadZone)
                return new Vector2(keyEventStick.X, keyEventStick.Y);

            if (Game1.PlatformInput.HasTouchInput)
            {
                Vector2 touchVec = VirtualController.GetRightStickOutput();
                if (touchVec != Vector2.Zero)
                    return touchVec;
            }

            return Vector2.Zero;
        }

        public static Vector2 GetAnalogDirection()
        {
            var st = InputHandler.GamePadState;
            Vector2 vec = new Vector2(st.ThumbSticks.Left.X, -st.ThumbSticks.Left.Y);
            if (Math.Abs(vec.X) > GameSettings.DeadZone || Math.Abs(vec.Y) > GameSettings.DeadZone)
                return vec;

            if (Game1.PlatformInput.HasTouchInput)
            {
                Vector2 touchVec = VirtualController.GetLeftStickOutput();
                if (touchVec != Vector2.Zero)
                    return touchVec;
            }

            return Vector2.Zero;
        }

        public static bool LastButtonDown(CButtons button)
        {
            for (var i = 0; i < ButtonDictionary[button].Keys.Length; i++)
            {
                if (InputHandler.LastKeyDown(ButtonDictionary[button].Keys[i]))
                    return true;
            }
            for (var i = 0; i < ButtonDictionary[button].Buttons.Length; i++)
            {
                if (InputHandler.LastGamePadDown(ButtonDictionary[button].Buttons[i]))
                    return true;
            }
            return false;
        }

        public static bool ButtonDown(CButtons button)
        {
            var direction = GetAnalogDirection();

            if (_initDirection && direction != Vector2.Zero)
            {
                var dir = AnimationHelper.GetDirection(direction);
                if ((dir == 0 && button == CButtons.Left) || (dir == 1 && button == CButtons.Up) ||
                    (dir == 2 && button == CButtons.Right) || (dir == 3 && button == CButtons.Down))
                    return true;
            }
            for (var i = 0; i < ButtonDictionary[button].Keys.Length; i++)
            {
                if (InputHandler.KeyDown(ButtonDictionary[button].Keys[i]))
                    return true;
            }
            for (var i = 0; i < ButtonDictionary[button].Buttons.Length; i++)
            {
                if (InputHandler.GamePadDown(ButtonDictionary[button].Buttons[i]))
                    return true;
            }
            if (Game1.PlatformInput.HasTouchInput && VirtualController.ButtonDown(button))
                return true;
            if (Game1.PlatformInput.IsButtonDown(button))
                return true;

            return false;
        }

        public static bool ButtonPressed(CButtons button, bool controllerOnly = false)
        {
            var direction = GetAnalogDirection();

            if (_initDirection && direction != Vector2.Zero)
            {
                var dir = AnimationHelper.GetDirection(direction);
                if ((dir == 0 && button == CButtons.Left) || (dir == 1 && button == CButtons.Up) ||
                    (dir == 2 && button == CButtons.Right) || (dir == 3 && button == CButtons.Down))
                    return true;
            }

            if (!controllerOnly)
            {
                for (var i = 0; i < ButtonDictionary[button].Keys.Length; i++)
                {
                    if (InputHandler.KeyPressed(ButtonDictionary[button].Keys[i]))
                        return true;
                }
            }

            for (var i = 0; i < ButtonDictionary[button].Buttons.Length; i++)
            {
                if (InputHandler.GamePadPressed(ButtonDictionary[button].Buttons[i]))
                    return true;
            }

            if ((DebugButtons & button) != 0)
                return true;

            if (Game1.PlatformInput.HasTouchInput && VirtualController.ButtonPressed(button))
                return true;
            if (button == CButtons.Select && InputHandler.PlatformSelectPressed())
                return true;
            if (Game1.PlatformInput.WasButtonPressed(button))
                return true;
            return false;
        }

        public static bool ButtonReleased(CButtons button)
        {
            var direction = GetAnalogDirection();
            if  (direction != Vector2.Zero)
            {
                var dir = AnimationHelper.GetDirection(direction);
                if ((dir == 0 && button == CButtons.Left) || (dir == 1 && button == CButtons.Up) ||
                    (dir == 2 && button == CButtons.Right) || (dir == 3 && button == CButtons.Down))
                    return true;
            }
            for (var i = 0; i < ButtonDictionary[button].Keys.Length; i++)
            {
                if (InputHandler.KeyReleased(ButtonDictionary[button].Keys[i]))
                    return true;
            }
            for (var i = 0; i < ButtonDictionary[button].Buttons.Length; i++)
            {
                if (InputHandler.GamePadReleased(ButtonDictionary[button].Buttons[i]))
                    return true;
            }
            if (Game1.PlatformInput.HasTouchInput && VirtualController.ButtonReleased(button))
                return true;
            if (Game1.PlatformInput.WasButtonReleased(button))
                return true;
            return false;
        }

        public static bool MenuButtonDown(CButtons button)
        {
            var direction = GetAnalogDirection();

            if (direction != Vector2.Zero)
            {
                var dir = AnimationHelper.GetDirection(direction);
                if (((dir == 0 && button == CButtons.Left) || 
                    (dir == 1 && button == CButtons.Up) ||
                    (dir == 2 && button == CButtons.Right) || 
                    (dir == 3 && button == CButtons.Down)) && 
                    (_scrollCounter < 0 || _initDirection))
                {
                    return true;
                }
            }
            return ButtonPressed(button) || (ButtonDown(button) && _scrollCounter < 0);
        }

        public static bool TrendyButtonDown(CButtons button)
        {
            foreach (var key in ButtonDictionary[button].Keys)
            {
                if (InputHandler.KeyDown(key))
                    return true;
            }

            if (button == CancelButton && InputHandler.GamePadDown(GameSettings.SwapButtons ? Buttons.A : Buttons.B))
                return true;

            if (button == ConfirmButton && InputHandler.GamePadDown(GameSettings.SwapButtons ? Buttons.B : Buttons.A))
                return true;

            if (Game1.PlatformInput.HasTouchInput && VirtualController.ButtonDown(button))
                return true;
            if (Game1.PlatformInput.IsButtonDown(button))
                return true;

            return false;
        }

        public static CButtons GetPressedButtons()
        {
            CButtons pressedButtons = 0;

            foreach (var bEntry in ButtonDictionary)
            {
                for (var i = 0; i < bEntry.Value.Keys.Length; i++)
                {
                    if (InputHandler.KeyPressed(bEntry.Value.Keys[i]))
                        pressedButtons |= bEntry.Key;
                }
                for (var i = 0; i < bEntry.Value.Buttons.Length; i++)
                {
                    if (InputHandler.GamePadPressed(bEntry.Value.Buttons[i]))
                        pressedButtons |= bEntry.Key;
                }
            }
            foreach (CButtons button in Enum.GetValues(typeof(CButtons)))
            {
                if (button == CButtons.None)
                    continue;
                if (Game1.PlatformInput.HasTouchInput && VirtualController.ButtonPressed(button))
                    pressedButtons |= button;
                if (Game1.PlatformInput.WasButtonPressed(button))
                    pressedButtons |= button;
            }
            return pressedButtons;
        }

        public static bool AnyButtonPressed()
        {
            foreach (var bEntry in ButtonDictionary)
            {
                for (var i = 0; i < bEntry.Value.Keys.Length; i++)
                {
                    if (InputHandler.KeyPressed(bEntry.Value.Keys[i]))
                        return true;
                }
                for (var i = 0; i < bEntry.Value.Buttons.Length; i++)
                {
                    if (InputHandler.GamePadPressed(bEntry.Value.Buttons[i]))
                        return true;
                }
            }

            foreach (CButtons button in Enum.GetValues(typeof(CButtons)))
            {
                if (button == CButtons.None)
                    continue;
                if (Game1.PlatformInput.HasTouchInput && VirtualController.ButtonPressed(button))
                    return true;
                if (Game1.PlatformInput.WasButtonPressed(button))
                    return true;
            }

            if (GetMoveVector2() != Vector2.Zero)
                return true;

            return false;
        }
    }
}
