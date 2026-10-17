using System;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using ProjectZ.InGame.SaveLoad;

namespace ProjectZ.InGame.Things
{
    public static class ModFile
    {
        private static Dictionary<string, List<(string key, string value)>> _advancedCache;

        public static void LoadAdvancedCache()
        {
            // Store options and their values in a dictionary for fast lookup.
            _advancedCache = new Dictionary<string, List<(string, string)>>();

            // If "advanced" file doesn't exist then nothing to do.
            string advancedFile = SaveManager.GetAdvancedFile();
            if (!GameFS.Exists(advancedFile))
                return;

            // Dictionary entries are by class name.
            string currentClass = null;

            // Loop through the advanced file line by line.
            foreach (string line in GameFS.ReadAllLines(advancedFile))
            {
                // Trim out the class name the option is for.
                if (line.TrimStart().StartsWith("//: "))
                {
                    // Add the class to the dictionary if it hasn't already been added.
                    currentClass = line.TrimStart().Substring(4).Trim();
                    if (!_advancedCache.ContainsKey(currentClass))
                        _advancedCache[currentClass] = new List<(string, string)>();
                    continue;
                }
                // Skip if the class name is invalid.
                if (currentClass == null || string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                    continue;

                // Split off the option name and value.
                string[] splitLine = line.Split(new char[] { '=', '/' });
                if (splitLine.Length < 2)
                    continue;

                // Add the class, option, and value to the dictionary.
                _advancedCache[currentClass].Add((splitLine[0].Trim(), splitLine[1].Trim()));
            }
        }

        private static void ApplyCachedEntries(string className, dynamic inputClass, BindingFlags flags)
        {
            // If the cache is empty or the class is not in the dictionary.
            if (_advancedCache == null || !_advancedCache.TryGetValue(className, out var entries))
                return;

            // Loop through the keys (option names) in the class name.
            foreach (var (key, value) in entries)
            {
                // Apply the value to the corresponding field of the class if it exists.
                FieldInfo field = inputClass.GetType().GetField(key, flags);
                if (field == null) continue;
                try
                {
                    field.SetValue(inputClass, Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
                }
                catch { }
            }
        }

        private static void ApplyCachedEntriesStatic(string className, Type inputClass, BindingFlags flags)
        {
            // If the cache is empty or the class is not in the dictionary.
            if (_advancedCache == null || !_advancedCache.TryGetValue(className, out var entries))
                return;

            // Loop through the keys (static option names) in the class name.
            foreach (var (key, value) in entries)
            {
                // Apply the value to the corresponding static field of the class if it exists.
                FieldInfo field = inputClass.GetField(key, flags);
                if (field == null) continue;
                try
                {
                    field.SetValue(null, Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
                }
                catch { }
            }
        }

        public static void Parse(string modFile, dynamic inputClass)
        {
            // Set up flags for non-static fields. 
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            // Try load the value from the cache. If it doesn't work, try falling back to the old parse method.
            if (_advancedCache != null)
                ApplyCachedEntries(inputClass.GetType().Name, inputClass, flags);

            // Check if an LAHDMod of the file exists which overwrites advanced file.
            if (!GameFS.Exists(modFile))
                return;

            // Parse each line in the lahdmod and extract the key value pair.
            foreach (string line in GameFS.ReadAllLines(modFile))
            {
                // Skip comments.
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                // Split off the option name and value.
                string[] splitLine = line.Split(new char[] { '=', '/' });
                if (splitLine.Length < 2)
                    continue;

                // Store the name and value.
                string varName = splitLine[0].Trim();
                string varValue = splitLine[1].Trim();

                // Get the option we are trying to update.
                FieldInfo field = inputClass.GetType().GetField(varName, flags);

                // If it doesn't exist then skip it.
                if (field == null)
                    continue;

                // Try to update the option value with the new value.
                try
                {
                    object convertedValue = Convert.ChangeType(varValue, field.FieldType, CultureInfo.InvariantCulture);
                    field.SetValue(inputClass, convertedValue);
                }
                catch { }
            }
        }

        public static void ParseStatic(string modFile, Type inputClass)
        {
            // Set up flags for static fields.
            var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

            // Try load the value from the cache. If it doesn't work, try falling back to the old parse method.
            if (_advancedCache != null)
                ApplyCachedEntriesStatic(inputClass.Name, inputClass, flags);

            // Check if an LAHDMod of the file exists which overwrites advanced file.
            if (!GameFS.Exists(modFile))
                return;

            // Parse each line in the lahdmod and extract the key value pair.
            foreach (string line in GameFS.ReadAllLines(modFile))
            {
                // Skip comments.
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                // Split off the option name and value.
                string[] splitLine = line.Split(new char[] { '=', '/' });
                if (splitLine.Length < 2)
                    continue;

                // Store the name and value.
                string varName = splitLine[0].Trim();
                string varValue = splitLine[1].Trim();

                // Get the option we are trying to update.
                FieldInfo field = inputClass.GetField(varName, flags);

                // If it doesn't exist then skip it.
                if (field == null)
                    continue;

                // Try to update the option value with the new value.
                try
                {
                    object convertedValue = Convert.ChangeType(varValue, field.FieldType, CultureInfo.InvariantCulture);
                    field.SetValue(inputClass, convertedValue);
                }
                catch { }
            }
        }
    }
}
