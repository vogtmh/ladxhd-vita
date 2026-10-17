using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Pages;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Controls
{
    public static class VirtualController
    {
        private static readonly List<VirtualButton> _buttons = new List<VirtualButton>();
        private static VirtualButton _controllerButton;
        private static VirtualStick _leftStick;
        private static VirtualStick _rightStick;

        private static DictAtlasEntry _dPadSprite;
        private static Rectangle _dPadBounds;
        private static Rectangle _dPadDefaultBounds;
        private static bool _dPadActive;
        private static bool _dPadEditorOnly;

        private static float _controllerHideTimer;
        private const float ControllerHideDelay = 5000f;
        private static bool _controllerButtonHidden;

        private static float ControlsScale => GameSettings.TouchScaling * 0.25f;

        public static float DPadButtonAlpha = GameSettings.TouchOpacity * 0.01f;
        public static float DPadShadowAlpha = GameSettings.ShadowOpacity * 0.01f;

        private static bool menu = true;

        private static int _screenWidth;
        private static int _screenHeight;

        // ----------------------------------------------------------------------------------------------------------------------------
        // LAYOUT EDITOR STATE
        // ----------------------------------------------------------------------------------------------------------------------------
        public const string DPadKey       = "DPad";
        public const string ControllerKey = "Controller";
        public const string LeftStickKey  = "LeftStick";
        public const string RightStickKey = "RightStick";

        // Every key that can be stored in GameSettings.TouchLayout (used by SettingsSaveLoad).
        public static readonly string[] LayoutKeys =
        {
            DPadKey, "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS",
            "Select", "Start", ControllerKey, LeftStickKey, RightStickKey
        };

        public static bool EditMode { get; private set; }

        private const int     EditKeepAliveFrames = 3;
        private const float   DragThreshold       = 6f;
        private const float   DoubleTapTime       = 350f;
        private const float   EditGridAlpha       = 0.35f;
        private const int     EditGridMinDraw     = 10;
        private static Color  EditGridColor       = Color.DodgerBlue * EditGridAlpha;

        private static int     _editKeepAlive;
        private static float   _editTime;
        private static int?    _dragTouchId;
        private static string  _dragKey;
        private static Vector2 _dragStartTouch;
        private static Vector2 _dragStartOffset;
        private static bool    _dragMoved;
        private static string  _lastTapKey;
        private static float   _lastTapTime;

        public static VirtualStick GetLeftStick() => _leftStick;
        public static VirtualStick GetRightStick() => _rightStick;
        public static List<VirtualButton> GetButtons() => _buttons;

        public static Vector2 GetLeftStickOutput() => _leftStick != null ? _leftStick.Output : Vector2.Zero;
        public static Vector2 GetRightStickOutput() => _rightStick != null ? _rightStick.Output : Vector2.Zero;

        private static Point GetShadowOffset()
        {
            int offset = (int)(3 * ControlsScale);
            return new Point(offset, offset);
        }

        public static void Initialize(int screenWidth, int screenHeight, bool? isMenu)
        {
            // Store the current states so button states survive reinitialization.
            var previousStates = new Dictionary<CButtons, (bool IsDown, bool WasDown, int? TouchId)>();
            foreach (var b in _buttons)
                previousStates[b.Button] = (b.IsDown, b.WasDown, b.TouchId);

            var previousControllerState = _controllerButton != null
                ? (_controllerButton.IsDown, _controllerButton.WasDown, _controllerButton.TouchId)
                : ((bool, bool, int?)?)null;

            _dPadSprite = null;
            _dPadActive = false;
            _dPadEditorOnly = false;
            _leftStick = null;
            _rightStick = null;
            _buttons.Clear();
            _controllerButton = null;
            _controllerHideTimer = 0f;
            _controllerButtonHidden = false;
            if (isMenu != null) menu = isMenu.Value;

            _screenWidth = screenWidth;
            _screenHeight = screenHeight;

            float scale = ControlsScale;
            int buttonSize = (int)(40 * scale);
            int margin = (int)(16 * scale);
            int spacing = (int)(8 * scale);

            // ------------------------------------------------------------------------------------------------------------------------
            // LEFT SIDE: DPAD 
            // ------------------------------------------------------------------------------------------------------------------------
            int leftX = margin;
            int leftY = screenHeight - margin;

            // Always computed: the LS button is placed relative to it even when the D-pad is hidden.
            _dPadDefaultBounds = new Rectangle(leftX, leftY - (buttonSize * 3) - (spacing * 2), buttonSize * 3 + spacing * 2, buttonSize * 3 + spacing * 2);
            _dPadBounds = _dPadDefaultBounds;

            // While editing, every control is created so it can be positioned. Controls that are turned off in the
            // current settings are flagged "EditorOnly" (drawn dimmer) and are never created outside the editor.
            bool edit = EditMode;

            bool dPadEnabled = GameSettings.TouchMovement is 0 or 2 || (GameSettings.TouchMovement == 1 && (menu || edit));

            if (dPadEnabled || edit)
            {
                _dPadSprite = Resources.GetSprite("button_dpad");
                _dPadActive = true;
                _dPadEditorOnly = !dPadEnabled;

                Rectangle rectLeft = new Rectangle(leftX, leftY - (buttonSize * 2) - spacing, buttonSize, buttonSize);
                Rectangle rectUp = new Rectangle(leftX + buttonSize + spacing, leftY - (buttonSize * 3) - (spacing * 2), buttonSize, buttonSize);
                Rectangle rectRight = new Rectangle(leftX + (buttonSize * 2) + (spacing * 2), leftY - (buttonSize * 2) - spacing, buttonSize, buttonSize);
                Rectangle rectDown = new Rectangle(leftX + buttonSize + spacing, leftY - buttonSize, buttonSize, buttonSize);

                _buttons.Add(new VirtualButton("null", CButtons.Left, rectLeft));
                _buttons.Add(new VirtualButton("null", CButtons.Up, rectUp));
                _buttons.Add(new VirtualButton("null", CButtons.Right, rectRight));
                _buttons.Add(new VirtualButton("null", CButtons.Down, rectDown));

                Rectangle rectUpLeft = new Rectangle(leftX, leftY - (buttonSize * 3) - (spacing * 2), buttonSize, buttonSize);
                Rectangle rectUpRight = new Rectangle(leftX + (buttonSize * 2) + (spacing * 2), leftY - (buttonSize * 3) - (spacing * 2), buttonSize, buttonSize);
                Rectangle rectDownLeft = new Rectangle(leftX, leftY - buttonSize, buttonSize, buttonSize);
                Rectangle rectDownRight = new Rectangle(leftX + (buttonSize * 2) + (spacing * 2), leftY - buttonSize, buttonSize, buttonSize);

                _buttons.Add(new VirtualButton("null", CButtons.Up | CButtons.Left, rectUpLeft));
                _buttons.Add(new VirtualButton("null", CButtons.Up | CButtons.Right, rectUpRight));
                _buttons.Add(new VirtualButton("null", CButtons.Down | CButtons.Left, rectDownLeft));
                _buttons.Add(new VirtualButton("null", CButtons.Down | CButtons.Right, rectDownRight));
            }
            // ------------------------------------------------------------------------------------------------------------------------
            // RIGHT SIDE: X / Y / B / A
            // ------------------------------------------------------------------------------------------------------------------------
            int rightX = screenWidth - margin;
            int rightY = screenHeight - margin;

            Rectangle rectX = new Rectangle(rightX - (buttonSize * 3) - (spacing * 2), rightY - (buttonSize * 2) - spacing, buttonSize, buttonSize);
            Rectangle rectY = new Rectangle(rightX - (buttonSize * 2) - spacing, rightY - (buttonSize * 3) - (spacing * 2), buttonSize, buttonSize);
            Rectangle rectB = new Rectangle(rightX - buttonSize, rightY - (buttonSize * 2) - spacing, buttonSize, buttonSize);
            Rectangle rectA = new Rectangle(rightX - (buttonSize * 2) - spacing, rightY - buttonSize, buttonSize, buttonSize);

            _buttons.Add(new VirtualButton("button_x", CButtons.X, rectX));
            _buttons.Add(new VirtualButton("button_y", CButtons.Y, rectY));
            _buttons.Add(new VirtualButton("button_b", CButtons.B, rectB));
            _buttons.Add(new VirtualButton("button_a", CButtons.A, rectA));

            // ------------------------------------------------------------------------------------------------------------------------
            // STICK BUTTONS
            // ------------------------------------------------------------------------------------------------------------------------
            if (GameSettings.TouchSticks || edit)
            {
                bool sticksEditorOnly = !GameSettings.TouchSticks;

                // RS sits higher when LB/RB are next to the face buttons. Offsets are always measured from the
                // six-button position so a moved RS lands in the same place whichever layout is active.
                int extraButtonLift = GameSettings.SixButtons ? buttonSize + spacing : spacing;
                Rectangle rectExtraR = new Rectangle(rectY.X, rectY.Y - buttonSize - spacing - extraButtonLift, buttonSize, buttonSize);
                Rectangle anchorExtraR = new Rectangle(rectY.X, rectY.Y - buttonSize - spacing - (buttonSize + spacing), buttonSize, buttonSize);
                _buttons.Add(new VirtualButton("button_rc", CButtons.RS, rectExtraR) { LayoutAnchor = anchorExtraR, EditorOnly = sticksEditorOnly });

                int dpadCenterX = _dPadDefaultBounds.X + (_dPadDefaultBounds.Width / 2) - (buttonSize / 2);
                Rectangle rectExtraL = new Rectangle(dpadCenterX, _dPadDefaultBounds.Y - (buttonSize * 2) - spacing, buttonSize, buttonSize);
                _buttons.Add(new VirtualButton("button_lc", CButtons.LS, rectExtraL) { EditorOnly = sticksEditorOnly });
            }
            // ------------------------------------------------------------------------------------------------------------------------
            // ANALOG STICKS
            // ------------------------------------------------------------------------------------------------------------------------
            float stickRadius = 40f * scale;
            float clusterWidth = buttonSize * 3 + spacing * 2;
            float stickGap = spacing * 2 + stickRadius;
            int stickLift = (int)(20 * scale);

            // Beside the D-pad (TouchMovement 0) or in the corner in its place (1 and 3). Offsets are always measured
            // from the beside-the-D-pad position so a moved stick lands in the same place in every movement mode.
            Vector2 stickBesideDPad = new Vector2(leftX + clusterWidth + stickGap, screenHeight - margin - stickRadius - stickLift);
            Vector2 stickInCorner = new Vector2(leftX + stickGap, screenHeight - margin - stickRadius - stickLift);

            bool leftStickEnabled = GameSettings.TouchMovement is 0 or 3 || (GameSettings.TouchMovement == 1 && (!menu || edit));

            if (leftStickEnabled || edit)
            {
                Vector2 leftStickCenter = GameSettings.TouchMovement is 1 or 3 ? stickInCorner : stickBesideDPad;
                _leftStick = new VirtualStick("button_ls", leftStickCenter, stickRadius)
                    { LayoutKey = LeftStickKey, AnchorCenter = stickBesideDPad, EditorOnly = !leftStickEnabled };
            }

            if (!GameSettings.CameraLock || edit)
            {
                Vector2 rightStickCenter = new Vector2(rightX - clusterWidth - stickGap, screenHeight - margin - stickRadius - stickLift);
                _rightStick = new VirtualStick("button_rs", rightStickCenter, stickRadius)
                    { LayoutKey = RightStickKey, EditorOnly = GameSettings.CameraLock };
            }

            // ------------------------------------------------------------------------------------------------------------------------
            // TOP SHOULDER BUTTONS / SIX BUTTON LAYOUT
            // ------------------------------------------------------------------------------------------------------------------------
            int topY = margin;

            // LB/RB sit next to the face buttons with Six Buttons, or at the top of the screen with only Triggers Scale.
            // Offsets are always measured from the six-button position so a moved LB/RB lands in the same place either way.
            Rectangle rectLB6 = new Rectangle(rectX.X, rectY.Y - buttonSize - spacing, buttonSize, buttonSize);
            Rectangle rectRB6 = new Rectangle(rectB.X, rectY.Y - buttonSize - spacing, buttonSize, buttonSize);

            bool triggersScaleOnly = GameSettings.TriggersScale && !GameSettings.SixButtons;
            bool triggersEnabled = GameSettings.SixButtons && GameSettings.TriggersScale;

            if (GameSettings.SixButtons || (edit && !triggersScaleOnly))
            {
                bool shouldersEditorOnly = !GameSettings.SixButtons;
                _buttons.Add(new VirtualButton("button_lb", CButtons.LB, rectLB6) { EditorOnly = shouldersEditorOnly });
                _buttons.Add(new VirtualButton("button_rb", CButtons.RB, rectRB6) { EditorOnly = shouldersEditorOnly });
            }
            else if (triggersScaleOnly)
            {
                _buttons.Add(new VirtualButton("button_rb", CButtons.RB, new Rectangle(screenWidth - margin - (buttonSize * 2) - (spacing * 2), topY, buttonSize, buttonSize)) { LayoutAnchor = rectRB6 });
                _buttons.Add(new VirtualButton("button_lb", CButtons.LB, new Rectangle(margin + buttonSize + (spacing * 2), topY, buttonSize, buttonSize)) { LayoutAnchor = rectLB6 });
            }

            if (triggersEnabled || edit)
            {
                _buttons.Add(new VirtualButton("button_rt", CButtons.RT, new Rectangle(screenWidth - margin - buttonSize, topY, buttonSize, buttonSize)) { EditorOnly = !triggersEnabled });
                _buttons.Add(new VirtualButton("button_lt", CButtons.LT, new Rectangle(margin, topY, buttonSize, buttonSize)) { EditorOnly = !triggersEnabled });
            }
            // ------------------------------------------------------------------------------------------------------------------------
            // SELECT / START
            // ------------------------------------------------------------------------------------------------------------------------
            int centerX = screenWidth / 2;
            int bottomY = screenHeight - margin - buttonSize;

            _buttons.Add(new VirtualButton("button_share", CButtons.Select, new Rectangle(centerX - (buttonSize * 2) - spacing, bottomY, buttonSize, buttonSize)));
            _controllerButton = new VirtualButton("button_controller", CButtons.None, new Rectangle(centerX - (buttonSize / 2), bottomY, buttonSize, buttonSize));
            _buttons.Add(new VirtualButton("button_menu", CButtons.Start, new Rectangle(centerX + buttonSize + spacing, bottomY, buttonSize, buttonSize)));

            // Move everything from its default position to the user's saved position.
            ApplyLayout();

            // Restore any touch that was already down before the rebuild.
            foreach (var button in _buttons)
            {
                if (previousStates.TryGetValue(button.Button, out var state))
                {
                    button.IsDown = state.IsDown;
                    button.WasDown = state.WasDown;
                    button.TouchId = state.TouchId;
                }
            }
            if (_controllerButton != null && previousControllerState.HasValue)
            {
                _controllerButton.IsDown = previousControllerState.Value.Item1;
                _controllerButton.WasDown = previousControllerState.Value.Item2;
                _controllerButton.TouchId = previousControllerState.Value.Item3;
            }
        }

        public static void UpdateButtonsAlpha()
        {
            foreach (var button in _buttons)
            {
                button.DisplayAlpha = UpdateButtonAlpha(button.DisplayAlpha, button.IsDown);
                button.ShadowAlpha  = UpdateShadowAlpha(button.ShadowAlpha, button.IsDown);
            }
            if (_controllerButton != null)
            {
                _controllerButton.DisplayAlpha = UpdateButtonAlpha(_controllerButton.DisplayAlpha, _controllerButton.IsDown, true);
                _controllerButton.ShadowAlpha  = UpdateShadowAlpha(_controllerButton.ShadowAlpha, _controllerButton.IsDown, true);
            }
            if (_leftStick != null)
            {
                _leftStick.DisplayAlpha = UpdateButtonAlpha(_leftStick.DisplayAlpha, _leftStick.IsDown);
                _leftStick.ShadowAlpha  = UpdateShadowAlpha(_leftStick.ShadowAlpha, _leftStick.IsDown);
            }
            if (_rightStick != null)
            {
                _rightStick.DisplayAlpha = UpdateButtonAlpha(_rightStick.DisplayAlpha, _rightStick.IsDown);
                _rightStick.ShadowAlpha  = UpdateShadowAlpha(_rightStick.ShadowAlpha, _rightStick.IsDown);
            }
            bool dPadActive = DPadIsActive();
            DPadButtonAlpha = UpdateButtonAlpha(DPadButtonAlpha, dPadActive);
            DPadShadowAlpha = UpdateShadowAlpha(DPadShadowAlpha, dPadActive);
        }

        private static float UpdateButtonAlpha(float currentAlpha, bool isActive, bool isControllerButton = false)
        {
            float buttonMaxAlpha = 1.00f;
            float targetAlpha;

            if (isControllerButton)
            {
                if (GameSettings.TouchControls == 0 && _controllerButtonHidden)
                    targetAlpha = 0f;
                else
                    targetAlpha = isActive ? buttonMaxAlpha : GameSettings.TouchOpacity * 0.01f;
            }
            else if (GameSettings.TouchControls == 0)
                targetAlpha = 0f;
            else if (GameSettings.TouchControls == 2)
                targetAlpha = buttonMaxAlpha;
            else
                targetAlpha = isActive ? buttonMaxAlpha : GameSettings.TouchOpacity * 0.01f;

            float speed = 0.01f * Game1.DeltaTime;
            return MathHelper.Lerp(currentAlpha, targetAlpha, MathHelper.Clamp(speed, 0f, 1f));
        }

        private static float UpdateShadowAlpha(float currentAlpha, bool isActive, bool isControllerButton = false)
        {
            float targetAlpha;

            if (isControllerButton)
            {
                if (GameSettings.TouchControls == 0 && _controllerButtonHidden)
                    targetAlpha = 0f;
                else
                    targetAlpha = GameSettings.ShadowOpacity * 0.01f;
            }
            else if (GameSettings.TouchControls == 0)
                targetAlpha = 0f;
            else
                targetAlpha = GameSettings.ShadowOpacity * 0.01f;

            float speed = 0.01f * Game1.DeltaTime;
            return MathHelper.Lerp(currentAlpha, targetAlpha, MathHelper.Clamp(speed, 0f, 1f));
        }

        public static void Update()
        {
            foreach (var button in _buttons)
                button.BeginUpdate();

            _controllerButton?.BeginUpdate();
            _leftStick?.BeginUpdate();
            _rightStick?.BeginUpdate();

            // While editing the layout, touches drag controls around and nothing is sent to the game.
            if (EditMode)
            {
                UpdateEditMode();
                return;
            }

            var touches = Game1.PlatformInput.Touches;

            int holdPadding = (int)(12 * ControlsScale);
            bool anyTouchActive = false;

            for (int i = 0; i < touches.Count; i++)
            {
                PlatformTouch touch = touches[i];

                if (touch.State == PlatformTouchState.Pressed ||
                    touch.State == PlatformTouchState.Moved)
                {
                    anyTouchActive = true;
                    break;
                }
            }

            // --------------------------------------------------------------------------------------------------------------------
            // CONTROLLER BUTTON AUTO-HIDE
            // --------------------------------------------------------------------------------------------------------------------
            if (GameSettings.TouchControls == 0)
            {
                if (anyTouchActive)
                {
                    _controllerHideTimer = 0f;
                    _controllerButtonHidden = false;
                }
                else
                {
                    _controllerHideTimer += Game1.DeltaTime;

                    if (_controllerHideTimer >= ControllerHideDelay)
                        _controllerButtonHidden = true;
                }
            }
            else
            {
                _controllerHideTimer = 0f;
                _controllerButtonHidden = false;
            }

            // --------------------------------------------------------------------------------------------------------------------
            // KEEP CONTROLLER BUTTON ALIVE
            // --------------------------------------------------------------------------------------------------------------------
            if (_controllerButton != null && _controllerButton.TouchId != null)
            {
                bool foundTouch = false;

                for (int i = 0; i < touches.Count; i++)
                {
                    PlatformTouch touch = touches[i];

                    if (touch.Id != _controllerButton.TouchId.Value)
                        continue;

                    if (touch.State == PlatformTouchState.Released ||
                        touch.State == PlatformTouchState.Invalid)
                        break;

                    foundTouch = true;

                    if (_controllerButton.ContainsExpanded(touch.Position.ToPoint(), holdPadding))
                        _controllerButton.IsDown = true;

                    break;
                }

                if (!foundTouch || !_controllerButton.IsDown)
                    _controllerButton.TouchId = null;
            }

            // --------------------------------------------------------------------------------------------------------------------
            // TOUCH CONTROLS DISABLED
            // Only the controller button is allowed to work here.
            // --------------------------------------------------------------------------------------------------------------------
            if (GameSettings.TouchControls == 0)
            {
                if (_controllerButton != null &&
                    !_controllerButtonHidden &&
                    _controllerButton.TouchId == null)
                {
                    for (int i = 0; i < touches.Count; i++)
                    {
                        PlatformTouch touch = touches[i];

                        if (touch.State != PlatformTouchState.Pressed)
                            continue;

                        if (_controllerButton.Contains(touch.Position.ToPoint()))
                        {
                            _controllerButton.IsDown = true;
                            _controllerButton.TouchId = touch.Id;
                            break;
                        }
                    }
                }

                foreach (var button in _buttons)
                    button.TouchId = null;

                if (_leftStick != null)
                    _leftStick.TouchId = null;

                if (_rightStick != null)
                    _rightStick.TouchId = null;

                UpdateButtonsAlpha();

                if (ControllerButtonPressed())
                {
                    GameSettings.TouchControls = 1;

                    if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(ControlOnScreenPage), out var onScreenControlsPage))
                    {
                        var onScreenSettingsPage = (ControlOnScreenPage)onScreenControlsPage;
                        onScreenSettingsPage.SetOnScreenControlsSlider(GameSettings.TouchControls);
                    }
                }

                return;
            }

            // --------------------------------------------------------------------------------------------------------------------
            // KEEP NORMAL BUTTONS ALIVE
            // --------------------------------------------------------------------------------------------------------------------
            foreach (var button in _buttons)
            {
                if (button.TouchId == null)
                    continue;

                bool foundTouch = false;

                for (int i = 0; i < touches.Count; i++)
                {
                    PlatformTouch touch = touches[i];

                    if (touch.Id != button.TouchId.Value)
                        continue;

                    if (touch.State == PlatformTouchState.Released ||
                        touch.State == PlatformTouchState.Invalid)
                        break;

                    foundTouch = true;

                    if (button.ContainsExpanded(touch.Position.ToPoint(), holdPadding))
                        button.IsDown = true;

                    break;
                }

                if (!foundTouch || !button.IsDown)
                    button.TouchId = null;
            }

            // --------------------------------------------------------------------------------------------------------------------
            // KEEP LEFT STICK ALIVE
            // --------------------------------------------------------------------------------------------------------------------
            if (_leftStick != null && _leftStick.TouchId != null)
            {
                bool foundTouch = false;

                for (int i = 0; i < touches.Count; i++)
                {
                    PlatformTouch touch = touches[i];

                    if (touch.Id != _leftStick.TouchId.Value)
                        continue;

                    if (touch.State == PlatformTouchState.Released ||
                        touch.State == PlatformTouchState.Invalid)
                        break;

                    foundTouch = true;
                    _leftStick.IsDown = true;
                    _leftStick.SetTouchPosition(touch.Position);
                    break;
                }

                if (!foundTouch || !_leftStick.IsDown)
                    _leftStick.TouchId = null;
            }

            // --------------------------------------------------------------------------------------------------------------------
            // KEEP RIGHT STICK ALIVE
            // --------------------------------------------------------------------------------------------------------------------
            if (_rightStick != null && _rightStick.TouchId != null)
            {
                bool foundTouch = false;

                for (int i = 0; i < touches.Count; i++)
                {
                    PlatformTouch touch = touches[i];

                    if (touch.Id != _rightStick.TouchId.Value)
                        continue;

                    if (touch.State == PlatformTouchState.Released ||
                        touch.State == PlatformTouchState.Invalid)
                        break;

                    foundTouch = true;
                    _rightStick.IsDown = true;
                    _rightStick.SetTouchPosition(touch.Position);
                    break;
                }

                if (!foundTouch || !_rightStick.IsDown)
                    _rightStick.TouchId = null;
            }

            // --------------------------------------------------------------------------------------------------------------------
            // CLAIM NEW TOUCHES
            // --------------------------------------------------------------------------------------------------------------------
            for (int i = 0; i < touches.Count; i++)
            {
                PlatformTouch touch = touches[i];

                // We need both "Pressed" and "Moved" here to smoothly move between buttons.
                if (touch.State != PlatformTouchState.Pressed &&
                    touch.State != PlatformTouchState.Moved)
                    continue;

                bool alreadyUsed = false;

                if (_controllerButton != null && _controllerButton.TouchId == touch.Id)
                    alreadyUsed = true;

                if (!alreadyUsed)
                {
                    foreach (var button in _buttons)
                    {
                        if (button.TouchId == touch.Id)
                        {
                            alreadyUsed = true;
                            break;
                        }
                    }
                }

                if (!alreadyUsed && _leftStick != null && _leftStick.TouchId == touch.Id)
                    alreadyUsed = true;

                if (!alreadyUsed && _rightStick != null && _rightStick.TouchId == touch.Id)
                    alreadyUsed = true;

                if (alreadyUsed)
                    continue;

                Point point = touch.Position.ToPoint();

                // Try controller button first.
                if (_controllerButton != null && _controllerButton.TouchId == null && _controllerButton.Contains(point))
                {
                    _controllerButton.IsDown = true;
                    _controllerButton.TouchId = touch.Id;
                    continue;
                }

                // Try buttons next.
                bool claimed = false;

                foreach (var button in _buttons)
                {
                    if (button.TouchId != null)
                        continue;

                    if (button.Contains(point))
                    {
                        button.IsDown = true;
                        button.TouchId = touch.Id;
                        claimed = true;
                        break;
                    }
                }

                if (claimed)
                    continue;

                // Then try left stick.
                if (_leftStick != null && _leftStick.TouchId == null && _leftStick.Contains(point))
                {
                    _leftStick.IsDown = true;
                    _leftStick.TouchId = touch.Id;
                    _leftStick.SetTouchPosition(touch.Position);
                    continue;
                }

                // Then try right stick.
                if (_rightStick != null && _rightStick.TouchId == null && _rightStick.Contains(point))
                {
                    _rightStick.IsDown = true;
                    _rightStick.TouchId = touch.Id;
                    _rightStick.SetTouchPosition(touch.Position);
                }
            }

            UpdateButtonsAlpha();

            if (ControllerButtonPressed())
            {
                GameSettings.TouchControls = 0;

                if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(ControlOnScreenPage), out var onScreenControlsPage))
                {
                    var onScreenSettingsPage = (ControlOnScreenPage)onScreenControlsPage;
                    onScreenSettingsPage.SetOnScreenControlsSlider(GameSettings.TouchControls);
                }
            }
        }

        public static bool ButtonDown(CButtons button)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if ((_buttons[i].Button & button) != 0 && _buttons[i].IsDown)
                    return true;
            }
            return false;
        }

        public static bool ButtonPressed(CButtons button)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if ((_buttons[i].Button & button) != 0 && _buttons[i].Pressed())
                    return true;
            }
            return false;
        }

        public static bool ButtonReleased(CButtons button)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if ((_buttons[i].Button & button) != 0 && _buttons[i].Released())
                    return true;
            }
            return false;
        }

        public static bool ControllerButtonDown()
        {
            return _controllerButton != null && _controllerButton.IsDown;
        }

        public static bool ControllerButtonPressed()
        {
            return _controllerButton != null && _controllerButton.Pressed();
        }

        public static bool ControllerButtonReleased()
        {
            return _controllerButton != null && _controllerButton.Released();
        }

        private static bool DPadIsActive()
        {
            return ButtonDown(CButtons.Left) || ButtonDown(CButtons.Up) || ButtonDown(CButtons.Right) || ButtonDown(CButtons.Down);
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (EditMode)
                DrawEditGrid(spriteBatch);

            if (_controllerButton != null)
            {
                float alpha = _controllerButton.DisplayAlpha;

                if (_controllerButton.Sprite != null)
                {
                    float shadowAlpha = _controllerButton.ShadowAlpha;
                    Point shadowOffset = GetShadowOffset();

                    Rectangle src = _controllerButton.Sprite.ScaledRectangle;
                    Vector2 position = new Vector2(_controllerButton.Bounds.X, _controllerButton.Bounds.Y);
                    Vector2 shadowPosition = new Vector2(_controllerButton.Bounds.X + shadowOffset.X, _controllerButton.Bounds.Y + shadowOffset.Y);

                    float scaleX = _controllerButton.Bounds.Width / (float)src.Width;
                    float scaleY = _controllerButton.Bounds.Height / (float)src.Height;
                    Vector2 scale = new Vector2(scaleX, scaleY);

                    spriteBatch.Draw(_controllerButton.Sprite.Texture, shadowPosition, src, Color.Black * shadowAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_controllerButton.Sprite.Texture, position, src, Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                else if (_controllerButton.SpriteName != "null")
                {
                    spriteBatch.Draw(Resources.SprWhite, _controllerButton.Bounds, Color.White * alpha);
                }
            }

            if (GameSettings.TouchControls == 0 && !EditMode)
                return;

            if (_dPadSprite != null)
            {
                float alpha = DPadButtonAlpha;
                float shadowAlpha = DPadShadowAlpha;
                Point shadowOffset = GetShadowOffset();

                Rectangle src = _dPadSprite.ScaledRectangle;
                Vector2 position = new Vector2(_dPadBounds.X, _dPadBounds.Y);
                Vector2 shadowPosition = new Vector2(_dPadBounds.X + shadowOffset.X, _dPadBounds.Y + shadowOffset.Y);

                float scaleX = _dPadBounds.Width / (float)src.Width;
                float scaleY = _dPadBounds.Height / (float)src.Height;
                Vector2 scale = new Vector2(scaleX, scaleY);

                spriteBatch.Draw(_dPadSprite.Texture, shadowPosition, src, Color.Black * shadowAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(_dPadSprite.Texture, position, src, Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            DrawStick(spriteBatch, _leftStick);
            DrawStick(spriteBatch, _rightStick);

            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                float alpha = button.DisplayAlpha;

                if (button.Sprite != null)
                {
                    float shadowAlpha = button.ShadowAlpha;
                    Point shadowOffset = GetShadowOffset();

                    Rectangle src = button.Sprite.ScaledRectangle;
                    Vector2 position = new Vector2(button.Bounds.X, button.Bounds.Y);
                    Vector2 shadowPosition = new Vector2(button.Bounds.X + shadowOffset.X, button.Bounds.Y + shadowOffset.Y);

                    float scaleX = button.Bounds.Width / (float)src.Width;
                    float scaleY = button.Bounds.Height / (float)src.Height;
                    Vector2 scale = new Vector2(scaleX, scaleY);

                    spriteBatch.Draw(button.Sprite.Texture, shadowPosition, src, Color.Black * shadowAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    spriteBatch.Draw(button.Sprite.Texture, position, src, Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                else if (button.SpriteName != "null")
                {
                    spriteBatch.Draw(Resources.SprWhite, button.Bounds, Color.White * alpha);
                }
            }
        }

        // --------------------------------------------------------------------------------------------------------------------------------
        // LAYOUT
        // --------------------------------------------------------------------------------------------------------------------------------
        public static string GetLayoutKey(CButtons button)
        {
            if (button == CButtons.None)
                return ControllerKey;

            // All eight D-pad hit areas (including diagonals) move together with the D-pad sprite.
            if ((button & (CButtons.Up | CButtons.Down | CButtons.Left | CButtons.Right)) != 0)
                return DPadKey;

            return button.ToString();
        }

        public static void ResetLayout()
        {
            GameSettings.TouchLayout.Clear();
            ApplyLayout();
        }

        // A control with no saved offset sits at its default position for the current settings. A control with a saved
        // offset sits at its anchor + offset. The anchor is the same in every configuration (see Initialize), so a moved
        // control stays where it was placed even if an option later changes its default position.
        private static void ApplyLayout()
        {
            // The D-pad sprite and its eight hit areas share one delta so they stay aligned (its anchor is its default).
            TryGetLayoutDelta(DPadKey, _dPadDefaultBounds, out Point dPadDelta);
            _dPadBounds = _dPadDefaultBounds;
            _dPadBounds.Offset(dPadDelta);

            foreach (var button in _buttons)
                ApplyButtonLayout(button, dPadDelta);

            if (_controllerButton != null)
                ApplyButtonLayout(_controllerButton, Point.Zero);

            ApplyStickLayout(_leftStick);
            ApplyStickLayout(_rightStick);
        }

        private static void ApplyButtonLayout(VirtualButton button, Point dPadDelta)
        {
            if (button.LayoutKey == DPadKey)
            {
                button.Bounds = button.DefaultBounds;
                button.Bounds.Offset(dPadDelta);
            }
            else if (TryGetLayoutDelta(button.LayoutKey, button.LayoutAnchor, out Point delta))
            {
                button.Bounds = button.LayoutAnchor;
                button.Bounds.Offset(delta);
            }
            else
            {
                button.Bounds = button.DefaultBounds;
            }
        }

        private static void ApplyStickLayout(VirtualStick stick)
        {
            if (stick == null)
                return;

            if (TryGetLayoutDelta(stick.LayoutKey, GetStickRect(stick.AnchorCenter, stick.Radius), out Point delta))
                stick.Center = stick.AnchorCenter + delta.ToVector2();
            else
                stick.Center = stick.DefaultCenter;

            stick.KnobPosition = stick.Center;
        }

        private static Rectangle GetStickRect(Vector2 center, float stickRadius)
        {
            int radius = (int)MathF.Ceiling(stickRadius);
            return new Rectangle((int)center.X - radius, (int)center.Y - radius, radius * 2, radius * 2);
        }

        // Converts the saved offset for a control into a pixel delta from its anchor, clamped so the control stays on screen.
        // Returns false (and a zero delta) if the control has no saved offset.
        private static bool TryGetLayoutDelta(string key, Rectangle anchor, out Point delta)
        {
            delta = Point.Zero;

            if (key == null || !GameSettings.TouchLayout.TryGetValue(key, out var offset))
                return false;

            float scale = ControlsScale;
            int x = anchor.X + (int)MathF.Round(offset.X * scale);
            int y = anchor.Y + (int)MathF.Round(offset.Y * scale);

            x = Math.Clamp(x, 0, Math.Max(0, _screenWidth - anchor.Width));
            y = Math.Clamp(y, 0, Math.Max(0, _screenHeight - anchor.Height));

            delta = new Point(x - anchor.X, y - anchor.Y);
            return true;
        }

        // Gets the anchor a control's offset is measured from, and where the control is currently drawn.
        private static bool TryGetLayoutRects(string key, out Rectangle anchor, out Rectangle current)
        {
            anchor = Rectangle.Empty;
            current = Rectangle.Empty;

            if (key == DPadKey)
            {
                anchor = _dPadDefaultBounds;
                current = _dPadBounds;
                return _dPadActive;
            }
            if (key == LeftStickKey && _leftStick != null)
            {
                anchor = GetStickRect(_leftStick.AnchorCenter, _leftStick.Radius);
                current = GetStickRect(_leftStick.Center, _leftStick.Radius);
                return true;
            }
            if (key == RightStickKey && _rightStick != null)
            {
                anchor = GetStickRect(_rightStick.AnchorCenter, _rightStick.Radius);
                current = GetStickRect(_rightStick.Center, _rightStick.Radius);
                return true;
            }
            if (_controllerButton != null && _controllerButton.LayoutKey == key)
            {
                anchor = _controllerButton.LayoutAnchor;
                current = _controllerButton.Bounds;
                return true;
            }
            foreach (var button in _buttons)
            {
                if (button.LayoutKey == key)
                {
                    anchor = button.LayoutAnchor;
                    current = button.Bounds;
                    return true;
                }
            }
            return false;
        }

        // --------------------------------------------------------------------------------------------------------------------------------
        // LAYOUT EDITOR
        // --------------------------------------------------------------------------------------------------------------------------------
        public static void BeginEditMode()
        {
            if (EditMode)
                return;

            EditMode = true;
            _editKeepAlive = EditKeepAliveFrames;
            _dragTouchId = null;
            _dragKey = null;
            _lastTapKey = null;

            // Rebuild so TouchMovement 1 shows both the D-pad and the stick.
            Initialize(Game1.WindowWidth, Game1.WindowHeight, null);
        }

        public static void EndEditMode()
        {
            if (!EditMode)
                return;

            EditMode = false;
            _dragTouchId = null;
            _dragKey = null;
            _lastTapKey = null;

            Initialize(Game1.WindowWidth, Game1.WindowHeight, null);
            SettingsSaveLoad.SaveSettings();
        }

        // Called every frame by the page that owns the editor. If the page stops updating
        // (popped, menu closed by Back, etc.) edit mode ends on its own so touch input can't get stuck.
        public static void KeepEditModeAlive() => _editKeepAlive = EditKeepAliveFrames;

        private static void UpdateEditMode()
        {
            if (--_editKeepAlive < 0)
            {
                EndEditMode();
                return;
            }

            _editTime += Game1.DeltaTime;

            foreach (var button in _buttons)
                button.TouchId = null;
            if (_controllerButton != null) _controllerButton.TouchId = null;
            if (_leftStick != null) _leftStick.TouchId = null;
            if (_rightStick != null) _rightStick.TouchId = null;

            var touches = Game1.PlatformInput.Touches;

            // Continue the active drag.
            if (_dragTouchId != null)
            {
                bool foundTouch = false;

                for (int i = 0; i < touches.Count; i++)
                {
                    PlatformTouch touch = touches[i];

                    if (touch.Id != _dragTouchId.Value)
                        continue;

                    if (touch.State == PlatformTouchState.Released ||
                        touch.State == PlatformTouchState.Invalid)
                        break;

                    foundTouch = true;

                    Vector2 delta = touch.Position - _dragStartTouch;
                    float threshold = DragThreshold * ControlsScale;

                    if (!_dragMoved && delta.LengthSquared() >= threshold * threshold)
                        _dragMoved = true;

                    if (_dragMoved)
                        SetDraggedOffset(delta);

                    break;
                }

                if (!foundTouch)
                {
                    string tappedKey = _dragMoved ? null : _dragKey;
                    _dragTouchId = null;
                    _dragKey = null;

                    if (tappedKey == ControllerKey)
                    {
                        // Tapping the controller button (without dragging it) finishes editing.
                        EndEditMode();
                        return;
                    }
                    if (tappedKey != null)
                    {
                        // Double-tap a control to put it back at its default position.
                        if (tappedKey == _lastTapKey && _editTime - _lastTapTime <= DoubleTapTime)
                        {
                            GameSettings.TouchLayout.Remove(tappedKey);
                            ApplyLayout();
                            _lastTapKey = null;
                        }
                        else
                        {
                            _lastTapKey = tappedKey;
                            _lastTapTime = _editTime;
                        }
                    }
                }
            }

            // Pick up a control with a new touch.
            if (_dragTouchId == null)
            {
                for (int i = 0; i < touches.Count; i++)
                {
                    PlatformTouch touch = touches[i];

                    if (touch.State != PlatformTouchState.Pressed)
                        continue;

                    string key = HitTestLayoutKey(touch.Position.ToPoint());

                    if (key == null)
                        continue;

                    _dragTouchId = touch.Id;
                    _dragKey = key;
                    _dragStartTouch = touch.Position;
                    _dragMoved = false;

                    // Start from where the control is actually drawn, measured from its anchor. It may be sitting at a
                    // default that differs from the anchor, or at a clamped position.
                    _dragStartOffset = TryGetLayoutRects(key, out var anchor, out var current)
                        ? new Vector2(current.X - anchor.X, current.Y - anchor.Y) / ControlsScale
                        : Vector2.Zero;
                    break;
                }
            }

            UpdateEditAlpha();
        }

        private static void SetDraggedOffset(Vector2 delta)
        {
            float scale = ControlsScale;
            Vector2 offset = _dragStartOffset + delta / scale;

            if (!TryGetLayoutRects(_dragKey, out var anchor, out _))
            {
                GameSettings.TouchLayout[_dragKey] = offset;
                ApplyLayout();
                return;
            }

            // Snap the control's top-left corner to the grid (screen pixels, measured from the top-left of the screen).
            int grid = GameSettings.TouchGridSize;
            if (grid > 1)
            {
                Vector2 anchorPosition = new Vector2(anchor.X, anchor.Y);
                Vector2 position = anchorPosition + offset * scale;
                position.X = MathF.Round(position.X / grid) * grid;
                position.Y = MathF.Round(position.Y / grid) * grid;
                offset = (position - anchorPosition) / scale;
            }
            GameSettings.TouchLayout[_dragKey] = offset;

            // Store the clamped result so dragging past the screen edge doesn't leave a hidden overshoot.
            // The screen edges win over the grid, so a control can always sit flush against an edge.
            if (TryGetLayoutDelta(_dragKey, anchor, out Point clamped))
                GameSettings.TouchLayout[_dragKey] = clamped.ToVector2() / scale;

            ApplyLayout();
        }

        private static string HitTestLayoutKey(Point point)
        {
            // Same priority as normal input: the controller button always wins so editing can be exited.
            if (_controllerButton != null && _controllerButton.Contains(point))
                return ControllerKey;

            // Buttons are drawn last (on top), so test them first, topmost first.
            for (int i = _buttons.Count - 1; i >= 0; i--)
            {
                var button = _buttons[i];

                if (button.LayoutKey == DPadKey)
                    continue;

                if (button.Contains(point))
                    return button.LayoutKey;
            }

            if (_rightStick != null && _rightStick.Contains(point))
                return RightStickKey;

            if (_leftStick != null && _leftStick.Contains(point))
                return LeftStickKey;

            if (_dPadActive && _dPadBounds.Contains(point))
                return DPadKey;

            return null;
        }

        private static void UpdateEditAlpha()
        {
            // Every control is fully visible while editing, regardless of the opacity settings.
            float shadowAlpha = GameSettings.ShadowOpacity * 0.01f;

            foreach (var button in _buttons)
            {
                button.DisplayAlpha = 1f;
                button.ShadowAlpha = shadowAlpha;
            }
            if (_controllerButton != null)
            {
                _controllerButton.DisplayAlpha = 1f;
                _controllerButton.ShadowAlpha = shadowAlpha;
            }
            if (_leftStick != null)
            {
                _leftStick.DisplayAlpha = 1f;
                _leftStick.ShadowAlpha = shadowAlpha;
            }
            if (_rightStick != null)
            {
                _rightStick.DisplayAlpha = 1f;
                _rightStick.ShadowAlpha = shadowAlpha;
            }
            DPadButtonAlpha = 1f;
            DPadShadowAlpha = shadowAlpha;
        }

        private static void DrawEditGrid(SpriteBatch spriteBatch)
        {
            int grid = GameSettings.TouchGridSize;

            if (grid < EditGridMinDraw || Resources.SprWhite == null)
                return;

            Color color = EditGridColor;

            for (int x = grid; x < _screenWidth; x += grid)
                spriteBatch.Draw(Resources.SprWhite, new Rectangle(x, 0, 1, _screenHeight), color);

            for (int y = grid; y < _screenHeight; y += grid)
                spriteBatch.Draw(Resources.SprWhite, new Rectangle(0, y, _screenWidth, 1), color);
        }

        private static void DrawStick(SpriteBatch spriteBatch, VirtualStick stick)
        {
            if (stick == null)
                return;

            int baseSize = (int)(stick.Radius * 2f);
            int knobSize = (int)(stick.Radius * 1.1f);

            Rectangle baseRect = new Rectangle((int)(stick.Center.X - stick.Radius), (int)(stick.Center.Y - stick.Radius), baseSize, baseSize);
            Rectangle knobRect = new Rectangle((int)(stick.KnobPosition.X - knobSize / 2f), (int)(stick.KnobPosition.Y - knobSize / 2f), knobSize, knobSize);

            float baseAlpha = stick.DisplayAlpha;
            float knobAlpha = stick.DisplayAlpha;

            Point shadowOffset = GetShadowOffset();

            if (stick.BaseSprite != null)
            {
                Rectangle src = stick.BaseSprite.ScaledRectangle;
                float scaleX = baseRect.Width / (float)src.Width;
                float scaleY = baseRect.Height / (float)src.Height;
                Vector2 scale = new Vector2(scaleX, scaleY);

                Vector2 basePosition = new Vector2(baseRect.X, baseRect.Y);
                Vector2 baseShadowPosition = new Vector2(baseRect.X + shadowOffset.X, baseRect.Y + shadowOffset.Y);

                spriteBatch.Draw(stick.BaseSprite.Texture, baseShadowPosition, src, Color.Black * stick.ShadowAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(stick.BaseSprite.Texture, basePosition, src, Color.White * baseAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            else
            {
                spriteBatch.Draw(Resources.SprWhite, baseRect, Color.White * baseAlpha);
            }
            // Draw moving head texture
            if (stick.HeadSprite != null)
            {
                Rectangle src = stick.HeadSprite.ScaledRectangle;
                float scaleX = knobRect.Width / (float)src.Width;
                float scaleY = knobRect.Height / (float)src.Height;
                Vector2 scale = new Vector2(scaleX, scaleY);

                Vector2 knobPosition = new Vector2(knobRect.X, knobRect.Y);
                Vector2 knobShadowPosition = new Vector2(knobRect.X + shadowOffset.X, knobRect.Y + shadowOffset.Y);

                spriteBatch.Draw(stick.HeadSprite.Texture, knobShadowPosition, src, Color.Black * stick.ShadowAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(stick.HeadSprite.Texture, knobPosition, src, Color.White * knobAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            else
            {
                spriteBatch.Draw(Resources.SprWhite, knobRect, Color.White * knobAlpha);
            }
        }
    }
}
