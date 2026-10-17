using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.Base.UI;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Overlay
{
    public class TextboxOverlay
    {
        struct ChoiceButton
        {
            public float Percentage;
            public float SelectionPercentage;
        }

        public Rectangle DialogBoxTextBox;

        public bool IsOpen;
        public bool OwlMode;
        public bool UpdateObjects;

        public float TransitionState => _currentOpacity;

        private int _scrollSpeed = 20;

        private readonly Animator _animator;

        private readonly UiRectangle _textboxBackground;
        private readonly UiRectangle[] _textboxBackgroundChoice = new UiRectangle[4];
        private readonly ChoiceButton[] _textboxChoice = new ChoiceButton[4];

        private Rectangle _dialogBoxRectangle;
        private Point _letterSize;

        private string _strFullText;
        private string _strDialog;

        private string _choiceKey;
        private string[] _choices;

        private float _textScrollCounter;
        private float _textMult;
        private float _textboxOffsetY;
        private float _currentOpacity;
        private const float TransitionSpeed = 0.15f;
        private int _textOffsetY;

        private float _selectionCounter;
        private const int SelectionTime = 250;

        private int _currentDialogCount;
        private int _paddingLeft = 5;
        private int _paddingRight = 12;
        private int _paddingV = 5;
        private int _textboxMargin = 16;
        private int _dialogBoxWidth = 200;
        private int _dialogBoxHeight;
        private int _currentState;
        private int _currentLine;
        private int _currentLineAddition;

        private int MaxCharacters;
        private int MaxLines;

        private float _choicePercentage;
        private int _currentChoiceSelection;
        private int _choiceWidth;

        private bool _running = true;
        private bool _end;
        private bool _isChoice;
        private bool _openDialog = false;
        private bool _boxEnd;
        private bool _skipToEnd;

        private string _scrollText;
        private float _scrollCounter;
        private const int ScrollTime = 125;
        private bool _textScrolling;

        public UiRectangle TextboxBackground => _textboxBackground;
        public UiRectangle[] TextboxBackgroundChoice => _textboxBackgroundChoice;

        private float _uiScale = 4;
        private Color _fontColor;
        private Color _backgroundColor;

        // Values configurable via lahdmod.
        private float textbox_scale            = 0;
        private int   max_textbox_characters   = 26;
        private int   max_textbox_lines        = 3;
        private int   textbox_font_color_red   = 248;
        private int   textbox_font_color_grn   = 248;
        private int   textbox_font_color_blu   = 136;
        private int   textbox_background_red   = 0;
        private int   textbox_background_grn   = 0;
        private int   textbox_background_blu   = 0;
        private float textbox_background_alpha = 0.85f;

        public TextboxOverlay()
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "TextboxOverlay.lahdmod");
            ModFile.Parse(modFile, this);

            MaxCharacters = max_textbox_characters;
            MaxLines = max_textbox_lines;

            // If background blur was disabled and/or the user adjusted the value, use the
            // value assigned otherwise blur is disabled so we want to make it nearly opaque.
            var alpha = !GameSettings.DisableUiBlur || textbox_background_alpha != 0.85f
                ? textbox_background_alpha
                : 0.92f;

            _fontColor = new Color(textbox_font_color_red, textbox_font_color_grn, textbox_font_color_blu);
            _backgroundColor = new Color(textbox_background_red, textbox_background_grn, textbox_background_blu) * alpha;

            _animator = AnimatorSaveLoad.LoadAnimator("dialog_arrow");

            _textboxBackground = new UiRectangle(Rectangle.Empty, "textboxblur", Values.ScreenNameGame, Color.Transparent, Color.Transparent, null) { Radius = Values.UiTextboxRadius };
            Game1.UiManager.AddElement(_textboxBackground, true);

            for (var i = 0; i < _textboxBackgroundChoice.Length; i++)
            {
                _textboxBackgroundChoice[i] = new UiRectangle(Rectangle.Empty, "", Values.ScreenNameGame, Color.Transparent, Color.Transparent, null) { Radius = Values.UiTextboxRadius };
                Game1.UiManager.AddElement(_textboxBackgroundChoice[i], true);
            }
        }

        public void Init()
        {
            IsOpen = false;

            _currentOpacity = 0;
            UpdateTextBoxState();
        }

        public void Update()
        {
            if (MapManager.ObjLink.IsTransitioning)
                return;

            // Fade textbox in or out depending on IsOpen.
            if (IsOpen && _currentOpacity < 1)
                _currentOpacity += TransitionSpeed * Game1.TimeMultiplier;
            else if (!IsOpen && _currentOpacity > 0)
                _currentOpacity -= TransitionSpeed * Game1.TimeMultiplier;
            _currentOpacity = MathHelper.Clamp(_currentOpacity, 0, 1);

            // If fully faded out, close logical dialog state.
            if (_currentOpacity <= 0 && _openDialog)
            {
                _openDialog = false;
                Game1.GameManager.SaveManager.SetString("dialogOpen", "0");
            }
            // When fully visible, allow dialog logic to run.
            if (_currentOpacity >= 1)
                UpdateDialogBox();

            // Apply slide-in easing animation from below.
            _textboxOffsetY = (float)Math.Sin((1 - _currentOpacity) * Math.PI / 2) * 3 * _uiScale;

            UpdateTextBoxState();

            // Update choice display animations when needed.
            if (_isChoice && !_running && _end)
            {
                _choicePercentage = AnimationHelper.MoveToTarget(_choicePercentage, 1, 0.075f * Game1.TimeMultiplier);

                _choiceWidth = 0;
                for (var i = 0; i < _choices.Length; i++)
                {
                    var width = (int)((DrawHelper.MeasureString("" + _choices[i] + "").X + 8) * _uiScale);
                    if (_choiceWidth < width)
                        _choiceWidth = width;
                }

                for (var i = 0; i < _choices.Length; i++)
                {
                    if (_choicePercentage >= i * 0.25f)
                        _textboxChoice[i].Percentage = AnimationHelper.MoveToTarget(_textboxChoice[i].Percentage, 1, TransitionSpeed * Game1.TimeMultiplier);

                    _textboxChoice[i].SelectionPercentage = AnimationHelper.MoveToTarget(_textboxChoice[i].SelectionPercentage, _currentChoiceSelection == i ? 1 : 0, TransitionSpeed * Game1.TimeMultiplier);

                    var choicePositionY = _dialogBoxHeight + 1;
                    var padding = (int)(_textboxChoice[i].SelectionPercentage * _uiScale);

                    _textboxBackgroundChoice[i].BackgroundColor = Color.Lerp(_backgroundColor, _fontColor, _textboxChoice[i].SelectionPercentage) * _currentOpacity * _textboxChoice[i].Percentage;
                    _textboxBackgroundChoice[i].BlurColor = Values.TextboxBlurColor * _currentOpacity * _textboxChoice[i].Percentage;
                    _textboxBackgroundChoice[i].Rectangle = new Rectangle(
                        (int)(_dialogBoxRectangle.X + _dialogBoxRectangle.Width - _choiceWidth * (_choices.Length - i) - (3 * _uiScale) * (_choices.Length - 1 - i) - padding - _uiScale),
                        (int)(_dialogBoxRectangle.Y + choicePositionY * _uiScale + (int)_textboxOffsetY - padding + (int)(Math.Sin((1 - _textboxChoice[i].Percentage) * Math.PI / 2) * 4 * _uiScale)),
                        _choiceWidth + 2 * padding, 
                        (int)((Resources.GameFontHeight + 4) * _uiScale + 2 * padding));
                }
            }
            // Freeze or unfreeze game logic as needed.
            UpdateGameState();
        }

        private void UpdateTextBoxState()
        {
            // set textbox fade in from the bottom
            _textboxBackground.BackgroundColor = _backgroundColor * _currentOpacity;
            _textboxBackground.BlurColor = Values.TextboxBlurColor * _currentOpacity;
            _textboxBackground.Rectangle.Y = _dialogBoxRectangle.Y + (int)_textboxOffsetY;
        }

        private void UpdateGameState()
        {
            // this function gets called by StartDialogX and Freezes the game when a dialog is loaded for the map we are transitioning in; not updating the transition for one frame
            if (MapManager.ObjLink.IsTransitioning)
                return;

            if (_openDialog && !UpdateObjects)
                Game1.UpdateGame = false;

            // needs to be called before the UpdateDialogBox method to ensure the player is frozen between a dialog box and a [freeze:x] coming after a [wait:dialogOpen:1]
            if (_openDialog && UpdateObjects)
                MapManager.ObjLink.FreezePlayer();
        }

        public void DrawTop(SpriteBatch spriteBatch)
        {
            if (_currentOpacity <= 0)
                return;

            var scrollOffset = 0f;
            if (_textScrolling)
            {
                var scrollPercentage = _scrollCounter / ScrollTime;
                scrollOffset = scrollPercentage * Resources.GameFontHeight * _uiScale;

                DrawHelper.DrawString(spriteBatch, _scrollText, new Vector2(DialogBoxTextBox.X, DialogBoxTextBox.Y + _textboxOffsetY - Resources.GameFontHeight * _uiScale + scrollOffset + _textOffsetY * _uiScale), _fontColor * _currentOpacity * Math.Clamp(scrollPercentage * 2f - 1f, 0, 1), 0, Vector2.Zero, _uiScale, SpriteEffects.None, 0);
            }

            // draw the dialog box
            DrawHelper.DrawString(spriteBatch, _strDialog, new Vector2(DialogBoxTextBox.X, DialogBoxTextBox.Y + _textboxOffsetY + scrollOffset + _textOffsetY * _uiScale), _fontColor * _currentOpacity, 0, Vector2.Zero, _uiScale, SpriteEffects.None, 0);

            if (!_running && !_end)
            {
                _animator.DrawBasic(spriteBatch, new Vector2(_dialogBoxRectangle.Right - _uiScale * 2, _dialogBoxRectangle.Bottom - _uiScale * 2), Color.White, _uiScale);
            }

            // show the choices if the text is fully shown
            if (_isChoice && !_running && _end)
            {
                for (var i = 0; i < _choices.Length; i++)
                {
                    var textSize = DrawHelper.MeasureString(_choices[i]);
                    var color = Color.Lerp(_fontColor, _backgroundColor, _textboxChoice[i].SelectionPercentage);
                    var posX = _textboxBackgroundChoice[i].Rectangle.X + _textboxBackgroundChoice[i].Rectangle.Width / 2 - (textSize.X * _uiScale) / 2;
                    var posY = _textboxBackgroundChoice[i].Rectangle.Y + _textboxBackgroundChoice[i].Rectangle.Height / 2 - (textSize.Y * _uiScale) / 2 + _uiScale;

                    DrawHelper.DrawString(spriteBatch, _choices[i], new Vector2((int)posX, (int)posY), color * _currentOpacity * _textboxChoice[i].Percentage, 0, Vector2.Zero, _uiScale, SpriteEffects.None, 0);
                }
            }
        }

        public void UpdateDialogBox()
        {
            // Update the text animation.
            _animator.Update();

            // It's a choice dialog at the end of the text so update the choice.
            if (_isChoice && !_running && _end)
                ChoiceUpdate();

            // If text is scrolling.
            if (_textScrolling)
            {
                // Subtract from scroll counter until end is areached.
                _scrollCounter -= Game1.DeltaTime;
                if (_scrollCounter <= 0)
                {
                    _textScrolling = false;
                    _scrollCounter = 0;
                }
                return;
            }
            // Detect confirm, cancel, and potentially start button if dialog skip is enabled in options.
            var confirmPressed = ControlHandler.ButtonPressed(ControlHandler.ConfirmButton);
            var cancelPressed = ControlHandler.ButtonPressed(ControlHandler.CancelButton);
            var startPressed = ControlHandler.ButtonPressed(CButtons.Start) && GameSettings.DialogSkip;

            // If pressing the start button rapidly skip through the text.
            if (startPressed)
            {
                _skipToEnd = true;
                _scrollSpeed = 100;
            }
            // Button was pressed to continue the textbox or it was skipped.
            if (confirmPressed || cancelPressed || startPressed || _skipToEnd)
            {
                // The end of the text has been reached.
                if (_end)
                {
                    // Close if not a choice, or select the choice if it is.
                    if (!_isChoice || (_isChoice && confirmPressed))
                    {
                        OwlMode = false;
                        IsOpen = false;
                        InputHandler.ResetInputState();

                        // Save the current choice (key value pair) to the SaveManager dictionary.
                        if (_isChoice && confirmPressed)
                            Game1.GameManager.SaveManager.SetString(_choiceKey, _currentChoiceSelection.ToString());

                        // Fix for stuck block/shield button
                        MapManager.ObjLink.ToggleBlockButton(false);
                    }
                }
                // Not the end of the text.
                else
                {
                    // Text was waiting for button press.
                    if (!_running)
                    {
                        // Set speed and continue printing text.
                        _textMult = _skipToEnd ? 350 : 1;
                        _running = true;

                        // If at the end of the textbox.
                        if (_boxEnd)
                        {
                            // Clear the textbox.
                            _currentLine = 0;
                            _currentState += _currentDialogCount + 1;
                            _currentDialogCount = 0;
                            _textScrollCounter = -150;
                            _strDialog = "";
                            _textOffsetY = CalculateTextOffsetY(_strFullText);
                        }
                        else
                            _currentLineAddition = 0;
                    }
                    // Button press speeds up printing text.
                    else
                        _textMult = _skipToEnd ? 350 : 4;
                }
            }
            // Scroll the text if currently printing text using the multiplier.
            if (_running)
                _textScrollCounter += Game1.DeltaTime * _textMult;

            // If there is room print the next letter (I think).
            var updated = false;
            while (_running && _currentState + _currentDialogCount < _strFullText.Length && _textScrollCounter > _scrollSpeed)
            {
                updated = true;
                _textScrollCounter -= _scrollSpeed;
                NextLetter(false);
            }
            if (updated)
                _strDialog = _strFullText.Substring(_currentState, _currentDialogCount);

            // The end of the dialog was reached so play a sound and update appropriate variables.
            if (_running && _currentState + _currentDialogCount >= _strFullText.Length)
            {
                _end = true;
                _running = false;
                Game1.AudioManager.PlaySoundEffect("D360-21-15");
            }
        }

        private void NextLetter(bool fastForward)
        {
            if (_strFullText[_currentState + _currentDialogCount] == '\f')
            {
                _animator.Stop();
                _animator.Play("idle");

                _boxEnd = true;
                _running = false;
                _currentLineAddition = MaxLines;

                return;
            }

            // line break?
            if (_strFullText[_currentState + _currentDialogCount] == '\n')
            {
                if (_currentLine + 1 < MaxLines)
                    _currentLine++; 
                else if (_currentLineAddition < MaxLines)
                {
                    _currentLineAddition++;
                    _textScrolling = true;
                    _scrollCounter = ScrollTime;

                    var offset = _strFullText.IndexOf('\n', _currentState) - _currentState + 1;
                    _scrollText = _strFullText.Substring(_currentState, offset);
                    _strDialog = _strFullText.Substring(_currentState, _currentDialogCount);

                    _currentState += offset;
                    _currentDialogCount -= offset;
                }
                else
                {
                    _animator.Stop();
                    _animator.Play("idle");

                    _boxEnd = false;
                    _running = false;

                    if (!fastForward)
                        Game1.AudioManager.PlaySoundEffect("D360-21-15");
                    return;
                }
            }
            // Get the current key that was used to get the dialog string and check if it's the monkey.
            bool isMonkey = Game1.GameManager.CurrentDialogKey.StartsWith("castle_monkey");

            if (!fastForward && isMonkey && _running && _currentDialogCount % 7 == 0)
                Game1.AudioManager.PlaySoundEffect("D370-20-14", true);

            else if (!fastForward && !OwlMode && _running && _currentDialogCount % 6 == 0)
                Game1.AudioManager.PlaySoundEffect("D370-15-0F", true);

            else if (!fastForward && OwlMode && _running && _currentDialogCount % 28 == 0)
                Game1.AudioManager.PlaySoundEffect("D370-25-19", true);

            _currentDialogCount++;
        }

        public void ChoiceUpdate()
        {
            var newSelection = _currentChoiceSelection;
            var direction = ControlHandler.GetMoveVector2();
            var dir = AnimationHelper.GetDirection(direction);

            if (direction.Length() > 0 && (dir == 0 || dir == 2))
            {
                _selectionCounter -= Game1.DeltaTime;

                if (_selectionCounter <= 0)
                {
                    _selectionCounter += SelectionTime;

                    if (dir == 0)
                        newSelection--;
                    else if (dir == 2)
                        newSelection++;
                }
            }
            else
            {
                _selectionCounter = 0;
            }

            newSelection = MathHelper.Clamp(newSelection, 0, _choices.Length - 1);

            if (_currentChoiceSelection != newSelection)
            {
                _currentChoiceSelection = newSelection;
                Game1.AudioManager.PlaySoundEffect("D360-10-0A");
            }
        }

        public void ResolutionChange()
        {
            if (textbox_scale > 0)
                _uiScale = textbox_scale;
            else
                _uiScale = Game1.UiScale;
            SetUpDialogBox();
        }

        public void SetUpDialogBox()
        {
            // only works if every letter has the same size
            _letterSize = new Point((int)DrawHelper.MeasureString("A").X, (int)DrawHelper.MeasureString("A").Y);

            _dialogBoxWidth = _letterSize.X * MaxCharacters + _paddingLeft + _paddingRight;
            _dialogBoxHeight = _letterSize.Y * MaxLines + _paddingV * 2;

            _dialogBoxRectangle = new Rectangle(
                (int)(Game1.WindowWidth / 2 - _dialogBoxWidth * _uiScale / 2),
                (int)(Game1.WindowHeight - (_uiScale * _dialogBoxHeight) - _uiScale * _textboxMargin), (int)(_dialogBoxWidth * _uiScale), (int)(_dialogBoxHeight * _uiScale));

            DialogBoxTextBox = new Rectangle(
                (int)(_dialogBoxRectangle.X + _paddingLeft * _uiScale),
                (int)(_dialogBoxRectangle.Y + _paddingV * _uiScale),
                (int)(_dialogBoxRectangle.Width - (_paddingLeft + _paddingRight) * _uiScale),
                (int)(_dialogBoxRectangle.Height - (_paddingV * 2) * _uiScale));

            _textboxBackground.Rectangle = _dialogBoxRectangle;
        }

        public void StartDialog(string dialogText)
        {
            _openDialog = true;
            Game1.GameManager.SaveManager.SetString("dialogOpen", "1");

            _currentLine = 0;
            _currentLineAddition = MaxLines;
            _currentState = 0;
            _currentDialogCount = 0;
            _textMult = 1;
            _skipToEnd = false;
            _scrollSpeed = 20;
            _textScrollCounter = 0;

            _strFullText = SetUpString(dialogText);
            _textOffsetY = CalculateTextOffsetY(_strFullText);

            _strDialog = "";
            _end = false;
            IsOpen = true;
            _running = true;

            _isChoice = false;
            ResetChoice();

            // needs to be called to make sure to freeze the game directly after the dialog was started
            UpdateGameState();
        }

        public void StartChoice(string choiceKey, string choiceText, params string[] choices)
        {
            _openDialog = true;
            Game1.GameManager.SaveManager.SetString("dialogOpen", "1");

            _currentLine = 0;
            _currentLineAddition = MaxLines;
            _currentState = 0;
            _currentDialogCount = 0;
            _textMult = 1;
            _skipToEnd = false;
            _scrollSpeed = 20;
            _textScrollCounter = 0;

            _choiceKey = choiceKey;
            _strFullText = SetUpString(choiceText);
            _textOffsetY = CalculateTextOffsetY(_strFullText);
            _choices = choices;

            _strDialog = "";
            _end = false;
            IsOpen = true;
            _running = true;

            _isChoice = true;
            ResetChoice();

            // needs to be called to make sure to freeze the game directly after the dialog was started
            UpdateGameState();
        }

        private void ResetChoice()
        {
            _choicePercentage = 0;
            for (var i = 0; i < _textboxChoice.Length; i++)
            {
                _textboxChoice[i].Percentage = 0;
                _textboxChoice[i].SelectionPercentage = 0;

                _textboxBackgroundChoice[i].BlurColor = Color.Transparent;
                _textboxBackgroundChoice[i].BackgroundColor = Color.Transparent;
            }
            _currentChoiceSelection = 0;
            _textboxChoice[0].SelectionPercentage = 1;
        }

        public string ReplaceKeys(string inputString)
        {
            string outputString = inputString;
            int openIndex;
            int closeIndex;

            do
            {
                openIndex = inputString.IndexOf('[');
                closeIndex = openIndex + 1;
                if (openIndex != -1)
                {
                    closeIndex = inputString.IndexOf(']', closeIndex + 1);
                    if (closeIndex != -1)
                    {
                        var stringKey = inputString.Substring(openIndex + 1, closeIndex - openIndex - 1);
                        var value = Game1.GameManager.SaveManager.GetString(stringKey);
                        if (value != null)
                        {
                            inputString = inputString.Remove(openIndex, closeIndex - openIndex + 1);
                            inputString = inputString.Insert(openIndex, value);
                        }
                    }
                    else
                        break;
                }
            } while (openIndex != -1);

            return outputString;
        }

        public string SetUpString(string inputString)
        {
            inputString = ReplaceKeys(inputString);
            return SetUpStringSplit(inputString);
        }

        public int CalculateTextOffsetY(string inputString)
        {
            // center text if \f is used
            var fIndex = inputString.IndexOf('\f', _currentState);
            if (fIndex > 0)
                inputString = inputString.Substring(_currentState, -_currentState + fIndex);
            else
                inputString = inputString.Substring(_currentState);

            var lineBreaks = inputString.Count(c => c == '\n');

            if (lineBreaks < 2)
                return ((MaxLines - lineBreaks - 1) * Resources.GameFontHeight) / 2;
            else
                return 0;
        }

        public string SetUpStringSplit(string inputString)
        {
            if (inputString == null)
                return "Error";

            inputString = inputString.Replace("\\n", "\n");
            inputString = inputString.Replace("\\f", "\f");

            var outString = "";
            var currentState = 0;
            var lines = 0;

            while (currentState < inputString.Length)
            {
                lines++;
                var subString = inputString.Substring(currentState, Math.Min(MaxCharacters, inputString.Length - currentState));

                var indexN = subString.IndexOf('\n');
                var indexF = subString.IndexOf('\f');
                var indexC = subString.IndexOf("{");

                indexN = indexN == -1 ? 999 : indexN;
                indexF = indexF == -1 ? 999 : indexF;
                indexC = indexC == -1 ? 999 : indexC;

                // add a new line
                if (indexC != 999 && indexC < indexN && indexC < indexF)
                {
                    // finish the line
                    if (indexC != 0)
                    {
                        outString += subString.Substring(0, indexC) + "\n";
                        currentState += indexC;
                        continue;
                    }

                    var closeIndex = subString.IndexOf('}', indexC);
                    if (closeIndex != -1)
                    {
                        var strCenter = subString.Substring(indexC + 1, closeIndex - (indexC + 1));
                        for (var i = 0; i < (MaxCharacters - strCenter.Length) / 2; i++)
                            outString += " ";

                        outString += strCenter;
                        // new line when there is still text and no new textbox
                        if (currentState + closeIndex + 1 < inputString.Length &&
                            inputString[currentState + closeIndex + 1] != '\f')
                            outString += "\n";

                        currentState += closeIndex + 1;
                    }
                    else
                    {
                        // this should not happen
                        currentState += MaxCharacters;
                        outString += subString;
                    }
                }
                else if (indexN != 999 && indexN < indexC && indexN < indexF)
                {
                    currentState = currentState + indexN + 1;
                    outString += subString.Substring(0, indexN + 1);
                }
                // add empty text
                else if (indexF != 999)
                {
                    currentState = currentState + indexF + 1;
                    outString += subString.Substring(0, indexF) + "\f";
                }
                // finished?
                else if (inputString.Length - currentState <= MaxCharacters)
                {
                    outString += subString;
                    break;
                }
                else
                {
                    // find a " " to add a line break
                    var splitString = false;
                    for (var i = 0; i < MaxCharacters; i++)
                    {
                        if (inputString[currentState + MaxCharacters - i] == ' ')
                        {
                            splitString = true;
                            outString += inputString.Substring(currentState, MaxCharacters - i) + "\n";
                            currentState = currentState + MaxCharacters - i + 1;
                            break;
                        }
                    }

                    if (!splitString)
                    {
                        outString += inputString.Substring(currentState, MaxCharacters) + "\n";
                        currentState = currentState + MaxCharacters;
                    }
                }
            }

            return outString;
        }
    }
}
