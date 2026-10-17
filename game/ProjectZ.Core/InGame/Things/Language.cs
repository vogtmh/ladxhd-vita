using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectZ.InGame.Controls;

namespace ProjectZ.InGame.Things
{
    public class Language
    {
        private Dictionary<string, string>[] _languageStrings;

        public Dictionary<string, string> Strings => _languageStrings[CurrentLanguageIndex];
        public List<string> LanguageCode { get; private set; } = new List<string> { "eng" };

        public int CurrentLanguageIndex = 0;
        public int CurrentSubLanguageIndex = 0;
        public string CurrentLanguageCode = "eng";

        public void Load()
        {
            var baseFiles = GameFS.EnumerateFiles(
                Path.Combine(Values.PathDataFolder, "Languages"),
                recursive: true,
                acceptFile: name => name.EndsWith(".lng", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var modFiles = GameFS.EnumerateFiles(
                Values.PathLanguageMods,
                recursive: true,
                acceptFile: name => name.EndsWith(".lng", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var languageStrings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["eng"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (var file in baseFiles)
                LoadLanguageEntry(languageStrings, file);

            foreach (var file in modFiles)
                LoadLanguageEntry(languageStrings, file);

            LanguageCode = new List<string> { "eng" };
            LanguageCode.AddRange(languageStrings.Keys.Where(k => !k.Equals("eng", StringComparison.OrdinalIgnoreCase)));

            _languageStrings = LanguageCode.Select(k => languageStrings[k]).ToArray();
            CurrentLanguageIndex = Math.Clamp(CurrentLanguageIndex, 0, _languageStrings.Length - 1);
            CurrentLanguageCode = LanguageCode[CurrentLanguageIndex];
        }

        private void LoadLanguageEntry(Dictionary<string, Dictionary<string, string>> languageStrings, string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var split = fileName.Split('_');

            string lngName = "";

            if (split.Length == 1)
                lngName = split[0];
            else if (split.Length == 2)
                lngName = split[1];
            else
                return;

            if (!languageStrings.TryGetValue(lngName, out var dict) || dict == null)
            {
                dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                languageStrings[lngName] = dict;
            }

            if (split.Length == 1 || (split.Length == 2 && split[0].Equals("dialog", StringComparison.OrdinalIgnoreCase)) || (split.Length == 2 && split[0].Equals("achieve", StringComparison.OrdinalIgnoreCase)))
                LoadFile(dict, filePath);
        }

        public void LoadFile(Dictionary<string, string> dictionary, string fileName)
        {
            using var stream = GameFS.OpenRead(fileName);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var strLine = reader.ReadLine();
                if (string.IsNullOrEmpty(strLine))
                    continue;

                var spacePosition = strLine.IndexOf(' ');
                if (spacePosition < 0 || strLine.StartsWith("//"))
                    continue;

                var strKey = strLine.Substring(0, spacePosition);

                if (spacePosition + 1 >= strLine.Length)
                {
                    dictionary[strKey] = "";
                    continue;
                }

                var strValue = strLine.Substring(spacePosition + 1);
                dictionary[strKey] = strValue;
            }
        }

        public string GetString(string strKey, string defaultString, bool skipReplaceTag = false, bool forceEnglish = false)
        {
            // Without a key there's no string to get.
            if (strKey == null)
                return "null";

            // Try to get the string in the selected language.
            if (Strings.TryGetValue(strKey, out var value) && !forceEnglish)
            {
                if (!skipReplaceTag)
                    value = Game1.LanguageManager.ReplacePlaceholderTag(value);

                return value;
            }

            // Use the English text if a translated string is not found.
            if (_languageStrings[0].TryGetValue(strKey, out value))
            {
                if (!skipReplaceTag)
                    value = Game1.LanguageManager.ReplacePlaceholderTag(value);

                return value;
            }

            // Return the default string if it's not in the dictionary.
            if (!skipReplaceTag)
                defaultString = Game1.LanguageManager.ReplacePlaceholderTag(defaultString);

            return defaultString;
        }

        public void ToggleLanguage()
        {
            // Update the currently selected language.
            CurrentLanguageIndex = (CurrentLanguageIndex + 1) % _languageStrings.Length;
            CurrentLanguageCode = LanguageCode[CurrentLanguageIndex];
        }

        public string ReplacePlaceholderTag(string inputString)
        {
            bool swap = GameSettings.SwapButtons;
            string Confirm = ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, swap ? 1 : 0];
            string Cancel  = ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, swap ? 0 : 1];

            string Select = ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, 8];
            string Start  = ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, 9];
            string X = ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, 2];
            string Y = ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, 3];

            // Inserts the players name.
            inputString = inputString.Replace("[NAME]", Game1.GameManager.SaveName);

            // Inserts the trade icons.
            inputString = inputString.Replace("[TRADE0]", "¯");
            inputString = inputString.Replace("[TRADE1]", "¢");
            inputString = inputString.Replace("[TRADE2]", "£");
            inputString = inputString.Replace("[TRADE3]", "¤");
            inputString = inputString.Replace("[TRADE4]", "¥");
            inputString = inputString.Replace("[TRADE5]", "¦");
            inputString = inputString.Replace("[TRADE6]", "§");
            inputString = inputString.Replace("[TRADE7]", "¨");
            inputString = inputString.Replace("[TRADE8]", "©");
            inputString = inputString.Replace("[TRADE9]", "ª");
            inputString = inputString.Replace("[TRADE10]", "«");
            inputString = inputString.Replace("[TRADE11]", "¬");
            inputString = inputString.Replace("[TRADE12]", "­");
            inputString = inputString.Replace("[TRADE13]", "®");

            // Inserts special icons.
            inputString = inputString.Replace("[SKULL]", "µ");
            inputString = inputString.Replace("[MARIN]", "¶");
            inputString = inputString.Replace("[LINK]", "·");

            // Inserts controller icons.
            inputString = inputString.Replace("[LEFT]", "°");
            inputString = inputString.Replace("[RIGHT]", "±");
            inputString = inputString.Replace("[DOWN]", "²");
            inputString = inputString.Replace("[UP]", "³");
            inputString = inputString.Replace("[DPAD]", "´");
            inputString = inputString.Replace("[CONFIRM]", Confirm);
            inputString = inputString.Replace("[CANCEL]", Cancel);
            inputString = inputString.Replace("[START]", Start);
            inputString = inputString.Replace("[SELECT]", Select);
            inputString = inputString.Replace("[X]", X);
            inputString = inputString.Replace("[Y]", Y);
            return inputString;
        }
    }
}
