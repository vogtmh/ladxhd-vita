using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Interface
{
    public static class PageTooltip
    {
        // Get the scale set by the menu.
        static float tooltip_scale => Game1.UiPageManager.MenuScale;

        // Sprite font texture.
        private static SpriteFont Font => Resources.GameFont;

        // Text padding.
        static float paddingX = 10f * tooltip_scale;
        static float paddingY = 10f * tooltip_scale;

        // Textbox scale: relative to menu size.
        static float widthScale  = 0.85f;

        // Textbox background.
        static Color backgroundColor = Color.Black;
        static float backgroundAlpha = 0.95f;

        // Textbox border.
        static Color borderColor = InterfaceElement.MainTextColor;
        static float borderAlpha = 0.95f;
        static int borderThickness = tooltip_scale == 1 ? 1 : (int)(2 * tooltip_scale);

        public static void Draw(SpriteBatch spriteBatch, string text)
        {
            // Update these as the scale may change.
            paddingX = 10f * tooltip_scale;
            paddingY = 10f * tooltip_scale;
            widthScale  = 0.85f;
            borderThickness = tooltip_scale == 1 ? 1 : (int)(2 * tooltip_scale);

            // Menu size reference.
            float menuWidth = (Values.MinWidth - 32) * tooltip_scale;
            float menuHeight = (Values.MinHeight - (Game1.PlatformPresentation.UseCompactMenus ? 16 : 32)) * tooltip_scale;
            // Menu top-left position.
            float menuX = (Game1.WindowWidth - menuWidth) / 2f;
            float menuY = (Game1.WindowHeight - menuHeight) / 2f;

            // Calculate the width of the tooltip.
            float boxWidth = menuWidth * widthScale;

            // Word-wrap text and apply padding.
            var wrappedLines = WrapText(text, boxWidth - paddingX * 2);
            float lineHeight = DrawHelper.LineSpacing * tooltip_scale;

            // Different scales make padding look different.
            float extraPadding = tooltip_scale == 1 ? paddingY : paddingY * 2;
            float reduceYPadding = 0;

            // If Chinese is selected, we need slightly more spacing between the lines.
            if (Game1.LanguageManager.CurrentLanguageCode == "chn")
            {
                lineHeight *= 1.25f;
                reduceYPadding = -(4 * tooltip_scale);
            }
            // Set the height of the text block.
            float textBlockHeight = wrappedLines.Count * lineHeight;

            // Dynamically scale the height of the tooltip.
            float boxHeight = textBlockHeight + paddingY + borderThickness * 2;
            float maxBoxHeight = menuHeight * 0.75f;
            boxHeight = MathHelper.Min(boxHeight, maxBoxHeight);

            // Tooltip centered within the menu.
            float boxX = menuX + (menuWidth - boxWidth) / 2f;
            float boxY = menuY + (menuHeight - boxHeight) / 2f;

            // Rectangle for black textbox.
            var boxRect = new Rectangle((int)boxX, (int)boxY, (int)boxWidth, (int)boxHeight);

            // Draw the border around the textbox.
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(boxRect.X - borderThickness, boxRect.Y - borderThickness, boxRect.Width + borderThickness * 2, borderThickness), borderColor * borderAlpha);
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(boxRect.X - borderThickness, boxRect.Bottom, boxRect.Width + borderThickness * 2, borderThickness), borderColor * borderAlpha);
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(boxRect.X - borderThickness, boxRect.Y, borderThickness, boxRect.Height), borderColor * borderAlpha);
            spriteBatch.Draw(Resources.SprWhite, new Rectangle(boxRect.Right, boxRect.Y, borderThickness, boxRect.Height), borderColor * borderAlpha);

            // Draw the black background.
            spriteBatch.Draw(Resources.SprWhite, boxRect, backgroundColor * backgroundAlpha);

            // Starting Y position vertically centered with top/bottom padding/
            float startY = boxRect.Y + paddingY + (boxRect.Height - paddingY * 2 - reduceYPadding - textBlockHeight) / 2f;

            foreach (var line in wrappedLines)
            {
                var lineSize = DrawHelper.MeasureString(line) * tooltip_scale;
                float lineX = boxRect.X + paddingX + (boxRect.Width - paddingX * 2 - lineSize.X) / 2f; // center horizontally with padding
                DrawHelper.DrawString(spriteBatch, line, new Vector2(lineX, startY), InterfaceElement.MainTextColor, 0f, Vector2.Zero, tooltip_scale, SpriteEffects.None, 0f);
                startY += lineHeight;
            }
        }

        private static List<string> WrapText(string text, float maxLineWidth)
        {
            // A list to hold the lines and a reference to the current line.
            List<string> lines = new List<string>();
            string currentLine = "";

            // Check if the current language is Chinese which splits on "characters".
            if (Game1.LanguageManager.CurrentLanguageCode == "chn")
            {
                // Wrap character by character for Chinese font.
                foreach (char c in text)
                {
                    // Test what the line would look like if added and measure it.
                    string testLine = currentLine + c;
                    float lineWidth = DrawHelper.MeasureString(testLine).X * tooltip_scale;

                    // If the line with the added character is too wide for the textbox
                    // add the line to the list and start a new line.
                    if (lineWidth > maxLineWidth)
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                            lines.Add(currentLine);

                        currentLine = c.ToString();
                    }
                    // If the character fits add it to the current line.
                    else
                    {
                        currentLine = testLine;
                    }
                }
            }
            // For all other languages we use the code below which splits on "spaces".
            else
            {
                // Split text into words.
                string[] words = text.Split(' ');

                // Loop through each word and decide if it fits in the current line.
                foreach (string word in words)
                {
                    // Test what the line would look like if added and measure it.
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    float lineWidth = DrawHelper.MeasureString(testLine).X * tooltip_scale;

                    // If the line with the added word is too wide for the textbox
                    // add the line to the list and start a new line.
                    if (lineWidth > maxLineWidth)
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                            lines.Add(currentLine);

                        currentLine = word;
                    }
                    // If the word fits add it to the current line.
                    else
                        currentLine = testLine;
                }
            }
            // Add remaining text to the final line.
            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            // Return the list of lines to display on the tooltip.
            return lines;
        }

        public static string GetTooltipIndex(InterfaceListLayout interfaceList, InterfaceListLayout contentLayout, List<string> tooltipList)
        {
            // Detect back button press by checking the index of the main InterfaceListLayout.
            if (interfaceList.SelectionIndex == 2)
                return Game1.LanguageManager.GetString("tooltip_default", "error");

            // Detect the chosen button by checking the content InterfaceListLayout.
            int index = contentLayout.SelectionIndex;
            string tooltip = "Select an option to view its tooltip.";

            // Get the tooltip that matches the index.
            for (int i = 0; i < tooltipList.Count; i++)
            {
                if (i == index)
                    tooltip = Game1.LanguageManager.GetString(tooltipList[i], "error");
            }
            // Display the tooltip in the tooltip window.
            return tooltip;
        }
    }
}
