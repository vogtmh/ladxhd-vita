using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;

namespace ProjectZ.InGame.Interface
{
    public class InterfaceListLayout : InterfaceElement
    {
        public List<InterfaceElement> Elements = new List<InterfaceElement>();

        public Gravities ContentAlignment = Gravities.Center;

        public bool HorizontalMode;
        public bool AutoSize;
        public bool PreventSelection;

        public bool Scrollable;
        public Color ScrollbarColor = new Color(90, 110, 170);
        public Color ScrollbarTrackColor = new Color(95, 95, 95);

        public int SelectionIndex => _selectionIndex;

        public InterfaceToggle Toggle
        {
            get
            {
                for (var i = 0; i < Elements.Count; i++)
                    if (Elements[i] is InterfaceToggle toggle)
                        return toggle;
                return null;
            }
        }

        private int _selectionIndex;
        private int _width;
        private int _height;

        private float _scrollOffset;
        private float _scrollTarget;

        private int _clipPaddingTop = 0;
        private int _clipPaddingBottom = 6;

        private int ViewHeight => Size.Y + _clipPaddingTop + _clipPaddingBottom;

        public InterfaceListLayout() { }

        public override void Update()
        {
            base.Update();

            foreach (var element in Elements)
                element.Update();

            // Ease the applied scroll toward the target each frame.
            if (Scrollable && !HorizontalMode)
            {
                var max = Math.Max(0, _height - ViewHeight);
                _scrollTarget = MathHelper.Clamp(_scrollTarget, 0, max);

                var t = MathHelper.Clamp(Game1.DeltaTime / 80f, 0f, 1f);
                _scrollOffset = MathHelper.Lerp(_scrollOffset, _scrollTarget, t);

                if (Math.Abs(_scrollOffset - _scrollTarget) < 0.5f)
                    _scrollOffset = _scrollTarget;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency)
        {
            // Look for changes.
            foreach (var element in Elements)
            {
                if (element.ChangeUp)
                {
                    Recalculate = true;
                    element.ChangeUp = false;
                }
            }

            // Recalculate the position of the elements if needed.
            if (Recalculate)
                CalculatePosition();

            base.Draw(spriteBatch, drawPosition, scale, transparency);

            // Non-scrolling path: unchanged from the original behavior.
            if (!Scrollable || HorizontalMode)
            {
                foreach (var element in Elements)
                {
                    if (element.Visible && !element.Hidden)
                        element.Draw(spriteBatch, element.Position.ToVector2() * scale + drawPosition, scale, transparency);
                }
                return;
            }

            // Scrolling path
            var device = spriteBatch.GraphicsDevice;
            var prevScissor = device.ScissorRectangle;
            var prevRasterizer = ActiveRasterizer;

            // Flush whatever batch is currently open so the scissor change takes effect.
            spriteBatch.End();

            // Seed from the render target bounds rather than the device's current scissor rect.
            var outerBounds = device.Viewport.Bounds;
            device.ScissorRectangle = Rectangle.Intersect(ComputeScreenClip(drawPosition, scale), outerBounds);

            // Children re-open the batch themselves, so they need the scissor state too.
            ActiveRasterizer = UiScissorRasterizer;
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, ActiveRasterizer, null, Game1.GetMatrix);

            var applied = (float)Math.Round(_scrollOffset);
            var offset = new Vector2(0, applied * scale);

            foreach (var element in Elements)
            {
                if (!element.Visible || element.Hidden)
                    continue;

                // Cull anything fully outside the viewport; the scissor handles partials.
                var top = element.Position.Y - applied;
                var bottom = top + element.Size.Y;
                if (bottom < -_clipPaddingTop || top > Size.Y + _clipPaddingBottom)
                    continue;

                element.Draw(spriteBatch, element.Position.ToVector2() * scale + drawPosition - offset, scale, transparency);
            }

            // Close the clipped batch and restore the previous state.
            spriteBatch.End();
            ActiveRasterizer = prevRasterizer;
            device.ScissorRectangle = prevScissor;

            // Reopen a batch for whatever the caller draws next (e.g. the bottom bar).
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, ActiveRasterizer, null, Game1.GetMatrix);

            // Draw the scrollbar unclipped so it sits cleanly at the edge.
            DrawScrollbar(spriteBatch, drawPosition, scale, transparency);
        }

        public int MoveSelection(int steps, bool playSound)
        {
            if (Elements.Count == 0 || steps == 0)
                return 0;

            int dir = Math.Sign(steps);
            int remaining = Math.Abs(steps);
            int index = _selectionIndex;
            int moved = 0;

            while (remaining > 0)
            {
                int next = index + dir;

                // Skip over any non-selectable / hidden elements in this direction.
                while (next >= 0 && next < Elements.Count &&
                       (!Elements[next].Selectable || !Elements[next].Visible))
                    next += dir;

                // Ran off the end without finding another selectable element: stop here.
                if (next < 0 || next >= Elements.Count)
                    break;

                index = next;
                moved++;
                remaining--;
            }

            if (moved == 0)
                return 0;

            Elements[_selectionIndex].Deselect(true);
            _selectionIndex = index;
            Elements[_selectionIndex].Select(
                dir < 0 ? (HorizontalMode ? Directions.Right : Directions.Down)
                        : (HorizontalMode ? Directions.Left : Directions.Top), true);

            EnsureSelectionVisible();

            if (playSound)
                Game1.AudioManager.PlaySoundEffect("D360-10-0A");

            return moved;
        }

        public override InputEventReturn PressedButton(CButtons pressedButton)
        {
            var eValue = Elements[_selectionIndex].PressedButton(pressedButton);

            // return if the upper element reacted to the button press
            if (eValue != InputEventReturn.Nothing)
                return eValue;

            var direction = 0;

            if (HorizontalMode ? ControlHandler.MenuButtonDown(CButtons.Left) : ControlHandler.MenuButtonDown(CButtons.Up))
                direction = -1;

            if (HorizontalMode ? ControlHandler.MenuButtonDown(CButtons.Right) : ControlHandler.MenuButtonDown(CButtons.Down))
                direction = 1;

            if (direction == 0)
                return InputEventReturn.Nothing;

            // move selections
            Elements[_selectionIndex].Deselect(true);

            var rValue = InputEventReturn.Something;

            do
            {
                _selectionIndex += direction;

                if (_selectionIndex < 0)
                {
                    rValue = PreventSelection ? InputEventReturn.Something : InputEventReturn.Nothing;
                    _selectionIndex = Elements.Count - 1;
                }
                else if (_selectionIndex >= Elements.Count)
                {
                    rValue = PreventSelection ? InputEventReturn.Something : InputEventReturn.Nothing;
                    _selectionIndex = 0;
                }

            } while (!Elements[_selectionIndex].Selectable || !Elements[_selectionIndex].Visible);

            if (direction < 0)
                Elements[_selectionIndex].Select(HorizontalMode ? Directions.Right : Directions.Down, true);
            else
                Elements[_selectionIndex].Select(HorizontalMode ? Directions.Left : Directions.Top, true);

            EnsureSelectionVisible();

            Game1.AudioManager.PlaySoundEffect("D360-10-0A");

            return rValue;
        }

        public override void Select(Directions direction, bool animate)
        {
            if (!Selectable || Elements.Count == 0)
                return;

            var dir = 1;

            if (!HorizontalMode && direction == Directions.Down ||
                HorizontalMode && direction == Directions.Right)
            {
                _selectionIndex = Elements.Count - 1;
                dir = -1;
            }
            else if (!HorizontalMode && direction == Directions.Top ||
                HorizontalMode && direction == Directions.Left)
            {
                _selectionIndex = 0;
                dir = 1;
            }
            else
            {
                // The incoming direction doesn't match this list's own orientation - e.g. a HorizontalMode list being entered with Top/Down from a
                // vertical parent. Neither branch above applies, so don't just keep scanning from whatever _selectionIndex happened to be left at
                // from a previous visit: if it's stale (points at something no longer Selectable/Visible, or is out of range because Elements changed),
                // that stale scan is exactly what walked off the array before. Keep it only if it's still a valid, currently-selectable target; otherwise
                // restart the scan from the beginning.
                if (_selectionIndex < 0 || _selectionIndex >= Elements.Count ||
                    !Elements[_selectionIndex].Selectable || !Elements[_selectionIndex].Visible)
                {
                    _selectionIndex = 0;
                    dir = 1;
                }
            }

            // Find a selectable item in the list.
            while (_selectionIndex >= 0 && _selectionIndex < Elements.Count &&
                   (!Elements[_selectionIndex].Selectable || !Elements[_selectionIndex].Visible))
                _selectionIndex += dir;

            // Nothing selectable/visible in this list at all, so bail out instead of indexing an out of bounds index.
            if (_selectionIndex < 0 || _selectionIndex >= Elements.Count)
            {
                _selectionIndex = MathHelper.Clamp(_selectionIndex, 0, Elements.Count - 1);
                return;
            }

            Elements[_selectionIndex].Select(direction, animate);

            EnsureSelectionVisible();

            base.Select(direction, animate);
        }

        public void SetSelectionIndex(int index)
        {
            _selectionIndex = MathHelper.Clamp(index, 0, Elements.Count - 1);
            EnsureSelectionVisible();
        }

        public void Select(int index, bool animate)
        {
            if (Elements.Count == 0)
                return;

            // Deselect the current entry before jumping, so no stale highlight is left behind.
            if (_selectionIndex >= 0 && _selectionIndex < Elements.Count)
                Elements[_selectionIndex].Deselect(false);

            _selectionIndex = MathHelper.Clamp(index, 0, Elements.Count - 1);
            Elements[_selectionIndex].Select(Directions.Left, animate);
            EnsureSelectionVisible();
        }

        public override void Deselect(bool animate)
        {
            if (!Selectable)
                return;

            if (_selectionIndex >= 0 && _selectionIndex < Elements.Count)
                Elements[_selectionIndex].Deselect(animate);

            base.Deselect(animate);
        }

        public InterfaceElement AddElement(InterfaceElement element)
        {
            Recalculate = true;
            Elements.Add(element);
            return element;
        }

        public InterfaceElement AddElement(int index, InterfaceElement element)
        {
            index = MathHelper.Clamp(index, 0, Elements.Count);

            Elements.Insert(index, element);

            if (Elements.Count == 1)
                _selectionIndex = 0;
            
            else if (index <= _selectionIndex)
                _selectionIndex++;

            Recalculate = true;
            return element;
        }

        public bool RemoveElement(InterfaceElement element)
        {
            int index = Elements.IndexOf(element);

            if (index < 0)
                return false;

            bool wasSelected = (index == _selectionIndex);

            if (wasSelected && Elements.Count > 1)
                element.Deselect(false);

            Elements.RemoveAt(index);

            if (Elements.Count == 0)
                _selectionIndex = 0;
            
            else
            {
                if (_selectionIndex >= Elements.Count)
                    _selectionIndex = Elements.Count - 1;

                if (wasSelected)
                {
                    int start = _selectionIndex;
                    do
                    {
                        if (Elements[_selectionIndex].Selectable &&
                            Elements[_selectionIndex].Visible)
                        {
                            Elements[_selectionIndex].Select(Directions.Top, false);
                            break;
                        }
                        _selectionIndex++;
                        if (_selectionIndex >= Elements.Count)
                            _selectionIndex = 0;

                    }
                    while (_selectionIndex != start);
                }
            }
            Recalculate = true;
            return true;
        }

        public InterfaceElement ReplaceElement(InterfaceElement oldElement, InterfaceElement newElement)
        {
            int index = Elements.IndexOf(oldElement);

            if (index < 0)
                return null;

            bool wasSelected = (_selectionIndex == index);

            if (wasSelected)
                oldElement.Deselect(false);

            Elements[index] = newElement;

            if (wasSelected && newElement.Selectable && newElement.Visible)
                newElement.Select(Directions.Top, false);

            Recalculate = true;
            return newElement;
        }

        public InterfaceElement ReplaceElement(int index, InterfaceElement newElement)
        {
            if (index < 0 || index >= Elements.Count)
                return null;

            bool wasSelected = (_selectionIndex == index);

            if (wasSelected)
                Elements[index].Deselect(false);

            Elements[index] = newElement;

            if (wasSelected && newElement.Selectable && newElement.Visible)
                newElement.Select(Directions.Top, false);

            Recalculate = true;
            return newElement;
        }

        public override void CalculatePosition()
        {
            Recalculate = false;

            _width = 0;
            _height = 0;

            // calculate the width
            foreach (var element in Elements)
            {
                if (element.Hidden)
                    continue;

                if (element.Recalculate)
                    element.CalculatePosition();

                if (HorizontalMode)
                {
                    _width += element.Size.X + element.Margin.X * 2;
                    _height = MathHelper.Max(element.Size.Y + element.Margin.Y * 2, _height);
                }
                else
                {
                    _width = MathHelper.Max(element.Size.X + element.Margin.X * 2, _width);
                    _height += element.Size.Y + element.Margin.Y * 2;
                }
            }

            // set the size of the layout
            if (AutoSize)
            {
                Size.X = _width;
                Size.Y = _height;
                ChangeUp = true;
            }

            var centerX = Size.X / 2;
            var centerY = Size.Y / 2;

            var currentX = centerX - _width / 2;
            var currentY = centerY - _height / 2;

            // When scrolling vertically the content always starts at the top; the scroll
            // offset (applied at draw time) is what moves it, not the layout origin.
            if (Scrollable && !HorizontalMode)
                currentY = 0;

            // align content left/right
            if ((ContentAlignment & Gravities.Left) != 0)
                currentX = 0;
            else if ((ContentAlignment & Gravities.Right) != 0)
                currentX = Size.X - _width;

            // align content top/bottom
            if ((ContentAlignment & Gravities.Top) != 0)
                currentY = 0;
            else if ((ContentAlignment & Gravities.Bottom) != 0)
                currentY = Size.Y - _height;

            foreach (var element in Elements)
            {
                if (element.Hidden)
                    continue;

                Point elementPosition;

                if (HorizontalMode)
                {
                    currentX += element.Margin.X;
                    elementPosition = new Point(currentX, centerY - element.Size.Y / 2);
                    currentX += element.Size.X + element.Margin.X;
                }
                else
                {
                    currentY += element.Margin.Y;
                    elementPosition = new Point(centerX - element.Size.X / 2, currentY);
                    currentY += element.Size.Y + element.Margin.Y;
                }
                element.Position = elementPosition;
            }

            // Re-clamp the scroll now that heights/positions are fresh.
            if (Scrollable && !HorizontalMode)
                EnsureSelectionVisible();
        }

        // Adjusts the scroll target so the currently selected element is fully visible.
        private void EnsureSelectionVisible()
        {
            if (!Scrollable || HorizontalMode)
                return;
            if (_selectionIndex < 0 || _selectionIndex >= Elements.Count)
                return;

            var el = Elements[_selectionIndex];
            var top = el.Position.Y - el.Margin.Y;
            var bottom = el.Position.Y + el.Size.Y + el.Margin.Y;

            if (top < _scrollTarget)
                _scrollTarget = top;
            else if (bottom > _scrollTarget + ViewHeight)
                _scrollTarget = bottom - ViewHeight;

            var max = Math.Max(0, _height - ViewHeight);
            _scrollTarget = MathHelper.Clamp(_scrollTarget, 0, max);
        }

        // This layout's on-screen pixel bounds, used as the scissor (clip) rectangle.
        // Derived from the actual UI matrix, so it stays correct regardless of menu scale.
        private Rectangle ComputeScreenClip(Vector2 drawPosition, float scale)
        {
            var m = Game1.GetMatrix;
            var tl = Vector2.Transform(drawPosition - new Vector2(0, _clipPaddingTop * scale), m);
            var br = Vector2.Transform(drawPosition + new Vector2(Size.X, Size.Y + _clipPaddingBottom) * scale, m);

            var x = (int)Math.Floor(Math.Min(tl.X, br.X));
            var y = (int)Math.Floor(Math.Min(tl.Y, br.Y));
            var w = (int)Math.Ceiling(Math.Abs(br.X - tl.X));
            var h = (int)Math.Ceiling(Math.Abs(br.Y - tl.Y));

            return new Rectangle(x, y, w, h);
        }

        private void DrawScrollbar(SpriteBatch spriteBatch, Vector2 drawPosition, float scale, float transparency)
        {
            var max = Math.Max(0, _height - ViewHeight);
            if (max <= 0)
                return;

            const int barWidth = 4;
            const int edgePad = 5;

            // Match the scissor rect: the bar spans the padded region, not just Size.Y.
            var viewHeight = Size.Y + _clipPaddingTop + _clipPaddingBottom;
            var viewTop = drawPosition.Y - _clipPaddingTop * scale;
            var trackX = drawPosition.X + (Size.X - barWidth - edgePad) * scale;

            // Track
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                (int)trackX,
                (int)viewTop,
                (int)(barWidth * scale),
                (int)(viewHeight * scale)),
                ScrollbarTrackColor * (0.5f * transparency));

            // Thumb
            var ratio = viewHeight / (float)_height;
            var thumbHeight = Math.Min(Math.Max(viewHeight * ratio, 8f), viewHeight);
            var travel = Math.Max(0f, viewHeight - thumbHeight);
            var thumbTop = (_scrollOffset / max) * travel;

            spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                (int)trackX,
                (int)(viewTop + thumbTop * scale),
                (int)(barWidth * scale),
                (int)(thumbHeight * scale)),
                ScrollbarColor * transparency);
        }

        public void ToggleElementColors(bool disableSetting)
        {
            foreach (var element in this.Elements)
            {
                // The interface element is a button.
                if (element is InterfaceButton buttonElement)
                {
                    buttonElement.Color = disableSetting
                        ? buttonElement.Backup_Color
                        : buttonElement.Backup_Color_Disabled;

                    buttonElement.SelectionColor = disableSetting
                        ? buttonElement.Backup_SelectionColor
                        : buttonElement.Backup_SelectionColor_Disabled;
                }
                // The interface element is a toggle.
                if (element is InterfaceToggle toggleElement)
                {
                    toggleElement.ColorToggled = disableSetting
                        ? toggleElement.Backup_ColorToggled
                        : toggleElement.Backup_ColorToggled_Disabled;

                    toggleElement.ColorToggledBackground = disableSetting
                        ? toggleElement.Backup_ColorToggledBackground
                        : toggleElement.Backup_ColorToggledBackground_Disabled;
                }
            }
        }
    }
}
